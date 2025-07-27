using GitGen.Services;

namespace GitGen.Configuration;

/// <summary>
///     Service for loading and validating GitGen configuration.
///     Provides backward compatibility while using the new secure multi-model configuration system.
/// </summary>
public class ConfigurationService
{
    private readonly IConsoleLogger _logger;
    private readonly ISecureConfigurationService? _secureConfig;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigurationService" /> class.
    /// </summary>
    /// <param name="logger">The console logger for user interaction and debugging.</param>
    /// <param name="secureConfig">Optional secure configuration service for multi-model support.</param>
    public ConfigurationService(IConsoleLogger logger, ISecureConfigurationService? secureConfig = null)
    {
        _logger = logger;
        _secureConfig = secureConfig;
    }

    /// <summary>
    ///     Loads GitGen configuration, attempting secure storage first, then falling back to environment variables.
    /// </summary>
    /// <returns>A GitGenConfiguration object populated from the active model or environment variables.</returns>
    public GitGenConfiguration LoadConfiguration()
    {
        // If we have secure config service, try to load from it first
        if (_secureConfig != null)
        {
            var task = LoadConfigurationAsync();
            task.Wait();
            var config = task.Result;
            if (config != null && config.IsValid)
                return config;
        }

        // Fall back to environment variables for backward compatibility
        return LoadFromEnvironmentVariables();
    }

    /// <summary>
    ///     Loads the active model configuration asynchronously.
    /// </summary>
    /// <param name="modelName">Optional specific model name to load.</param>
    /// <returns>The configuration for the specified or default model.</returns>
    public async Task<GitGenConfiguration?> LoadConfigurationAsync(string? modelName = null)
    {
        if (_secureConfig == null)
            return LoadFromEnvironmentVariables();

        ModelConfiguration? model;

        if (!string.IsNullOrEmpty(modelName))
        {
            model = await _secureConfig.GetModelAsync(modelName);
            if (model == null)
            {
                _logger.Error($"Model '{modelName}' not found");
                return null;
            }
        }
        else
        {
            model = await _secureConfig.GetDefaultModelAsync();
            if (model == null)
            {
                _logger.Debug("No models configured in secure storage");
                return LoadFromEnvironmentVariables();
            }
        }

        // Update last used timestamp
        model.LastUsed = DateTime.UtcNow;
        await _secureConfig.UpdateModelAsync(model);

        // Convert to GitGenConfiguration for backward compatibility
        return ConvertToGitGenConfiguration(model);
    }

    /// <summary>
    ///     Gets the active model configuration for use in providers.
    /// </summary>
    /// <returns>The active model configuration if using secure storage; otherwise, null.</returns>
    public async Task<ModelConfiguration?> GetActiveModelAsync()
    {
        if (_secureConfig == null)
            return null;

        return await _secureConfig.GetDefaultModelAsync();
    }

    /// <summary>
    ///     Converts a ModelConfiguration to GitGenConfiguration for backward compatibility.
    /// </summary>
    private GitGenConfiguration ConvertToGitGenConfiguration(ModelConfiguration model)
    {
        return new GitGenConfiguration
        {
            ProviderType = model.ProviderType,
            BaseUrl = model.Url,  // Map Url back to BaseUrl for compatibility
            Model = model.ModelId,
            ApiKey = model.ApiKey,
            RequiresAuth = model.RequiresAuth,
            OpenAiUseLegacyMaxTokens = model.UseLegacyMaxTokens,
            Temperature = model.Temperature,
            MaxOutputTokens = model.MaxOutputTokens,
            SystemPrompt = model.SystemPrompt
        };
    }

    /// <summary>
    ///     Loads GitGen configuration from environment variables.
    /// </summary>
    /// <returns>A GitGenConfiguration object populated from environment variables.</returns>
    private GitGenConfiguration LoadFromEnvironmentVariables()
    {
        _logger.Debug("Loading configuration from environment variables.");

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
            _logger.Debug("Configuration validation successful");
            return;
        }

        _logger.Warning("Configuration validation failed:");

        // Validate each component and provide specific feedback
        if (!ValidationService.Provider.IsValid(config.ProviderType))
            _logger.Warning("- {Error}", ValidationService.Provider.GetValidationError(config.ProviderType));

        if (!ValidationService.Url.IsValid(config.BaseUrl))
            _logger.Warning("- {Error}", ValidationService.Url.GetValidationError(config.BaseUrl));

        if (!ValidationService.Model.IsValid(config.Model))
            _logger.Warning("- {Error}", ValidationService.Model.GetValidationError(config.Model));

        if (!ValidationService.ApiKey.IsValid(config.ApiKey, config.RequiresAuth))
            _logger.Warning("- {Error}",
                ValidationService.ApiKey.GetValidationError(config.ApiKey, config.RequiresAuth));

        if (!ValidationService.Temperature.IsValid(config.Temperature))
            _logger.Warning("- {Error}", ValidationService.Temperature.GetValidationError(config.Temperature));

        if (!ValidationService.TokenCount.IsValid(config.MaxOutputTokens))
            _logger.Warning("- {Error}", ValidationService.TokenCount.GetValidationError(config.MaxOutputTokens));
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
            _logger.Debug("Invalid temperature value '{Value}', using default {Default}",
                value, Constants.Configuration.DefaultTemperature);
            return Constants.Configuration.DefaultTemperature;
        }

        if (!ValidationService.Temperature.IsValid(temperature))
        {
            var clampedTemperature = ValidationService.Temperature.Clamp(temperature);
            _logger.Warning("Temperature value {Value} is out of range. Clamped to {ClampedValue}.",
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
            _logger.Debug("Invalid token count value '{Value}', using default {Default}",
                value, Constants.Configuration.DefaultMaxOutputTokens);
            return Constants.Configuration.DefaultMaxOutputTokens;
        }

        if (!ValidationService.TokenCount.IsValid(tokens))
        {
            var clampedTokens = ValidationService.TokenCount.Clamp(tokens);
            _logger.Warning(Constants.ErrorMessages.TokensOutOfRange,
                tokens,
                Constants.Configuration.MinOutputTokens,
                Constants.Configuration.MaxOutputTokens,
                clampedTokens);
            return clampedTokens;
        }

        return tokens;
    }
}