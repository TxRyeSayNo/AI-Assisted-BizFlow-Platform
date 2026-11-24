using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Workflows;

namespace BizFlow.Application.Tasks;

public sealed record TaskStartedView(Guid TaskId, Guid AssignmentId, string Status, DateTimeOffset StartedAt);
public sealed record StoredTaskExecution(string Fingerprint, TaskStartedView Result);
public interface ITaskExecutionStore
{
    Task<ITaskExecutionTransaction> BeginAsync(Guid tenantId, Guid actorId, Guid taskId, string? keyHash, CancellationToken cancellationToken);
    Task<TaskReportView> RecordProgressReportAsync(Guid tenantId, Guid actorId, Guid taskId, short percent, string content, DateTimeOffset now, CancellationToken cancellationToken)
        => throw new NotSupportedException();
}
public interface ITaskExecutionTransaction : IAsyncDisposable
{
    WorkTask? Task { get; }
    TaskAssignment? Assignment { get; }
    UserAccount? Actor { get; }
    Task<StoredTaskExecution?> FindReplayAsync(CancellationToken cancellationToken);
    Task CommitAsync(AuditLog audit, CancellationToken cancellationToken);
}

public sealed class TaskExecutionCommand(ITenantContext context, IResourceAuthorizer authorizer, IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter securityAudit, ITaskExecutionStore store, IWorkflowEngine workflow, TimeProvider clock)
{
    public async Task<TaskStartedView> StartAsync(Guid taskId, string? key, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("tasks.execute", new(context.TenantId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId) throw ApplicationFault.NotFound();
        var keyHash = TaskCreationRules.KeyHash(key);
        var fingerprint = TaskCreationRules.Hash(JsonSerializer.Serialize(new { version = 1, taskId }));
        await using var transaction = await store.BeginAsync(tenantId, actorId, taskId, keyHash, cancellationToken);
        await authorizer.AuthorizeAsync("tasks.execute", new(tenantId), cancellationToken: cancellationToken);
        var replay = keyHash is null ? null : await transaction.FindReplayAsync(cancellationToken);
        if (replay is not null && replay.Fingerprint != fingerprint)
            throw new ApplicationFault(FaultKind.Conflict, "IDEMPOTENCY.CONFLICT", "This key was already used to start different work.");
        var task = transaction.Task; var assignment = transaction.Assignment; var actor = transaction.Actor;
        if (task is null || assignment is null || actor is null || assignment.UserId != actorId)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "tasks.execute", AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }
        if (replay is not null) return replay.Result;
        var access = await snapshots.ResolveAsync(actorId, tenantId, cancellationToken);
        if (access is null || access.UserId != actorId || access.TenantId != tenantId) throw ApplicationFault.NotFound();
        var decision = workflow.StartTask(task, assignment, actor, access, TaskCreationRules.DatabaseTime(clock.GetUtcNow()));
        if (!decision.Allowed && decision.AccessDenial is { } denial)
        {
            await securityAudit.RecordDeniedAccessAsync(actorId, tenantId, "tasks.execute", denial, cancellationToken);
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "You no longer have access to execute this work.");
        }
        if (!decision.Allowed) throw new ApplicationFault(FaultKind.Conflict, "TASK.TRANSITION_DENIED", "This task cannot start or resume in its current state or configuration.");
        await transaction.CommitAsync(AuditLog.TaskStarted(task, assignment, keyHash, keyHash is null ? null : fingerprint), cancellationToken);
        return new(task.Id, assignment.Id, "IN_PROGRESS", task.UpdatedAt);
    }

    public async Task<TaskReportView> ReportProgressAsync(Guid taskId, short percent, string? content, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("tasks.execute", new(context.TenantId), cancellationToken: cancellationToken);
        if (context.TenantId is not { } tenantId || context.UserId is not { } actorId) throw ApplicationFault.NotFound();
        if (percent is < 0 or > 99)
            throw new ApplicationFault(FaultKind.Validation, "TASK.INVALID_PROGRESS_PERCENT",
                "Tiến độ báo cáo định kỳ phải nằm trong khoảng từ 0% đến 99%. Để hoàn thành 100%, vui lòng nộp kết quả nghiệm thu chính thức.");
        var clean = string.IsNullOrWhiteSpace(content) ? null : content.Trim();
        if (clean is null || clean.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ApplicationFault(FaultKind.Validation, "TASK.PROGRESS_CONTENT_REQUIRED",
                "Vui lòng nhập nội dung báo cáo tiến độ rõ ràng.");
        var now = TaskCreationRules.DatabaseTime(clock.GetUtcNow());
        return await store.RecordProgressReportAsync(tenantId, actorId, taskId, percent, clean, now, cancellationToken);
    }
}
