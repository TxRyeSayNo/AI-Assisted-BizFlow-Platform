using System;
using System.Text.Json;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Infrastructure.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BizFlow.UnitTests.AI;

public sealed class AiConfirmationTokenServiceTests
{
    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        private DateTimeOffset _now = now;
        public override DateTimeOffset GetUtcNow() => _now;
        public void Advance(TimeSpan duration) => _now = _now.Add(duration);
    }

    [Fact]
    public void CreateConfirmationToken_and_ValidateAndConsume_succeeds_for_matching_parameters()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().Build();
        var service = new AiConfirmationTokenService(config, cache, clock);

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var toolName = "create_task_draft";
        var argsJson = JsonSerializer.Serialize(new { title = "Prepare audit" });

        var token = service.CreateConfirmationToken(tenantId, userId, toolName, argsJson, TimeSpan.FromMinutes(5));
        Assert.NotNull(token);

        var payload = service.ValidateAndConsumeToken(token, tenantId, userId, toolName, argsJson);
        Assert.Equal(tenantId, payload.TenantId);
        Assert.Equal(userId, payload.UserId);
        Assert.Equal(toolName, payload.ToolName);
    }

    [Fact]
    public void ValidateAndConsumeToken_throws_when_replayed()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().Build();
        var service = new AiConfirmationTokenService(config, cache, clock);

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var toolName = "create_task_draft";
        var argsJson = "{}";

        var token = service.CreateConfirmationToken(tenantId, userId, toolName, argsJson);

        // First consume succeeds
        service.ValidateAndConsumeToken(token, tenantId, userId, toolName, argsJson);

        // Second consume throws Conflict (single-use)
        var ex = Assert.Throws<ApplicationFault>(() =>
            service.ValidateAndConsumeToken(token, tenantId, userId, toolName, argsJson));

        Assert.Equal(FaultKind.Conflict, ex.Kind);
        Assert.Equal("AI.CONFIRMATION_ALREADY_USED", ex.Code);
    }

    [Fact]
    public void ValidateAndConsumeToken_throws_when_expired()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().Build();
        var service = new AiConfirmationTokenService(config, cache, clock);

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var toolName = "create_task_draft";
        var argsJson = "{}";

        var token = service.CreateConfirmationToken(tenantId, userId, toolName, argsJson, TimeSpan.FromMinutes(2));

        clock.Advance(TimeSpan.FromMinutes(3));

        var ex = Assert.Throws<ApplicationFault>(() =>
            service.ValidateAndConsumeToken(token, tenantId, userId, toolName, argsJson));

        Assert.Equal(FaultKind.Validation, ex.Kind);
        Assert.Equal("AI.CONFIRMATION_EXPIRED", ex.Code);
    }

    [Fact]
    public void ValidateAndConsumeToken_throws_on_cross_tenant_attempt()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().Build();
        var service = new AiConfirmationTokenService(config, cache, clock);

        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var toolName = "create_task_draft";
        var argsJson = "{}";

        var token = service.CreateConfirmationToken(tenantA, userId, toolName, argsJson);

        var ex = Assert.Throws<ApplicationFault>(() =>
            service.ValidateAndConsumeToken(token, tenantB, userId, toolName, argsJson));

        Assert.Equal(FaultKind.Forbidden, ex.Kind);
        Assert.Equal("AI.CROSS_TENANT_CONFIRMATION", ex.Code);
    }

    [Fact]
    public void ValidateAndConsumeToken_throws_on_actor_mismatch()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().Build();
        var service = new AiConfirmationTokenService(config, cache, clock);

        var tenantId = Guid.NewGuid();
        var userAlice = Guid.NewGuid();
        var userBob = Guid.NewGuid();
        var toolName = "create_task_draft";
        var argsJson = "{}";

        var token = service.CreateConfirmationToken(tenantId, userAlice, toolName, argsJson);

        var ex = Assert.Throws<ApplicationFault>(() =>
            service.ValidateAndConsumeToken(token, tenantId, userBob, toolName, argsJson));

        Assert.Equal(FaultKind.Forbidden, ex.Kind);
        Assert.Equal("AI.ACTOR_MISMATCH", ex.Code);
    }

    [Fact]
    public void ValidateAndConsumeToken_throws_when_arguments_are_tampered()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().Build();
        var service = new AiConfirmationTokenService(config, cache, clock);

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var toolName = "create_task_draft";
        var originalArgs = JsonSerializer.Serialize(new { title = "Clean desks" });
        var tamperedArgs = JsonSerializer.Serialize(new { title = "Delete all files" });

        var token = service.CreateConfirmationToken(tenantId, userId, toolName, originalArgs);

        var ex = Assert.Throws<ApplicationFault>(() =>
            service.ValidateAndConsumeToken(token, tenantId, userId, toolName, tamperedArgs));

        Assert.Equal(FaultKind.Validation, ex.Kind);
        Assert.Equal("AI.ARGS_TAMPERED", ex.Code);
    }

    [Fact]
    public void ValidateAndConsumeToken_throws_when_signature_tampered()
    {
        var clock = new FakeClock(DateTimeOffset.UtcNow);
        var cache = new MemoryCache(new MemoryCacheOptions());
        var config = new ConfigurationBuilder().Build();
        var service = new AiConfirmationTokenService(config, cache, clock);

        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var toolName = "create_task_draft";
        var argsJson = "{}";

        var token = service.CreateConfirmationToken(tenantId, userId, toolName, argsJson);
        var tamperedToken = token[..^4] + "ABCD";

        var ex = Assert.Throws<ApplicationFault>(() =>
            service.ValidateAndConsumeToken(tamperedToken, tenantId, userId, toolName, argsJson));

        Assert.Equal(FaultKind.Forbidden, ex.Kind);
        Assert.Equal("AI.CONFIRMATION_TAMPERED", ex.Code);
    }
}
