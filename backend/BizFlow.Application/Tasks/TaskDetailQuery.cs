using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.Application.Tasks;

public sealed record TaskDetailPages(int ChecklistPage = 1, int ReportPage = 1, int ResultPage = 1, int PageSize = 25);
public sealed record TaskEvidencePage<T>(IReadOnlyList<T> Items, int Page, int PageSize, long Total);
public sealed record TaskChecklistView(Guid Id, string Title, int SortOrder, bool IsCompleted);
public sealed record TaskReportView(Guid Id, Guid AuthorId, short Percent, string? Content, DateTimeOffset SubmittedAt);
public sealed record TaskResultView(Guid Id, Guid AuthorId, int RevisionNo, string? Content, DateTimeOffset SubmittedAt);
public sealed record TaskDetailView(TaskListRow Task, string? Description, string? CreatorName,
    Guid? WorkflowVersionId, Guid? SlaVersionId, DateTimeOffset? CompletedAt, DateTimeOffset UpdatedAt,
    TaskEvidencePage<TaskChecklistView> Checklist, TaskEvidencePage<TaskReportView> Reports, TaskEvidencePage<TaskResultView> Results);
public interface ITaskDetailReader
{
    Task<TaskDetailView?> GetAsync(TaskReadScope scope, Guid id, TaskDetailPages pages, CancellationToken cancellationToken);
}

public sealed class TaskDetailQuery(ITenantContext context, IAccessSnapshotProvider snapshots,
    ISecurityAuditWriter audit, ITaskDetailReader reader)
{
    public async Task<TaskDetailView> GetAsync(Guid id, TaskDetailPages pages, CancellationToken cancellationToken)
    {
        var access = context.UserId is { } user && user != Guid.Empty
            ? await snapshots.ResolveAsync(user, context.TenantId, cancellationToken) : null;
        var (decision, scope) = access is null || access.UserId != context.UserId || access.TenantId != context.TenantId
            ? (AccessDecision.Deny(AccessDenial.Unauthenticated), (TaskReadScope?)null) : TaskReadPolicy.Resolve(access);
        if (!decision.Allowed || scope is null)
        {
            await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, "tasks.read", decision.Denial, cancellationToken);
            if (context.UserId is null) throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.REQUIRED", "Authentication is required.");
            throw ApplicationFault.NotFound();
        }
        if (pages.PageSize is < 1 or > 100 || new[] { pages.ChecklistPage, pages.ReportPage, pages.ResultPage }
            .Any(p => p < 1 || (long)(p - 1) * pages.PageSize > int.MaxValue))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Use positive pages and a page size from 1 to 100.");
        var result = await reader.GetAsync(scope, id, pages, cancellationToken);
        if (result is not null) return result;
        await audit.RecordDeniedAccessAsync(context.UserId, context.TenantId, "tasks.read", AccessDenial.ResourceNotFound, cancellationToken);
        throw ApplicationFault.NotFound();
    }
}
