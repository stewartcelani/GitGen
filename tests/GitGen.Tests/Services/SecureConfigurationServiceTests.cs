using FluentAssertions;
using GitGen.Configuration;
using GitGen.Services;
using System.IO.Abstractions.TestingHelpers;
using Xunit;

namespace GitGen.Tests.Services;

public class SecureConfigurationServiceTests : TestBase
{
    private readonly SecureConfigurationService _service;
    private readonly string _configPath;

    public SecureConfigurationServiceTests()
    {
        _service = new SecureConfigurationService(Logger);
        
        // Get the actual config path using reflection
        var field = typeof(SecureConfigurationService).GetField("_configPath", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        _configPath = (string)field!.GetValue(_service)!;
    }

    private ModelConfiguration CreateValidTestModel(string id, string name, List<string>? aliases = null)
    {
        var model = new ModelConfiguration
        {
            Id = id,
            Name = name,
            Type = "openai-compatible",
            Provider = "TestProvider",
            Url = "https://api.test.com/v1/chat/completions",
            ModelId = "test-model-id",
            ApiKey = "test-key-123456789",
            RequiresAuth = true,
            Temperature = 0.2,
            MaxOutputTokens = 2000
        };
        
        // Set pricing values
        model.Pricing.InputPer1M = 10;
        model.Pricing.OutputPer1M = 20;
        model.Pricing.CurrencyCode = "USD";
        
        if (aliases != null)
        {
            model.Aliases = aliases;
        }
        
        return model;
    }

    [Fact]
    public async Task LoadSettingsAsync_WithNoConfigFile_ReturnsEmptySettings()
    {
        // Arrange
        if (File.Exists(_configPath))
            File.Delete(_configPath);

        // Act
        var settings = await _service.LoadSettingsAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.Models.Should().BeEmpty();
        settings.DefaultModelId.Should().BeNull();
    }

    [Fact]
    public async Task SaveAndLoadSettingsAsync_RoundTrip_PreservesData()
    {
        // Arrange
        var originalSettings = new GitGenSettings
        {
            Version = Constants.Configuration.CurrentConfigVersion,
            DefaultModelId = "test-id",
            Models = new List<ModelConfiguration>
            {
                CreateValidTestModel("test-id", "test-model", new List<string> { "test", "demo" })
            },
            Settings = new AppSettings
            {
                ShowTokenUsage = false,
                CopyToClipboard = false,
                EnablePartialAliasMatching = true,
                MinimumAliasMatchLength = 3,
                RequirePromptConfirmation = false
            }
        };

        // Act
        await _service.SaveSettingsAsync(originalSettings);
        var loadedSettings = await _service.LoadSettingsAsync();

        // Assert
        loadedSettings.Should().NotBeNull();
        loadedSettings.Version.Should().Be(originalSettings.Version);
        loadedSettings.DefaultModelId.Should().Be(originalSettings.DefaultModelId);
        loadedSettings.Models.Should().HaveCount(1);
        
        var loadedModel = loadedSettings.Models[0];
        var originalModel = originalSettings.Models[0];
        
        loadedModel.Id.Should().Be(originalModel.Id);
        loadedModel.Name.Should().Be(originalModel.Name);
        loadedModel.ApiKey.Should().Be(originalModel.ApiKey);
        loadedModel.Aliases.Should().BeEquivalentTo(originalModel.Aliases);
        
        loadedSettings.Settings.ShowTokenUsage.Should().Be(false);
        loadedSettings.Settings.EnablePartialAliasMatching.Should().Be(true);
        loadedSettings.Settings.RequirePromptConfirmation.Should().Be(false);
    }

    [Fact]
    public async Task GetModelAsync_ByName_ReturnsCorrectModel()
    {
        // Arrange
        var settings = new GitGenSettings
        {
            Version = Constants.Configuration.CurrentConfigVersion,
            Models = new List<ModelConfiguration>
            {
                CreateValidTestModel("1", "gpt-4"),
                CreateValidTestModel("2", "claude-3")
            }
        };
        await _service.SaveSettingsAsync(settings);

        // Act
        var model = await _service.GetModelAsync("claude-3");

        // Assert
        model.Should().NotBeNull();
        model!.Id.Should().Be("2");
        model.Name.Should().Be("claude-3");
    }

    [Fact]
    public async Task GetModelAsync_ByAlias_ReturnsCorrectModel()
    {
        // Arrange
        var settings = new GitGenSettings
        {
            Version = Constants.Configuration.CurrentConfigVersion,
            Models = new List<ModelConfiguration>
            {
                CreateValidTestModel("1", "gpt-4", new List<string> { "fast", "quick" })
            }
        };
        await _service.SaveSettingsAsync(settings);

        // Act
        var model1 = await _service.GetModelAsync("fast");
        var model2 = await _service.GetModelAsync("@quick");

        // Assert
        model1.Should().NotBeNull();
        model1!.Name.Should().Be("gpt-4");
        model2.Should().NotBeNull();
        model2!.Name.Should().Be("gpt-4");
    }

    [Fact]
    public async Task AddModelAsync_WithUniqueModel_AddsSuccessfully()
    {
        // Arrange
        var model = CreateValidTestModel("new-id", "new-model");

        // Act
        await _service.AddModelAsync(model);
        var settings = await _service.LoadSettingsAsync();

        // Assert
        settings.Models.Should().Contain(m => m.Id == "new-id");
    }

    [Fact]
    public async Task AddModelAsync_FirstModel_SetsAsDefault()
    {
        // Arrange
        var model = CreateValidTestModel("first-id", "first-model");

        // Act
        await _service.AddModelAsync(model);
        var settings = await _service.LoadSettingsAsync();

        // Assert
        settings.DefaultModelId.Should().Be("first-id");
    }

    [Fact]
    public async Task DeleteModelAsync_RemovesModelAndUpdatesDefault()
    {
        // Arrange
        var settings = new GitGenSettings
        {
            Version = Constants.Configuration.CurrentConfigVersion,
            DefaultModelId = "1",
            Models = new List<ModelConfiguration>
            {
                CreateValidTestModel("1", "model-1"),
                CreateValidTestModel("2", "model-2")
            }
        };
        await _service.SaveSettingsAsync(settings);

        // Act
        await _service.DeleteModelAsync("model-1");
        var updatedSettings = await _service.LoadSettingsAsync();

        // Assert
        updatedSettings.Models.Should().HaveCount(1);
        updatedSettings.Models.Should().NotContain(m => m.Id == "1");
        updatedSettings.DefaultModelId.Should().Be("2");
    }

    [Fact]
    public async Task AddAliasAsync_AddsUniqueAlias()
    {
        // Arrange
        var model = CreateValidTestModel("1", "test-model");
        await _service.AddModelAsync(model);

        // Act
        await _service.AddAliasAsync("test-model", "fast");
        var updatedModel = await _service.GetModelAsync("test-model");

        // Assert
        updatedModel!.Aliases.Should().Contain("fast");
    }

    [Fact]
    public async Task GetModelsByPartialMatchAsync_WithPartialAlias_ReturnsMatches()
    {
        // Arrange
        var settings = new GitGenSettings
        {
            Version = Constants.Configuration.CurrentConfigVersion,
            Models = new List<ModelConfiguration>
            {
                CreateValidTestModel("1", "model-1", new List<string> { "ultrafast", "ultrasmart" }),
                CreateValidTestModel("2", "model-2", new List<string> { "fast" })
            },
            Settings = new AppSettings
            {
                EnablePartialAliasMatching = true,
                MinimumAliasMatchLength = 3
            }
        };
        await _service.SaveSettingsAsync(settings);

        // Act
        var matches = await _service.GetModelsByPartialMatchAsync("ult");

        // Assert
        matches.Should().HaveCount(1);
        matches[0].Name.Should().Be("model-1");
    }

    [Fact]
    public async Task HealDefaultModelAsync_WithOnlyOneModel_AutoSetsDefault()
    {
        // Arrange
        var settings = new GitGenSettings
        {
            Version = Constants.Configuration.CurrentConfigVersion,
            DefaultModelId = null,
            Models = new List<ModelConfiguration>
            {
                CreateValidTestModel("1", "only-model")
            }
        };
        await _service.SaveSettingsAsync(settings);

        // Act
        var healed = await _service.HealDefaultModelAsync(Logger);
        var updatedSettings = await _service.LoadSettingsAsync();

        // Assert
        healed.Should().BeTrue();
        updatedSettings.DefaultModelId.Should().Be("1");
    }

    [Fact]
    public async Task LoadSettingsAsync_NewConfiguration_HasRequirePromptConfirmationTrue()
    {
        // Arrange
        if (File.Exists(_configPath))
            File.Delete(_configPath);

        // Act
        var settings = await _service.LoadSettingsAsync();

        // Assert
        settings.Should().NotBeNull();
        settings.Settings.Should().NotBeNull();
        settings.Settings.RequirePromptConfirmation.Should().BeTrue("new configurations should default to requiring confirmation for safety");
    }

    [Fact]
    public async Task SaveSettingsAsync_WithRequirePromptConfirmation_PersistsValue()
    {
        // Arrange
        var settings = new GitGenSettings
        {
            Version = Constants.Configuration.CurrentConfigVersion,
            Settings = new AppSettings
            {
                RequirePromptConfirmation = true
            }
        };

        // Act
        await _service.SaveSettingsAsync(settings);
        
        // Change it to false and save again
        settings.Settings.RequirePromptConfirmation = false;
        await _service.SaveSettingsAsync(settings);
        
        var loadedSettings = await _service.LoadSettingsAsync();

        // Assert
        loadedSettings.Settings.RequirePromptConfirmation.Should().BeFalse();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // Clean up test config file
            try
            {
                if (File.Exists(_configPath))
                    File.Delete(_configPath);
            }
            catch { }
        }
        base.Dispose(disposing);
    }
}