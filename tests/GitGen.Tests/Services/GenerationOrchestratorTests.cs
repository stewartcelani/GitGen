using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using GitGen.Configuration;
using GitGen.Exceptions;
using GitGen.Providers;
using GitGen.Services;
using LibGit2Sharp;
using Moq;
using Xunit;

namespace GitGen.Tests.Services;

public class GenerationOrchestratorTests
{
    [Fact]
    public async Task ExecuteAsync_WithValidConfiguration_GeneratesCommitMessage()
    {
        // Arrange
        var modelConfig = CreateTestModelConfiguration();
        var commitResult = new CommitMessageResult
        {
            Message = "Test commit message",
            InputTokens = 100,
            OutputTokens = 50,
            TotalTokens = 150
        };
        var settings = CreateTestSettings();

        var testHarness = new TestHarness()
            .WithModelConfiguration(modelConfig)
            .WithGitRepository(true)
            .WithGitDiff("test diff content")
            .WithSettings(settings)
            .WithCommitResult(commitResult);

        // Act
        var result = await testHarness.Orchestrator.ExecuteAsync(null, null, false);

        // Assert
        result.Should().Be(0);
        testHarness.VerifyCommitGenerated("test diff content");
    }

    [Fact]
    public async Task ExecuteAsync_WithSpecificModelNotFound_ReturnsError()
    {
        // Arrange
        var testHarness = new TestHarness()
            .WithModelConfiguration(null, "nonexistent")
            .WithSettings(CreateTestSettings());

        // Act
        var result = await testHarness.Orchestrator.ExecuteAsync("nonexistent", null, false);

        // Assert
        result.Should().Be(1);
        testHarness.VerifyErrorLogged("not found");
    }

    [Fact]
    public async Task ExecuteAsync_WhenNotGitRepository_ShowsErrorAndReturns()
    {
        // Arrange
        var modelConfig = CreateTestModelConfiguration();
        var testHarness = new TestHarness()
            .WithModelConfiguration(modelConfig)
            .WithGitRepository(false);

        // Act
        var result = await testHarness.Orchestrator.ExecuteAsync(null, null, false);

        // Assert
        result.Should().Be(0);
        testHarness.VerifyErrorLogged("not a Git repository");
        testHarness.VerifyNoCommitGenerated();
    }

    [Fact]
    public async Task ExecuteAsync_WithNoUncommittedChanges_ShowsInfoAndReturns()
    {
        // Arrange
        var modelConfig = CreateTestModelConfiguration();
        var testHarness = new TestHarness()
            .WithModelConfiguration(modelConfig)
            .WithGitRepository(true)
            .WithGitDiff("");

        // Act
        var result = await testHarness.Orchestrator.ExecuteAsync(null, null, false);

        // Assert
        result.Should().Be(0);
        testHarness.VerifyInfoLogged("No uncommitted changes");
        testHarness.VerifyNoCommitGenerated();
    }

    [Fact]
    public async Task ExecuteAsync_InPreviewMode_DoesNotCallGenerator()
    {
        // Arrange
        var modelConfig = CreateTestModelConfiguration();
        var testHarness = new TestHarness()
            .WithModelConfiguration(modelConfig)
            .WithGitRepository(true)
            .WithGitDiff("test diff");

        // Act
        var result = await testHarness.Orchestrator.ExecuteAsync(null, null, true);

        // Assert
        result.Should().Be(0);
        testHarness.VerifyInfoLogged("[PREVIEW MODE");
        testHarness.VerifyNoCommitGenerated();
    }

    [Fact]
    public async Task ExecuteAsync_WithNoConfigurationAndWizardSuccess_UsesWizardConfig()
    {
        // Arrange
        var wizardConfig = CreateTestModelConfiguration();
        var testHarness = new TestHarness()
            .WithModelConfiguration(null)
            .WithWizardResult(wizardConfig)
            .WithGitRepository(true)
            .WithGitDiff("test diff")
            .WithSettings(CreateTestSettings())
            .WithCommitResult(new CommitMessageResult { Message = "Test message" });

        // Act
        var result = await testHarness.Orchestrator.ExecuteAsync(null, null, false);

        // Assert
        result.Should().Be(0);
        testHarness.VerifyWizardRun();
        testHarness.VerifyCommitGenerated("test diff");
    }

    [Fact]
    public async Task ExecuteAsync_WithContextLengthExceeded_AsksForRetry()
    {
        // Arrange
        var modelConfig = CreateTestModelConfiguration();
        var contextException = new ContextLengthExceededException("Context too long", 
            maxContextLength: 4000, requestedTokens: 5000);
        
        var testHarness = new TestHarness()
            .WithModelConfiguration(modelConfig)
            .WithGitRepository(true)
            .WithGitDiff("very long diff")
            .WithSettings(CreateTestSettings())
            .WithException(contextException)
            .WithUserInput("n");

        // Act
        var result = await testHarness.Orchestrator.ExecuteAsync(null, null, false);

        // Assert
        result.Should().Be(0);
        testHarness.VerifyErrorLogged("Context length exceeded");
        testHarness.VerifyUserPrompted();
    }

    [Fact]
    public async Task ExecuteAsync_WithContextLengthExceededAndRetryAccepted_UsesTruncatedDiff()
    {
        // Arrange
        var modelConfig = CreateTestModelConfiguration();
        var contextException = new ContextLengthExceededException("Context too long");
        var commitResult = new CommitMessageResult { Message = "Truncated commit message" };
        
        var testHarness = new TestHarness()
            .WithModelConfiguration(modelConfig)
            .WithGitRepository(true)
            .WithGitDiff("very long diff")
            .WithSettings(CreateTestSettings())
            .WithException(contextException)
            .WithUserInput("y")
            .WithTruncatedDiff("truncated diff")
            .WithCommitResult(commitResult, isTruncated: true);

        // Act
        var result = await testHarness.Orchestrator.ExecuteAsync(null, null, false);

        // Assert
        result.Should().Be(0);
        testHarness.VerifyDiffTruncated("very long diff");
        testHarness.VerifyCommitGenerated("truncated diff");
    }

    [Fact]
    public async Task ExecuteAsync_WithAuthenticationException_ShowsHelpfulError()
    {
        // Arrange
        var modelConfig = CreateTestModelConfiguration();
        var authException = new AuthenticationException("Invalid API key");
        
        var testHarness = new TestHarness()
            .WithModelConfiguration(modelConfig)
            .WithGitRepository(true)
            .WithGitDiff("test diff")
            .WithSettings(CreateTestSettings())
            .WithException(authException);

        // Act
        var result = await testHarness.Orchestrator.ExecuteAsync(null, null, false);

        // Assert
        result.Should().Be(0);
        testHarness.VerifyErrorLogged("Authentication failed");
        testHarness.VerifyInfoLogged("gitgen config");
    }

    [Fact]
    public async Task ExecuteAsync_WithFreeModelAndConfirmationRequired_AsksForConfirmation()
    {
        // Arrange
        var modelConfig = CreateTestModelConfiguration();
        modelConfig.Aliases = new List<string> { "free" }; // Mark as free model
        var settings = CreateTestSettings();
        settings.Settings.RequireFreeModelConfirmation = true;
        
        var testHarness = new TestHarness()
            .WithModelConfiguration(modelConfig)
            .WithGitRepository(true)
            .WithGitDiff("test diff")
            .WithSettings(settings)
            .WithUserInput("y")
            .WithCommitResult(new CommitMessageResult { Message = "Test message" });

        // Act
        var result = await testHarness.Orchestrator.ExecuteAsync(null, null, false);

        // Assert
        result.Should().Be(0);
        testHarness.VerifyWarningLogged("Free/Public Model Detected");
        testHarness.VerifyUserPrompted();
    }

    [Fact]
    public async Task ExecuteAsync_WithUnexpectedException_ReturnsErrorCode()
    {
        // Arrange
        var testHarness = new TestHarness()
            .WithConfigurationException(new InvalidOperationException("Unexpected error"));

        // Act
        var result = await testHarness.Orchestrator.ExecuteAsync(null, null, false);

        // Assert
        result.Should().Be(1);
        testHarness.VerifyErrorLogged("An unexpected error occurred");
    }

    // Helper methods
    private static ModelConfiguration CreateTestModelConfiguration()
    {
        return new ModelConfiguration
        {
            Id = "test-model",
            Name = "test-model",
            Type = "openai-compatible",
            Url = "https://api.test.com",
            ModelId = "test-model-id",
            Provider = "TestProvider",
            MaxOutputTokens = 2000,
            Temperature = 0.2,
            ApiKey = "test-api-key"
        };
    }

    private static GitGenSettings CreateTestSettings()
    {
        return new GitGenSettings
        {
            Version = "4.0",
            DefaultModelId = "test-model",
            Models = new List<ModelConfiguration> { CreateTestModelConfiguration() },
            Settings = new AppSettings
            {
                ShowTokenUsage = true,
                CopyToClipboard = true,
                RequireFreeModelConfirmation = false,
                RequirePromptConfirmation = false
            }
        };
    }

    // Test harness to simplify test setup and verification
    private class TestHarness
    {
        private readonly Mock<IConsoleLogger> _loggerMock = new();
        private readonly Mock<ISecureConfigurationService> _secureConfigMock = new();
        private readonly Mock<IConsoleInput> _consoleInputMock = new();
        private readonly List<string> _userInputs = new();
        private int _userInputIndex = 0;

        // Test doubles
        private ModelConfiguration? _modelConfig;
        private string? _specificModelRequested;
        private ModelConfiguration? _wizardResult;
        private bool _needsHealing = false;
        private bool _isGitRepo = true;
        private string _gitDiff = "";
        private Exception? _generatorException;
        private Exception? _configException;
        private CommitMessageResult? _commitResult;
        private CommitMessageResult? _truncatedCommitResult;
        private string _truncatedDiff = "";
        private bool _wizardRun = false;
        private string? _lastGeneratedDiff = null;

        public GenerationOrchestrator Orchestrator { get; }

        public TestHarness()
        {
            // Setup console input to return queued inputs
            _consoleInputMock.Setup(x => x.ReadLine())
                .Returns(() => _userInputIndex < _userInputs.Count ? _userInputs[_userInputIndex++] : "n");

            // Create test doubles
            var configService = new TestConfigurationService(this);
            var wizardService = new TestWizardService(this);
            var gitService = new TestGitService(this);
            var generator = new TestCommitMessageGenerator(this);
            var truncationService = new TestTruncationService(this);

            Orchestrator = new GenerationOrchestrator(
                _loggerMock.Object,
                _secureConfigMock.Object,
                configService,
                wizardService,
                gitService,
                generator,
                truncationService,
                _consoleInputMock.Object);
        }

        public TestHarness WithModelConfiguration(ModelConfiguration? config, string? specificModelRequested = null)
        {
            _modelConfig = config;
            _specificModelRequested = specificModelRequested;
            return this;
        }

        public TestHarness WithSettings(GitGenSettings settings)
        {
            _secureConfigMock.Setup(x => x.LoadSettingsAsync()).ReturnsAsync(settings);
            _secureConfigMock.Setup(x => x.GetModelsByPartialMatchAsync(It.IsAny<string>()))
                .ReturnsAsync(settings.Models);
            return this;
        }

        public TestHarness WithWizardResult(ModelConfiguration config)
        {
            _wizardResult = config;
            return this;
        }

        public TestHarness WithGitRepository(bool isRepo)
        {
            _isGitRepo = isRepo;
            return this;
        }

        public TestHarness WithGitDiff(string diff)
        {
            _gitDiff = diff;
            return this;
        }

        public TestHarness WithCommitResult(CommitMessageResult result, bool isTruncated = false)
        {
            if (isTruncated)
                _truncatedCommitResult = result;
            else
                _commitResult = result;
            return this;
        }

        public TestHarness WithException(Exception ex)
        {
            _generatorException = ex;
            return this;
        }

        public TestHarness WithConfigurationException(Exception ex)
        {
            _configException = ex;
            return this;
        }

        public TestHarness WithUserInput(params string[] inputs)
        {
            _userInputs.AddRange(inputs);
            return this;
        }

        public TestHarness WithTruncatedDiff(string truncatedDiff)
        {
            _truncatedDiff = truncatedDiff;
            return this;
        }

        public TestHarness WithNeedsHealing(bool needsHealing)
        {
            _needsHealing = needsHealing;
            _secureConfigMock.Setup(x => x.HealDefaultModelAsync(It.IsAny<IConsoleLogger>()))
                .ReturnsAsync(true);
            return this;
        }

        public void VerifyCommitGenerated(string expectedDiff)
        {
            _lastGeneratedDiff.Should().Be(expectedDiff);
        }

        public void VerifyNoCommitGenerated()
        {
            _lastGeneratedDiff.Should().BeNull();
        }

        public void VerifyWizardRun()
        {
            _wizardRun.Should().BeTrue();
        }

        public void VerifyUserPrompted()
        {
            _consoleInputMock.Verify(x => x.ReadLine(), Times.AtLeastOnce);
        }

        public void VerifyDiffTruncated(string originalDiff)
        {
            // Verified by the fact that truncated diff was used
            _lastGeneratedDiff.Should().Be(_truncatedDiff);
        }

        public void VerifyErrorLogged(string containing)
        {
            // Check both overloads - with and without parameters
            _loggerMock.Verify(x => x.Error(It.Is<string>(s => s.Contains(containing)), It.IsAny<object[]>()), Times.AtLeastOnce);
        }

        public void VerifyInfoLogged(string containing)
        {
            _loggerMock.Verify(x => x.Information(It.Is<string>(s => s.Contains(containing))), Times.AtLeastOnce);
        }

        public void VerifyWarningLogged(string containing)
        {
            _loggerMock.Verify(x => x.Warning(It.Is<string>(s => s.Contains(containing))), Times.AtLeastOnce);
        }

        // Test double implementations
        private class TestConfigurationService : ConfigurationService
        {
            private readonly TestHarness _harness;

            public TestConfigurationService(TestHarness harness) 
                : base(harness._loggerMock.Object, harness._secureConfigMock.Object)
            {
                _harness = harness;
            }

            public override async Task<ModelConfiguration?> LoadConfigurationAsync(string? modelName = null)
            {
                if (_harness._configException != null)
                    throw _harness._configException;

                if (modelName != null && modelName != _harness._specificModelRequested)
                    return null;

                return await Task.FromResult(_harness._modelConfig);
            }

            public override async Task<bool> NeedsDefaultModelHealingAsync()
            {
                return await Task.FromResult(_harness._needsHealing);
            }
        }

        private class TestWizardService : ConfigurationWizardService
        {
            private readonly TestHarness _harness;

            public TestWizardService(TestHarness harness) 
                : base(harness._loggerMock.Object, null!, null!, harness._consoleInputMock.Object, null)
            {
                _harness = harness;
            }

            public override async Task<ModelConfiguration?> RunWizardAsync()
            {
                _harness._wizardRun = true;
                return await Task.FromResult(_harness._wizardResult);
            }
        }

        private class TestGitService : GitAnalysisService
        {
            private readonly TestHarness _harness;

            public TestGitService(TestHarness harness) : base(harness._loggerMock.Object)
            {
                _harness = harness;
            }

            public override bool IsGitRepository()
            {
                return _harness._isGitRepo;
            }

            public override string GetRepositoryDiff()
            {
                return _harness._gitDiff;
            }
        }

        private class TestCommitMessageGenerator : CommitMessageGenerator
        {
            private readonly TestHarness _harness;

            public TestCommitMessageGenerator(TestHarness harness) 
                : base(null!, harness._loggerMock.Object)
            {
                _harness = harness;
            }

            public override async Task<CommitMessageResult> GenerateAsync(ModelConfiguration config, string diff, string? instruction)
            {
                _harness._lastGeneratedDiff = diff;

                if (_harness._generatorException != null)
                    throw _harness._generatorException;

                var result = diff == _harness._truncatedDiff ? _harness._truncatedCommitResult : _harness._commitResult;
                if (result == null)
                    throw new InvalidOperationException("No commit result configured for this test");

                return await Task.FromResult(result);
            }
        }

        private class TestTruncationService : GitDiffTruncationService
        {
            private readonly TestHarness _harness;

            public TestTruncationService(TestHarness harness) : base(harness._loggerMock.Object)
            {
                _harness = harness;
            }

            public override string TruncateDiff(string diff, int contextLimit, int systemPromptTokens)
            {
                return _harness._truncatedDiff;
            }
        }
    }
}