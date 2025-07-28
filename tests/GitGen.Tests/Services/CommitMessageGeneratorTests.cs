using FluentAssertions;
using GitGen.Configuration;
using GitGen.Exceptions;
using GitGen.Providers;
using GitGen.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace GitGen.Tests.Services;

public class CommitMessageGeneratorTests : TestBase
{
    private readonly CommitMessageGenerator _generator;
    private readonly ProviderFactory _providerFactory;
    private readonly ICommitMessageProvider _provider;

    public CommitMessageGeneratorTests()
    {
        _providerFactory = Substitute.For<ProviderFactory>(CreateServiceProvider(), Logger);
        _provider = Substitute.For<ICommitMessageProvider>();
        _providerFactory.CreateProvider(Arg.Any<ModelConfiguration>()).Returns(_provider);
        
        _generator = new CommitMessageGenerator(_providerFactory, Logger);
    }

    [Fact]
    public async Task GenerateAsync_WithValidDiff_ReturnsCleanedMessage()
    {
        // Arrange
        var modelConfig = new ModelConfiguration
        {
            Name = "test-model",
            Type = "openai-compatible",
            Provider = "TestProvider"
        };
        
        var diff = "diff --git a/test.cs b/test.cs\n+Added new feature";
        var rawMessage = "<think>Processing diff</think>Added authentication feature with JWT support";
        var expectedMessage = "Added authentication feature with JWT support";
        
        _provider.GenerateCommitMessageAsync(diff, null)
            .Returns(new CommitMessageResult 
            { 
                Message = rawMessage,
                InputTokens = 100,
                OutputTokens = 20,
                TotalTokens = 120
            });

        // Act
        var result = await _generator.GenerateAsync(modelConfig, diff);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be(expectedMessage);
        result.InputTokens.Should().Be(100);
        result.OutputTokens.Should().Be(20);
        result.TotalTokens.Should().Be(120);
    }

    [Fact]
    public async Task GenerateAsync_WithCustomInstruction_PassesItToProvider()
    {
        // Arrange
        var modelConfig = new ModelConfiguration
        {
            Name = "test-model",
            Type = "openai-compatible"
        };
        
        var diff = "diff content";
        var customInstruction = "Make it a haiku";
        
        _provider.GenerateCommitMessageAsync(diff, customInstruction)
            .Returns(new CommitMessageResult { Message = "Code changes made\nRefactoring complete now\nTests are passing green" });

        // Act
        await _generator.GenerateAsync(modelConfig, diff, customInstruction);

        // Assert
        await _provider.Received(1).GenerateCommitMessageAsync(diff, customInstruction);
    }

    [Fact]
    public async Task GenerateAsync_WithEmptyDiff_ThrowsArgumentException()
    {
        // Arrange
        var modelConfig = new ModelConfiguration();

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _generator.GenerateAsync(modelConfig, ""));
        
        await Assert.ThrowsAsync<ArgumentException>(() => 
            _generator.GenerateAsync(modelConfig, null!));
    }

    [Fact]
    public async Task GenerateAsync_WhenProviderReturnsEmpty_UsesFallbackMessage()
    {
        // Arrange
        var modelConfig = new ModelConfiguration
        {
            Name = "test-model",
            Type = "openai-compatible"
        };
        
        var diff = "diff content";
        
        _provider.GenerateCommitMessageAsync(diff, null)
            .Returns(new CommitMessageResult { Message = "" });

        // Act
        var result = await _generator.GenerateAsync(modelConfig, diff);

        // Assert
        result.Message.Should().Be("Automated commit of code changes.");
    }

    [Fact]
    public async Task GenerateAsync_WithAuthenticationError_Rethrows()
    {
        // Arrange
        var modelConfig = new ModelConfiguration
        {
            Name = "test-model",
            Type = "openai-compatible"
        };
        
        var diff = "diff content";
        
        _provider.GenerateCommitMessageAsync(diff, null)
            .Throws(new AuthenticationException("Invalid API key"));

        // Act & Assert
        await Assert.ThrowsAsync<AuthenticationException>(() => 
            _generator.GenerateAsync(modelConfig, diff));
    }

    [Fact]
    public async Task GenerateAsync_LogsModelInformation()
    {
        // Arrange
        var modelConfig = new ModelConfiguration
        {
            Name = "gpt-4-turbo",
            ModelId = "gpt-4-turbo-preview",
            Provider = "OpenAI",
            Type = "openai-compatible"
        };
        
        var diff = "diff content";
        
        _provider.GenerateCommitMessageAsync(diff, null)
            .Returns(new CommitMessageResult { Message = "Test commit" });

        // Act
        await _generator.GenerateAsync(modelConfig, diff);

        // Assert
        Logger.Received().Information(
            Arg.Is<string>(s => s.Contains("Using")),
            Arg.Any<object[]>());
    }

    [Fact]
    public async Task GenerateAsync_WithUnknownProviderType_ThrowsNotSupportedException()
    {
        // Arrange
        var modelConfig = new ModelConfiguration
        {
            Name = "test-model",
            Type = "unknown-type"
        };
        
        var diff = "diff content";
        
        _providerFactory.CreateProvider(Arg.Any<ModelConfiguration>())
            .Throws(new NotSupportedException("API type 'unknown-type' is not supported"));

        // Act & Assert
        await Assert.ThrowsAsync<NotSupportedException>(() => 
            _generator.GenerateAsync(modelConfig, diff));
    }
}