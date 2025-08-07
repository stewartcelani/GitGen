using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using GitGen.Configuration;
using GitGen.Exceptions;
using GitGen.Providers.OpenAI;
using GitGen.Services;
using Moq;
using Xunit;

namespace GitGen.Tests.Providers.OpenAI;

public class OpenAIParameterDetectorTests
{
    private readonly Mock<IHttpClientService> _mockHttpClient;
    private readonly Mock<IConsoleLogger> _mockLogger;
    private readonly Mock<ILlmCallTracker> _mockCallTracker;
    private readonly OpenAIParameterDetector _detector;

    public OpenAIParameterDetectorTests()
    {
        _mockHttpClient = new Mock<IHttpClientService>();
        _mockLogger = new Mock<IConsoleLogger>();
        _mockCallTracker = new Mock<ILlmCallTracker>();
        
        _detector = new OpenAIParameterDetector(
            _mockHttpClient.Object,
            _mockLogger.Object,
            "https://api.openai.com/v1",
            "test-api-key",
            requiresAuth: true,
            _mockCallTracker.Object,
            null);
    }

    #region DetectParametersAsync Tests

    [Fact]
    public async Task DetectParametersAsync_WithMaxCompletionTokens_UsesModernFormat()
    {
        // Arrange
        var modelId = "gpt-4";
        var responseContent = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new { message = new { content = "Test response" } }
            },
            usage = new { prompt_tokens = 10, completion_tokens = 20, total_tokens = 30 }
        });

        _mockHttpClient.Setup(x => x.SendAsync(
            It.IsAny<HttpRequestMessage>(),
            It.IsAny<GitGen.Services.HttpRequestOptions>()))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(responseContent, Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _detector.DetectParametersAsync(modelId);

        // Assert
        result.Should().NotBeNull();
        result.Model.Should().Be(modelId);
        result.UseLegacyMaxTokens.Should().BeFalse();
        _mockLogger.Verify(x => x.Debug(It.Is<string>(s => s.Contains("max_completion_tokens"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task DetectParametersAsync_WithMaxTokensOnly_UsesLegacyFormat()
    {
        // Arrange
        var modelId = "older-model";
        _mockHttpClient.SetupSequence(x => x.SendAsync(
            It.IsAny<HttpRequestMessage>(),
            It.IsAny<GitGen.Services.HttpRequestOptions>()))
            .ThrowsAsync(new HttpResponseException(
                HttpStatusCode.BadRequest,
                "https://api.openai.com/v1/chat/completions",
                "POST",
                "{\"error\":{\"type\":\"invalid_request_error\",\"message\":\"Invalid parameter: max_completion_tokens\"}}",
                "{\"error\":{\"type\":\"invalid_request_error\",\"message\":\"Invalid parameter: max_completion_tokens\"}}"
            ))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    choices = new[] { new { message = new { content = "Test" } } },
                    usage = new { prompt_tokens = 10, completion_tokens = 20, total_tokens = 30 }
                }), Encoding.UTF8, "application/json")
            });

        // Act
        var result = await _detector.DetectParametersAsync(modelId);

        // Assert
        result.Should().NotBeNull();
        result.Model.Should().Be(modelId);
        result.UseLegacyMaxTokens.Should().BeTrue();
        _mockLogger.Verify(x => x.Debug(It.Is<string>(s => s.Contains("max_tokens"))), Times.AtLeastOnce);
    }

    [Fact]
    public async Task DetectParametersAsync_WithAuthenticationError_ThrowsInvalidOperationException()
    {
        // Arrange
        _mockHttpClient.Setup(x => x.SendAsync(
            It.IsAny<HttpRequestMessage>(),
            It.IsAny<GitGen.Services.HttpRequestOptions>()))
            .ThrowsAsync(new HttpResponseException(
                HttpStatusCode.Unauthorized,
                "https://api.openai.com/v1/chat/completions",
                "POST",
                "{\"error\":{\"message\":\"Invalid API key\"}}",
                "{\"error\":{\"message\":\"Invalid API key\"}}"
            ));

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _detector.DetectParametersAsync("gpt-4"));
    }

    [Fact]
    public async Task DetectParametersAsync_WithContextLengthError_HandlesDifferentFormats()
    {
        // Arrange
        var contextErrorResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.BadRequest,
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                error = new { message = "maximum context length is 4096 tokens" }
            }), Encoding.UTF8, "application/json")
        };

        var successResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                choices = new[] { new { message = new { content = "Test" } } }
            }), Encoding.UTF8, "application/json")
        };

        _mockHttpClient.SetupSequence(x => x.SendAsync(
            It.IsAny<HttpRequestMessage>(),
            It.IsAny<GitGen.Services.HttpRequestOptions>()))
            .ReturnsAsync(contextErrorResponse)
            .ReturnsAsync(successResponse);

        // Act
        var result = await _detector.DetectParametersAsync("test-model");

        // Assert
        result.Should().NotBeNull();
        _mockHttpClient.Verify(x => x.SendAsync(It.IsAny<HttpRequestMessage>(), It.IsAny<GitGen.Services.HttpRequestOptions>()), Times.Exactly(2));
    }

    [Fact]
    public async Task DetectParametersAsync_WithTemperatureWarning_AdjustsTemperature()
    {
        // Arrange
        var warningResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.BadRequest,
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                error = new { message = "temperature must be between 0 and 1" }
            }), Encoding.UTF8, "application/json")
        };

        var successResponse = new HttpResponseMessage
        {
            StatusCode = HttpStatusCode.OK,
            Content = new StringContent(JsonSerializer.Serialize(new
            {
                choices = new[] { new { message = new { content = "Test" } } }
            }), Encoding.UTF8, "application/json")
        };

        _mockHttpClient.SetupSequence(x => x.SendAsync(
            It.IsAny<HttpRequestMessage>(),
            It.IsAny<GitGen.Services.HttpRequestOptions>()))
            .ReturnsAsync(warningResponse)
            .ReturnsAsync(successResponse);

        // Act
        var result = await _detector.DetectParametersAsync("test-model");

        // Assert
        result.Should().NotBeNull();
        result.Temperature.Should().BeLessThanOrEqualTo(1.0);
        _mockLogger.Verify(x => x.Debug(It.Is<string>(s => s.Contains("temperature")), It.IsAny<object[]>()), Times.AtLeastOnce);
    }

    #endregion

    // Note: SendRequestAsync is a private method and cannot be tested directly.
    // The public API is tested through DetectParametersAsync and ValidateConnectionAsync tests.

    #region Constructor Tests

    [Fact]
    public void Constructor_WithAllParameters_InitializesCorrectly()
    {
        // Arrange & Act
        var detector = new OpenAIParameterDetector(
            _mockHttpClient.Object,
            _mockLogger.Object,
            "https://api.example.com",
            "api-key",
            requiresAuth: true,
            _mockCallTracker.Object,
            new ModelConfiguration { Name = "test-model" });

        // Assert
        detector.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithoutOptionalParameters_InitializesCorrectly()
    {
        // Arrange & Act
        var detector = new OpenAIParameterDetector(
            _mockHttpClient.Object,
            _mockLogger.Object,
            "https://api.example.com",
            null,
            requiresAuth: false);

        // Assert
        detector.Should().NotBeNull();
    }

    #endregion

    #region Helper Method Tests

    [Fact]
    public async Task DetectParametersAsync_WithLocalLLM_SkipsAuthentication()
    {
        // Arrange
        var detector = new OpenAIParameterDetector(
            _mockHttpClient.Object,
            _mockLogger.Object,
            "http://localhost:11434",
            null,
            requiresAuth: false);

        _mockHttpClient.Setup(x => x.SendAsync(
            It.IsAny<HttpRequestMessage>(),
            It.IsAny<GitGen.Services.HttpRequestOptions>()))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    choices = new[] { new { message = new { content = "Test" } } }
                }), Encoding.UTF8, "application/json")
            });

        // Act
        var result = await detector.DetectParametersAsync("local-model");

        // Assert
        result.Should().NotBeNull();
        _mockHttpClient.Verify(x => x.SendAsync(
            It.Is<HttpRequestMessage>(req => !req.Headers.Contains("Authorization")),
            It.IsAny<GitGen.Services.HttpRequestOptions>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task DetectParametersAsync_LogsDebugInformation()
    {
        // Arrange
        _mockHttpClient.Setup(x => x.SendAsync(
            It.IsAny<HttpRequestMessage>(),
            It.IsAny<GitGen.Services.HttpRequestOptions>()))
            .ReturnsAsync(new HttpResponseMessage
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(JsonSerializer.Serialize(new
                {
                    choices = new[] { new { message = new { content = "Test" } } }
                }), Encoding.UTF8, "application/json")
            });

        // Act
        await _detector.DetectParametersAsync("test-model");

        // Assert
        _mockLogger.Verify(x => x.Debug(It.IsAny<string>()), Times.AtLeastOnce);
        _mockLogger.Verify(x => x.Debug(It.Is<string>(s => s.Contains("Starting API parameter detection")), It.IsAny<object[]>()), Times.Once);
    }

    #endregion
}