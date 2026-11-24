using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.Common;
using BizFlow.Infrastructure.AI;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BizFlow.IntegrationTests.AI;

public sealed class OpenAiModelClientTests
{
    private sealed class MockHttpMessageHandler(Func<HttpRequestMessage, Task<HttpResponseMessage>> responder) : HttpMessageHandler
    {
        public List<HttpRequestMessage> SentRequests { get; } = [];

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            SentRequests.Add(request);
            return await responder(request);
        }
    }

    [Fact]
    public async Task Sol_uses_responses_reasoning_and_parses_typed_output_and_usage()
    {
        System.Text.Json.JsonElement captured = default;
        var handler = new MockHttpMessageHandler(async request =>
        {
            Assert.Equal("https://api.openai.com/v1/responses", request.RequestUri!.ToString());
            captured = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync()).RootElement.Clone();
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(
                """{"model":"gpt-6-sol","status":"completed","output":[{"type":"reasoning","summary":[]},{"type":"message","content":[{"type":"output_text","text":"{\"reply\":\"Understood\"}"}]}],"usage":{"input_tokens":120,"output_tokens":40}}""") };
        });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> {
            ["OpenAI:ApiKey"] = "test-only", ["OpenAI:Model"] = "gpt-6-sol", ["OpenAI:ReasoningEffort"] = "medium" }).Build();
        var result = await new OpenAiModelClient(new HttpClient(handler), config).CompleteConversationAsync("{}", "Return JSON", [new("user", "Earlier"), new("robot", "Noted")], "Follow-up");
        Assert.Equal("gpt-6-sol", result.ModelName);
        Assert.Equal("{\"reply\":\"Understood\"}", result.Content);
        Assert.Equal(120, result.PromptTokens); Assert.Equal(40, result.CompletionTokens);
        Assert.False(captured.GetProperty("store").GetBoolean());
        Assert.False(captured.TryGetProperty("temperature", out _));
        Assert.Equal("medium", captured.GetProperty("reasoning").GetProperty("effort").GetString());
        var turns = captured.GetProperty("input");
        Assert.Equal("assistant", turns[2].GetProperty("role").GetString());
        Assert.Equal("Follow-up", turns[turns.GetArrayLength()-1].GetProperty("content").GetString());
    }

    [Theory]
    [InlineData("incomplete")]
    [InlineData("failed")]
    public async Task Incomplete_responses_never_become_an_action(string status)
    {
        var handler = new MockHttpMessageHandler(_ => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) {
            Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(new { status, output = new object[0] })) }));
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string,string?> { ["OpenAI:ApiKey"] = "test-only" }).Build();
        var fault = await Assert.ThrowsAsync<ApplicationFault>(() => new OpenAiModelClient(new HttpClient(handler), config).CompleteAsync("JSON", "JSON"));
        Assert.Equal("AI.INCOMPLETE_RESPONSE", fault.Code); Assert.Single(handler.SentRequests);
    }

    [Fact]
    public async Task Conversation_preserves_native_roles_and_places_latest_user_message_last()
    {
        System.Text.Json.JsonElement captured = default;
        var handler = new MockHttpMessageHandler(async request =>
        {
            captured = System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync()).RootElement.Clone();
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{"choices":[{"message":{"content":"{\"reply\":\"OK\"}"}}]}""", Encoding.UTF8, "application/json")
            };
        });
        var config = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?> { ["OpenAI:ApiKey"] = "test-only", ["OpenAI:Model"] = "gpt-4o-mini" }).Build();
        var client = new OpenAiModelClient(new HttpClient(handler), config);
        const string latest = "Nội dung về báo cáo tổng kết công ty Quý 4, toàn thể công ty";
        await client.CompleteConversationAsync("{\"knownFacts\":{}}", "Extract facts", [new("user", "Chuẩn bị cuộc họp ngày mai"), new("robot", "Chủ đề là gì?")], latest);
        var messages = captured.GetProperty("messages");
        Assert.Equal("system", messages[0].GetProperty("role").GetString());
        Assert.Equal("user", messages[1].GetProperty("role").GetString());
        Assert.Equal("assistant", messages[2].GetProperty("role").GetString());
        Assert.Equal("Chủ đề là gì?", messages[2].GetProperty("content").GetString());
        Assert.Equal("user", messages[messages.GetArrayLength() - 1].GetProperty("role").GetString());
        Assert.Equal(latest, messages[messages.GetArrayLength() - 1].GetProperty("content").GetString());
    }

    [Fact]
    public void IsConfigured_ReturnsFalse_WhenApiKeyMissing()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var client = new OpenAiModelClient(new HttpClient(), config);

        Assert.False(client.IsConfigured);
    }

    [Fact]
    public void IsConfigured_ReturnsTrue_WhenApiKeyPresent()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["OpenAI:ApiKey"] = "sk-test-key-123"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();
        var client = new OpenAiModelClient(new HttpClient(), config);

        Assert.True(client.IsConfigured);
    }

    [Fact]
    public async Task CompleteAsync_ThrowsUnavailableFault_WhenNotConfigured()
    {
        var config = new ConfigurationBuilder().AddInMemoryCollection().Build();
        var client = new OpenAiModelClient(new HttpClient(), config);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            client.CompleteAsync("prompt", "instructions"));

        Assert.Equal(FaultKind.Unavailable, fault.Kind);
        Assert.Equal("AI.NOT_CONFIGURED", fault.Code);
    }

    [Fact]
    public async Task CompleteAsync_ParsesValidResponse_AndPassesCorrectHeaders()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["OpenAI:ApiKey"] = "sk-valid-key",
            ["OpenAI:Model"] = "gpt-4o-mini",
            ["OpenAI:BaseUrl"] = "https://api.openai.com/v1"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        var jsonResponse = """
        {
            "id": "chatcmpl-123",
            "model": "gpt-4o-mini-2024-07-18",
            "choices": [
                {
                    "index": 0,
                    "message": {
                        "role": "assistant",
                        "content": "{\"status\":\"ok\",\"suggestion\":\"Test response\"}"
                    },
                    "finish_reason": "stop"
                }
            ],
            "usage": {
                "prompt_tokens": 42,
                "completion_tokens": 18,
                "total_tokens": 60
            }
        }
        """;

        var handler = new MockHttpMessageHandler(req =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(jsonResponse, Encoding.UTF8, "application/json")
            };
            return Task.FromResult(res);
        });

        var httpClient = new HttpClient(handler);
        var client = new OpenAiModelClient(httpClient, config);

        var response = await client.CompleteAsync("Hello world", "Be concise");

        Assert.Single(handler.SentRequests);
        var sentReq = handler.SentRequests[0];
        Assert.Equal("Bearer", sentReq.Headers.Authorization?.Scheme);
        Assert.Equal("sk-valid-key", sentReq.Headers.Authorization?.Parameter);
        Assert.Equal("https://api.openai.com/v1/chat/completions", sentReq.RequestUri?.ToString());

        Assert.Equal("{\"status\":\"ok\",\"suggestion\":\"Test response\"}", response.Content);
        Assert.Equal("gpt-4o-mini-2024-07-18", response.ModelName);
        Assert.Equal(42, response.PromptTokens);
        Assert.Equal(18, response.CompletionTokens);
        Assert.True(response.LatencyMs >= 0);
    }

    [Fact]
    public async Task CompleteAsync_ThrowsUnauthorized_When401Returned()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["OpenAI:ApiKey"] = "sk-invalid-key"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        var handler = new MockHttpMessageHandler(req =>
        {
            var res = new HttpResponseMessage(HttpStatusCode.Unauthorized)
            {
                Content = new StringContent("{\"error\": {\"message\": \"Incorrect API key\"}}", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(res);
        });

        var client = new OpenAiModelClient(new HttpClient(handler), config);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            client.CompleteAsync("prompt", "instructions"));

        Assert.Equal(FaultKind.Unavailable, fault.Kind);
        Assert.Equal("AI.UNAUTHORIZED", fault.Code);
    }

    [Fact]
    public async Task CompleteAsync_ThrowsRateLimited_When429RetriesExceeded()
    {
        var inMemory = new Dictionary<string, string?>
        {
            ["OpenAI:ApiKey"] = "sk-test-key",
            ["OpenAI:TimeoutSeconds"] = "5"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemory).Build();

        var attempts = 0;
        var handler = new MockHttpMessageHandler(req =>
        {
            attempts++;
            var res = new HttpResponseMessage((HttpStatusCode)429)
            {
                Content = new StringContent("{\"error\": {\"message\": \"Rate limit exceeded\"}}", Encoding.UTF8, "application/json")
            };
            return Task.FromResult(res);
        });

        var client = new OpenAiModelClient(new HttpClient(handler), config);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            client.CompleteAsync("prompt", "instructions"));

        Assert.Equal(FaultKind.TooManyRequests, fault.Kind);
        Assert.Equal("AI.RATE_LIMITED", fault.Code);
        Assert.True(attempts > 1, "Expected bounded retries on 429 status code");
    }
}
