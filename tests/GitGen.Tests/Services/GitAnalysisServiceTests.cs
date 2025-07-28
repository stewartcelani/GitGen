using FluentAssertions;
using GitGen.Services;
using LibGit2Sharp;
using NSubstitute;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace GitGen.Tests.Services;

public class GitAnalysisServiceTests : TestBase
{
    private readonly GitAnalysisService _service;
    private readonly string _testRepoPath;

    public GitAnalysisServiceTests()
    {
        _service = new GitAnalysisService(Logger);
        _testRepoPath = Path.Combine(TestDirectory, "test-repo");
    }

    [Fact]
    public void IsGitRepository_WithValidRepo_ReturnsTrue()
    {
        // Arrange
        CreateTestRepository();

        // Act
        bool result;
        using (new DirectoryChanger(_testRepoPath))
        {
            result = _service.IsGitRepository();
        }

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void IsGitRepository_WithoutRepo_ReturnsFalse()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);

        // Act
        bool result;
        using (new DirectoryChanger(_testRepoPath))
        {
            result = _service.IsGitRepository();
        }

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void GetRepositoryDiff_WithUncommittedChanges_ReturnsDiff()
    {
        // Arrange
        var repo = CreateTestRepository();
        var testFile = Path.Combine(_testRepoPath, "test.txt");
        File.WriteAllText(testFile, "Modified content");

        // Act
        string diff;
        using (new DirectoryChanger(_testRepoPath))
        {
            diff = _service.GetRepositoryDiff();
        }

        // Assert
        diff.Should().NotBeNullOrEmpty();
        diff.Should().Contain("Modified content");
    }

    [Fact]
    public void GetRepositoryDiff_WithNoChanges_ReturnsEmptyString()
    {
        // Arrange
        CreateTestRepository();

        // Act
        string diff;
        using (new DirectoryChanger(_testRepoPath))
        {
            diff = _service.GetRepositoryDiff();
        }

        // Assert
        diff.Should().BeEmpty();
    }

    [Fact]
    public void GetRepositoryDiff_WithStagedChanges_IncludesInDiff()
    {
        // Arrange
        var repo = CreateTestRepository();
        var testFile = Path.Combine(_testRepoPath, "staged.txt");
        File.WriteAllText(testFile, "Staged content");
        Commands.Stage(repo, testFile);

        // Act
        string diff;
        using (new DirectoryChanger(_testRepoPath))
        {
            diff = _service.GetRepositoryDiff();
        }

        // Assert
        diff.Should().NotBeNullOrEmpty();
        diff.Should().Contain("Staged content");
    }

    [Fact]
    public void GetRepositoryDiff_WithNewRepository_HandlesNoCommits()
    {
        // Arrange
        Repository.Init(_testRepoPath);
        var testFile = Path.Combine(_testRepoPath, "initial.txt");
        File.WriteAllText(testFile, "Initial content");

        // Act
        string diff;
        using (new DirectoryChanger(_testRepoPath))
        {
            diff = _service.GetRepositoryDiff();
        }

        // Assert
        diff.Should().NotBeNullOrEmpty();
        diff.Should().Contain("Initial content");
    }

    [Fact]
    public void GetRepositoryDiff_NotInRepository_ThrowsInvalidOperationException()
    {
        // Arrange
        Directory.CreateDirectory(_testRepoPath);

        // Act & Assert
        using (new DirectoryChanger(_testRepoPath))
        {
            var act = () => _service.GetRepositoryDiff();
            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*No Git repository found*");
        }
    }

    private Repository CreateTestRepository()
    {
        Repository.Init(_testRepoPath);
        var repo = new Repository(_testRepoPath);
        
        // Create initial commit
        var sig = new Signature("Test User", "test@example.com", DateTimeOffset.Now);
        var testFile = Path.Combine(_testRepoPath, "test.txt");
        File.WriteAllText(testFile, "Initial content");
        
        Commands.Stage(repo, testFile);
        repo.Commit("Initial commit", sig, sig);
        
        return repo;
    }

    private class DirectoryChanger : IDisposable
    {
        private readonly string _originalDirectory;

        public DirectoryChanger(string newDirectory)
        {
            _originalDirectory = Directory.GetCurrentDirectory();
            Directory.SetCurrentDirectory(newDirectory);
        }

        public void Dispose()
        {
            Directory.SetCurrentDirectory(_originalDirectory);
        }
    }
}