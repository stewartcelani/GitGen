namespace GitGen.Providers;

/// <summary>
///     Represents the result of a commit message generation operation.
///     Contains the generated message and optional token usage statistics.
/// </summary>
public class CommitMessageResult
{
    /// <summary>
    ///     Gets or sets the generated commit message content.
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the number of tokens in the input prompt, if available.
    /// </summary>
    public int? InputTokens { get; set; }

    /// <summary>
    ///     Gets or sets the number of tokens in the generated response, if available.
    /// </summary>
    public int? OutputTokens { get; set; }

    /// <summary>
    ///     Gets or sets the total number of tokens used in the API call, if available.
    /// </summary>
    public int? TotalTokens { get; set; }
}

/// <summary>
///     Defines the contract for an AI provider that can generate commit messages.
///     Implementations handle specific AI service integrations and API communication.
/// </summary>
public interface ICommitMessageProvider
{
    /// <summary>
    ///     Gets the user-friendly name of the provider (e.g., "OpenAI").
    /// </summary>
    string ProviderName { get; }

    /// <summary>
    ///     Generates a commit message based on a git diff and an optional custom instruction.
    /// </summary>
    /// <param name="diff">The git diff content of uncommitted changes.</param>
    /// <param name="customInstruction">An optional user-provided instruction to guide the AI's response style.</param>
    /// <returns>A <see cref="CommitMessageResult" /> containing the generated message and token usage.</returns>
    Task<CommitMessageResult> GenerateCommitMessageAsync(string diff, string? customInstruction = null);

    /// <summary>
    ///     Generates a generic response from the AI based on a given prompt.
    ///     Used for testing connections and non-commit-specific interactions.
    /// </summary>
    /// <param name="prompt">The input prompt to send to the AI.</param>
    /// <returns>A <see cref="CommitMessageResult" /> containing the generated response.</returns>
    Task<CommitMessageResult> GenerateAsync(string prompt);

    /// <summary>
    ///     Tests the connection and detects the correct API parameters for the provider.
    ///     First tries modern parameters, then falls back to legacy if needed.
    ///     Also detects the correct temperature value to use (0.2 or 1.0).
    /// </summary>
    /// <returns>A tuple containing success status, whether legacy parameters are required, and the correct temperature to use.</returns>
    Task<(bool Success, bool UseLegacyTokens, double Temperature)> TestConnectionAndDetectParametersAsync();
}