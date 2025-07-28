using System.Net;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using GitGen.Configuration;
using GitGen.Exceptions;
using GitGen.Providers.OpenAI;
using GitGen.Services;
using NSubstitute;
using Xunit;

namespace GitGen.Tests.Providers.OpenAI;

public class OpenAIProviderNetworkTests : TestBase
{
    private readonly IHttpClientService _httpClient;
    private readonly ModelConfiguration _modelConfig;
    private readonly OpenAIProvider _provider;

    public OpenAIProviderNetworkTests()
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
    public async Task GenerateCommitMessageAsync_WithParameterMismatch_SelfHeals()
    {
        // Arrange
        var diff = "diff content";
        
        // First call fails with parameter error
        var parameterErrorException = new HttpResponseException(
            HttpStatusCode.BadRequest,
            "https://api.test.com",
            "POST",
            "{\"error\":{\"message\":\"Unsupported parameter: max_completion_tokens\"}}",
            null,
            "during commit generation");
        
        // Second call (parameter detection) succeeds
        var detectionResponse = CreateMockHttpResponse(new OpenAIResponse
        {
            Choices = new[] { new Choice { Message = new Message { Content = "test" } } }
        });
        
        // Third call (retry with corrected parameters) succeeds
        var successResponse = CreateMockHttpResponse(new OpenAIResponse
        {
            Choices = new[] { new Choice { Message = new Message { Content = "Fixed bug" } } }
        });

        _httpClient.SendAsync(
                Arg.Any<HttpRequestMessage>(), 
                Arg.Any<GitGen.Services.HttpRequestOptions>())
            .Returns(
                x => throw parameterErrorException,
                x => Task.FromResult(detectionResponse),
                x => Task.FromResult(successResponse));

        // Act
        var result = await _provider.GenerateCommitMessageAsync(diff);

        // Assert
        result.Message.Should().Be("Fixed bug");
        await _httpClient.Received(3).SendAsync(
            Arg.Any<HttpRequestMessage>(), 
            Arg.Any<GitGen.Services.HttpRequestOptions>());
    }

    [Fact]
    public async Task GenerateCommitMessageAsync_WithTemperatureError_SelfHeals()
    {
        // Arrange
        var diff = "diff content";
        
        // First call fails with temperature error
        var temperatureErrorException = new HttpResponseException(
            HttpStatusCode.BadRequest,
            "https://api.test.com",
            "POST",
            "{\"error\":{\"message\":\"Unsupported value for temperature\"}}",
            null,
            "during commit generation");
        
        // Parameter detection calls
        var detectionResponses = new[]
        {
            CreateMockHttpResponse(new OpenAIResponse
            {
                Choices = new[] { new Choice { Message = new Message { Content = "test" } } }
            }),
            CreateMockHttpResponse(new OpenAIResponse
            {
                Choices = new[] { new Choice { Message = new Message { Content = "test" } } }
            })
        };
        
        // Final successful call
        var successResponse = CreateMockHttpResponse(new OpenAIResponse
        {
            Choices = new[] { new Choice { Message = new Message { Content = "Updated docs" } } }
        });

        var callCount = 0;
        _httpClient.SendAsync(
                Arg.Any<HttpRequestMessage>(), 
                Arg.Any<GitGen.Services.HttpRequestOptions>())
            .Returns(x =>
            {
                callCount++;
                return callCount switch
                {
                    1 => throw temperatureErrorException,
                    2 => Task.FromResult(detectionResponses[0]),
                    3 => Task.FromResult(detectionResponses[1]),
                    _ => Task.FromResult(successResponse)
                };
            });

        // Act
        var result = await _provider.GenerateCommitMessageAsync(diff);

        // Assert
        result.Message.Should().Be("Updated docs");
        _modelConfig.Temperature.Should().Be(0.2); // Should be updated after self-healing
    }

    [Fact]
    public async Task SendRequestAsync_WithAzureEndpoint_UsesCorrectHeaders()
    {
        // Arrange
        _modelConfig.Url = "https://myresource.openai.azure.com/openai/deployments/gpt-4";
        var provider = new OpenAIProvider(_httpClient, Logger, _modelConfig);
        
        HttpRequestMessage? capturedRequest = null;
        _httpClient.SendAsync(
                Arg.Do<HttpRequestMessage>(req => capturedRequest = req),
                Arg.Any<GitGen.Services.HttpRequestOptions>())
            .Returns(Task.FromResult(CreateMockHttpResponse(new OpenAIResponse
            {
                Choices = new[] { new Choice { Message = new Message { Content = "Test" } } }
            })));

        // Act
        await provider.GenerateAsync("test prompt");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Should().ContainKey("api-key");
        capturedRequest.Headers.GetValues("api-key").Should().Contain(_modelConfig.ApiKey);
        capturedRequest.Headers.Authorization.Should().BeNull();
    }

    [Fact]
    public async Task SendRequestAsync_WithStandardEndpoint_UsesBearerAuth()
    {
        // Arrange
        HttpRequestMessage? capturedRequest = null;
        _httpClient.SendAsync(
                Arg.Do<HttpRequestMessage>(req => capturedRequest = req),
                Arg.Any<GitGen.Services.HttpRequestOptions>())
            .Returns(Task.FromResult(CreateMockHttpResponse(new OpenAIResponse
            {
                Choices = new[] { new Choice { Message = new Message { Content = "Test" } } }
            })));

        // Act
        await _provider.GenerateAsync("test prompt");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization.Should().NotBeNull();
        capturedRequest.Headers.Authorization!.Scheme.Should().Be("Bearer");
        capturedRequest.Headers.Authorization.Parameter.Should().Be(_modelConfig.ApiKey);
    }

    [Fact]
    public async Task SendRequestAsync_WithoutAuth_NoAuthHeaders()
    {
        // Arrange
        _modelConfig.RequiresAuth = false;
        _modelConfig.ApiKey = "";
        var provider = new OpenAIProvider(_httpClient, Logger, _modelConfig);
        
        HttpRequestMessage? capturedRequest = null;
        _httpClient.SendAsync(
                Arg.Do<HttpRequestMessage>(req => capturedRequest = req),
                Arg.Any<GitGen.Services.HttpRequestOptions>())
            .Returns(Task.FromResult(CreateMockHttpResponse(new OpenAIResponse
            {
                Choices = new[] { new Choice { Message = new Message { Content = "Test" } } }
            })));

        // Act
        await provider.GenerateAsync("test prompt");

        // Assert
        capturedRequest.Should().NotBeNull();
        capturedRequest!.Headers.Authorization.Should().BeNull();
        capturedRequest.Headers.Should().NotContainKey("api-key");
    }

    private HttpResponseMessage CreateMockHttpResponse(OpenAIResponse response)
    {
        var json = JsonSerializer.Serialize(response, OpenAIJsonContext.Default.OpenAIResponse);
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }
}