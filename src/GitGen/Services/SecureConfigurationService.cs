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
        // Check environment variables first (for testing), then fall back to GetFolderPath
        var homeDir = Environment.GetEnvironmentVariable("USERPROFILE") ?? 
                      Environment.GetEnvironmentVariable("HOME") ??
                      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var gitgenDir = Path.Combine(homeDir, ".gitgen");
        Directory.CreateDirectory(gitgenDir);
        _configPath = Path.Combine(gitgenDir, "config.json");

        _logger.Debug("Configuration path: {Path}", _configPath);
    }

    /// <inheritdoc />
    public async Task<GitGenSettings> LoadSettingsAsync()
    {
        if (_cachedSettings != null)
        {
            _logger.Debug("Returning cached settings");
            return _cachedSettings;
        }

        if (!File.Exists(_configPath))
        {
            _logger.Debug("No configuration file found at {Path}", _configPath);
            // Don't cache empty settings - return without caching so next call will check disk again
            return new GitGenSettings
            {
                Settings = new AppSettings { ConfigPath = _configPath }
            };
        }

        try
        {
            _logger.Debug("Loading configuration from {Path}", _configPath);
            var fileContent = await File.ReadAllTextAsync(_configPath);
            _logger.Debug("File content length: {Length} bytes", fileContent.Length);

            // Try to decrypt first (normal case)
            try
            {
                _logger.Debug("Attempting to decrypt configuration");
                var jsonData = _protector.Unprotect(fileContent);
                _logger.Debug("Decrypted JSON length: {Length} characters", jsonData.Length);

                // Log first 200 chars of JSON for debugging
                var preview = jsonData.Length > 200 ? jsonData.Substring(0, 200) + "..." : jsonData;
                _logger.Debug("JSON preview: {Preview}", preview);

                _cachedSettings = JsonSerializer.Deserialize(jsonData, ConfigurationJsonContext.Default.GitGenSettings);

                if (_cachedSettings == null)
                {
                    _logger.Error("Deserialization returned null");
                    return new GitGenSettings
                    {
                        Settings = new AppSettings { ConfigPath = _configPath }
                    };
                }

                _logger.Debug("Successfully deserialized settings with {Count} models", _cachedSettings.Models?.Count ?? 0);

                // Log detailed model information after deserialization
                foreach (var model in _cachedSettings.Models)
                {
                    _logger.Debug($"  Loaded Model '{model.Name}':");
                    _logger.Debug($"    - Aliases count: {model.Aliases?.Count ?? 0}");
                    if (model.Aliases != null && model.Aliases.Count > 0)
                    {
                        _logger.Debug($"    - Aliases: [{string.Join(", ", model.Aliases.Select(a => $"'{a}'"))}]");
                    }
                    else
                    {
                        _logger.Debug($"    - Aliases is {(model.Aliases == null ? "null" : "empty list")}");
                    }
                }

                // Version check immediately after deserialization
                if (_cachedSettings.Version != Constants.Configuration.CurrentConfigVersion)
                {
                    _logger.Debug("Configuration version mismatch. Found: {Found}, Expected: {Expected}",
                        _cachedSettings.Version, Constants.Configuration.CurrentConfigVersion);
                    _logger.Debug("Migrating configuration to new version");

                    // Perform migration
                    _cachedSettings = MigrateConfiguration(_cachedSettings);

                    // Save the migrated configuration
                    await SaveSettingsAsync(_cachedSettings);
                    _logger.Information("Configuration migrated successfully to version {Version}",
                        Constants.Configuration.CurrentConfigVersion);
                }
            }
            catch (Exception decryptEx)
            {
                _logger.Debug("Decryption failed: {Message}", decryptEx.Message);
                _logger.Debug("Exception type: {Type}", decryptEx.GetType().FullName);

                // Fallback: try to read as unencrypted JSON (for debugging or recovery)
                try
                {
                    _logger.Debug("Attempting to read as plain JSON");
                    _cachedSettings = JsonSerializer.Deserialize(fileContent, ConfigurationJsonContext.Default.GitGenSettings);

                    if (_cachedSettings == null)
                    {
                        _logger.Error("Plain JSON deserialization returned null");
                        throw new InvalidOperationException("Deserialization returned null");
                    }

                    _logger.Warning("Loaded unencrypted configuration - will re-encrypt on next save");

                    // Version check for plain JSON path too
                    if (_cachedSettings.Version != Constants.Configuration.CurrentConfigVersion)
                    {
                        _logger.Debug("Plain JSON configuration version mismatch. Found: {Found}, Expected: {Expected}",
                            _cachedSettings.Version, Constants.Configuration.CurrentConfigVersion);
                        _logger.Debug("Migrating configuration to new version");

                        // Perform migration
                        _cachedSettings = MigrateConfiguration(_cachedSettings);

                        // Save the migrated configuration
                        await SaveSettingsAsync(_cachedSettings);
                        _logger.Information("Configuration migrated successfully to version {Version}",
                            Constants.Configuration.CurrentConfigVersion);
                    }
                }
                catch (Exception jsonEx)
                {
                    _logger.Error("Plain JSON deserialization also failed: {Message}", jsonEx.Message);
                    _logger.Debug("JSON Exception type: {Type}", jsonEx.GetType().FullName);

                    // If both decryption and plain JSON fail, it's corrupted
                    _logger.Error("Configuration file is corrupted and cannot be loaded");

                    // Backup the corrupted file
                    var backupPath = _configPath + ".corrupt." + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                    File.Copy(_configPath, backupPath, true);
                    _logger.Information("Corrupted config backed up to: {Path}", backupPath);

                    // Return empty settings without caching
                    return new GitGenSettings
                    {
                        Settings = new AppSettings { ConfigPath = _configPath }
                    };
                }
            }

            // Ensure settings object exists
            if (_cachedSettings.Settings == null)
            {
                _logger.Debug("Settings object was null, creating new instance");
                _cachedSettings.Settings = new AppSettings();
            }

            _cachedSettings.Settings.ConfigPath = _configPath;

            _logger.Debug("Successfully loaded configuration with {Count} models", _cachedSettings.Models.Count);
            if (_cachedSettings.Models.Count > 0)
            {
                _logger.Debug("Default model ID: {DefaultId}, First model: {FirstModel}",
                    _cachedSettings.DefaultModelId ?? "(none)",
                    _cachedSettings.Models.FirstOrDefault()?.Name ?? "(none)");
            }

            _logger.Debug("Configuration loaded successfully");
            return _cachedSettings;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to read configuration file from {Path}", _configPath);
            _logger.Error("Exception details: {Details}", ex.ToString());
            // Return empty settings without caching on error
            return new GitGenSettings
            {
                Settings = new AppSettings { ConfigPath = _configPath }
            };
        }
    }

    /// <inheritdoc />
    public async Task SaveSettingsAsync(GitGenSettings settings)
    {
        try
        {
            _logger.Debug("SaveSettingsAsync called with {ModelCount} models", settings.Models?.Count ?? 0);

            // Log detailed model information before serialization
            foreach (var model in settings.Models)
            {
                _logger.Debug($"  Model '{model.Name}':");
                _logger.Debug($"    - Aliases count: {model.Aliases?.Count ?? 0}");
                if (model.Aliases != null && model.Aliases.Count > 0)
                {
                    _logger.Debug($"    - Aliases: [{string.Join(", ", model.Aliases.Select(a => $"'{a}'"))}]");
                }
            }

            var jsonData = JsonSerializer.Serialize(settings, ConfigurationJsonContext.Default.GitGenSettings);
            _logger.Debug("Serialized JSON length: {Length} characters", jsonData.Length);

            // Log the full JSON for debugging alias issues
            _logger.Debug("Full JSON data being saved:");
            _logger.Debug(jsonData);

            var encryptedData = _protector.Protect(jsonData);
            _logger.Debug("Encrypted data length: {Length} bytes", encryptedData.Length);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_configPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                _logger.Debug("Creating directory: {Directory}", directory);
                Directory.CreateDirectory(directory);
            }

            // Write atomically using temp file
            var tempPath = _configPath + ".tmp";
            _logger.Debug("Writing to temp file: {Path}", tempPath);
            await File.WriteAllTextAsync(tempPath, encryptedData);

            // Verify temp file was written
            if (!File.Exists(tempPath))
            {
                throw new InvalidOperationException($"Temp file was not created at {tempPath}");
            }

            var tempFileSize = new FileInfo(tempPath).Length;
            _logger.Debug("Temp file size: {Size} bytes", tempFileSize);

            _logger.Debug("Moving temp file to final location: {Path}", _configPath);
            File.Move(tempPath, _configPath, true);

            // Verify final file exists
            if (!File.Exists(_configPath))
            {
                throw new InvalidOperationException($"Configuration file was not created at {_configPath}");
            }

            var finalFileSize = new FileInfo(_configPath).Length;
            _logger.Debug("Final file size: {Size} bytes", finalFileSize);

            _cachedSettings = settings;
            _logger.Debug("Configuration saved successfully to {Path}", _configPath);
            _logger.Debug("Saved {Count} models, default model ID: {DefaultId}",
                settings.Models.Count,
                settings.DefaultModelId ?? "(none)");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save configuration to {Path}", _configPath);
            _logger.Error("Exception type: {Type}", ex.GetType().FullName);
            _logger.Error("Exception details: {Details}", ex.ToString());
            throw new InvalidOperationException($"Failed to save configuration: {ex.Message}", ex);
        }
    }

    /// <inheritdoc />
    public async Task<ModelConfiguration?> GetModelAsync(string nameOrId)
    {
        _logger.Debug($"GetModelAsync called with: '{nameOrId}'");
        var settings = await LoadSettingsAsync();

        _logger.Debug($"Total models configured: {settings.Models.Count}");
        _logger.Debug($"Configuration loaded from: {_configPath}");

        // Try exact match by ID/Name (case-insensitive)
        // Note: For new configs, ID and Name are the same. We check both for backward compatibility
        // with old GUID-based configs during migration.
        var model = settings.Models.FirstOrDefault(m =>
            m.Id.Equals(nameOrId, StringComparison.OrdinalIgnoreCase) ||
            m.Name.Equals(nameOrId, StringComparison.OrdinalIgnoreCase));
        if (model != null)
        {
            _logger.Debug($"Exact match found: '{model.Name}'");
            return model;
        }

        // Try exact match by alias (case-insensitive)
        // Handle both "free" and "@free" formats for robustness
        _logger.Debug($"Checking aliases for '{nameOrId}'");

        // Enhanced debug logging to diagnose alias issues
        _logger.Debug($"Total models with aliases check:");
        foreach (var m in settings.Models)
        {
            if (m.Aliases != null && m.Aliases.Count > 0)
            {
                _logger.Debug($"  Model '{m.Name}' has {m.Aliases.Count} aliases: [{string.Join(", ", m.Aliases.Select(a => $"'{a}'"))}]");
            }
            else
            {
                _logger.Debug($"  Model '{m.Name}' has no aliases (Aliases={m.Aliases})");
            }
        }

        model = settings.Models.FirstOrDefault(m =>
            m.Aliases != null && m.Aliases.Any(alias =>
            {
                var normalizedInput = nameOrId.TrimStart('@');
                var normalizedAlias = alias.TrimStart('@');

                var match1 = alias.Equals(nameOrId, StringComparison.OrdinalIgnoreCase);
                var match2 = alias.Equals(normalizedInput, StringComparison.OrdinalIgnoreCase);
                var match3 = normalizedAlias.Equals(normalizedInput, StringComparison.OrdinalIgnoreCase);

                if (match1 || match2 || match3)
                {
                    _logger.Debug($"    MATCH: Alias '{alias}' matches input '{nameOrId}' (match1={match1}, match2={match2}, match3={match3})");
                    return true;
                }

                return false;
            }));
        if (model != null)
        {
            var matchedAlias = model.Aliases!.FirstOrDefault(alias =>
                alias.Equals(nameOrId, StringComparison.OrdinalIgnoreCase) ||
                alias.Equals(nameOrId.TrimStart('@'), StringComparison.OrdinalIgnoreCase) ||
                ("@" + alias).Equals(nameOrId, StringComparison.OrdinalIgnoreCase));
            _logger.Debug("Matched '{Input}' to model '{Model}' via alias '{Alias}'", nameOrId, model.Name, matchedAlias ?? string.Empty);
            return model;
        }

        _logger.Debug($"No exact match found for '{nameOrId}'");

        // If no exact match found and partial matching is enabled, try partial matching
        if (settings.Settings.EnablePartialAliasMatching &&
            nameOrId.Length >= settings.Settings.MinimumAliasMatchLength)
        {
            _logger.Debug($"Attempting partial match (enabled={settings.Settings.EnablePartialAliasMatching}, min length={settings.Settings.MinimumAliasMatchLength})");
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
            else
            {
                _logger.Debug($"No partial matches found for '{nameOrId}'");
            }
        }
        else
        {
            _logger.Debug($"Partial matching not attempted (enabled={settings.Settings.EnablePartialAliasMatching}, input length={nameOrId.Length}, min length={settings.Settings.MinimumAliasMatchLength})");
        }

        _logger.Debug($"GetModelAsync returning null for '{nameOrId}'");
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

        // Ensure the Id is set to the Name
        model.Id = model.Name;

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
        _logger.Debug("Added model '{Name}'", model.Name);
    }

    /// <inheritdoc />
    public async Task UpdateModelAsync(ModelConfiguration model)
    {
        var settings = await LoadSettingsAsync();

        var existingIndex = settings.Models.FindIndex(m => m.Id == model.Id);
        if (existingIndex == -1)
            throw new InvalidOperationException($"Model with ID '{model.Id}' not found");

        var existingModel = settings.Models[existingIndex];
        var oldName = existingModel.Name;

        // If name is changing, validate uniqueness
        if (!model.Name.Equals(oldName, StringComparison.OrdinalIgnoreCase))
        {
            if (settings.Models.Any(m => m.Id != model.Id &&
                m.Name.Equals(model.Name, StringComparison.OrdinalIgnoreCase)))
            {
                throw new InvalidOperationException($"A model named '{model.Name}' already exists");
            }

            // Update the Id to match the new name
            model.Id = model.Name;

            // If this is the default model, update DefaultModelId
            if (settings.DefaultModelId == oldName)
            {
                settings.DefaultModelId = model.Name;
            }
        }

        // Update last used timestamp
        model.LastUsed = DateTime.UtcNow;

        settings.Models[existingIndex] = model;
        await SaveSettingsAsync(settings);
        _logger.Debug("Updated model '{Name}'", model.Name);
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
        _logger.Debug("Deleted model '{Name}'", model.Name);
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
        // Normalize the new alias if provided
        var normalizedNewAlias = newAlias?.TrimStart('@');

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
        if (!string.IsNullOrWhiteSpace(normalizedNewAlias))
        {
            if (allAliases.ContainsKey(normalizedNewAlias))
            {
                return (false, $"Alias '@{normalizedNewAlias}' is already used by model '{allAliases[normalizedNewAlias]}'");
            }

            // Also check if alias conflicts with existing model names
            if (settings.Models.Any(m => m.Id != excludeModelId &&
                m.Name.Equals(normalizedNewAlias, StringComparison.OrdinalIgnoreCase)))
            {
                return (false, $"Alias '@{normalizedNewAlias}' conflicts with an existing model name");
            }
        }

        return (true, null);
    }

    /// <inheritdoc />
    public async Task AddAliasAsync(string modelNameOrId, string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Alias cannot be empty or whitespace", nameof(alias));

        // Normalize alias by removing @ prefix if present
        var normalizedAlias = alias.TrimStart('@');
        _logger.Debug($"AddAliasAsync: normalizing '{alias}' to '{normalizedAlias}'");

        var settings = await LoadSettingsAsync();
        var model = await GetModelAsync(modelNameOrId);

        if (model == null)
            throw new InvalidOperationException($"Model '{modelNameOrId}' not found");

        // Initialize aliases list if null
        if (model.Aliases == null)
            model.Aliases = new List<string>();

        // Check if alias already exists for this model (using normalized version)
        if (model.Aliases.Any(a => a.Equals(normalizedAlias, StringComparison.OrdinalIgnoreCase)))
        {
            _logger.Warning("Alias '{Alias}' already exists for model '{Model}'", normalizedAlias, model.Name);
            return;
        }

        // Validate uniqueness (using normalized alias)
        var validation = ValidateAliasUniqueness(settings, normalizedAlias, model.Id);
        if (!validation.IsValid)
            throw new InvalidOperationException(validation.ErrorMessage);

        // Add the normalized alias (without @ prefix)
        model.Aliases.Add(normalizedAlias);
        await UpdateModelAsync(model);

        _logger.Success($"{Constants.UI.CheckMark} Added alias '@{normalizedAlias}' to model '{model.Name}'");
    }

    /// <inheritdoc />
    public async Task RemoveAliasAsync(string modelNameOrId, string alias)
    {
        if (string.IsNullOrWhiteSpace(alias))
            throw new ArgumentException("Alias cannot be empty or whitespace", nameof(alias));

        // Normalize alias by removing @ prefix if present
        var normalizedAlias = alias.TrimStart('@');
        _logger.Debug($"RemoveAliasAsync: normalizing '{alias}' to '{normalizedAlias}'");

        var model = await GetModelAsync(modelNameOrId);

        if (model == null)
            throw new InvalidOperationException($"Model '{modelNameOrId}' not found");

        if (model.Aliases == null || model.Aliases.Count == 0)
        {
            _logger.Warning("Model '{Model}' has no aliases", model.Name);
            return;
        }

        // Remove alias (case-insensitive, using normalized version)
        var removed = model.Aliases.RemoveAll(a => a.Equals(normalizedAlias, StringComparison.OrdinalIgnoreCase));

        if (removed == 0)
        {
            _logger.Warning("Alias '{Alias}' not found for model '{Model}'", normalizedAlias, model.Name);
            return;
        }

        await UpdateModelAsync(model);
        _logger.Success($"{Constants.UI.CheckMark} Removed alias '@{normalizedAlias}' from model '{model.Name}'");
    }

    /// <inheritdoc />
    public async Task<List<ModelConfiguration>> GetModelsByPartialMatchAsync(string partial)
    {
        _logger.Debug($"GetModelsByPartialMatchAsync called with: '{partial}'");

        if (string.IsNullOrWhiteSpace(partial))
        {
            _logger.Debug("Partial string is null or whitespace, returning empty list");
            return new List<ModelConfiguration>();
        }

        var settings = await LoadSettingsAsync();

        // Check if partial matching is enabled and meets minimum length
        if (!settings.Settings.EnablePartialAliasMatching ||
            partial.Length < settings.Settings.MinimumAliasMatchLength)
        {
            _logger.Debug($"Partial matching criteria not met (enabled={settings.Settings.EnablePartialAliasMatching}, length={partial.Length}, min={settings.Settings.MinimumAliasMatchLength})");
            return new List<ModelConfiguration>();
        }

        // Normalize the partial string by removing @ prefix if present
        var normalizedPartial = partial.TrimStart('@');

        // Find models where name or any alias contains the partial string (case-insensitive)
        // Check both with and without @ prefix for robustness
        var matches = settings.Models.Where(m =>
            m.Name.Contains(partial, StringComparison.OrdinalIgnoreCase) ||
            m.Name.Contains(normalizedPartial, StringComparison.OrdinalIgnoreCase) ||
            (m.Aliases != null && m.Aliases.Any(alias =>
                alias.Contains(partial, StringComparison.OrdinalIgnoreCase) ||
                alias.Contains(normalizedPartial, StringComparison.OrdinalIgnoreCase) ||
                ("@" + alias).Contains(partial, StringComparison.OrdinalIgnoreCase)))
        ).ToList();

        _logger.Debug($"Partial match results for '{partial}': {matches.Count} matches");
        foreach (var match in matches)
        {
            var aliasInfo = match.Aliases != null && match.Aliases.Count > 0
                ? $" (aliases: {string.Join(", ", match.Aliases)})"
                : "";
            _logger.Debug($"  - {match.Name}{aliasInfo}");
        }

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
        // Check environment variables first (for testing), then fall back to GetFolderPath
        var homeDir = Environment.GetEnvironmentVariable("USERPROFILE") ?? 
                      Environment.GetEnvironmentVariable("HOME") ??
                      Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var keyPath = Path.Combine(homeDir, ".gitgen", "keys");
        Directory.CreateDirectory(keyPath);
        return keyPath;
    }

    /// <summary>
    ///     Migrates configuration from older versions to the current version.
    /// </summary>
    /// <param name="settings">The settings to migrate.</param>
    /// <returns>The migrated settings.</returns>
    private GitGenSettings MigrateConfiguration(GitGenSettings settings)
    {
        _logger.Debug("Starting configuration migration from version {OldVersion} to {NewVersion}",
            settings.Version, Constants.Configuration.CurrentConfigVersion);

        // Migrate from version 3.0 to 4.0: Convert GUID IDs to model names
        if (settings.Version == "3.0")
        {
            _logger.Debug("Migrating from version 3.0: Converting GUID IDs to model names");

            foreach (var model in settings.Models)
            {
                var oldId = model.Id;

                // Check if the ID looks like a GUID
                if (Guid.TryParse(oldId, out _))
                {
                    // Update the ID to be the model name
                    model.Id = model.Name;
                    _logger.Debug("Migrated model '{Name}' from ID '{OldId}' to '{NewId}'",
                        model.Name, oldId, model.Id);

                    // If this model was the default, update the DefaultModelId
                    if (settings.DefaultModelId == oldId)
                    {
                        settings.DefaultModelId = model.Name;
                        _logger.Debug("Updated DefaultModelId from '{OldId}' to '{NewId}'",
                            oldId, model.Name);
                    }
                }
                else
                {
                    _logger.Debug("Model '{Name}' already has non-GUID ID '{Id}', skipping",
                        model.Name, model.Id);
                }
            }

            // Update version
            settings.Version = "4.0";
        }

        // Future migrations can be added here
        // if (settings.Version == "4.0") { ... migrate to 5.0 ... }

        return settings;
    }
    
    /// <summary>
    ///     Clears the cached settings. This is primarily for testing purposes.
    /// </summary>
    public void ClearCache()
    {
        _cachedSettings = null;
    }
}