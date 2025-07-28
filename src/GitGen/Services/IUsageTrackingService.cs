using GitGen.Models;

namespace GitGen.Services;

/// <summary>
///     Service for tracking and persisting LLM usage data to JSONL files.
/// </summary>
public interface IUsageTrackingService
{
    /// <summary>
    ///     Records a usage entry to the appropriate JSONL file.
    /// </summary>
    /// <param name="entry">The usage entry to record.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    Task RecordUsageAsync(UsageEntry entry);

    /// <summary>
    ///     Gets or creates a session ID for the current execution.
    /// </summary>
    /// <returns>A unique session identifier.</returns>
    string GetSessionId();
}