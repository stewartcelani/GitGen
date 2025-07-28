using FluentAssertions;
using GitGen.Configuration;
using GitGen.Services;
using GitGen.Providers;
using GitGen.Exceptions;
using NSubstitute;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using LibGit2Sharp;
using System.IO;

namespace GitGen.Tests.Services;

public class GenerationOrchestratorTests_Fixed : TestBase
{
    private readonly ISecureConfigurationService _secureConfig;
    private readonly IGenerationOrchestrator _orchestrator;
    private readonly string _testRepoPath;

    public GenerationOrchestratorTests_Fixed()
    {
        _secureConfig = Substitute.For<ISecureConfigurationService>();
        
        // Create a temporary test repository
        _testRepoPath = Path.Combine(TestDirectory, "test-repo");
        Directory.CreateDirectory(_testRepoPath);
        Repository.Init(_testRepoPath);
        
        // Create real services with mocked dependencies
        var configService = new ConfigurationService(Logger, _secureConfig);
        var providerFactory = Substitute.For<ProviderFactory>(CreateServiceProvider(), Logger);
        var provider = Substitute.For<ICommitMessageProvider>();
        providerFactory.CreateProvider(Arg.Any<ModelConfiguration>()).Returns(provider);
        
        var wizardService = new ConfigurationWizardService(Logger, providerFactory, configService, _secureConfig);
        var gitService = new GitAnalysisService(Logger);
        var generator = new CommitMessageGenerator(providerFactory, Logger);
        
        _orchestrator = new GenerationOrchestrator(
            Logger,
            _secureConfig,
            configService,
            wizardService,
            gitService,
            generator);

        // Default setup for provider
        provider.GenerateCommitMessageAsync(Arg.Any<string>(), Arg.Any<string>())
            .Returns(new CommitMessageResult
            {
                Message = "test: default commit",
                InputTokens = 100,
                OutputTokens = 50
            });
            
        // Change to test repo directory for git operations
        Environment.CurrentDirectory = _testRepoPath;
    }

    public class BasicTests : GenerationOrchestratorTests_Fixed
    {
        [Fact]
        public async Task ExecuteAsync_WithValidModel_ReturnsSuccess()
        {
            // Arrange
            var model = CreateTestModel("gpt-4");
            _secureConfig.GetModelAsync("gpt-4").Returns(model);
            _secureConfig.LoadSettingsAsync().Returns(new GitGenSettings
            {
                Models = new List<ModelConfiguration> { model },
                Settings = new AppSettings { CopyToClipboard = false }
            });

            // Create a test file and stage it
            File.WriteAllText(Path.Combine(_testRepoPath, "test.txt"), "test content");
            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, "test.txt");
            }

            // Act
            var result = await _orchestrator.ExecuteAsync("gpt-4", null, false);

            // Assert
            result.Should().Be(0);
            Logger.Received().Success(Arg.Is<string>(s => s.Contains("Generated Commit Message")));
        }

        [Fact]
        public async Task ExecuteAsync_WithNonExistentModel_ShowsSuggestionsAndReturnsError()
        {
            // Arrange
            _secureConfig.GetModelAsync("unknown").Returns((ModelConfiguration?)null);
            _secureConfig.LoadSettingsAsync().Returns(new GitGenSettings
            {
                Models = new List<ModelConfiguration>
                {
                    CreateTestModel("gpt-4"),
                    CreateTestModel("claude-3")
                },
                Settings = new AppSettings()
            });

            // Act
            var result = await _orchestrator.ExecuteAsync("unknown", null, false);

            // Assert
            result.Should().Be(1);
            Logger.Received().Error(Arg.Is<string>(s => s.Contains("Model or alias 'unknown' not found")));
        }

        [Fact]
        public async Task ExecuteAsync_InPreviewMode_DoesNotGenerateCommit()
        {
            // Arrange
            var model = CreateTestModel("gpt-4");
            _secureConfig.GetModelAsync(null).Returns(model);
            _secureConfig.LoadSettingsAsync().Returns(new GitGenSettings
            {
                Models = new List<ModelConfiguration> { model },
                Settings = new AppSettings(),
                DefaultModelId = model.Id
            });

            // Create a test file
            File.WriteAllText(Path.Combine(_testRepoPath, "test.txt"), "test content");
            using (var repo = new Repository(_testRepoPath))
            {
                Commands.Stage(repo, "test.txt");
            }

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, true);

            // Assert
            result.Should().Be(0);
            Logger.Received().Information("[PREVIEW MODE - No LLM call will be made]");
            Logger.DidNotReceive().Success(Arg.Is<string>(s => s.Contains("Generated Commit Message")));
        }

        [Fact]
        public async Task ExecuteAsync_NoGitRepository_ShowsError()
        {
            // Arrange
            var model = CreateTestModel();
            _secureConfig.GetModelAsync(null).Returns(model);
            
            // Change to a non-git directory
            var nonGitDir = Path.Combine(TestDirectory, "non-git");
            Directory.CreateDirectory(nonGitDir);
            Environment.CurrentDirectory = nonGitDir;

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, false);

            // Assert
            result.Should().Be(0);
            Logger.Received().Error(Arg.Is<string>(s => s.Contains("Not a git repository")));
        }

        [Fact]
        public async Task ExecuteAsync_WithConfigurationWizard_RunsWizard()
        {
            // Arrange
            _secureConfig.GetModelAsync(null).Returns((ModelConfiguration?)null);
            _secureConfig.LoadSettingsAsync().Returns(new GitGenSettings
            {
                Models = new List<ModelConfiguration>(),
                Settings = new AppSettings()
            });
            
            // Simulate wizard cancellation
            _secureConfig.HealDefaultModelAsync(Arg.Any<IConsoleLogger>()).Returns(false);

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, false);

            // Assert
            result.Should().Be(1);
            Logger.Received().Error(Arg.Is<string>(s => s.Contains("Configuration missing")));
        }
    }

    public override void Dispose()
    {
        // Restore original directory
        Environment.CurrentDirectory = Path.GetTempPath();
        
        // Clean up test repository
        if (Directory.Exists(_testRepoPath))
        {
            try
            {
                // Force delete .git directory
                var gitDir = Path.Combine(_testRepoPath, ".git");
                if (Directory.Exists(gitDir))
                {
                    RemoveReadOnlyAttributes(gitDir);
                }
                Directory.Delete(_testRepoPath, true);
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
        
        base.Dispose();
    }

    private void RemoveReadOnlyAttributes(string path)
    {
        foreach (var dir in Directory.GetDirectories(path))
        {
            RemoveReadOnlyAttributes(dir);
        }
        
        foreach (var file in Directory.GetFiles(path))
        {
            File.SetAttributes(file, FileAttributes.Normal);
        }
        
        new DirectoryInfo(path).Attributes = FileAttributes.Normal;
    }
}