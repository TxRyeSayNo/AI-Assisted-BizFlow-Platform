using System;
using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.AI;

public sealed class OpenAiModelClient : IAiModelClient
{
    private readonly HttpClient _httpClient;
    private readonly string? _apiKey;
    private readonly string _model;
    private readonly string _baseUrl;
    private readonly int _timeoutSeconds;
    private readonly ILogger<OpenAiModelClient>? _logger;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(_apiKey);

    public OpenAiModelClient(
        HttpClient httpClient,
        IConfiguration configuration,
        ILogger<OpenAiModelClient>? logger = null)
    {
        _httpClient = httpClient;
        _logger = logger;

        _apiKey = configuration["OpenAI:ApiKey"]
            ?? configuration["OPENAI_API_KEY"];

        _model = configuration["OpenAI:Model"]
            ?? configuration["OPENAI_MODEL"]
            ?? "gpt-4o-mini";

        var rawBaseUrl = configuration["OpenAI:BaseUrl"]
            ?? configuration["OPENAI_BASE_URL"]
            ?? "https://api.openai.com/v1";
        _baseUrl = rawBaseUrl.TrimEnd('/');

        if (int.TryParse(configuration["OpenAI:TimeoutSeconds"] ?? configuration["OPENAI_TIMEOUT_SECONDS"], out var parsedTimeout) && parsedTimeout > 0)
        {
            _timeoutSeconds = parsedTimeout;
        }
        else
        {
            _timeoutSeconds = 30;
        }
    }

    public async Task<AiModelResponse> CompleteAsync(
        string prompt,
        string systemInstructions,
        CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new ApplicationFault(
                FaultKind.Unavailable,
                "AI.NOT_CONFIGURED",
                "OpenAI API key is not configured on this server.");
        }

        using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        cts.CancelAfter(TimeSpan.FromSeconds(_timeoutSeconds));

        var requestPayload = new
        {
            model = _model,
            messages = new object[]
            {
                new { role = "system", content = systemInstructions },
                new { role = "user", content = prompt }
            },
            temperature = 0.2,
            response_format = new { type = "json_object" }
        };

        var requestJson = JsonSerializer.Serialize(requestPayload);
        var url = $"{_baseUrl}/chat/completions";

        const int maxRetries = 2;
        var attempt = 0;
        var stopwatch = Stopwatch.StartNew();

        while (true)
        {
            attempt++;
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _apiKey);
            httpRequest.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            HttpResponseMessage? response = null;
            try
            {
                response = await _httpClient.SendAsync(httpRequest, cts.Token);

                if (response.IsSuccessStatusCode)
                {
                    stopwatch.Stop();
                    var responseJson = await response.Content.ReadAsStringAsync(cts.Token);
                    return ParseOpenAiResponse(responseJson, _model, (int)stopwatch.ElapsedMilliseconds);
                }

                var statusCode = (int)response.StatusCode;
                var errorBody = await response.Content.ReadAsStringAsync(cts.Token);

                // Handle rate limiting (429) or transient server errors (500, 502, 503, 504) with bounded retry
                if ((statusCode == 429 || statusCode >= 500) && attempt <= maxRetries)
                {
                    var backoffMs = attempt * 500;
                    _logger?.LogWarning("OpenAI API returned {StatusCode}. Retrying attempt {Attempt} after {BackoffMs}ms.", statusCode, attempt, backoffMs);
                    await Task.Delay(backoffMs, cts.Token);
                    continue;
                }

                _logger?.LogError("OpenAI API request failed with status {StatusCode}: {ErrorBody}", statusCode, errorBody);

                if (statusCode == 401)
                {
                    throw new ApplicationFault(FaultKind.Unavailable, "AI.UNAUTHORIZED", "OpenAI authentication failed. Verify API key configuration.");
                }

                if (statusCode == 429)
                {
                    throw new ApplicationFault(FaultKind.TooManyRequests, "AI.RATE_LIMITED", "OpenAI API rate limit exceeded. Please retry later.");
                }

                throw new ApplicationFault(FaultKind.Unavailable, "AI.PROVIDER_ERROR", $"OpenAI API returned status code {statusCode}.");
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                stopwatch.Stop();
                _logger?.LogError("OpenAI API request timed out after {Timeout}s.", _timeoutSeconds);
                throw new ApplicationFault(FaultKind.Unavailable, "AI.TIMEOUT", $"OpenAI request timed out after {_timeoutSeconds} seconds.");
            }
            catch (ApplicationFault)
            {
                throw;
            }
            catch (Exception ex)
            {
                stopwatch.Stop();
                if (attempt <= maxRetries)
                {
                    var backoffMs = attempt * 500;
                    _logger?.LogWarning(ex, "OpenAI connection failed on attempt {Attempt}. Retrying after {BackoffMs}ms.", attempt, backoffMs);
                    await Task.Delay(backoffMs, cts.Token);
                    continue;
                }

                _logger?.LogError(ex, "OpenAI connection failed after {Attempt} attempts.", attempt);
                throw new ApplicationFault(FaultKind.Unavailable, "AI.CONNECTION_FAILED", "Failed to connect to OpenAI service.");
            }
            finally
            {
                response?.Dispose();
            }
        }
    }

    private static AiModelResponse ParseOpenAiResponse(string json, string fallbackModel, int latencyMs)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var actualModel = root.TryGetProperty("model", out var modelProp) ? modelProp.GetString() ?? fallbackModel : fallbackModel;

        var content = string.Empty;
        if (root.TryGetProperty("choices", out var choices) && choices.GetArrayLength() > 0)
        {
            var firstChoice = choices[0];
            if (firstChoice.TryGetProperty("message", out var message) && message.TryGetProperty("content", out var contentProp))
            {
                content = contentProp.GetString() ?? string.Empty;
            }
        }

        int? promptTokens = null;
        int? completionTokens = null;
        if (root.TryGetProperty("usage", out var usage))
        {
            if (usage.TryGetProperty("prompt_tokens", out var ptProp) && ptProp.TryGetInt32(out var pt))
                promptTokens = pt;
            if (usage.TryGetProperty("completion_tokens", out var ctProp) && ctProp.TryGetInt32(out var ct))
                completionTokens = ct;
        }

        return new AiModelResponse(content, actualModel, latencyMs, promptTokens, completionTokens);
    }
}
