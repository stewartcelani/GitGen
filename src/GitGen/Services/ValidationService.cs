using GitGen;
using GitGen.Configuration;

namespace GitGen.Services;

/// <summary>
///     Centralized validation service for all user inputs and configuration values.
///     Provides consistent validation rules and helpful error messages.
/// </summary>
public static class ValidationService
{
    /// <summary>
    ///     Validation methods for AI model names and identifiers.
    /// </summary>
    public static class Model
    {
        /// <summary>
        ///     Validates if a model name is acceptable for use.
        /// </summary>
        /// <param name="modelName">The model name to validate</param>
        /// <returns>True if the model name is valid</returns>
        public static bool IsValid(string? modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                return false;

            // Check length constraints
            if (modelName.Length > Constants.Configuration.MaxModelNameLength)
                return false;

            // Check for control characters that could cause issues
            if (modelName.Any(c => char.IsControl(c)))
                return false;

            // Model names should not contain quotes or shell-sensitive characters
            var invalidChars = new[] { '"', '\'', '`', '$', '\\', '\n', '\r', '\t' };
            if (modelName.Any(c => invalidChars.Contains(c)))
                return false;

            return true;
        }

        /// <summary>
        ///     Gets validation error message for invalid model names.
        /// </summary>
        /// <param name="modelName">The invalid model name</param>
        /// <returns>Descriptive error message</returns>
        public static string GetValidationError(string? modelName)
        {
            if (string.IsNullOrWhiteSpace(modelName))
                return "Model name cannot be empty";

            if (modelName.Length > Constants.Configuration.MaxModelNameLength)
                return $"Model name cannot exceed {Constants.Configuration.MaxModelNameLength} characters";

            if (modelName.Any(c => char.IsControl(c)))
                return "Model name cannot contain control characters";

            var invalidChars = new[] { '"', '\'', '`', '$', '\\', '\n', '\r', '\t' };
            if (modelName.Any(c => invalidChars.Contains(c)))
                return "Model name contains invalid characters";

            return "Model name is invalid";
        }
    }

    /// <summary>
    ///     Validation methods for URLs and endpoints.
    /// </summary>
    public static class Url
    {
        /// <summary>
        ///     Validates if a URL is acceptable for API endpoints.
        /// </summary>
        /// <param name="url">The URL to validate</param>
        /// <returns>True if the URL is valid</returns>
        public static bool IsValid(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return false;

            try
            {
                var uri = new Uri(url, UriKind.Absolute);

                // Only allow HTTP and HTTPS schemes
                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                    return false;

                // URL must have a valid host
                if (string.IsNullOrWhiteSpace(uri.Host))
                    return false;

                return true;
            }
            catch (UriFormatException)
            {
                return false;
            }
        }

        /// <summary>
        ///     Gets validation error message for invalid URLs.
        /// </summary>
        /// <param name="url">The invalid URL</param>
        /// <returns>Descriptive error message</returns>
        public static string GetValidationError(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return "URL cannot be empty";

            try
            {
                var uri = new Uri(url, UriKind.Absolute);

                if (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps)
                    return "URL must use HTTP or HTTPS protocol";

                if (string.IsNullOrWhiteSpace(uri.Host))
                    return "URL must have a valid hostname";

                return "URL is invalid";
            }
            catch (UriFormatException)
            {
                return "URL format is invalid";
            }
        }
    }

    /// <summary>
    ///     Validation methods for API keys and authentication tokens.
    /// </summary>
    public static class ApiKey
    {
        /// <summary>
        ///     Validates if an API key is acceptable when authentication is required.
        /// </summary>
        /// <param name="apiKey">The API key to validate</param>
        /// <param name="requiresAuth">Whether authentication is required</param>
        /// <returns>True if the API key is valid for the auth requirements</returns>
        public static bool IsValid(string? apiKey, bool requiresAuth)
        {
            // If auth is not required, any value (including null/empty) is acceptable
            if (!requiresAuth)
                return true;

            // If auth is required, key must be present and valid
            if (string.IsNullOrWhiteSpace(apiKey))
                return false;

            // Check length constraints for security
            if (apiKey.Length < Constants.Configuration.MinApiKeyLength ||
                apiKey.Length > Constants.Configuration.MaxApiKeyLength)
                return false;

            // API keys should not contain control characters
            if (apiKey.Any(c => char.IsControl(c)))
                return false;

            return true;
        }

        /// <summary>
        ///     Gets validation error message for invalid API keys.
        /// </summary>
        /// <param name="apiKey">The invalid API key</param>
        /// <param name="requiresAuth">Whether authentication is required</param>
        /// <returns>Descriptive error message</returns>
        public static string GetValidationError(string? apiKey, bool requiresAuth)
        {
            if (!requiresAuth)
                return "API key validation not required";

            if (string.IsNullOrWhiteSpace(apiKey))
                return "API key is required for this provider";

            if (apiKey.Length < Constants.Configuration.MinApiKeyLength)
                return $"API key must be at least {Constants.Configuration.MinApiKeyLength} characters";

            if (apiKey.Length > Constants.Configuration.MaxApiKeyLength)
                return $"API key cannot exceed {Constants.Configuration.MaxApiKeyLength} characters";

            if (apiKey.Any(c => char.IsControl(c)))
                return "API key cannot contain control characters";

            return "API key is invalid";
        }

        /// <summary>
        ///     Masks an API key for display purposes while preserving readability.
        /// </summary>
        /// <param name="apiKey">The API key to mask</param>
        /// <returns>Masked API key for safe display</returns>
        public static string Mask(string? apiKey)
        {
            if (string.IsNullOrEmpty(apiKey))
                return Constants.Fallbacks.NotSetValue;

            var visibleLength = Math.Min(Constants.Configuration.ApiKeyMaskLength, apiKey.Length);
            var maskedLength = Math.Max(0, apiKey.Length - visibleLength);

            return apiKey[..visibleLength] + new string('*', maskedLength);
        }
    }

    /// <summary>
    ///     Validation methods for token counts and limits.
    /// </summary>
    public static class TokenCount
    {
        /// <summary>
        ///     Validates if a token count is within acceptable limits.
        /// </summary>
        /// <param name="tokens">The token count to validate</param>
        /// <returns>True if the token count is valid</returns>
        public static bool IsValid(int tokens)
        {
            return tokens >= Constants.Configuration.MinOutputTokens &&
                   tokens <= Constants.Configuration.MaxOutputTokens;
        }

        /// <summary>
        ///     Clamps a token count to valid range.
        /// </summary>
        /// <param name="tokens">The token count to clamp</param>
        /// <returns>Token count within valid range</returns>
        public static int Clamp(int tokens)
        {
            return Math.Max(
                Constants.Configuration.MinOutputTokens,
                Math.Min(Constants.Configuration.MaxOutputTokens, tokens));
        }

        /// <summary>
        ///     Gets validation error message for invalid token counts.
        /// </summary>
        /// <param name="tokens">The invalid token count</param>
        /// <returns>Descriptive error message</returns>
        public static string GetValidationError(int tokens)
        {
            if (tokens < Constants.Configuration.MinOutputTokens)
                return $"Token count must be at least {Constants.Configuration.MinOutputTokens}";

            if (tokens > Constants.Configuration.MaxOutputTokens)
                return $"Token count cannot exceed {Constants.Configuration.MaxOutputTokens}";

            return "Token count is invalid";
        }

        /// <summary>
        ///     Suggests an appropriate token count based on model name.
        /// </summary>
        /// <param name="modelName">The model name to analyze</param>
        /// <returns>Suggested token count</returns>
        public static int GetSuggestedCount(string? modelName)
        {
            if (string.IsNullOrEmpty(modelName))
                return Constants.Configuration.DefaultMaxOutputTokens;

            var model = modelName.ToLowerInvariant();

            // Known reasoning models that need higher token limits
            if (model.StartsWith("o1") || model.StartsWith("o3") ||
                model.StartsWith("o4") || model.StartsWith("o5") ||
                model.Contains("reasoning") || model.Contains("think"))
                return Constants.Configuration.DefaultMaxOutputTokens;

            // Known non-reasoning models that work with lower limits
            if (model.StartsWith("gpt-4.1")) return 500;

            // Unknown models - default to higher limit for safety
            return Constants.Configuration.DefaultMaxOutputTokens;
        }
    }

    /// <summary>
    ///     Validation methods for temperature values.
    /// </summary>
    public static class Temperature
    {
        /// <summary>
        ///     Validates if a temperature value is within acceptable range.
        /// </summary>
        /// <param name="temperature">The temperature to validate</param>
        /// <returns>True if the temperature is valid</returns>
        public static bool IsValid(double temperature)
        {
            return temperature >= Constants.Configuration.MinTemperature &&
                   temperature <= Constants.Configuration.MaxTemperature &&
                   !double.IsNaN(temperature) &&
                   !double.IsInfinity(temperature);
        }

        /// <summary>
        ///     Clamps a temperature value to valid range.
        /// </summary>
        /// <param name="temperature">The temperature to clamp</param>
        /// <returns>Temperature within valid range</returns>
        public static double Clamp(double temperature)
        {
            if (double.IsNaN(temperature) || double.IsInfinity(temperature))
                return Constants.Configuration.DefaultTemperature;

            return Math.Max(
                Constants.Configuration.MinTemperature,
                Math.Min(Constants.Configuration.MaxTemperature, temperature));
        }

        /// <summary>
        ///     Gets validation error message for invalid temperatures.
        /// </summary>
        /// <param name="temperature">The invalid temperature</param>
        /// <returns>Descriptive error message</returns>
        public static string GetValidationError(double temperature)
        {
            if (double.IsNaN(temperature) || double.IsInfinity(temperature))
                return "Temperature must be a valid number";

            if (temperature < Constants.Configuration.MinTemperature)
                return $"Temperature must be at least {Constants.Configuration.MinTemperature}";

            if (temperature > Constants.Configuration.MaxTemperature)
                return $"Temperature cannot exceed {Constants.Configuration.MaxTemperature}";

            return "Temperature is invalid";
        }
    }

    /// <summary>
    ///     Validation methods for provider types and configurations.
    /// </summary>
    public static class Provider
    {
        /// <summary>
        ///     Validates if a provider type is supported.
        /// </summary>
        /// <param name="providerType">The provider type to validate</param>
        /// <returns>True if the provider type is valid</returns>
        public static bool IsValid(string? providerType)
        {
            if (string.IsNullOrWhiteSpace(providerType))
                return false;

            // Currently only OpenAI compatible providers are supported
            return providerType.Equals(Constants.Configuration.ProviderTypeOpenAI,
                       StringComparison.OrdinalIgnoreCase) ||
                   providerType.Equals(Constants.Configuration.ProviderTypeOpenAICompatible,
                       StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        ///     Gets validation error message for invalid provider types.
        /// </summary>
        /// <param name="providerType">The invalid provider type</param>
        /// <returns>Descriptive error message</returns>
        public static string GetValidationError(string? providerType)
        {
            if (string.IsNullOrWhiteSpace(providerType))
                return "Provider type cannot be empty";

            return
                $"Unsupported provider type: {providerType}. Currently supported: {Constants.Configuration.ProviderTypeOpenAI}";
        }
    }

    /// <summary>
    ///     Validation methods for pricing information.
    /// </summary>
    public static class Pricing
    {
        /// <summary>
        ///     Validates if pricing information is acceptable.
        /// </summary>
        /// <param name="pricing">The pricing info to validate</param>
        /// <returns>True if the pricing is valid</returns>
        public static bool IsValid(PricingInfo? pricing)
        {
            if (pricing == null)
                return false;

            // Input and output costs must be non-negative
            if (pricing.InputPer1M < 0 || pricing.OutputPer1M < 0)
                return false;

            // Currency code must be present and valid
            if (string.IsNullOrWhiteSpace(pricing.CurrencyCode))
                return false;

            // Currency code should be 3 letters (ISO 4217)
            if (pricing.CurrencyCode.Length != 3 || !pricing.CurrencyCode.All(char.IsLetter))
                return false;

            return true;
        }

        /// <summary>
        ///     Gets validation error message for invalid pricing.
        /// </summary>
        /// <param name="pricing">The invalid pricing info</param>
        /// <returns>Descriptive error message</returns>
        public static string GetValidationError(PricingInfo? pricing)
        {
            if (pricing == null)
                return "Pricing information is required";

            if (pricing.InputPer1M < 0)
                return "Input cost per million tokens cannot be negative";

            if (pricing.OutputPer1M < 0)
                return "Output cost per million tokens cannot be negative";

            if (string.IsNullOrWhiteSpace(pricing.CurrencyCode))
                return "Currency code is required";

            if (pricing.CurrencyCode.Length != 3 || !pricing.CurrencyCode.All(char.IsLetter))
                return "Currency code must be a 3-letter code (e.g., USD, EUR)";

            return "Pricing information is invalid";
        }
    }

    /// <summary>
    ///     General validation methods for common patterns.
    /// </summary>
    public static class General
    {
        // Methods removed as they were only used for environment variable validation
    }

    /// <summary>
    ///     Provides domain extraction utilities for URL processing.
    /// </summary>
    public static class DomainExtractor
    {
        /// <summary>
        ///     Extracts the domain from a URL, removing www. prefix if present.
        /// </summary>
        /// <param name="url">The URL to extract domain from.</param>
        /// <returns>The extracted domain, or null if extraction fails.</returns>
        public static string? ExtractDomain(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            try
            {
                var uri = new Uri(url, UriKind.Absolute);
                var domain = uri.Host;
                
                // Remove www. prefix if present
                if (domain.StartsWith("www.", StringComparison.OrdinalIgnoreCase))
                    domain = domain.Substring(4);
                
                return domain;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        ///     Gets the provider name from a URL by checking against known provider patterns.
        ///     Returns null if no known provider pattern matches.
        /// </summary>
        /// <param name="url">The URL to check.</param>
        /// <returns>The provider name if matched, or null if no match found.</returns>
        public static string? GetProviderNameFromUrl(string? url)
        {
            if (string.IsNullOrWhiteSpace(url))
                return null;

            var lowerUrl = url.ToLowerInvariant();
            
            // Check each provider pattern (case-insensitive)
            if (lowerUrl.StartsWith(Constants.Providers.XAIUrlPattern.ToLowerInvariant()))
                return Constants.Providers.XAI;

            if (lowerUrl.StartsWith(Constants.Providers.OpenRouterUrlPattern.ToLowerInvariant()))
                return Constants.Providers.OpenRouter;

            if (lowerUrl.StartsWith(Constants.Providers.GroqUrlPattern.ToLowerInvariant()))
                return Constants.Providers.Groq;

            if (lowerUrl.StartsWith(Constants.Providers.OpenAIUrlPattern.ToLowerInvariant()))
                return Constants.Providers.OpenAI;

            if (lowerUrl.StartsWith(Constants.Providers.AnthropicUrlPattern.ToLowerInvariant()))
                return Constants.Providers.Anthropic;

            if (lowerUrl.Contains(Constants.Providers.GoogleGeminiUrlPattern.ToLowerInvariant()))
                return Constants.Providers.GoogleGemini;

            if (lowerUrl.Contains(Constants.Providers.GoogleVertexUrlPattern.ToLowerInvariant()))
                return Constants.Providers.GoogleVertex;

            if (lowerUrl.Contains(Constants.Providers.AzureUrlPattern.ToLowerInvariant()))
                return Constants.Providers.Azure;

            if (lowerUrl.StartsWith(Constants.Providers.LocalUrlPattern.ToLowerInvariant()))
                return Constants.Providers.Local;

            return null;
        }
    }
}