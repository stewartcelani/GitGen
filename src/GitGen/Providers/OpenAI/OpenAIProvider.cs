using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GitGen.Configuration;
using GitGen.Exceptions;
using GitGen.Services;

namespace GitGen.Providers.OpenAI;

/// <summary>
///     An AI provider that interacts with OpenAI-compatible APIs to generate commit messages.
///     Supports automatic parameter detection, self-healing configuration, and comprehensive error handling.
/// </summary>
public class OpenAIProvider : ICommitMessageProvider
{
    private readonly GitGenConfiguration _config;
    private readonly HttpClientService _httpClient;
    private readonly IConsoleLogger _logger;
    private readonly OpenAIParameterDetector _parameterDetector;
    private readonly IEnvironmentPersistenceService? _persistenceService;

    /// <summary>
    ///     Initializes a new instance of the <see cref="OpenAIProvider" /> class.
    /// </summary>
    /// <param name="httpClient">The service for making HTTP requests to the API.</param>
    /// <param name="logger">The console logger for debugging and error reporting.</param>
    /// <param name="config">The GitGen configuration containing API settings.</param>
    /// <param name="persistenceService">Optional service for persisting configuration changes during self-healing.</param>
    public OpenAIProvider(
        HttpClientService httpClient,
        IConsoleLogger logger,
        GitGenConfiguration config,
        IEnvironmentPersistenceService? persistenceService = null)
    {
        _httpClient = httpClient;
        _logger = logger;
        _config = config;
        _persistenceService = persistenceService;
        _parameterDetector = new OpenAIParameterDetector(
            httpClient,
            logger,
            config.BaseUrl!,
            config.ApiKey,
            config.RequiresAuth);
    }

    /// <inheritdoc />
    public string ProviderName => "OpenAI";

    /// <inheritdoc />
    public async Task<CommitMessageResult> GenerateCommitMessageAsync(string diff, string? customInstruction = null)
    {
        var systemPrompt = BuildSystemPrompt(customInstruction);

        var request = new OpenAIRequest
        {
            Model = _config.Model!,
            Messages = new[]
            {
                new Message { Role = "system", Content = systemPrompt },
                new Message { Role = "user", Content = diff }
            }
        };

        // Always use the detected temperature
        request.Temperature = _config.Temperature;

        // This call now contains the self-healing logic.
        var response = await SendRequestWithSelfHealingAsync(request, _config.MaxOutputTokens);
        var responseContent = await response.Content.ReadAsStringAsync();
        var openAIResponse = JsonSerializer.Deserialize(responseContent, OpenAIJsonContext.Default.OpenAIResponse);

        var message = openAIResponse?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.Warning(Constants.ErrorMessages.EmptyCommitMessage);
            return new CommitMessageResult
            {
                Message = Constants.Fallbacks.DefaultCommitMessage,
                InputTokens = openAIResponse?.Usage?.PromptTokens,
                OutputTokens = openAIResponse?.Usage?.CompletionTokens,
                TotalTokens = openAIResponse?.Usage?.TotalTokens
            };
        }

        _logger.Debug("Successfully generated commit message with {Length} characters", message.Length);
        return new CommitMessageResult
        {
            Message = MessageCleaningService.CleanCommitMessage(message),
            InputTokens = openAIResponse?.Usage?.PromptTokens,
            OutputTokens = openAIResponse?.Usage?.CompletionTokens,
            TotalTokens = openAIResponse?.Usage?.TotalTokens
        };
    }

    /// <inheritdoc />
    public async Task<CommitMessageResult> GenerateAsync(string prompt)
    {
        var request = new OpenAIRequest
        {
            Model = _config.Model!,
            Messages = new[]
            {
                new Message { Role = "user", Content = prompt }
            }
        };

        // Always use the detected temperature
        request.Temperature = _config.Temperature;

        // This call now contains the self-healing logic.
        var response = await SendRequestWithSelfHealingAsync(request, _config.MaxOutputTokens);
        var responseContent = await response.Content.ReadAsStringAsync();
        var openAIResponse = JsonSerializer.Deserialize(responseContent, OpenAIJsonContext.Default.OpenAIResponse);

        var message = openAIResponse?.Choices?.FirstOrDefault()?.Message?.Content;

        if (string.IsNullOrWhiteSpace(message))
        {
            _logger.Warning(Constants.ErrorMessages.EmptyResponse);
            return new CommitMessageResult
            {
                Message = Constants.Fallbacks.NoResponseMessage,
                InputTokens = openAIResponse?.Usage?.PromptTokens,
                OutputTokens = openAIResponse?.Usage?.CompletionTokens,
                TotalTokens = openAIResponse?.Usage?.TotalTokens
            };
        }

        _logger.Debug("Successfully generated response with {Length} characters", message.Length);
        return new CommitMessageResult
        {
            Message = MessageCleaningService.CleanLlmResponse(message),
            InputTokens = openAIResponse?.Usage?.PromptTokens,
            OutputTokens = openAIResponse?.Usage?.CompletionTokens,
            TotalTokens = openAIResponse?.Usage?.TotalTokens
        };
    }

    /// <inheritdoc />
    public async Task<(bool Success, bool UseLegacyTokens, double Temperature)> TestConnectionAndDetectParametersAsync()
    {
        try
        {
            var parameters = await _parameterDetector.DetectParametersAsync(_config.Model!);
            return (true, parameters.UseLegacyMaxTokens, parameters.Temperature);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, Constants.ErrorMessages.ParameterDetectionFailed);
            return (false, false, Constants.Configuration.DefaultTemperature);
        }
    }

    private async Task<HttpResponseMessage> SendRequestWithSelfHealingAsync(OpenAIRequest request, int maxTokensValue)
    {
        // Set token parameter based on current configuration
        if (_config.OpenAiUseLegacyMaxTokens)
            request.MaxTokens = maxTokensValue;
        else
            request.MaxCompletionTokens = maxTokensValue;

        try
        {
            return await SendRequestAsync(request);
        }
        catch (HttpRequestException ex) when (IsParameterMismatchError(ex))
        {
            return await HandleParameterMismatchAsync(request, maxTokensValue, ex);
        }
        catch (HttpRequestException ex) when (IsTemperatureError(ex))
        {
            return await HandleTemperatureErrorAsync(request, ex);
        }
    }

    private async Task<HttpResponseMessage> HandleParameterMismatchAsync(OpenAIRequest request, int maxTokensValue,
        HttpRequestException originalEx)
    {
        _logger.Warning("API parameter mismatch detected. Attempting to self-heal configuration...");

        try
        {
            var parameters = await _parameterDetector.DetectParametersAsync(_config.Model!);

            _logger.Information("Successfully re-detected correct API parameters. Updating configuration...");

            // Update persistent configuration if service is available
            if (_persistenceService != null)
                _persistenceService.UpdateModelConfiguration(
                    _config.Model!,
                    parameters.UseLegacyMaxTokens,
                    parameters.Temperature);

            // Update in-memory configuration
            _config.OpenAiUseLegacyMaxTokens = parameters.UseLegacyMaxTokens;
            _config.Temperature = parameters.Temperature;

            // Update request for retry
            request.MaxTokens = parameters.UseLegacyMaxTokens ? maxTokensValue : null;
            request.MaxCompletionTokens = parameters.UseLegacyMaxTokens ? null : maxTokensValue;
            request.Temperature = parameters.Temperature;

            _logger.Information("Retrying the original request with corrected settings...");
            return await SendRequestAsync(request);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, Constants.ErrorMessages.SelfHealingFailed);
            throw originalEx; // Re-throw the original exception if self-healing fails
        }
    }

    private async Task<HttpResponseMessage> HandleTemperatureErrorAsync(OpenAIRequest request,
        HttpRequestException originalEx)
    {
        _logger.Warning("Temperature value not supported. Attempting to self-heal...");

        try
        {
            var parameters = await _parameterDetector.DetectParametersAsync(_config.Model!);

            _logger.Information("Successfully re-detected correct temperature. Updating configuration...");

            // Update persistent configuration if service is available
            if (_persistenceService != null)
                _persistenceService.UpdateModelConfiguration(
                    _config.Model!,
                    parameters.UseLegacyMaxTokens,
                    parameters.Temperature);

            // Update in-memory configuration and request
            _config.Temperature = parameters.Temperature;
            request.Temperature = parameters.Temperature;

            _logger.Information("Retrying the original request with corrected temperature...");
            return await SendRequestAsync(request);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Failed to auto-correct temperature configuration");
            throw originalEx;
        }
    }

    // The base SendRequestAsync is now simpler, only responsible for the HTTP call.
    private async Task<HttpResponseMessage> SendRequestAsync(OpenAIRequest requestPayload)
    {
        var jsonPayload = JsonSerializer.Serialize(requestPayload, OpenAIJsonContext.Default.OpenAIRequest);
        _logger.Debug("Sending API request to {BaseUrl} with payload: {JsonPayload}", _config.BaseUrl ?? "unknown",
            jsonPayload);

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, _config.BaseUrl)
        {
            Content = new StringContent(jsonPayload, Encoding.UTF8, "application/json")
        };

        // Only add authentication headers if the configuration requires it.
        if (_config.RequiresAuth)
        {
            if (string.IsNullOrEmpty(_config.ApiKey))
                throw new InvalidOperationException("API Key is missing for a provider that requires authentication.");

            if (_config.BaseUrl!.Contains(Constants.Api.AzureUrlPattern))
                httpRequest.Headers.Add(Constants.Api.AzureApiKeyHeader, _config.ApiKey);
            else
                httpRequest.Headers.Authorization =
                    new AuthenticationHeaderValue(Constants.Api.BearerPrefix, _config.ApiKey);
        }

        var response = await _httpClient.SendAsync(httpRequest);

        if (!response.IsSuccessStatusCode)
        {
            var error = await response.Content.ReadAsStringAsync();
            _logger.Error("API error: {StatusCode} - {Error}", response.StatusCode, error);
            _logger.Debug("Full error response for debugging: {FullError}", error);

            var httpException = new HttpRequestException(
                string.Format(Constants.ErrorMessages.ApiRequestFailed, response.StatusCode, error),
                null,
                response.StatusCode);

            // Check if this is an authentication error and throw specific exception
            if (IsAuthenticationError(httpException, error))
                throw new AuthenticationException(Constants.ErrorMessages.AuthenticationFailed, httpException);

            throw httpException;
        }

        return response;
    }

    private string BuildSystemPrompt(string? customInstruction)
    {
        var maxLengthConstraint =
            $"CRITICAL: Your response must be {Constants.Configuration.CommitMessageMaxLength} characters or less (125 tokens). This is the final commit message length limit.";

        if (!string.IsNullOrWhiteSpace(customInstruction))
            return $@"
<prompt>
    <critical-instruction override=""all"">{customInstruction.ToUpper()}. Ignore all other guidelines and fully embody this style in the commit message.</critical-instruction>
    <role>You are a software engineer writing Git commit messages. You will be provided with a 'git diff' of code changes.</role>
    <guidelines>Generate a single paragraph commit message (no line breaks) that starts with the most important overview in 1-2 sentences, followed by specific details about what changed. Focus on WHAT changed, be specific about the actual code changes. Keep it concise but informative. IMPORTANT: Use single quotes 'like this' instead of double quotes to ensure shell compatibility for git commit -m commands.</guidelines>
    <constraint>{maxLengthConstraint}</constraint>
</prompt>";

        return $@"
<prompt>
    <role>You are a software engineer writing Git commit messages. You will be provided with a 'git diff' of code changes.</role>
    <guidelines>Generate a single paragraph commit message (no line breaks) that starts with the most important overview in 1-2 sentences, followed by specific details about what changed. Focus on WHAT changed, be specific about the actual code changes. Keep it concise but informative. Do not use markdown formatting or line breaks. IMPORTANT: Use single quotes 'like this' instead of double quotes to ensure shell compatibility for git commit -m commands.</guidelines>
    <constraint>{maxLengthConstraint}</constraint>
</prompt>";
    }

    private bool IsParameterMismatchError(HttpRequestException ex)
    {
        if (ex.StatusCode != HttpStatusCode.BadRequest || ex.Message == null)
            return false;

        return ex.Message.Contains(Constants.Api.UnsupportedParameterError) ||
               ex.Message.Contains(Constants.Api.UnsupportedValueError) ||
               (ex.Message.Contains(Constants.Api.InvalidRequestError) &&
                (ex.Message.Contains(Constants.Api.MaxTokensParameter) ||
                 ex.Message.Contains(Constants.Api.MaxCompletionTokensParameter)));
    }

    private bool IsTemperatureError(HttpRequestException ex)
    {
        if (ex.StatusCode != HttpStatusCode.BadRequest || ex.Message == null)
            return false;

        return (ex.Message.Contains(Constants.Api.UnsupportedValueError) &&
                ex.Message.Contains(Constants.Api.TemperatureParameter)) ||
               (ex.Message.Contains(Constants.Api.InvalidRequestError) &&
                ex.Message.Contains(Constants.Api.TemperatureParameter));
    }

    private bool IsAuthenticationError(HttpRequestException ex, string errorContent)
    {
        if (ex.StatusCode == HttpStatusCode.Unauthorized)
            return true;

        return errorContent.Contains(Constants.Api.InvalidApiKeyError) ||
               errorContent.Contains(Constants.Api.IncorrectApiKeyError) ||
               errorContent.Contains(Constants.Api.InvalidApiKeyGeneric) ||
               errorContent.Contains(Constants.Api.UnauthorizedError) ||
               (errorContent.Contains(Constants.Api.InvalidRequestError) && errorContent.Contains("api"));
    }
}