using System.Net.Http;
using System.Threading.Tasks;

namespace GitGen.Services;

/// <summary>
/// Interface for HTTP client service with built-in retry logic and request cloning.
/// </summary>
public interface IHttpClientService
{
    /// <summary>
    /// Sends an HTTP request with retry logic and proper error handling.
    /// </summary>
    /// <param name="request">The HTTP request message to send.</param>
    /// <param name="options">Optional request configuration options.</param>
    /// <returns>The HTTP response message.</returns>
    Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, HttpRequestOptions? options = null);
}