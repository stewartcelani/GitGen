using FluentAssertions;
using GitGen.Configuration;
using GitGen.Services;
using Xunit;

namespace GitGen.Tests.Services;

public class ValidationServiceTests
{
    #region Model Validation Tests

    public class ModelValidationTests
    {
        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        public void IsValid_WithNullOrWhitespace_ReturnsFalse(string? modelName)
        {
            // Act
            var result = ValidationService.Model.IsValid(modelName);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsValid_WithTooLongName_ReturnsFalse()
        {
            // Arrange
            var longName = new string('a', Constants.Configuration.MaxModelNameLength + 1);

            // Act
            var result = ValidationService.Model.IsValid(longName);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData("model\nname")]
        [InlineData("model\rname")]
        [InlineData("model\tname")]
        [InlineData("model\0name")]
        public void IsValid_WithControlCharacters_ReturnsFalse(string modelName)
        {
            // Act
            var result = ValidationService.Model.IsValid(modelName);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData("model\"name")]
        [InlineData("model'name")]
        [InlineData("model`name")]
        [InlineData("model$name")]
        [InlineData("model\\name")]
        public void IsValid_WithInvalidCharacters_ReturnsFalse(string modelName)
        {
            // Act
            var result = ValidationService.Model.IsValid(modelName);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData("gpt-4")]
        [InlineData("claude-3-opus")]
        [InlineData("gemini-pro")]
        [InlineData("llama-2-70b")]
        public void IsValid_WithValidModelNames_ReturnsTrue(string modelName)
        {
            // Act
            var result = ValidationService.Model.IsValid(modelName);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void GetValidationError_WithNullName_ReturnsAppropriateMessage()
        {
            // Act
            var error = ValidationService.Model.GetValidationError(null);

            // Assert
            error.Should().Contain("Model name cannot be empty");
        }

        [Fact]
        public void GetValidationError_WithLongName_ReturnsLengthMessage()
        {
            // Arrange
            var longName = new string('a', Constants.Configuration.MaxModelNameLength + 1);

            // Act
            var error = ValidationService.Model.GetValidationError(longName);

            // Assert
            error.Should().Contain($"cannot exceed {Constants.Configuration.MaxModelNameLength} characters");
        }

        [Fact]
        public void GetValidationError_WithInvalidCharacters_ReturnsCharacterMessage()
        {
            // Act
            var error = ValidationService.Model.GetValidationError("model\"name");

            // Assert
            error.Should().Contain("contains invalid characters");
        }
    }

    #endregion

    #region URL Validation Tests

    public class UrlValidationTests
    {
        [Theory]
        [InlineData("https://api.openai.com/v1")]
        [InlineData("http://localhost:8080")]
        [InlineData("https://example.com:443/api")]
        [InlineData("http://192.168.1.1:5000")]
        public void IsValid_WithValidUrls_ReturnsTrue(string url)
        {
            // Act
            var result = ValidationService.Url.IsValid(url);

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("not-a-url")]
        [InlineData("ftp://example.com")]
        [InlineData("https://")]
        [InlineData("//example.com")]
        public void IsValid_WithInvalidUrls_ReturnsFalse(string? url)
        {
            // Act
            var result = ValidationService.Url.IsValid(url);

            // Assert
            result.Should().BeFalse();
        }

        // Note: URL length validation is not implemented in ValidationService.Url.IsValid
        // The method only validates scheme and format, not length

        [Fact]
        public void GetValidationError_WithEmptyUrl_ReturnsEmptyMessage()
        {
            // Act
            var error = ValidationService.Url.GetValidationError("");

            // Assert
            error.Should().Contain("URL cannot be empty");
        }

        [Fact]
        public void GetValidationError_WithInvalidScheme_ReturnsSchemeMessage()
        {
            // Act
            var error = ValidationService.Url.GetValidationError("ftp://example.com");

            // Assert
            error.Should().Contain("must use HTTP or HTTPS");
        }

        [Fact]
        public void GetValidationError_WithMalformedUrl_ReturnsMalformedMessage()
        {
            // Act
            var error = ValidationService.Url.GetValidationError("not-a-url");

            // Assert
            error.Should().Contain("not a valid URL");
        }
    }

    #endregion

    #region API Key Validation Tests

    public class ApiKeyValidationTests
    {
        [Theory]
        [InlineData("sk-abcdef123456", true)]
        [InlineData("valid-api-key-123", true)]
        [InlineData("", false)] // Empty allowed when auth not required
        [InlineData(null, false)] // Null allowed when auth not required
        public void IsValid_WithVariousKeys_ReturnsExpected(string? apiKey, bool requiresAuth)
        {
            // Act
            var result = ValidationService.ApiKey.IsValid(apiKey, requiresAuth);

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData("", true)]
        [InlineData(null, true)]
        [InlineData("   ", true)]
        public void IsValid_WithEmptyKeyWhenAuthRequired_ReturnsFalse(string? apiKey, bool requiresAuth)
        {
            // Act
            var result = ValidationService.ApiKey.IsValid(apiKey, requiresAuth);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsValid_WithTooShortKey_ReturnsFalse()
        {
            // Act
            var result = ValidationService.ApiKey.IsValid("abc", true);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsValid_WithTooLongKey_ReturnsFalse()
        {
            // Arrange
            var longKey = new string('a', Constants.Configuration.MaxApiKeyLength + 1);

            // Act
            var result = ValidationService.ApiKey.IsValid(longKey, true);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData("key with spaces")]
        [InlineData("key\nwith\nnewlines")]
        [InlineData("key\twith\ttabs")]
        public void IsValid_WithWhitespace_ReturnsFalse(string apiKey)
        {
            // Act
            var result = ValidationService.ApiKey.IsValid(apiKey, true);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData("sk-1234567890abcdefghij", "sk-123456...ghij")]
        [InlineData("shortkey", "shor...tkey")]
        [InlineData(null, "[empty]")]
        [InlineData("", "[empty]")]
        public void Mask_WithVariousKeys_ReturnsMaskedVersion(string? apiKey, string expected)
        {
            // Act
            var masked = ValidationService.ApiKey.Mask(apiKey);

            // Assert
            masked.Should().Be(expected);
        }

        [Fact]
        public void GetValidationError_WithEmptyKeyWhenRequired_ReturnsAuthMessage()
        {
            // Act
            var error = ValidationService.ApiKey.GetValidationError("", true);

            // Assert
            error.Should().Contain("API key is required");
        }

        [Fact]
        public void GetValidationError_WithShortKey_ReturnsLengthMessage()
        {
            // Act
            var error = ValidationService.ApiKey.GetValidationError("abc", true);

            // Assert
            error.Should().Contain($"at least {Constants.Configuration.MinApiKeyLength} characters");
        }
    }

    #endregion

    #region Token Count Validation Tests

    public class TokenCountValidationTests
    {
        [Theory]
        [InlineData(100)]
        [InlineData(1000)]
        [InlineData(4096)]
        [InlineData(Constants.Configuration.MaxOutputTokens)]
        public void IsValid_WithValidCounts_ReturnsTrue(int tokens)
        {
            // Act
            var result = ValidationService.TokenCount.IsValid(tokens);

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData(0)]
        [InlineData(-1)]
        [InlineData(-100)]
        public void IsValid_WithZeroOrNegative_ReturnsFalse(int tokens)
        {
            // Act
            var result = ValidationService.TokenCount.IsValid(tokens);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void IsValid_WithTooLarge_ReturnsFalse()
        {
            // Act
            var result = ValidationService.TokenCount.IsValid(Constants.Configuration.MaxOutputTokens + 1);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void GetValidationError_WithZero_ReturnsPositiveMessage()
        {
            // Act
            var error = ValidationService.TokenCount.GetValidationError(0);

            // Assert
            error.Should().Contain("must be at least 100");
        }

        [Fact]
        public void GetValidationError_WithTooLarge_ReturnsMaxMessage()
        {
            // Act
            var error = ValidationService.TokenCount.GetValidationError(Constants.Configuration.MaxOutputTokens + 1);

            // Assert
            error.Should().Contain($"cannot exceed {Constants.Configuration.MaxOutputTokens}");
        }
    }

    #endregion

    #region Temperature Validation Tests

    public class TemperatureValidationTests
    {
        [Theory]
        [InlineData(0.0)]
        [InlineData(0.5)]
        [InlineData(1.0)]
        [InlineData(1.5)]
        [InlineData(2.0)]
        public void IsValid_WithValidTemperatures_ReturnsTrue(double temperature)
        {
            // Act
            var result = ValidationService.Temperature.IsValid(temperature);

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData(-0.1)]
        [InlineData(-1.0)]
        [InlineData(2.1)]
        [InlineData(3.0)]
        [InlineData(double.NaN)]
        [InlineData(double.PositiveInfinity)]
        [InlineData(double.NegativeInfinity)]
        public void IsValid_WithInvalidTemperatures_ReturnsFalse(double temperature)
        {
            // Act
            var result = ValidationService.Temperature.IsValid(temperature);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void GetValidationError_WithNegative_ReturnsRangeMessage()
        {
            // Act
            var error = ValidationService.Temperature.GetValidationError(-0.5);

            // Assert
            error.Should().Contain("between 0.0 and 2.0");
        }

        [Fact]
        public void GetValidationError_WithNaN_ReturnsInvalidMessage()
        {
            // Act
            var error = ValidationService.Temperature.GetValidationError(double.NaN);

            // Assert
            error.Should().Contain("not a valid number");
        }
    }

    #endregion

    #region Provider Validation Tests

    public class ProviderValidationTests
    {
        [Theory]
        [InlineData("openai")]
        [InlineData("openai-compatible")]
        [InlineData("OPENAI")]
        [InlineData("OpenAI-Compatible")]
        public void IsValid_WithValidProviders_ReturnsTrue(string provider)
        {
            // Act
            var result = ValidationService.Provider.IsValid(provider);

            // Assert
            result.Should().BeTrue();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("   ")]
        [InlineData("azure")]
        [InlineData("anthropic")]
        [InlineData("unknown")]
        public void IsValid_WithInvalidProviders_ReturnsFalse(string? provider)
        {
            // Act
            var result = ValidationService.Provider.IsValid(provider);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void GetValidationError_WithEmpty_ReturnsEmptyMessage()
        {
            // Act
            var error = ValidationService.Provider.GetValidationError("");

            // Assert
            error.Should().Contain("Provider type cannot be empty");
        }

        [Fact]
        public void GetValidationError_WithUnsupported_ReturnsUnsupportedMessage()
        {
            // Act
            var error = ValidationService.Provider.GetValidationError("azure");

            // Assert
            error.Should().Contain("not supported");
            error.Should().Contain("openai, openai-compatible");
        }
    }

    #endregion

    #region Pricing Validation Tests

    public class PricingValidationTests
    {
        [Fact]
        public void IsValid_WithValidPricing_ReturnsTrue()
        {
            // Arrange
            var pricing = new PricingInfo
            {
                InputPer1M = 0.01m,
                OutputPer1M = 0.03m,
                CurrencyCode = "USD"
            };

            // Act
            var result = ValidationService.Pricing.IsValid(pricing);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public void IsValid_WithNullPricing_ReturnsFalse()
        {
            // Act
            var result = ValidationService.Pricing.IsValid(null);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData(-1, 1, "USD")]
        [InlineData(1, -1, "USD")]
        public void IsValid_WithInvalidPrices_ReturnsFalse(decimal input, decimal output, string currency)
        {
            // Arrange
            var pricing = new PricingInfo
            {
                InputPer1M = input,
                OutputPer1M = output,
                CurrencyCode = currency
            };

            // Act
            var result = ValidationService.Pricing.IsValid(pricing);

            // Assert
            result.Should().BeFalse();
        }

        [Theory]
        [InlineData("", "", "")]
        [InlineData("", null, null)]
        [InlineData("   ", "   ", "   ")]
        [InlineData("US", "US", "")] // Too short
        [InlineData("USDD", "USDD", "")] // Too long
        [InlineData("US1", "US1", "")] // Contains number
        public void IsValid_WithInvalidCurrency_ReturnsFalse(string currency, string? input1, string? input2)
        {
            // Arrange
            var pricing = new PricingInfo
            {
                InputPer1M = 0.01m,
                OutputPer1M = 0.03m,
                CurrencyCode = input1 ?? input2 ?? currency // Use various invalid values
            };

            // Act
            var result = ValidationService.Pricing.IsValid(pricing);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public void GetValidationError_WithNullPricing_ReturnsNullMessage()
        {
            // Act
            var error = ValidationService.Pricing.GetValidationError(null);

            // Assert
            error.Should().Contain("Pricing information is required");
        }

        [Fact]
        public void GetValidationError_WithNegativePrices_ReturnsNegativeMessage()
        {
            // Arrange
            var pricing = new PricingInfo
            {
                InputPer1M = -1m,
                OutputPer1M = 1m,
                CurrencyCode = "USD"
            };

            // Act
            var error = ValidationService.Pricing.GetValidationError(pricing);

            // Assert
            error.Should().Contain("cannot be negative");
        }

        [Fact]
        public void GetValidationError_WithInvalidCurrency_ReturnsCurrencyMessage()
        {
            // Arrange
            var pricing = new PricingInfo
            {
                InputPer1M = 0.01m,
                OutputPer1M = 0.03m,
                CurrencyCode = "US"
            };

            // Act
            var error = ValidationService.Pricing.GetValidationError(pricing);

            // Assert
            error.Should().Contain("3-letter currency code");
        }
    }

    #endregion

    #region Domain Extractor Tests

    public class DomainExtractorTests
    {
        [Theory]
        [InlineData("https://api.openai.com/v1", "api.openai.com")]
        [InlineData("http://localhost:8080", "localhost")]
        [InlineData("https://example.com:443/api/v1", "example.com")]
        [InlineData("https://subdomain.example.co.uk/path", "subdomain.example.co.uk")]
        public void ExtractDomain_WithValidUrls_ExtractsDomain(string url, string expectedDomain)
        {
            // Act
            var domain = ValidationService.DomainExtractor.ExtractDomain(url);

            // Assert
            domain.Should().Be(expectedDomain);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-url")]
        [InlineData("ftp://example.com")]
        public void ExtractDomain_WithInvalidUrls_ReturnsNull(string? url)
        {
            // Act
            var domain = ValidationService.DomainExtractor.ExtractDomain(url);

            // Assert
            domain.Should().BeNull();
        }

        [Theory]
        [InlineData("https://api.openai.com/v1", "OpenAI")]
        [InlineData("https://api.anthropic.com/v1", "Anthropic")]
        [InlineData("https://generativelanguage.googleapis.com", "Google AI")]
        [InlineData("https://api.groq.com/v1", "Groq")]
        [InlineData("http://localhost:8080", "Local LLM")]
        [InlineData("https://unknown.example.com", "OpenAI-Compatible")]
        public void GetProviderNameFromUrl_WithVariousUrls_ReturnsExpectedName(string url, string expectedName)
        {
            // Act
            var name = ValidationService.DomainExtractor.GetProviderNameFromUrl(url);

            // Assert
            name.Should().Be(expectedName);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData("not-a-url")]
        public void GetProviderNameFromUrl_WithInvalidUrl_ReturnsUnknown(string? url)
        {
            // Act
            var name = ValidationService.DomainExtractor.GetProviderNameFromUrl(url);

            // Assert
            name.Should().Be("Unknown");
        }
    }

    #endregion
}