using System.Diagnostics;
using System.Text.Json;
using GitGen.Configuration;
using GitGen.Providers;

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
    /// <returns>The result with timing information</returns>
    Task<LlmCallResult> TrackCallAsync(
        string operation,
        string prompt,
        ModelConfiguration? model,
        Func<Task<CommitMessageResult>> apiCall);
}

/// <summary>
///     Implementation of LLM call tracking with transparent logging and cost calculation.
/// </summary>
public class LlmCallTracker : ILlmCallTracker
{
    private readonly IConsoleLogger _logger;
    
    public LlmCallTracker(IConsoleLogger logger)
    {
        _logger = logger;
    }
    
    public async Task<LlmCallResult> TrackCallAsync(
        string operation,
        string prompt,
        ModelConfiguration? model,
        Func<Task<CommitMessageResult>> apiCall)
    {
        // Log what we're doing
        _logger.Debug($"🤖 {operation}");
        _logger.Debug($"Model: {model?.Name ?? "Unknown"} ({model?.ModelId ?? "Unknown"})");
        _logger.Debug($"Prompt length: {prompt.Length} characters");
        
        // Show truncated prompt in debug mode
        if (prompt.Length > 200)
        {
            _logger.Debug($"Prompt preview: {prompt.Substring(0, 200)}...");
        }
        else
        {
            _logger.Debug($"Prompt: {prompt}");
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
            DisplayCallResult(operation, llmResult);
            
            return llmResult;
        }
        catch (Exception)
        {
            stopwatch.Stop();
            _logger.Error($"🤖❌ {operation} failed after {stopwatch.Elapsed.TotalSeconds:F1}s");
            throw;
        }
    }
    
    private void DisplayCallResult(string operation, LlmCallResult result)
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
        _logger.Muted(statusLine);
        
        // In debug mode, show response preview
        if (result.Message.Length > 100)
        {
            _logger.Debug($"Response preview: {result.Message.Substring(0, 100)}...");
        }
        else if (!string.IsNullOrWhiteSpace(result.Message))
        {
            _logger.Debug($"Response: {result.Message}");
        }
    }
}