using System.Net;

namespace GitGen.Exceptions;

/// <summary>
///     Exception thrown when an HTTP request fails with structured error information.
/// </summary>
public class HttpResponseException : Exception
{
    /// <summary>
    ///     Gets the HTTP status code of the failed response.
    /// </summary>
    public HttpStatusCode StatusCode { get; }

    /// <summary>
    ///     Gets the URL that was requested.
    /// </summary>
    public string? RequestUrl { get; }

    /// <summary>
    ///     Gets the HTTP method used for the request.
    /// </summary>
    public string? RequestMethod { get; }

    /// <summary>
    ///     Gets the raw response body from the server.
    /// </summary>
    public string? ResponseBody { get; }

    /// <summary>
    ///     Gets the error context provided in the request options.
    /// </summary>
    public string? ErrorContext { get; }

    /// <summary>
    ///     Initializes a new instance of the <see cref="HttpResponseException" /> class.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="requestUrl">The URL that was requested.</param>
    /// <param name="requestMethod">The HTTP method used.</param>
    /// <param name="responseBody">The response body from the server.</param>
    /// <param name="includeResponseBodyInMessage">Whether to include the response body in the exception message.</param>
    /// <param name="errorContext">Optional context about when the error occurred.</param>
    public HttpResponseException(
        HttpStatusCode statusCode,
        string? requestUrl,
        string? requestMethod,
        string? responseBody,
        string? includeResponseBodyInMessage,
        string? errorContext = null)
        : base(BuildMessage(statusCode, requestUrl, requestMethod, includeResponseBodyInMessage, errorContext))
    {
        StatusCode = statusCode;
        RequestUrl = requestUrl;
        RequestMethod = requestMethod;
        ResponseBody = responseBody;
        ErrorContext = errorContext;
    }

    /// <summary>
    ///     Initializes a new instance of the <see cref="HttpResponseException" /> class with an inner exception.
    /// </summary>
    /// <param name="statusCode">The HTTP status code.</param>
    /// <param name="requestUrl">The URL that was requested.</param>
    /// <param name="requestMethod">The HTTP method used.</param>
    /// <param name="responseBody">The response body from the server.</param>
    /// <param name="includeResponseBodyInMessage">Whether to include the response body in the exception message.</param>
    /// <param name="errorContext">Optional context about when the error occurred.</param>
    /// <param name="innerException">The inner exception.</param>
    public HttpResponseException(
        HttpStatusCode statusCode,
        string? requestUrl,
        string? requestMethod,
        string? responseBody,
        string? includeResponseBodyInMessage,
        string? errorContext,
        Exception innerException)
        : base(BuildMessage(statusCode, requestUrl, requestMethod, includeResponseBodyInMessage, errorContext), innerException)
    {
        StatusCode = statusCode;
        RequestUrl = requestUrl;
        RequestMethod = requestMethod;
        ResponseBody = responseBody;
        ErrorContext = errorContext;
    }

    private static string BuildMessage(
        HttpStatusCode statusCode,
        string? requestUrl,
        string? requestMethod,
        string? responseBody,
        string? errorContext)
    {
        var message = $"HTTP request failed with status {(int)statusCode} {statusCode}";

        if (!string.IsNullOrEmpty(requestUrl))
        {
            message += $" from {requestUrl}";
        }

        if (!string.IsNullOrEmpty(requestMethod))
        {
            message += $" (Method: {requestMethod})";
        }

        if (!string.IsNullOrEmpty(errorContext))
        {
            message += $" - {errorContext}";
        }

        if (!string.IsNullOrEmpty(responseBody))
        {
            // Include a truncated version of the response body in the message
            var truncated = responseBody.Length > 200
                ? responseBody.Substring(0, 200) + "..."
                : responseBody;
            message += $". Response: {truncated}";
        }

        return message;
    }

    /// <summary>
    ///     Checks if this is an authentication error based on the status code.
    /// </summary>
    public bool IsAuthenticationError => StatusCode == HttpStatusCode.Unauthorized || StatusCode == HttpStatusCode.Forbidden;

    /// <summary>
    ///     Checks if this is a rate limit error.
    /// </summary>
    public bool IsRateLimitError => StatusCode == HttpStatusCode.TooManyRequests;

    /// <summary>
    ///     Checks if this is a client error (4xx status codes).
    /// </summary>
    public bool IsClientError => (int)StatusCode >= 400 && (int)StatusCode < 500;

    /// <summary>
    ///     Checks if this is a server error (5xx status codes).
    /// </summary>
    public bool IsServerError => (int)StatusCode >= 500 && (int)StatusCode < 600;
}