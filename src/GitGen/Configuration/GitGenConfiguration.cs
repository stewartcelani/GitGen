using GitGen.Services;

namespace GitGen.Configuration;

/// <summary>
///     Holds all configuration for GitGen, loaded from environment variables.
/// </summary>
public class GitGenConfiguration
{
    /// <summary>
    ///     The high-level provider type, e.g., "openai".
    /// </summary>
    public string? ProviderType { get; set; }

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
    ///     Can be adjusted anytime using 'gitgen configure'.
    /// </summary>
    public int MaxOutputTokens { get; set; } = 5000;

    /// <summary>
    ///     Validates that the configuration has all necessary values and passes comprehensive validation.
    /// </summary>
    public bool IsValid =>
        ValidationService.Provider.IsValid(ProviderType) &&
        ValidationService.Url.IsValid(BaseUrl) &&
        ValidationService.Model.IsValid(Model) &&
        ValidationService.ApiKey.IsValid(ApiKey, RequiresAuth) &&
        ValidationService.Temperature.IsValid(Temperature) &&
        ValidationService.TokenCount.IsValid(MaxOutputTokens);
}