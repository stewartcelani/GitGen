using GitGen.Services;

namespace GitGen.Configuration;

/// <summary>
///     Service for loading and validating GitGen configuration from secure storage.
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
    ///     Loads GitGen configuration from secure storage.
    /// </summary>
    /// <returns>A GitGenConfiguration object populated from the active model.</returns>
    public GitGenConfiguration LoadConfiguration()
    {
        if (_secureConfig == null)
        {
            _logger.Error("Secure configuration service not available");
            return new GitGenConfiguration();
        }

        var task = LoadConfigurationAsync();
        task.Wait();
        return task.Result ?? new GitGenConfiguration();
    }

    /// <summary>
    ///     Loads the active model configuration asynchronously.
    /// </summary>
    /// <param name="modelName">Optional specific model name to load.</param>
    /// <returns>The configuration for the specified or default model.</returns>
    public async Task<GitGenConfiguration?> LoadConfigurationAsync(string? modelName = null)
    {
        if (_secureConfig == null)
        {
            _logger.Error("Secure configuration service not available");
            return null;
        }

        // Check if we have ANY models in secure storage
        var hasModels = await HasModelsAsync();
        if (!hasModels)
        {
            _logger.Debug("No models configured in secure storage");
            return null;
        }
        
        _logger.Debug("Found models in secure storage");

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
                // We have models but no default - this needs healing
                _logger.Debug("Models exist but no default model is set");
                return null;
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
            Type = model.Type,
            Provider = model.Provider,
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
    ///     Checks if any models are configured in secure storage.
    /// </summary>
    /// <returns>True if at least one model exists; otherwise, false.</returns>
    public async Task<bool> HasModelsAsync()
    {
        if (_secureConfig == null)
            return false;

        var settings = await _secureConfig.LoadSettingsAsync();
        return settings.Models.Count > 0;
    }

    /// <summary>
    ///     Checks if the default model configuration needs healing.
    /// </summary>
    /// <returns>True if healing is needed; otherwise, false.</returns>
    public async Task<bool> NeedsDefaultModelHealingAsync()
    {
        if (_secureConfig == null)
            return false;

        var settings = await _secureConfig.LoadSettingsAsync();
        
        // No models exist, so no healing possible
        if (settings.Models.Count == 0)
        {
            _logger.Debug("No models exist, healing not possible");
            return false;
        }
        
        // Check if default model is missing or invalid
        if (string.IsNullOrEmpty(settings.DefaultModelId))
        {
            _logger.Debug("Default model ID is missing, healing needed");
            return true;
        }
        
        // Check if default model ID points to non-existent model
        if (!settings.Models.Any(m => m.Id == settings.DefaultModelId))
        {
            _logger.Debug("Default model ID '{DefaultId}' points to non-existent model, healing needed", settings.DefaultModelId);
            return true;
        }
        
        _logger.Debug("Default model configuration is valid");
        return false;
    }
}