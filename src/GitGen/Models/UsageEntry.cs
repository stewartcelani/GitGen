using System.Text.Json.Serialization;

namespace GitGen.Models;

/// <summary>
///     Represents a single usage entry for tracking LLM API calls.
///     Designed to be serialized as JSONL (JSON Lines) format.
/// </summary>
public class UsageEntry
{
    /// <summary>
    ///     Gets or sets the timestamp when the API call was made.
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets a unique session identifier for grouping related calls.
    /// </summary>
    [JsonPropertyName("sessionId")]
    public string SessionId { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the model information used for this call.
    /// </summary>
    [JsonPropertyName("model")]
    public ModelInfo Model { get; set; } = new();

    /// <summary>
    ///     Gets or sets the token usage information.
    /// </summary>
    [JsonPropertyName("tokens")]
    public TokenUsage Tokens { get; set; } = new();

    /// <summary>
    ///     Gets or sets the cost information for this call.
    /// </summary>
    [JsonPropertyName("cost")]
    public CostInfo? Cost { get; set; }

    /// <summary>
    ///     Gets or sets the operation type (e.g., "commit_message_generation").
    /// </summary>
    [JsonPropertyName("operation")]
    public string Operation { get; set; } = "commit_message_generation";

    /// <summary>
    ///     Gets or sets the duration of the API call in seconds.
    /// </summary>
    [JsonPropertyName("duration")]
    public double Duration { get; set; }

    /// <summary>
    ///     Gets or sets whether the operation was successful.
    /// </summary>
    [JsonPropertyName("success")]
    public bool Success { get; set; } = true;

    /// <summary>
    ///     Gets or sets the project path where GitGen was executed.
    /// </summary>
    [JsonPropertyName("projectPath")]
    public string? ProjectPath { get; set; }

    /// <summary>
    ///     Gets or sets the git branch name at the time of execution.
    /// </summary>
    [JsonPropertyName("gitBranch")]
    public string? GitBranch { get; set; }

    /// <summary>
    ///     Gets or sets any error message if the operation failed.
    /// </summary>
    [JsonPropertyName("error")]
    public string? Error { get; set; }
}

/// <summary>
///     Represents model information in the usage entry.
/// </summary>
public class ModelInfo
{
    /// <summary>
    ///     Gets or sets the user-friendly model name.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the provider name.
    /// </summary>
    [JsonPropertyName("provider")]
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the actual model ID used in the API call.
    /// </summary>
    [JsonPropertyName("modelId")]
    public string ModelId { get; set; } = string.Empty;
}

/// <summary>
///     Represents token usage information.
/// </summary>
public class TokenUsage
{
    /// <summary>
    ///     Gets or sets the number of input tokens.
    /// </summary>
    [JsonPropertyName("input")]
    public int Input { get; set; }

    /// <summary>
    ///     Gets or sets the number of output tokens.
    /// </summary>
    [JsonPropertyName("output")]
    public int Output { get; set; }

    /// <summary>
    ///     Gets or sets the total number of tokens.
    /// </summary>
    [JsonPropertyName("total")]
    public int Total { get; set; }
}

/// <summary>
///     Represents cost information for the API call.
/// </summary>
public class CostInfo
{
    /// <summary>
    ///     Gets or sets the cost amount.
    /// </summary>
    [JsonPropertyName("amount")]
    public decimal Amount { get; set; }

    /// <summary>
    ///     Gets or sets the currency code (e.g., "USD").
    /// </summary>
    [JsonPropertyName("currency")]
    public string Currency { get; set; } = "USD";
}