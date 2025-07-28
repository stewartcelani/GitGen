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
        
        // Preprocess arguments to handle @model syntax
        var processedArgs = PreprocessArguments(args);
        
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
                    provider.GetRequiredService<GitDiffTruncationService>()))
            .AddSingleton<IUsageReportingService>(provider =>
                new UsageReportingService(factory.CreateLogger<UsageReportingService>()))
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

            if (showVersionShort || showVersionLong)
            {
                // Use compile-time version info (trimming-safe)
                var version = Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "Unknown";
                Console.WriteLine($"GitGen v{version}");
                return;
            }

            ConsoleLogger.SetDebugMode(debug);

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
            Console.WriteLine("Options:");
            Console.WriteLine("  -d, --debug            Enable debug logging");
            Console.WriteLine("  -p, --preview          Preview mode - show what would happen without calling LLM");
            Console.WriteLine("  -v, --version          Show version information");
            Console.WriteLine("  -?, -h, --help         Show help and usage information");
            Console.WriteLine();
            Console.WriteLine("Commands:");
            Console.WriteLine("  config                 Run the interactive configuration menu");
            Console.WriteLine("  usage                  Display usage statistics and cost analysis");
            Console.WriteLine("  help                   Display help information");
            Console.WriteLine();
        });

        // Define 'usage' command
        var usageCommand = new Command("usage", "Display usage statistics and cost analysis.");
        
        // Add subcommands for different report types
        var dailyCommand = new Command("daily", "Show daily usage report (default)");
        var monthlyCommand = new Command("monthly", "Show monthly usage report");
        
        // Add options
        var sinceOption = new Option<DateTime?>("--since", "Start date for custom range (YYYY-MM-DD)");
        var untilOption = new Option<DateTime?>("--until", "End date for custom range (YYYY-MM-DD)");
        var modelFilterOption = new Option<string?>("--model", "Filter by model name");
        var jsonOption = new Option<bool>("--json", "Output in JSON format");
        var costOption = new Option<bool>("--cost", "Focus on cost breakdown");
        
        usageCommand.AddOption(sinceOption);
        usageCommand.AddOption(untilOption);
        usageCommand.AddOption(modelFilterOption);
        usageCommand.AddOption(jsonOption);
        usageCommand.AddOption(costOption);
        
        // Default usage command handler (daily report)
        usageCommand.SetHandler(async (invocationContext) =>
        {
            var reportingService = serviceProvider.GetRequiredService<IUsageReportingService>();
            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();
            
            var since = invocationContext.ParseResult.GetValueForOption(sinceOption);
            var until = invocationContext.ParseResult.GetValueForOption(untilOption);
            var model = invocationContext.ParseResult.GetValueForOption(modelFilterOption);
            var json = invocationContext.ParseResult.GetValueForOption(jsonOption);
            
            try
            {
                string report;
                
                if (since.HasValue || until.HasValue)
                {
                    // Custom date range
                    var startDate = since ?? DateTime.Today.AddDays(-30);
                    var endDate = until ?? DateTime.Today;
                    report = await reportingService.GenerateCustomReportAsync(startDate, endDate, model, json);
                }
                else
                {
                    // Default to daily report
                    report = await reportingService.GenerateDailyReportAsync();
                }
                
                Console.WriteLine(report);
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to generate usage report: {ex.Message}");
                invocationContext.ExitCode = 1;
            }
        });
        
        // Daily subcommand handler
        dailyCommand.SetHandler(async (invocationContext) =>
        {
            var reportingService = serviceProvider.GetRequiredService<IUsageReportingService>();
            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();
            
            try
            {
                var report = await reportingService.GenerateDailyReportAsync();
                Console.WriteLine(report);
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to generate daily report: {ex.Message}");
                invocationContext.ExitCode = 1;
            }
        });
        
        // Monthly subcommand handler
        monthlyCommand.SetHandler(async (invocationContext) =>
        {
            var reportingService = serviceProvider.GetRequiredService<IUsageReportingService>();
            var logger = serviceProvider.GetRequiredService<IConsoleLogger>();
            
            try
            {
                var report = await reportingService.GenerateMonthlyReportAsync();
                Console.WriteLine(report);
            }
            catch (Exception ex)
            {
                logger.Error($"Failed to generate monthly report: {ex.Message}");
                invocationContext.ExitCode = 1;
            }
        });
        
        usageCommand.AddCommand(dailyCommand);
        usageCommand.AddCommand(monthlyCommand);
        
        rootCommand.AddCommand(configCommand);
        rootCommand.AddCommand(helpCommand);
        rootCommand.AddCommand(usageCommand);
        return rootCommand;
    }

}