using System.Text.Json.Serialization;

namespace GitGen.Providers.OpenAI;

/// <summary>
///     Represents a request payload for OpenAI-compatible chat completion APIs.
///     Contains all necessary parameters for generating AI responses.
/// </summary>
public class OpenAIRequest
{
    /// <summary>
    ///     Gets or sets the model name to use for generation (e.g., "gpt-4", "o1-mini").
    /// </summary>
    [JsonPropertyName("model")]
    public string Model { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the array of messages that comprise the conversation.
    /// </summary>
    [JsonPropertyName("messages")]
    public Message[] Messages { get; set; } = Array.Empty<Message>();

    /// <summary>
    ///     Gets or sets the sampling temperature between 0 and 2. Higher values make output more random.
    ///     Omitted from JSON when null.
    /// </summary>
    [JsonPropertyName("temperature")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Temperature { get; set; }

    /// <summary>
    ///     Gets or sets the maximum number of tokens to generate (legacy parameter for older models).
    ///     Omitted from JSON when null.
    /// </summary>
    [JsonPropertyName("max_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxTokens { get; set; }

    /// <summary>
    ///     Gets or sets the maximum number of completion tokens to generate (modern parameter for newer models).
    ///     Omitted from JSON when null.
    /// </summary>
    [JsonPropertyName("max_completion_tokens")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? MaxCompletionTokens { get; set; }
}

/// <summary>
///     Represents a single message in a chat conversation.
///     Used in both request and response payloads.
/// </summary>
public class Message
{
    /// <summary>
    ///     Gets or sets the role of the message sender (e.g., "system", "user", "assistant").
    /// </summary>
    [JsonPropertyName("role")]
    public string Role { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the content of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;
}