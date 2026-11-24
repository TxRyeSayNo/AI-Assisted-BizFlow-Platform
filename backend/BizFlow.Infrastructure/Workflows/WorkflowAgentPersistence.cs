using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BizFlow.Application.Workflows;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Workflows;

public sealed class WorkflowAgentRow
{
    public Guid Id { get; set; }
    public Guid TenantId { get; set; }
    public Guid InstanceId { get; set; }
    public Guid RequestId { get; set; }
    public Guid StepId { get; set; }
    public string Mode { get; set; } = "";
    public string Instructions { get; set; } = "";
    public string InputJson { get; set; } = "{}";
    public string InputHash { get; set; } = "";
    public string Status { get; set; } = "";
    public int Attempts { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset NextAttemptAt { get; set; }
    public Guid? LeaseToken { get; set; }
    public DateTimeOffset? LeaseUntil { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public string? ResultJson { get; set; }
    public string? ErrorCode { get; set; }
    public string? Model { get; set; }
    public WorkflowAgentRunView View() => new(Id, Status, Attempts, CreatedAt, CompletedAt, ErrorCode, Model,
        ResultJson is null ? null : JsonSerializer.Deserialize<WorkflowAgentResult>(ResultJson, WorkflowAgentContract.Json));
}

public static class WorkflowAgentPersistence
{
    public static string Hash(string json) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    public static Task<WorkflowAgentRow[]> ReadAsync(BizFlowDbContext db, Guid tenant, Guid instance, CancellationToken ct) =>
        db.Database.SqlQuery<WorkflowAgentRow>($"SELECT * FROM \"WorkflowAgentRun\" WHERE \"TenantId\"={tenant} AND \"InstanceId\"={instance}").ToArrayAsync(ct);
}
