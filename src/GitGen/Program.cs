using System.CommandLine;
using System.CommandLine.Builder;
using System.CommandLine.Parsing;
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
            .AddSingleton<ConfigurationService>(provider =>
                new ConfigurationService(factory.CreateLogger<ConfigurationService>()))
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
                    provider.GetRequiredService<ConfigurationService>()))
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

        rootCommand.SetHandler(async invocationContext =>
        {
            var debug = invocationContext.ParseResult.GetValueForOption(debugOption);
            var customInstruction = invocationContext.ParseResult.GetValueForOption(customInstructionOption);
            var showVersionShort = invocationContext.ParseResult.GetValueForOption(versionShortOption);
            var showVersionLong = invocationContext.ParseResult.GetValueForOption(versionLongOption);

            if (showVersionShort || showVersionLong)
            {
                // Use compile-time version info (trimming-safe)
                var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
                Console.WriteLine($"GitGen v{version}");
                return;
            }

            ConsoleLogger.SetDebugMode(debug);

            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();

            var configService = serviceProvider.GetRequiredService<ConfigurationService>();
            var config = configService.LoadConfiguration();

            if (!config.IsValid)
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

            // Main generation logic
            await GenerateCommitMessage(serviceProvider, logger, config, customInstruction);
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
            var config = configService.LoadConfiguration();

            if (!config.IsValid)
            {
                logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.ConfigurationInvalid}");
                invocationContext.ExitCode = 1;
                return;
            }

            await TestLLMConnection(serviceProvider, logger, config);
        });

        // Define 'info' command
        var infoCommand = new Command("info", "Display current configuration information.");

        infoCommand.SetHandler(invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();

            var configService = serviceProvider.GetRequiredService<ConfigurationService>();
            var config = configService.LoadConfiguration();

            DisplayConfigurationInfo(logger, config);
        });

        // Define 'model' command
        var modelCommand = new Command("model", "Change the AI model for the current provider configuration.");
        var modelArgument = new Argument<string>("model-name", "The name of the model to switch to");
        modelCommand.AddArgument(modelArgument);

        modelCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var modelName = invocationContext.ParseResult.GetValueForArgument(modelArgument);

            var wizard = serviceProvider.GetRequiredService<ConfigurationWizardService>();
            var success = await wizard.ChangeModelAsync(modelName);
            invocationContext.ExitCode = success ? 0 : 1;
        });

        // Define 'reset' command
        var resetCommand = new Command("reset", "Reset all GitGen environment variables and configuration.");
        resetCommand.SetHandler(invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var wizard = serviceProvider.GetRequiredService<ConfigurationWizardService>();
            wizard.ResetConfiguration();
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
            var config = configService.LoadConfiguration();

            if (!config.IsValid)
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

            await GenerateCommitMessage(serviceProvider, logger, config, customInstruction);
        });

        // Define 'health' command
        var healthCommand = new Command("health", "Display configuration info and test LLM connection.");

        healthCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();

            var configService = serviceProvider.GetRequiredService<ConfigurationService>();
            var config = configService.LoadConfiguration();

            // First display configuration info
            DisplayConfigurationInfo(logger, config);

            // Add a separator between info and test
            Console.WriteLine();
            logger.Information("═════════════════════════════════════════════════════════════");
            Console.WriteLine();

            // Then test LLM connection if config is valid
            if (config.IsValid)
            {
                await TestLLMConnection(serviceProvider, logger, config);
            }
            else
            {
                logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.ConfigurationInvalid}");
                logger.Information("💡 To configure GitGen, run: gitgen configure");
                invocationContext.ExitCode = 1;
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
            Console.WriteLine("  model <model-name>     Change the AI model for the current provider configuration.");
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
        string? instruction)
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
            var result = await generator.GenerateAsync(config, diff, instruction);

            logger.Success($"{Constants.UI.CheckMark} Generated Commit Message:");

            // Display commit message in teal color for visibility
            logger.Highlight($"{Constants.UI.CommitMessageQuotes}{result.Message}{Constants.UI.CommitMessageQuotes}",
                ConsoleColor.DarkCyan);

            Console.WriteLine();

            // Display token usage information if available
            if (result.InputTokens.HasValue && result.OutputTokens.HasValue)
                logger.Muted(
                    $"Generated with {result.InputTokens:N0} input tokens, {result.OutputTokens:N0} output tokens ({result.TotalTokens:N0} total) • {result.Message.Length} characters");
            else
                logger.Muted($"Generated with {result.Message.Length} characters");

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

    private static async Task TestLLMConnection(IServiceProvider sp, IConsoleLogger logger, GitGenConfiguration config)
    {
        try
        {
            logger.Information($"{Constants.UI.TestTubeSymbol} {Constants.Messages.TestingConnection}");

            var providerFactory = sp.GetRequiredService<ProviderFactory>();
            var provider = providerFactory.CreateProvider(config);

            logger.Information(
                $"{Constants.UI.LinkSymbol} Using {provider.ProviderName} provider via {config.BaseUrl} ({config.Model ?? Constants.Fallbacks.UnknownModelName})");

            var result = await provider.GenerateAsync(Constants.Api.TestLlmPrompt);

            Console.WriteLine();
            logger.Success($"{Constants.UI.CheckMark} LLM Response:");

            // Display response in teal color for visibility
            logger.Highlight($"{Constants.UI.CommitMessageQuotes}{result.Message}{Constants.UI.CommitMessageQuotes}",
                ConsoleColor.DarkCyan);

            Console.WriteLine();

            // Display token usage information if available
            if (result.InputTokens.HasValue && result.OutputTokens.HasValue)
                logger.Muted(
                    $"Generated with {result.InputTokens:N0} input tokens, {result.OutputTokens:N0} output tokens ({result.TotalTokens:N0} total) • {result.Message.Length} characters");
            else
                logger.Muted($"Generated with {result.Message.Length} characters");

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
}