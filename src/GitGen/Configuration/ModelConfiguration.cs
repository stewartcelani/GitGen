using GitGen.Services;

namespace GitGen.Configuration;

/// <summary>
///     Represents a single AI model configuration with all its settings, metadata, and pricing information.
/// </summary>
public class ModelConfiguration
{
    private string _name = string.Empty;

    /// <summary>
    ///     Gets or sets the unique identifier for this model configuration.
    ///     This is automatically set to the model's Name for readability.
    /// </summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the user-friendly name for this model (e.g., "gpt-4-turbo", "claude-work").
    ///     Setting this also updates the Id property.
    /// </summary>
    public string Name
    {
        get => _name;
        set
        {
            _name = value;
            // Automatically sync Id with Name
            if (!string.IsNullOrEmpty(value))
            {
                Id = value;
            }
        }
    }

    /// <summary>
    ///     Gets or sets the API compatibility type (e.g., "openai-compatible").
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the provider name (e.g., "openrouter.ai", "OpenAI", "Anthropic").
    ///     This can be the domain extracted from the URL or a custom name set by the user.
    /// </summary>
    public string Provider { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the base URL for the API endpoint.
    /// </summary>
    public string Url { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the model identifier used by the provider (e.g., "gpt-4-turbo-preview").
    /// </summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets the API key for authentication (stored encrypted).
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets whether this model requires authentication.
    /// </summary>
    public bool RequiresAuth { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to use legacy max_tokens parameter (for compatibility).
    /// </summary>
    public bool UseLegacyMaxTokens { get; set; }

    /// <summary>
    ///     Gets or sets the temperature value for this model (0.0 to 2.0).
    /// </summary>
    public double Temperature { get; set; } = 0.2;

    /// <summary>
    ///     Gets or sets the maximum number of output tokens for this model.
    /// </summary>
    public int MaxOutputTokens { get; set; } = 5000;

    /// <summary>
    ///     Gets or sets the context length (maximum input tokens) for this model.
    ///     If not set, a conservative default will be used for truncation.
    /// </summary>
    public int? ContextLength { get; set; }

    /// <summary>
    ///     Gets or sets when this model configuration was created.
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets when this model was last used.
    /// </summary>
    public DateTime LastUsed { get; set; } = DateTime.UtcNow;

    /// <summary>
    ///     Gets or sets an optional note or description for this model.
    /// </summary>
    public string? Note { get; set; }

    /// <summary>
    ///     Gets or sets a custom system prompt to append to GitGen's base instructions.
    /// </summary>
    public string? SystemPrompt { get; set; }

    /// <summary>
    ///     Gets or sets the list of aliases that can be used to reference this model.
    ///     Aliases must be unique across all models and are case-insensitive.
    /// </summary>
    public List<string> Aliases { get; set; } = new();

    /// <summary>
    ///     Gets or sets the pricing information for this model.
    /// </summary>
    public PricingInfo Pricing { get; set; } = new();

    /// <summary>
    ///     Validates that the configuration has all necessary values and passes comprehensive validation.
    /// </summary>
    public bool IsValid =>
        ValidationService.Provider.IsValid(Type) &&
        ValidationService.Url.IsValid(Url) &&
        ValidationService.Model.IsValid(ModelId) &&
        ValidationService.ApiKey.IsValid(ApiKey, RequiresAuth) &&
        ValidationService.Temperature.IsValid(Temperature) &&
        ValidationService.TokenCount.IsValid(MaxOutputTokens) &&
        ValidationService.Pricing.IsValid(Pricing);

    /// <summary>
    ///     Gets a detailed validation report for debugging purposes.
    /// </summary>
    /// <returns>A dictionary of field names and their validation errors.</returns>
    public Dictionary<string, string> GetValidationErrors()
    {
        var errors = new Dictionary<string, string>();

        if (!ValidationService.Provider.IsValid(Type))
            errors["Type"] = ValidationService.Provider.GetValidationError(Type);

        if (!ValidationService.Url.IsValid(Url))
            errors["Url"] = ValidationService.Url.GetValidationError(Url);

        if (!ValidationService.Model.IsValid(ModelId))
            errors["ModelId"] = ValidationService.Model.GetValidationError(ModelId);

        if (!ValidationService.ApiKey.IsValid(ApiKey, RequiresAuth))
            errors["ApiKey"] = ValidationService.ApiKey.GetValidationError(ApiKey, RequiresAuth);

        if (!ValidationService.Temperature.IsValid(Temperature))
            errors["Temperature"] = ValidationService.Temperature.GetValidationError(Temperature);

        if (!ValidationService.TokenCount.IsValid(MaxOutputTokens))
            errors["MaxOutputTokens"] = ValidationService.TokenCount.GetValidationError(MaxOutputTokens);

        if (!ValidationService.Pricing.IsValid(Pricing))
            errors["Pricing"] = ValidationService.Pricing.GetValidationError(Pricing);

        return errors;
    }

    /// <summary>
    ///     Determines if this model appears to be configured for free/public use only.
    ///     Checks aliases, description, model ID, and pricing for indicators.
    /// </summary>
    /// <returns>True if the model appears to be for free/public use; otherwise, false.</returns>
    public bool IsFreeModel()
    {
        // Check if pricing indicates it's free (both input and output costs are zero)
        if (Pricing != null && Pricing.InputPer1M == 0 && Pricing.OutputPer1M == 0)
        {
            return true;
        }

        // Check if any alias contains "free" or "public" (case-insensitive)
        if (Aliases.Any(alias => alias.Contains("free", StringComparison.OrdinalIgnoreCase) ||
                                 alias.Contains("public", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Check if the note/description contains indicators
        if (!string.IsNullOrWhiteSpace(Note) &&
            (Note.Contains("free", StringComparison.OrdinalIgnoreCase) ||
             Note.Contains("public", StringComparison.OrdinalIgnoreCase) ||
             Note.Contains("PUBLIC REPO", StringComparison.OrdinalIgnoreCase)))
        {
            return true;
        }

        // Check if the model ID has :free or :public suffix
        if (ModelId.Contains(":free", StringComparison.OrdinalIgnoreCase) ||
            ModelId.Contains(":public", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return false;
    }
}

/// <summary>
///     Represents pricing information for a model including currency and rates.
/// </summary>
public class PricingInfo
{
    /// <summary>
    ///     Gets or sets the cost per million input tokens.
    /// </summary>
    public decimal InputPer1M { get; set; }

    /// <summary>
    ///     Gets or sets the cost per million output tokens.
    /// </summary>
    public decimal OutputPer1M { get; set; }

    /// <summary>
    ///     Gets or sets the currency code (e.g., "USD", "EUR", "AUD").
    /// </summary>
    public string CurrencyCode { get; set; } = "USD";

    /// <summary>
    ///     Gets or sets when the pricing information was last updated.
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}