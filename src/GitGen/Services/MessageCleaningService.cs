using System.Text.RegularExpressions;

namespace GitGen.Services;

/// <summary>
///     Centralized service for cleaning LLM responses by removing thinking tags.
///     Handles both &lt;think&gt; and &lt;thinking&gt; tag formats used by different models.
/// </summary>
public static class MessageCleaningService
{
    /// <summary>
    ///     Cleans an LLM response by removing both &lt;think&gt; and &lt;thinking&gt; tags with their content.
    /// </summary>
    /// <param name="message">The raw message from the LLM</param>
    /// <returns>A cleaned message with thinking tags removed</returns>
    public static string CleanLlmResponse(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return Constants.Fallbacks.NoResponseMessage;

        var cleaned = message;

        // Remove <think>...</think> tags (used by models like DeepSeek)
        cleaned = Regex.Replace(cleaned, @"<think>.*?</think>", "", RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Remove <thinking>...</thinking> tags (used by other models)
        cleaned = Regex.Replace(cleaned, @"<thinking>.*?</thinking>", "",
            RegexOptions.IgnoreCase | RegexOptions.Singleline);

        // Sanitize for shell command safety (git commit -m compatibility)
        cleaned = SanitizeForShell(cleaned);

        return cleaned.Trim();
    }

    /// <summary>
    ///     Cleans an LLM response specifically for commit messages.
    /// </summary>
    /// <param name="message">The raw commit message from the LLM</param>
    /// <returns>A cleaned commit message</returns>
    public static string CleanCommitMessage(string message)
    {
        return CleanLlmResponse(message);
    }

    /// <summary>
    ///     Cleans an LLM response for display purposes.
    /// </summary>
    /// <param name="message">The raw message from the LLM</param>
    /// <returns>A cleaned message suitable for console display</returns>
    public static string CleanForDisplay(string message)
    {
        return CleanLlmResponse(message);
    }

    /// <summary>
    ///     Sanitizes text for shell command safety by replacing problematic characters.
    /// </summary>
    /// <param name="text">The text to sanitize</param>
    /// <returns>Shell-safe text suitable for git commit -m commands</returns>
    private static string SanitizeForShell(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;

        // Replace double quotes with single quotes for shell safety
        text = text.Replace("\"", "'");

        // Remove or replace other potentially problematic shell characters
        text = text.Replace("`", "'"); // Replace backticks with single quotes
        text = text.Replace(";", ","); // Replace semicolons with commas
        text = text.Replace("|", "-"); // Replace pipes with dashes
        text = text.Replace("&", "and"); // Replace ampersands with "and"
        text = text.Replace("$", ""); // Remove dollar signs
        text = text.Replace("\\", "/"); // Replace backslashes with forward slashes

        return text;
    }
}