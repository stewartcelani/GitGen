namespace GitGen.Configuration;

/// <summary>
///     Root configuration containing all model configurations and application settings.
/// </summary>
public class GitGenSettings
{
    /// <summary>
    ///     Gets or sets the configuration file format version.
    /// </summary>
    public string Version { get; set; } = "2.0";

    /// <summary>
    ///     Gets or sets the list of configured AI models.
    /// </summary>
    public List<ModelConfiguration> Models { get; set; } = new();

    /// <summary>
    ///     Gets or sets the ID of the default model to use.
    /// </summary>
    public string? DefaultModelId { get; set; }

    /// <summary>
    ///     Gets or sets the application-wide settings.
    /// </summary>
    public AppSettings Settings { get; set; } = new();
}

/// <summary>
///     Application-wide settings that affect GitGen's behavior.
/// </summary>
public class AppSettings
{
    /// <summary>
    ///     Gets or sets whether to display token usage information after generation.
    /// </summary>
    public bool ShowTokenUsage { get; set; } = true;

    /// <summary>
    ///     Gets or sets whether to automatically copy commit messages to clipboard.
    /// </summary>
    public bool CopyToClipboard { get; set; } = true;

    /// <summary>
    ///     Gets or sets the path to the configuration file (for informational purposes).
    /// </summary>
    public string ConfigPath { get; set; } = string.Empty;

    /// <summary>
    ///     Gets or sets whether to enable partial alias matching (e.g., @gr matches @grok-3-mini).
    /// </summary>
    public bool EnablePartialAliasMatching { get; set; } = true;

    /// <summary>
    ///     Gets or sets the minimum number of characters required for partial alias matching.
    /// </summary>
    public int MinimumAliasMatchLength { get; set; } = 2;
}