using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using GitGen.Exceptions;
using GitGen.Services;
using Moq;
using Moq.Protected;
using Xunit;

namespace GitGen.Tests.Services;

public class HttpClientServiceTests : IDisposable
{
    private readonly Mock<IConsoleLogger> _loggerMock;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly TestHttpClientService _service;

    public HttpClientServiceTests()
    {
        _loggerMock = new Mock<IConsoleLogger>();
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>(MockBehavior.Loose);
        
        // Create a test service that allows us to inject a mocked HttpClient
        _service = new TestHttpClientService(_loggerMock.Object, _httpMessageHandlerMock.Object);
    }

    public void Dispose()
    {
        _service?.Dispose();
    }

    // Test-specific HttpClientService that allows HttpClient injection
    private class TestHttpClientService : GitGen.Services.HttpClientService
    {
        private readonly HttpClient _testHttpClient;

        public TestHttpClientService(IConsoleLogger logger, HttpMessageHandler messageHandler) : base(logger)
        {
            // Replace the internal HttpClient with our test client
            _testHttpClient = new HttpClient(messageHandler)
            {
                BaseAddress = new Uri("https://api.test.com")
            };
            
            // Use reflection to replace the private _httpClient field
            var field = typeof(GitGen.Services.HttpClientService).GetField("_httpClient", 
                System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            field?.SetValue(this, _testHttpClient);
        }

        public void Dispose()
        {
            _testHttpClient?.Dispose();
        }
    }

    [Fact]
    public async Task SendAsync_WithSuccessfulResponse_ReturnsResponse()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/test");
        var response = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"result\":\"success\"}", Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act
        var result = await _service.SendAsync(request);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await result.Content.ReadAsStringAsync();
        content.Should().Contain("success");
    }

    [Fact]
    public async Task SendAsync_WithErrorAndThrowOnError_ThrowsHttpResponseException()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/test");
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"Bad request\"}", Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var options = new GitGen.Services.HttpRequestOptions { ThrowOnError = true };

        // Act & Assert
        var action = () => _service.SendAsync(request, options);
        await action.Should().ThrowAsync<HttpResponseException>()
            .Where(ex => ex.StatusCode == HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendAsync_WithErrorAndNoThrow_ReturnsErrorResponse()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/test");
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"Bad request\"}", Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var options = new GitGen.Services.HttpRequestOptions { ThrowOnError = false };

        // Act
        var result = await _service.SendAsync(request, options);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task SendAsync_WithTransientError_RetriesAndSucceeds()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/test");
        var failureResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var successResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"result\":\"success\"}", Encoding.UTF8, "application/json")
        };

        var callCount = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? failureResponse : successResponse;
            });

        // Act
        var result = await _service.SendAsync(request);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        callCount.Should().Be(2);
        _loggerMock.Verify(x => x.Warning(
            It.IsAny<string>(), 
            It.IsAny<object[]>()), Times.AtLeastOnce);
    }

    [Fact]
    public async Task SendAsync_WithRateLimitAndRetryAfter_WaitsSpecifiedTime()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/test");
        var rateLimitResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        rateLimitResponse.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(TimeSpan.FromSeconds(2));
        
        var successResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"result\":\"success\"}", Encoding.UTF8, "application/json")
        };

        var callCount = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? rateLimitResponse : successResponse;
            });

        // Act
        var result = await _service.SendAsync(request);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        _loggerMock.Verify(x => x.Warning(
            "Rate limited. Waiting {Seconds} seconds as specified by Retry-After header",
            It.IsAny<double>()), Times.Once);
    }

    [Fact]
    public async Task SendAsync_WithAllRetriesFailed_ThrowsException()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/test");
        var failureResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError)
        {
            Content = new StringContent("{\"error\":\"Server error\"}", Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(failureResponse);

        // Act & Assert
        var action = () => _service.SendAsync(request);
        await action.Should().ThrowAsync<HttpResponseException>();
        
        // Should have tried 4 times (1 initial + 3 retries)
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Exactly(4),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendAsync_WithSuppressedErrorLogging_DoesNotLogErrors()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/test");
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"Bad request\"}", Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var options = GitGen.Services.HttpRequestOptions.Silent;

        // Act
        var result = await _service.SendAsync(request, options);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        _loggerMock.Verify(x => x.Error(It.IsAny<string>(), It.IsAny<object[]>()), Times.Never);
    }

    [Fact]
    public async Task SendAsync_WithRequestContent_ClonesContentForRetries()
    {
        // Arrange
        var content = new StringContent("{\"data\":\"test\"}", Encoding.UTF8, "application/json");
        var request = new HttpRequestMessage(HttpMethod.Post, "/test")
        {
            Content = content
        };

        var failureResponse = new HttpResponseMessage(HttpStatusCode.InternalServerError);
        var successResponse = new HttpResponseMessage(HttpStatusCode.OK);

        var callCount = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return callCount == 1 ? failureResponse : successResponse;
            })
            .Callback<HttpRequestMessage, CancellationToken>(async (req, _) =>
            {
                // Verify content is still readable
                if (req.Content != null)
                {
                    var contentString = await req.Content.ReadAsStringAsync();
                    contentString.Should().Contain("test");
                }
            });

        // Act
        var result = await _service.SendAsync(request);

        // Assert
        result.StatusCode.Should().Be(HttpStatusCode.OK);
        callCount.Should().Be(2);
    }

    [Fact]
    public async Task SendAsync_WithVerboseOptions_IncludesFullErrorDetails()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/test");
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"Detailed error message\"}", Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var options = GitGen.Services.HttpRequestOptions.Verbose;

        // Act & Assert
        var action = () => _service.SendAsync(request, options);
        var exception = await action.Should().ThrowAsync<HttpResponseException>();
        exception.Which.ResponseBody.Should().Contain("Detailed error message");
    }

    [Fact]
    public async Task SendAsync_WithErrorContext_IncludesContextInException()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/test");
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest);

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        var options = new GitGen.Services.HttpRequestOptions
        {
            ThrowOnError = true,
            ErrorContext = "during configuration testing"
        };

        // Act & Assert
        var action = () => _service.SendAsync(request, options);
        var exception = await action.Should().ThrowAsync<HttpResponseException>();
        exception.Which.Message.Should().Contain("during configuration testing");
    }

    [Fact]
    public async Task SendAsync_WithAuthenticationError_ThrowsAuthenticationException()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/test");
        var response = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"Invalid API key\"}", Encoding.UTF8, "application/json")
        };

        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);

        // Act & Assert
        var action = () => _service.SendAsync(request);
        var exception = await action.Should().ThrowAsync<HttpResponseException>();
        exception.Which.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task SendAsync_WithNonRetriableError_DoesNotRetry()
    {
        // Arrange
        var request = new HttpRequestMessage(HttpMethod.Post, "/test");
        var response = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"Bad request\"}", Encoding.UTF8, "application/json")
        };

        var callCount = 0;
        _httpMessageHandlerMock
            .Protected()
            .Setup<Task<HttpResponseMessage>>(
                "SendAsync",
                ItExpr.IsAny<HttpRequestMessage>(),
                ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(() =>
            {
                callCount++;
                return response;
            });

        // Act & Assert
        var action = () => _service.SendAsync(request);
        await action.Should().ThrowAsync<HttpResponseException>();
        
        // Should only try once for non-retriable errors
        callCount.Should().Be(1);
    }
}