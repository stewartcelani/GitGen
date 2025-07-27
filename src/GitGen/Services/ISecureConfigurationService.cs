using GitGen.Configuration;

namespace GitGen.Services;

/// <summary>
///     Interface for secure configuration management with multi-model support.
/// </summary>
public interface ISecureConfigurationService
{
    /// <summary>
    ///     Loads the complete GitGen settings from encrypted storage.
    /// </summary>
    /// <returns>The loaded settings or a new instance if none exist.</returns>
    Task<GitGenSettings> LoadSettingsAsync();

    /// <summary>
    ///     Saves the complete GitGen settings to encrypted storage.
    /// </summary>
    /// <param name="settings">The settings to save.</param>
    Task SaveSettingsAsync(GitGenSettings settings);

    /// <summary>
    ///     Gets a specific model configuration by name or ID.
    /// </summary>
    /// <param name="nameOrId">The name or ID of the model to retrieve.</param>
    /// <returns>The model configuration if found; otherwise, null.</returns>
    Task<ModelConfiguration?> GetModelAsync(string nameOrId);

    /// <summary>
    ///     Gets the default model configuration.
    /// </summary>
    /// <returns>The default model if configured; otherwise, null.</returns>
    Task<ModelConfiguration?> GetDefaultModelAsync();

    /// <summary>
    ///     Adds a new model configuration.
    /// </summary>
    /// <param name="model">The model configuration to add.</param>
    Task AddModelAsync(ModelConfiguration model);

    /// <summary>
    ///     Updates an existing model configuration.
    /// </summary>
    /// <param name="model">The model configuration to update.</param>
    Task UpdateModelAsync(ModelConfiguration model);

    /// <summary>
    ///     Deletes a model configuration by name or ID.
    /// </summary>
    /// <param name="nameOrId">The name or ID of the model to delete.</param>
    Task DeleteModelAsync(string nameOrId);

    /// <summary>
    ///     Sets the default model by name or ID.
    /// </summary>
    /// <param name="nameOrId">The name or ID of the model to set as default.</param>
    Task SetDefaultModelAsync(string nameOrId);

    /// <summary>
    ///     Adds an alias to a model configuration.
    /// </summary>
    /// <param name="modelNameOrId">The name or ID of the model to add the alias to.</param>
    /// <param name="alias">The alias to add.</param>
    Task AddAliasAsync(string modelNameOrId, string alias);

    /// <summary>
    ///     Removes an alias from a model configuration.
    /// </summary>
    /// <param name="modelNameOrId">The name or ID of the model to remove the alias from.</param>
    /// <param name="alias">The alias to remove.</param>
    Task RemoveAliasAsync(string modelNameOrId, string alias);

    /// <summary>
    ///     Gets models that match a partial name or alias.
    /// </summary>
    /// <param name="partial">The partial name or alias to match.</param>
    /// <returns>A list of models that match the partial string.</returns>
    Task<List<ModelConfiguration>> GetModelsByPartialMatchAsync(string partial);

    /// <summary>
    ///     Heals broken default model configuration by prompting user to select a new default.
    /// </summary>
    /// <param name="logger">The console logger for user interaction.</param>
    /// <returns>True if healing was successful; otherwise, false.</returns>
    Task<bool> HealDefaultModelAsync(IConsoleLogger logger);
}