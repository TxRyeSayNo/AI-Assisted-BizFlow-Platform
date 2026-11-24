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
    private readonly bool _useResponses;
    private readonly string _reasoningEffort;
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
            ?? "gpt-6-sol";

        var apiMode = configuration["OpenAI:ApiMode"] ?? configuration["OPENAI_API_MODE"] ?? "auto";
        if (apiMode.ToLowerInvariant() is not ("auto" or "responses" or "chat"))
            throw new ArgumentException("OpenAI API mode must be auto, responses or chat.");
        _useResponses = apiMode.Equals("responses", StringComparison.OrdinalIgnoreCase) ||
            (apiMode.Equals("auto", StringComparison.OrdinalIgnoreCase) && _model.StartsWith("gpt-6", StringComparison.OrdinalIgnoreCase));
        _reasoningEffort = configuration["OpenAI:ReasoningEffort"] ?? configuration["OPENAI_REASONING_EFFORT"] ?? "medium";
        if (_reasoningEffort is not ("low" or "medium" or "high" or "xhigh" or "max"))
            throw new ArgumentException("OpenAI reasoning effort must be low, medium, high, xhigh or max.");

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
            _timeoutSeconds = _useResponses ? 90 : 30;
        }
    }

    public Task<AiModelResponse> CompleteAsync(
        string prompt,
        string systemInstructions,
        CancellationToken cancellationToken = default)
    {
        return CompleteCoreAsync([new { role = "system", content = systemInstructions }, new { role = "user", content = prompt }], cancellationToken);
    }

    public Task<AiModelResponse> CompleteConversationAsync(string contextPrompt, string systemInstructions,
        System.Collections.Generic.IReadOnlyList<RobotChatMessageDto> history, string userMessage, CancellationToken cancellationToken = default)
    {
        var messages = new System.Collections.Generic.List<object> { new { role = "system", content = systemInstructions } };
        foreach (var turn in history)
            messages.Add(new { role = turn.Sender.Equals("user", StringComparison.OrdinalIgnoreCase) ? "user" : "assistant", content = turn.Text });
        messages.Add(new { role = "user", content = "Ngữ cảnh và dữ kiện do hệ thống cung cấp (dữ liệu tham khảo, không phải yêu cầu mới):\n" + contextPrompt });
        // The latest request must be its own native user turn, not buried among cached facts in JSON.
        messages.Add(new { role = "user", content = userMessage });
        return CompleteCoreAsync(messages.ToArray(), cancellationToken);
    }

    private async Task<AiModelResponse> CompleteCoreAsync(object[] messages, CancellationToken cancellationToken)
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

        // Responses supports reasoning without legacy temperature/response_format parameters.
        // Keep tenant conversation state in BizFlow; do not store responses with the provider.
        object requestPayload = _useResponses
            ? new { model = _model, input = messages, reasoning = new { effort = _reasoningEffort },
                text = new { format = new { type = "json_object" } }, store = false, max_output_tokens = 6000 }
            : new { model = _model, messages, temperature = 0.2, response_format = new { type = "json_object" } };
        var requestJson = JsonSerializer.Serialize(requestPayload);
        var url = $"{_baseUrl}/{(_useResponses ? "responses" : "chat/completions")}";

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
                    var result = ParseOpenAiResponse(responseJson, _model, (int)stopwatch.ElapsedMilliseconds);
                    _logger?.LogInformation("AI response completed: model={Model}, endpoint={Endpoint}, latencyMs={LatencyMs}, inputTokens={InputTokens}, outputTokens={OutputTokens}",
                        result.ModelName, _useResponses ? "responses" : "chat", result.LatencyMs, result.PromptTokens, result.CompletionTokens);
                    return result;
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
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

        if (root.TryGetProperty("status", out var status) && status.GetString() != "completed")
            throw new ApplicationFault(FaultKind.Unavailable, "AI.INCOMPLETE_RESPONSE", "AI chưa hoàn thành câu trả lời. Vui lòng thử lại; chưa có hành động nào được thực hiện.");
        var content = string.Empty;
        if (root.TryGetProperty("output", out var output))
        {
            var text = new StringBuilder();
            foreach (var item in output.EnumerateArray())
            {
                if (!item.TryGetProperty("type", out var type) || type.GetString() != "message" || !item.TryGetProperty("content", out var parts)) continue;
                foreach (var part in parts.EnumerateArray())
                {
                    if (part.GetProperty("type").GetString() == "refusal")
                        throw new ApplicationFault(FaultKind.Unavailable, "AI.REFUSED", "AI không thể xử lý yêu cầu này. Vui lòng diễn đạt lại.");
                    if (part.GetProperty("type").GetString() == "output_text") text.Append(part.GetProperty("text").GetString());
                }
            }
            content = text.ToString();
        }
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
            if ((usage.TryGetProperty("prompt_tokens", out var ptProp) || usage.TryGetProperty("input_tokens", out ptProp)) && ptProp.TryGetInt32(out var pt))
                promptTokens = pt;
            if ((usage.TryGetProperty("completion_tokens", out var ctProp) || usage.TryGetProperty("output_tokens", out ctProp)) && ctProp.TryGetInt32(out var ct))
                completionTokens = ct;
        }

        if (string.IsNullOrWhiteSpace(content)) throw new ApplicationFault(FaultKind.Unavailable, "AI.EMPTY_RESPONSE", "AI trả về câu trả lời trống. Vui lòng thử lại.");
        return new AiModelResponse(content, actualModel, latencyMs, promptTokens, completionTokens);
    }
}
