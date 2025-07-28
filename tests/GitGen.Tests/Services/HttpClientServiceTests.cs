using System.Net;
using System.Text;
using FluentAssertions;
using GitGen.Exceptions;
using GitGen.Services;
using NSubstitute;
using Xunit;

namespace GitGen.Tests.Services;

public class HttpClientServiceTests : TestBase
{
    private readonly HttpClientService _service;
    private readonly HttpClient _httpClient;
    private readonly TestHttpMessageHandler _messageHandler;

    public HttpClientServiceTests()
    {
        _messageHandler = new TestHttpMessageHandler();
        _httpClient = new HttpClient(_messageHandler);
        _service = new HttpClientService(Logger);
        
        // Use reflection to inject our test HttpClient
        var field = typeof(HttpClientService).GetField("_httpClient", 
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        field?.SetValue(_service, _httpClient);
    }

    [Fact]
    public async Task SendAsync_WithSuccessfulResponse_ReturnsResponse()
    {
        // Arrange
        var expectedResponse = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("Success")
        };
        _messageHandler.SetupResponse(expectedResponse);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com");

        // Act
        var response = await _service.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var content = await response.Content.ReadAsStringAsync();
        content.Should().Be("Success");
    }

    [Fact]
    public async Task SendAsync_WithRetryableError_RetriesRequest()
    {
        // Arrange
        var failureResponse = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var successResponse = new HttpResponseMessage(HttpStatusCode.OK);
        
        _messageHandler.SetupResponses(failureResponse, successResponse);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com");

        // Act
        var response = await _service.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        _messageHandler.CallCount.Should().Be(2);
    }

    [Fact]
    public async Task SendAsync_WithRateLimitAndRetryAfter_WaitsCorrectTime()
    {
        // Arrange
        var rateLimitResponse = new HttpResponseMessage(HttpStatusCode.TooManyRequests);
        rateLimitResponse.Headers.RetryAfter = new System.Net.Http.Headers.RetryConditionHeaderValue(
            TimeSpan.FromSeconds(2));
        
        var successResponse = new HttpResponseMessage(HttpStatusCode.OK);
        
        _messageHandler.SetupResponses(rateLimitResponse, successResponse);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com");

        // Act
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        var response = await _service.SendAsync(request);
        stopwatch.Stop();

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        stopwatch.Elapsed.Should().BeGreaterThan(TimeSpan.FromSeconds(1.5));
    }

    [Fact]
    public async Task SendAsync_WithNonRetryableError_ThrowsException()
    {
        // Arrange
        var errorResponse = new HttpResponseMessage(HttpStatusCode.BadRequest)
        {
            Content = new StringContent("{\"error\":\"Invalid request\"}")
        };
        _messageHandler.SetupResponse(errorResponse);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com");

        // Act & Assert
        await Assert.ThrowsAsync<HttpResponseException>(
            () => _service.SendAsync(request));
    }

    [Fact]
    public async Task SendAsync_WithSuppressedErrorLogging_DoesNotLog()
    {
        // Arrange
        var errorResponse = new HttpResponseMessage(HttpStatusCode.BadRequest);
        _messageHandler.SetupResponse(errorResponse);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com");
        var options = GitGen.Services.HttpRequestOptions.Silent;

        // Act
        var response = await _service.SendAsync(request, options);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        Logger.DidNotReceive().Error(Arg.Any<string>(), Arg.Any<object[]>());
    }

    [Fact]
    public async Task SendAsync_WithoutThrowOnError_ReturnsErrorResponse()
    {
        // Arrange
        var errorResponse = new HttpResponseMessage(HttpStatusCode.NotFound);
        _messageHandler.SetupResponse(errorResponse);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com");
        var options = new GitGen.Services.HttpRequestOptions { ThrowOnError = false };

        // Act
        var response = await _service.SendAsync(request, options);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task SendAsync_WithAuthenticationError_ThrowsHttpResponseException()
    {
        // Arrange
        var errorResponse = new HttpResponseMessage(HttpStatusCode.Unauthorized)
        {
            Content = new StringContent("{\"error\":\"Invalid API key\"}")
        };
        _messageHandler.SetupResponse(errorResponse);
        
        var request = new HttpRequestMessage(HttpMethod.Get, "https://api.test.com");

        // Act & Assert
        var ex = await Assert.ThrowsAsync<HttpResponseException>(
            () => _service.SendAsync(request));
        
        ex.IsAuthenticationError.Should().BeTrue();
    }

    [Fact]
    public async Task SendAsync_ClonesRequestForRetries()
    {
        // Arrange
        var failureResponse = new HttpResponseMessage(HttpStatusCode.ServiceUnavailable);
        var successResponse = new HttpResponseMessage(HttpStatusCode.OK);
        
        _messageHandler.SetupResponses(failureResponse, successResponse);
        
        var request = new HttpRequestMessage(HttpMethod.Post, "https://api.test.com")
        {
            Content = new StringContent("Test content", Encoding.UTF8, "application/json")
        };
        request.Headers.Add("X-Custom-Header", "TestValue");

        // Act
        var response = await _service.SendAsync(request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        
        // Verify both requests had the same content and headers
        var firstRequest = _messageHandler.ReceivedRequests[0];
        var secondRequest = _messageHandler.ReceivedRequests[1];
        
        firstRequest.Method.Should().Be(secondRequest.Method);
        firstRequest.RequestUri.Should().Be(secondRequest.RequestUri);
        firstRequest.Headers.GetValues("X-Custom-Header").Should().Equal(
            secondRequest.Headers.GetValues("X-Custom-Header"));
    }

    private class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly Queue<HttpResponseMessage> _responses = new();
        public List<HttpRequestMessage> ReceivedRequests { get; } = new();
        public int CallCount => ReceivedRequests.Count;

        public void SetupResponse(HttpResponseMessage response)
        {
            _responses.Clear();
            _responses.Enqueue(response);
        }

        public void SetupResponses(params HttpResponseMessage[] responses)
        {
            _responses.Clear();
            foreach (var response in responses)
            {
                _responses.Enqueue(response);
            }
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            ReceivedRequests.Add(request);
            
            if (_responses.Count == 0)
            {
                // If no more responses, keep returning the last one
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.InternalServerError));
            }

            var response = _responses.Dequeue();
            
            // Re-enqueue if this is a retryable status
            if ((int)response.StatusCode >= 500 || response.StatusCode == HttpStatusCode.TooManyRequests)
            {
                _responses.Enqueue(response);
            }
            
            return Task.FromResult(response);
        }
    }
}