using FluentAssertions;
using GitGen.Configuration;
using Xunit;

namespace GitGen.Tests.Configuration;

public class ModelConfigurationTests
{
    [Fact]
    public void IsValid_WithAllRequiredFields_ReturnsTrue()
    {
        // Arrange
        var model = new ModelConfiguration
        {
            Id = "test-id",
            Name = "test-model",
            Type = "openai-compatible",
            Provider = "TestProvider",
            Url = "https://api.test.com/v1/chat/completions",
            ModelId = "gpt-4",
            ApiKey = "sk-test1234567890",
            RequiresAuth = true,
            Temperature = 0.7,
            MaxOutputTokens = 1000
        };

        // Act & Assert
        model.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithMissingApiKeyWhenNotRequired_ReturnsTrue()
    {
        // Arrange
        var model = new ModelConfiguration
        {
            Id = "test-id",
            Name = "test-model",
            Type = "openai-compatible",
            Provider = "Local",
            Url = "http://localhost:11434/v1/chat/completions",
            ModelId = "llama3",
            ApiKey = "",
            RequiresAuth = false,
            Temperature = 0.7,
            MaxOutputTokens = 1000
        };

        // Act & Assert
        model.IsValid.Should().BeTrue();
    }

    [Fact]
    public void GetValidationErrors_WithInvalidFields_ReturnsErrors()
    {
        // Arrange
        var model = new ModelConfiguration
        {
            Type = "invalid-type",
            Url = "not-a-url",
            ModelId = "",
            ApiKey = "short",
            RequiresAuth = true,
            Temperature = -1,
            MaxOutputTokens = 50
        };

        // Act
        var errors = model.GetValidationErrors();

        // Assert
        errors.Should().HaveCount(6);
        errors.Should().ContainKey("Type");
        errors.Should().ContainKey("Url");
        errors.Should().ContainKey("ModelId");
        errors.Should().ContainKey("ApiKey");
        errors.Should().ContainKey("Temperature");
        errors.Should().ContainKey("MaxOutputTokens");
    }

    [Fact]
    public void DefaultValues_AreSetCorrectly()
    {
        // Arrange & Act
        var model = new ModelConfiguration();

        // Assert
        model.Id.Should().NotBeNullOrEmpty();
        model.Name.Should().BeEmpty();
        model.RequiresAuth.Should().BeTrue();
        model.UseLegacyMaxTokens.Should().BeFalse();
        model.Temperature.Should().Be(0.2);
        model.MaxOutputTokens.Should().Be(5000);
        model.Aliases.Should().NotBeNull().And.BeEmpty();
        model.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        model.LastUsed.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }
}