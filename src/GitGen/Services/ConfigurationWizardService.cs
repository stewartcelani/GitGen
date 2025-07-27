using GitGen.Configuration;
using GitGen.Providers;
using System.Globalization;

namespace GitGen.Services;

/// <summary>
///     Interactive configuration wizard service for setting up and managing GitGen configuration.
///     Provides guided setup, testing, and modification of provider settings and parameters.
/// </summary>
public class ConfigurationWizardService
{
    private readonly ConfigurationService _configurationService;
    private readonly IConsoleLogger _logger;
    private readonly IEnvironmentPersistenceService _persistenceService;
    private readonly ProviderFactory _providerFactory;
    private readonly ISecureConfigurationService? _secureConfigService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigurationWizardService" /> class.
    /// </summary>
    /// <param name="logger">The console logger for user interaction and debugging.</param>
    /// <param name="providerFactory">The factory for creating AI provider instances.</param>
    /// <param name="persistenceService">The service for persisting configuration to environment variables.</param>
    /// <param name="configurationService">The service for loading and validating configuration.</param>
    public ConfigurationWizardService(
        IConsoleLogger logger,
        ProviderFactory providerFactory,
        IEnvironmentPersistenceService persistenceService,
        ConfigurationService configurationService,
        ISecureConfigurationService? secureConfigService = null)
    {
        _logger = logger;
        _providerFactory = providerFactory;
        _persistenceService = persistenceService;
        _configurationService = configurationService;
        _secureConfigService = secureConfigService;
    }

    /// <summary>
    ///     Runs the interactive configuration wizard to set up or modify GitGen configuration.
    ///     Guides the user through provider selection, configuration, testing, and persistence.
    /// </summary>
    /// <returns>The configured <see cref="GitGenConfiguration" /> if successful; otherwise, null if cancelled or failed.</returns>
    public async Task<GitGenConfiguration?> RunWizardAsync()
    {
        // Use the new multi-model wizard if secure config is available
        if (_secureConfigService != null)
        {
            var model = await RunMultiModelWizardAsync();
            if (model != null)
            {
                // Convert to GitGenConfiguration for backward compatibility
                return new GitGenConfiguration
                {
                    ProviderType = model.ProviderType,
                    BaseUrl = model.Url,
                    Model = model.ModelId,
                    ApiKey = model.ApiKey,
                    RequiresAuth = model.RequiresAuth,
                    OpenAiUseLegacyMaxTokens = model.UseLegacyMaxTokens,
                    Temperature = model.Temperature,
                    MaxOutputTokens = model.MaxOutputTokens,
                    SystemPrompt = model.SystemPrompt
                };
            }
            return null;
        }

        // Fall back to legacy wizard for environment variables
        _logger.Information($"{Constants.UI.InfoSymbol} {Constants.Messages.WelcomeToWizard}");
        _logger.Information(Constants.Messages.WizardGuidance);

        // Load existing configuration to use as defaults
        var existingConfig = _configurationService.LoadConfiguration();
        var hasExistingConfig = existingConfig.IsValid;

        if (hasExistingConfig)
        {
            DisplayCurrentConfiguration(existingConfig);

            // Ask if user wants to modify existing or start fresh
            var modifyChoice = Prompt("Do you want to modify existing configuration (m) or start fresh (f)?", "m");
            if (modifyChoice.ToLower() == "f")
            {
                existingConfig = new GitGenConfiguration();
                hasExistingConfig = false;
            }
        }

        var config = hasExistingConfig ? CloneConfiguration(existingConfig) : new GitGenConfiguration();

        // Step 1: Select broad provider type (with existing value as default)
        if (!SelectProviderType(config, existingConfig)) return null;

        // Step 2: Select specific configuration based on provider type
        if (!SelectProviderConfiguration(config, existingConfig)) return null;

        // Step 3: Configure max output tokens
        ConfigureMaxOutputTokens(config, existingConfig);

        // Step 4: Test the configuration and detect API parameters
        _logger.Information("");
        _logger.Information("ℹ️ Testing your configuration and detecting optimal API parameters...");
        try
        {
            var provider = _providerFactory.CreateProvider(config);
            var (success, useLegacyTokens, detectedTemperature) =
                await provider.TestConnectionAndDetectParametersAsync();

            if (!success)
            {
                _logger.Error("❌ Configuration test failed with an unknown error.");
                return null;
            }

            // Store the detected choices in the config object
            config.OpenAiUseLegacyMaxTokens = useLegacyTokens;
            config.Temperature = detectedTemperature;

            _logger.Information(
                $"ℹ️ Detected API parameter style: {(useLegacyTokens ? "Legacy (max_tokens)" : "Modern (max_completion_tokens)")}");
            _logger.Information($"ℹ️ Model temperature: {detectedTemperature}");

            // Now, perform a full test to show the user the response
            _logger.Information("");
            _logger.Information($"{Constants.UI.TestTubeSymbol} {Constants.Messages.TestingConnection}");

            _logger.Information(
                $"{Constants.UI.LinkSymbol} Using {provider.ProviderName} provider via {config.BaseUrl} ({config.Model ?? Constants.Fallbacks.UnknownModelName})");

            var testResult = await provider.GenerateAsync(Constants.Api.TestLlmPrompt);
            var cleanedMessage = MessageCleaningService.CleanForDisplay(testResult.Message);

            _logger.Information("");
            _logger.Success($"{Constants.UI.CheckMark} LLM Response:");

            _logger.Highlight($"{Constants.UI.CommitMessageQuotes}{cleanedMessage}{Constants.UI.CommitMessageQuotes}",
                ConsoleColor.DarkCyan);
            _logger.Information("");

            if (testResult.InputTokens.HasValue && testResult.OutputTokens.HasValue)
                _logger.Muted(
                    $"{testResult.InputTokens:N0} → {testResult.OutputTokens:N0} tokens ({testResult.TotalTokens:N0} total)");
            else
                _logger.Muted($"{cleanedMessage.Length} characters");
            _logger.Information("");
            _logger.Success($"{Constants.UI.PartySymbol} Configuration test successful!");
        }
        catch (Exception ex)
        {
            _logger.Error("❌ Configuration test failed: {Message}", ex.Message);
            _logger.Error(ex, "Failed to connect to provider during wizard setup.");
            return null;
        }

        // Step 5: Show configuration summary before saving
        DisplayConfigurationSummary(config, existingConfig);

        var confirmSave = Prompt("Save this configuration?", "y");
        if (confirmSave.ToLower() != "y")
        {
            _logger.Warning("Configuration not saved. Exiting wizard.");
            return null;
        }

        // Step 6: Persist the configuration
        _logger.Information("");
        _logger.Information($"{Constants.UI.InfoSymbol} Saving configuration as user-level environment variables...");
        _persistenceService.SaveConfiguration(config);
        _logger.Information("");
        _logger.Success($"{Constants.UI.CheckMark} {Constants.Messages.ConfigurationSaved}");
        _logger.Warning($"{Constants.UI.WarningSymbol} {Constants.Messages.RestartTerminalWarning}");

        return config;
    }

    private bool SelectProviderType(GitGenConfiguration config, GitGenConfiguration existingConfig)
    {
        _logger.Information("");
        _logger.Information("Step 1: Select your provider's API compatibility type.");
        _logger.Information("  1. OpenAI Compatible (e.g., OpenAI, Azure, Groq, Ollama)");
        // Add future providers like Anthropic here

        // Determine default choice based on existing config
        var defaultChoice = "1";
        if (existingConfig.ProviderType == Constants.Configuration.ProviderTypeOpenAI)
            defaultChoice = "1";

        var choice = Prompt("Enter your choice:", defaultChoice);

        switch (choice)
        {
            case "1":
                config.ProviderType = Constants.Configuration.ProviderTypeOpenAI;
                return true;
            default:
                _logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.InvalidChoice}");
                return false;
        }
    }

    private bool SelectProviderConfiguration(GitGenConfiguration config, GitGenConfiguration existingConfig)
    {
        _logger.Information("");
        _logger.Information("Step 2: Select your specific provider preset.");
        _logger.Information("  1. OpenAI (Official Platform)");
        _logger.Information(
            "  2. Custom Provider (API Key required, e.g., Azure, Anthropic, Google, OpenRouter, Groq)");
        _logger.Information("  3. Custom Provider (No API Key required, e.g., Ollama, LM Studio)");

        // Determine default choice based on existing config
        var defaultChoice = "1";
        if (!string.IsNullOrEmpty(existingConfig.BaseUrl))
        {
            if (existingConfig.BaseUrl == Constants.Configuration.DefaultOpenAIBaseUrl)
                defaultChoice = "1";
            else if (existingConfig.RequiresAuth)
                defaultChoice = "2";
            else
                defaultChoice = "3";
        }

        var choice = Prompt("Enter your choice:", defaultChoice);

        switch (choice)
        {
            case "1": // OpenAI
                config.BaseUrl = Constants.Configuration.DefaultOpenAIBaseUrl;
                config.Model = Prompt("Enter your model name:",
                    existingConfig.Model ?? Constants.Configuration.DefaultOpenAIModel);
                config.ApiKey = PromptForApiKey("Enter your OpenAI API Key:", existingConfig.ApiKey);
                config.RequiresAuth = true;
                break;
            case "2": // Custom with Auth (covers Azure, etc.)
                config.BaseUrl = Prompt("Enter the provider's chat completions URL (e.g., your Azure endpoint):",
                    existingConfig.BaseUrl);
                config.Model = Prompt("Enter the model name (e.g., your Azure deployment name):", existingConfig.Model);
                config.ApiKey = PromptForApiKey("Enter the provider's API Key:", existingConfig.ApiKey);
                config.RequiresAuth = true;
                break;
            case "3": // Local/No Auth
                config.BaseUrl = Prompt("Enter your custom provider's chat completions URL:",
                    existingConfig.BaseUrl ?? Constants.Configuration.DefaultLocalBaseUrl);
                config.Model = Prompt("Enter the model name (e.g., llama3):", existingConfig.Model);
                config.RequiresAuth = false;
                config.ApiKey = Constants.Fallbacks.NotRequiredValue;
                break;
            default:
                _logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.InvalidChoice}");
                return false;
        }

        return true;
    }

    private void ConfigureMaxOutputTokens(GitGenConfiguration config, GitGenConfiguration existingConfig)
    {
        _logger.Information("");
        _logger.Information("Step 3: Configure maximum output tokens.");
        _logger.Information("This controls how many tokens the AI model can generate in responses.");

        // Use existing value as default, or smart default based on model
        var currentDefault = existingConfig.MaxOutputTokens > 0
            ? existingConfig.MaxOutputTokens
            : GetSuggestedMaxOutputTokens(config.Model);

        var modelType = GetModelTypeDescription(config.Model);

        _logger.Information($"ℹ️ Current/Suggested: {currentDefault} tokens{modelType}");
        _logger.Information("ℹ️ Range: 100-8000 tokens. Higher values needed for reasoning models to avoid cut-off.");

        while (true)
        {
            var input = Prompt("Enter max output tokens:", currentDefault.ToString());

            if (int.TryParse(input, out var maxTokens) && ValidationService.TokenCount.IsValid(maxTokens))
            {
                config.MaxOutputTokens = maxTokens;
                break;
            }

            _logger.Warning($"{Constants.UI.WarningSymbol} {Constants.ErrorMessages.InvalidTokenRange}",
                Constants.Configuration.MinOutputTokens,
                Constants.Configuration.MaxOutputTokens);
        }
    }

    private int GetSuggestedMaxOutputTokens(string? modelName)
    {
        return ValidationService.TokenCount.GetSuggestedCount(modelName);
    }

    private string GetModelTypeDescription(string? modelName)
    {
        if (string.IsNullOrEmpty(modelName)) return " (Unknown model - using safe default)";

        var model = modelName.ToLowerInvariant();

        // Known reasoning models
        if (model.StartsWith("o1") || model.StartsWith("o3") || model.StartsWith("o4") || model.StartsWith("o5") ||
            model.Contains("reasoning") || model.Contains("think"))
            return " (Reasoning model - higher limit to avoid cut-off during thinking)";

        // Known non-reasoning models
        if (model.StartsWith("gpt-4.1")) return " (Standard model - lower limit sufficient)";

        // Unknown models
        return " (Unknown model - using safe default)";
    }

    private string PromptForApiKey(string message, string? existingApiKey)
    {
        if (!string.IsNullOrEmpty(existingApiKey))
        {
            var masked = ValidationService.ApiKey.Mask(existingApiKey);
            _logger.Information($"Current API Key: {masked}");
            var keepExisting = Prompt("Keep existing API key? (y/n)", "y");

            if (keepExisting.ToLower() == "y") return existingApiKey;
        }

        return Prompt(message, secret: true);
    }

    private string Prompt(string message, string? defaultValue = null, bool secret = false)
    {
        while (true)
        {
            Console.Write(message);
            if (!string.IsNullOrEmpty(defaultValue)) Console.Write($" [{defaultValue}]");
            Console.Write(" ");

            var input = secret ? ReadPassword() : Console.ReadLine();
            if (secret) Console.WriteLine();

            if (!string.IsNullOrWhiteSpace(input)) return input;
            if (!string.IsNullOrWhiteSpace(defaultValue)) return defaultValue;

            _logger.Warning($"{Constants.UI.WarningSymbol} {Constants.ErrorMessages.ValueCannotBeEmpty}");
        }
    }

    private static string ReadPassword()
    {
        var pass = string.Empty;
        ConsoleKey key;
        do
        {
            var keyInfo = Console.ReadKey(true);
            key = keyInfo.Key;
            if (key == ConsoleKey.Backspace && pass.Length > 0)
            {
                Console.Write("\b \b");
                pass = pass[..^1];
            }
            else if (!char.IsControl(keyInfo.KeyChar))
            {
                Console.Write("*");
                pass += keyInfo.KeyChar;
            }
        } while (key != ConsoleKey.Enter);

        return pass;
    }


    // Quick settings methods for changing individual values
    /// <summary>
    ///     Provides a quick way to change only the maximum output tokens setting without running the full wizard.
    /// </summary>
    /// <returns>True if the setting was successfully updated; otherwise, false.</returns>
    public async Task<bool> QuickChangeMaxTokens()
    {
        if (_secureConfigService != null)
        {
            // Use new multi-model approach
            var settings = await _secureConfigService.LoadSettingsAsync();
            if (settings.Models.Count == 0)
            {
                _logger.Error("No models configured. Please run 'gitgen configure' first.");
                return false;
            }

            ModelConfiguration model;
            if (settings.Models.Count == 1)
            {
                model = settings.Models[0];
            }
            else
            {
                // Let user select which model to update
                _logger.Information("Select model to update:");
                for (int i = 0; i < settings.Models.Count; i++)
                {
                    var m = settings.Models[i];
                    var defaultMarker = m.Id == settings.DefaultModelId ? " ⭐" : "";
                    _logger.Information($"  {i + 1}. {m.Name}{defaultMarker}");
                }

                while (true)
                {
                    var choice = Prompt($"Enter choice (1-{settings.Models.Count}):", "1");
                    if (int.TryParse(choice, out var idx) && idx > 0 && idx <= settings.Models.Count)
                    {
                        model = settings.Models[idx - 1];
                        break;
                    }
                    _logger.Warning("Invalid choice. Please try again.");
                }
            }

            _logger.Information($"Current max output tokens for '{model.Name}': {model.MaxOutputTokens}");

            while (true)
            {
                var input = Prompt("Enter new max output tokens:", model.MaxOutputTokens.ToString());

                if (int.TryParse(input, out var maxTokens) && ValidationService.TokenCount.IsValid(maxTokens))
                {
                    model.MaxOutputTokens = maxTokens;
                    await _secureConfigService.UpdateModelAsync(model);
                    _logger.Success($"✅ Max output tokens for '{model.Name}' updated to {maxTokens}");
                    return true;
                }

                _logger.Warning($"{Constants.UI.WarningSymbol} {Constants.ErrorMessages.InvalidTokenRange}",
                    Constants.Configuration.MinOutputTokens,
                    Constants.Configuration.MaxOutputTokens);
            }
        }

        // Fall back to legacy approach
        var config = _configurationService.LoadConfiguration();
        if (!config.IsValid)
        {
            _logger.Error("No valid configuration found. Please run 'gitgen configure' first.");
            return false;
        }

        _logger.Information($"Current max output tokens: {config.MaxOutputTokens}");

        while (true)
        {
            var input = Prompt("Enter new max output tokens:", config.MaxOutputTokens.ToString());

            if (int.TryParse(input, out var maxTokens) && ValidationService.TokenCount.IsValid(maxTokens))
            {
                config.MaxOutputTokens = maxTokens;
                _persistenceService.SaveConfiguration(config);
                _logger.Success($"✅ Max output tokens updated to {maxTokens}");
                _logger.Warning($"{Constants.UI.WarningSymbol} {Constants.Messages.RestartTerminalWarning}");
                return true;
            }

            _logger.Warning($"{Constants.UI.WarningSymbol} {Constants.ErrorMessages.InvalidTokenRange}",
                Constants.Configuration.MinOutputTokens,
                Constants.Configuration.MaxOutputTokens);
        }
    }

    /// <summary>
    ///     Provides a quick way to change only the AI model without running the full wizard.
    ///     Tests the new model configuration before applying changes.
    /// </summary>
    /// <returns>True if the model was successfully changed; otherwise, false.</returns>
    public async Task<bool> QuickChangeModel()
    {
        if (_secureConfigService != null)
        {
            // Use new multi-model approach
            var settings = await _secureConfigService.LoadSettingsAsync();
            if (settings.Models.Count == 0)
            {
                _logger.Error("No models configured. Please run 'gitgen configure' first.");
                return false;
            }

            ModelConfiguration model;
            if (settings.Models.Count == 1)
            {
                model = settings.Models[0];
            }
            else
            {
                // Let user select which model to update
                _logger.Information("Select model to update:");
                for (int i = 0; i < settings.Models.Count; i++)
                {
                    var m = settings.Models[i];
                    var defaultMarker = m.Id == settings.DefaultModelId ? " ⭐" : "";
                    _logger.Information($"  {i + 1}. {m.Name}{defaultMarker}");
                }

                while (true)
                {
                    var choice = Prompt($"Enter choice (1-{settings.Models.Count}):", "1");
                    if (int.TryParse(choice, out var idx) && idx > 0 && idx <= settings.Models.Count)
                    {
                        model = settings.Models[idx - 1];
                        break;
                    }
                    _logger.Warning("Invalid choice. Please try again.");
                }
            }

            _logger.Information($"Current model ID for '{model.Name}': {model.ModelId}");
            var newModelId = Prompt("Enter new model ID:", model.ModelId);

            if (newModelId == model.ModelId)
            {
                _logger.Information("Model ID unchanged.");
                return true;
            }

            // Test the new model
            var oldModelId = model.ModelId;
            model.ModelId = newModelId;
            _logger.Information("Testing new model configuration...");

            try
            {
                // Create temporary config for testing
                var testConfig = new GitGenConfiguration
                {
                    ProviderType = model.ProviderType,
                    BaseUrl = model.Url,
                    Model = model.ModelId,
                    ApiKey = model.ApiKey,
                    RequiresAuth = model.RequiresAuth,
                    MaxOutputTokens = model.MaxOutputTokens,
                    Temperature = model.Temperature
                };

                var provider = _providerFactory.CreateProvider(testConfig, model);
                var (success, useLegacyTokens, detectedTemperature) =
                    await provider.TestConnectionAndDetectParametersAsync();

                if (!success)
                {
                    _logger.Error($"Failed to connect with model '{newModelId}'");
                    model.ModelId = oldModelId; // Restore old model ID
                    return false;
                }

                model.UseLegacyMaxTokens = useLegacyTokens;
                model.Temperature = detectedTemperature;

                await _secureConfigService.UpdateModelAsync(model);
                _logger.Success($"✅ Model ID for '{model.Name}' updated to {newModelId}");
                return true;
            }
            catch (Exception ex)
            {
                _logger.Error($"Failed to test new model: {ex.Message}");
                model.ModelId = oldModelId; // Restore old model ID
                return false;
            }
        }

        // Fall back to legacy approach
        var config = _configurationService.LoadConfiguration();
        if (!config.IsValid)
        {
            _logger.Error("No valid configuration found. Please run 'gitgen configure' first.");
            return false;
        }

        _logger.Information($"Current model: {config.Model}");
        var newModel = Prompt("Enter new model name:", config.Model);

        if (newModel == config.Model)
        {
            _logger.Information("Model unchanged.");
            return true;
        }

        // Test the new model
        config.Model = newModel;
        _logger.Information("Testing new model configuration...");

        try
        {
            var provider = _providerFactory.CreateProvider(config);
            var (success, useLegacyTokens, detectedTemperature) =
                await provider.TestConnectionAndDetectParametersAsync();

            if (!success)
            {
                _logger.Error($"Failed to connect with model '{newModel}'");
                return false;
            }

            config.OpenAiUseLegacyMaxTokens = useLegacyTokens;
            config.Temperature = detectedTemperature;

            _persistenceService.SaveConfiguration(config);
            _logger.Success($"✅ Model updated to {newModel}");
            _logger.Warning($"{Constants.UI.WarningSymbol} {Constants.Messages.RestartTerminalWarning}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error($"Failed to test new model: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    ///     Changes the AI model to the specified model name while preserving other configuration settings.
    ///     Performs automatic parameter detection and testing before applying changes.
    /// </summary>
    /// <param name="newModelName">The name of the new model to switch to.</param>
    /// <returns>True if the model was successfully changed; otherwise, false.</returns>
    public async Task<bool> ChangeModelAsync(string newModelName)
    {
        var currentConfig = _configurationService.LoadConfiguration();
        if (!currentConfig.IsValid)
        {
            _logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.ConfigurationInvalid}");
            return false;
        }

        try
        {
            _logger.Information($"🔄 Changing model from '{currentConfig.Model}' to '{newModelName}'...");
            _logger.Information(
                $"ℹ️ Keeping provider: {currentConfig.ProviderType}, Base URL: {currentConfig.BaseUrl}");
            Console.WriteLine();

            // Create a new configuration with the new model but same provider settings
            var newConfig = new GitGenConfiguration
            {
                ProviderType = currentConfig.ProviderType,
                BaseUrl = currentConfig.BaseUrl,
                ApiKey = currentConfig.ApiKey,
                RequiresAuth = currentConfig.RequiresAuth,
                Model = newModelName,
                // These will be rediscovered during testing
                OpenAiUseLegacyMaxTokens = currentConfig.OpenAiUseLegacyMaxTokens,
                Temperature = currentConfig.Temperature
            };

            // Test the new configuration and detect parameters
            _logger.Information("🧪 Testing new model configuration and detecting optimal parameters...");
            var provider = _providerFactory.CreateProvider(newConfig);

            var (success, useLegacyTokens, detectedTemperature) =
                await provider.TestConnectionAndDetectParametersAsync();

            if (!success)
            {
                _logger.Error($"❌ Failed to connect to model '{newModelName}'. Model change cancelled.");
                return false;
            }

            // Update the configuration with detected parameters
            newConfig.OpenAiUseLegacyMaxTokens = useLegacyTokens;
            newConfig.Temperature = detectedTemperature;

            _logger.Success("✅ Model test successful!");
            _logger.Information(
                $"ℹ️ Detected API parameter style: {(useLegacyTokens ? "Legacy (max_tokens)" : "Modern (max_completion_tokens)")}");
            _logger.Information($"ℹ️ Model temperature: {detectedTemperature}");
            Console.WriteLine();

            // Persist only the model-related changes
            _logger.Information("💾 Saving model configuration changes...");
            _persistenceService.UpdateModelConfiguration(newModelName, useLegacyTokens, detectedTemperature);

            _logger.Success("✅ Model configuration updated successfully!");
            _logger.Information($"🎯 Now using model: {newModelName}");
            _logger.Warning("⚠️ You may need to restart your terminal for the changes to take effect.");

            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to change model configuration");
            _logger.Error($"❌ Model change failed: {ex.Message}");
            return false;
        }
    }

    private void DisplayCurrentConfiguration(GitGenConfiguration config)
    {
        _logger.Information("");
        _logger.Information("📋 Current Configuration:");
        _logger.Information($"   Provider: {config.ProviderType}");
        _logger.Information($"   Base URL: {config.BaseUrl}");
        _logger.Information($"   Model: {config.Model}");
        _logger.Information($"   API Key: {ValidationService.ApiKey.Mask(config.ApiKey)}");
        _logger.Information($"   Max Tokens: {config.MaxOutputTokens}");
        _logger.Information("");
    }

    private void DisplayConfigurationSummary(GitGenConfiguration newConfig, GitGenConfiguration oldConfig)
    {
        _logger.Information("");
        _logger.Information("📋 Configuration Summary:");

        DisplayConfigChange("Provider", oldConfig.ProviderType, newConfig.ProviderType);
        DisplayConfigChange("Base URL", oldConfig.BaseUrl, newConfig.BaseUrl);
        DisplayConfigChange("Model", oldConfig.Model, newConfig.Model);
        DisplayConfigChange("API Key",
            ValidationService.ApiKey.Mask(oldConfig.ApiKey),
            ValidationService.ApiKey.Mask(newConfig.ApiKey));
        DisplayConfigChange("Max Tokens",
            oldConfig.MaxOutputTokens.ToString(),
            newConfig.MaxOutputTokens.ToString());

        _logger.Information("");
    }

    private void DisplayConfigChange(string field, string? oldValue, string? newValue)
    {
        if (oldValue != newValue)
            _logger.Information($"   {field}: {oldValue ?? "(not set)"} → {newValue ?? "(not set)"} ✨");
        else
            _logger.Information($"   {field}: {newValue ?? "(not set)"}");
    }

    private GitGenConfiguration CloneConfiguration(GitGenConfiguration source)
    {
        return new GitGenConfiguration
        {
            ProviderType = source.ProviderType,
            BaseUrl = source.BaseUrl,
            Model = source.Model,
            ApiKey = source.ApiKey,
            RequiresAuth = source.RequiresAuth,
            OpenAiUseLegacyMaxTokens = source.OpenAiUseLegacyMaxTokens,
            Temperature = source.Temperature,
            MaxOutputTokens = source.MaxOutputTokens
        };
    }

    /// <summary>
    ///     Resets all GitGen configuration by clearing environment variables and shell profile entries.
    /// </summary>
    public async Task ResetConfiguration()
    {
        _logger.Information($"{Constants.UI.InfoSymbol} {Constants.Messages.ResettingConfiguration}");
        
        // Clear secure storage if available
        if (_secureConfigService != null)
        {
            var settings = await _secureConfigService.LoadSettingsAsync();
            settings.Models.Clear();
            settings.DefaultModelId = null;
            await _secureConfigService.SaveSettingsAsync(settings);
            _logger.Success($"{Constants.UI.CheckMark} Secure storage cleared.");
        }
        
        // Clear environment variables
        _persistenceService.ClearConfiguration();
        _logger.Success($"{Constants.UI.CheckMark} {Constants.Messages.ConfigurationReset}");
        _logger.Warning($"{Constants.UI.WarningSymbol} {Constants.Messages.RestartTerminalWarning}");
    }

    /// <summary>
    ///     Runs the new multi-model configuration wizard.
    /// </summary>
    private async Task<ModelConfiguration?> RunMultiModelWizardAsync()
    {
        _logger.Information($"{Constants.UI.InfoSymbol} Welcome to the GitGen Multi-Model Configuration Wizard");
        _logger.Information("This will guide you through setting up a new AI model configuration.");

        // Check for migration from environment variables
        var migrated = await _secureConfigService!.MigrateFromEnvironmentVariablesAsync();
        if (migrated)
        {
            _logger.Success("✅ Existing configuration migrated to secure storage!");
            _logger.Information("");
        }

        var model = new ModelConfiguration();

        // Step 1: Model Name
        if (!await ConfigureModelName(model)) return null;

        // Step 2: Select provider type
        if (!SelectProviderType(model)) return null;

        // Step 3: Select specific configuration
        if (!SelectProviderConfiguration(model)) return null;

        // Step 4: Configure max output tokens
        ConfigureMaxOutputTokens(model);

        // Step 5: Test the configuration
        if (!await TestModelConfiguration(model)) return null;

        // Step 6: Additional optional configurations
        await ConfigureAdditionalSettings(model);

        // Step 7: Show summary and save
        DisplayModelSummary(model);

        var confirmSave = Prompt("Save this model configuration?", "y");
        if (confirmSave.ToLower() != "y")
        {
            _logger.Warning("Model configuration not saved. Exiting wizard.");
            return null;
        }

        // Save the model
        await _secureConfigService!.AddModelAsync(model);
        _logger.Success($"{Constants.UI.CheckMark} Model '{model.Name}' saved successfully!");
        
        return model;
    }

    private async Task<bool> ConfigureModelName(ModelConfiguration model)
    {
        _logger.Information("");
        _logger.Information("Step 1: Choose a name for this model configuration.");
        _logger.Information("This name will help you identify and switch between different models.");
        _logger.Muted("Examples: 'gpt-4-work', 'claude-personal', 'llama-local'");

        while (true)
        {
            var name = Prompt("Enter model name:");
            if (string.IsNullOrWhiteSpace(name))
            {
                _logger.Warning("Model name cannot be empty.");
                continue;
            }

            // Check if name already exists
            var existing = await _secureConfigService!.GetModelAsync(name);
            if (existing != null)
            {
                _logger.Warning($"A model named '{name}' already exists. Please choose a different name.");
                continue;
            }

            model.Name = name;
            return true;
        }
    }

    private bool SelectProviderType(ModelConfiguration model)
    {
        _logger.Information("");
        _logger.Information("Step 2: Select your provider's API compatibility type.");
        _logger.Information("  1. OpenAI Compatible (e.g., OpenAI, Azure, Groq, Ollama)");

        var choice = Prompt("Enter your choice:", "1");

        switch (choice)
        {
            case "1":
                model.ProviderType = Constants.Configuration.ProviderTypeOpenAI;
                return true;
            default:
                _logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.InvalidChoice}");
                return false;
        }
    }

    private bool SelectProviderConfiguration(ModelConfiguration model)
    {
        _logger.Information("");
        _logger.Information("Step 3: Select your specific provider preset.");
        _logger.Information("  1. OpenAI (Official Platform)");
        _logger.Information("  2. Custom Provider (API Key required, e.g., Azure, Anthropic, Google, OpenRouter, Groq)");
        _logger.Information("  3. Custom Provider (No API Key required, e.g., Ollama, LM Studio)");

        var choice = Prompt("Enter your choice:", "1");

        switch (choice)
        {
            case "1": // OpenAI
                model.Url = Constants.Configuration.DefaultOpenAIBaseUrl;
                model.ModelId = Prompt("Enter the model ID (e.g., gpt-4-turbo):", Constants.Configuration.DefaultOpenAIModel);
                model.ApiKey = PromptForApiKey("Enter your OpenAI API Key:", null);
                model.RequiresAuth = true;
                break;
            case "2": // Custom with Auth
                model.Url = Prompt("Enter the provider's chat completions URL:");
                model.ModelId = Prompt("Enter the model ID:");
                model.ApiKey = PromptForApiKey("Enter the provider's API Key:", null);
                model.RequiresAuth = true;
                break;
            case "3": // Local/No Auth
                model.Url = Prompt("Enter your custom provider's chat completions URL:", Constants.Configuration.DefaultLocalBaseUrl);
                model.ModelId = Prompt("Enter the model ID (e.g., llama3):");
                model.RequiresAuth = false;
                model.ApiKey = Constants.Fallbacks.NotRequiredValue;
                break;
            default:
                _logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.InvalidChoice}");
                return false;
        }

        return true;
    }

    private void ConfigureMaxOutputTokens(ModelConfiguration model)
    {
        _logger.Information("");
        _logger.Information("Step 4: Configure maximum output tokens.");
        _logger.Information("This controls how many tokens the AI model can generate in responses.");

        var suggestedTokens = GetSuggestedMaxOutputTokens(model.ModelId);
        var modelType = GetModelTypeDescription(model.ModelId);

        _logger.Information($"ℹ️ Suggested: {suggestedTokens} tokens{modelType}");
        _logger.Information("ℹ️ Range: 100-8000 tokens. Higher values needed for reasoning models to avoid cut-off.");

        while (true)
        {
            var input = Prompt("Enter max output tokens:", suggestedTokens.ToString());

            if (int.TryParse(input, out var maxTokens) && ValidationService.TokenCount.IsValid(maxTokens))
            {
                model.MaxOutputTokens = maxTokens;
                break;
            }

            _logger.Warning($"{Constants.UI.WarningSymbol} {Constants.ErrorMessages.InvalidTokenRange}",
                Constants.Configuration.MinOutputTokens,
                Constants.Configuration.MaxOutputTokens);
        }
    }

    private async Task<bool> TestModelConfiguration(ModelConfiguration model)
    {
        _logger.Information("");
        _logger.Information("Testing your configuration and detecting optimal API parameters...");
        
        try
        {
            // Create a temporary GitGenConfiguration for testing
            var testConfig = new GitGenConfiguration
            {
                ProviderType = model.ProviderType,
                BaseUrl = model.Url,
                Model = model.ModelId,
                ApiKey = model.ApiKey,
                RequiresAuth = model.RequiresAuth,
                MaxOutputTokens = model.MaxOutputTokens,
                Temperature = model.Temperature
            };

            var provider = _providerFactory.CreateProvider(testConfig, model);
            var (success, useLegacyTokens, detectedTemperature) = await provider.TestConnectionAndDetectParametersAsync();

            if (!success)
            {
                _logger.Error("❌ Configuration test failed with an unknown error.");
                return false;
            }

            // Store the detected parameters
            model.UseLegacyMaxTokens = useLegacyTokens;
            model.Temperature = detectedTemperature;

            _logger.Success("✅ Parameter detection complete.");
            _logger.Information($"ℹ️ Token parameter: {(useLegacyTokens ? "Legacy (max_tokens)" : "Modern (max_completion_tokens)")}");
            _logger.Information($"ℹ️ Temperature: {detectedTemperature}");

            // Perform a full test
            _logger.Information("");
            _logger.Information($"{Constants.UI.TestTubeSymbol} Testing LLM connection...");
            _logger.Information($"{Constants.UI.LinkSymbol} Using {provider.ProviderName} provider via {model.Url} ({model.ModelId})");

            var testResult = await provider.GenerateAsync(Constants.Api.TestLlmPrompt);
            var cleanedMessage = MessageCleaningService.CleanForDisplay(testResult.Message);

            _logger.Information("");
            _logger.Success($"{Constants.UI.CheckMark} LLM Response:");
            _logger.Highlight($"{Constants.UI.CommitMessageQuotes}{cleanedMessage}{Constants.UI.CommitMessageQuotes}", ConsoleColor.DarkCyan);
            _logger.Information("");

            if (testResult.InputTokens.HasValue && testResult.OutputTokens.HasValue)
                _logger.Muted($"Generated with {testResult.InputTokens:N0} input tokens, {testResult.OutputTokens:N0} output tokens ({testResult.TotalTokens:N0} total) • {cleanedMessage.Length} characters");
            else
                _logger.Muted($"Generated with {cleanedMessage.Length} characters");

            _logger.Information("");
            _logger.Success($"{Constants.UI.PartySymbol} Configuration test successful!");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error("❌ Configuration test failed: {Message}", ex.Message);
            _logger.Error(ex, "Failed to connect to provider during wizard setup.");
            return false;
        }
    }

    private async Task ConfigureAdditionalSettings(ModelConfiguration model)
    {
        _logger.Information("");
        _logger.Information("Step 5: Configure additional settings (optional)");

        // Note/Description
        var addNote = Prompt("Add a note or description for this model? (y/n)", "n");
        if (addNote.ToLower() == "y")
        {
            model.Note = Prompt("Enter note:");
        }

        // Pricing
        var addPricing = Prompt("Configure pricing information? (y/n)", "n");
        if (addPricing.ToLower() == "y")
        {
            await ConfigurePricing(model);
        }

        // Custom System Prompt
        var addSystemPrompt = Prompt("Add a custom system prompt? (y/n)", "n");
        if (addSystemPrompt.ToLower() == "y")
        {
            await ConfigureSystemPrompt(model);
        }
    }

    private async Task ConfigurePricing(ModelConfiguration model)
    {
        _logger.Information("");
        _logger.Information("Pricing Configuration");
        _logger.Information("Enter costs per million tokens. Leave blank to skip.");

        model.Pricing = new PricingInfo();

        // Currency
        _logger.Information("");
        _logger.Information("Select currency:");
        _logger.Information("  1. USD ($)");
        _logger.Information("  2. EUR (€)");
        _logger.Information("  3. GBP (£)");
        _logger.Information("  4. AUD (A$)");
        _logger.Information("  5. Other");

        var currencyChoice = Prompt("Enter your choice:", "1");
        switch (currencyChoice)
        {
            case "1": model.Pricing.CurrencyCode = "USD"; break;
            case "2": model.Pricing.CurrencyCode = "EUR"; break;
            case "3": model.Pricing.CurrencyCode = "GBP"; break;
            case "4": model.Pricing.CurrencyCode = "AUD"; break;
            case "5":
                model.Pricing.CurrencyCode = Prompt("Enter currency code (e.g., JPY):").ToUpper();
                break;
            default: model.Pricing.CurrencyCode = "USD"; break;
        }

        // Input cost
        while (true)
        {
            var inputCostStr = Prompt("Input cost per million tokens:", "0");
            if (decimal.TryParse(inputCostStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var inputCost) && inputCost >= 0)
            {
                model.Pricing.InputPer1M = inputCost;
                break;
            }
            _logger.Warning("Please enter a valid positive number.");
        }

        // Output cost
        while (true)
        {
            var outputCostStr = Prompt("Output cost per million tokens:", "0");
            if (decimal.TryParse(outputCostStr, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var outputCost) && outputCost >= 0)
            {
                model.Pricing.OutputPer1M = outputCost;
                break;
            }
            _logger.Warning("Please enter a valid positive number.");
        }

        await Task.CompletedTask;
    }

    private async Task ConfigureSystemPrompt(ModelConfiguration model)
    {
        _logger.Information("");
        _logger.Information("Custom System Prompt");
        _logger.Information("This will be appended to GitGen's base instructions for this model.");
        _logger.Muted("Example: 'Always use conventional commit format' or 'Focus on architectural changes'");

        model.SystemPrompt = Prompt("Enter custom system prompt:");
        await Task.CompletedTask;
    }

    private void DisplayModelSummary(ModelConfiguration model)
    {
        _logger.Information("");
        _logger.Information("📋 Model Configuration Summary:");
        _logger.Information($"   Name: {model.Name}");
        _logger.Information($"   Provider: {model.ProviderType}");
        _logger.Information($"   URL: {model.Url}");
        _logger.Information($"   Model ID: {model.ModelId}");
        _logger.Information($"   API Key: {ValidationService.ApiKey.Mask(model.ApiKey)}");
        _logger.Information($"   Max Tokens: {model.MaxOutputTokens}");
        
        if (!string.IsNullOrWhiteSpace(model.Note))
            _logger.Information($"   Note: {model.Note}");
        
        if (model.Pricing != null && (model.Pricing.InputPer1M > 0 || model.Pricing.OutputPer1M > 0))
        {
            var pricingInfo = CostCalculationService.FormatPricingInfo(model.Pricing);
            _logger.Information($"   Pricing: {pricingInfo}");
        }
        
        if (!string.IsNullOrWhiteSpace(model.SystemPrompt))
            _logger.Information($"   System Prompt: {model.SystemPrompt.Substring(0, Math.Min(50, model.SystemPrompt.Length))}...");
        
        _logger.Information("");
    }
}