using GitGen.Configuration;

namespace GitGen.Services;

/// <summary>
///     Service for calculating and formatting costs based on token usage and model pricing.
/// </summary>
public static class CostCalculationService
{
    /// <summary>
    ///     Currency symbols mapped by currency code.
    /// </summary>
    private static readonly Dictionary<string, string> CurrencySymbols = new()
    {
        ["USD"] = "$",
        ["EUR"] = "€",
        ["GBP"] = "£",
        ["JPY"] = "¥",
        ["AUD"] = "A$",
        ["CAD"] = "C$",
        ["CHF"] = "CHF",
        ["CNY"] = "¥",
        ["INR"] = "₹",
        ["KRW"] = "₩",
        ["SGD"] = "S$",
        ["NZD"] = "NZ$",
        ["BRL"] = "R$",
        ["MXN"] = "MX$",
        ["HKD"] = "HK$",
        ["SEK"] = "kr",
        ["NOK"] = "kr",
        ["DKK"] = "kr",
        ["PLN"] = "zł",
        ["ZAR"] = "R",
        ["THB"] = "฿",
        ["MYR"] = "RM",
        ["PHP"] = "₱",
        ["IDR"] = "Rp",
        ["RUB"] = "₽",
        ["TRY"] = "₺",
        ["AED"] = "AED",
        ["SAR"] = "SAR",
        ["ILS"] = "₪",
        ["CZK"] = "Kč",
        ["HUF"] = "Ft",
        ["RON"] = "lei",
        ["BGN"] = "лв",
        ["HRK"] = "kn",
        ["CLP"] = "CLP$",
        ["COP"] = "COL$",
        ["PEN"] = "S/",
        ["UYU"] = "$U",
        ["ARS"] = "AR$",
        ["VND"] = "₫",
        ["NGN"] = "₦",
        ["UAH"] = "₴",
        ["GHS"] = "₵",
        ["KES"] = "KSh",
        ["EGP"] = "E£",
        ["MAD"] = "MAD",
        ["TND"] = "DT",
        ["LKR"] = "Rs",
        ["PKR"] = "Rs",
        ["BDT"] = "৳",
        ["NPR"] = "रू"
    };

    /// <summary>
    ///     Calculates and formats the cost based on token usage and model pricing.
    /// </summary>
    /// <param name="model">The model configuration with pricing information.</param>
    /// <param name="inputTokens">The number of input tokens used.</param>
    /// <param name="outputTokens">The number of output tokens generated.</param>
    /// <returns>A formatted cost string, or empty string if pricing is not configured.</returns>
    public static string CalculateAndFormatCost(ModelConfiguration model, int inputTokens, int outputTokens)
    {
        if (model.Pricing == null || (model.Pricing.InputPer1M == 0 && model.Pricing.OutputPer1M == 0))
            return string.Empty;

        // Calculate cost based on millions of tokens
        decimal inputCost = (inputTokens / 1_000_000m) * model.Pricing.InputPer1M;
        decimal outputCost = (outputTokens / 1_000_000m) * model.Pricing.OutputPer1M;
        decimal totalCost = inputCost + outputCost;

        return FormatCurrency(totalCost, model.Pricing.CurrencyCode);
    }

    /// <summary>
    ///     Formats a currency amount with the appropriate symbol.
    /// </summary>
    /// <param name="amount">The amount to format.</param>
    /// <param name="currencyCode">The ISO currency code.</param>
    /// <returns>A formatted currency string.</returns>
    public static string FormatCurrency(decimal amount, string currencyCode)
    {
        var symbol = GetCurrencySymbol(currencyCode);
        
        // Special formatting for certain currencies
        return currencyCode switch
        {
            // Currencies that typically show symbol after amount
            "SEK" or "NOK" or "DKK" or "CZK" or "PLN" or "HUF" or "RON" or "BGN" or "HRK" 
                => $"{amount:F2} {symbol}",
            
            // Currencies with no decimal places
            "JPY" or "KRW" or "VND" or "IDR" or "CLP" or "COP" or "HUF" 
                => $"{symbol}{amount:F0}",
            
            // Default: symbol before amount with 2 decimal places
            _ => $"{symbol}{amount:F2}"
        };
    }
    
    /// <summary>
    ///     Formats a currency amount with the appropriate symbol and specified decimal places.
    /// </summary>
    /// <param name="amount">The amount to format.</param>
    /// <param name="currencyCode">The ISO currency code.</param>
    /// <param name="decimalPlaces">The number of decimal places to display.</param>
    /// <returns>A formatted currency string.</returns>
    public static string FormatCurrency(decimal amount, string currencyCode, int decimalPlaces)
    {
        var symbol = GetCurrencySymbol(currencyCode);
        var format = $"F{decimalPlaces}";
        
        // Special formatting for certain currencies
        return currencyCode switch
        {
            // Currencies that typically show symbol after amount
            "SEK" or "NOK" or "DKK" or "CZK" or "PLN" or "HUF" or "RON" or "BGN" or "HRK" 
                => $"{amount.ToString(format)} {symbol}",
            
            // Currencies with no decimal places (override decimalPlaces)
            "JPY" or "KRW" or "VND" or "IDR" or "CLP" or "COP" or "HUF" 
                => $"{symbol}{amount:F0}",
            
            // Default: symbol before amount with specified decimal places
            _ => $"{symbol}{amount.ToString(format)}"
        };
    }

    /// <summary>
    ///     Gets the currency symbol for a given currency code.
    /// </summary>
    /// <param name="currencyCode">The ISO currency code.</param>
    /// <returns>The currency symbol, or the code itself if not found.</returns>
    public static string GetCurrencySymbol(string currencyCode)
    {
        return CurrencySymbols.GetValueOrDefault(currencyCode.ToUpper(), currencyCode + " ");
    }

    /// <summary>
    ///     Gets the number of decimal places in a decimal value.
    /// </summary>
    /// <param name="value">The decimal value to analyze.</param>
    /// <returns>The number of decimal places, capped at 4.</returns>
    private static int GetDecimalPlaces(decimal value)
    {
        // Convert to string and find decimal places
        var str = value.ToString(System.Globalization.CultureInfo.InvariantCulture);
        var decimalIndex = str.IndexOf('.');
        if (decimalIndex < 0)
            return 0;
            
        var decimalPlaces = str.Length - decimalIndex - 1;
        
        // Remove trailing zeros to get actual significant decimal places
        str = str.TrimEnd('0');
        decimalPlaces = str.Length - decimalIndex - 1;
        
        // Cap at 4 decimal places max, default to 2 if less than 2
        return Math.Max(2, Math.Min(decimalPlaces, 4));
    }
    
    /// <summary>
    ///     Formats the pricing information for display.
    /// </summary>
    /// <param name="pricing">The pricing information to format.</param>
    /// <returns>A formatted string showing input/output costs per million tokens.</returns>
    public static string FormatPricingInfo(PricingInfo pricing)
    {
        // If both costs are 0, display "Free"
        if (pricing.InputPer1M == 0 && pricing.OutputPer1M == 0)
            return "Free";
        
        // Detect decimal places from the actual values (minimum 2, maximum 4)
        var inputDecimals = GetDecimalPlaces(pricing.InputPer1M);
        var outputDecimals = GetDecimalPlaces(pricing.OutputPer1M);
        
        var inputFormatted = FormatCurrency(pricing.InputPer1M, pricing.CurrencyCode, inputDecimals);
        var outputFormatted = FormatCurrency(pricing.OutputPer1M, pricing.CurrencyCode, outputDecimals);
        
        return $"Input: {inputFormatted}/M, Output: {outputFormatted}/M";
    }

    /// <summary>
    ///     Creates a cost breakdown string for detailed display.
    /// </summary>
    /// <param name="model">The model configuration with pricing information.</param>
    /// <param name="inputTokens">The number of input tokens used.</param>
    /// <param name="outputTokens">The number of output tokens generated.</param>
    /// <returns>A detailed cost breakdown string.</returns>
    public static string GetCostBreakdown(ModelConfiguration model, int inputTokens, int outputTokens)
    {
        if (model.Pricing == null)
            return string.Empty;

        decimal inputCost = (inputTokens / 1_000_000m) * model.Pricing.InputPer1M;
        decimal outputCost = (outputTokens / 1_000_000m) * model.Pricing.OutputPer1M;
        decimal totalCost = inputCost + outputCost;

        var inputCostStr = FormatCurrency(inputCost, model.Pricing.CurrencyCode);
        var outputCostStr = FormatCurrency(outputCost, model.Pricing.CurrencyCode);
        var totalCostStr = FormatCurrency(totalCost, model.Pricing.CurrencyCode);

        return $"Input: {inputCostStr} ({inputTokens:N0} tokens) + Output: {outputCostStr} ({outputTokens:N0} tokens) = Total: {totalCostStr}";
    }
}