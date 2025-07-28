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
using TextCopy;

namespace GitGen.Tests.Services;

public class GenerationOrchestratorTests : TestBase
{
    private readonly ISecureConfigurationService _secureConfig;
    private readonly ConfigurationService _configService;
    private readonly ConfigurationWizardService _wizardService;
    private readonly GitAnalysisService _gitService;
    private readonly CommitMessageGenerator _generator;
    private readonly IGenerationOrchestrator _orchestrator;
    private readonly ProviderFactory _providerFactory;
    private readonly ICommitMessageProvider _provider;

    public GenerationOrchestratorTests()
    {
        _secureConfig = Substitute.For<ISecureConfigurationService>();
        _configService = new ConfigurationService(Logger, _secureConfig);
        _provider = Substitute.For<ICommitMessageProvider>();
        _providerFactory = Substitute.For<ProviderFactory>(CreateServiceProvider(), Logger);
        _providerFactory.CreateProvider(Arg.Any<ModelConfiguration>()).Returns(_provider);
        _wizardService = new ConfigurationWizardService(Logger, _providerFactory, _configService, _secureConfig);
        _gitService = Substitute.For<GitAnalysisService>(Logger);
        _generator = new CommitMessageGenerator(_providerFactory, Logger);
        var truncationService = Substitute.For<GitDiffTruncationService>(Logger);
        
        _orchestrator = new GenerationOrchestrator(
            Logger,
            _secureConfig,
            _configService,
            _wizardService,
            _gitService,
            _generator,
            truncationService);
    }

    public class ExecuteAsyncTests : GenerationOrchestratorTests
    {
        [Fact]
        public async Task ExecuteAsync_WithValidModel_ReturnsSuccess()
        {
            // Arrange
            var model = CreateTestModel("gpt-4");
            _configService.LoadConfigurationAsync("gpt-4").Returns(model);
            _gitService.IsGitRepository().Returns(true);
            _gitService.GetRepositoryDiff().Returns("diff content");
            _generator.GenerateAsync(model, "diff content", null).Returns(new CommitMessageResult
            {
                Message = "feat: test commit",
                InputTokens = 100,
                OutputTokens = 50
            });
            _secureConfig.LoadSettingsAsync().Returns(new GitGenSettings
            {
                Settings = new AppSettings { CopyToClipboard = false, ShowTokenUsage = false }
            });

            // Act
            var result = await _orchestrator.ExecuteAsync("gpt-4", null, false);

            // Assert
            result.Should().Be(0);
            await _configService.Received(1).LoadConfigurationAsync("gpt-4");
            await _provider.Received(1).GenerateCommitMessageAsync("diff content", null);
        }

        [Fact]
        public async Task ExecuteAsync_WithAlias_LoadsCorrectModel()
        {
            // Arrange
            var model = CreateTestModel("claude-3", aliases: new List<string> { "smart" });
            _configService.LoadConfigurationAsync("smart").Returns(model);
            _gitService.IsGitRepository().Returns(true);
            _gitService.GetRepositoryDiff().Returns("diff content");
            _generator.GenerateAsync(model, "diff content", null).Returns(new CommitMessageResult
            {
                Message = "fix: bug fix"
            });
            _secureConfig.LoadSettingsAsync().Returns(CreateDefaultSettings());

            // Act
            var result = await _orchestrator.ExecuteAsync("smart", null, false);

            // Assert
            result.Should().Be(0);
            await _configService.Received(1).LoadConfigurationAsync("smart");
        }

        [Fact]
        public async Task ExecuteAsync_WithCustomInstruction_PassesItToGenerator()
        {
            // Arrange
            var model = CreateTestModel();
            var customInstruction = "make it a haiku";
            _configService.LoadConfigurationAsync(null).Returns(model);
            _gitService.IsGitRepository().Returns(true);
            _gitService.GetRepositoryDiff().Returns("diff");
            _generator.GenerateAsync(model, "diff", customInstruction).Returns(new CommitMessageResult
            {
                Message = "code flows like water\nbugs dissolve in morning dew\nfeatures bloom anew"
            });
            _secureConfig.LoadSettingsAsync().Returns(CreateDefaultSettings());

            // Act
            var result = await _orchestrator.ExecuteAsync(null, customInstruction, false);

            // Assert
            result.Should().Be(0);
            await _generator.Received(1).GenerateAsync(model, "diff", customInstruction);
        }
    }

    public class ConfigurationLoadingTests : GenerationOrchestratorTests
    {
        [Fact]
        public async Task ExecuteAsync_WithNoModelName_LoadsDefaultModel()
        {
            // Arrange
            var defaultModel = CreateTestModel("default-model");
            _configService.LoadConfigurationAsync(null).Returns(defaultModel);
            SetupValidGitRepository();
            _generator.GenerateAsync(Arg.Any<ModelConfiguration>(), Arg.Any<string>(), null)
                .Returns(CreateTestCommitMessageResult());
            _secureConfig.LoadSettingsAsync().Returns(CreateDefaultSettings());

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, false);

            // Assert
            result.Should().Be(0);
            await _configService.Received(1).LoadConfigurationAsync(null);
        }

        [Fact]
        public async Task ExecuteAsync_WithNonExistentModel_ShowsSuggestionsAndReturnsError()
        {
            // Arrange
            _configService.LoadConfigurationAsync("unknown-model").Returns((ModelConfiguration?)null);
            _secureConfig.LoadSettingsAsync().Returns(new GitGenSettings
            {
                Models = new List<ModelConfiguration>
                {
                    CreateTestModel("gpt-4"),
                    CreateTestModel("claude-3", aliases: new List<string> { "smart" })
                },
                Settings = new AppSettings(),
                DefaultModelId = "model-1"
            });

            // Act
            var result = await _orchestrator.ExecuteAsync("unknown-model", null, false);

            // Assert
            result.Should().Be(1);
            Logger.Received().Error(Arg.Is<string>(s => s.Contains("Model or alias 'unknown-model' not found")));
            Logger.Received().Information(Arg.Is<string>(s => s.Contains("Did you mean one of these?")));
        }
    }

    public class ModelHealingTests : GenerationOrchestratorTests
    {
        [Fact]
        public async Task ExecuteAsync_WithNoSpecificModelAndNeedsHealing_HealsDefault()
        {
            // Arrange
            _configService.LoadConfigurationAsync(null).Returns((ModelConfiguration?)null);
            _configService.NeedsDefaultModelHealingAsync().Returns(true);
            _secureConfig.HealDefaultModelAsync(Logger).Returns(true);
            
            var healedModel = CreateTestModel("healed-model");
            _configService.LoadConfigurationAsync(null)
                .Returns(x => null, x => healedModel); // First call returns null, second returns healed model
            
            SetupValidGitRepository();
            _generator.GenerateAsync(Arg.Any<ModelConfiguration>(), Arg.Any<string>(), null)
                .Returns(CreateTestCommitMessageResult());
            _secureConfig.LoadSettingsAsync().Returns(CreateDefaultSettings());

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, false);

            // Assert
            result.Should().Be(0);
            await _secureConfig.Received(1).HealDefaultModelAsync(Logger);
            await _configService.Received(2).LoadConfigurationAsync(null);
        }

        [Fact]
        public async Task ExecuteAsync_WithSpecificModelRequest_DoesNotHeal()
        {
            // Arrange
            _configService.LoadConfigurationAsync("specific-model").Returns((ModelConfiguration?)null);
            _configService.NeedsDefaultModelHealingAsync().Returns(true);
            _secureConfig.LoadSettingsAsync().Returns(CreateDefaultSettings());

            // Act
            var result = await _orchestrator.ExecuteAsync("specific-model", null, false);

            // Assert
            result.Should().Be(1);
            await _secureConfig.DidNotReceive().HealDefaultModelAsync(Arg.Any<IConsoleLogger>());
        }

        [Fact]
        public async Task ExecuteAsync_HealingFails_ReturnsError()
        {
            // Arrange
            _configService.LoadConfigurationAsync(null).Returns((ModelConfiguration?)null);
            _configService.NeedsDefaultModelHealingAsync().Returns(true);
            _secureConfig.HealDefaultModelAsync(Logger).Returns(false);

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, false);

            // Assert
            result.Should().Be(1);
            Logger.Received().Error(Arg.Is<string>(s => s.Contains("Failed to heal default model configuration")));
        }
    }

    public class ConfigurationWizardTests : GenerationOrchestratorTests
    {
        [Fact]
        public async Task ExecuteAsync_WithNoModels_RunsWizard()
        {
            // Arrange
            _configService.LoadConfigurationAsync(null).Returns((ModelConfiguration?)null);
            _configService.NeedsDefaultModelHealingAsync().Returns(false);
            
            var wizardModel = CreateTestModel("wizard-model");
            _wizardService.RunWizardAsync().Returns(wizardModel);
            
            SetupValidGitRepository();
            _generator.GenerateAsync(Arg.Any<ModelConfiguration>(), Arg.Any<string>(), null)
                .Returns(CreateTestCommitMessageResult());
            _secureConfig.LoadSettingsAsync().Returns(CreateDefaultSettings());

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, false);

            // Assert
            result.Should().Be(0);
            await _wizardService.Received(1).RunWizardAsync();
            Logger.Received().Information(Arg.Is<string>(s => s.Contains("Starting configuration wizard")));
        }

        [Fact]
        public async Task ExecuteAsync_WizardCancelled_ReturnsError()
        {
            // Arrange
            _configService.LoadConfigurationAsync(null).Returns((ModelConfiguration?)null);
            _configService.NeedsDefaultModelHealingAsync().Returns(false);
            _wizardService.RunWizardAsync().Returns((ModelConfiguration?)null);

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, false);

            // Assert
            result.Should().Be(1);
            Logger.Received().Error(Arg.Is<string>(s => s.Contains("Configuration setup failed")));
        }
    }

    public class PreviewModeTests : GenerationOrchestratorTests
    {
        [Fact]
        public async Task ExecuteAsync_InPreviewMode_ShowsPreviewWithoutGenerating()
        {
            // Arrange
            var model = CreateTestModel("gpt-4", pricing: new PricingInfo
            {
                InputPer1M = 30m,
                OutputPer1M = 60m,
                CurrencyCode = "USD"
            });
            _configService.LoadConfigurationAsync(null).Returns(model);
            _gitService.IsGitRepository().Returns(true);
            _gitService.GetRepositoryDiff().Returns("diff content\nwith multiple lines");

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, true);

            // Assert
            result.Should().Be(0);
            await _generator.DidNotReceive().GenerateAsync(Arg.Any<ModelConfiguration>(), Arg.Any<string>(), Arg.Any<string>());
            Logger.Received().Information("[PREVIEW MODE - No LLM call will be made]");
            Logger.Received().Information(Arg.Is<string>(s => s.Contains("Git diff: 2 lines")));
            Logger.Received().Information(Arg.Is<string>(s => s.Contains("Estimated cost:")));
        }

        [Fact]
        public async Task ExecuteAsync_PreviewModeWithCustomInstruction_ShowsInstruction()
        {
            // Arrange
            var model = CreateTestModel();
            _configService.LoadConfigurationAsync(null).Returns(model);
            _gitService.IsGitRepository().Returns(true);
            _gitService.GetRepositoryDiff().Returns("diff");
            var customInstruction = "make it funny";

            // Act
            var result = await _orchestrator.ExecuteAsync(null, customInstruction, true);

            // Assert
            result.Should().Be(0);
            Logger.Received().Information(Arg.Is<string>(s => s.Contains($"Custom instruction: \"{customInstruction}\"")));
        }

        [Fact]
        public async Task ExecuteAsync_PreviewModeNoRepository_ShowsError()
        {
            // Arrange
            var model = CreateTestModel();
            _configService.LoadConfigurationAsync(null).Returns(model);
            _gitService.IsGitRepository().Returns(false);

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, true);

            // Assert
            result.Should().Be(0);
            Logger.Received().Error(Arg.Is<string>(s => s.Contains("Not a git repository")));
        }

        [Fact]
        public async Task ExecuteAsync_PreviewModeNoChanges_ShowsInfo()
        {
            // Arrange
            var model = CreateTestModel();
            _configService.LoadConfigurationAsync(null).Returns(model);
            _gitService.IsGitRepository().Returns(true);
            _gitService.GetRepositoryDiff().Returns("");

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, true);

            // Assert
            result.Should().Be(0);
            Logger.Received().Information(Arg.Is<string>(s => s.Contains("No uncommitted changes")));
        }
    }

    public class CommitGenerationTests : GenerationOrchestratorTests
    {
        [Fact]
        public async Task ExecuteAsync_GeneratesCommitSuccessfully()
        {
            // Arrange
            var model = CreateTestModel();
            _configService.LoadConfigurationAsync(null).Returns(model);
            SetupValidGitRepository();
            
            var commitResult = new CommitMessageResult
            {
                Message = "feat: awesome feature",
                InputTokens = 1000,
                OutputTokens = 200
            };
            _generator.GenerateAsync(model, "diff", null).Returns(commitResult);
            
            var settings = CreateDefaultSettings();
            settings.Settings.ShowTokenUsage = true;
            settings.Settings.CopyToClipboard = true;
            _secureConfig.LoadSettingsAsync().Returns(settings);

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, false);

            // Assert
            result.Should().Be(0);
            Logger.Received().Success(Arg.Is<string>(s => s.Contains("Generated Commit Message")));
            Logger.Received().Highlight(Arg.Is<string>(s => s.Contains("feat: awesome feature")), ConsoleColor.DarkCyan);
            Logger.Received().Muted(Arg.Is<string>(s => s.Contains("1,000 input tokens") && s.Contains("200 output tokens")));
        }

        [Fact]
        public async Task ExecuteAsync_WithCostCalculation_ShowsCost()
        {
            // Arrange
            var model = CreateTestModel("gpt-4", pricing: new PricingInfo
            {
                InputPer1M = 30m,
                OutputPer1M = 60m,
                CurrencyCode = "USD"
            });
            _configService.LoadConfigurationAsync(null).Returns(model);
            SetupValidGitRepository();
            
            var commitResult = new CommitMessageResult
            {
                Message = "test",
                InputTokens = 1000,
                OutputTokens = 200
            };
            _generator.GenerateAsync(model, "diff", null).Returns(commitResult);
            
            var settings = CreateDefaultSettings();
            settings.Settings.ShowTokenUsage = true;
            _secureConfig.LoadSettingsAsync().Returns(settings);

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, false);

            // Assert
            result.Should().Be(0);
            Logger.Received().Muted(Arg.Is<string>(s => s.Contains("Estimated cost: $")));
        }

        [Fact]
        public async Task ExecuteAsync_NoGitRepository_ShowsError()
        {
            // Arrange
            var model = CreateTestModel();
            _configService.LoadConfigurationAsync(null).Returns(model);
            _gitService.IsGitRepository().Returns(false);

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, false);

            // Assert
            result.Should().Be(0);
            Logger.Received().Error(Arg.Is<string>(s => s.Contains("Not a git repository")));
            await _generator.DidNotReceive().GenerateAsync(Arg.Any<ModelConfiguration>(), Arg.Any<string>(), Arg.Any<string>());
        }

        [Fact]
        public async Task ExecuteAsync_NoUncommittedChanges_ShowsInfo()
        {
            // Arrange
            var model = CreateTestModel();
            _configService.LoadConfigurationAsync(null).Returns(model);
            _gitService.IsGitRepository().Returns(true);
            _gitService.GetRepositoryDiff().Returns("");

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, false);

            // Assert
            result.Should().Be(0);
            Logger.Received().Information(Arg.Is<string>(s => s.Contains("No uncommitted changes")));
            await _generator.DidNotReceive().GenerateAsync(Arg.Any<ModelConfiguration>(), Arg.Any<string>(), Arg.Any<string>());
        }
    }

    public class ModelSuggestionTests : GenerationOrchestratorTests
    {
        [Fact]
        public async Task ExecuteAsync_ShowsPartialMatches_WhenEnabled()
        {
            // Arrange
            _configService.LoadConfigurationAsync("sma").Returns((ModelConfiguration?)null);
            
            var smartModel = CreateTestModel("claude-3", aliases: new List<string> { "smart" });
            var otherModel = CreateTestModel("gpt-4");
            
            var settings = new GitGenSettings
            {
                Models = new List<ModelConfiguration> { smartModel, otherModel },
                Settings = new AppSettings
                {
                    EnablePartialAliasMatching = true,
                    MinimumAliasMatchLength = 2
                },
                DefaultModelId = "model-1"
            };
            
            _secureConfig.LoadSettingsAsync().Returns(settings);
            _secureConfig.GetModelsByPartialMatchAsync("sma").Returns(new List<ModelConfiguration> { smartModel });

            // Act
            var result = await _orchestrator.ExecuteAsync("sma", null, false);

            // Assert
            result.Should().Be(1);
            Logger.Received().Information(Arg.Is<string>(s => s.Contains("Did you mean one of these models matching 'sma'?")));
            Logger.Received().Success(Arg.Is<string>(s => s.Contains("claude-3")));
        }

        [Fact]
        public async Task ExecuteAsync_ShowsAllModels_WhenNoPartialMatch()
        {
            // Arrange
            _configService.LoadConfigurationAsync("xyz").Returns((ModelConfiguration?)null);
            
            var model1 = CreateTestModel("gpt-4");
            var model2 = CreateTestModel("claude-3");
            
            var settings = new GitGenSettings
            {
                Models = new List<ModelConfiguration> { model1, model2 },
                Settings = new AppSettings
                {
                    EnablePartialAliasMatching = true,
                    MinimumAliasMatchLength = 2
                }
            };
            
            _secureConfig.LoadSettingsAsync().Returns(settings);
            _secureConfig.GetModelsByPartialMatchAsync("xyz").Returns(new List<ModelConfiguration>());

            // Act
            var result = await _orchestrator.ExecuteAsync("xyz", null, false);

            // Assert
            result.Should().Be(1);
            Logger.Received().Information(Arg.Is<string>(s => s.Contains("No models match 'xyz'. Here are all available models:")));
        }

        [Fact]
        public async Task ExecuteAsync_ShowsDefaultModelIndicator()
        {
            // Arrange
            _configService.LoadConfigurationAsync("unknown").Returns((ModelConfiguration?)null);
            
            var defaultModel = CreateTestModel("gpt-4", id: "default-id");
            var otherModel = CreateTestModel("claude-3");
            
            var settings = new GitGenSettings
            {
                Models = new List<ModelConfiguration> { defaultModel, otherModel },
                Settings = new AppSettings(),
                DefaultModelId = "default-id"
            };
            
            _secureConfig.LoadSettingsAsync().Returns(settings);

            // Act
            var result = await _orchestrator.ExecuteAsync("unknown", null, false);

            // Assert
            result.Should().Be(1);
            Logger.Received().Success(Arg.Is<string>(s => s.Contains("gpt-4") && s.Contains("⭐ (default)")));
        }
    }

    public class ErrorHandlingTests : GenerationOrchestratorTests
    {
        [Fact]
        public async Task ExecuteAsync_HandlesAuthenticationException()
        {
            // Arrange
            var model = CreateTestModel();
            _configService.LoadConfigurationAsync(null).Returns(model);
            SetupValidGitRepository();
            
            _generator.GenerateAsync(Arg.Any<ModelConfiguration>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns<CommitMessageResult>(x => throw new AuthenticationException("Invalid API key"));
            
            _secureConfig.LoadSettingsAsync().Returns(CreateDefaultSettings());

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, false);

            // Assert
            result.Should().Be(0); // Note: Current implementation returns 0 even on auth error
            Logger.Received().Error(Arg.Is<string>(s => s.Contains("Authentication failed")));
            Logger.Received().Information(Arg.Is<string>(s => s.Contains("Check your API key")));
        }

        [Fact]
        public async Task ExecuteAsync_HandlesGeneralException()
        {
            // Arrange
            var model = CreateTestModel();
            _configService.LoadConfigurationAsync(null).Returns(model);
            SetupValidGitRepository();
            
            _generator.GenerateAsync(Arg.Any<ModelConfiguration>(), Arg.Any<string>(), Arg.Any<string>())
                .Returns<CommitMessageResult>(x => throw new Exception("Something went wrong"));
            
            _secureConfig.LoadSettingsAsync().Returns(CreateDefaultSettings());

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, false);

            // Assert
            result.Should().Be(0); // Note: Current implementation returns 0 even on general error
            Logger.Received().Error(Arg.Is<string>(s => s.Contains("Unexpected error occurred")));
        }

        [Fact]
        public async Task ExecuteAsync_HandlesExceptionDuringConfiguration()
        {
            // Arrange
            _configService.LoadConfigurationAsync(null).Returns<ModelConfiguration?>(x => throw new Exception("Config error"));

            // Act
            var result = await _orchestrator.ExecuteAsync(null, null, false);

            // Assert
            result.Should().Be(1);
            Logger.Received().Error(Arg.Any<Exception>(), "Orchestration failed");
        }
    }

    // Helper methods
    private static ModelConfiguration CreateTestModel(string name = "test-model", 
        string? id = null,
        List<string>? aliases = null,
        PricingInfo? pricing = null)
    {
        return new ModelConfiguration
        {
            Id = id ?? Guid.NewGuid().ToString(),
            Name = name,
            Type = "openai-compatible",
            Provider = "TestProvider",
            Url = "https://api.test.com",
            ModelId = name,
            ApiKey = "test-key",
            RequiresAuth = true,
            Temperature = 0.7,
            MaxOutputTokens = 5000,
            Aliases = aliases ?? new List<string>(),
            Pricing = pricing
        };
    }

    private static GitGenSettings CreateDefaultSettings()
    {
        return new GitGenSettings
        {
            Models = new List<ModelConfiguration>(),
            Settings = new AppSettings
            {
                CopyToClipboard = false,
                ShowTokenUsage = false,
                EnablePartialAliasMatching = false
            }
        };
    }

    private static CommitMessageResult CreateTestCommitMessageResult()
    {
        return new CommitMessageResult
        {
            Message = "test: default commit message",
            InputTokens = 100,
            OutputTokens = 50
        };
    }

    private void SetupValidGitRepository()
    {
        _gitService.IsGitRepository().Returns(true);
        _gitService.GetRepositoryDiff().Returns("diff content");
    }
}