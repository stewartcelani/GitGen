using GitGen.Configuration;
using GitGen.Providers.OpenAI;
using GitGen.Services;
using Microsoft.Extensions.DependencyInjection;

namespace GitGen.Providers;

/// <summary>
///     Factory for creating AI provider instances based on configuration.
///     Handles provider instantiation and dependency injection.
/// </summary>
public class ProviderFactory
{
    private readonly IConsoleLogger _logger;
    private readonly IServiceProvider _serviceProvider;

    /// <summary>
    ///     Initializes a new instance of the <see cref="ProviderFactory" /> class.
    /// </summary>
    /// <param name="serviceProvider">The service provider for dependency injection.</param>
    /// <param name="logger">The console logger for debugging and error reporting.</param>
    public ProviderFactory(IServiceProvider serviceProvider, IConsoleLogger logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    /// <summary>
    ///     Creates a provider instance based on the specified configuration.
    /// </summary>
    /// <param name="modelConfig">The model configuration containing provider type and settings.</param>
    /// <returns>An instance of the appropriate <see cref="ICommitMessageProvider" />.</returns>
    /// <exception cref="NotSupportedException">Thrown when the provider type is not supported.</exception>
    public virtual ICommitMessageProvider CreateProvider(ModelConfiguration modelConfig)
    {
        // Handle null case for test mocking scenarios
        if (modelConfig == null)
            return null!;

        var type = modelConfig.Type?.ToLowerInvariant();

        return type switch
        {
            "openai" => new OpenAIProvider(
                _serviceProvider.GetRequiredService<IHttpClientService>(),
                _serviceProvider.GetRequiredService<ConsoleLoggerFactory>().CreateLogger<OpenAIProvider>(),
                modelConfig,
                _serviceProvider.GetService<ILlmCallTracker>()),
            "openai-compatible" => new OpenAIProvider(
                _serviceProvider.GetRequiredService<IHttpClientService>(),
                _serviceProvider.GetRequiredService<ConsoleLoggerFactory>().CreateLogger<OpenAIProvider>(),
                modelConfig,
                _serviceProvider.GetService<ILlmCallTracker>()),
            _ => throw new NotSupportedException(
                $"API type '{modelConfig.Type}' is not supported. Supported types: openai, openai-compatible")
        };
    }
}