using System.Linq.Expressions;
using BizFlow.Application.Dashboard;
using BizFlow.Domain.Dashboard;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using BizFlow.Infrastructure.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Dashboard;

/// <summary>
/// Reads projects as they already exist in storage: a parent task that owns work items. Every
/// query is additionally constrained to <c>scope.TenantId</c> on top of the context's global
/// tenant filter, and candidate visibility is delegated to <see cref="TaskReadQueries.Visible"/>
/// so a dashboard can never reveal a task the task screens would hide.
/// </summary>
public sealed class ProjectDashboardReader(BizFlowDbContext db) : IProjectDashboardReader
{
    public async Task<ProjectListSlice> ListAsync(TaskReadScope scope, ProjectDashboardFilter filter,
        ProjectSizeThresholds thresholds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(filter);
        ArgumentNullException.ThrowIfNull(thresholds);

        var tenantId = scope.TenantId;
        var query = Candidates(scope);

        if (filter.Search is { } search)
        {
            var normalized = search.ToUpperInvariant();
            query = query.Where(t => t.Title.ToUpper().Contains(normalized));
        }

        if (filter.DepartmentId is { } department)
        {
            // A project belongs to a department when the department, or one of its members,
            // currently holds the parent task or any of its work items.
            query = query.Where(t =>
                db.TaskAssignments.Any(a => a.EndedAt == null && a.TaskId == t.Id &&
                    (a.DepartmentId == department ||
                        (a.UserId != null && db.Users.Any(u => u.TenantId == tenantId && u.Id == a.UserId && u.DepartmentId == department)))) ||
                db.WorkTasks.Any(st => st.TenantId == tenantId && st.DeletedAt == null && st.ParentTaskId == t.Id &&
                    db.TaskAssignments.Any(a => a.EndedAt == null && a.TaskId == st.Id &&
                        (a.DepartmentId == department ||
                            (a.UserId != null && db.Users.Any(u => u.TenantId == tenantId && u.Id == a.UserId && u.DepartmentId == department))))));
        }

        if (filter.Size is "LARGE" or "SMALL")
        {
            // The same rule as ProjectDashboardPolicy.Classify, evaluated in SQL so the page
            // boundary and the total both refer to the filtered set.
            var minWorkItems = thresholds.MinWorkItems;
            var minParticipants = thresholds.MinParticipants;
            var large = filter.Size == "LARGE";
            query = query.Where(t =>
                (db.WorkTasks.Count(st => st.TenantId == tenantId && st.DeletedAt == null &&
                    st.ParentTaskId == t.Id && st.Status != TaskState.Cancelled) >= minWorkItems ||
                 db.TaskAssignments.Where(a => a.EndedAt == null && a.UserId != null &&
                        db.WorkTasks.Any(st => st.Id == a.TaskId && st.TenantId == tenantId && st.DeletedAt == null &&
                            st.ParentTaskId == t.Id && st.Status != TaskState.Cancelled))
                     .Select(a => a.UserId).Distinct().Count() >= minParticipants) == large);
        }

        var total = await query.LongCountAsync(cancellationToken);
        var headers = await query
            .OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
            .Skip(checked((filter.Page - 1) * filter.PageSize)).Take(filter.PageSize)
            .Select(HeaderProjection(tenantId))
            .ToListAsync(cancellationToken);

        var slice = await BuildSummariesAsync(tenantId, headers, now, cancellationToken);
        return new ProjectListSlice(slice, total);
    }

    public async Task<IReadOnlyList<ProjectSummaryData>> SummariesAsync(TaskReadScope scope,
        IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(projectIds);
        if (projectIds.Count == 0) return [];

        var headers = await Candidates(scope)
            .Where(t => projectIds.Contains(t.Id))
            .OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
            .Select(HeaderProjection(scope.TenantId))
            .ToListAsync(cancellationToken);
        return await BuildSummariesAsync(scope.TenantId, headers, now, cancellationToken);
    }

    private async Task<IReadOnlyList<ProjectSummaryData>> BuildSummariesAsync(Guid tenantId,
        List<HeaderRow> headers, DateTimeOffset now, CancellationToken cancellationToken)
    {
        if (headers.Count == 0) return [];
        var ids = headers.Select(x => x.Id).ToArray();
        var items = await LoadWorkItemRowsAsync(tenantId, ids, cancellationToken);
        var names = await ResolveNamesAsync(tenantId, headers, items, cancellationToken);
        var grouped = items.GroupBy(x => x.ParentId).ToDictionary(x => x.Key, x => x.ToArray());
        return headers
            .Select(header => new ProjectSummaryData(
                Header(header, names),
                (grouped.TryGetValue(header.Id, out var rows) ? rows : Array.Empty<WorkItemRow>())
                    .Select(row => Progress(row, now)).ToArray()))
            .ToArray();
    }

    public async Task<ProjectData?> GetAsync(TaskReadScope scope, Guid projectId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var tenantId = scope.TenantId;

        var header = await Candidates(scope)
            .Where(t => t.Id == projectId)
            .Select(HeaderProjection(tenantId))
            .FirstOrDefaultAsync(cancellationToken);
        if (header is null) return null;

        var items = await LoadWorkItemRowsAsync(tenantId, [projectId], cancellationToken);
        var names = await ResolveNamesAsync(tenantId, [header], items, cancellationToken);

        // The caller's own task-read scope decides which rows may be disclosed; the aggregate
        // over every row stays available so each viewer of the project sees the same totals.
        var visible = await TaskReadQueries.Visible(db, scope)
            .Where(t => t.ParentTaskId == projectId)
            .Select(t => t.Id)
            .ToListAsync(cancellationToken);
        var visibleIds = visible.ToHashSet();

        var workItems = items
            .OrderBy(x => x.Deadline ?? DateTimeOffset.MaxValue).ThenBy(x => x.Id)
            .Select(row => new ProjectWorkItemData(
                Progress(row, now),
                row.Title,
                row.Deadline,
                row.UpdatedAt,
                row.AssignedUserId is { } user && names.Users.TryGetValue(user, out var profile) ? profile.FullName : null,
                row.AssignedUserId is { } coded && names.Users.TryGetValue(coded, out var codedProfile) ? codedProfile.EmployeeCode : null,
                DepartmentName(row.AssignedDepartmentId, row.AssignedUserId, names),
                visibleIds.Contains(row.Id)))
            .ToArray();

        return new ProjectData(Header(header, names), workItems);
    }

    // ---------------------------------------------------------------------------------------
    // Query building
    // ---------------------------------------------------------------------------------------

    /// <summary>
    /// Parent tasks of this tenant that own at least one live work item and that the caller can
    /// reach either directly or through a work item they hold. The second branch is what lets an
    /// employee open the shared header of a project they participate in.
    /// </summary>
    private IQueryable<WorkTask> Candidates(TaskReadScope scope)
    {
        var tenantId = scope.TenantId;
        var visible = TaskReadQueries.Visible(db, scope);
        return db.WorkTasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.ParentTaskId == null && t.DeletedAt == null &&
                (t.TaskKind == null || t.TaskKind == "PROJECT") &&
                db.WorkTasks.Any(st => st.TenantId == tenantId && st.DeletedAt == null && st.ParentTaskId == t.Id) &&
                (visible.Any(v => v.Id == t.Id) || visible.Any(v => v.ParentTaskId == t.Id)));
    }

    private Expression<Func<WorkTask, HeaderRow>> HeaderProjection(Guid tenantId) => t => new HeaderRow(
        t.Id,
        t.Title,
        t.Status,
        t.Priority,
        t.Deadline,
        t.CreatedAt,
        t.UpdatedAt,
        t.CreatorId,
        db.TaskAssignments.Where(a => a.TaskId == t.Id && a.EndedAt == null)
            .OrderByDescending(a => a.AssignedAt).Select(a => a.UserId).FirstOrDefault(),
        db.TaskAssignments.Where(a => a.TaskId == t.Id && a.EndedAt == null)
            .OrderByDescending(a => a.AssignedAt).Select(a => a.DepartmentId).FirstOrDefault());

    private async Task<List<WorkItemRow>> LoadWorkItemRowsAsync(Guid tenantId, IReadOnlyCollection<Guid> projectIds,
        CancellationToken cancellationToken) =>
        await db.WorkTasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.DeletedAt == null &&
                t.ParentTaskId != null && projectIds.Contains(t.ParentTaskId.Value))
            .Select(t => new WorkItemRow(
                t.ParentTaskId!.Value,
                t.Id,
                t.Title,
                t.Status,
                t.Priority,
                t.Deadline,
                t.UpdatedAt,
                db.TaskChecklistItems.Count(c => c.TaskId == t.Id),
                db.TaskChecklistItems.Count(c => c.TaskId == t.Id && c.IsCompleted),
                db.TaskAssignments.Where(a => a.TaskId == t.Id && a.EndedAt == null)
                    .OrderByDescending(a => a.AssignedAt).Select(a => a.UserId).FirstOrDefault(),
                db.TaskAssignments.Where(a => a.TaskId == t.Id && a.EndedAt == null)
                    .OrderByDescending(a => a.AssignedAt).Select(a => a.DepartmentId).FirstOrDefault()))
            .ToListAsync(cancellationToken);

    /// <summary>
    /// Resolves display names in two tenant-scoped lookups instead of per-row correlated
    /// subqueries. A soft-deleted account is filtered out by the context, so its name simply
    /// resolves to null rather than leaking a removed record.
    /// </summary>
    private async Task<NameCache> ResolveNamesAsync(Guid tenantId, IReadOnlyCollection<HeaderRow> headers,
        IReadOnlyCollection<WorkItemRow> items, CancellationToken cancellationToken)
    {
        var userIds = headers.Select(x => x.AssignedUserId).Concat(headers.Select(x => (Guid?)x.CreatorId))
            .Concat(items.Select(x => x.AssignedUserId))
            .Where(x => x is not null).Select(x => x!.Value).Distinct().ToArray();
        var departmentIds = headers.Select(x => x.AssignedDepartmentId)
            .Concat(items.Select(x => x.AssignedDepartmentId))
            .Where(x => x is not null).Select(x => x!.Value).Distinct().ToArray();

        var users = new List<UserRow>();
        if (userIds.Length != 0)
        {
            users = await db.Users.AsNoTracking()
                .Where(u => u.TenantId == tenantId && userIds.Contains(u.Id))
                .Select(u => new UserRow(u.Id, u.FullName, u.EmployeeCode, u.DepartmentId))
                .ToListAsync(cancellationToken);
        }

        var referenced = departmentIds
            .Concat(users.Select(u => u.DepartmentId).Where(x => x is not null).Select(x => x!.Value))
            .Distinct().ToArray();
        var departments = new List<DepartmentRow>();
        if (referenced.Length != 0)
        {
            departments = await db.Departments.AsNoTracking()
                .Where(d => d.TenantId == tenantId && referenced.Contains(d.Id))
                .Select(d => new DepartmentRow(d.Id, d.Name))
                .ToListAsync(cancellationToken);
        }

        return new NameCache(
            users.ToDictionary(u => u.Id),
            departments.ToDictionary(d => d.Id, d => d.Name));
    }

    // ---------------------------------------------------------------------------------------
    // Mapping
    // ---------------------------------------------------------------------------------------

    private static ProjectHeaderData Header(HeaderRow row, NameCache names)
    {
        // The parent task's current assignee owns the project; with none, the creator does.
        var ownerId = row.AssignedUserId ?? row.CreatorId;
        names.Users.TryGetValue(ownerId, out var owner);
        var departmentId = row.AssignedDepartmentId ?? owner?.DepartmentId;
        return new ProjectHeaderData(
            row.Id,
            row.Title,
            row.Status,
            row.Priority,
            row.Deadline,
            row.CreatedAt,
            row.UpdatedAt,
            ownerId,
            owner?.FullName,
            departmentId,
            departmentId is { } id && names.Departments.TryGetValue(id, out var name) ? name : null);
    }

    private static WorkItemProgressInput Progress(WorkItemRow row, DateTimeOffset now) => new(
        row.Id,
        row.Status,
        row.Priority,
        row.ChecklistTotal,
        row.ChecklistDone,
        row.AssignedUserId,
        row.AssignedDepartmentId,
        IsOverdue(row.Status, row.Deadline, now));

    // The same overdue rule the existing manager report applies, so both screens agree.
    internal static bool IsOverdue(TaskState status, DateTimeOffset? deadline, DateTimeOffset now) =>
        status == TaskState.Overdue ||
        (status is not (TaskState.Completed or TaskState.Cancelled) && deadline.HasValue && deadline.Value < now);

    private static string? DepartmentName(Guid? assignedDepartmentId, Guid? assignedUserId, NameCache names)
    {
        if (assignedDepartmentId is { } assigned)
            return names.Departments.TryGetValue(assigned, out var direct) ? direct : null;
        if (assignedUserId is { } user && names.Users.TryGetValue(user, out var profile) &&
            profile.DepartmentId is { } owned)
            return names.Departments.TryGetValue(owned, out var inherited) ? inherited : null;
        return null;
    }

    private sealed record HeaderRow(
        Guid Id, string Title, TaskState Status, TaskPriority Priority, DateTimeOffset? Deadline,
        DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt, Guid CreatorId,
        Guid? AssignedUserId, Guid? AssignedDepartmentId);

    private sealed record WorkItemRow(
        Guid ParentId, Guid Id, string Title, TaskState Status, TaskPriority Priority,
        DateTimeOffset? Deadline, DateTimeOffset UpdatedAt, int ChecklistTotal, int ChecklistDone,
        Guid? AssignedUserId, Guid? AssignedDepartmentId);

    private sealed record UserRow(Guid Id, string FullName, string EmployeeCode, Guid? DepartmentId);

    private sealed record DepartmentRow(Guid Id, string Name);

    private sealed record NameCache(
        IReadOnlyDictionary<Guid, UserRow> Users,
        IReadOnlyDictionary<Guid, string> Departments);
}
