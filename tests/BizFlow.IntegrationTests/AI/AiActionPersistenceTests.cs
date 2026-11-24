using BizFlow.Domain.AI;
using BizFlow.Infrastructure.AI;
using BizFlow.IntegrationTests.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.IntegrationTests.AI;

[Collection("Postgres")]
public sealed class AiActionPersistenceTests(PostgresFixture database)
{
    [Theory]
    [InlineData(AgentExecutionStatus.NotExecuted)]
    [InlineData(AgentExecutionStatus.Succeeded)]
    [InlineData(AgentExecutionStatus.Failed)]
    public async Task Action_status_round_trips_through_postgres_constraints(AgentExecutionStatus status)
    {
        var seed = await database.SeedTenantAsync();
        await using var db = database.Create(seed.UserId, seed.TenantId);
        var store = new AiStore(db);
        var interaction = await store.RecordInteractionAsync(seed.TenantId, seed.UserId, "ai.010", "tool-orchestrator",
            null, null, AIInteractionStatus.Succeeded, null, null);
        var action = await store.RecordActionAsync(seed.TenantId, interaction.Id, "create_task_draft", "{\"title\":\"Preview only\"}",
            AgentAuthorizationResult.Authorized, status, null);
        db.ChangeTracker.Clear();
        var persisted = await db.AIAgentActions.SingleAsync(a => a.Id == action.Id);
        Assert.Equal(status, persisted.ExecutionStatus);
        Assert.Empty(await db.WorkTasks.ToListAsync());
    }
}
