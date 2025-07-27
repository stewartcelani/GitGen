using GitGen.Configuration;
using GitGen.Exceptions;
using GitGen.Providers;

namespace GitGen.Services;

/// <summary>
///     Service responsible for orchestrating the commit message generation process.
///     Coordinates between AI providers and message cleaning services to produce clean, formatted commit messages.
/// </summary>
public class CommitMessageGenerator
{
    private readonly IConsoleLogger _logger;
    private readonly ProviderFactory _providerFactory;

    /// <summary>
    ///     Initializes a new instance of the <see cref="CommitMessageGenerator" /> class.
    /// </summary>
    /// <param name="providerFactory">The factory to create AI provider instances.</param>
    /// <param name="logger">The console logger for debugging and error reporting.</param>
    public CommitMessageGenerator(ProviderFactory providerFactory, IConsoleLogger logger)
    {
        _providerFactory = providerFactory;
        _logger = logger;
    }

    /// <summary>
    ///     Generates a commit message using the configured AI provider and cleans the output.
    /// </summary>
    /// <param name="modelConfig">The model configuration containing provider settings.</param>
    /// <param name="diff">The git diff content to be summarized into a commit message.</param>
    /// <param name="customInstruction">An optional custom instruction to guide the generation style.</param>
    /// <returns>A <see cref="CommitMessageResult" /> containing the cleaned message and token usage statistics.</returns>
    /// <exception cref="ArgumentException">Thrown when the diff is null or empty.</exception>
    /// <exception cref="AuthenticationException">Thrown when authentication with the AI provider fails.</exception>
    public async Task<CommitMessageResult> GenerateAsync(ModelConfiguration modelConfig, string diff,
        string? customInstruction = null)
    {
        if (string.IsNullOrWhiteSpace(diff)) throw new ArgumentException("Diff cannot be null or empty", nameof(diff));

        try
        {
            var provider = _providerFactory.CreateProvider(modelConfig);
            
            // Display model information
            _logger.Information("🔗 Using {ModelName} ({ModelId} via {Provider})",
                modelConfig.Name, modelConfig.ModelId, modelConfig.Provider);

            if (!string.IsNullOrWhiteSpace(customInstruction))
                _logger.Information("Applying custom instruction: {Instruction}", customInstruction);

            var result = await provider.GenerateCommitMessageAsync(diff, customInstruction);

            if (string.IsNullOrWhiteSpace(result.Message))
            {
                _logger.Warning("Provider returned empty commit message, using automated fallback message");
                return new CommitMessageResult
                {
                    Message = "Automated commit of code changes.",
                    InputTokens = result.InputTokens,
                    OutputTokens = result.OutputTokens,
                    TotalTokens = result.TotalTokens
                };
            }

            var cleanedMessage = MessageCleaningService.CleanCommitMessage(result.Message);

            return new CommitMessageResult
            {
                Message = cleanedMessage,
                InputTokens = result.InputTokens,
                OutputTokens = result.OutputTokens,
                TotalTokens = result.TotalTokens
            };
        }
        catch (AuthenticationException)
        {
            // Re-throw authentication exceptions so they can be handled specifically by the caller
            throw;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to generate commit message using {Type}",
                modelConfig.Type ?? "unknown");
            throw;
        }
    }
}