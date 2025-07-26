namespace GitGen;

/// <summary>
///     Centralized constants for GitGen application to eliminate magic values and ensure consistency.
/// </summary>
public static class Constants
{
    /// <summary>
    ///     Configuration-related constants including defaults, limits, and provider settings.
    /// </summary>
    public static class Configuration
    {
        // Provider Types
        public const string ProviderTypeOpenAI = "openai";

        // Temperature Settings
        public const double DefaultTemperature = 0.2;
        public const double ReasoningModelTemperature = 1.0;
        public const double MinTemperature = 0.0;
        public const double MaxTemperature = 2.0;

        // Token Limits
        public const int DefaultMaxOutputTokens = 5000;
        public const int MinOutputTokens = 100;
        public const int MaxOutputTokens = 8000;

        // Default Models and URLs
        public const string DefaultOpenAIModel = "o4-mini";
        public const string DefaultOpenAIBaseUrl = "https://api.openai.com/v1/chat/completions";
        public const string DefaultLocalBaseUrl = "http://localhost:11434/v1/chat/completions";

        // API Key Security
        public const int MinApiKeyLength = 10;
        public const int MaxApiKeyLength = 200;
        public const int ApiKeyMaskLength = 8;

        // Model Name Validation
        public const int MaxModelNameLength = 100;

        // Commit Message Limits
        public const int CommitMessageMaxLength = 300;
    }

    /// <summary>
    ///     User-facing messages for success, information, and guidance.
    /// </summary>
    public static class Messages
    {
        // Repository Status
        public const string NoGitRepository = "Current directory is not a Git repository.";
        public const string NoUncommittedChanges = "No uncommitted changes detected.";

        // Configuration
        public const string ConfigurationMissing = "Configuration is missing or invalid.";
        public const string ConfigurationSaved = "Configuration saved successfully!";
        public const string ConfigurationTestSuccessful = "Configuration test successful!";

        public const string RestartTerminalWarning =
            "You may need to restart your terminal for the changes to take effect.";

        // Generation Process
        public const string GeneratingCommitMessage = "Generating commit message...";
        public const string CommitMessageCopied = "Commit message copied to clipboard.";
        public const string TestingConnection = "Testing LLM connection...";

        // Wizard
        public const string WelcomeToWizard = "Welcome to the GitGen configuration wizard.";
        public const string WizardGuidance = "This will guide you through setting up your AI provider.";

        // Reset
        public const string ConfigurationReset = "GitGen configuration reset successfully!";
        public const string ResettingConfiguration = "Resetting GitGen configuration...";
    }

    /// <summary>
    ///     Error messages for consistent error reporting across the application.
    /// </summary>
    public static class ErrorMessages
    {
        // Authentication
        public const string AuthenticationFailed =
            "Authentication failed. Your API key appears to be invalid or expired.";

        public const string AuthenticationFailedGuidance = "To fix this issue, please run: gitgen configure";

        public const string AuthenticationFailedDetail =
            "This will guide you through updating your API key and configuration.";

        // Configuration
        public const string ConfigurationTestFailed = "Configuration test failed: {0}";
        public const string FailedToSaveConfiguration = "Failed to save configuration: {0}";
        public const string ConfigurationSetupFailed = "Configuration setup failed or was cancelled.";

        public const string ConfigurationInvalid =
            "Configuration is missing or invalid. Please run 'gitgen configure' first.";

        // API Errors
        public const string ApiRequestFailed = "API request failed with status {0}: {1}";
        public const string ConnectionTestFailed = "Connection test failed during {0} detection. Cannot proceed.";
        public const string ParameterDetectionFailed = "Failed to detect API parameters";

        public const string SelfHealingFailed =
            "Failed to auto-correct API parameter configuration. Please run 'gitgen configure'.";

        // Environment Variables
        public const string ShellProfileNotFound =
            "Could not determine shell profile (~/.bashrc, ~/.zshrc, etc.). Please set variables manually:";

        public const string FailedToUpdateShellProfile = "Failed to update shell profile {0}: {1}";
        public const string FailedToClearVariable = "Failed to clear environment variable {0}: {1}";

        // Generation
        public const string GenerationFailed = "Failed to generate commit message";
        public const string UnexpectedError = "An unexpected error occurred: {0}";
        public const string EmptyResponse = "Provider returned empty response";

        public const string EmptyCommitMessage =
            "Provider returned empty commit message (may have hit token limit during reasoning)";

        // Model Change
        public const string ModelChangeFailed = "Failed to connect to model '{0}'. Model change cancelled.";
        public const string ModelChangeError = "Model change failed: {0}";

        // Validation
        public const string InvalidChoice = "Invalid choice. Aborting.";
        public const string ValueCannotBeEmpty = "This value cannot be empty.";

        public const string TokensOutOfRange =
            "GITGEN_MAX_OUTPUT_TOKENS value {0} is out of range ({1}-{2}). Using default value {3}.";

        public const string InvalidTokenRange = "Please enter a number between {0} and {1}.";
    }

    /// <summary>
    ///     Environment variable names and prefixes used by GitGen.
    /// </summary>
    public static class EnvironmentVariables
    {
        public const string Prefix = "GITGEN_";

        // Core Configuration
        public const string ProviderType = "GITGEN_PROVIDERTYPE";
        public const string BaseUrl = "GITGEN_BASEURL";
        public const string Model = "GITGEN_MODEL";
        public const string ApiKey = "GITGEN_APIKEY";
        public const string RequiresAuth = "GITGEN_REQUIRESAUTH";

        // OpenAI Specific
        public const string UseLegacyMaxTokens = "GITGEN_OPENAI_USE_LEGACY_MAX_TOKENS";
        public const string Temperature = "GITGEN_TEMPERATURE";
        public const string MaxOutputTokens = "GITGEN_MAX_OUTPUT_TOKENS";

        /// <summary>
        ///     All GitGen environment variable names (without GITGEN_ prefix) for cleanup operations.
        /// </summary>
        public static readonly string[] AllVariableNames =
        {
            "PROVIDERTYPE",
            "BASEURL",
            "MODEL",
            "APIKEY",
            "REQUIRESAUTH",
            "OPENAI_USE_LEGACY_MAX_TOKENS",
            "TEMPERATURE",
            "MAX_OUTPUT_TOKENS"
        };
    }

    /// <summary>
    ///     API-related constants for error detection and parameter handling.
    /// </summary>
    public static class Api
    {
        // Error Patterns
        public const string UnsupportedParameterError = "unsupported_parameter";
        public const string UnsupportedValueError = "unsupported_value";
        public const string InvalidRequestError = "invalid_request_error";
        public const string InvalidApiKeyError = "invalid_api_key";
        public const string IncorrectApiKeyError = "Incorrect API key provided";
        public const string InvalidApiKeyGeneric = "Invalid API key";
        public const string UnauthorizedError = "Unauthorized";

        // Parameter Names
        public const string MaxTokensParameter = "max_tokens";
        public const string MaxCompletionTokensParameter = "max_completion_tokens";
        public const string TemperatureParameter = "temperature";

        // Headers
        public const string AuthorizationHeader = "Authorization";
        public const string BearerPrefix = "Bearer";
        public const string AzureApiKeyHeader = "api-key";

        // Azure Detection
        public const string AzureUrlPattern = "openai.azure.com";

        // Test Values
        public const string TestPrompt = "Hello!";
        public const string TestLlmPrompt = "Testing.";
        public const int TestTokenLimit = 5;
    }

    /// <summary>
    ///     UI and formatting constants for consistent user experience.
    /// </summary>
    public static class UI
    {
        // Emojis and Symbols
        public const string CheckMark = "✅";
        public const string CrossMark = "❌";
        public const string InfoSymbol = "ℹ️";
        public const string WarningSymbol = "⚠️";
        public const string LoadingSymbol = "⏳";
        public const string GearSymbol = "🔧";
        public const string WorldSymbol = "🌍";
        public const string TestTubeSymbol = "🧪";
        public const string LinkSymbol = "🔗";
        public const string PartySymbol = "🎉";
        public const string ClipboardSymbol = "📋";
        public const string BulbSymbol = "💡";
        public const string SaveSymbol = "💾";
        public const string TargetSymbol = "🎯";
        public const string RefreshSymbol = "🔄";
        public const string ChartSymbol = "📋";

        // Formatting
        public const string TimestampFormat = "HH:mm:ss";
        public const int DebugLevelPadding = 7;
        public const string CommitMessageQuotes = "\"";

        // Configuration Section Header
        public const string GitGenConfigSection = "# GitGen Configuration (managed by gitgen)";
        public const string GitGenConfigSectionWizard = "# GitGen Configuration (managed by gitgen configure)";
        public const string GitGenConfigSectionModel = "# GitGen Configuration (managed by gitgen model)";
    }

    /// <summary>
    ///     Shell and platform-specific constants.
    /// </summary>
    public static class Platform
    {
        // Shell Types
        public const string ZshShell = "zsh";
        public const string BashShell = "bash";

        // Profile Files
        public const string ZshProfile = ".zshrc";
        public const string BashProfile = ".bashrc";
        public const string GenericProfile = ".profile";

        // Environment Variable Export
        public const string ExportPrefix = "export ";
        public const string ExportFormat = "export {0}=\"{1}\"";
        public const string ExportStartPattern = "export GITGEN_";
    }

    /// <summary>
    ///     Fallback and default values for error recovery.
    /// </summary>
    public static class Fallbacks
    {
        public const string DefaultCommitMessage = "Automated commit of code changes.";
        public const string NoResponseMessage = "No response received from LLM.";
        public const string UnknownProviderName = "Unknown";
        public const string UnknownModelName = "unknown";
        public const string NotSetValue = "(not set)";
        public const string NotRequiredValue = "not-required";
    }
}