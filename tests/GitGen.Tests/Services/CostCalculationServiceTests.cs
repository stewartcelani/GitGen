using FluentAssertions;
using GitGen.Configuration;
using GitGen.Services;
using Xunit;

namespace GitGen.Tests.Services;

public class CostCalculationServiceTests
{
    [Fact]
    public void CalculateAndFormatCost_WithValidPricing_ReturnsFormattedCost()
    {
        // Arrange
        var model = new ModelConfiguration
        {
            Pricing = new PricingInfo
            {
                InputPer1M = 10.0m,
                OutputPer1M = 30.0m,
                CurrencyCode = "USD"
            }
        };

        // Act
        var result = CostCalculationService.CalculateAndFormatCost(model, 1000, 500);

        // Assert
        result.Should().Be("$0.03");
    }

    [Fact]
    public void CalculateAndFormatCost_WithFreePricing_ReturnsEmptyString()
    {
        // Arrange
        var model = new ModelConfiguration
        {
            Pricing = new PricingInfo
            {
                InputPer1M = 0m,
                OutputPer1M = 0m,
                CurrencyCode = "USD"
            }
        };

        // Act
        var result = CostCalculationService.CalculateAndFormatCost(model, 10000, 5000);

        // Assert
        result.Should().BeEmpty();
    }

    [Theory]
    [InlineData("USD", "$")]
    [InlineData("EUR", "€")]
    [InlineData("GBP", "£")]
    [InlineData("JPY", "¥")]
    [InlineData("AUD", "A$")]
    [InlineData("UNKNOWN", "UNKNOWN ")]
    public void GetCurrencySymbol_ReturnsCorrectSymbol(string currencyCode, string expectedSymbol)
    {
        // Act
        var result = CostCalculationService.GetCurrencySymbol(currencyCode);

        // Assert
        result.Should().Be(expectedSymbol);
    }

    [Theory]
    [InlineData("USD", 10.5, "$10.50")]
    [InlineData("EUR", 10.5, "€10.50")]
    [InlineData("JPY", 1000.5, "¥1001")] // No decimals for JPY
    [InlineData("SEK", 10.5, "10.50 kr")] // Symbol after amount
    public void FormatCurrency_WithDifferentCurrencies_FormatsCorrectly(string currencyCode, decimal amount, string expected)
    {
        // Act
        var result = CostCalculationService.FormatCurrency(amount, currencyCode);

        // Assert
        result.Should().Be(expected);
    }

    [Fact]
    public void FormatPricingInfo_WithFreePricing_ReturnsFree()
    {
        // Arrange
        var pricing = new PricingInfo
        {
            InputPer1M = 0m,
            OutputPer1M = 0m,
            CurrencyCode = "USD"
        };

        // Act
        var result = CostCalculationService.FormatPricingInfo(pricing);

        // Assert
        result.Should().Be("Free");
    }

    [Fact]
    public void FormatPricingInfo_WithPaidPricing_ReturnsFormattedInfo()
    {
        // Arrange
        var pricing = new PricingInfo
        {
            InputPer1M = 0.15m,
            OutputPer1M = 0.60m,
            CurrencyCode = "USD"
        };

        // Act
        var result = CostCalculationService.FormatPricingInfo(pricing);

        // Assert
        result.Should().Be("Input: $0.15/M, Output: $0.60/M");
    }

    [Fact]
    public void GetCostBreakdown_ReturnsDetailedBreakdown()
    {
        // Arrange
        var model = new ModelConfiguration
        {
            Pricing = new PricingInfo
            {
                InputPer1M = 10.0m,
                OutputPer1M = 30.0m,
                CurrencyCode = "USD"
            }
        };

        // Act
        var result = CostCalculationService.GetCostBreakdown(model, 2000, 500);

        // Assert
        result.Should().Contain("Input: $0.02 (2,000 tokens)");
        result.Should().Contain("Output: $0.02 (500 tokens)");
        result.Should().Contain("Total: $0.04");
    }
}