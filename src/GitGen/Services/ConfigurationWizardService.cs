using GitGen.Configuration;
using GitGen.Providers;
using GitGen.Exceptions;
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
    private readonly ProviderFactory _providerFactory;
    private readonly ISecureConfigurationService? _secureConfigService;
    private readonly IConsoleInput _consoleInput;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ConfigurationWizardService" /> class.
    /// </summary>
    /// <param name="logger">The console logger for user interaction and debugging.</param>
    /// <param name="providerFactory">The factory for creating AI provider instances.</param>
    /// <param name="configurationService">The service for loading and validating configuration.</param>
    public ConfigurationWizardService(
        IConsoleLogger logger,
        ProviderFactory providerFactory,
        ConfigurationService configurationService,
        IConsoleInput consoleInput,
        ISecureConfigurationService? secureConfigService = null)
    {
        _logger = logger;
        _providerFactory = providerFactory;
        _configurationService = configurationService;
        _consoleInput = consoleInput;
        _secureConfigService = secureConfigService;
    }

    /// <summary>
    ///     Runs the interactive configuration wizard to set up or modify GitGen configuration.
    ///     Guides the user through provider selection, configuration, testing, and persistence.
    /// </summary>
    /// <returns>The configured <see cref="ModelConfiguration" /> if successful; otherwise, null if cancelled or failed.</returns>
    public virtual async Task<ModelConfiguration?> RunWizardAsync()
    {
        // Always use the new multi-model wizard
        if (_secureConfigService == null)
        {
            _logger.Error("Secure configuration service not available");
            return null;
        }

        return await RunMultiModelWizardAsync();
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
            if (defaultValue != null)
            {
                var displayDefault = string.IsNullOrEmpty(defaultValue) ? "none" : defaultValue;
                Console.Write($" [{displayDefault}]");
            }
            Console.Write(" ");
            Console.Out.Flush();

            var input = secret ? _consoleInput.ReadPassword() : _consoleInput.ReadLine();
            if (secret) Console.WriteLine();

            if (!string.IsNullOrWhiteSpace(input)) return input;
            if (defaultValue != null) return defaultValue;

            _logger.Warning($"{Constants.UI.WarningSymbol} {Constants.ErrorMessages.ValueCannotBeEmpty}");
        }
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
                _logger.Error("No models configured. Please run 'gitgen config' first.");
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

        // No legacy support
        _logger.Error("Secure configuration service not available.");
        return false;
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
                _logger.Error("No models configured. Please run 'gitgen config' first.");
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
                var provider = _providerFactory.CreateProvider(model);
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

        // No legacy support
        _logger.Error("Secure configuration service not available.");
        return false;
    }



    private void DisplayConfigChange(string field, string? oldValue, string? newValue)
    {
        if (oldValue != newValue)
            _logger.Information($"   {field}: {oldValue ?? "(not set)"} → {newValue ?? "(not set)"} ✨");
        else
            _logger.Information($"   {field}: {newValue ?? "(not set)"}");
    }


    /// <summary>
    ///     Runs the new multi-model configuration wizard.
    /// </summary>
    public async Task<ModelConfiguration?> RunMultiModelWizardAsync()
    {
        _logger.Information($"{Constants.UI.PartySymbol} Welcome to the GitGen Multi-Model Configuration Wizard");
        _logger.Information("This will guide you through setting up a new AI model configuration.");

        var model = new ModelConfiguration();

        // Step 1: Model Name
        if (!await ConfigureModelName(model)) return null;

        // Step 2: Model Aliases
        if (!await ConfigureModelAliases(model)) return null;

        // Step 3: Note/Description
        await ConfigureModelNote(model);

        // Step 4: Select provider type
        if (!SelectProviderType(model)) return null;

        // Step 5: Select specific configuration
        if (!SelectProviderConfiguration(model)) return null;

        // Step 6: Configure max output tokens
        ConfigureMaxOutputTokens(model);

        // Step 7: Test the configuration
        if (!await TestModelConfiguration(model)) return null;

        // Step 8: Pricing configuration
        await ConfigurePricing(model);

        // Step 9: System prompt
        await ConfigureSystemPrompt(model);

        // Step 10: Show summary and save
        DisplayModelSummary(model);

        var confirmSave = Prompt("Save this model configuration?", "y");
        if (confirmSave.ToLower() != "y")
        {
            _logger.Warning("Model configuration not saved. Exiting wizard.");
            return null;
        }

        // Check if this will be the first (and thus default) model
        var settings = await _secureConfigService!.LoadSettingsAsync();
        bool isFirstModel = settings.Models.Count == 0;

        // Save the model
        await _secureConfigService!.AddModelAsync(model);
        _logger.Success($"{Constants.UI.CheckMark} Model '{model.Name}' saved successfully!");

        // Warn if this is the first model and appears to be for public/free use
        if (isFirstModel && AppearsToBePublicModel(model))
        {
            _logger.Information("");
            _logger.Warning($"{Constants.UI.WarningSymbol} Important: This model has been set as your default because it's your first model.");
            _logger.Warning("   Since it appears to be configured for public/free use, running 'gitgen' without");
            _logger.Warning("   specifying a model will send your code to this service.");
            _logger.Information("");
            _logger.Information("   Consider adding a secure model and setting it as default for private repositories.");
            _logger.Information("   You can change the default model anytime using 'gitgen config'.");
        }

        return model;
    }

    private async Task<bool> ConfigureModelName(ModelConfiguration model)
    {
        _logger.Information("");
        _logger.Information("");
        _logger.Information("");
        _logger.Highlight("Step 1: Choose a name for this model configuration.", ConsoleColor.Cyan);
        _logger.Information("This is a friendly name to identify this configuration, NOT the model ID the provider uses.");
        _logger.Information("You'll configure the actual model ID (e.g., 'gpt-4.1-nano', 'claude-4-sonnet') in a later step.");
        _logger.Muted("Examples: 'gpt-4-work', 'sonnet', 'kimik2', 'llama-local'");

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

    private async Task<bool> ConfigureModelAliases(ModelConfiguration model)
    {
        _logger.Information("");
        _logger.Information("");
        _logger.Information("");
        _logger.Highlight("Step 2: Configure aliases for quick access (optional).", ConsoleColor.Green);
        _logger.Information("Aliases allow you to quickly reference models with memorable shortcuts.");
        _logger.Information("");
        _logger.Information("Examples:");
        _logger.Success("  @ultrathink - For complex reasoning tasks");
        _logger.Success("  @sonnet    - For general coding tasks");
        _logger.Success("  @free      - For public repos where privacy isn't an issue");
        _logger.Muted("");
        _logger.Muted("💡 Tip: Configure a free model as @free to save money on public repositories");
        _logger.Muted("   where sending code to free APIs doesn't matter.");
        _logger.Muted("   OpenRouter often has great free models in public previews!");
        _logger.Warning("");
        _logger.Warning("⚠️  Important: Avoid setting a free model as your default to prevent accidentally");
        _logger.Warning("   sending private code to public APIs. Always use explicit model selection");
        _logger.Warning("   (e.g., 'gitgen @free') when working with public repositories.");
        _logger.Information("");

        // Suggest a dash-stripped alias if the model name contains dashes
        var suggestedAlias = model.Name.Contains('-')
            ? model.Name.Replace("-", "").ToLower()
            : string.Empty;

        var aliasInput = suggestedAlias != string.Empty
            ? Prompt($"Enter aliases (comma-separated) [{suggestedAlias}]:", suggestedAlias)
            : Prompt("Enter aliases (comma-separated) [none]:", "");

        if (string.IsNullOrWhiteSpace(aliasInput))
        {
            // If there's a suggested alias, use it; otherwise, leave aliases empty
            if (!string.IsNullOrEmpty(suggestedAlias))
            {
                model.Aliases = new List<string> { suggestedAlias };
            }
            else
            {
                model.Aliases = new List<string>();
            }
            return true;
        }

        // Parse and validate aliases
        var aliases = aliasInput.Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(a => a.Trim())
            .Where(a => !string.IsNullOrWhiteSpace(a))
            .ToList();

        var validatedAliases = new List<string>();
        var settings = await _secureConfigService!.LoadSettingsAsync();

        foreach (var alias in aliases)
        {
            // Normalize alias - remove @ if present, we'll add it back
            var normalizedAlias = alias.TrimStart('@');

            // Check if it conflicts with existing model names
            var existingModel = settings.Models.FirstOrDefault(m =>
                m.Name.Equals(normalizedAlias, StringComparison.OrdinalIgnoreCase));
            if (existingModel != null)
            {
                _logger.Warning($"Alias '@{normalizedAlias}' conflicts with existing model name '{existingModel.Name}'. Skipping.");
                continue;
            }

            // Check if alias already exists in any model
            var conflictingModel = settings.Models.FirstOrDefault(m =>
                m.Aliases != null && m.Aliases.Any(a =>
                    a.Equals(normalizedAlias, StringComparison.OrdinalIgnoreCase)));
            if (conflictingModel != null)
            {
                _logger.Warning($"Alias '@{normalizedAlias}' is already used by model '{conflictingModel.Name}'. Skipping.");
                continue;
            }

            validatedAliases.Add(normalizedAlias);
        }

        // Set the validated aliases (can be empty)
        model.Aliases = validatedAliases;

        if (validatedAliases.Count > 0)
        {
            _logger.Success($"✅ Configured aliases: {string.Join(", ", validatedAliases.Select(a => $"@{a}"))}");
        }
        else
        {
            _logger.Information("ℹ️ No aliases configured for this model.");
        }

        return true;
    }

    private async Task ConfigureModelNote(ModelConfiguration model)
    {
        _logger.Information("");
        _logger.Information("");
        _logger.Information("");
        _logger.Highlight("Step 3: Add a description for this model (optional).", ConsoleColor.Yellow);
        _logger.Muted("This helps you remember what this model is best used for.");

        var note = Prompt("Enter description [none]:", "");
        if (!string.IsNullOrWhiteSpace(note))
        {
            model.Note = note;
        }

        await Task.CompletedTask;
    }

    private bool SelectProviderType(ModelConfiguration model)
    {
        _logger.Information("");
        _logger.Information("");
        _logger.Information("");
        _logger.Highlight("Step 4: Select your provider's API compatibility type.", ConsoleColor.Magenta);
        _logger.Information("  1. OpenAI Compatible (e.g., OpenAI, Azure, Groq, Ollama)");

        var choice = Prompt("Enter your choice:", "1");

        switch (choice)
        {
            case "1":
                model.Type = Constants.Configuration.ProviderTypeOpenAICompatible;
                return true;
            default:
                _logger.Error($"{Constants.UI.CrossMark} {Constants.ErrorMessages.InvalidChoice}");
                return false;
        }
    }

    private bool SelectProviderConfiguration(ModelConfiguration model)
    {
        _logger.Information("");
        _logger.Information("");
        _logger.Information("");
        _logger.Highlight("Step 5: Select your specific provider preset.", ConsoleColor.Cyan);
        _logger.Information("  1. OpenAI (Official Platform)");
        _logger.Information("  2. Custom Provider (API Key required, e.g., Azure, Anthropic, Google, OpenRouter, Groq)");
        _logger.Information("  3. Custom Provider (No API Key required, e.g., Ollama, LM Studio)");

        var choice = Prompt("Enter your choice:", "1");

        switch (choice)
        {
            case "1": // OpenAI
                model.Url = Constants.Configuration.DefaultOpenAIBaseUrl;

                // Check if URL matches a known provider
                var openAIProvider = ValidationService.DomainExtractor.GetProviderNameFromUrl(model.Url);
                if (!string.IsNullOrEmpty(openAIProvider))
                {
                    model.Provider = openAIProvider;
                    _logger.Success($"✅ Detected provider: {openAIProvider}");
                }
                else
                {
                    // Fallback to domain extraction if auto-detection fails
                    var openaiDomain = ValidationService.DomainExtractor.ExtractDomain(model.Url) ?? "openai.com";
                    model.Provider = Prompt($"Provider name [{openaiDomain}]:", openaiDomain);
                }
                model.ModelId = Prompt("Enter the model ID used by the provider's API (e.g., gpt-4-turbo):", Constants.Configuration.DefaultOpenAIModel);
                model.ApiKey = PromptForApiKey("Enter your OpenAI API Key:", null);
                model.RequiresAuth = true;
                break;
            case "2": // Custom with Auth
                model.Url = Prompt("Enter the provider's chat completions URL:");

                // Check if URL matches a known provider
                var knownProvider = ValidationService.DomainExtractor.GetProviderNameFromUrl(model.Url);

                if (!string.IsNullOrEmpty(knownProvider))
                {
                    model.Provider = knownProvider;
                    _logger.Success($"✅ Detected provider: {knownProvider}");
                }
                else
                {
                    // Extract and suggest domain as provider name for unknown providers
                    var customDomain = ValidationService.DomainExtractor.ExtractDomain(model.Url) ?? "custom";
                    model.Provider = Prompt($"Provider name [{customDomain}]:", customDomain);
                }

                model.ModelId = Prompt("Enter the model ID used by the provider's API:");
                model.ApiKey = PromptForApiKey("Enter the provider's API Key:", null);
                model.RequiresAuth = true;
                break;
            case "3": // Local/No Auth
                model.Url = Prompt("Enter your custom provider's chat completions URL:", Constants.Configuration.DefaultLocalBaseUrl);

                // Check if URL matches a known provider
                var knownLocalProvider = ValidationService.DomainExtractor.GetProviderNameFromUrl(model.Url);
                if (!string.IsNullOrEmpty(knownLocalProvider))
                {
                    model.Provider = knownLocalProvider;
                    _logger.Success($"✅ Detected provider: {knownLocalProvider}");
                }
                else
                {
                    // Extract and suggest domain as provider name for unknown providers
                    var localDomain = ValidationService.DomainExtractor.ExtractDomain(model.Url) ?? "localhost";
                    model.Provider = Prompt($"Provider name [{localDomain}]:", localDomain);
                }

                model.ModelId = Prompt("Enter the model ID used by the provider's API (e.g., llama3):");
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
        _logger.Information("");
        _logger.Information("");
        _logger.Highlight("Step 6: Configure maximum output tokens.", ConsoleColor.Green);
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
        _logger.Information("");
        _logger.Information("");
        _logger.Highlight("Step 7: Test the configuration.", ConsoleColor.Yellow);
        _logger.Information("Testing your configuration and detecting optimal API parameters...");

        const int maxAttempts = 3;
        int attempt = 0;

        while (attempt < maxAttempts)
        {
            attempt++;

            try
            {
                var provider = _providerFactory.CreateProvider(model);
                var (success, useLegacyTokens, detectedTemperature) = await provider.TestConnectionAndDetectParametersAsync();

                if (!success)
                {
                    // Error details have already been shown by parameter detector

                    if (attempt < maxAttempts)
                    {
                        var retry = Prompt($"Would you like to retry? (attempt {attempt}/{maxAttempts}) (y/n)", "y");
                        if (retry.ToLower() == "y")
                        {
                            _logger.Information("Retrying configuration test...");
                            continue;
                        }
                    }

                    return false;
                }

                // Store the detected parameters
                model.UseLegacyMaxTokens = useLegacyTokens;
                model.Temperature = detectedTemperature;

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
            catch (HttpResponseException httpEx) when (httpEx.StatusCode == System.Net.HttpStatusCode.TooManyRequests)
            {
                _logger.Warning("⚠️  Rate limited by the API provider. This is common with free models.");

                if (attempt < maxAttempts)
                {
                    _logger.Information("The API's built-in retry mechanism will handle rate limiting automatically.");
                    var retry = Prompt($"Would you like to retry? (attempt {attempt}/{maxAttempts}) (y/n)", "y");
                    if (retry.ToLower() == "y")
                    {
                        _logger.Information("Waiting a moment before retrying...");
                        await Task.Delay(TimeSpan.FromSeconds(Math.Pow(2, attempt)));
                        continue;
                    }
                }

                // Rate limiting error already shown
                return false;
            }
            catch (Exception ex)
            {
                // For unexpected errors not handled by parameter detector, show the error
                if (!(ex.InnerException is HttpRequestException))
                {
                    _logger.Error($"❌ Unexpected error: {ex.Message}");
                }
                // Otherwise, error was already shown by parameter detector

                if (attempt < maxAttempts)
                {
                    var retry = Prompt($"Would you like to retry? (attempt {attempt}/{maxAttempts}) (y/n)", "y");
                    if (retry.ToLower() == "y")
                    {
                        continue;
                    }
                }

                return false;
            }
        }

        // All retries exhausted
        return false;
    }

    private async Task ConfigurePricing(ModelConfiguration model)
    {
        _logger.Information("");
        _logger.Information("");
        _logger.Information("");
        _logger.Highlight("Step 8: Configure pricing information.", ConsoleColor.Magenta);
        _logger.Information("Enter costs per million tokens.");

        // Pricing is already initialized in ModelConfiguration constructor

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
            var inputCostStr = Prompt("Input cost per million tokens:");
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
            var outputCostStr = Prompt("Output cost per million tokens:");
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
        _logger.Information("");
        _logger.Information("");
        _logger.Highlight("Step 9: Configure custom system prompt (optional).", ConsoleColor.Cyan);
        _logger.Information("This will be appended to GitGen's base instructions for this model.");
        _logger.Muted("Example: 'Always use conventional commit format' or 'Must start with a Haiku' or 'Focus on architectural changes'");

        var systemPrompt = Prompt("Enter custom system prompt:", "");
        if (!string.IsNullOrWhiteSpace(systemPrompt))
        {
            model.SystemPrompt = systemPrompt;
        }

        await Task.CompletedTask;
    }

    private void DisplayModelSummary(ModelConfiguration model)
    {
        _logger.Information("");
        _logger.Information("");
        _logger.Information("");
        _logger.Highlight("Step 10: Review configuration summary.", ConsoleColor.Green);
        _logger.Information("");
        _logger.Information("📋 Model Configuration Summary:");
        _logger.Information($"   Name: {model.Name}");

        // Display aliases
        if (model.Aliases != null && model.Aliases.Count > 0)
            _logger.Information($"   Aliases: {string.Join(", ", model.Aliases)}");

        // Display note/description
        if (!string.IsNullOrWhiteSpace(model.Note))
            _logger.Information($"   Description: {model.Note}");

        _logger.Information($"   Type: {model.Type}");
        _logger.Information($"   Provider: {model.Provider}");
        _logger.Information($"   URL: {model.Url}");
        _logger.Information($"   Model ID: {model.ModelId}");
        _logger.Information($"   API Key: {ValidationService.ApiKey.Mask(model.ApiKey)}");
        _logger.Information($"   Max Tokens: {model.MaxOutputTokens}");

        // Display pricing with currency symbol
        var pricingInfo = CostCalculationService.FormatPricingInfo(model.Pricing);
        _logger.Information($"   Pricing: {pricingInfo}");
        _logger.Information($"   Currency: {model.Pricing.CurrencyCode} ({CostCalculationService.GetCurrencySymbol(model.Pricing.CurrencyCode)})");

        // Display system prompt
        if (!string.IsNullOrWhiteSpace(model.SystemPrompt))
        {
            var truncatedPrompt = model.SystemPrompt.Length > 50
                ? model.SystemPrompt.Substring(0, 50) + "..."
                : model.SystemPrompt;
            _logger.Information($"   System Prompt: {truncatedPrompt}");
        }
        else
        {
            _logger.Information($"   System Prompt: (default)");
        }

        _logger.Information("");
    }

    /// <summary>
    ///     Checks if a model appears to be configured for public/free use based on its aliases and description.
    /// </summary>
    private bool AppearsToBePublicModel(ModelConfiguration model)
    {
        // Keywords that suggest public/free usage
        string[] publicKeywords = { "free", "public", "open" };

        // Check aliases
        if (model.Aliases != null)
        {
            foreach (var alias in model.Aliases)
            {
                if (publicKeywords.Any(keyword => alias.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                    return true;
            }
        }

        // Check note/description
        if (!string.IsNullOrWhiteSpace(model.Note))
        {
            if (publicKeywords.Any(keyword => model.Note.Contains(keyword, StringComparison.OrdinalIgnoreCase)))
                return true;
        }

        // Check if it's a known free provider endpoint
        if (!string.IsNullOrWhiteSpace(model.Url))
        {
            // Check for common free model endpoints
            if (model.Url.Contains("openrouter", StringComparison.OrdinalIgnoreCase) &&
                model.ModelId != null && model.ModelId.Contains(":free", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        // Check if pricing indicates it's free
        if (model.Pricing.InputPer1M == 0 &&
            model.Pricing.OutputPer1M == 0)
            return true;

        return false;
    }
}