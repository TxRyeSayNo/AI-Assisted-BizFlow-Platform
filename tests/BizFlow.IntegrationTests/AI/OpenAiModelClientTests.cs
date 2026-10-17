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
