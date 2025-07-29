using System.Net;
using System.Text;
using System.Text.Json;
using GitGen.Exceptions;
using Polly;

namespace GitGen.Services;

/// <summary>
///     Configuration options for HTTP requests, allowing fine-grained control over error handling and logging.
/// </summary>
public class HttpRequestOptions
{
    /// <summary>
    ///     When true, suppresses all error logging for this request.
    ///     Useful for expected failures like parameter detection probes.
    /// </summary>
    public bool SuppressErrorLogging { get; set; } = false;

    /// <summary>
    ///     When true, throws an exception on HTTP error responses.
    ///     When false, returns the error response for manual handling.
    /// </summary>
    public bool ThrowOnError { get; set; } = true;

    /// <summary>
    ///     When true, includes the full response body in thrown exceptions.
    ///     Set to false for sensitive data or large responses.
    /// </summary>
    public bool IncludeResponseBodyInException { get; set; } = true;

    /// <summary>
    ///     Controls the level of detail in error logging.
    /// </summary>
    public ErrorDetailLevel DetailLevel { get; set; } = ErrorDetailLevel.Normal;

    /// <summary>
    ///     Optional context information for error messages.
    ///     Example: "during parameter detection" or "while testing connection"
    /// </summary>
    public string? ErrorContext { get; set; }

    /// <summary>
    ///     Creates options for silent error handling (no logging, no throwing).
    /// </summary>
    public static HttpRequestOptions Silent => new()
    {
        SuppressErrorLogging = true,
        ThrowOnError = false,
        DetailLevel = ErrorDetailLevel.Silent
    };

    /// <summary>
    ///     Creates options for verbose error handling (full details).
    /// </summary>
    public static HttpRequestOptions Verbose => new()
    {
        DetailLevel = ErrorDetailLevel.Verbose,
        IncludeResponseBodyInException = true
    };

    /// <summary>
    ///     Creates options for expected errors during probing (no logging, but throw).
    /// </summary>
    public static HttpRequestOptions ForProbing => new()
    {
        SuppressErrorLogging = true,
        ThrowOnError = true,
        DetailLevel = ErrorDetailLevel.Silent
    };

    /// <summary>
    ///     Creates options for configuration testing (show errors, throw exceptions).
    ///     Used during model configuration to ensure developers see error details.
    /// </summary>
    public static HttpRequestOptions ForConfigurationTesting => new()
    {
        SuppressErrorLogging = false,
        ThrowOnError = true,
        DetailLevel = ErrorDetailLevel.Verbose,
        IncludeResponseBodyInException = true,
        ErrorContext = "during configuration testing"
    };
}

/// <summary>
///     Specifies the level of detail to include in error messages.
/// </summary>
public enum ErrorDetailLevel
{
    /// <summary>
    ///     No error logging at all.
    /// </summary>
    Silent,

    /// <summary>
    ///     Minimal error information (status code only).
    /// </summary>
    Minimal,

    /// <summary>
    ///     Standard error information (status code, URL, basic message).
    /// </summary>
    Normal,

    /// <summary>
    ///     Full error details including response body and headers.
    /// </summary>
    Verbose
}

/// <summary>
///     HTTP client service with built-in retry logic and request cloning for reliable API communication.
///     Handles rate limiting, transient failures, and provides comprehensive error handling.
/// </summary>
public class HttpClientService : IHttpClientService
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
        return await SendAsync(request, null);
    }

    /// <summary>
    ///     Sends an HTTP request with automatic retry logic and configurable error handling.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="options">Options for controlling error handling and logging.</param>
    /// <returns>The HTTP response message from the server.</returns>
    /// <exception cref="HttpResponseException">Thrown when the request fails and ThrowOnError is true.</exception>
    /// <exception cref="TaskCanceledException">Thrown when the request times out.</exception>
    public async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpRequestOptions? options)
    {
        options ??= new HttpRequestOptions();
        var errorFormatter = new HttpErrorFormatter(_logger);

        return await _retryPolicy.ExecuteAsync(async () =>
        {
            var clonedRequest = await CloneHttpRequestMessageAsync(request);
            var response = await _httpClient.SendAsync(clonedRequest);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                
                // Log error if not suppressed
                if (!options.SuppressErrorLogging)
                {
                    errorFormatter.LogHttpError(response, responseBody, options);
                }

                // Throw exception if requested
                if (options.ThrowOnError)
                {
                    throw new HttpResponseException(
                        response.StatusCode,
                        clonedRequest.RequestUri?.ToString(),
                        clonedRequest.Method.ToString(),
                        responseBody,
                        options.IncludeResponseBodyInException ? responseBody : null,
                        options.ErrorContext);
                }
            }

            return response;
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

    /// <summary>
    ///     Private nested class for formatting HTTP error messages.
    /// </summary>
    private class HttpErrorFormatter
    {
        private readonly IConsoleLogger _logger;

        public HttpErrorFormatter(IConsoleLogger logger)
        {
            _logger = logger;
        }

        public void LogHttpError(HttpResponseMessage response, string responseBody, HttpRequestOptions options)
        {
            if (options.DetailLevel == ErrorDetailLevel.Silent)
                return;

            var url = response.RequestMessage?.RequestUri?.ToString() ?? "unknown URL";
            var method = response.RequestMessage?.Method?.ToString() ?? "unknown method";
            var statusCode = response.StatusCode;
            var statusCodeInt = (int)statusCode;

            // Format base error message
            var baseMessage = $"HTTP {statusCodeInt} {statusCode} from {url}";
            
            if (!string.IsNullOrEmpty(options.ErrorContext))
            {
                baseMessage += $" ({options.ErrorContext})";
            }

            // Log based on detail level
            switch (options.DetailLevel)
            {
                case ErrorDetailLevel.Minimal:
                    _logger.Error($"❌ {baseMessage}");
                    break;

                case ErrorDetailLevel.Normal:
                    _logger.Error($"❌ {baseMessage}");
                    var errorMessage = ExtractErrorMessage(responseBody);
                    if (!string.IsNullOrEmpty(errorMessage))
                    {
                        _logger.Error($"   Error: {errorMessage}");
                    }
                    break;

                case ErrorDetailLevel.Verbose:
                    _logger.Error($"❌ {baseMessage}");
                    _logger.Error($"   Method: {method}");
                    
                    var verboseErrorMessage = ExtractErrorMessage(responseBody);
                    if (!string.IsNullOrEmpty(verboseErrorMessage))
                    {
                        _logger.Error($"   Error: {verboseErrorMessage}");
                    }

                    // Extract request ID if present
                    var requestId = ExtractRequestId(response);
                    if (!string.IsNullOrEmpty(requestId))
                    {
                        _logger.Error($"   Request ID: {requestId}");
                    }

                    // Show full response body if not too large
                    if (!string.IsNullOrWhiteSpace(responseBody) && responseBody.Length < 5000)
                    {
                        _logger.Error("");
                        _logger.Error("   Response Body:");
                        FormatJsonResponse(responseBody);
                    }
                    break;
            }
        }

        private string? ExtractErrorMessage(string responseBody)
        {
            if (string.IsNullOrWhiteSpace(responseBody))
                return null;

            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var root = doc.RootElement;

                // Try common error message patterns
                // Pattern 1: { "error": { "message": "..." } }
                if (root.TryGetProperty("error", out var errorObj))
                {
                    if (errorObj.TryGetProperty("message", out var message))
                        return message.GetString();
                }

                // Pattern 2: { "error": "..." }
                if (root.TryGetProperty("error", out var errorString) && errorString.ValueKind == JsonValueKind.String)
                    return errorString.GetString();

                // Pattern 3: { "message": "..." }
                if (root.TryGetProperty("message", out var directMessage))
                    return directMessage.GetString();

                // Pattern 4: { "detail": "..." }
                if (root.TryGetProperty("detail", out var detail))
                    return detail.GetString();
            }
            catch
            {
                // If JSON parsing fails, check if it's plain text
                if (!responseBody.TrimStart().StartsWith("{") && !responseBody.TrimStart().StartsWith("["))
                {
                    // Return first line of plain text response
                    var firstLine = responseBody.Split('\n')[0].Trim();
                    if (firstLine.Length > 200)
                        firstLine = firstLine.Substring(0, 200) + "...";
                    return firstLine;
                }
            }

            return null;
        }

        private string? ExtractRequestId(HttpResponseMessage response)
        {
            // Check common headers for request ID
            var headers = new[] { "x-request-id", "request-id", "x-amzn-requestid", "x-ms-request-id" };
            
            foreach (var header in headers)
            {
                if (response.Headers.TryGetValues(header, out var values))
                {
                    return values.FirstOrDefault();
                }
            }

            return null;
        }

        private void FormatJsonResponse(string responseBody)
        {
            try
            {
                using var doc = JsonDocument.Parse(responseBody);
                var formatted = JsonSerializer.Serialize(doc, new JsonSerializerOptions 
                { 
                    WriteIndented = true 
                });
                
                foreach (var line in formatted.Split('\n'))
                {
                    _logger.Error($"   {line}");
                }
            }
            catch
            {
                // If not valid JSON, show as-is
                foreach (var line in responseBody.Split('\n'))
                {
                    _logger.Error($"   {line}");
                }
            }
        }
    }
}