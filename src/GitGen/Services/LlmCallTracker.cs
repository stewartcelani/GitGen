using System.Diagnostics;
using System.Text.Json;
using GitGen.Configuration;
using GitGen.Models;
using GitGen.Providers;
using LibGit2Sharp;

namespace GitGen.Services;

/// <summary>
///     Extended result that includes timing information for LLM calls.
/// </summary>
public class LlmCallResult : CommitMessageResult
{
    /// <summary>
    ///     Gets or sets the time taken for the LLM call.
    /// </summary>
    public TimeSpan ElapsedTime { get; set; }
    
    /// <summary>
    ///     Gets or sets the prompt that was sent to the LLM.
    /// </summary>
    public string? Prompt { get; set; }
    
    /// <summary>
    ///     Gets or sets the model configuration used for this call.
    /// </summary>
    public ModelConfiguration? Model { get; set; }
}

/// <summary>
///     Service for tracking and displaying all LLM API calls with complete transparency.
/// </summary>
public interface ILlmCallTracker
{
    /// <summary>
    ///     Tracks an LLM call with timing and displays the results.
    /// </summary>
    /// <typeparam name="T">The type of result expected</typeparam>
    /// <param name="operation">Description of the operation being performed</param>
    /// <param name="prompt">The prompt being sent to the LLM</param>
    /// <param name="model">The model configuration being used</param>
    /// <param name="apiCall">The actual API call function</param>
    /// <param name="indent">Optional indentation for display output</param>
    /// <returns>The result with timing information</returns>
    Task<LlmCallResult> TrackCallAsync(
        string operation,
        string prompt,
        ModelConfiguration? model,
        Func<Task<CommitMessageResult>> apiCall,
        string indent = "");
}

/// <summary>
///     Implementation of LLM call tracking with transparent logging and cost calculation.
/// </summary>
public class LlmCallTracker : ILlmCallTracker
{
    private readonly IConsoleLogger _logger;
    private readonly IUsageTrackingService _usageTracking;
    
    public LlmCallTracker(IConsoleLogger logger, IUsageTrackingService usageTracking)
    {
        _logger = logger;
        _usageTracking = usageTracking;
    }
    
    public async Task<LlmCallResult> TrackCallAsync(
        string operation,
        string prompt,
        ModelConfiguration? model,
        Func<Task<CommitMessageResult>> apiCall,
        string indent = "")
    {
        // Log what we're doing
        _logger.Debug($"{indent}🤖 {operation}");
        _logger.Debug($"{indent}Model: {model?.Name ?? "Unknown"} ({model?.ModelId ?? "Unknown"})");
        _logger.Debug($"{indent}Prompt length: {prompt.Length} characters");
        
        // Show truncated prompt in debug mode
        if (prompt.Length > 200)
        {
            _logger.Debug($"{indent}Prompt preview: {prompt.Substring(0, 200)}...");
        }
        else
        {
            _logger.Debug($"{indent}Prompt: {prompt}");
        }
        
        // Start timing
        var stopwatch = Stopwatch.StartNew();
        
        try
        {
            // Make the actual API call
            var result = await apiCall();
            stopwatch.Stop();
            
            // Create extended result
            var llmResult = new LlmCallResult
            {
                Message = result.Message,
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
                TotalTokens = result.TotalTokens,
                ElapsedTime = stopwatch.Elapsed,
                Prompt = prompt,
                Model = model
            };
            
            // Display the results
            DisplayCallResult(operation, llmResult, indent);
            
            // Record usage
            await RecordUsageAsync(llmResult);
            
            return llmResult;
        }
        catch (Exception)
        {
            stopwatch.Stop();
            _logger.Error($"{indent}🤖❌ {operation} failed after {stopwatch.Elapsed.TotalSeconds:F1}s");
            throw;
        }
    }
    
    private void DisplayCallResult(string operation, LlmCallResult result, string indent = "")
    {
        // Build the status line
        var statusParts = new List<string>();
        
        // Token information
        if (result.InputTokens.HasValue && result.OutputTokens.HasValue)
        {
            statusParts.Add($"{result.InputTokens:N0} → {result.OutputTokens:N0} tokens ({result.TotalTokens:N0} total)");
        }
        
        // Cost information
        if (result.Model?.Pricing != null && result.InputTokens.HasValue && result.OutputTokens.HasValue)
        {
            var cost = CostCalculationService.CalculateAndFormatCost(
                result.Model, 
                result.InputTokens.Value, 
                result.OutputTokens.Value);
                
            if (!string.IsNullOrEmpty(cost))
            {
                statusParts.Add($"~{cost}");
            }
        }
        
        // Timing
        statusParts.Add($"{result.ElapsedTime.TotalSeconds:F1}s");
        
        // Display the status line
        var statusLine = string.Join(" • ", statusParts);
        _logger.Muted($"{indent}📊 {statusLine}");
        
        // In debug mode, show response preview
        if (result.Message.Length > 100)
        {
            _logger.Debug($"{indent}Response preview: {result.Message.Substring(0, 100)}...");
        }
        else if (!string.IsNullOrWhiteSpace(result.Message))
        {
            _logger.Debug($"{indent}Response: {result.Message}");
        }
    }
    
    private async Task RecordUsageAsync(LlmCallResult result)
    {
        _logger.Debug("LlmCallTracker.RecordUsageAsync started");
        try
        {
            // Get git repository information
            string? projectPath = null;
            string? gitBranch = null;
            
            try
            {
                projectPath = Directory.GetCurrentDirectory();
                using var repo = new Repository(projectPath);
                gitBranch = repo.Head.FriendlyName;
            }
            catch
            {
                // Ignore git errors - not all projects are git repositories
            }
            
            // Create usage entry
            var entry = new UsageEntry
            {
                Timestamp = DateTime.UtcNow,
                SessionId = _usageTracking.GetSessionId(),
                Model = new ModelInfo
                {
                    Name = result.Model?.Name ?? "Unknown",
                    Provider = result.Model?.Provider ?? "Unknown",
                    ModelId = result.Model?.ModelId ?? "Unknown"
                },
                Tokens = new TokenUsage
                {
                    Input = result.InputTokens ?? 0,
                    Output = result.OutputTokens ?? 0,
                    Total = result.TotalTokens ?? 0
                },
                Operation = "commit_message_generation",
                Duration = result.ElapsedTime.TotalSeconds,
                Success = true,
                ProjectPath = projectPath,
                GitBranch = gitBranch
            };
            
            // Add cost information if available
            if (result.Model?.Pricing != null && result.InputTokens.HasValue && result.OutputTokens.HasValue)
            {
                var inputCost = (result.InputTokens.Value / 1_000_000m) * result.Model.Pricing.InputPer1M;
                var outputCost = (result.OutputTokens.Value / 1_000_000m) * result.Model.Pricing.OutputPer1M;
                var totalCost = inputCost + outputCost;
                
                entry.Cost = new CostInfo
                {
                    Amount = totalCost,
                    Currency = result.Model.Pricing.CurrencyCode
                };
            }
            
            // Record the usage
            _logger.Debug($"Recording usage for model: {entry.Model.Name}, tokens: {entry.Tokens.Total}, cost: {entry.Cost?.Amount ?? 0}");
            await _usageTracking.RecordUsageAsync(entry);
            _logger.Debug("Usage recording completed successfully");
        }
        catch (Exception ex)
        {
            _logger.Debug($"Failed to record usage: {ex.Message}");
        }
    }
}