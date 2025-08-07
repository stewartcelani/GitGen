using System;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using GitGen.Services;
using LibGit2Sharp;
using Moq;
using Xunit;

namespace GitGen.Tests.Services;

public class GitAnalysisServiceTests : IDisposable
{
    private readonly Mock<IConsoleLogger> _loggerMock;
    private readonly GitAnalysisService _service;
    private readonly string _tempPath;
    private readonly string _originalDirectory;

    public GitAnalysisServiceTests()
    {
        _loggerMock = new Mock<IConsoleLogger>();
        _service = new GitAnalysisService(_loggerMock.Object);
        
        // Create a temporary directory for test repositories
        _tempPath = Path.Combine(Path.GetTempPath(), $"GitGenTests_{Guid.NewGuid()}");
        Directory.CreateDirectory(_tempPath);
        
        // Save the original directory to restore later
        _originalDirectory = Directory.GetCurrentDirectory();
    }

    public void Dispose()
    {
        // Restore the original directory
        Directory.SetCurrentDirectory(_originalDirectory);
        
        // Clean up temporary directories
        if (Directory.Exists(_tempPath))
        {
            try
            {
                Directory.Delete(_tempPath, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }

    [Fact]
    public void IsGitRepository_WhenNotInGitRepo_ReturnsFalse()
    {
        // Arrange
        var nonGitDir = Path.Combine(_tempPath, "not-a-repo");
        Directory.CreateDirectory(nonGitDir);
        Directory.SetCurrentDirectory(nonGitDir);

        // Act
        var result = _service.IsGitRepository();

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void IsGitRepository_WhenInGitRepo_ReturnsTrue()
    {
        // Arrange
        var repoPath = CreateTestRepository();
        Directory.SetCurrentDirectory(repoPath);

        // Act
        var result = _service.IsGitRepository();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsGitRepository_WhenInSubdirectoryOfGitRepo_ReturnsTrue()
    {
        // Arrange
        var repoPath = CreateTestRepository();
        var subDir = Path.Combine(repoPath, "subdirectory");
        Directory.CreateDirectory(subDir);
        Directory.SetCurrentDirectory(subDir);

        // Act
        var result = _service.IsGitRepository();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void GetRepositoryDiff_WhenNotInGitRepo_ThrowsInvalidOperationException()
    {
        // Arrange
        var nonGitDir = Path.Combine(_tempPath, "not-a-repo");
        Directory.CreateDirectory(nonGitDir);
        Directory.SetCurrentDirectory(nonGitDir);

        // Act & Assert
        var action = () => _service.GetRepositoryDiff();
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*No Git repository found*");
        
        _loggerMock.Verify(x => x.Error(
            It.IsAny<Exception>(), 
            "Error analyzing Git repository"), Times.Once);
    }

    [Fact]
    public void GetRepositoryDiff_WithNoChanges_ReturnsEmptyString()
    {
        // Arrange
        var repoPath = CreateTestRepository();
        Directory.SetCurrentDirectory(repoPath);

        // Act
        var result = _service.GetRepositoryDiff();

        // Assert
        result.Should().BeEmpty();
        _loggerMock.Verify(x => x.Information(
            "No uncommitted changes detected in repository"), Times.Once);
    }

    [Fact]
    public void GetRepositoryDiff_WithUnstagedChanges_ReturnsDiff()
    {
        // Arrange
        var repoPath = CreateTestRepository();
        Directory.SetCurrentDirectory(repoPath);
        
        // Create a file and modify it
        var filePath = Path.Combine(repoPath, "test.txt");
        File.WriteAllText(filePath, "Initial content\n");
        CommitAllChanges(repoPath, "Initial commit");
        
        // Modify the file (unstaged change)
        File.AppendAllText(filePath, "Modified content\n");

        // Act
        var result = _service.GetRepositoryDiff();

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("test.txt");
        result.Should().Contain("+Modified content");
        
        _loggerMock.Verify(x => x.Information(
            "Found {Count} changed files", 1), Times.Once);
    }

    [Fact]
    public void GetRepositoryDiff_WithStagedChanges_ReturnsDiff()
    {
        // Arrange
        var repoPath = CreateTestRepository();
        Directory.SetCurrentDirectory(repoPath);
        
        // Create and commit initial file
        var filePath = Path.Combine(repoPath, "test.txt");
        File.WriteAllText(filePath, "Initial content\n");
        CommitAllChanges(repoPath, "Initial commit");
        
        // Modify and stage the file
        File.AppendAllText(filePath, "Staged content\n");
        using (var repo = new Repository(repoPath))
        {
            Commands.Stage(repo, filePath);
        }

        // Act
        var result = _service.GetRepositoryDiff();

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("test.txt");
        result.Should().Contain("+Staged content");
    }

    [Fact]
    public void GetRepositoryDiff_WithNewFile_ReturnsDiff()
    {
        // Arrange
        var repoPath = CreateTestRepository();
        Directory.SetCurrentDirectory(repoPath);
        
        // Create initial commit
        var initialFile = Path.Combine(repoPath, "initial.txt");
        File.WriteAllText(initialFile, "Initial file\n");
        CommitAllChanges(repoPath, "Initial commit");
        
        // Add new file
        var newFile = Path.Combine(repoPath, "new-file.txt");
        File.WriteAllText(newFile, "New file content\n");

        // Act
        var result = _service.GetRepositoryDiff();

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("new-file.txt");
        result.Should().Contain("+New file content");
    }

    [Fact]
    public void GetRepositoryDiff_WithDeletedFile_ReturnsDiff()
    {
        // Arrange
        var repoPath = CreateTestRepository();
        Directory.SetCurrentDirectory(repoPath);
        
        // Create and commit file
        var filePath = Path.Combine(repoPath, "to-delete.txt");
        File.WriteAllText(filePath, "File to be deleted\n");
        CommitAllChanges(repoPath, "Add file");
        
        // Delete the file
        File.Delete(filePath);

        // Act
        var result = _service.GetRepositoryDiff();

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("to-delete.txt");
        result.Should().Contain("-File to be deleted");
    }

    [Fact]
    public void GetRepositoryDiff_WithMultipleChanges_ReturnsCompleteDiff()
    {
        // Arrange
        var repoPath = CreateTestRepository();
        Directory.SetCurrentDirectory(repoPath);
        
        // Create initial files
        var file1 = Path.Combine(repoPath, "file1.txt");
        var file2 = Path.Combine(repoPath, "file2.txt");
        File.WriteAllText(file1, "File 1 content\n");
        File.WriteAllText(file2, "File 2 content\n");
        CommitAllChanges(repoPath, "Initial commit");
        
        // Make multiple changes
        File.AppendAllText(file1, "Modified content\n");
        File.Delete(file2);
        var file3 = Path.Combine(repoPath, "file3.txt");
        File.WriteAllText(file3, "New file content\n");

        // Act
        var result = _service.GetRepositoryDiff();

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("file1.txt");
        result.Should().Contain("file2.txt");
        result.Should().Contain("file3.txt");
        result.Should().Contain("+Modified content");
        result.Should().Contain("-File 2 content");
        result.Should().Contain("+New file content");
        
        _loggerMock.Verify(x => x.Information(
            "Found {Count} changed files", 3), Times.Once);
    }

    [Fact]
    public void GetRepositoryDiff_InNewRepoWithNoCommits_ReturnsDiff()
    {
        // Arrange
        var repoPath = CreateTestRepository();
        Directory.SetCurrentDirectory(repoPath);
        
        // Add files without committing (new repo state)
        var file1 = Path.Combine(repoPath, "file1.txt");
        File.WriteAllText(file1, "First file in new repo\n");

        // Act
        var result = _service.GetRepositoryDiff();

        // Assert
        result.Should().NotBeEmpty();
        result.Should().Contain("file1.txt");
        result.Should().Contain("+First file in new repo");
    }

    [Fact]
    public void GetRepositoryDiff_LogsDebugInfoAboutDiffSize()
    {
        // Arrange
        var repoPath = CreateTestRepository();
        Directory.SetCurrentDirectory(repoPath);
        
        var filePath = Path.Combine(repoPath, "test.txt");
        File.WriteAllText(filePath, "Some content to generate a diff\n");

        // Act
        var result = _service.GetRepositoryDiff();

        // Assert
        _loggerMock.Verify(x => x.Debug(
            "Generated diff with {Length} characters", 
            It.IsAny<int>()), Times.Once);
    }

    // Helper methods

    private string CreateTestRepository()
    {
        var repoPath = Path.Combine(_tempPath, $"test-repo-{Guid.NewGuid()}");
        Directory.CreateDirectory(repoPath);
        Repository.Init(repoPath);
        return repoPath;
    }

    private void CommitAllChanges(string repoPath, string message)
    {
        using var repo = new Repository(repoPath);
        
        // Stage all changes
        Commands.Stage(repo, "*");
        
        // Create signature
        var author = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
        var committer = author;
        
        // Commit
        repo.Commit(message, author, committer);
    }
}