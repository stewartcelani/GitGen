using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.CommandLine.Invocation;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using GitGen.Configuration;
using GitGen.Exceptions;
using GitGen.Helpers;
using GitGen.Providers;
using GitGen.Services;
using Microsoft.Extensions.DependencyInjection;
using TextCopy;

namespace GitGen;

/// <summary>
///     Main entry point for the GitGen application.
///     Handles command-line parsing, dependency injection setup, and application orchestration.
/// </summary>
internal class Program
{
    /// <summary>
    ///     Main entry point for the GitGen application.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code (0 for success, non-zero for failure).</returns>
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        
        // Pre-parse arguments to handle @model syntax
        string? modelNameFromAlias = null;
        var processedArgs = new List<string>(args.Length);

        foreach (var arg in args)
        {
            // An alias is only detected if the entire argument begins with '@'.
            // This correctly ignores '@' symbols inside a quoted prompt.
            if (arg.StartsWith("@") && arg.Length > 1)
            {
                // Capture the model name (e.g., "fast" from "@fast").
                // If multiple @aliases are used, the last one wins.
                modelNameFromAlias = arg.Substring(1);
            }
            else
            {
                // This is part of the prompt.
                processedArgs.Add(arg);
            }
        }

        // If an @-alias was found, inject it back into the argument list
        // as a standard, hidden option that System.CommandLine can parse safely.
        if (!string.IsNullOrEmpty(modelNameFromAlias))
        {
            processedArgs.Insert(0, modelNameFromAlias);
            processedArgs.Insert(0, "--model");
        }
        
        var serviceProvider = ConfigureServices();
        var rootCommand = BuildCommandLine(serviceProvider);

        // Use CommandLineBuilder for command processing
        var parser = new CommandLineBuilder(rootCommand)
            .UseHelp()
            .UseParseDirective()
            .UseSuggestDirective()
            .RegisterWithDotnetSuggest()
            .UseTypoCorrections()
            .UseParseErrorReporting()
            .UseExceptionHandler((exception, context) =>
            {
                // Handle all exceptions with a user-friendly error message
                Console.Error.WriteLine($"Error: {exception.Message}");
                context.ExitCode = 1;
            })
            .CancelOnProcessTermination()
            .Build();

        return await parser.InvokeAsync(processedArgs.ToArray());
    }

    private static ServiceProvider ConfigureServices()
    {
        var factory = new ConsoleLoggerFactory();
        return new ServiceCollection()
            .AddSingleton<ConsoleLoggerFactory>(factory)
            .AddSingleton(factory.CreateLogger<Program>())
            .AddSingleton<ISecureConfigurationService>(provider =>
                new SecureConfigurationService(factory.CreateLogger<SecureConfigurationService>()))
            .AddSingleton<ConfigurationService>(provider =>
                new ConfigurationService(
                    factory.CreateLogger<ConfigurationService>(),
                    provider.GetRequiredService<ISecureConfigurationService>()))
            .AddSingleton<ILlmCallTracker>(provider =>
                new LlmCallTracker(factory.CreateLogger<LlmCallTracker>()))
            .AddSingleton<HttpClientService>(provider =>
                new HttpClientService(factory.CreateLogger<HttpClientService>()))
            .AddSingleton<ProviderFactory>(provider =>
                new ProviderFactory(provider, factory.CreateLogger<ProviderFactory>()))
            .AddSingleton<GitAnalysisService>(provider =>
                new GitAnalysisService(factory.CreateLogger<GitAnalysisService>()))
            .AddSingleton<CommitMessageGenerator>(provider =>
                new CommitMessageGenerator(
                    provider.GetRequiredService<ProviderFactory>(),
                    factory.CreateLogger<CommitMessageGenerator>()))
            .AddSingleton<ConfigurationWizardService>(provider =>
                new ConfigurationWizardService(
                    factory.CreateLogger<ConfigurationWizardService>(),
                    provider.GetRequiredService<ProviderFactory>(),
                    provider.GetRequiredService<ConfigurationService>(),
                    provider.GetRequiredService<ISecureConfigurationService>()))
            .AddSingleton<ConfigurationMenuService>(provider =>
                new ConfigurationMenuService(
                    factory.CreateLogger<ConfigurationMenuService>(),
                    provider.GetRequiredService<ISecureConfigurationService>(),
                    provider.GetRequiredService<ConfigurationWizardService>(),
                    provider.GetRequiredService<ConfigurationService>(),
                    provider.GetRequiredService<ProviderFactory>()))
            .BuildServiceProvider();
    }

    private static RootCommand BuildCommandLine(IServiceProvider serviceProvider)
    {
        // Define root command and its options - testing
        var debugOption = new Option<bool>("-d", "Enable debug logging.");
        debugOption.AddAlias("--debug");
        var versionShortOption = new Option<bool>("-v", "Show version information");
        var versionLongOption = new Option<bool>("--version", "Show version information");
        var rootCommand = new RootCommand("GitGen - AI-Powered Git Commit Message Generator")
        {
            debugOption,
            versionShortOption,
            versionLongOption
        };

        // Add hidden option to carry the model name from @alias syntax
        var modelOption = new Option<string>("--model", "The AI model alias to use for generation")
        {
            IsHidden = true
        };
        rootCommand.AddOption(modelOption);

        // Add catch-all argument to support @ syntax
        var inputArgument = new Argument<string[]>("input", "Input text with optional @model syntax")
        {
            Arity = ArgumentArity.ZeroOrMore
        };
        rootCommand.AddArgument(inputArgument);

        rootCommand.SetHandler(async invocationContext =>
        {
            var debug = invocationContext.ParseResult.GetValueForOption(debugOption);
            var showVersionShort = invocationContext.ParseResult.GetValueForOption(versionShortOption);
            var showVersionLong = invocationContext.ParseResult.GetValueForOption(versionLongOption);
            
            // Read the model name from our hidden option and the prompt from the argument
            var modelName = invocationContext.ParseResult.GetValueForOption(modelOption);
            var promptParts = invocationContext.ParseResult.GetValueForArgument(inputArgument);
            var customInstruction = promptParts != null && promptParts.Any() 
                ? string.Join(" ", promptParts) : null;

            if (showVersionShort || showVersionLong)
            {
                // Use compile-time version info (trimming-safe)
                var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
                Console.WriteLine($"GitGen v{version}");
                return;
            }

            ConsoleLogger.SetDebugMode(debug);

            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();
            var secureConfig = serviceProvider.GetRequiredService<ISecureConfigurationService>();
            var configService = serviceProvider.GetRequiredService<ConfigurationService>();
            var config = await configService.LoadConfigurationAsync(modelName);

            // If a specific model was requested but not found, show available models
            if (config == null && !string.IsNullOrEmpty(modelName))
            {
                await DisplayModelSuggestions(serviceProvider, logger, modelName);
                invocationContext.ExitCode = 1;
                return;
            }

            if (config == null || !config.IsValid)
            {
                // Check if we have models but just need to fix the default
                if (secureConfig != null && await configService.NeedsDefaultModelHealingAsync())
                {
                    logger.Debug("Default model configuration needs healing");
                    
                    // Attempt to heal the default model configuration
                    var healed = await secureConfig.HealDefaultModelAsync(logger);
                    if (healed)
                    {
                        // Try loading configuration again after healing
                        config = await configService.LoadConfigurationAsync(modelName);
                        if (config != null && config.IsValid)
                        {
                            Console.WriteLine();
                            // Successfully healed, continue with generation
                        }
                        else
                        {
                            logger.Error($"{Constants.UI.CrossMark} Failed to load configuration after healing");
                            invocationContext.ExitCode = 1;
                            return;
                        }
                    }
                    else
                    {
                        logger.Error($"{Constants.UI.CrossMark} Failed to heal default model configuration");
                        invocationContext.ExitCode = 1;
                        return;
                    }
                }
                else
                {
                    // No models exist or no secure config, run the wizard
                    logger.Error($"{Constants.UI.CrossMark} {Constants.Messages.ConfigurationMissing}");
                    logger.Information($"{Constants.UI.GearSymbol} Starting configuration wizard...");
                    Console.WriteLine();

                    var wizard = serviceProvider.GetRequiredService<ConfigurationWizardService>();
                    var wizardConfig = await wizard.RunWizardAsync();

                    if (wizardConfig == null)
                    {
                        logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.ConfigurationSetupFailed}");
                        invocationContext.ExitCode = 1;
                        return;
                    }

                    // Use the configuration returned from the wizard directly
                    config = wizardConfig;

                    logger.Success($"{Constants.UI.CheckMark} {Constants.Messages.ConfigurationSaved}");
                    Console.WriteLine();
                }
            }

            // Get the active model for cost calculation
            var activeModel = await configService.GetActiveModelAsync();

            // Main generation logic
            await GenerateCommitMessage(serviceProvider, logger, config, customInstruction, activeModel);
        });

        // Define 'config' command
        var configCommand = new Command("config", "Run the interactive configuration menu.");

        configCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var menuService = serviceProvider.GetRequiredService<ConfigurationMenuService>();
            await menuService.RunAsync();
            invocationContext.ExitCode = 0;
        });


        // Define 'help' command
        var helpCommand = new Command("help", "Display help information.");

        helpCommand.SetHandler(invocationContext =>
        {
            // Display the root command help
            Console.WriteLine(rootCommand.Description);
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  gitgen                       Generate commit message with default model");
            Console.WriteLine("  gitgen [prompt]              Generate with custom prompt");
            Console.WriteLine("  gitgen @<model>              Generate with specific model or alias");
            Console.WriteLine("  gitgen [prompt] @<model>     Generate with custom prompt and model");
            Console.WriteLine("  gitgen @<model> [prompt]     Alternative syntax");
            Console.WriteLine("  gitgen [command] [options]   Run a specific command");
            Console.WriteLine();
            Console.WriteLine("Examples:");
            Console.WriteLine("  gitgen");
            Console.WriteLine("  gitgen \"must be a haiku\"");
            Console.WriteLine("  gitgen @fast                 # Use your fast model");
            Console.WriteLine("  gitgen @free                 # Use free model for public repos");
            Console.WriteLine("  gitgen \"focus on security\" @ultrathink");
            Console.WriteLine("  gitgen @sonnet \"explain the refactoring\"");
            Console.WriteLine();
            Console.WriteLine("💡 Tip: Configure a free model as @free to save money on public repositories");
            Console.WriteLine("   where sending code to free APIs doesn't matter.");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -d, --debug            Enable debug logging");
            Console.WriteLine("  -v, --version          Show version information");
            Console.WriteLine("  -?, -h, --help         Show help and usage information");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  config                 Run the interactive configuration menu");
            Console.WriteLine("  help                   Display help information");
            Console.WriteLine();
        });

        rootCommand.AddCommand(configCommand);
        rootCommand.AddCommand(helpCommand);
        return rootCommand;
    }

    private static async Task GenerateCommitMessage(IServiceProvider sp, IConsoleLogger logger,
        GitGenConfiguration config,
        string? instruction,
        ModelConfiguration? activeModel = null)
    {
        try
        {
            var gitService = sp.GetRequiredService<GitAnalysisService>();
            if (!gitService.IsGitRepository())
            {
                logger.Error($"{Constants.UI.CrossMark} {Constants.Messages.NoGitRepository}");
                return;
            }

            var diff = gitService.GetRepositoryDiff();
            if (string.IsNullOrWhiteSpace(diff))
            {
                logger.Information($"{Constants.UI.InfoSymbol} {Constants.Messages.NoUncommittedChanges}");
                return;
            }

            logger.Information($"{Constants.UI.LoadingSymbol} {Constants.Messages.GeneratingCommitMessage}");
            Console.WriteLine();

            var generator = sp.GetRequiredService<CommitMessageGenerator>();
            var result = await generator.GenerateAsync(config, diff, instruction, activeModel);

            logger.Success($"{Constants.UI.CheckMark} Generated Commit Message:");

            // Display commit message in teal color for visibility
            logger.Highlight($"{Constants.UI.CommitMessageQuotes}{result.Message}{Constants.UI.CommitMessageQuotes}",
                ConsoleColor.DarkCyan);

            Console.WriteLine();

            // Get app settings
            var secureConfig = sp.GetRequiredService<ISecureConfigurationService>();
            var settings = await secureConfig.LoadSettingsAsync();

            // Display token usage if enabled
            if (settings.Settings.ShowTokenUsage && result.InputTokens.HasValue && result.OutputTokens.HasValue)
            {
                var tokenInfo = $"Generated with {result.InputTokens:N0} input tokens, {result.OutputTokens:N0} output tokens ({result.TotalTokens:N0} total)";
                
                // Add cost if pricing is configured
                if (activeModel?.Pricing != null)
                {
                    var cost = CostCalculationService.CalculateAndFormatCost(
                        activeModel, 
                        result.InputTokens.Value, 
                        result.OutputTokens.Value);
                    if (!string.IsNullOrEmpty(cost))
                    {
                        tokenInfo += $" • Estimated cost: {cost}";
                    }
                }
                
                logger.Muted(tokenInfo);
                Console.WriteLine();
            }

            // Copy to clipboard if enabled
            if (settings.Settings.CopyToClipboard)
            {
                await ClipboardService.SetTextAsync(result.Message);
                logger.Information($"{Constants.UI.ClipboardSymbol} {Constants.Messages.CommitMessageCopied}");
            }
        }
        catch (AuthenticationException ex)
        {
            logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.AuthenticationFailed}");
            logger.Information("");
            logger.Information($"{Constants.UI.BulbSymbol} {Constants.ErrorMessages.AuthenticationFailedGuidance}");
            logger.Information($"   {Constants.ErrorMessages.AuthenticationFailedDetail}");
            logger.Error(ex, "Authentication error details");
        }
        catch (Exception ex)
        {
            logger.Error(ex, Constants.ErrorMessages.GenerationFailed);
            logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.UnexpectedError}", ex.Message);
        }
    }

    private static async Task<bool> TestLLMConnection(IServiceProvider sp, IConsoleLogger logger, GitGenConfiguration config, ModelConfiguration? activeModel = null, string indent = "")
    {
        try
        {
            logger.Information($"{indent}{Constants.UI.TestTubeSymbol} {Constants.Messages.TestingConnection}");

            var providerFactory = sp.GetRequiredService<ProviderFactory>();
            var provider = providerFactory.CreateProvider(config, activeModel);

            logger.Information(
                $"{indent}{Constants.UI.LinkSymbol} Using {provider.ProviderName} provider via {config.BaseUrl} ({config.Model ?? Constants.Fallbacks.UnknownModelName})");

            var result = await provider.GenerateAsync(Constants.Api.TestLlmPrompt);

            logger.Success($"{indent}{Constants.UI.CheckMark} LLM Response: \"{result.Message}\"");
            return true;
        }
        catch (Exception ex)
        {
            logger.Error($"{indent}{Constants.UI.CrossMark} Test failed: {ex.Message}");
            return false;
        }
    }


    private static void DisplayModelInfo(IConsoleLogger logger, ModelConfiguration model)
    {
        logger.Information($"{Constants.UI.ChartSymbol} Model: {model.Name}");
        logger.Information($"   Type: {model.Type}");
        logger.Information($"   Provider: {model.Provider}");
        logger.Information($"   URL: {model.Url}");
        logger.Information($"   Model ID: {model.ModelId}");
        
        // Mask API key for security
        var maskedApiKey = string.IsNullOrEmpty(model.ApiKey)
            ? "(not set)"
            : $"{model.ApiKey[..Math.Min(8, model.ApiKey.Length)]}..." +
              new string('*', Math.Max(0, model.ApiKey.Length - 8));
        logger.Information($"   API Key: {maskedApiKey}");
        
        logger.Information($"   Requires Auth: {model.RequiresAuth}");
        logger.Information($"   Legacy Max Tokens: {model.UseLegacyMaxTokens}");
        logger.Information($"   Temperature: {model.Temperature}");
        logger.Information($"   Max Output Tokens: {model.MaxOutputTokens}");
        
        if (!string.IsNullOrWhiteSpace(model.Note))
            logger.Information($"   Note: {model.Note}");
        
        if (model.Aliases != null && model.Aliases.Count > 0)
        {
            var aliasesStr = string.Join(", ", model.Aliases.OrderBy(a => a).Select(a => $"@{a}"));
            logger.Information($"   Aliases: {aliasesStr}");
        }
        
        if (!string.IsNullOrWhiteSpace(model.SystemPrompt))
        {
            var truncatedPrompt = model.SystemPrompt.Length > 50 
                ? model.SystemPrompt.Substring(0, 50) + "..." 
                : model.SystemPrompt;
            logger.Information($"   System Prompt: {truncatedPrompt}");
        }
        
        if (model.Pricing != null)
        {
            var pricingInfo = CostCalculationService.FormatPricingInfo(model.Pricing);
            logger.Information($"   Pricing: {pricingInfo}");
            logger.Muted($"   Pricing updated: {DateTimeHelper.ToLocalDateString(model.Pricing.UpdatedAt)}");
        }
        
        logger.Muted($"   Created: {DateTimeHelper.ToLocalDateString(model.CreatedAt)}");
        logger.Muted($"   Last used: {DateTimeHelper.ToLocalDateTimeString(model.LastUsed)}");
    }


    /// <summary>
    ///     Displays available models when an alias is not found.
    /// </summary>
    private static async Task DisplayModelSuggestions(IServiceProvider serviceProvider, IConsoleLogger logger, string requestedAlias)
    {
        logger.Error($"{Constants.UI.CrossMark} Model or alias '{requestedAlias}' not found");
        Console.WriteLine();
        
        var secureConfig = serviceProvider.GetRequiredService<ISecureConfigurationService>();
        var settings = await secureConfig.LoadSettingsAsync();
        
        if (!settings.Models.Any())
        {
            logger.Information("No models configured. Run 'gitgen config' to add a model.");
            return;
        }
        
        // Check if partial matching is enabled and filter suggestions
        var modelsToShow = settings.Models;
        if (settings.Settings.EnablePartialAliasMatching && 
            requestedAlias.Length >= settings.Settings.MinimumAliasMatchLength)
        {
            var partialMatches = await secureConfig.GetModelsByPartialMatchAsync(requestedAlias);
            if (partialMatches.Any())
            {
                modelsToShow = partialMatches;
                logger.Information($"{Constants.UI.BulbSymbol} Did you mean one of these models matching '{requestedAlias}'?");
            }
            else
            {
                logger.Information($"{Constants.UI.BulbSymbol} No models match '{requestedAlias}'. Here are all available models:");
            }
        }
        else
        {
            logger.Information($"{Constants.UI.BulbSymbol} Did you mean one of these?");
        }
        
        Console.WriteLine();
        
        // Display filtered models with their details
        foreach (var model in modelsToShow.OrderBy(m => m.Name))
        {
            var defaultMarker = model.Id == settings.DefaultModelId ? " ⭐ (default)" : "";
            logger.Success($"  {model.Name}{defaultMarker}");
            
            // Show aliases if any
            if (model.Aliases != null && model.Aliases.Count > 0)
            {
                var aliasesStr = string.Join(", ", model.Aliases.OrderBy(a => a).Select(a => $"@{a}"));
                logger.Information($"    Aliases: {aliasesStr}");
            }
            
            // Show model details
            logger.Muted($"    Type: {model.Type} | Provider: {model.Provider} | Model: {model.ModelId}");
            logger.Muted($"    URL: {model.Url}");
            
            // Show pricing if available
            if (model.Pricing != null)
            {
                var pricingInfo = CostCalculationService.FormatPricingInfo(model.Pricing);
                logger.Muted($"    Pricing: {pricingInfo}");
            }
            
            Console.WriteLine();
        }
        
        logger.Information($"{Constants.UI.InfoSymbol} Usage examples:");
        logger.Information("  gitgen @modelname");
        logger.Information("  gitgen \"your prompt\" @modelname");
        logger.Information("  gitgen @modelname \"your prompt\"");
    }
}