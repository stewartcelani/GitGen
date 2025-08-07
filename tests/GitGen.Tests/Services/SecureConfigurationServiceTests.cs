using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using GitGen.Configuration;
using GitGen.Services;
using Moq;
using Xunit;

namespace GitGen.Tests.Services;

[Collection("SecureConfigurationTests")]
public class SecureConfigurationServiceTests : IDisposable
{
    private readonly Mock<IConsoleLogger> _loggerMock;
    private readonly string _tempPath;
    private readonly SecureConfigurationService _service;

    public SecureConfigurationServiceTests()
    {
        _loggerMock = new Mock<IConsoleLogger>();
        
        // Create a temporary directory for test configuration
        _tempPath = Path.Combine(Path.GetTempPath(), $"GitGenTests_{Guid.NewGuid()}");
        var configPath = Path.Combine(_tempPath, ".gitgen");
        Directory.CreateDirectory(configPath);
        
        // Override the home directory for testing
        Environment.SetEnvironmentVariable("USERPROFILE", _tempPath);
        Environment.SetEnvironmentVariable("HOME", _tempPath);
        
        _service = new SecureConfigurationService(_loggerMock.Object);
        _service.ClearCache();
    }

    public void Dispose()
    {
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
    public async Task LoadSettingsAsync_WhenNoConfigFile_ReturnsEmptySettings()
    {
        // Act
        var result = await _service.LoadSettingsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Models.Should().BeEmpty();
        result.DefaultModelId.Should().BeNull();
        result.Settings.Should().NotBeNull();
        result.Settings.ConfigPath.Should().Contain("config.json");
        
        _loggerMock.Verify(x => x.Debug("No configuration file found at {Path}", It.IsAny<string>()), Times.Once);
    }

    [Fact]
    public async Task SaveSettingsAsync_SavesAndEncryptsConfiguration()
    {
        // Arrange
        var settings = CreateTestSettings();

        // Act
        await _service.SaveSettingsAsync(settings);

        // Assert
        var configPath = Path.Combine(_tempPath, ".gitgen", "config.json");
        File.Exists(configPath).Should().BeTrue();
        
        // File should be encrypted (not readable as plain JSON)
        var fileContent = await File.ReadAllTextAsync(configPath);
        fileContent.Should().NotContain("test-model");
        fileContent.Should().NotContain("test-api-key");
        
        // Should be able to load it back
        var loadedSettings = await _service.LoadSettingsAsync();
        loadedSettings.Models.Should().HaveCount(1);
        loadedSettings.Models[0].Name.Should().Be("test-model");
    }

    [Fact]
    public async Task LoadSettingsAsync_WithCachedSettings_ReturnsCached()
    {
        // Arrange
        var settings = CreateTestSettings();
        await _service.SaveSettingsAsync(settings);
        
        // First load to cache
        await _service.LoadSettingsAsync();
        
        // Clear mock invocations before second load
        _loggerMock.Invocations.Clear();

        // Act
        var result = await _service.LoadSettingsAsync();

        // Assert
        result.Should().NotBeNull();
        _loggerMock.Verify(x => x.Debug("Returning cached settings"), Times.Once);
    }

    [Fact]
    public async Task GetModelAsync_WithValidName_ReturnsModel()
    {
        // Arrange
        var settings = CreateTestSettings();
        await _service.SaveSettingsAsync(settings);

        // Act
        var result = await _service.GetModelAsync("test-model");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("test-model");
        result.ApiKey.Should().Be("test-api-key");
    }

    [Fact]
    public async Task GetModelAsync_WithValidId_ReturnsModel()
    {
        // Arrange
        var settings = CreateTestSettings();
        var modelId = settings.Models[0].Id;
        await _service.SaveSettingsAsync(settings);

        // Act
        var result = await _service.GetModelAsync(modelId);

        // Assert
        result.Should().NotBeNull();
        result!.Id.Should().Be(modelId);
    }

    [Fact]
    public async Task GetModelAsync_WithAlias_ReturnsModel()
    {
        // Arrange
        var settings = CreateTestSettings();
        settings.Models[0].Aliases = new List<string> { "fast", "quick" };
        await _service.SaveSettingsAsync(settings);

        // Act
        var result = await _service.GetModelAsync("fast");

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("test-model");
    }

    [Fact]
    public async Task GetModelAsync_WithInvalidName_ReturnsNull()
    {
        // Arrange
        var settings = CreateTestSettings();
        await _service.SaveSettingsAsync(settings);

        // Act
        var result = await _service.GetModelAsync("non-existent");

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetDefaultModelAsync_WithDefaultSet_ReturnsDefaultModel()
    {
        // Arrange
        var settings = CreateTestSettings();
        settings.DefaultModelId = settings.Models[0].Id;
        await _service.SaveSettingsAsync(settings);

        // Act
        var result = await _service.GetDefaultModelAsync();

        // Assert
        result.Should().NotBeNull();
        result!.Name.Should().Be("test-model");
    }

    [Fact]
    public async Task GetDefaultModelAsync_WithNoDefault_ReturnsFirstModel()
    {
        // Arrange
        var settings = CreateTestSettings();
        settings.DefaultModelId = null;
        await _service.SaveSettingsAsync(settings);

        // Act
        var result = await _service.GetDefaultModelAsync();

        // Assert
        // When DefaultModelId is null or empty, GetDefaultModelAsync returns the first model
        result.Should().NotBeNull();
        result!.Name.Should().Be("test-model");
    }

    [Fact]
    public async Task AddModelAsync_AddsNewModel()
    {
        // Arrange
        var settings = CreateTestSettings();
        await _service.SaveSettingsAsync(settings);
        
        var newModel = new ModelConfiguration
        {
            Name = "new-model",
            Type = "openai-compatible",
            Url = "https://api.new.com",
            ModelId = "new-model-id",
            Provider = "NewProvider",
            ApiKey = "new-api-key"
        };

        // Act
        await _service.AddModelAsync(newModel);

        // Assert
        var updatedSettings = await _service.LoadSettingsAsync();
        updatedSettings.Models.Should().HaveCount(2);
        updatedSettings.Models.Should().Contain(m => m.Name == "new-model");
    }

    [Fact]
    public async Task UpdateModelAsync_UpdatesExistingModel()
    {
        // Arrange
        var settings = CreateTestSettings();
        await _service.SaveSettingsAsync(settings);
        
        var model = await _service.GetModelAsync("test-model");
        model!.ApiKey = "updated-api-key";
        model.Provider = "UpdatedProvider";

        // Act
        await _service.UpdateModelAsync(model);

        // Assert
        var updatedModel = await _service.GetModelAsync("test-model");
        updatedModel!.ApiKey.Should().Be("updated-api-key");
        updatedModel.Provider.Should().Be("UpdatedProvider");
    }

    [Fact]
    public async Task DeleteModelAsync_RemovesModel()
    {
        // Arrange
        var settings = CreateTestSettings();
        settings.Models.Add(new ModelConfiguration
        {
            Name = "model-to-delete",
            Type = "openai-compatible",
            Url = "https://api.delete.com",
            ModelId = "delete-model-id",
            Provider = "DeleteProvider",
            ApiKey = "delete-api-key"
        });
        await _service.SaveSettingsAsync(settings);

        // Act
        await _service.DeleteModelAsync("model-to-delete");

        // Assert
        var updatedSettings = await _service.LoadSettingsAsync();
        updatedSettings.Models.Should().HaveCount(1);
        updatedSettings.Models.Should().NotContain(m => m.Name == "model-to-delete");
    }

    [Fact]
    public async Task SetDefaultModelAsync_SetsDefaultModel()
    {
        // Arrange
        var settings = CreateTestSettings();
        settings.Models.Add(new ModelConfiguration
        {
            Name = "second-model",
            Type = "openai-compatible",
            Url = "https://api.second.com",
            ModelId = "second-model-id",
            Provider = "SecondProvider",
            ApiKey = "second-api-key"
        });
        await _service.SaveSettingsAsync(settings);

        // Act
        await _service.SetDefaultModelAsync("second-model");

        // Assert
        var defaultModel = await _service.GetDefaultModelAsync();
        defaultModel!.Name.Should().Be("second-model");
    }

    [Fact]
    public async Task AddAliasAsync_AddsAliasToModel()
    {
        // Arrange
        var settings = CreateTestSettings();
        await _service.SaveSettingsAsync(settings);

        // Act
        await _service.AddAliasAsync("test-model", "new-alias");

        // Assert
        var model = await _service.GetModelAsync("test-model");
        model!.Aliases.Should().Contain("new-alias");
        
        // Should be able to find by new alias
        var modelByAlias = await _service.GetModelAsync("new-alias");
        modelByAlias!.Name.Should().Be("test-model");
    }

    [Fact]
    public async Task RemoveAliasAsync_RemovesAliasFromModel()
    {
        // Arrange
        var settings = CreateTestSettings();
        settings.Models[0].Aliases = new List<string> { "alias1", "alias2" };
        await _service.SaveSettingsAsync(settings);

        // Act
        await _service.RemoveAliasAsync("test-model", "alias1");

        // Assert
        var model = await _service.GetModelAsync("test-model");
        model!.Aliases.Should().NotContain("alias1");
        model.Aliases.Should().Contain("alias2");
    }

    [Fact]
    public async Task GetModelsByPartialMatchAsync_FindsMatchingModels()
    {
        // Arrange
        var settings = CreateTestSettings();
        settings.Models.Add(new ModelConfiguration
        {
            Id = "gpt-4-turbo",
            Name = "gpt-4-turbo",
            Type = "openai-compatible",
            Url = "https://api.openai.com",
            ModelId = "gpt-4-turbo",
            Provider = "OpenAI",
            ApiKey = "key1",
            Aliases = new List<string> { "turbo", "smart" }
        });
        settings.Models.Add(new ModelConfiguration
        {
            Id = "gpt-3.5-turbo",
            Name = "gpt-3.5-turbo",
            Type = "openai-compatible",
            Url = "https://api.openai.com",
            ModelId = "gpt-3.5-turbo",
            Provider = "OpenAI",
            ApiKey = "key2"
        });
        await _service.SaveSettingsAsync(settings);

        // Act
        var result = await _service.GetModelsByPartialMatchAsync("turbo");

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(m => m.Name == "gpt-4-turbo");
        result.Should().Contain(m => m.Name == "gpt-3.5-turbo");
    }

    [Fact]
    public async Task GetModelsByPartialMatchAsync_MatchesAliases()
    {
        // Arrange
        var settings = CreateTestSettings();
        settings.Models[0].Aliases = new List<string> { "fast", "quick" };
        await _service.SaveSettingsAsync(settings);

        // Act
        var result = await _service.GetModelsByPartialMatchAsync("qui");

        // Assert
        result.Should().HaveCount(1);
        result[0].Name.Should().Be("test-model");
    }

    [Fact]
    public async Task LoadSettingsAsync_HandlesCorruptedFile()
    {
        // Arrange
        var configPath = Path.Combine(_tempPath, ".gitgen", "config.json");
        await File.WriteAllTextAsync(configPath, "corrupted data that cannot be decrypted");

        // Act
        var result = await _service.LoadSettingsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Models.Should().BeEmpty();
        _loggerMock.Verify(x => x.Error(It.IsAny<string>(), It.IsAny<object[]>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task LoadSettingsAsync_HandlesPlainJsonFallback()
    {
        // Arrange
        var settings = CreateTestSettings();
        var json = System.Text.Json.JsonSerializer.Serialize(settings, ConfigurationJsonContext.Default.GitGenSettings);
        var configPath = Path.Combine(_tempPath, ".gitgen", "config.json");
        await File.WriteAllTextAsync(configPath, json);

        // Act
        var result = await _service.LoadSettingsAsync();

        // Assert
        result.Should().NotBeNull();
        result.Models.Should().HaveCount(1);
        result.Models[0].Name.Should().Be("test-model");
        _loggerMock.Verify(x => x.Warning("Loaded unencrypted configuration - will re-encrypt on next save"), Times.Once);
    }

    // Helper methods
    private static GitGenSettings CreateTestSettings()
    {
        return new GitGenSettings
        {
            Version = "4.0",
            Models = new List<ModelConfiguration>
            {
                new ModelConfiguration
                {
                    Id = "test-model",
                    Name = "test-model",
                    Type = "openai-compatible",
                    Url = "https://api.test.com",
                    ModelId = "test-model-id",
                    Provider = "TestProvider",
                    ApiKey = "test-api-key",
                    MaxOutputTokens = 2000,
                    Temperature = 0.2
                }
            },
            Settings = new AppSettings
            {
                ShowTokenUsage = true,
                CopyToClipboard = true
            }
        };
    }
}