using System.Threading.Tasks;

namespace GitGen.Services;

/// <summary>
///     Orchestrates the main commit message generation workflow.
///     Handles configuration loading, validation, and generation execution.
/// </summary>
public interface IGenerationOrchestrator
{
    /// <summary>
    ///     Executes the main generation workflow.
    /// </summary>
    /// <param name="modelName">Optional model name or alias to use.</param>
    /// <param name="customInstruction">Optional custom instruction for generation.</param>
    /// <param name="isPreviewMode">Whether to run in preview mode (no LLM calls).</param>
    /// <returns>Exit code (0 for success, non-zero for failure).</returns>
    Task<int> ExecuteAsync(string? modelName, string? customInstruction, bool isPreviewMode);
}