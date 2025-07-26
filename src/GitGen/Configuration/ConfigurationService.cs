using GitGen.Services;

namespace GitGen.Configuration;

/// <summary>
///     Service for loading and validating GitGen configuration from environment variables.
///     Handles parsing, validation, and error reporting for application configuration.
/// </summary>
public class ConfigurationService(IConsoleLogger logger)
{
    /// <summary>
    ///     Loads GitGen configuration from environment variables and validates the resulting configuration.
    /// </summary>
    /// <returns>A GitGenConfiguration object populated from environment variables.</returns>
    public GitGenConfiguration LoadConfiguration()
    {
        logger.Debug("Loading configuration from environment variables.");

        var config = new GitGenConfiguration
        {
            ProviderType = Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ProviderType),
            BaseUrl = Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.BaseUrl),
            Model = Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.Model),
            ApiKey = Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.ApiKey),
            RequiresAuth = ParseBooleanWithDefault(
                Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.RequiresAuth),
                true),
            OpenAiUseLegacyMaxTokens = ParseBooleanWithDefault(
                Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.UseLegacyMaxTokens),
                false),
            Temperature = ParseTemperatureWithDefault(
                Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.Temperature)),
            MaxOutputTokens = ParseTokenCountWithDefault(
                Environment.GetEnvironmentVariable(Constants.EnvironmentVariables.MaxOutputTokens))
        };

        // Validate and report issues
        ValidateConfiguration(config);

        return config;
    }

    /// <summary>
    ///     Validates the loaded configuration and logs specific issues found.
    /// </summary>
    /// <param name="config">The configuration to validate</param>
    private void ValidateConfiguration(GitGenConfiguration config)
    {
        if (config.IsValid)
        {
            logger.Debug("Configuration validation successful");
            return;
        }

        logger.Warning("Configuration validation failed:");

        // Validate each component and provide specific feedback
        if (!ValidationService.Provider.IsValid(config.ProviderType))
            logger.Warning("- {Error}", ValidationService.Provider.GetValidationError(config.ProviderType));

        if (!ValidationService.Url.IsValid(config.BaseUrl))
            logger.Warning("- {Error}", ValidationService.Url.GetValidationError(config.BaseUrl));

        if (!ValidationService.Model.IsValid(config.Model))
            logger.Warning("- {Error}", ValidationService.Model.GetValidationError(config.Model));

        if (!ValidationService.ApiKey.IsValid(config.ApiKey, config.RequiresAuth))
            logger.Warning("- {Error}",
                ValidationService.ApiKey.GetValidationError(config.ApiKey, config.RequiresAuth));

        if (!ValidationService.Temperature.IsValid(config.Temperature))
            logger.Warning("- {Error}", ValidationService.Temperature.GetValidationError(config.Temperature));

        if (!ValidationService.TokenCount.IsValid(config.MaxOutputTokens))
            logger.Warning("- {Error}", ValidationService.TokenCount.GetValidationError(config.MaxOutputTokens));
    }

    /// <summary>
    ///     Parses a boolean value with a specified default.
    /// </summary>
    /// <param name="value">The string value to parse</param>
    /// <param name="defaultValue">The default value if parsing fails</param>
    /// <returns>Parsed boolean or default value</returns>
    private bool ParseBooleanWithDefault(string? value, bool defaultValue)
    {
        if (string.IsNullOrEmpty(value))
            return defaultValue;

        return bool.TryParse(value, out var result) ? result : defaultValue;
    }

    /// <summary>
    ///     Parses a temperature value with validation and default fallback.
    /// </summary>
    /// <param name="value">The string value to parse</param>
    /// <returns>Valid temperature value</returns>
    private double ParseTemperatureWithDefault(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return Constants.Configuration.DefaultTemperature;

        if (!double.TryParse(value, out var temperature))
        {
            logger.Debug("Invalid temperature value '{Value}', using default {Default}",
                value, Constants.Configuration.DefaultTemperature);
            return Constants.Configuration.DefaultTemperature;
        }

        if (!ValidationService.Temperature.IsValid(temperature))
        {
            var clampedTemperature = ValidationService.Temperature.Clamp(temperature);
            logger.Warning("Temperature value {Value} is out of range. Clamped to {ClampedValue}.",
                temperature, clampedTemperature);
            return clampedTemperature;
        }

        return temperature;
    }

    /// <summary>
    ///     Parses a token count value with validation and default fallback.
    /// </summary>
    /// <param name="value">The string value to parse</param>
    /// <returns>Valid token count value</returns>
    private int ParseTokenCountWithDefault(string? value)
    {
        if (string.IsNullOrEmpty(value))
            return Constants.Configuration.DefaultMaxOutputTokens;

        if (!int.TryParse(value, out var tokens))
        {
            logger.Debug("Invalid token count value '{Value}', using default {Default}",
                value, Constants.Configuration.DefaultMaxOutputTokens);
            return Constants.Configuration.DefaultMaxOutputTokens;
        }

        if (!ValidationService.TokenCount.IsValid(tokens))
        {
            var clampedTokens = ValidationService.TokenCount.Clamp(tokens);
            logger.Warning(Constants.ErrorMessages.TokensOutOfRange,
                tokens,
                Constants.Configuration.MinOutputTokens,
                Constants.Configuration.MaxOutputTokens,
                clampedTokens);
            return clampedTokens;
        }

        return tokens;
    }
}