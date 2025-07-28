using System.IO.Abstractions.TestingHelpers;
using GitGen.Configuration;
using GitGen.Services;
using Microsoft.Extensions.DependencyInjection;
using NSubstitute;

namespace GitGen.Tests;

/// <summary>
/// Base class for all tests providing common setup and utilities.
/// </summary>
public abstract class TestBase : IDisposable
{
    protected IConsoleLogger Logger { get; }
    protected MockFileSystem FileSystem { get; }
    protected string TestDirectory { get; }
    private bool _disposed;

    protected TestBase()
    {
        Logger = Substitute.For<IConsoleLogger>();
        FileSystem = new MockFileSystem();
        TestDirectory = Path.Combine(Path.GetTempPath(), "gitgen-tests", Guid.NewGuid().ToString());
        
        // Ensure test directory exists
        Directory.CreateDirectory(TestDirectory);
        
        // Capture original console streams
        OriginalOut = Console.Out;
        OriginalIn = Console.In;
    }

    protected TextWriter OriginalOut { get; }
    protected TextReader OriginalIn { get; }

    /// <summary>
    /// Creates a service provider with test dependencies.
    /// </summary>
    protected IServiceProvider CreateServiceProvider(Action<IServiceCollection>? configure = null)
    {
        var services = new ServiceCollection();
        
        // Add test dependencies
        services.AddSingleton(Logger);
        services.AddSingleton<ConsoleLoggerFactory>(_ => 
        {
            var factory = Substitute.For<ConsoleLoggerFactory>();
            factory.CreateLogger<object>().Returns(Logger);
            factory.CreateLogger(Arg.Any<string>()).Returns(Logger);
            return factory;
        });
        
        // Allow custom configuration
        configure?.Invoke(services);
        
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Creates a temporary file with content.
    /// </summary>
    protected string CreateTempFile(string content, string extension = ".txt")
    {
        var fileName = $"{Guid.NewGuid()}{extension}";
        var filePath = Path.Combine(TestDirectory, fileName);
        File.WriteAllText(filePath, content);
        return filePath;
    }

    /// <summary>
    /// Creates a test model configuration with reasonable defaults.
    /// </summary>
    protected ModelConfiguration CreateTestModel(string? name = null)
    {
        var model = new ModelConfiguration
        {
            Id = Guid.NewGuid().ToString(),
            Name = name ?? "test-model",
            Type = "openai-compatible",
            Provider = "TestProvider",
            Url = "https://api.test.com/v1/chat/completions",
            ModelId = "test-model-id",
            ApiKey = "test-key-123456789",
            RequiresAuth = true,
            Temperature = 0.2,
            MaxOutputTokens = 2000
        };
        
        // Set pricing values for tests
        model.Pricing.InputPer1M = 10;
        model.Pricing.OutputPer1M = 20;
        model.Pricing.CurrencyCode = "USD";
        
        return model;
    }

    public virtual void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Restore console streams
                Console.SetOut(OriginalOut);
                Console.SetIn(OriginalIn);
                
                // Clean up test directory if it exists
                try
                {
                    if (Directory.Exists(TestDirectory))
                    {
                        Directory.Delete(TestDirectory, true);
                    }
                }
                catch
                {
                    // Ignore cleanup errors in tests
                }
            }
            _disposed = true;
        }
    }
}