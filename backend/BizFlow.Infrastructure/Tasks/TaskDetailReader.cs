using BizFlow.Application.Tasks;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Tasks;

internal static class TaskReadQueries
{
    internal static IQueryable<WorkTask> Visible(BizFlowDbContext db, TaskReadScope scope)
    {
        var managed = scope.ManagedDepartmentIds.ToArray();
        return db.WorkTasks.AsNoTracking().Where(t => t.TenantId == scope.TenantId &&
            (scope.Tenant || (scope.Own && t.CreatorId == scope.UserId) ||
             db.TaskAssignments.Any(a => a.TaskId == t.Id && a.EndedAt == null &&
                ((scope.Assigned && (a.UserId == scope.UserId ||
                    (a.UserId == null && a.DepartmentId != null && db.Users.Any(u => u.TenantId == scope.TenantId &&
                        u.Id == scope.UserId && u.DepartmentId == a.DepartmentId)))) ||
                 (scope.Managed && ((a.DepartmentId != null && managed.Contains(a.DepartmentId.Value)) ||
                    (a.DepartmentId == null && db.Users.Any(u => u.TenantId == scope.TenantId && u.Id == a.UserId &&
                        u.DepartmentId != null && managed.Contains(u.DepartmentId.Value)))))))));
    }
}

public sealed class TaskDetailReader(BizFlowDbContext db) : ITaskDetailReader
{
    public async Task<TaskDetailView?> GetAsync(TaskReadScope scope, Guid id, TaskDetailPages pages, CancellationToken cancellationToken)
    {
        // One consistent database snapshot prevents mixed assignment/history pages during concurrent changes.
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        var task = await TaskReadQueries.Visible(db, scope).SingleOrDefaultAsync(t => t.Id == id, cancellationToken);
        if (task is null) return null;
        var creator = await db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == task.CreatorId)
            .Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken);
        var assignment = await db.TaskAssignments.AsNoTracking().SingleOrDefaultAsync(a => a.TaskId == id && a.EndedAt == null, cancellationToken);
        string? userName = null, departmentName = null;
        if (assignment?.UserId is { } userId) userName = await db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == userId).Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken);
        if (assignment?.DepartmentId is { } departmentId) departmentName = await db.Departments.Where(d => d.TenantId == scope.TenantId && d.Id == departmentId).Select(d => d.Name).SingleOrDefaultAsync(cancellationToken);
        var checklist = db.TaskChecklistItems.AsNoTracking().Where(c => c.TaskId == id).OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .Select(c => new TaskChecklistView(c.Id, c.Title, c.SortOrder, c.IsCompleted));
        var reports = db.TaskProgressReports.AsNoTracking().Where(r => r.TaskId == id).OrderByDescending(r => r.SubmittedAt).ThenBy(r => r.Id)
            .Select(r => new TaskReportView(r.Id, r.AuthorId, r.Percent, r.Content, r.SubmittedAt));
        var results = db.TaskResults.AsNoTracking().Where(r => r.TaskId == id).OrderByDescending(r => r.RevisionNo).ThenBy(r => r.Id)
            .Select(r => new TaskResultView(r.Id, r.AuthorId, r.RevisionNo, r.Content, r.SubmittedAt));
        async Task<TaskEvidencePage<T>> Page<T>(IQueryable<T> query, int page) => new(
            await query.Skip((page - 1) * pages.PageSize).Take(pages.PageSize).ToArrayAsync(cancellationToken),
            page, pages.PageSize, await query.LongCountAsync(cancellationToken));
        return new(new(task.Id, task.Title, TaskListCodes.State(task.Status), TaskListCodes.Priority(task.Priority), task.Deadline,
            task.CreatedAt, task.CreatorId, task.RequestId, assignment?.UserId, userName, assignment?.DepartmentId, departmentName),
            task.Description, creator, task.WorkflowVersionId, task.SlaVersionId, task.CompletedAt, task.UpdatedAt,
            await Page(checklist, pages.ChecklistPage), await Page(reports, pages.ReportPage), await Page(results, pages.ResultPage));
    }
}
