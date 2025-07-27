using System.Text.Json;
using GitGen.Configuration;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.DependencyInjection;

namespace GitGen.Services;

/// <summary>
///     Service for managing GitGen configuration with encryption using Microsoft.AspNetCore.DataProtection.
/// </summary>
public class SecureConfigurationService : ISecureConfigurationService
{
    private readonly IDataProtector _protector;
    private readonly IConsoleLogger _logger;
    private readonly string _configPath;
    private GitGenSettings? _cachedSettings;

    /// <summary>
    ///     Initializes a new instance of the <see cref="SecureConfigurationService" /> class.
    /// </summary>
    /// <param name="logger">The console logger for debugging and error reporting.</param>
    public SecureConfigurationService(IConsoleLogger logger)
    {
        _logger = logger;
        
        // Create DataProtection provider with GitGen-specific purpose
        var serviceCollection = new ServiceCollection();
        serviceCollection.AddDataProtection()
            .SetApplicationName("GitGen")
            .PersistKeysToFileSystem(new DirectoryInfo(GetKeyStorePath()));
        
        var services = serviceCollection.BuildServiceProvider();
        var dataProtectionProvider = services.GetRequiredService<IDataProtectionProvider>();
        _protector = dataProtectionProvider.CreateProtector("GitGen.Configuration");

        // Set up configuration directory in user's home
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var gitgenDir = Path.Combine(homeDir, ".gitgen");
        Directory.CreateDirectory(gitgenDir);
        _configPath = Path.Combine(gitgenDir, "config.json");
        
        _logger.Debug("Configuration path: {Path}", _configPath);
    }

    /// <inheritdoc />
    public async Task<GitGenSettings> LoadSettingsAsync()
    {
        if (_cachedSettings != null)
            return _cachedSettings;

        if (!File.Exists(_configPath))
        {
            _logger.Debug("No configuration file found at {Path}", _configPath);
            _cachedSettings = new GitGenSettings 
            { 
                Settings = new AppSettings { ConfigPath = _configPath } 
            };
            return _cachedSettings;
        }

        try
        {
            var fileContent = await File.ReadAllTextAsync(_configPath);
            
            // Try to decrypt first (normal case)
            try
            {
                var jsonData = _protector.Unprotect(fileContent);
                _cachedSettings = JsonSerializer.Deserialize(jsonData, ConfigurationJsonContext.Default.GitGenSettings) 
                    ?? new GitGenSettings();
            }
            catch (Exception decryptEx)
            {
                _logger.Debug("Failed to decrypt configuration, trying as plain JSON: {Message}", decryptEx.Message);
                
                // Fallback: try to read as unencrypted JSON (for debugging or recovery)
                try
                {
                    _cachedSettings = JsonSerializer.Deserialize(fileContent, ConfigurationJsonContext.Default.GitGenSettings) 
                        ?? new GitGenSettings();
                    _logger.Warning("Loaded unencrypted configuration - will re-encrypt on next save");
                }
                catch
                {
                    // If both decryption and plain JSON fail, it's corrupted
                    _logger.Error("Configuration file is corrupted and cannot be loaded");
                    
                    // Backup the corrupted file
                    var backupPath = _configPath + ".corrupt." + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    File.Copy(_configPath, backupPath, true);
                    _logger.Information("Corrupted config backed up to: {Path}", backupPath);
                    
                    // Return empty settings
                    _cachedSettings = new GitGenSettings 
                    { 
                        Settings = new AppSettings { ConfigPath = _configPath } 
                    };
                    return _cachedSettings;
                }
            }
            
            // Ensure settings object exists
            if (_cachedSettings.Settings == null)
                _cachedSettings.Settings = new AppSettings();
            
            _cachedSettings.Settings.ConfigPath = _configPath;
            
            _logger.Debug("Successfully loaded configuration with {Count} models", _cachedSettings.Models.Count);
            if (_cachedSettings.Models.Count > 0)
            {
                _logger.Debug("Default model ID: {DefaultId}, First model: {FirstModel}", 
                    _cachedSettings.DefaultModelId ?? "(none)",
                    _cachedSettings.Models.FirstOrDefault()?.Name ?? "(none)");
            }
            return _cachedSettings;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to read configuration file from {Path}", _configPath);
            _cachedSettings = new GitGenSettings 
            { 
                Settings = new AppSettings { ConfigPath = _configPath } 
            };
            return _cachedSettings;
        }
    }

    /// <inheritdoc />
    public async Task SaveSettingsAsync(GitGenSettings settings)
    {
        try
        {
            var jsonData = JsonSerializer.Serialize(settings, ConfigurationJsonContext.Default.GitGenSettings);
            var encryptedData = _protector.Protect(jsonData);
            
            // Write atomically using temp file
            var tempPath = _configPath + ".tmp";
            await File.WriteAllTextAsync(tempPath, encryptedData);
            File.Move(tempPath, _configPath, true);
            
            _cachedSettings = settings;
            _logger.Debug("Configuration saved successfully to {Path}", _configPath);
            _logger.Debug("Saved {Count} models, default model ID: {DefaultId}", 
                settings.Models.Count, 
                settings.DefaultModelId ?? "(none)");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save configuration to {Path}", _configPath);
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<ModelConfiguration?> GetModelAsync(string nameOrId)
    {
        var settings = await LoadSettingsAsync();

        // Try exact match by ID first
        var model = settings.Models.FirstOrDefault(m => m.Id == nameOrId);
        if (model != null) return model;

        // Try exact match by name (case-insensitive)
        model = settings.Models.FirstOrDefault(m =>
            m.Name.Equals(nameOrId, StringComparison.OrdinalIgnoreCase));
        if (model != null) return model;

        // Try exact match by alias (case-insensitive)
        model = settings.Models.FirstOrDefault(m =>
            m.Aliases != null && m.Aliases.Any(alias => 
                alias.Equals(nameOrId, StringComparison.OrdinalIgnoreCase)));
        if (model != null)
        {
            _logger.Debug("Matched '{Input}' to model '{Model}' via alias", nameOrId, model.Name);
            return model;
        }

        // If no exact match found and partial matching is enabled, try partial matching
        if (settings.Settings.EnablePartialAliasMatching && 
            nameOrId.Length >= settings.Settings.MinimumAliasMatchLength)
        {
            var partialMatches = await GetModelsByPartialMatchAsync(nameOrId);
            
            if (partialMatches.Count == 1)
            {
                _logger.Debug("Partial matched '{Input}' to model '{Model}'", nameOrId, partialMatches[0].Name);
                return partialMatches[0];
            }
            
            if (partialMatches.Count > 1)
            {
                var matchNames = string.Join(", ", partialMatches.Select(m => m.Name));
                _logger.Debug("Multiple models match '{Input}': {Matches}", nameOrId, matchNames);
            }
        }

        return null;
    }

    /// <inheritdoc />
    public async Task<ModelConfiguration?> GetDefaultModelAsync()
    {
        var settings = await LoadSettingsAsync();

        if (string.IsNullOrEmpty(settings.DefaultModelId))
            return settings.Models.FirstOrDefault();

        return settings.Models.FirstOrDefault(m => m.Id == settings.DefaultModelId);
    }

    /// <inheritdoc />
    public async Task AddModelAsync(ModelConfiguration model)
    {
        var settings = await LoadSettingsAsync();
        
        // Ensure unique name
        if (settings.Models.Any(m => m.Name.Equals(model.Name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"A model named '{model.Name}' already exists");
        
        // Validate aliases if any are provided
        if (model.Aliases != null && model.Aliases.Count > 0)
        {
            foreach (var alias in model.Aliases)
            {
                var validation = ValidateAliasUniqueness(settings, alias);
                if (!validation.IsValid)
                    throw new InvalidOperationException(validation.ErrorMessage);
            }
        }
        
        settings.Models.Add(model);
        
        // Set as default if it's the first model
        if (settings.Models.Count == 1)
            settings.DefaultModelId = model.Id;
        
        await SaveSettingsAsync(settings);
        _logger.Debug("Added model '{Name}' with ID {Id}", model.Name, model.Id);
    }

    /// <inheritdoc />
    public async Task UpdateModelAsync(ModelConfiguration model)
    {
        var settings = await LoadSettingsAsync();
        
        var existingIndex = settings.Models.FindIndex(m => m.Id == model.Id);
        if (existingIndex == -1)
            throw new InvalidOperationException($"Model with ID '{model.Id}' not found");
        
        // Update last used timestamp
        model.LastUsed = DateTime.UtcNow;
        
        settings.Models[existingIndex] = model;
        await SaveSettingsAsync(settings);
        _logger.Debug("Updated model '{Name}' with ID {Id}", model.Name, model.Id);
    }

    /// <inheritdoc />
    public async Task DeleteModelAsync(string nameOrId)
    {
        var settings = await LoadSettingsAsync();
        
        var model = await GetModelAsync(nameOrId);
        if (model == null)
            throw new InvalidOperationException($"Model '{nameOrId}' not found");
        
        settings.Models.RemoveAll(m => m.Id == model.Id);
        
        // Update default if necessary
        if (settings.DefaultModelId == model.Id)
            settings.DefaultModelId = settings.Models.FirstOrDefault()?.Id;
        
        await SaveSettingsAsync(settings);
        _logger.Debug("Deleted model '{Name}' with ID {Id}", model.Name, model.Id);
    }

    /// <inheritdoc />
    public async Task SetDefaultModelAsync(string nameOrId)
    {
        var settings = await LoadSettingsAsync();
        
        var model = await GetModelAsync(nameOrId);
        if (model == null)
            throw new InvalidOperationException($"Model '{nameOrId}' not found");
        
        settings.DefaultModelId = model.Id;
        
        await SaveSettingsAsync(settings);
        _logger.Debug("Set model '{Name}' as default", model.Name);
    }


    /// <summary>
    ///     Validates that all aliases are unique across all models.
    /// </summary>
    /// <param name="settings">The settings to validate.</param>
    /// <param name="newAlias">Optional new alias being added (for validation before adding).</param>
    /// <param name="excludeModelId">Optional model ID to exclude from validation (when updating a model's aliases).</param>
    /// <returns>Validation result with error message if not unique.</returns>
    public (bool IsValid, string? ErrorMessage) ValidateAliasUniqueness(
        GitGenSettings settings, 
        string? newAlias = null, 
        string? excludeModelId = null)
    {
        var allAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        
        // Collect all existing aliases
        foreach (var model in settings.Models)
        {
            if (model.Id == excludeModelId) continue;
            
            if (model.Aliases != null)
            {
                foreach (var alias in model.Aliases)
                {
                    if (allAliases.ContainsKey(alias))
                    {
                        return (false, $"Duplicate alias '{alias}' found between models '{allAliases[alias]}' and '{model.Name}'");
                    }
                    allAliases[alias] = model.Name;
                }
            }
        }
        
        // Check new alias if provided
        if (!string.IsNullOrWhiteSpace(newAlias))
        {
            if (allAliases.ContainsKey(newAlias))
            {
                return (false, $"Alias '{newAlias}' is already used by model '{allAliases[newAlias]}'");
            }
            
            // Also check if alias conflicts with existing model names
            if (settings.Models.Any(m => m.Id != excludeModelId && 
                m.Name.Equals(newAlias, StringComparison.OrdinalIgnoreCase)))
            {
                return (false, $"Alias '{newAlias}' conflicts with an existing model name");
            }
        }
        
        return (true, null);
    }

    /// <inheritdoc />
    public async Task AddAliasAsync(string modelNameOrId, string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Alias cannot be empty or whitespace", nameof(alias));
        
        var settings = await LoadSettingsAsync();
        var model = await GetModelAsync(modelNameOrId);
        
        if (model == null)
            throw new InvalidOperationException($"Model '{modelNameOrId}' not found");
        
        // Initialize aliases list if null
        if (model.Aliases == null)
            model.Aliases = new List<string>();
        
        // Check if alias already exists for this model
        if (model.Aliases.Any(a => a.Equals(alias, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.Warning("Alias '{Alias}' already exists for model '{Model}'", alias, model.Name);
            return;
        }
        
        // Validate uniqueness
        var validation = ValidateAliasUniqueness(settings, alias, model.Id);
        if (!validation.IsValid)
            throw new InvalidOperationException(validation.ErrorMessage);
        
        // Add the alias
        model.Aliases.Add(alias);
        await UpdateModelAsync(model);
        
        _logger.Success($"{Constants.UI.CheckMark} Added alias '{alias}' to model '{model.Name}'");
    }

    /// <inheritdoc />
    public async Task RemoveAliasAsync(string modelNameOrId, string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Alias cannot be empty or whitespace", nameof(alias));
        
        var model = await GetModelAsync(modelNameOrId);
        
        if (model == null)
            throw new InvalidOperationException($"Model '{modelNameOrId}' not found");
        
        if (model.Aliases == null || model.Aliases.Count == 0)
        {
            _logger.Warning("Model '{Model}' has no aliases", model.Name);
            return;
        }
        
        // Remove alias (case-insensitive)
        var removed = model.Aliases.RemoveAll(a => a.Equals(alias, StringComparison.OrdinalIgnoreCase));
        
        if (removed == 0)
        {
            _logger.Warning("Alias '{Alias}' not found for model '{Model}'", alias, model.Name);
            return;
        }
        
        await UpdateModelAsync(model);
        _logger.Success($"{Constants.UI.CheckMark} Removed alias '{alias}' from model '{model.Name}'");
    }

    /// <inheritdoc />
    public async Task<List<ModelConfiguration>> GetModelsByPartialMatchAsync(string partial)
    {
        if (string.IsNullOrWhiteSpace(partial))
            return new List<ModelConfiguration>();
        
        var settings = await LoadSettingsAsync();
        
        // Check if partial matching is enabled and meets minimum length
        if (!settings.Settings.EnablePartialAliasMatching || 
            partial.Length < settings.Settings.MinimumAliasMatchLength)
        {
            return new List<ModelConfiguration>();
        }
        
        // Find models where name or any alias starts with the partial string (case-insensitive)
        var matches = settings.Models.Where(m =>
            m.Name.StartsWith(partial, StringComparison.OrdinalIgnoreCase) ||
            (m.Aliases != null && m.Aliases.Any(alias => 
                alias.StartsWith(partial, StringComparison.OrdinalIgnoreCase)))
        ).ToList();
        
        return matches;
    }

    /// <inheritdoc />
    public async Task<bool> HealDefaultModelAsync(IConsoleLogger logger)
    {
        var settings = await LoadSettingsAsync();
        
        // If no models exist, we can't heal anything
        if (settings.Models.Count == 0)
        {
            logger.Debug("No models exist, cannot heal default model");
            return false;
        }
        
        // Check if default model is missing or invalid
        bool needsHealing = false;
        string? healingReason = null;
        
        if (string.IsNullOrEmpty(settings.DefaultModelId))
        {
            needsHealing = true;
            healingReason = "There is currently no default model set.";
        }
        else if (!settings.Models.Any(m => m.Id == settings.DefaultModelId))
        {
            needsHealing = true;
            healingReason = $"The default model ID '{settings.DefaultModelId}' refers to a model that no longer exists.";
        }
        
        if (!needsHealing)
        {
            logger.Debug("Default model configuration is valid");
            return false;
        }
        
        // If only one model exists, auto-set it as default
        if (settings.Models.Count == 1)
        {
            var singleModel = settings.Models[0];
            logger.Information($"{Constants.UI.WarningSymbol} {healingReason}");
            logger.Information($"Setting '{singleModel.Name}' as the default model (only model available).");
            
            settings.DefaultModelId = singleModel.Id;
            await SaveSettingsAsync(settings);
            logger.Success($"{Constants.UI.CheckMark} Default model set to '{singleModel.Name}'");
            return true;
        }
        
        // Multiple models exist, prompt user to select
        logger.Information($"{Constants.UI.WarningSymbol} {healingReason}");
        logger.Information("Which model do you want to use as the default?");
        logger.Information("");
        
        // Display models with numbers
        for (int i = 0; i < settings.Models.Count; i++)
        {
            var model = settings.Models[i];
            var aliasText = model.Aliases?.Count > 0 
                ? $" (aliases: {string.Join(", ", model.Aliases.Select(a => $"@{a}"))})" 
                : "";
            logger.Information($"  [{i + 1}] {model.Name}{aliasText}");
            if (!string.IsNullOrEmpty(model.Note))
            {
                logger.Information($"      {model.Note}");
            }
        }
        
        logger.Information("");
        
        // Get user choice
        while (true)
        {
            Console.Write("Enter choice (1-" + settings.Models.Count + "): ");
            var input = Console.ReadLine()?.Trim();
            
            if (int.TryParse(input, out var choice) && choice >= 1 && choice <= settings.Models.Count)
            {
                var selectedModel = settings.Models[choice - 1];
                settings.DefaultModelId = selectedModel.Id;
                await SaveSettingsAsync(settings);
                
                logger.Success($"{Constants.UI.CheckMark} Default model set to '{selectedModel.Name}'");
                return true;
            }
            
            logger.Error("Invalid choice. Please enter a number between 1 and " + settings.Models.Count);
        }
    }

    /// <summary>
    ///     Gets the path for storing DataProtection keys.
    /// </summary>
    private static string GetKeyStorePath()
    {
        var homeDir = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var keyPath = Path.Combine(homeDir, ".gitgen", "keys");
        Directory.CreateDirectory(keyPath);
        return keyPath;
    }
}