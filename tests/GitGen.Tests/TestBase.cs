using System.IO.Abstractions.TestingHelpers;
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

    protected TestBase()
    {
        Logger = Substitute.For<IConsoleLogger>();
        FileSystem = new MockFileSystem();
        TestDirectory = Path.Combine(Path.GetTempPath(), "gitgen-tests", Guid.NewGuid().ToString());
    }

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
        FileSystem.AddFile(filePath, new MockFileData(content));
        return filePath;
    }

    public virtual void Dispose()
    {
        // Clean up test directory if it exists
        if (Directory.Exists(TestDirectory))
        {
            Directory.Delete(TestDirectory, true);
        }
    }
}