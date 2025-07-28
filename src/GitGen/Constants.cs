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
        // Version Management
        public const string CurrentConfigVersion = "4.0";
        
        // Provider Types
        public const string ProviderTypeOpenAI = "openai";
        public const string ProviderTypeOpenAICompatible = "openai-compatible";

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

        // Generation Process
        public const string GeneratingCommitMessage = "Generating commit message...";
        public const string CommitMessageCopied = "Commit message copied to clipboard.";
        public const string TestingConnection = "Testing LLM connection...";

        // Wizard
        public const string WelcomeToWizard = "Welcome to the GitGen configuration wizard.";
        public const string WizardGuidance = "This will guide you through setting up your AI provider.";

        // Model Management
        public const string ModelDeleted = "Model '{0}' deleted successfully.";
        public const string ModelSet = "Default model set to '{0}'.";
    }

    /// <summary>
    ///     Error messages for consistent error reporting across the application.
    /// </summary>
    public static class ErrorMessages
    {
        // Authentication
        public const string AuthenticationFailed =
            "Authentication failed. Your API key appears to be invalid or expired.";

        public const string AuthenticationFailedGuidance = "To fix this issue, please run: gitgen config";

        public const string AuthenticationFailedDetail =
            "This will guide you through updating your API key and configuration.";

        // Configuration
        public const string ConfigurationTestFailed = "Configuration test failed: {0}";
        public const string FailedToSaveConfiguration = "Failed to save configuration: {0}";
        public const string ConfigurationSetupFailed = "Configuration setup failed or was cancelled.";

        public const string ConfigurationInvalid =
            "Configuration is missing or invalid. Please run 'gitgen config' first.";

        // API Errors
        public const string ApiRequestFailed = "API request failed with status {0}: {1}";
        public const string ConnectionTestFailed = "Connection test failed during {0} detection. Cannot proceed.";
        public const string ParameterDetectionFailed = "Failed to detect API parameters";

        public const string SelfHealingFailed =
            "Failed to auto-correct API parameter configuration. Please run 'gitgen config'.";

        // Generation
        public const string GenerationFailed = "Failed to generate commit message";
        public const string UnexpectedError = "An unexpected error occurred: {0}";
        public const string EmptyResponse = "Provider returned empty response";

        public const string EmptyCommitMessage =
            "Provider returned empty commit message (may have hit token limit during reasoning)";

        // Context Length
        public const string ContextLengthExceeded = "Context length exceeded";
        public const string ContextLengthRetryPrompt = "Would you like to retry with a truncated diff? (y/N): ";

        // Model Change
        public const string ModelChangeFailed = "Failed to connect to model '{0}'. Model change cancelled.";
        public const string ModelChangeError = "Model change failed: {0}";

        // Validation
        public const string InvalidChoice = "Invalid choice. Aborting.";
        public const string ValueCannotBeEmpty = "This value cannot be empty.";

        public const string TokensOutOfRange =
            "Max output tokens value {0} is out of range ({1}-{2}). Using default value {3}.";

        public const string InvalidTokenRange = "Please enter a number between {0} and {1}.";
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
        public const string ContextLengthExceededError = "context_length_exceeded";

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
    ///     Provider-specific constants for automatic provider detection and naming.
    /// </summary>
    public static class Providers
    {
        // Provider Names
        public const string OpenAI = "OpenAI";
        public const string Azure = "Azure";
        public const string Anthropic = "Anthropic";
        public const string GoogleGemini = "Google Gemini";
        public const string GoogleVertex = "Google Vertex AI";
        public const string Groq = "Groq";
        public const string OpenRouter = "OpenRouter";
        public const string XAI = "xAI";
        public const string Local = "Local";

        // Provider URL Patterns for auto-detection
        public const string OpenAIUrlPattern = "https://api.openai.com/";
        public const string AnthropicUrlPattern = "https://api.anthropic.com/";
        public const string GoogleGeminiUrlPattern = "generativelanguage.googleapis.com";
        public const string GoogleVertexUrlPattern = "aiplatform.googleapis.com";
        public const string GroqUrlPattern = "https://api.groq.com/";
        public const string OpenRouterUrlPattern = "https://openrouter.ai/";
        public const string XAIUrlPattern = "https://api.x.ai/";
        public const string AzureUrlPattern = "openai.azure.com";
        public const string LocalUrlPattern = "http://localhost";
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

        // Configuration Section Header (kept for historical reference if needed)
        public const string GitGenConfigSection = "# GitGen Configuration";
    }

    /// <summary>
    ///     Culture-aware date format specifiers for consistent date/time display.
    /// </summary>
    public static class DateFormats
    {
        /// <summary>Short date pattern (e.g., 1/27/2025 for US, 27/01/2025 for AU)</summary>
        public const string ShortDate = "d";
        
        /// <summary>Long date pattern (e.g., Monday, January 27, 2025 for US)</summary>
        public const string LongDate = "D";
        
        /// <summary>Short date/time pattern (e.g., 1/27/2025 2:32 PM for US)</summary>
        public const string ShortDateTime = "g";
        
        /// <summary>Short time pattern (e.g., 2:32 PM for US, 14:32 for many EU countries)</summary>
        public const string ShortTime = "t";
        
        /// <summary>Invariant format for filenames and technical use (always yyyyMMddHHmmss)</summary>
        public const string InvariantFileTimestamp = "yyyyMMddHHmmss";
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