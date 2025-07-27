using GitGen.Services;

namespace GitGen.Configuration;

/// <summary>
///     Holds all configuration for GitGen.
/// </summary>
public class GitGenConfiguration
{
    /// <summary>
    ///     The API compatibility type, e.g., "openai-compatible".
    /// </summary>
    public string? Type { get; set; }

    /// <summary>
    ///     The provider name (e.g., "openrouter.ai", "OpenAI", "Anthropic").
    ///     This can be the domain extracted from the URL or a custom name set by the user.
    /// </summary>
    public string? Provider { get; set; }

    public string? BaseUrl { get; set; }
    public string? Model { get; set; }
    public string? ApiKey { get; set; }

    /// <summary>
    ///     Determines whether an API key is required for requests. Defaults to true.
    ///     This allows for local, no-auth providers.
    /// </summary>
    public bool RequiresAuth { get; set; } = true;

    /// <summary>
    ///     If true, the application will use the legacy 'max_tokens' parameter for OpenAI-compatible providers.
    ///     This is determined during setup and auto-corrected if a mismatch is detected.
    /// </summary>
    public bool OpenAiUseLegacyMaxTokens { get; set; }

    /// <summary>
    ///     The specific temperature value required by the configured model.
    ///     This is discovered during setup and defaults to 0.2.
    /// </summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>
    ///     Maximum number of tokens the AI model should generate in responses.
    ///     Defaults to 5000. Higher values (6000+) recommended for reasoning models.
    ///     Can be adjusted anytime using 'gitgen config'.
    /// </summary>
    public int MaxOutputTokens { get; set; } = 5000;

    /// <summary>
    ///     Custom system prompt to append to the base GitGen instructions.
    ///     This allows per-model customization of behavior.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    ///     Validates that the configuration has all necessary values and passes comprehensive validation.
    /// </summary>
    public bool IsValid =>
        ValidationService.Provider.IsValid(Type) &&
        ValidationService.Url.IsValid(BaseUrl) &&
        ValidationService.Model.IsValid(Model) &&
        ValidationService.ApiKey.IsValid(ApiKey, RequiresAuth) &&
        ValidationService.Temperature.IsValid(Temperature) &&
        ValidationService.TokenCount.IsValid(MaxOutputTokens);

    /// <summary>
    ///     Gets a detailed validation report for debugging purposes.
    /// </summary>
    /// <returns>A dictionary of field names and their validation errors.</returns>
    public Dictionary<string, string> GetValidationErrors()
    {
        var errors = new Dictionary<string, string>();

        if (!ValidationService.Provider.IsValid(Type))
            errors["Type"] = ValidationService.Provider.GetValidationError(Type);

        if (!ValidationService.Url.IsValid(BaseUrl))
            errors["BaseUrl"] = ValidationService.Url.GetValidationError(BaseUrl);

        if (!ValidationService.Model.IsValid(Model))
            errors["Model"] = ValidationService.Model.GetValidationError(Model);

        if (!ValidationService.ApiKey.IsValid(ApiKey, RequiresAuth))
            errors["ApiKey"] = ValidationService.ApiKey.GetValidationError(ApiKey, RequiresAuth);

        if (!ValidationService.Temperature.IsValid(Temperature))
            errors["Temperature"] = ValidationService.Temperature.GetValidationError(Temperature);

        if (!ValidationService.TokenCount.IsValid(MaxOutputTokens))
            errors["MaxOutputTokens"] = ValidationService.TokenCount.GetValidationError(MaxOutputTokens);

        return errors;
    }

    /// <summary>
    ///     Checks if the configuration has any values set (not necessarily valid).
    /// </summary>
    public bool HasAnyConfiguration =>
        !string.IsNullOrEmpty(Type) ||
        !string.IsNullOrEmpty(BaseUrl) ||
        !string.IsNullOrEmpty(Model) ||
        !string.IsNullOrEmpty(ApiKey);
}