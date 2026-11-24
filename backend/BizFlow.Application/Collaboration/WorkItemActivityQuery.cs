using BizFlow.Application.Audit;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Collaboration;

public sealed record ActivityRow(Guid AuditLogId, string Action, string ActorType, Guid? ActorId,
    string? ActorName, DateTimeOffset CreatedAt);

public sealed record ActivityPage(string ObjectType, Guid ObjectId, IReadOnlyList<ActivityRow> Items,
    int Page, int PageSize, long Total);

public interface IWorkItemActivityStore
{
    Task<bool> IsTaskVisibleAsync(TaskReadScope scope, Guid taskId, CancellationToken cancellationToken);
    Task<bool> IsRequestVisibleAsync(RequestReadScope scope, Guid requestId, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, string>> ActorNamesAsync(Guid tenantId, IReadOnlyCollection<Guid> actorIds,
        CancellationToken cancellationToken);
}

/// <summary>
/// The activity feed shown inside a task or request (F11). Read-only.
///
/// It reuses the existing <see cref="IAuditReader"/> rather than adding a second way to read the
/// audit trail, but deliberately does not go through <see cref="AuditQuery"/>: that one demands
/// the <c>audit.read</c> permission, which is an auditor's capability. A feed embedded in an item
/// has to be gated by that item's own read policy instead, so whoever can open the work can see
/// what happened to it — and nobody else.
///
/// Only the shape of each event is exposed: action, actor and time. The before/after payloads
/// stay in the audit viewer, because they can carry field values the reader has no business
/// seeing just by holding the item open.
/// </summary>
public sealed class WorkItemActivityQuery(
    ITenantContext context,
    IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter audit,
    IAuditReader reader,
    IWorkItemActivityStore store)
{
    public static CommentObjectType ParseObjectType(string? value) => value?.Trim().ToUpperInvariant() switch
    {
        "TASK" => CommentObjectType.Task,
        "REQUEST" => CommentObjectType.Request,
        _ => throw new ApplicationFault(FaultKind.Validation, "ACTIVITY.OBJECT_TYPE",
            "Use TASK or REQUEST as the work item type.")
    };

    public async Task<ActivityPage> ListAsync(string objectType, Guid objectId, int page, int pageSize,
        CancellationToken cancellationToken)
    {
        var type = ParseObjectType(objectType);
        if (objectId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "ACTIVITY.OBJECT_REQUIRED",
                "A work item id is required.");
        if (page < 1 || pageSize is < 1 or > 100)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED",
                "Use a positive page and a page size from 1 to 100.");

        if (context.UserId is not { } userId || userId == Guid.Empty || context.TenantId is not { } tenantId)
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Authentication is required.");
        var access = await snapshots.ResolveAsync(userId, tenantId, cancellationToken);
        if (access is null || access.UserId != userId || access.TenantId != tenantId)
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Authentication is required.");

        var code = type == CommentObjectType.Task ? "TASK" : "REQUEST";
        // The audit trail stores "Task"/"Request", and AuditReader compares the value exactly, so
        // the stored spelling is what narrows the read. The API keeps its own upper-case vocabulary.
        var stored = type == CommentObjectType.Task ? "Task" : "Request";
        var visible = type == CommentObjectType.Task
            ? TaskReadPolicy.Resolve(access) is { Decision.Allowed: true, Scope: { } taskScope } &&
              await store.IsTaskVisibleAsync(taskScope, objectId, cancellationToken)
            : RequestReadPolicy.Resolve(access) is { Decision.Allowed: true, Scope: { } requestScope } &&
              await store.IsRequestVisibleAsync(requestScope, objectId, cancellationToken);
        if (!visible)
        {
            await audit.RecordDeniedAccessAsync(userId, tenantId,
                type == CommentObjectType.Task ? "tasks.read" : "requests.read",
                AccessDenial.ResourceNotFound, cancellationToken);
            throw ApplicationFault.NotFound();
        }

        var entries = await reader.ListAsync(tenantId,
            new(page, pageSize, null, null, null, stored, objectId, null, null), cancellationToken);

        var actorIds = entries.Items.Where(e => e.ActorId is not null).Select(e => e.ActorId!.Value)
            .Distinct().ToArray();
        var names = actorIds.Length == 0
            ? new Dictionary<Guid, string>()
            : await store.ActorNamesAsync(tenantId, actorIds, cancellationToken);

        return new(code, objectId,
            [.. entries.Items.Select(e => new ActivityRow(e.AuditLogId, e.Action, e.ActorType, e.ActorId,
                e.ActorId is { } id && names.TryGetValue(id, out var name) ? name : null, e.CreatedAt))],
            entries.Page, entries.PageSize, entries.Total);
    }
}
