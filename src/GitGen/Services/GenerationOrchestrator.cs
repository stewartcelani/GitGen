using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using GitGen.Configuration;
using GitGen.Exceptions;
using GitGen.Helpers;
using TextCopy;

namespace GitGen.Services;

/// <summary>
///     Orchestrates the main commit message generation workflow.
/// </summary>
public class GenerationOrchestrator : IGenerationOrchestrator
{
    private readonly IConsoleLogger _logger;
    private readonly ISecureConfigurationService _secureConfig;
    private readonly ConfigurationService _configService;
    private readonly ConfigurationWizardService _wizardService;
    private readonly GitAnalysisService _gitService;
    private readonly CommitMessageGenerator _generator;
    private readonly GitDiffTruncationService _truncationService;

    public GenerationOrchestrator(
        IConsoleLogger logger,
        ISecureConfigurationService secureConfig,
        ConfigurationService configService,
        ConfigurationWizardService wizardService,
        GitAnalysisService gitService,
        CommitMessageGenerator generator,
        GitDiffTruncationService truncationService)
    {
        _logger = logger;
        _secureConfig = secureConfig;
        _configService = configService;
        _wizardService = wizardService;
        _gitService = gitService;
        _generator = generator;
        _truncationService = truncationService;
    }

    /// <inheritdoc />
    public async Task<int> ExecuteAsync(string? modelName, string? customInstruction, bool isPreviewMode)
    {
        try
        {
            // Track if a specific model was requested
            bool specificModelRequested = !string.IsNullOrEmpty(modelName);
            
            // Load configuration
            var activeModel = await _configService.LoadConfigurationAsync(modelName);

            // If a specific model was requested but not found, show available models and EXIT
            if (activeModel == null && specificModelRequested)
            {
                _logger.Debug($"Specific model '{modelName}' was requested but not found");
                await DisplayModelSuggestionsAsync(modelName!);
                return 1;  // EXIT HERE - do not continue to default model or healing logic
            }

            // Only proceed with healing/wizard if NO specific model was requested
            if (activeModel == null)
            {
                // Check if we have models but just need to fix the default
                // IMPORTANT: Only heal if NO specific model was requested
                if (!specificModelRequested && _secureConfig != null && await _configService.NeedsDefaultModelHealingAsync())
                {
                    _logger.Debug("Default model configuration needs healing");
                    
                    // Attempt to heal the default model configuration
                    var healed = await _secureConfig.HealDefaultModelAsync(_logger);
                    if (healed)
                    {
                        // Try loading configuration again after healing
                        // Since no specific model was requested, load the default
                        activeModel = await _configService.LoadConfigurationAsync(null);
                        if (activeModel != null)
                        {
                            Console.WriteLine();
                            // Successfully healed, continue with generation
                        }
                        else
                        {
                            _logger.Error($"{Constants.UI.CrossMark} Failed to load configuration after healing");
                            return 1;
                        }
                    }
                    else
                    {
                        _logger.Error($"{Constants.UI.CrossMark} Failed to heal default model configuration");
                        return 1;
                    }
                }
                else
                {
                    // No models exist or no secure config, run the wizard
                    _logger.Error($"{Constants.UI.CrossMark} {Constants.Messages.ConfigurationMissing}");
                    _logger.Information($"{Constants.UI.GearSymbol} Starting configuration wizard...");
                    Console.WriteLine();

                    var wizardConfig = await _wizardService.RunWizardAsync();

                    if (wizardConfig == null)
                    {
                        _logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.ConfigurationSetupFailed}");
                        return 1;
                    }

                    // Use the configuration returned from the wizard directly
                    activeModel = wizardConfig;

                    _logger.Success($"{Constants.UI.CheckMark} {Constants.Messages.ConfigurationSaved}");
                    Console.WriteLine();
                }
            }

            // Main generation logic - check for preview mode
            if (isPreviewMode)
            {
                await ShowPreviewInfoAsync(activeModel!, customInstruction);
            }
            else
            {
                await GenerateCommitMessageAsync(activeModel!, customInstruction);
            }

            return 0;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Orchestration failed");
            _logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.UnexpectedError}", ex.Message);
            return 1;
        }
    }

    private Task ShowPreviewInfoAsync(ModelConfiguration activeModel, string? customInstruction)
    {
        try
        {
            if (!_gitService.IsGitRepository())
            {
                _logger.Error($"{Constants.UI.CrossMark} {Constants.Messages.NoGitRepository}");
                return Task.CompletedTask;
            }

            var diff = _gitService.GetRepositoryDiff();
            if (string.IsNullOrWhiteSpace(diff))
            {
                _logger.Information($"{Constants.UI.InfoSymbol} {Constants.Messages.NoUncommittedChanges}");
                return Task.CompletedTask;
            }

            // Show preview header
            _logger.Information("[PREVIEW MODE - No LLM call will be made]");
            Console.WriteLine();

            // Show model info
            _logger.Information($"{Constants.UI.LinkSymbol} Would use: {activeModel.Name} ({activeModel.ModelId} via {activeModel.Provider})");

            // Calculate and show diff stats
            var diffLines = diff.Split('\n').Length;
            var diffChars = diff.Length;
            _logger.Information($"{Constants.UI.BulbSymbol} Git diff: {diffLines:N0} lines, {diffChars:N0} characters");

            // Estimate tokens
            var systemPromptSize = EstimateSystemPromptSize(activeModel, customInstruction);
            var diffTokens = EstimateTokens(diff);
            var totalTokens = systemPromptSize + diffTokens;

            _logger.Information($"{Constants.UI.ChartSymbol} Estimated tokens:");
            _logger.Information($"   • System prompt: ~{systemPromptSize:N0} tokens");
            _logger.Information($"   • Git diff: ~{diffTokens:N0} tokens");
            _logger.Information($"   • Total input: ~{totalTokens:N0} tokens");
            
            // Estimate output tokens as midpoint of max output tokens
            var maxOutputTokens = activeModel.MaxOutputTokens;
            var estimatedOutputTokens = maxOutputTokens / 2;
            _logger.Information($"   • Estimated output: ~{estimatedOutputTokens:N0} tokens (midpoint of {maxOutputTokens:N0} max)");

            // Show estimated cost if pricing available
            if (activeModel.Pricing != null)
            {
                var inputCost = (totalTokens / 1_000_000.0) * (double)activeModel.Pricing.InputPer1M;
                var outputCost = (estimatedOutputTokens / 1_000_000.0) * (double)activeModel.Pricing.OutputPer1M;
                var totalCost = inputCost + outputCost;
                
                // Use CostCalculationService to format with proper currency
                var inputCostStr = CostCalculationService.FormatCurrency((decimal)inputCost, activeModel.Pricing.CurrencyCode, 4);
                var outputCostStr = CostCalculationService.FormatCurrency((decimal)outputCost, activeModel.Pricing.CurrencyCode, 4);
                var totalCostStr = CostCalculationService.FormatCurrency((decimal)totalCost, activeModel.Pricing.CurrencyCode, 4);
                
                _logger.Information($"💰 Estimated cost:");
                _logger.Information($"   • Input: ~{inputCostStr}");
                _logger.Information($"   • Output: ~{outputCostStr}");
                _logger.Information($"   • Total: ~{totalCostStr}");
            }

            // Show custom instruction if provided
            if (!string.IsNullOrWhiteSpace(customInstruction))
            {
                Console.WriteLine();
                _logger.Information($"{Constants.UI.InfoSymbol} Custom instruction: \"{customInstruction}\"");
            }

            // Show how to run for real
            Console.WriteLine();
            _logger.Muted("To generate actual commit message, run without -p flag");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Preview failed");
            _logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.UnexpectedError}", ex.Message);
        }
        
        return Task.CompletedTask;
    }

    private async Task ShowPreviewForConfirmation(ModelConfiguration activeModel, string diff, string? customInstruction)
    {
        // Show model info
        _logger.Information($"{Constants.UI.LinkSymbol} Model: {activeModel.Name} ({activeModel.ModelId} via {activeModel.Provider})");

        // Calculate and show diff stats
        var diffLines = diff.Split('\n').Length;
        var diffChars = diff.Length;
        _logger.Information($"{Constants.UI.BulbSymbol} Git diff: {diffLines:N0} lines, {diffChars:N0} characters");

        // Estimate tokens
        var systemPromptSize = EstimateSystemPromptSize(activeModel, customInstruction);
        var diffTokens = EstimateTokens(diff);
        var totalTokens = systemPromptSize + diffTokens;

        _logger.Information($"{Constants.UI.ChartSymbol} Estimated tokens:");
        _logger.Information($"   • System prompt: ~{systemPromptSize:N0} tokens");
        _logger.Information($"   • Git diff: ~{diffTokens:N0} tokens");
        _logger.Information($"   • Total input: ~{totalTokens:N0} tokens");
        
        // Estimate output tokens as midpoint of max output tokens
        var maxOutputTokens = activeModel.MaxOutputTokens;
        var estimatedOutputTokens = maxOutputTokens / 2;
        _logger.Information($"   • Estimated output: ~{estimatedOutputTokens:N0} tokens (midpoint of {maxOutputTokens:N0} max)");

        // Show estimated cost if pricing available
        if (activeModel.Pricing != null)
        {
            var inputCost = (totalTokens / 1_000_000.0) * (double)activeModel.Pricing.InputPer1M;
            var outputCost = (estimatedOutputTokens / 1_000_000.0) * (double)activeModel.Pricing.OutputPer1M;
            var totalCost = inputCost + outputCost;
            
            // Use CostCalculationService to format with proper currency
            var inputCostStr = CostCalculationService.FormatCurrency((decimal)inputCost, activeModel.Pricing.CurrencyCode, 4);
            var outputCostStr = CostCalculationService.FormatCurrency((decimal)outputCost, activeModel.Pricing.CurrencyCode, 4);
            var totalCostStr = CostCalculationService.FormatCurrency((decimal)totalCost, activeModel.Pricing.CurrencyCode, 4);
            
            _logger.Information($"💰 Estimated cost:");
            _logger.Information($"   • Input: ~{inputCostStr}");
            _logger.Information($"   • Output: ~{outputCostStr}");
            _logger.Information($"   • Total: ~{totalCostStr}");
        }

        // Show custom instruction if provided
        if (!string.IsNullOrWhiteSpace(customInstruction))
        {
            Console.WriteLine();
            _logger.Information($"{Constants.UI.InfoSymbol} Custom instruction: \"{customInstruction}\"");
        }
    }

    private async Task GenerateCommitMessageAsync(ModelConfiguration activeModel, string? instruction)
    {
        string diff = "";
        
        try
        {
            if (!_gitService.IsGitRepository())
            {
                _logger.Error($"{Constants.UI.CrossMark} {Constants.Messages.NoGitRepository}");
                return;
            }

            diff = _gitService.GetRepositoryDiff();
            if (string.IsNullOrWhiteSpace(diff))
            {
                _logger.Information($"{Constants.UI.InfoSymbol} {Constants.Messages.NoUncommittedChanges}");
                return;
            }

            // Get app settings to check if confirmation is required
            var settings = await _secureConfig.LoadSettingsAsync();
            
            // Show preview information if confirmation is required
            if (settings.Settings.RequirePromptConfirmation)
            {
                await ShowPreviewForConfirmation(activeModel, diff, instruction);
                
                Console.WriteLine();
                Console.Write("Send to LLM? (y/N): ");
                var confirm = Console.ReadLine()?.Trim().ToLower();
                
                if (confirm != "y" && confirm != "yes")
                {
                    _logger.Information("Generation cancelled.");
                    return;
                }
                
                Console.WriteLine();
            }

            _logger.Information($"{Constants.UI.LoadingSymbol} {Constants.Messages.GeneratingCommitMessage}");
            Console.WriteLine();

            var result = await _generator.GenerateAsync(activeModel, diff, instruction);

            _logger.Success($"{Constants.UI.CheckMark} Generated Commit Message:");

            // Display commit message in teal color for visibility
            _logger.Highlight($"{Constants.UI.CommitMessageQuotes}{result.Message}{Constants.UI.CommitMessageQuotes}",
                ConsoleColor.DarkCyan);

            Console.WriteLine();

            // Display token usage if enabled
            if (settings.Settings.ShowTokenUsage && result.InputTokens.HasValue && result.OutputTokens.HasValue)
            {
                var tokenInfo = $"Generated with {result.InputTokens:N0} input tokens, {result.OutputTokens:N0} output tokens ({result.TotalTokens:N0} total)";
                
                // Add cost if pricing is configured
                if (activeModel.Pricing != null)
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
                
                _logger.Muted(tokenInfo);
                Console.WriteLine();
            }

            // Copy to clipboard if enabled
            if (settings.Settings.CopyToClipboard)
            {
                await ClipboardService.SetTextAsync(result.Message);
                _logger.Information($"{Constants.UI.ClipboardSymbol} {Constants.Messages.CommitMessageCopied}");
            }
        }
        catch (ContextLengthExceededException ex)
        {
            _logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.ContextLengthExceeded}");
            Console.WriteLine();
            
            // Display detailed error information
            if (ex.MaxContextLength.HasValue)
            {
                _logger.Information($"This model's maximum context length is {ex.MaxContextLength:N0} tokens");
            }
            
            if (ex.RequestedTokens.HasValue)
            {
                var tokenLabel = ex.PromptTokens.HasValue ? "" : "~";
                _logger.Information($"Your request used {tokenLabel}{ex.RequestedTokens:N0} tokens:");
                
                if (ex.PromptTokens.HasValue && ex.CompletionTokens.HasValue)
                {
                    _logger.Information($"   • Messages: {ex.PromptTokens:N0} tokens");
                    _logger.Information($"   • Completion: {ex.CompletionTokens:N0} tokens");
                }
                else
                {
                    // Estimate breakdown if not provided
                    var systemPromptSize = EstimateSystemPromptSize(activeModel, instruction);
                    var diffTokens = EstimateTokens(diff);
                    _logger.Information($"   • System prompt: ~{systemPromptSize:N0} tokens");
                    _logger.Information($"   • Git diff: ~{diffTokens:N0} tokens");
                }
            }
            else
            {
                // Fallback to our own estimation if API didn't provide token counts
                var systemPromptSize = EstimateSystemPromptSize(activeModel, instruction);
                var diffTokens = EstimateTokens(diff);
                var totalTokens = systemPromptSize + diffTokens;
                
                _logger.Information($"Estimated tokens in your request: ~{totalTokens:N0}");
                _logger.Information($"   • System prompt: ~{systemPromptSize:N0} tokens");
                _logger.Information($"   • Git diff: ~{diffTokens:N0} tokens");
            }
            
            Console.WriteLine();
            Console.Write(Constants.ErrorMessages.ContextLengthRetryPrompt);
            
            var confirm = Console.ReadLine()?.Trim().ToLower();
            if (confirm == "y" || confirm == "yes")
            {
                await GenerateWithTruncatedDiffAsync(activeModel, diff, instruction);
            }
            else
            {
                _logger.Information("Generation cancelled.");
            }
        }
        catch (AuthenticationException ex)
        {
            _logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.AuthenticationFailed}");
            _logger.Information("");
            _logger.Information($"{Constants.UI.BulbSymbol} {Constants.ErrorMessages.AuthenticationFailedGuidance}");
            _logger.Information($"   {Constants.ErrorMessages.AuthenticationFailedDetail}");
            _logger.Error(ex, "Authentication error details");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, Constants.ErrorMessages.GenerationFailed);
            _logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.UnexpectedError}", ex.Message);
        }
    }

    /// <summary>
    ///     Displays available models when an alias is not found.
    /// </summary>
    private async Task DisplayModelSuggestionsAsync(string requestedAlias)
    {
        _logger.Error($"{Constants.UI.CrossMark} Model or alias '{requestedAlias}' not found");
        Console.WriteLine();
        
        var settings = await _secureConfig.LoadSettingsAsync();
        
        if (!settings.Models.Any())
        {
            _logger.Information("No models configured. Run 'gitgen config' to add a model.");
            return;
        }
        
        // Check if partial matching is enabled and filter suggestions
        var modelsToShow = settings.Models;
        if (settings.Settings.EnablePartialAliasMatching && 
            requestedAlias.Length >= settings.Settings.MinimumAliasMatchLength)
        {
            var partialMatches = await _secureConfig.GetModelsByPartialMatchAsync(requestedAlias);
            if (partialMatches.Any())
            {
                modelsToShow = partialMatches;
                _logger.Information($"{Constants.UI.BulbSymbol} Did you mean one of these models matching '{requestedAlias}'?");
            }
            else
            {
                _logger.Information($"{Constants.UI.BulbSymbol} No models match '{requestedAlias}'. Here are all available models:");
            }
        }
        else
        {
            _logger.Information($"{Constants.UI.BulbSymbol} Did you mean one of these?");
        }
        
        Console.WriteLine();
        
        // Display filtered models with their details
        foreach (var model in modelsToShow.OrderBy(m => m.Name))
        {
            var defaultMarker = model.Id == settings.DefaultModelId ? " ⭐ (default)" : "";
            _logger.Success($"  {model.Name}{defaultMarker}");
            
            // Show aliases if any
            if (model.Aliases != null && model.Aliases.Count > 0)
            {
                var aliasesStr = string.Join(", ", model.Aliases.OrderBy(a => a).Select(a => $"@{a}"));
                _logger.Information($"    Aliases: {aliasesStr}");
            }
            
            // Show model details
            _logger.Muted($"    Type: {model.Type} | Provider: {model.Provider} | Model: {model.ModelId}");
            _logger.Muted($"    URL: {model.Url}");
            
            // Show pricing if available
            if (model.Pricing != null)
            {
                var pricingInfo = CostCalculationService.FormatPricingInfo(model.Pricing);
                _logger.Muted($"    Pricing: {pricingInfo}");
            }
            
            Console.WriteLine();
        }
        
        _logger.Information($"{Constants.UI.InfoSymbol} Usage examples:");
        _logger.Information("  gitgen @modelname");
        _logger.Information("  gitgen \"your prompt\" @modelname");
        _logger.Information("  gitgen @modelname \"your prompt\"");
    }

    /// <summary>
    ///     Estimates the number of tokens in a text string using a very rough approximation.
    ///     This uses a simple 4 characters per token ratio which is reasonable for English text and code.
    ///     Actual token counts will vary significantly based on content and tokenizer used by each model.
    /// </summary>
    /// <param name="text">The text to estimate tokens for.</param>
    /// <returns>Rough estimate of token count.</returns>
    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrEmpty(text)) return 0;
        // Rough estimate: ~4 characters per token
        return text.Length / 4;
    }

    /// <summary>
    ///     Generates a commit message using a truncated diff after a context length error.
    /// </summary>
    private async Task GenerateWithTruncatedDiffAsync(ModelConfiguration activeModel, string originalDiff, string? instruction)
    {
        try
        {
            _logger.Information($"{Constants.UI.LoadingSymbol} Truncating diff and retrying...");
            Console.WriteLine();

            // Get the model's context limit (use a conservative default if not known)
            var contextLimit = activeModel.ContextLength ?? 128000;
            
            // Estimate system prompt size
            var systemPromptTokens = EstimateSystemPromptSize(activeModel, instruction);
            
            // Truncate the diff
            var truncatedDiff = _truncationService.TruncateDiff(originalDiff, contextLimit, systemPromptTokens);
            
            _logger.Debug($"Truncated diff from {originalDiff.Length:N0} to {truncatedDiff.Length:N0} characters");
            
            // Get app settings to check if confirmation is required
            var settings = await _secureConfig.LoadSettingsAsync();
            
            // Show preview information if confirmation is required
            if (settings.Settings.RequirePromptConfirmation)
            {
                _logger.Information("Truncated diff preview:");
                await ShowPreviewForConfirmation(activeModel, truncatedDiff, instruction);
                
                Console.WriteLine();
                Console.Write("Send truncated diff to LLM? (y/N): ");
                var confirm = Console.ReadLine()?.Trim().ToLower();
                
                if (confirm != "y" && confirm != "yes")
                {
                    _logger.Information("Generation cancelled.");
                    return;
                }
                
                Console.WriteLine();
            }
            
            // Generate with truncated diff
            var result = await _generator.GenerateAsync(activeModel, truncatedDiff, instruction);

            _logger.Success($"{Constants.UI.CheckMark} Generated Commit Message (from truncated diff):");

            // Display commit message in teal color for visibility
            _logger.Highlight($"{Constants.UI.CommitMessageQuotes}{result.Message}{Constants.UI.CommitMessageQuotes}",
                ConsoleColor.DarkCyan);

            Console.WriteLine();

            // Display token usage if enabled
            if (settings.Settings.ShowTokenUsage && result.InputTokens.HasValue && result.OutputTokens.HasValue)
            {
                var tokenInfo = $"Generated with {result.InputTokens:N0} input tokens, {result.OutputTokens:N0} output tokens ({result.TotalTokens:N0} total)";
                
                // Add cost if pricing is configured
                if (activeModel.Pricing != null)
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
                
                _logger.Muted(tokenInfo);
                Console.WriteLine();
            }

            // Copy to clipboard if enabled
            if (settings.Settings.CopyToClipboard)
            {
                await ClipboardService.SetTextAsync(result.Message);
                _logger.Information($"{Constants.UI.ClipboardSymbol} {Constants.Messages.CommitMessageCopied}");
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to generate with truncated diff");
            _logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.UnexpectedError}", ex.Message);
        }
    }

    /// <summary>
    ///     Estimates the size of the system prompt in tokens.
    ///     This is a rough approximation based on the base prompt template plus any custom instructions.
    /// </summary>
    /// <param name="model">The model configuration which may contain a custom system prompt.</param>
    /// <param name="customInstruction">Optional custom instruction from the user.</param>
    /// <returns>Rough estimate of system prompt token count.</returns>
    private static int EstimateSystemPromptSize(ModelConfiguration model, string? customInstruction)
    {
        // Base prompt is approximately 1600 characters
        var basePromptSize = 1600;
        
        // Add custom instruction if provided
        if (!string.IsNullOrWhiteSpace(customInstruction))
        {
            basePromptSize += customInstruction.Length;
        }
        
        // Add model's custom system prompt if configured
        if (!string.IsNullOrWhiteSpace(model.SystemPrompt))
        {
            basePromptSize += model.SystemPrompt.Length;
        }
        
        return EstimateTokens(new string('x', basePromptSize));
    }
}