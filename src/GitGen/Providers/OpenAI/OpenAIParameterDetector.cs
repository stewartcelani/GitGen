using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GitGen.Configuration;
using GitGen.Exceptions;
using GitGen.Services;

namespace GitGen.Providers.OpenAI;

/// <summary>
///     Result of API parameter detection for OpenAI-compatible providers.
/// </summary>
public class ApiParameters
{
    public string Model { get; set; } = "";
    public bool UseLegacyMaxTokens { get; set; }
    public double Temperature { get; set; }
}

/// <summary>
///     Service for detecting optimal API parameters for OpenAI-compatible providers.
///     Separates parameter detection logic from the main provider implementation.
/// </summary>
public class OpenAIParameterDetector
{
    private readonly string? _apiKey;
    private readonly string _baseUrl;
    private readonly HttpClientService _httpClient;
    private readonly IConsoleLogger _logger;
    private readonly bool _requiresAuth;
    private readonly ILlmCallTracker? _callTracker;
    private readonly ModelConfiguration? _modelConfig;

    public OpenAIParameterDetector(
        HttpClientService httpClient,
        IConsoleLogger logger,
        string baseUrl,
        string? apiKey,
        bool requiresAuth,
        ILlmCallTracker? callTracker = null,
        ModelConfiguration? modelConfig = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _baseUrl = baseUrl;
        _apiKey = apiKey;
        _requiresAuth = requiresAuth;
        _callTracker = callTracker;
        _modelConfig = modelConfig;
    }

    /// <summary>
    ///     Detects the optimal API parameters for the specified model.
    ///     Determines token parameter style and temperature requirements.
    /// </summary>
    /// <param name="model">The model to test</param>
    /// <returns>Detected API parameters</returns>
    /// <exception cref="InvalidOperationException">Thrown when detection fails</exception>
    public async Task<ApiParameters> DetectParametersAsync(string model)
    {
        _logger.Debug("Starting API parameter detection for model: {Model}", model);

        if (!ValidationService.Model.IsValid(model))
            throw new ArgumentException(ValidationService.Model.GetValidationError(model), nameof(model));

        var parameters = new ApiParameters { Model = model };

        try
        {
            // Step 1: Detect token parameter style
            parameters.UseLegacyMaxTokens = await DetectTokenParameterStyle(model);

            // Step 2: Detect temperature requirements
            parameters.Temperature = await DetectTemperatureRequirement(model, parameters.UseLegacyMaxTokens);

            _logger.Information($"{Constants.UI.CheckMark} Parameter detection complete.");
            _logger.Information(
                $"ℹ️ Token parameter: {(parameters.UseLegacyMaxTokens ? "Legacy (max_tokens)" : "Modern (max_completion_tokens)")}");
            _logger.Information($"ℹ️ Temperature: {parameters.Temperature}");

            return parameters;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, Constants.ErrorMessages.ParameterDetectionFailed);
            throw new InvalidOperationException(
                string.Format(Constants.ErrorMessages.ConnectionTestFailed, "parameter"), ex);
        }
    }

    /// <summary>
    ///     Detects whether the model uses legacy max_tokens or modern max_completion_tokens parameter.
    /// </summary>
    /// <param name="model">The model to test</param>
    /// <returns>True if legacy max_tokens should be used</returns>
    private async Task<bool> DetectTokenParameterStyle(string model)
    {
        _logger.Debug("Testing token parameter style for model: {Model}", model);

        // Try modern parameter first
        var modernRequest = CreateTestRequest(model);
        modernRequest.MaxCompletionTokens = Constants.Api.TestTokenLimit;

        try
        {
            await SendRequestAsync(modernRequest);
            _logger.Debug("Model supports modern max_completion_tokens parameter");
            return false;
        }
        catch (HttpRequestException ex) when (IsParameterError(ex, Constants.Api.MaxCompletionTokensParameter))
        {
            _logger.Debug("Model requires legacy max_tokens parameter");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to detect token parameter style");
            throw;
        }
    }

    /// <summary>
    ///     Detects the temperature value required by the model.
    /// </summary>
    /// <param name="model">The model to test</param>
    /// <param name="useLegacyTokens">Whether to use legacy token parameter</param>
    /// <returns>Required temperature value</returns>
    private async Task<double> DetectTemperatureRequirement(string model, bool useLegacyTokens)
    {
        _logger.Debug("Testing temperature requirements for model: {Model}", model);

        var testRequest = CreateTestRequest(model);

        // Set appropriate token parameter
        if (useLegacyTokens)
            testRequest.MaxTokens = Constants.Api.TestTokenLimit;
        else
            testRequest.MaxCompletionTokens = Constants.Api.TestTokenLimit;

        // Try custom temperature first
        testRequest.Temperature = Constants.Configuration.DefaultTemperature;

        try
        {
            await SendRequestAsync(testRequest);
            _logger.Debug("Model supports custom temperature: {Temperature}",
                Constants.Configuration.DefaultTemperature);
            return Constants.Configuration.DefaultTemperature;
        }
        catch (HttpRequestException ex) when (IsParameterError(ex, Constants.Api.TemperatureParameter))
        {
            _logger.Debug("Custom temperature not supported, trying reasoning model default");

            // Try reasoning model temperature
            testRequest.Temperature = Constants.Configuration.ReasoningModelTemperature;

            try
            {
                await SendRequestAsync(testRequest);
                _logger.Debug("Model requires fixed temperature: {Temperature}",
                    Constants.Configuration.ReasoningModelTemperature);
                return Constants.Configuration.ReasoningModelTemperature;
            }
            catch (Exception fallbackEx)
            {
                _logger.Error(fallbackEx, "Temperature detection failed with both default and reasoning temperatures");
                throw;
            }
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to detect temperature requirements");
            throw;
        }
    }

    /// <summary>
    ///     Creates a test request for parameter detection.
    /// </summary>
    /// <param name="model">The model to test</param>
    /// <returns>Test request object</returns>
    private OpenAIRequest CreateTestRequest(string model)
    {
        return new OpenAIRequest
        {
            Model = model,
            Messages = new[] { new Message { Role = "user", Content = Constants.Api.TestPrompt } }
        };
    }

    /// <summary>
    ///     Sends a test request to the API endpoint.
    /// </summary>
    /// <param name="request">The request to send</param>
    /// <returns>HTTP response message</returns>
    /// <exception cref="HttpRequestException">Thrown when API request fails</exception>
    private async Task<HttpResponseMessage> SendRequestAsync(OpenAIRequest request)
    {
        var jsonPayload = JsonSerializer.Serialize(request, OpenAIJsonContext.Default.OpenAIRequest);
        var httpRequest = new HttpRequestMessage(HttpMethod.Post, _baseUrl)
        {
            Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
        };

        // Add authentication if required
        if (_requiresAuth && !string.IsNullOrEmpty(_apiKey))
        {
            if (_baseUrl.Contains(Constants.Api.AzureUrlPattern))
                // Azure OpenAI uses api-key header
                httpRequest.Headers.Add(Constants.Api.AzureApiKeyHeader, _apiKey);
            else
                // Standard OpenAI uses Bearer token
                httpRequest.Headers.Authorization = new AuthenticationHeaderValue(Constants.Api.BearerPrefix, _apiKey);
        }

        try
        {
            // Use HttpRequestOptions.ForProbing to suppress error logging during parameter detection
            // Since we expect failures during probing, we don't want to spam the user with error messages
            var response = await _httpClient.SendAsync(httpRequest, GitGen.Services.HttpRequestOptions.ForProbing);
            return response;
        }
        catch (HttpResponseException ex)
        {
            // Convert to HttpRequestException for compatibility with existing error handling
            _logger.Debug("API error during parameter detection: {StatusCode} - {Error}", ex.StatusCode, ex.ResponseBody);
            
            throw new HttpRequestException(
                string.Format(Constants.ErrorMessages.ApiRequestFailed, ex.StatusCode, ex.Message),
                ex,
                ex.StatusCode);
        }
    }

    /// <summary>
    ///     Checks if an HTTP exception represents a parameter-related error.
    /// </summary>
    /// <param name="ex">The exception to check</param>
    /// <param name="parameter">The specific parameter name to check for</param>
    /// <returns>True if this is a parameter error for the specified parameter</returns>
    private bool IsParameterError(HttpRequestException ex, string parameter)
    {
        if (ex.StatusCode != HttpStatusCode.BadRequest || ex.Message == null)
            return false;

        // Check for various parameter error patterns
        var isParameterError = ex.Message.Contains(Constants.Api.UnsupportedParameterError) ||
                               ex.Message.Contains(Constants.Api.UnsupportedValueError) ||
                               ex.Message.Contains(Constants.Api.InvalidRequestError);

        return isParameterError && ex.Message.Contains(parameter);
    }

    /// <summary>
    ///     Validates the API connection without parameter detection.
    ///     Used for basic connectivity testing.
    /// </summary>
    /// <param name="model">The model to test</param>
    /// <returns>True if connection is successful</returns>
    public async Task<bool> ValidateConnectionAsync(string model)
    {
        _logger.Debug("Validating API connection for model: {Model}", model);

        try
        {
            var testRequest = CreateTestRequest(model);

            // Use minimal parameters to avoid parameter-specific errors
            testRequest.MaxTokens = Constants.Api.TestTokenLimit;
            testRequest.Temperature = Constants.Configuration.DefaultTemperature;

            await SendRequestAsync(testRequest);

            _logger.Debug("API connection validated successfully");
            return true;
        }
        catch (HttpResponseException ex) when (ex.IsAuthenticationError)
        {
            _logger.Error("Authentication failed during connection validation");
            throw new AuthenticationException(Constants.ErrorMessages.AuthenticationFailed, ex);
        }
        catch (HttpRequestException ex) when (IsAuthenticationError(ex))
        {
            _logger.Error("Authentication failed during connection validation");
            throw new AuthenticationException(Constants.ErrorMessages.AuthenticationFailed, ex);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "API connection validation failed");
            return false;
        }
    }

    /// <summary>
    ///     Checks if an HTTP exception represents an authentication error.
    /// </summary>
    /// <param name="ex">The exception to check</param>
    /// <returns>True if this is an authentication error</returns>
    private bool IsAuthenticationError(HttpRequestException ex)
    {
        if (ex.StatusCode == HttpStatusCode.Unauthorized)
            return true;

        if (ex.Message == null)
            return false;

        return ex.Message.Contains(Constants.Api.InvalidApiKeyError) ||
               ex.Message.Contains(Constants.Api.IncorrectApiKeyError) ||
               ex.Message.Contains(Constants.Api.InvalidApiKeyGeneric) ||
               ex.Message.Contains(Constants.Api.UnauthorizedError) ||
               (ex.Message.Contains(Constants.Api.InvalidRequestError) && ex.Message.Contains("api"));
    }
}