using System.Net;
using System.Text;
using Polly;

namespace GitGen.Services;

/// <summary>
///     HTTP client service with built-in retry logic and request cloning for reliable API communication.
///     Handles rate limiting, transient failures, and provides comprehensive error handling.
/// </summary>
public class HttpClientService
{
    private readonly HttpClient _httpClient;
    private readonly IConsoleLogger _logger;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;

    /// <summary>
    ///     Initializes a new instance of the <see cref="HttpClientService" /> class.
    /// </summary>
    /// <param name="logger">The console logger for debugging and error reporting.</param>
    public HttpClientService(IConsoleLogger logger)
    {
        _logger = logger;
        _httpClient = new HttpClient();
        _retryPolicy = CreateRetryPolicy();
    }

    private IAsyncPolicy<HttpResponseMessage> CreateRetryPolicy()
    {
        var httpStatusCodesToRetry = new[]
        {
            HttpStatusCode.RequestTimeout,
            HttpStatusCode.InternalServerError,
            HttpStatusCode.BadGateway,
            HttpStatusCode.ServiceUnavailable,
            HttpStatusCode.GatewayTimeout,
            HttpStatusCode.TooManyRequests
        };

        return Policy
            .HandleResult<HttpResponseMessage>(r => httpStatusCodesToRetry.Contains(r.StatusCode))
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                3,
                (retryAttempt, response, context) =>
                {
                    if (response.Result?.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        if (response.Result.Headers.RetryAfter != null)
                        {
                            var retryAfter = response.Result.Headers.RetryAfter;
                            if (retryAfter.Delta.HasValue)
                            {
                                _logger.Warning(
                                    "Rate limited. Waiting {Seconds} seconds as specified by Retry-After header",
                                    retryAfter.Delta.Value.TotalSeconds);
                                return retryAfter.Delta.Value;
                            }
                        }

                        var delay = TimeSpan.FromSeconds(Math.Pow(2, retryAttempt) * 2);
                        _logger.Warning("Rate limited. Waiting {Seconds} seconds before retry", delay.TotalSeconds);
                        return delay;
                    }

                    var standardDelay = TimeSpan.FromSeconds(retryAttempt);
                    _logger.Warning("Request failed. Waiting {Seconds} seconds before retry attempt {Attempt}",
                        standardDelay.TotalSeconds, retryAttempt);
                    return standardDelay;
                },
                (outcome, timespan, retryCount, context) =>
                {
                    var statusCode = outcome.Result?.StatusCode;
                    _logger.Warning("Retry {RetryCount} after {Timespan}s delay. Status: {StatusCode}",
                        retryCount, timespan.TotalSeconds, statusCode?.ToString() ?? "Exception");
                    return Task.CompletedTask;
                });
    }

    /// <summary>
    ///     Sends an HTTP request with automatic retry logic for transient failures and rate limiting.
    ///     Clones the request for each retry attempt to ensure request state is preserved.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <returns>The HTTP response message from the server.</returns>
    /// <exception cref="HttpRequestException">Thrown when the request fails after all retry attempts.</exception>
    /// <exception cref="TaskCanceledException">Thrown when the request times out.</exception>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var clonedRequest = await CloneHttpRequestMessageAsync(request);
            return await _httpClient.SendAsync(clonedRequest);
        });
    }

    private static async Task<HttpRequestMessage> CloneHttpRequestMessageAsync(HttpRequestMessage req)
    {
        var clone = new HttpRequestMessage(req.Method, req.RequestUri)
        {
            Version = req.Version
        };

        if (req.Content != null)
        {
            var content = await req.Content.ReadAsStringAsync();
            clone.Content = new StringContent(content, Encoding.UTF8,
                req.Content.Headers.ContentType?.MediaType ?? "application/json");
        }

        foreach (var header in req.Headers) clone.Headers.TryAddWithoutValidation(header.Key, header.Value);

        return clone;
    }
}