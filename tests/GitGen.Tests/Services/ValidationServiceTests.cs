using FluentAssertions;
using GitGen.Services;
using Xunit;

namespace GitGen.Tests.Services;

public class ValidationServiceTests
{
    public class ModelValidationTests
    {
        [Theory]
        [InlineData("gpt-4")]
        [InlineData("claude-3-opus")]
        [InlineData("my-custom-model")]
        [InlineData("a")] // Single character should be valid
        public void IsValid_WithValidModelNames_ReturnsTrue(string modelName)
        {
            // Act
            var result = ValidationService.Model.IsValid(modelName);

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData(null!)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("model\"with\"quotes")]
        [InlineData("model'with'quotes")]
        [InlineData("model`with`backticks")]
        [InlineData("model$with$dollar")]
        [InlineData("model\\with\\backslash")]
        [InlineData("model\nwith\nnewlines")]
        public void IsValid_WithInvalidModelNames_ReturnsFalse(string modelName)
        {
            // Act
            var result = ValidationService.Model.IsValid(modelName);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsValid_WithModelNameExceedingMaxLength_ReturnsFalse()
        {
            // Arrange
            var longModelName = new string('a', 101); // Max is 100

            // Act
            var result = ValidationService.Model.IsValid(longModelName);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void GetValidationError_WithEmptyModelName_ReturnsCorrectMessage()
        {
            // Act
            var error = ValidationService.Model.GetValidationError("");

            // Assert
            error.Should().Be("Model name cannot be empty");
        }
    }

    public class UrlValidationTests
    {
        [Theory]
        [InlineData("https://api.openai.com/v1/chat/completions")]
        [InlineData("http://localhost:8080/api")]
        [InlineData("https://custom-domain.com:443/path")]
        public void IsValid_WithValidUrls_ReturnsTrue(string url)
        {
            // Act
            var result = ValidationService.Url.IsValid(url);

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData(null!)]
        [InlineData("")]
        [InlineData("not-a-url")]
        [InlineData("ftp://example.com")] // Wrong scheme
        [InlineData("https://")] // No host
        public void IsValid_WithInvalidUrls_ReturnsFalse(string url)
        {
            // Act
            var result = ValidationService.Url.IsValid(url);

            // Assert
            result.Should().BeFalse();
        }
    }

    public class ApiKeyValidationTests
    {
        [Fact]
        public void IsValid_WithApiKeyWhenAuthRequired_ReturnsTrue()
        {
            // Arrange
            var apiKey = "sk-1234567890abcdef";

            // Act
            var result = ValidationService.ApiKey.IsValid(apiKey, requiresAuth: true);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsValid_WithoutApiKeyWhenAuthNotRequired_ReturnsTrue()
        {
            // Act
            var result = ValidationService.ApiKey.IsValid(null, requiresAuth: false);

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData("short")] // Less than 10 chars
        [InlineData("key\nwith\ncontrol\nchars")]
        public void IsValid_WithInvalidApiKeyWhenAuthRequired_ReturnsFalse(string apiKey)
        {
            // Act
            var result = ValidationService.ApiKey.IsValid(apiKey, requiresAuth: true);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void Mask_WithValidApiKey_ReturnsMaskedKey()
        {
            // Arrange
            var apiKey = "sk-1234567890abcdef";

            // Act
            var masked = ValidationService.ApiKey.Mask(apiKey);

            // Assert
            masked.Should().Be("sk-12345***********");
        }
    }

    public class TokenCountValidationTests
    {
        [Theory]
        [InlineData(100)]
        [InlineData(5000)]
        [InlineData(8000)]
        public void IsValid_WithValidTokenCounts_ReturnsTrue(int tokens)
        {
            // Act
            var result = ValidationService.TokenCount.IsValid(tokens);

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData(99)]
        [InlineData(8001)]
        public void IsValid_WithInvalidTokenCounts_ReturnsFalse(int tokens)
        {
            // Act
            var result = ValidationService.TokenCount.IsValid(tokens);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(50, 100)]
        [InlineData(9000, 8000)]
        [InlineData(5000, 5000)]
        public void Clamp_WithOutOfRangeValues_ReturnsClampedValue(int input, int expected)
        {
            // Act
            var result = ValidationService.TokenCount.Clamp(input);

            // Assert
            result.Should().Be(expected);
        }
    }

    public class DomainExtractorTests
    {
        [Theory]
        [InlineData("https://api.openai.com/v1/chat", "api.openai.com")]
        [InlineData("https://www.example.com", "example.com")]
        [InlineData("http://localhost:8080", "localhost")]
        public void ExtractDomain_WithValidUrl_ExtractsDomain(string url, string expectedDomain)
        {
            // Act
            var result = ValidationService.DomainExtractor.ExtractDomain(url);

            // Assert
            result.Should().Be(expectedDomain);
        }

        [Theory]
        [InlineData("https://api.openai.com/", "OpenAI")]
        [InlineData("https://openrouter.ai/api/v1", "OpenRouter")]
        [InlineData("https://api.groq.com/openai", "Groq")]
        [InlineData("http://localhost:11434", "Local")]
        [InlineData("https://unknown-provider.com", null!)]
        public void GetProviderNameFromUrl_WithKnownProviders_ReturnsProviderName(string url, string expectedProvider)
        {
            // Act
            var result = ValidationService.DomainExtractor.GetProviderNameFromUrl(url);

            // Assert
            result.Should().Be(expectedProvider);
        }
    }
}