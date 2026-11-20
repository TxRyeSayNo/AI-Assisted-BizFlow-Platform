using BizFlow.Application.Dashboard;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Dashboard;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using BizFlow.Infrastructure.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Dashboard;

/// <summary>
/// Reads the authenticated caller's own work. The subject is <c>scope.UserId</c>, which the
/// Application layer takes from the validated principal, so this reader has no way to be aimed
/// at another account even if a request tried to supply one.
/// </summary>
public sealed class MyWorkDashboardReader(BizFlowDbContext db, IProjectDashboardReader projects) : IMyWorkDashboardReader
{
    public async Task<MyWorkSlice> GetAsync(TaskReadScope scope, MyWorkFilter filter, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(filter);

        var tenantId = scope.TenantId;
        var userId = scope.UserId;

        // Work currently held by this caller, inside what their own read scope already allows.
        var mine = TaskReadQueries.Visible(db, scope)
            .Where(t => t.TenantId == tenantId &&
                db.TaskAssignments.Any(a => a.TaskId == t.Id && a.EndedAt == null && a.UserId == userId));

        if (filter.Status is { } status)
        {
            var state = Enum.GetValues<TaskState>().Single(s => TaskListCodes.State(s) == status);
            mine = mine.Where(t => t.Status == state);
        }

        var total = await mine.LongCountAsync(cancellationToken);
        var rows = await mine
            .OrderBy(t => t.Deadline == null).ThenBy(t => t.Deadline).ThenBy(t => t.Id)
            .Skip(checked((filter.Page - 1) * filter.PageSize)).Take(filter.PageSize)
            .Select(t => new MineRow(
                t.Id,
                t.Title,
                t.Status,
                t.Priority,
                t.Deadline,
                t.UpdatedAt,
                t.ParentTaskId,
                db.WorkTasks.Where(p => p.Id == t.ParentTaskId && p.TenantId == tenantId && p.DeletedAt == null)
                    .Select(p => p.Title).FirstOrDefault(),
                db.TaskChecklistItems.Count(c => c.TaskId == t.Id),
                db.TaskChecklistItems.Count(c => c.TaskId == t.Id && c.IsCompleted),
                db.TaskAssignments.Where(a => a.TaskId == t.Id && a.EndedAt == null && a.UserId == userId)
                    .OrderByDescending(a => a.AssignedAt).Select(a => a.DepartmentId).FirstOrDefault()))
            .ToListAsync(cancellationToken);

        // Work routed to this caller's department that nobody has claimed yet: it is not "mine"
        // until accepted, so it is surfaced as a separate count rather than mixed into progress.
        var pendingClaims = await TaskReadQueries.Visible(db, scope)
            .CountAsync(t => t.TenantId == tenantId && t.Status == TaskState.Assigned &&
                db.TaskAssignments.Any(a => a.TaskId == t.Id && a.EndedAt == null && a.UserId == null &&
                    a.DepartmentId != null &&
                    db.Users.Any(u => u.TenantId == tenantId && u.Id == userId && u.DepartmentId == a.DepartmentId)),
                cancellationToken);

        var items = rows
            .Select(row => new MyWorkItemData(
                new ProjectWorkItemData(
                    new WorkItemProgressInput(row.Id, row.Status, row.Priority, row.ChecklistTotal,
                        row.ChecklistDone, userId, row.AssignedDepartmentId,
                        ProjectDashboardReader.IsOverdue(row.Status, row.Deadline, now)),
                    row.Title,
                    row.Deadline,
                    row.UpdatedAt,
                    AssigneeName: null,
                    AssigneeEmployeeCode: null,
                    AssigneeDepartmentName: null,
                    VisibleToCaller: true),
                row.ParentTaskId,
                row.ParentTitle))
            .ToArray();

        var projects = await LoadMyProjectsAsync(scope, now, cancellationToken);
        return new MyWorkSlice(items, total, projects, pendingClaims);
    }

    /// <summary>
    /// The projects this caller takes part in, each with the project-wide work items so the
    /// shared progress figure matches what the project dashboard shows.
    /// </summary>
    private async Task<IReadOnlyList<ProjectSummaryData>> LoadMyProjectsAsync(TaskReadScope scope,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        var tenantId = scope.TenantId;
        var userId = scope.UserId;

        var projectIds = await db.WorkTasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.DeletedAt == null && t.ParentTaskId != null &&
                db.TaskAssignments.Any(a => a.TaskId == t.Id && a.EndedAt == null && a.UserId == userId))
            .Select(t => t.ParentTaskId!.Value)
            .Distinct()
            .OrderBy(id => id)
            .Take(MyProjectLimit)
            .ToListAsync(cancellationToken);

        // Reuse the project reader so visibility, ownership, classification and overdue rules
        // are identical to the project dashboard instead of being restated here.
        return await projects.SummariesAsync(scope, projectIds, now, cancellationToken);
    }

    // A personal dashboard lists the projects behind the caller's own work, not a catalogue.
    private const int MyProjectLimit = 20;

    private sealed record MineRow(
        Guid Id, string Title, TaskState Status, TaskPriority Priority, DateTimeOffset? Deadline,
        DateTimeOffset UpdatedAt, Guid? ParentTaskId, string? ParentTitle,
        int ChecklistTotal, int ChecklistDone, Guid? AssignedDepartmentId);
}
