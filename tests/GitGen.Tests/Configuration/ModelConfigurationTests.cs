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
        model.Id.Should().BeEmpty(); // ID is set from Name, which starts empty
        model.Name.Should().BeEmpty();
        model.RequiresAuth.Should().BeTrue();
        model.UseLegacyMaxTokens.Should().BeFalse();
        model.Temperature.Should().Be(0.2);
        model.MaxOutputTokens.Should().Be(5000);
        model.Aliases.Should().NotBeNull().And.BeEmpty();
        model.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        model.LastUsed.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        model.Pricing.Should().NotBeNull();
        model.Pricing.InputPer1M.Should().Be(0);
        model.Pricing.OutputPer1M.Should().Be(0);
        model.Pricing.CurrencyCode.Should().Be("USD");
        model.Pricing.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void IsFreeModel_WithZeroPricing_ReturnsTrue()
    {
        // Arrange
        var model = new ModelConfiguration();
        model.Pricing.InputPer1M = 0;
        model.Pricing.OutputPer1M = 0;

        // Act & Assert
        model.IsFreeModel().Should().BeTrue();
    }

    [Fact]
    public void IsFreeModel_WithNonZeroPricing_ReturnsFalse()
    {
        // Arrange
        var model = new ModelConfiguration();
        model.Pricing.InputPer1M = 10;
        model.Pricing.OutputPer1M = 15;

        // Act & Assert
        model.IsFreeModel().Should().BeFalse();
    }

    [Theory]
    [InlineData("free")]
    [InlineData("FREE")]
    [InlineData("public")]
    [InlineData("PUBLIC")]
    [InlineData("free-tier")]
    [InlineData("public-model")]
    public void IsFreeModel_WithFreeOrPublicAlias_ReturnsTrue(string alias)
    {
        // Arrange
        var model = new ModelConfiguration
        {
            Aliases = new List<string> { "main", alias, "other" }
        };

        // Act & Assert
        model.IsFreeModel().Should().BeTrue();
    }

    [Theory]
    [InlineData("free")]
    [InlineData("FREE")]
    [InlineData("public")]
    [InlineData("PUBLIC")]
    [InlineData("PUBLIC REPO")]
    [InlineData("free model for testing")]
    [InlineData("This is a public model")]
    public void IsFreeModel_WithFreeOrPublicInNote_ReturnsTrue(string note)
    {
        // Arrange
        var model = new ModelConfiguration
        {
            Note = note
        };

        // Act & Assert
        model.IsFreeModel().Should().BeTrue();
    }

    [Theory]
    [InlineData("gpt-4:free")]
    [InlineData("claude:FREE")]
    [InlineData("model:public")]
    [InlineData("llama:PUBLIC")]
    public void IsFreeModel_WithFreeOrPublicSuffixInModelId_ReturnsTrue(string modelId)
    {
        // Arrange
        var model = new ModelConfiguration
        {
            ModelId = modelId
        };

        // Act & Assert
        model.IsFreeModel().Should().BeTrue();
    }

    [Fact]
    public void IsFreeModel_WithNoFreeIndicators_ReturnsFalse()
    {
        // Arrange
        var model = new ModelConfiguration
        {
            ModelId = "gpt-4",
            Note = "Premium model for production use",
            Aliases = new List<string> { "smart", "production" }
        };
        model.Pricing.InputPer1M = 30;
        model.Pricing.OutputPer1M = 60;

        // Act & Assert
        model.IsFreeModel().Should().BeFalse();
    }

    [Fact]
    public void IsFreeModel_WithMultipleFreeIndicators_ReturnsTrue()
    {
        // Arrange
        var model = new ModelConfiguration
        {
            ModelId = "gpt-3.5:free",
            Note = "FREE model for public repos",
            Aliases = new List<string> { "free", "public" }
        };
        model.Pricing.InputPer1M = 0;
        model.Pricing.OutputPer1M = 0;

        // Act & Assert
        model.IsFreeModel().Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithValidPricing_ReturnsTrue()
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
        model.Pricing.InputPer1M = 10;
        model.Pricing.OutputPer1M = 20;
        model.Pricing.CurrencyCode = "USD";

        // Act & Assert
        model.IsValid.Should().BeTrue();
    }

    [Fact]
    public void IsValid_WithInvalidPricingCurrency_ReturnsFalse()
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
        model.Pricing.CurrencyCode = "INVALID";

        // Act & Assert
        model.IsValid.Should().BeFalse();
        var errors = model.GetValidationErrors();
        errors.Should().ContainKey("Pricing");
        errors["Pricing"].Should().Be("Currency code must be a 3-letter code (e.g., USD, EUR)");
    }

    [Fact]
    public void IsValid_WithNegativePricingValues_ReturnsFalse()
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
        model.Pricing.InputPer1M = -5;
        model.Pricing.OutputPer1M = 10;

        // Act & Assert
        model.IsValid.Should().BeFalse();
        var errors = model.GetValidationErrors();
        errors.Should().ContainKey("Pricing");
        errors["Pricing"].Should().Be("Input cost per million tokens cannot be negative");
    }
    
    [Fact]
    public void SettingName_UpdatesId()
    {
        // Arrange
        var model = new ModelConfiguration();
        
        // Act
        model.Name = "test-model";
        
        // Assert
        model.Id.Should().Be("test-model");
        
        // Act - Update name
        model.Name = "updated-model";
        
        // Assert - Id should update
        model.Id.Should().Be("updated-model");
    }
}