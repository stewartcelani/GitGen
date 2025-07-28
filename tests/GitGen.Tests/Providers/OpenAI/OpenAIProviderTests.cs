using System.Net;
using System.Text.Json;
using FluentAssertions;
using GitGen.Configuration;
using GitGen.Exceptions;
using GitGen.Providers.OpenAI;
using GitGen.Services;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using Xunit;

namespace GitGen.Tests.Providers.OpenAI;

public class OpenAIProviderTests : TestBase
{
    private readonly IHttpClientService _httpClient;
    private readonly ModelConfiguration _modelConfig;
    private readonly OpenAIProvider _provider;

    public OpenAIProviderTests()
    {
        _httpClient = Substitute.For<IHttpClientService>();
        _modelConfig = new ModelConfiguration
        {
            Id = "test-id",
            Name = "test-model",
            Type = "openai-compatible",
            Provider = "TestProvider",
            Url = "https://api.test.com/v1/chat/completions",
            ModelId = "test-model-id",
            ApiKey = "test-api-key",
            RequiresAuth = true,
            UseLegacyMaxTokens = false,
            Temperature = 0.2,
            MaxOutputTokens = 1000
        };
        
        _provider = new OpenAIProvider(_httpClient, Logger, _modelConfig);
    }

    [Fact]
    public async Task GenerateCommitMessageAsync_WithValidResponse_ReturnsCleanedMessage()
    {
        // Arrange
        var diff = "diff --git a/test.cs b/test.cs\n+Added new feature";
        var expectedMessage = "Added new feature with improved error handling";
        
        var mockResponse = CreateMockHttpResponse(new OpenAIResponse
        {
            Choices = new[]
            {
                new Choice 
                { 
                    Message = new Message 
                    { 
                        Role = "assistant", 
                        Content = $"<think>Analyzing diff</think>{expectedMessage}" 
                    }
                }
            },
            Usage = new Usage
            {
                PromptTokens = 100,
                CompletionTokens = 20,
                TotalTokens = 120
            }
        });

        _httpClient.SendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<GitGen.Services.HttpRequestOptions>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await _provider.GenerateCommitMessageAsync(diff);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be(expectedMessage);
        result.InputTokens.Should().Be(100);
        result.OutputTokens.Should().Be(20);
        result.TotalTokens.Should().Be(120);
    }

    [Fact]
    public async Task GenerateCommitMessageAsync_WithCustomInstruction_IncludesInPrompt()
    {
        // Arrange
        var diff = "diff --git a/test.cs b/test.cs";
        var customInstruction = "Make it a haiku";
        HttpRequestMessage? capturedRequest = null;

        _httpClient.SendAsync(
                Arg.Do<HttpRequestMessage>(req => capturedRequest = req),
                Arg.Any<GitGen.Services.HttpRequestOptions>())
            .Returns(Task.FromResult(CreateMockHttpResponse(new OpenAIResponse
            {
                Choices = new[] { new Choice { Message = new Message { Content = "Test response" } } }
            })));

        // Act
        await _provider.GenerateCommitMessageAsync(diff, customInstruction);

        // Assert
        capturedRequest.Should().NotBeNull();
        var content = await capturedRequest!.Content!.ReadAsStringAsync();
        content.Should().Contain(customInstruction.ToUpper());
    }

    [Fact]
    public async Task GenerateCommitMessageAsync_WithEmptyResponse_ReturnsFallbackMessage()
    {
        // Arrange
        var diff = "diff --git a/test.cs b/test.cs";
        
        var mockResponse = CreateMockHttpResponse(new OpenAIResponse
        {
            Choices = new[] { new Choice { Message = new Message { Content = "" } } }
        });

        _httpClient.SendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<GitGen.Services.HttpRequestOptions>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await _provider.GenerateCommitMessageAsync(diff);

        // Assert
        result.Message.Should().Be("Automated commit of code changes.");
    }

    [Fact]
    public async Task GenerateCommitMessageAsync_WithAuthenticationError_ThrowsAuthenticationException()
    {
        // Arrange
        var diff = "diff --git a/test.cs b/test.cs";
        
        _httpClient.SendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<GitGen.Services.HttpRequestOptions>())
            .Throws(new HttpResponseException(
                HttpStatusCode.Unauthorized,
                "https://api.test.com",
                "POST",
                "Unauthorized",
                null,
                "Authentication failed"));

        // Act
        var act = () => _provider.GenerateCommitMessageAsync(diff);

        // Assert
        await act.Should().ThrowAsync<AuthenticationException>()
            .WithMessage("Authentication failed*");
    }

    [Fact]
    public async Task TestConnectionAndDetectParametersAsync_WithValidConnection_ReturnsSuccess()
    {
        // Arrange
        var mockResponse = CreateMockHttpResponse(new OpenAIResponse
        {
            Choices = new[] { new Choice { Message = new Message { Content = "Test" } } }
        });

        _httpClient.SendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<GitGen.Services.HttpRequestOptions>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var (success, useLegacyTokens, temperature) = await _provider.TestConnectionAndDetectParametersAsync();

        // Assert
        success.Should().BeTrue();
        useLegacyTokens.Should().BeFalse();
        temperature.Should().Be(0.2);
    }

    [Fact]
    public async Task GenerateAsync_WithValidPrompt_ReturnsCleanedResponse()
    {
        // Arrange
        var prompt = "Test prompt";
        var expectedResponse = "Test response";
        
        var mockResponse = CreateMockHttpResponse(new OpenAIResponse
        {
            Choices = new[]
            {
                new Choice 
                { 
                    Message = new Message 
                    { 
                        Role = "assistant", 
                        Content = expectedResponse 
                    }
                }
            }
        });

        _httpClient.SendAsync(Arg.Any<HttpRequestMessage>(), Arg.Any<GitGen.Services.HttpRequestOptions>())
            .Returns(Task.FromResult(mockResponse));

        // Act
        var result = await _provider.GenerateAsync(prompt);

        // Assert
        result.Should().NotBeNull();
        result.Message.Should().Be(expectedResponse);
    }

    private HttpResponseMessage CreateMockHttpResponse(OpenAIResponse response)
    {
        var json = JsonSerializer.Serialize(response, OpenAIJsonContext.Default.OpenAIResponse);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
        };
    }
}