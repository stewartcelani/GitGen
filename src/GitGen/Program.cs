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
    ///     Preprocesses command line arguments to handle @model syntax.
    ///     Exposed as internal for testing purposes.
    /// </summary>
    /// <param name="args">Raw command line arguments.</param>
    /// <returns>Processed arguments with @model converted to --model option.</returns>
    internal static string[] PreprocessArguments(string[] args)
    {
        string? modelNameFromAlias = null;
        var processedArgs = new List<string>();
        var pendingPromptParts = new List<string>();

        for (int i = 0; i < args.Length; i++)
        {
            var arg = args[i];

            // Check if this is an @model reference
            if (arg.StartsWith("@") && arg.Length > 1)
            {
                // Extract model name
                modelNameFromAlias = arg.Substring(1);
                continue; // Don't add to processed args
            }

            // Check if this is an option (starts with - or --)
            if (arg.StartsWith("-"))
            {
                processedArgs.Add(arg);

                // Check if this option takes a value
                if (i + 1 < args.Length && !args[i + 1].StartsWith("-") && !args[i + 1].StartsWith("@"))
                {
                    // This could be an option value, but we need to be careful
                    // For boolean options like -d, we shouldn't consume the next arg
                    // For now, just add the option and continue
                }
            }
            else
            {
                // This is part of the prompt
                pendingPromptParts.Add(arg);
            }
        }

        // Add all prompt parts
        processedArgs.AddRange(pendingPromptParts);

        // If a model was specified via @alias, add it as a proper option
        if (!string.IsNullOrEmpty(modelNameFromAlias))
        {
            processedArgs.Add("--model");
            processedArgs.Add(modelNameFromAlias);
        }

        return processedArgs.ToArray();
    }
    /// <summary>
    ///     Main entry point for the GitGen application.
    /// </summary>
    /// <param name="args">Command-line arguments.</param>
    /// <returns>Exit code (0 for success, non-zero for failure).</returns>
    private static async Task<int> Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Debug raw arguments
        if (args.Contains("-d") || args.Contains("--debug"))
        {
            Console.WriteLine($"[DEBUG] Raw args: [{string.Join(", ", args.Select(a => $"'{a}'"))}]");
        }

        // Preprocess arguments to handle @model syntax
        var processedArgs = PreprocessArguments(args);

        // Debug processed arguments
        if (args.Contains("-d") || args.Contains("--debug"))
        {
            Console.WriteLine($"[DEBUG] Processed args: [{string.Join(", ", processedArgs.Select(a => $"'{a}'"))}]");
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

        return await parser.InvokeAsync(processedArgs);
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
            .AddSingleton<IUsageTrackingService>(provider =>
                new UsageTrackingService(factory.CreateLogger<UsageTrackingService>()))
            .AddSingleton<ILlmCallTracker>(provider =>
                new LlmCallTracker(
                    factory.CreateLogger<LlmCallTracker>(),
                    provider.GetRequiredService<IUsageTrackingService>()))
            .AddSingleton<HttpClientService>(provider =>
                new HttpClientService(factory.CreateLogger<HttpClientService>()))
            .AddSingleton<IHttpClientService>(provider =>
                provider.GetRequiredService<HttpClientService>())
            .AddSingleton<ProviderFactory>(provider =>
                new ProviderFactory(provider, factory.CreateLogger<ProviderFactory>()))
            .AddSingleton<GitAnalysisService>(provider =>
                new GitAnalysisService(factory.CreateLogger<GitAnalysisService>()))
            .AddSingleton<CommitMessageGenerator>(provider =>
                new CommitMessageGenerator(
                    provider.GetRequiredService<ProviderFactory>(),
                    factory.CreateLogger<CommitMessageGenerator>()))
            .AddSingleton<GitDiffTruncationService>(provider =>
                new GitDiffTruncationService(factory.CreateLogger<GitDiffTruncationService>()))
            .AddSingleton<ConfigurationWizardService>(provider =>
                new ConfigurationWizardService(
                    factory.CreateLogger<ConfigurationWizardService>(),
                    provider.GetRequiredService<ProviderFactory>(),
                    provider.GetRequiredService<ConfigurationService>(),
                    provider.GetRequiredService<IConsoleInput>(),
                    provider.GetRequiredService<ISecureConfigurationService>()))
            .AddSingleton<ConfigurationMenuService>(provider =>
                new ConfigurationMenuService(
                    factory.CreateLogger<ConfigurationMenuService>(),
                    provider.GetRequiredService<ISecureConfigurationService>(),
                    provider.GetRequiredService<ConfigurationWizardService>(),
                    provider.GetRequiredService<ConfigurationService>(),
                    provider.GetRequiredService<ProviderFactory>()))
            .AddSingleton<IGenerationOrchestrator>(provider =>
                new GenerationOrchestrator(
                    factory.CreateLogger<GenerationOrchestrator>(),
                    provider.GetRequiredService<ISecureConfigurationService>(),
                    provider.GetRequiredService<ConfigurationService>(),
                    provider.GetRequiredService<ConfigurationWizardService>(),
                    provider.GetRequiredService<GitAnalysisService>(),
                    provider.GetRequiredService<CommitMessageGenerator>(),
                    provider.GetRequiredService<GitDiffTruncationService>(),
                    provider.GetRequiredService<IConsoleInput>()))
            .AddSingleton<IUsageReportingService>(provider =>
                new UsageReportingService(factory.CreateLogger<UsageReportingService>()))
            .AddSingleton<IConsoleInput, SystemConsoleInput>()
            .AddSingleton<IConsoleOutput, SystemConsoleOutput>()
            .AddSingleton<UsageMenuService>(provider =>
                new UsageMenuService(
                    factory.CreateLogger<UsageMenuService>(),
                    provider.GetRequiredService<IUsageReportingService>(),
                    provider.GetRequiredService<ISecureConfigurationService>(),
                    provider.GetRequiredService<IConsoleInput>(),
                    provider.GetRequiredService<IConsoleOutput>()))
            .BuildServiceProvider();
    }

    private static RootCommand BuildCommandLine(IServiceProvider serviceProvider)
    {
        // Define root command and its options - testing
        var debugOption = new Option<bool>("-d", "Enable debug logging.");
        debugOption.AddAlias("--debug");
        var versionShortOption = new Option<bool>("-v", "Show version information");
        var versionLongOption = new Option<bool>("--version", "Show version information");
        var previewOption = new Option<bool>("-p", "Preview mode - show what would happen without calling LLM.");
        previewOption.AddAlias("--preview");
        var rootCommand = new RootCommand("GitGen - AI-Powered Git Commit Message Generator")
        {
            debugOption,
            versionShortOption,
            versionLongOption,
            previewOption
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
            var preview = invocationContext.ParseResult.GetValueForOption(previewOption);

            // Read the model name from our hidden option and the prompt from the argument
            var modelName = invocationContext.ParseResult.GetValueForOption(modelOption);
            var promptParts = invocationContext.ParseResult.GetValueForArgument(inputArgument);
            var customInstruction = promptParts != null && promptParts.Any()
                ? string.Join(" ", promptParts) : null;

            // Enable debug mode first if requested
            ConsoleLogger.SetDebugMode(debug);

            // Debug what we received
            if (debug)
            {
                var logger = serviceProvider.GetRequiredService<IConsoleLogger>();
                logger.Debug($"Command line parsing results:");
                logger.Debug($"  Model from option: '{modelName ?? "(null)"}'");
                logger.Debug($"  Custom instruction: '{customInstruction ?? "(null)"}'");
                logger.Debug($"  Preview mode: {preview}");
            }

            if (showVersionShort || showVersionLong)
            {
                // Use compile-time version info (trimming-safe)
                var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
                Console.WriteLine($"GitGen v{version}");
                return;
            }

            // Use the orchestrator to handle the main workflow
            var orchestrator = serviceProvider.GetRequiredService<IGenerationOrchestrator>();
            invocationContext.ExitCode = await orchestrator.ExecuteAsync(modelName, customInstruction, preview);
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
            Console.WriteLine("  gitgen -p @fast              # Preview model selection and cost");
            Console.WriteLine("  gitgen \"focus on security\" @ultrathink");
            Console.WriteLine("  gitgen @sonnet \"explain the refactoring\"");
            Console.WriteLine();
            Console.WriteLine("💡 Tip: Configure a free model as @free to save money on public repositories");
            Console.WriteLine("   where sending code to free APIs doesn't matter.");
            Console.WriteLine();
            Console.WriteLine("⚠️  PowerShell Users: The @ symbol must be quoted or escaped:");
            Console.WriteLine("   gitgen \"@fast\"    # Use quotes");
            Console.WriteLine("   gitgen '@free'    # Single quotes work too");
            Console.WriteLine("   gitgen `@smart    # Or use backtick to escape");
            Console.WriteLine();
            Console.WriteLine("Options:");
            Console.WriteLine("  -d, --debug            Enable debug logging");
            Console.WriteLine("  -p, --preview          Preview mode - show what would happen without calling LLM");
            Console.WriteLine("  -v, --version          Show version information");
            Console.WriteLine("  -?, -h, --help         Show help and usage information");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  config                 Run the interactive configuration menu");
            Console.WriteLine("  usage                  View usage statistics (interactive menu)");
            Console.WriteLine("  help                   Display help information");
            Console.WriteLine();
        });

        // Define 'usage' command
        var usageCommand = new Command("usage", "Display usage statistics and cost analysis (interactive menu).");

        usageCommand.SetHandler(async invocationContext =>
        {
            ConsoleLogger.SetDebugMode(false);
            var menuService = serviceProvider.GetRequiredService<UsageMenuService>();
            await menuService.RunAsync();
            invocationContext.ExitCode = 0;
        });

        rootCommand.AddCommand(configCommand);
        rootCommand.AddCommand(helpCommand);
        rootCommand.AddCommand(usageCommand);
        return rootCommand;
    }

}