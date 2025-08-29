using System;
using FluentAssertions;
using GitGen.Configuration;
using GitGen.Providers;
using GitGen.Providers.OpenAI;
using GitGen.Services;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace GitGen.Tests.Providers;

public class ProviderFactoryTests
{
    private readonly Mock<IServiceProvider> _mockServiceProvider;
    private readonly Mock<IConsoleLogger> _mockLogger;
    private readonly Mock<IHttpClientService> _mockHttpClient;
    private readonly Mock<ConsoleLoggerFactory> _mockLoggerFactory;
    private readonly Mock<ILlmCallTracker> _mockCallTracker;
    private readonly ProviderFactory _factory;

    public ProviderFactoryTests()
    {
        _mockServiceProvider = new Mock<IServiceProvider>();
        _mockLogger = new Mock<IConsoleLogger>();
        _mockHttpClient = new Mock<IHttpClientService>();
        _mockLoggerFactory = new Mock<ConsoleLoggerFactory>();
        _mockCallTracker = new Mock<ILlmCallTracker>();

        // Setup service provider to return mocked dependencies
        _mockServiceProvider.Setup(x => x.GetService(typeof(IHttpClientService)))
            .Returns(_mockHttpClient.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(ConsoleLoggerFactory)))
            .Returns(_mockLoggerFactory.Object);
        _mockServiceProvider.Setup(x => x.GetService(typeof(ILlmCallTracker)))
            .Returns(_mockCallTracker.Object);

        _mockLoggerFactory.Setup(x => x.CreateLogger<OpenAIProvider>())
            .Returns(_mockLogger.Object);

        _factory = new ProviderFactory(_mockServiceProvider.Object, _mockLogger.Object);
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange & Act
        var factory = new ProviderFactory(_mockServiceProvider.Object, _mockLogger.Object);

        // Assert
        factory.Should().NotBeNull();
    }

    #endregion

    #region CreateProvider Tests

    [Fact]
    public void CreateProvider_WithOpenAIType_ReturnsOpenAIProvider()
    {
        // Arrange
        var config = new ModelConfiguration
        {
            Type = "openai",
            Name = "gpt-4",
            ApiKey = "test-key",
            Url = "https://api.openai.com/v1"
        };

        // Act
        var provider = _factory.CreateProvider(config);

        // Assert
        provider.Should().NotBeNull();
        provider.Should().BeOfType<OpenAIProvider>();
        _mockServiceProvider.Verify(x => x.GetService(typeof(IHttpClientService)), Times.Once);
        _mockServiceProvider.Verify(x => x.GetService(typeof(ConsoleLoggerFactory)), Times.Once);
    }

    [Fact]
    public void CreateProvider_WithOpenAICompatibleType_ReturnsOpenAIProvider()
    {
        // Arrange
        var config = new ModelConfiguration
        {
            Type = "openai-compatible",
            Name = "custom-model",
            ApiKey = "test-key",
            Url = "https://custom.api.com/v1"
        };

        // Act
        var provider = _factory.CreateProvider(config);

        // Assert
        provider.Should().NotBeNull();
        provider.Should().BeOfType<OpenAIProvider>();
    }

    [Theory]
    [InlineData("OPENAI")]
    [InlineData("OpenAI")]
    [InlineData("openai")]
    [InlineData("OpenAI-Compatible")]
    [InlineData("OPENAI-COMPATIBLE")]
    public void CreateProvider_WithVariousCasing_HandlesCorrectly(string type)
    {
        // Arrange
        var config = new ModelConfiguration
        {
            Type = type,
            Name = "test-model",
            ApiKey = "test-key",
            Url = "https://api.example.com"
        };

        // Act
        var provider = _factory.CreateProvider(config);

        // Assert
        provider.Should().NotBeNull();
        provider.Should().BeOfType<OpenAIProvider>();
    }

    [Fact]
    public void CreateProvider_WithUnsupportedType_ThrowsNotSupportedException()
    {
        // Arrange
        var config = new ModelConfiguration
        {
            Type = "anthropic",
            Name = "claude-3",
            ApiKey = "test-key"
        };

        // Act
        var act = () => _factory.CreateProvider(config);

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage("*anthropic*not supported*")
            .WithMessage("*openai, openai-compatible*");
    }

    [Fact]
    public void CreateProvider_WithNullType_ThrowsNotSupportedException()
    {
        // Arrange
        var config = new ModelConfiguration
        {
            Type = null,
            Name = "test-model"
        };

        // Act
        var act = () => _factory.CreateProvider(config);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void CreateProvider_WithEmptyType_ThrowsNotSupportedException()
    {
        // Arrange
        var config = new ModelConfiguration
        {
            Type = "",
            Name = "test-model"
        };

        // Act
        var act = () => _factory.CreateProvider(config);

        // Assert
        act.Should().Throw<NotSupportedException>();
    }

    [Fact]
    public void CreateProvider_WithNullConfiguration_ThrowsArgumentNullException()
    {
        // Act
        var action = () => _factory.CreateProvider(null as ModelConfiguration);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("modelConfig");
    }

    #endregion

    #region Dependency Injection Tests

    [Fact]
    public void CreateProvider_WithMissingHttpClientService_ThrowsInvalidOperationException()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(IHttpClientService)))
            .Returns(null);
        serviceProvider.Setup(x => x.GetService(typeof(ConsoleLoggerFactory)))
            .Returns(_mockLoggerFactory.Object);

        var factory = new ProviderFactory(serviceProvider.Object, _mockLogger.Object);
        var config = new ModelConfiguration { Type = "openai", Name = "gpt-4" };

        // Act
        var act = () => factory.CreateProvider(config);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateProvider_WithMissingLoggerFactory_ThrowsInvalidOperationException()
    {
        // Arrange
        var serviceProvider = new Mock<IServiceProvider>();
        serviceProvider.Setup(x => x.GetService(typeof(IHttpClientService)))
            .Returns(_mockHttpClient.Object);
        serviceProvider.Setup(x => x.GetService(typeof(ConsoleLoggerFactory)))
            .Returns(null);

        var factory = new ProviderFactory(serviceProvider.Object, _mockLogger.Object);
        var config = new ModelConfiguration { Type = "openai", Name = "gpt-4" };

        // Act
        var act = () => factory.CreateProvider(config);

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void CreateProvider_WithOptionalCallTracker_WorksWithOrWithout()
    {
        // Arrange - With call tracker
        var config = new ModelConfiguration { Type = "openai", Name = "gpt-4" };

        // Act
        var providerWithTracker = _factory.CreateProvider(config);

        // Assert
        providerWithTracker.Should().NotBeNull();

        // Arrange - Without call tracker
        _mockServiceProvider.Setup(x => x.GetService(typeof(ILlmCallTracker)))
            .Returns(null);

        // Act
        var providerWithoutTracker = _factory.CreateProvider(config);

        // Assert
        providerWithoutTracker.Should().NotBeNull();
    }

    #endregion

    #region Virtual Method Tests

    [Fact]
    public void CreateProvider_IsVirtual_CanBeOverridden()
    {
        // Arrange
        var mockFactory = new Mock<ProviderFactory>(_mockServiceProvider.Object, _mockLogger.Object);
        var expectedProvider = new Mock<ICommitMessageProvider>().Object;
        var config = new ModelConfiguration { Type = "custom", Name = "custom-model" };

        mockFactory.Setup(x => x.CreateProvider(It.IsAny<ModelConfiguration>()))
            .Returns(expectedProvider);

        // Act
        var provider = mockFactory.Object.CreateProvider(config);

        // Assert
        provider.Should().BeSameAs(expectedProvider);
        mockFactory.Verify(x => x.CreateProvider(config), Times.Once);
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void CreateProvider_WithWhitespaceType_HandlesCorrectly()
    {
        // Arrange
        var config = new ModelConfiguration
        {
            Type = "  openai  ",
            Name = "gpt-4"
        };

        // Act
        var provider = _factory.CreateProvider(config);

        // Assert
        provider.Should().NotBeNull();
        provider.Should().BeOfType<OpenAIProvider>();
    }

    [Theory]
    [InlineData("azure-openai")]
    [InlineData("claude")]
    [InlineData("gemini")]
    [InlineData("unknown-provider")]
    [InlineData("123")]
    [InlineData("!@#$%")]
    public void CreateProvider_WithVariousUnsupportedTypes_ThrowsNotSupportedException(string type)
    {
        // Arrange
        var config = new ModelConfiguration
        {
            Type = type,
            Name = "test-model"
        };

        // Act
        var act = () => _factory.CreateProvider(config);

        // Assert
        act.Should().Throw<NotSupportedException>()
            .WithMessage($"*{type}*not supported*");
    }

    #endregion
}