using LibGit2Sharp;

namespace GitGen.Services;

/// <summary>
///     Service for interacting with Git repositories to analyze changes and repository status.
///     Provides functionality to generate diffs and validate repository state.
/// </summary>
public class GitAnalysisService
{
    private readonly IConsoleLogger _logger;

    /// <summary>
    ///     Initializes a new instance of the <see cref="GitAnalysisService" /> class.
    /// </summary>
    /// <param name="logger">The console logger for debugging and error reporting.</param>
    public GitAnalysisService(IConsoleLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Generates a diff of all uncommitted changes (both staged and unstaged) in the current repository.
    ///     Handles repositories with and without initial commits.
    /// </summary>
    /// <returns>A string containing the git diff, or an empty string if there are no changes.</returns>
    /// <exception cref="InvalidOperationException">
    ///     Thrown if the current directory is not a Git repository or if Git
    ///     operations fail.
    /// </exception>
    public virtual string GetRepositoryDiff()
    {
        try
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var repoPath = Repository.Discover(currentDirectory);

            if (string.IsNullOrEmpty(repoPath))
                throw new InvalidOperationException(
                    "No Git repository found in current directory or parent directories.");

            using var repo = new Repository(repoPath);

            var status = repo.RetrieveStatus();
            if (!status.IsDirty)
            {
                _logger.Information("No uncommitted changes detected in repository");
                return string.Empty;
            }

            _logger.Information("Found {Count} changed files", status.Count(s => s.State != FileStatus.Ignored));

            // Correctly compare the HEAD with the index and the working directory to get all changes.
            // This handles cases where there is no initial commit (repo.Head.Tip is null).
            var fromTree = repo.Head?.Tip?.Tree;
            var diff = repo.Diff.Compare<Patch>(fromTree, DiffTargets.Index | DiffTargets.WorkingDirectory);

            var diffContent = diff.Content;

            if (string.IsNullOrWhiteSpace(diffContent))
            {
                _logger.Information("Diff is empty; no changes to commit.");
                return string.Empty;
            }

            _logger.Debug("Generated diff with {Length} characters", diffContent.Length);

            return diffContent;
        }
        catch (RepositoryNotFoundException)
        {
            throw new InvalidOperationException("No Git repository found in current directory or parent directories.");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Error analyzing Git repository");
            throw new InvalidOperationException($"Failed to analyze Git repository: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Checks if the current directory is within a Git repository.
    /// </summary>
    /// <returns><c>true</c> if a Git repository is found; otherwise, <c>false</c>.</returns>
    public virtual bool IsGitRepository()
    {
        try
        {
            var currentDirectory = Directory.GetCurrentDirectory();
            var repoPath = Repository.Discover(currentDirectory);
            return !string.IsNullOrEmpty(repoPath);
        }
        catch
        {
            return false;
        }
    }
}