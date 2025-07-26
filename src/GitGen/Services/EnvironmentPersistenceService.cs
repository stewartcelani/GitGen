using GitGen.Configuration;
using GitGen.Helpers;

namespace GitGen.Services;

/// <summary>
///     Interface for environment variable persistence operations.
///     Provides atomic configuration updates with proper error handling.
/// </summary>
public interface IEnvironmentPersistenceService
{
    /// <summary>
    ///     Saves a complete GitGen configuration to environment variables.
    /// </summary>
    /// <param name="config">The configuration to persist</param>
    void SaveConfiguration(GitGenConfiguration config);

    /// <summary>
    ///     Updates only model-related configuration values.
    /// </summary>
    /// <param name="model">The model name</param>
    /// <param name="useLegacyTokens">Whether to use legacy max_tokens parameter</param>
    /// <param name="temperature">The model temperature</param>
    void UpdateModelConfiguration(string model, bool useLegacyTokens, double temperature);

    /// <summary>
    ///     Clears all GitGen environment variables and configuration.
    /// </summary>
    void ClearConfiguration();
}

/// <summary>
///     Service for managing GitGen environment variable persistence across platforms.
///     Consolidates all environment variable operations with proper error handling and security.
/// </summary>
public class EnvironmentPersistenceService : IEnvironmentPersistenceService
{
    private readonly IConsoleLogger _logger;

    public EnvironmentPersistenceService(IConsoleLogger logger)
    {
        _logger = logger;
    }

    /// <summary>
    ///     Saves a complete GitGen configuration to environment variables.
    ///     Performs atomic updates where possible and validates input values.
    /// </summary>
    /// <param name="config">The configuration to persist</param>
    /// <exception cref="InvalidOperationException">Thrown when persistence fails</exception>
    public void SaveConfiguration(GitGenConfiguration config)
    {
        _logger.Debug("Saving complete GitGen configuration to environment variables");

        // Validate configuration before attempting to save
        var variables = BuildConfigurationVariables(config);
        ValidateVariables(variables);

        try
        {
            // Clear existing configuration first to prevent stale values
            ClearConfiguration();

            // Save all variables
            SaveVariables(variables);

            _logger.Debug("Successfully saved {Count} configuration variables", variables.Count);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to save complete configuration");
            throw new InvalidOperationException(
                string.Format(Constants.ErrorMessages.FailedToSaveConfiguration, ex.Message), ex);
        }
    }

    /// <summary>
    ///     Updates only model-related configuration values without affecting other settings.
    /// </summary>
    /// <param name="model">The model name</param>
    /// <param name="useLegacyTokens">Whether to use legacy max_tokens parameter</param>
    /// <param name="temperature">The model temperature</param>
    /// <exception cref="InvalidOperationException">Thrown when update fails</exception>
    public void UpdateModelConfiguration(string model, bool useLegacyTokens, double temperature)
    {
        _logger.Debug("Updating model configuration: {Model}, Legacy: {Legacy}, Temperature: {Temperature}",
            model, useLegacyTokens, temperature);

        // Validate inputs
        if (!ValidationService.Model.IsValid(model))
            throw new ArgumentException(ValidationService.Model.GetValidationError(model), nameof(model));

        if (!ValidationService.Temperature.IsValid(temperature))
            throw new ArgumentException(ValidationService.Temperature.GetValidationError(temperature),
                nameof(temperature));

        var variables = new Dictionary<string, string>
        {
            ["MODEL"] = model,
            ["OPENAI_USE_LEGACY_MAX_TOKENS"] = useLegacyTokens.ToString().ToLower(),
            ["TEMPERATURE"] = temperature.ToString()
        };

        try
        {
            SaveVariables(variables);
            _logger.Debug("Successfully updated model configuration variables");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to update model configuration");
            throw new InvalidOperationException(
                string.Format(Constants.ErrorMessages.FailedToSaveConfiguration, ex.Message), ex);
        }
    }

    /// <summary>
    ///     Clears all GitGen environment variables and removes them from shell profiles.
    /// </summary>
    public void ClearConfiguration()
    {
        _logger.Debug("Clearing all GitGen environment variables");

        try
        {
            // Clear from current process first
            foreach (var varName in Constants.EnvironmentVariables.AllVariableNames)
                Environment.SetEnvironmentVariable($"{Constants.EnvironmentVariables.Prefix}{varName}", null);

            // Clear from persistent storage based on platform
            if (PlatformHelper.IsWindows())
                ClearWindowsVariables();
            else
                UpdateShellProfile(null);

            _logger.Debug("Successfully cleared all GitGen configuration");
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to clear configuration");
            throw new InvalidOperationException($"Failed to clear configuration: {ex.Message}", ex);
        }
    }

    /// <summary>
    ///     Builds the environment variable dictionary from configuration.
    /// </summary>
    /// <param name="config">The configuration to convert</param>
    /// <returns>Dictionary of variable names (without prefix) to values</returns>
    private Dictionary<string, string> BuildConfigurationVariables(GitGenConfiguration config)
    {
        return new Dictionary<string, string>
        {
            ["PROVIDERTYPE"] = config.ProviderType ?? "",
            ["BASEURL"] = config.BaseUrl ?? "",
            ["MODEL"] = config.Model ?? "",
            ["APIKEY"] = config.ApiKey ?? "",
            ["REQUIRESAUTH"] = config.RequiresAuth.ToString().ToLower(),
            ["OPENAI_USE_LEGACY_MAX_TOKENS"] = config.OpenAiUseLegacyMaxTokens.ToString().ToLower(),
            ["TEMPERATURE"] = config.Temperature.ToString(),
            ["MAX_OUTPUT_TOKENS"] = config.MaxOutputTokens.ToString()
        };
    }

    /// <summary>
    ///     Validates that all variable values are safe for environment variable storage.
    /// </summary>
    /// <param name="variables">The variables to validate</param>
    /// <exception cref="ArgumentException">Thrown when validation fails</exception>
    private void ValidateVariables(Dictionary<string, string> variables)
    {
        foreach (var kvp in variables)
        {
            if (!ValidationService.General.IsEnvironmentVariableSafe(kvp.Value))
                throw new ArgumentException($"Variable {kvp.Key} contains unsafe characters: {kvp.Value}");

            if (!ValidationService.General.IsShellSafe(kvp.Value))
                throw new ArgumentException($"Variable {kvp.Key} contains shell-unsafe characters: {kvp.Value}");
        }
    }

    /// <summary>
    ///     Saves variables to both current process and persistent storage.
    /// </summary>
    /// <param name="variables">The variables to save (without GITGEN_ prefix)</param>
    private void SaveVariables(Dictionary<string, string> variables)
    {
        // Set for current process first
        foreach (var kvp in variables)
            Environment.SetEnvironmentVariable($"{Constants.EnvironmentVariables.Prefix}{kvp.Key}", kvp.Value);

        // Persist based on platform
        if (PlatformHelper.IsWindows())
            SaveWindowsVariables(variables);
        else
            UpdateShellProfile(variables);
    }

    /// <summary>
    ///     Saves variables to Windows user-level environment variables.
    /// </summary>
    /// <param name="variables">The variables to save</param>
    /// <exception cref="InvalidOperationException">Thrown when Windows operations fail</exception>
    private void SaveWindowsVariables(Dictionary<string, string> variables)
    {
        var errors = new List<string>();

        foreach (var kvp in variables)
            try
            {
                Environment.SetEnvironmentVariable(
                    $"{Constants.EnvironmentVariables.Prefix}{kvp.Key}",
                    kvp.Value,
                    EnvironmentVariableTarget.User);

                _logger.Debug("Set Windows environment variable: {Name}", kvp.Key);
            }
            catch (Exception ex)
            {
                var error = $"Failed to save Windows environment variable {kvp.Key}: {ex.Message}";
                _logger.Error(ex, error);
                errors.Add(error);
            }

        if (errors.Count > 0)
            throw new InvalidOperationException($"Failed to save configuration: {string.Join("; ", errors)}");
    }

    /// <summary>
    ///     Clears all GitGen variables from Windows user environment.
    /// </summary>
    private void ClearWindowsVariables()
    {
        foreach (var varName in Constants.EnvironmentVariables.AllVariableNames)
            try
            {
                Environment.SetEnvironmentVariable(
                    $"{Constants.EnvironmentVariables.Prefix}{varName}",
                    null,
                    EnvironmentVariableTarget.User);

                _logger.Debug("Cleared Windows environment variable: {Name}", varName);
            }
            catch (Exception ex)
            {
                _logger.Warning(
                    string.Format(Constants.ErrorMessages.FailedToClearVariable, varName, ex.Message));
            }
    }

    /// <summary>
    ///     **[NEW IMPLEMENTATION]**
    ///     Updates the shell profile using a robust line-filtering strategy. This method removes all existing GitGen lines,
    ///     regardless of location, and appends a single, clean configuration block to the end of the file.
    /// </summary>
    /// <param name="variables">A dictionary of variables to save. If null, the method will only clear existing configuration.</param>
    /// <exception cref="InvalidOperationException">Thrown when shell profile operations fail.</exception>
    private void UpdateShellProfile(Dictionary<string, string>? variables)
    {
        var profilePath = GetShellProfilePath();
        if (string.IsNullOrEmpty(profilePath))
        {
            HandleShellProfileNotFound(variables);
            return;
        }

        try
        {
            var originalLines = File.Exists(profilePath) ? File.ReadAllLines(profilePath) : Array.Empty<string>();

            // 1. FILTER: Create a new list, excluding any line related to GitGen's configuration. This is the "clear" step.
            var cleanedLines = originalLines
                .Where(line =>
                    !line.Trim().StartsWith(Constants.Platform.ExportStartPattern) &&
                    !line.Trim().StartsWith(Constants.UI.GitGenConfigSection)
                )
                .ToList();

            // 2. APPEND: If new variables are provided, add them as a fresh block at the end.
            if (variables != null && variables.Any())
            {
                // Ensure there's a blank line before our block for readability, if the file isn't empty.
                if (cleanedLines.Count > 0 && !string.IsNullOrWhiteSpace(cleanedLines.Last())) cleanedLines.Add("");

                // Add the header for readability. It will be removed by the filter next time.
                cleanedLines.Add(Constants.UI.GitGenConfigSection);

                // Add all the export commands.
                foreach (var kvp in variables)
                    cleanedLines.Add(string.Format(Constants.Platform.ExportFormat,
                        $"{Constants.EnvironmentVariables.Prefix}{kvp.Key}", kvp.Value));

                _logger.Debug("Appended a clean GitGen configuration block to shell profile.");
            }
            else
            {
                _logger.Debug("All GitGen lines have been cleared from the shell profile.");
            }

            // 3. COMMIT: Atomically write the newly constructed content back to the profile file.
            var tempPath = profilePath + ".gitgen.tmp";
            File.WriteAllLines(tempPath, cleanedLines);
            File.Move(tempPath, profilePath, true);

            _logger.Debug("Successfully updated shell profile: {Path}", profilePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex,
                string.Format(Constants.ErrorMessages.FailedToUpdateShellProfile, profilePath, ex.Message));
            throw new InvalidOperationException(
                string.Format(Constants.ErrorMessages.FailedToSaveConfiguration, ex.Message), ex);
        }
    }

    /// <summary>
    ///     Handles the case where shell profile cannot be determined.
    /// </summary>
    /// <param name="variables">Variables that need to be set manually</param>
    private void HandleShellProfileNotFound(Dictionary<string, string>? variables)
    {
        _logger.Error(Constants.ErrorMessages.ShellProfileNotFound);

        if (variables != null && variables.Count > 0)
        {
            _logger.Information("Please set the following environment variables manually:");
            foreach (var kvp in variables)
                _logger.Information($"export {Constants.EnvironmentVariables.Prefix}{kvp.Key}=\"{kvp.Value}\"");
        }
    }


    /// <summary>
    ///     Determines the appropriate shell profile path for the current system.
    /// </summary>
    /// <returns>Path to shell profile, or null if cannot be determined</returns>
    private string? GetShellProfilePath()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var shell = Environment.GetEnvironmentVariable("SHELL");

        // Priority order: shell-specific profiles, then generic profile
        var profiles = new[]
        {
            (shell?.Contains(Constants.Platform.ZshShell) == true,
                Path.Combine(home, Constants.Platform.ZshProfile)),
            (shell?.Contains(Constants.Platform.BashShell) == true,
                Path.Combine(home, Constants.Platform.BashProfile)),
            (true, Path.Combine(home, Constants.Platform.GenericProfile))
        };

        var profilePath = profiles.FirstOrDefault(p => p.Item1).Item2;
        _logger.Debug("Detected shell profile path: {Path}", profilePath);

        return profilePath;
    }
}