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
    ///     Loads the active model configuration asynchronously.
    /// </summary>
    /// <param name="modelName">Optional specific model name to load.</param>
    /// <returns>The configuration for the specified or default model.</returns>
    public async Task<ModelConfiguration?> LoadConfigurationAsync(string? modelName = null)
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
        bool specificModelRequested = !string.IsNullOrEmpty(modelName);

        if (specificModelRequested)
        {
            _logger.Debug($"Loading specific model: '{modelName}'");
            model = await _secureConfig.GetModelAsync(modelName);
            if (model == null)
            {
                // IMPORTANT: When a specific model is requested but not found,
                // we must return null. Never fall back to the default model.
                _logger.Error($"Model '{modelName}' not found - no fallback will be attempted");
                return null;
            }
            _logger.Debug($"Successfully loaded model '{model.Name}' (requested as '{modelName}')");
        }
        else
        {
            _logger.Debug("No specific model requested, loading default model");
            model = await _secureConfig.GetDefaultModelAsync();
            if (model == null)
            {
                // We have models but no default - this needs healing
                _logger.Debug("Models exist but no default model is set");
                return null;
            }
            _logger.Debug($"Successfully loaded default model '{model.Name}'");
        }

        // Update last used timestamp
        model.LastUsed = DateTime.UtcNow;
        await _secureConfig.UpdateModelAsync(model);

        // Debug log the model configuration
        _logger.Debug("Model configuration loaded:");
        _logger.Debug("  Model.Name: {Name}", model.Name);
        _logger.Debug("  Model.Type: {Type}", model.Type ?? "(null)");
        _logger.Debug("  Model.Provider: {Provider}", model.Provider ?? "(null)");
        _logger.Debug("  Model.Url: {Url}", model.Url ?? "(null)");
        _logger.Debug("  Model.ModelId: {ModelId}", model.ModelId ?? "(null)");

        return model;
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