using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using GitGen.Configuration;
using GitGen.Exceptions;
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
        var serviceProvider = ConfigureServices();
        var rootCommand = BuildCommandLine(serviceProvider);

        // Use CommandLineBuilder to disable automatic version handling
        var parser = new CommandLineBuilder(rootCommand)
            .UseHelp()
            .UseEnvironmentVariableDirective()
            .UseParseDirective()
            .UseSuggestDirective()
            .RegisterWithDotnetSuggest()
            .UseTypoCorrections()
            .UseParseErrorReporting()
            .UseExceptionHandler()
            .CancelOnProcessTermination()
            .Build(); // Notice: no .UseVersionOption()

        return await parser.InvokeAsync(args);
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
            .AddSingleton<IEnvironmentPersistenceService>(provider =>
                new EnvironmentPersistenceService(factory.CreateLogger<EnvironmentPersistenceService>()))
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
                    provider.GetRequiredService<IEnvironmentPersistenceService>(),
                    provider.GetRequiredService<ConfigurationService>(),
                    provider.GetRequiredService<ISecureConfigurationService>()))
            .BuildServiceProvider();
    }

    private static RootCommand BuildCommandLine(IServiceProvider serviceProvider)
    {
        // Define root command and its options - testing
        var customInstructionOption =
            new Option<string?>("-p", "Custom prompt to focus guide/focus LLM when generating commit message.");
        customInstructionOption.AddAlias("--prompt");
        var debugOption = new Option<bool>("-d", "Enable debug logging.");
        debugOption.AddAlias("--debug");
        var versionShortOption = new Option<bool>("-v", "Show version information");
        var versionLongOption = new Option<bool>("--version", "Show version information");
        var rootCommand = new RootCommand("GitGen - AI-Powered Git Commit Message Generator")
        {
            customInstructionOption,
            debugOption,
            versionShortOption,
            versionLongOption
        };

        // Add catch-all argument to support @ syntax
        var inputArgument = new Argument<string[]>("input", "Input text with optional @model syntax")
        {
            Arity = ArgumentArity.ZeroOrMore
        };
        rootCommand.AddArgument(inputArgument);

        rootCommand.SetHandler(async invocationContext =>
        {
            var debug = invocationContext.ParseResult.GetValueForOption(debugOption);
            var customInstruction = invocationContext.ParseResult.GetValueForOption(customInstructionOption);
            var showVersionShort = invocationContext.ParseResult.GetValueForOption(versionShortOption);
            var showVersionLong = invocationContext.ParseResult.GetValueForOption(versionLongOption);
            var inputArgs = invocationContext.ParseResult.GetValueForArgument(inputArgument) ?? Array.Empty<string>();

            // Parse input arguments for @ syntax
            var (modelName, parsedPrompt) = ParseInputForModelAndPrompt(inputArgs);
            
            // If prompt was parsed from input, use it (unless -p option overrides)
            if (!string.IsNullOrEmpty(parsedPrompt) && string.IsNullOrEmpty(customInstruction))
            {
                customInstruction = parsedPrompt;
            }

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

            // Check for migration on first run
            var migrated = await secureConfig.MigrateFromEnvironmentVariablesAsync();
            if (migrated)
            {
                logger.Information("");
                logger.Warning($"{Constants.UI.WarningSymbol} Your environment variable configuration has been migrated to secure storage.");
                logger.Information("You can now remove GitGen environment variables if desired.");
                Console.WriteLine();
            }

            var configService = serviceProvider.GetRequiredService<ConfigurationService>();
            var config = await configService.LoadConfigurationAsync(modelName);

            if (config == null || !config.IsValid)
            {
                logger.Error($"{Constants.UI.CrossMark} {Constants.Messages.ConfigurationMissing}");
                logger.Information($"{Constants.UI.InfoSymbol} Starting configuration wizard...");
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

            // Get the active model for cost calculation
            var activeModel = await configService.GetActiveModelAsync();

            // Main generation logic
            await GenerateCommitMessage(serviceProvider, logger, config, customInstruction, activeModel);
        });

        // Define 'configure' command
        var configureCommand = new Command("configure", "Run the interactive configuration wizard.");

        configureCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var wizard = serviceProvider.GetRequiredService<ConfigurationWizardService>();
            var wizardConfig = await wizard.RunWizardAsync();
            invocationContext.ExitCode = wizardConfig != null ? 0 : 1;
        });

        // Define 'test' command
        var testCommand = new Command("test", "Send 'Testing.' to the LLM and print the response.");

        testCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();

            var configService = serviceProvider.GetRequiredService<ConfigurationService>();
            var config = await configService.LoadConfigurationAsync();

            if (config == null || !config.IsValid)
            {
                logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.ConfigurationInvalid}");
                invocationContext.ExitCode = 1;
                return;
            }

            var activeModel = await configService.GetActiveModelAsync();
            await TestLLMConnection(serviceProvider, logger, config, activeModel);
        });

        // Define 'info' command
        var infoCommand = new Command("info", "Display current configuration information.");

        infoCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();

            var configService = serviceProvider.GetRequiredService<ConfigurationService>();
            var config = await configService.LoadConfigurationAsync();

            if (config == null)
            {
                logger.Error($"{Constants.UI.CrossMark} No configuration found");
                logger.Information("💡 To configure GitGen, run: gitgen configure");
                invocationContext.ExitCode = 1;
                return;
            }

            DisplayConfigurationInfo(logger, config);
        });

        // Define 'model' command with subcommands
        var modelCommand = new Command("model", "Manage AI models");
        
        // Subcommand: model set <name>
        var modelSetCommand = new Command("set", "Set the default model");
        var modelNameArgument = new Argument<string>("model-name", "The name of the model");
        modelSetCommand.AddArgument(modelNameArgument);
        
        modelSetCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var modelName = invocationContext.ParseResult.GetValueForArgument(modelNameArgument);
            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();
            var secureConfig = serviceProvider.GetRequiredService<ISecureConfigurationService>();
            
            try
            {
                await secureConfig.SetDefaultModelAsync(modelName);
                logger.Success($"{Constants.UI.CheckMark} Default model changed to '{modelName}'");
                invocationContext.ExitCode = 0;
            }
            catch (Exception ex)
            {
                logger.Error($"{Constants.UI.CrossMark} {ex.Message}");
                invocationContext.ExitCode = 1;
            }
        });
        
        // Subcommand: model delete <name>
        var modelDeleteCommand = new Command("delete", "Delete a model");
        modelDeleteCommand.AddArgument(modelNameArgument);
        
        modelDeleteCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var modelName = invocationContext.ParseResult.GetValueForArgument(modelNameArgument);
            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();
            var secureConfig = serviceProvider.GetRequiredService<ISecureConfigurationService>();
            
            try
            {
                // Confirm deletion
                Console.Write($"Are you sure you want to delete model '{modelName}'? (y/N): ");
                var confirm = Console.ReadLine()?.Trim().ToLower();
                if (confirm != "y" && confirm != "yes")
                {
                    logger.Information("Deletion cancelled.");
                    return;
                }
                
                await secureConfig.DeleteModelAsync(modelName);
                logger.Success($"{Constants.UI.CheckMark} Model '{modelName}' deleted successfully");
                invocationContext.ExitCode = 0;
            }
            catch (Exception ex)
            {
                logger.Error($"{Constants.UI.CrossMark} {ex.Message}");
                invocationContext.ExitCode = 1;
            }
        });
        
        // Subcommand: model info <name>
        var modelInfoCommand = new Command("info", "Show detailed model information");
        modelInfoCommand.AddArgument(modelNameArgument);
        
        modelInfoCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var modelName = invocationContext.ParseResult.GetValueForArgument(modelNameArgument);
            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();
            var secureConfig = serviceProvider.GetRequiredService<ISecureConfigurationService>();
            
            var model = await secureConfig.GetModelAsync(modelName);
            if (model == null)
            {
                logger.Error($"{Constants.UI.CrossMark} Model '{modelName}' not found");
                invocationContext.ExitCode = 1;
                return;
            }
            
            DisplayModelInfo(logger, model);
            invocationContext.ExitCode = 0;
        });
        
        // Subcommand: model alias
        var modelAliasCommand = new Command("alias", "Manage model aliases");
        
        // Subcommand: model alias add <model> <alias>
        var aliasAddCommand = new Command("add", "Add an alias to a model");
        var aliasModelArgument = new Argument<string>("model", "The model name or ID");
        var aliasNameArgument = new Argument<string>("alias", "The alias to add");
        aliasAddCommand.AddArgument(aliasModelArgument);
        aliasAddCommand.AddArgument(aliasNameArgument);
        
        aliasAddCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var modelName = invocationContext.ParseResult.GetValueForArgument(aliasModelArgument);
            var alias = invocationContext.ParseResult.GetValueForArgument(aliasNameArgument);
            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();
            var secureConfig = serviceProvider.GetRequiredService<ISecureConfigurationService>();
            
            try
            {
                await secureConfig.AddAliasAsync(modelName, alias);
                invocationContext.ExitCode = 0;
            }
            catch (Exception ex)
            {
                logger.Error($"{Constants.UI.CrossMark} {ex.Message}");
                invocationContext.ExitCode = 1;
            }
        });
        
        // Subcommand: model alias remove <model> <alias>
        var aliasRemoveCommand = new Command("remove", "Remove an alias from a model");
        aliasRemoveCommand.AddArgument(aliasModelArgument);
        aliasRemoveCommand.AddArgument(aliasNameArgument);
        
        aliasRemoveCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var modelName = invocationContext.ParseResult.GetValueForArgument(aliasModelArgument);
            var alias = invocationContext.ParseResult.GetValueForArgument(aliasNameArgument);
            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();
            var secureConfig = serviceProvider.GetRequiredService<ISecureConfigurationService>();
            
            try
            {
                await secureConfig.RemoveAliasAsync(modelName, alias);
                invocationContext.ExitCode = 0;
            }
            catch (Exception ex)
            {
                logger.Error($"{Constants.UI.CrossMark} {ex.Message}");
                invocationContext.ExitCode = 1;
            }
        });
        
        // Subcommand: model alias list <model>
        var aliasListCommand = new Command("list", "List aliases for a model");
        aliasListCommand.AddArgument(aliasModelArgument);
        
        aliasListCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var modelName = invocationContext.ParseResult.GetValueForArgument(aliasModelArgument);
            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();
            var secureConfig = serviceProvider.GetRequiredService<ISecureConfigurationService>();
            
            try
            {
                var model = await secureConfig.GetModelAsync(modelName);
                if (model == null)
                {
                    logger.Error($"{Constants.UI.CrossMark} Model '{modelName}' not found");
                    invocationContext.ExitCode = 1;
                    return;
                }
                
                logger.Information($"{Constants.UI.ChartSymbol} Aliases for model '{model.Name}':");
                
                if (model.Aliases == null || model.Aliases.Count == 0)
                {
                    logger.Information("   No aliases configured");
                }
                else
                {
                    foreach (var alias in model.Aliases.OrderBy(a => a))
                    {
                        logger.Information($"   @{alias}");
                    }
                }
                
                invocationContext.ExitCode = 0;
            }
            catch (Exception ex)
            {
                logger.Error($"{Constants.UI.CrossMark} {ex.Message}");
                invocationContext.ExitCode = 1;
            }
        });
        
        modelAliasCommand.AddCommand(aliasAddCommand);
        modelAliasCommand.AddCommand(aliasRemoveCommand);
        modelAliasCommand.AddCommand(aliasListCommand);
        
        modelCommand.AddCommand(modelSetCommand);
        modelCommand.AddCommand(modelDeleteCommand);
        modelCommand.AddCommand(modelInfoCommand);
        modelCommand.AddCommand(modelAliasCommand);

        // Define 'reset' command
        var resetCommand = new Command("reset", "Reset all GitGen environment variables and configuration.");
        resetCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var wizard = serviceProvider.GetRequiredService<ConfigurationWizardService>();
            await wizard.ResetConfiguration();
            invocationContext.ExitCode = 0;
        });

        // Define 'settings' command with subcommands
        var settingsCommand = new Command("settings", "Quick settings management");

        // Subcommand: settings tokens
        var tokensCommand = new Command("tokens", "Change max output tokens");
        tokensCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var wizard = serviceProvider.GetRequiredService<ConfigurationWizardService>();
            var success = await wizard.QuickChangeMaxTokens();
            invocationContext.ExitCode = success ? 0 : 1;
        });

        // Subcommand: settings model
        var modelQuickCommand = new Command("model", "Change AI model");
        modelQuickCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var wizard = serviceProvider.GetRequiredService<ConfigurationWizardService>();
            var success = await wizard.QuickChangeModel();
            invocationContext.ExitCode = success ? 0 : 1;
        });

        settingsCommand.AddCommand(tokensCommand);
        settingsCommand.AddCommand(modelQuickCommand);

        // Define 'prompt' command
        var promptCommand = new Command("prompt", "Generate commit message with custom prompt instruction.");
        var promptArgument = new Argument<string[]>("prompt-text", "The custom prompt instruction to guide the LLM")
            { Arity = ArgumentArity.OneOrMore };
        promptCommand.AddArgument(promptArgument);

        promptCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var promptText = invocationContext.ParseResult.GetValueForArgument(promptArgument);
            var customInstruction = string.Join(" ", promptText);

            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();

            var configService = serviceProvider.GetRequiredService<ConfigurationService>();
            var config = await configService.LoadConfigurationAsync();

            if (config == null || !config.IsValid)
            {
                logger.Error($"{Constants.UI.CrossMark} {Constants.Messages.ConfigurationMissing}");
                logger.Information($"{Constants.UI.InfoSymbol} Starting configuration wizard...");
                Console.WriteLine();

                var wizard = serviceProvider.GetRequiredService<ConfigurationWizardService>();
                var wizardConfig = await wizard.RunWizardAsync();

                if (wizardConfig == null)
                {
                    logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.ConfigurationSetupFailed}");
                    invocationContext.ExitCode = 1;
                    return;
                }

                config = wizardConfig;
                logger.Success($"{Constants.UI.CheckMark} {Constants.Messages.ConfigurationSaved}");
                Console.WriteLine();
            }

            var activeModel = await configService.GetActiveModelAsync();
            await GenerateCommitMessage(serviceProvider, logger, config, customInstruction, activeModel);
        });

        // Define 'health' command
        var healthCommand = new Command("health", "Display configuration info and test LLM connection.");

        healthCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();

            var configService = serviceProvider.GetRequiredService<ConfigurationService>();
            var config = await configService.LoadConfigurationAsync();

            if (config == null)
            {
                logger.Error($"{Constants.UI.CrossMark} No configuration found");
                logger.Information("💡 To configure GitGen, run: gitgen configure");
                invocationContext.ExitCode = 1;
                return;
            }

            // First display configuration info
            DisplayConfigurationInfo(logger, config);

            // Add a separator between info and test
            Console.WriteLine();
            logger.Information("═════════════════════════════════════════════════════════════");
            Console.WriteLine();

            // Then test LLM connection if config is valid
            if (config.IsValid)
            {
                var activeModel = await configService.GetActiveModelAsync();
                await TestLLMConnection(serviceProvider, logger, config, activeModel);
            }
            else
            {
                logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.ConfigurationInvalid}");
                logger.Information("💡 To configure GitGen, run: gitgen configure");
                invocationContext.ExitCode = 1;
            }
        });

        // Define 'list' command
        var listCommand = new Command("list", "List all configured models");
        
        listCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();
            var secureConfig = serviceProvider.GetRequiredService<ISecureConfigurationService>();
            var settings = await secureConfig.LoadSettingsAsync();
            
            if (!settings.Models.Any())
            {
                logger.Information("No models configured. Run 'gitgen configure' to add a model.");
                return;
            }
            
            logger.Information($"{Constants.UI.ChartSymbol} Configured Models ({settings.Models.Count}):");
            Console.WriteLine();
            
            foreach (var model in settings.Models.OrderBy(m => m.Name))
            {
                var defaultMarker = model.Id == settings.DefaultModelId ? " ⭐" : "";
                var lastUsed = model.LastUsed.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
                
                logger.Information($"  {model.Name}{defaultMarker}");
                logger.Muted($"    Provider: {model.ProviderType} | Model: {model.ModelId}");
                
                if (!string.IsNullOrWhiteSpace(model.Note))
                    logger.Muted($"    Note: {model.Note}");
                
                if (model.Aliases != null && model.Aliases.Count > 0)
                {
                    var aliasesStr = string.Join(", ", model.Aliases.OrderBy(a => a).Select(a => $"@{a}"));
                    logger.Muted($"    Aliases: {aliasesStr}");
                }
                    
                if (model.Pricing != null)
                {
                    var pricingInfo = CostCalculationService.FormatPricingInfo(model.Pricing);
                    logger.Muted($"    Pricing: {pricingInfo}");
                }
                
                logger.Muted($"    Last used: {lastUsed}");
                Console.WriteLine();
            }
        });

        // Define 'help' command
        var helpCommand = new Command("help", "Display help information.");

        helpCommand.SetHandler(invocationContext =>
        {
            // Display the root command help
            Console.WriteLine(rootCommand.Description);
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine("  gitgen [options]");
            Console.WriteLine("  gitgen [command]");
            Console.WriteLine("  gitgen @<model> [prompt]     Use specific model via alias");
            Console.WriteLine("  gitgen [prompt] @<model>     Alternative alias syntax");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine(
                "  -p, --prompt <prompt>  Custom prompt to focus guide/focus LLM when generating commit message.");
            Console.WriteLine("  -d, --debug            Enable debug logging.");
            Console.WriteLine("  -v, --version          Show version information");
            Console.WriteLine("  -?, -h, --help         Show help and usage information");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  configure              Run the interactive configuration wizard.");
            Console.WriteLine("  test                   Send 'Testing.' to the LLM and print the response.");
            Console.WriteLine("  info                   Display current configuration information.");
            Console.WriteLine("  list                   List all configured models");
            Console.WriteLine("  model                  Manage AI models (set, delete, info, alias)");
            Console.WriteLine("  reset                  Reset all GitGen environment variables and configuration.");
            Console.WriteLine("  settings               Quick settings management");
            Console.WriteLine("  prompt <prompt-text>   Generate commit message with custom prompt instruction.");
            Console.WriteLine("  health                 Display configuration info and test LLM connection.");
            Console.WriteLine("  help                   Display help information.");
            Console.WriteLine();
        });

        rootCommand.AddCommand(configureCommand);
        rootCommand.AddCommand(testCommand);
        rootCommand.AddCommand(infoCommand);
        rootCommand.AddCommand(listCommand);
        rootCommand.AddCommand(modelCommand);
        rootCommand.AddCommand(resetCommand);
        rootCommand.AddCommand(settingsCommand);
        rootCommand.AddCommand(promptCommand);
        rootCommand.AddCommand(healthCommand);
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

            await ClipboardService.SetTextAsync(result.Message);
            logger.Information($"{Constants.UI.ClipboardSymbol} {Constants.Messages.CommitMessageCopied}");
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

    private static async Task TestLLMConnection(IServiceProvider sp, IConsoleLogger logger, GitGenConfiguration config, ModelConfiguration? activeModel = null)
    {
        try
        {
            logger.Information($"{Constants.UI.TestTubeSymbol} {Constants.Messages.TestingConnection}");

            var providerFactory = sp.GetRequiredService<ProviderFactory>();
            var provider = providerFactory.CreateProvider(config, activeModel);

            logger.Information(
                $"{Constants.UI.LinkSymbol} Using {provider.ProviderName} provider via {config.BaseUrl} ({config.Model ?? Constants.Fallbacks.UnknownModelName})");

            var result = await provider.GenerateAsync(Constants.Api.TestLlmPrompt);

            Console.WriteLine();
            logger.Success($"{Constants.UI.CheckMark} LLM Response:");

            // Display response in teal color for visibility
            logger.Highlight($"{Constants.UI.CommitMessageQuotes}{result.Message}{Constants.UI.CommitMessageQuotes}",
                ConsoleColor.DarkCyan);

            Console.WriteLine();
            logger.Success($"{Constants.UI.PartySymbol} Test completed successfully!");
        }
        catch (Exception ex)
        {
            logger.Error(ex, "Failed to test LLM connection");
            logger.Error($"{Constants.UI.CrossMark} Test failed: {{Message}}", ex.Message);
        }
    }

    private static void DisplayConfigurationInfo(IConsoleLogger logger, GitGenConfiguration config)
    {
        logger.Information("📋 Current GitGen Configuration:");
        Console.WriteLine();

        // Configuration status
        var statusEmoji = config.IsValid ? "✅" : "❌";
        var statusText = config.IsValid ? "Valid" : "Invalid";
        logger.Information($"{statusEmoji} Configuration Status: {statusText}");
        Console.WriteLine();

        // Display configuration values
        logger.Information("🔧 Configuration Values:");
        logger.Information($"   Provider Type:     {config.ProviderType ?? "(not set)"}");
        logger.Information($"   Base URL:          {config.BaseUrl ?? "(not set)"}");
        logger.Information($"   Model:             {config.Model ?? "(not set)"}");

        // Mask API key for security
        var maskedApiKey = string.IsNullOrEmpty(config.ApiKey)
            ? "(not set)"
            : $"{config.ApiKey[..Math.Min(8, config.ApiKey.Length)]}..." +
              new string('*', Math.Max(0, config.ApiKey.Length - 8));
        logger.Information($"   API Key:           {maskedApiKey}");

        logger.Information($"   Requires Auth:     {config.RequiresAuth}");
        logger.Information($"   Legacy Max Tokens: {config.OpenAiUseLegacyMaxTokens}");
        logger.Information($"   Temperature:       {config.Temperature}");

        Console.WriteLine();

        // Display environment variable information
        logger.Information("🌍 Environment Variables:");
        DisplayEnvVar(logger, "GITGEN_PROVIDERTYPE", config.ProviderType);
        DisplayEnvVar(logger, "GITGEN_BASEURL", config.BaseUrl);
        DisplayEnvVar(logger, "GITGEN_MODEL", config.Model);
        DisplayEnvVar(logger, "GITGEN_APIKEY", config.ApiKey, true);
        DisplayEnvVar(logger, "GITGEN_REQUIRESAUTH", config.RequiresAuth.ToString());
        DisplayEnvVar(logger, "GITGEN_OPENAI_USE_LEGACY_MAX_TOKENS", config.OpenAiUseLegacyMaxTokens.ToString());
        DisplayEnvVar(logger, "GITGEN_TEMPERATURE", config.Temperature.ToString());

        Console.WriteLine();

        if (!config.IsValid) logger.Information("💡 To configure GitGen, run: gitgen configure");
    }

    private static void DisplayEnvVar(IConsoleLogger logger, string varName, string? value, bool maskValue = false)
    {
        if (string.IsNullOrEmpty(value))
        {
            logger.Information($"   {varName}: (not set)");
        }
        else
        {
            var displayValue = maskValue
                ? $"{value[..Math.Min(8, value.Length)]}..." + new string('*', Math.Max(0, value.Length - 8))
                : value;
            logger.Information($"   {varName}: {displayValue}");
        }
    }

    private static void DisplayModelInfo(IConsoleLogger logger, ModelConfiguration model)
    {
        logger.Information($"{Constants.UI.ChartSymbol} Model: {model.Name}");
        logger.Information($"   Provider: {model.ProviderType}");
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
            logger.Muted($"   Pricing updated: {model.Pricing.UpdatedAt.ToLocalTime():yyyy-MM-dd}");
        }
        
        logger.Information($"   Is Default: {model.IsDefault}");
        logger.Muted($"   Created: {model.CreatedAt.ToLocalTime():yyyy-MM-dd}");
        logger.Muted($"   Last used: {model.LastUsed.ToLocalTime():yyyy-MM-dd HH:mm}");
    }

    /// <summary>
    ///     Parses input arguments to extract @model syntax and custom prompt.
    /// </summary>
    /// <param name="args">The input arguments from command line.</param>
    /// <returns>Tuple of (modelName, prompt) extracted from the arguments.</returns>
    private static (string? modelName, string? prompt) ParseInputForModelAndPrompt(string[] args)
    {
        if (args == null || args.Length == 0)
            return (null, null);
        
        string? modelName = null;
        var promptParts = new List<string>();
        
        foreach (var arg in args)
        {
            if (arg.StartsWith("@") && arg.Length > 1)
            {
                // Extract model name (everything after @)
                modelName = arg.Substring(1);
            }
            else
            {
                // Everything else is part of the prompt
                promptParts.Add(arg);
            }
        }
        
        var prompt = promptParts.Count > 0 ? string.Join(" ", promptParts) : null;
        return (modelName, prompt);
    }
}