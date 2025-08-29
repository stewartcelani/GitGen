using System.Net;

namespace GitGen.Exceptions;

/// <summary>
///     Exception thrown when the API request exceeds the model's context length limit.
/// </summary>
public class ContextLengthExceededException : Exception
{
    /// <summary>
    ///     Gets the maximum context length for the model.
    /// </summary>
    public int? MaxContextLength { get; }

    /// <summary>
    ///     Gets the total tokens that were requested.
    /// </summary>
    public int? RequestedTokens { get; }

    /// <summary>
    ///     Gets the number of tokens used by the messages/prompt.
    /// </summary>
    public int? PromptTokens { get; }

    /// <summary>
    ///     Gets the number of tokens requested for completion.
    /// </summary>
    public int? CompletionTokens { get; }

    /// <summary>
    ///     Gets the raw error message from the API.
    /// </summary>
    public string? ApiErrorMessage { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="ContextLengthExceededException" /> class.
    /// </summary>
    public ContextLengthExceededException(
        string message,
        string? apiErrorMessage = null,
        int? maxContextLength = null,
        int? requestedTokens = null,
        int? promptTokens = null,
        int? completionTokens = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ApiErrorMessage = apiErrorMessage;
        MaxContextLength = maxContextLength;
        RequestedTokens = requestedTokens;
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
    }

    /// <summary>
    ///     Parses token information from the API error message.
    /// </summary>
    /// <param name="apiErrorMessage">The error message from the API.</param>
    /// <returns>A new instance with parsed token information.</returns>
    public static ContextLengthExceededException ParseFromApiError(string apiErrorMessage, Exception? innerException = null)
    {
        // Parse patterns like:
        // "This model's maximum context length is 4097 tokens. However, you requested 4927 tokens (3927 in the messages, 1000 in the completion)."

        int? maxContext = null;
        int? requested = null;
        int? prompt = null;
        int? completion = null;

        // Extract max context length
        var maxContextMatch = System.Text.RegularExpressions.Regex.Match(
            apiErrorMessage,
            @"maximum context length is (\d+) tokens");
        if (maxContextMatch.Success && int.TryParse(maxContextMatch.Groups[1].Value, out var max))
        {
            maxContext = max;
        }

        // Also check for xAI format: "maximum prompt length is X"
        if (!maxContext.HasValue)
        {
            var maxPromptMatch = System.Text.RegularExpressions.Regex.Match(
                apiErrorMessage,
                @"maximum prompt length is (\d+)");
            if (maxPromptMatch.Success && int.TryParse(maxPromptMatch.Groups[1].Value, out var maxPrompt))
            {
                maxContext = maxPrompt;
            }
        }

        // Extract requested tokens
        var requestedMatch = System.Text.RegularExpressions.Regex.Match(
            apiErrorMessage,
            @"requested (\d+) tokens");
        if (requestedMatch.Success && int.TryParse(requestedMatch.Groups[1].Value, out var req))
        {
            requested = req;
        }

        // Also check for xAI format: "request contains X tokens"
        if (!requested.HasValue)
        {
            var containsMatch = System.Text.RegularExpressions.Regex.Match(
                apiErrorMessage,
                @"request contains (\d+) tokens");
            if (containsMatch.Success && int.TryParse(containsMatch.Groups[1].Value, out var contains))
            {
                requested = contains;
            }
        }

        // Extract breakdown if available
        var breakdownMatch = System.Text.RegularExpressions.Regex.Match(
            apiErrorMessage,
            @"\((\d+) in the messages, (\d+) in the completion\)");
        if (breakdownMatch.Success)
        {
            if (int.TryParse(breakdownMatch.Groups[1].Value, out var p))
                prompt = p;
            if (int.TryParse(breakdownMatch.Groups[2].Value, out var c))
                completion = c;
        }

        return new ContextLengthExceededException(
            Constants.ErrorMessages.ContextLengthExceeded,
            apiErrorMessage,
            maxContext,
            requested,
            prompt,
            completion,
            innerException);
    }
}