using BizFlow.Application.Tasks;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Tasks;

public sealed class TaskListReader(BizFlowDbContext db) : ITaskListReader
{
    public async Task<TaskListPage> ListAsync(TaskReadScope scope, TaskListFilter filter, CancellationToken cancellationToken)
    {
        var query = TaskReadQueries.Visible(db, scope);
        if (filter.Search is { } search)
        {
            var normalized = search.ToUpperInvariant();
            query = query.Where(t => t.Title.ToUpper().Contains(normalized));
        }
        if (filter.Status is { } status)
        {
            var state = Enum.GetValues<TaskState>().Single(s => TaskListCodes.State(s) == status);
            query = query.Where(t => t.Status == state);
        }
        if (filter.Priority is { } priority)
        {
            var value = Enum.GetValues<TaskPriority>().Single(p => TaskListCodes.Priority(p) == priority);
            query = query.Where(t => t.Priority == value);
        }
        if (filter.RequestId is { } reqId)
        {
            query = query.Where(t => t.RequestId == reqId);
        }
        if (filter.ParentTaskId is { } parentId)
        {
            query = query.Where(t => t.ParentTaskId == parentId);
        }
        else if (filter.IsParentOnly == true)
        {
            query = query.Where(t => t.ParentTaskId == null);
        }
        var now = DateTimeOffset.UtcNow;
        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
            .Skip(checked((filter.Page - 1) * filter.PageSize)).Take(filter.PageSize)
            .Select(t => new
            {
                t.Id, t.Title, t.Status, t.Priority, t.Deadline, t.CreatedAt, t.CreatorId, t.RequestId, t.ParentTaskId, t.TaskKind,
                CreatorName = db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == t.CreatorId && u.DeletedAt == null).Select(u => u.FullName).FirstOrDefault(),
                ParentTaskTitle = t.ParentTaskId == null
                    ? null
                    : db.WorkTasks.Where(pt => pt.TenantId == scope.TenantId && pt.Id == t.ParentTaskId && pt.DeletedAt == null)
                        .Select(pt => pt.Title).FirstOrDefault(),
                SubtaskCount = db.WorkTasks.Count(st => st.TenantId == scope.TenantId && st.ParentTaskId == t.Id && st.DeletedAt == null && st.Status != TaskState.Cancelled),
                CompletedSubtaskCount = db.WorkTasks.Count(st => st.TenantId == scope.TenantId && st.ParentTaskId == t.Id && (st.Status == TaskState.Completed || st.Status == TaskState.Confirmed) && st.DeletedAt == null),
                InProgressSubtaskCount = db.WorkTasks.Count(st => st.TenantId == scope.TenantId && st.ParentTaskId == t.Id && (st.Status == TaskState.Accepted || st.Status == TaskState.InProgress || st.Status == TaskState.Submitted) && st.DeletedAt == null),
                OverdueSubtaskCount = db.WorkTasks.Count(st => st.TenantId == scope.TenantId && st.ParentTaskId == t.Id && st.DeletedAt == null && (st.Status == TaskState.Overdue || (st.Status != TaskState.Completed && st.Status != TaskState.Confirmed && st.Status != TaskState.Cancelled && st.Deadline != null && st.Deadline < now))),
                ParticipantCount = db.TaskAssignments
                    .Where(a => a.EndedAt == null && a.UserId != null && db.WorkTasks.Any(st => st.TenantId == scope.TenantId && st.ParentTaskId == t.Id && st.Id == a.TaskId && st.DeletedAt == null && st.Status != TaskState.Cancelled))
                    .Select(a => a.UserId)
                    .Distinct()
                    .Count(),
                LatestReportPercent = db.TaskProgressReports
                    .Where(r => r.TaskId == t.Id)
                    .OrderByDescending(r => r.SubmittedAt)
                    .Select(r => (short?)r.Percent)
                    .FirstOrDefault(),
                Assignment = db.TaskAssignments.Where(a => a.TaskId == t.Id && a.EndedAt == null)
                    .Select(a => new
                    {
                        a.UserId, a.DepartmentId, a.AssignedBy,
                        UserName = db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == a.UserId).Select(u => u.FullName).FirstOrDefault(),
                        AssignedByName = db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == a.AssignedBy && u.DeletedAt == null).Select(u => u.FullName).FirstOrDefault(),
                        UserDepartmentId = db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == a.UserId).Select(u => u.DepartmentId).FirstOrDefault(),
                        UserDepartmentName = db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == a.UserId)
                            .SelectMany(u => db.Departments.Where(d => d.TenantId == scope.TenantId && d.Id == u.DepartmentId).Select(d => d.Name)).FirstOrDefault(),
                        DepartmentName = db.Departments.Where(d => d.TenantId == scope.TenantId && d.Id == a.DepartmentId).Select(d => d.Name).FirstOrDefault(),
                        IsUserAnActualManager = a.UserId != null &&
                            (db.UserRoles.Any(ur => ur.UserId == a.UserId && db.Roles.Any(r => r.Id == ur.RoleId && (r.Name == "MANAGER" || r.Name == "COMPANY_ADMIN"))) ||
                             db.ManagementScopes.Any(ms => ms.TenantId == scope.TenantId && ms.UserId == a.UserId)),
                        DepartmentManagerName = db.ManagementScopes.Where(ms => ms.TenantId == scope.TenantId && ms.DepartmentId == (a.DepartmentId ?? db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == a.UserId).Select(u => u.DepartmentId).FirstOrDefault()))
                            .SelectMany(ms => db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == ms.UserId && u.DeletedAt == null && u.Status == UserStatus.Active).Select(u => u.FullName))
                            .FirstOrDefault() ??
                            (from u in db.Users
                             join ur in db.UserRoles on u.Id equals ur.UserId
                             join r in db.Roles on ur.RoleId equals r.Id
                             where u.TenantId == scope.TenantId
                               && u.DepartmentId == (a.DepartmentId ?? db.Users.Where(u2 => u2.TenantId == scope.TenantId && u2.Id == a.UserId).Select(u2 => u2.DepartmentId).FirstOrDefault())
                               && u.DeletedAt == null
                               && u.Status == UserStatus.Active
                               && r.Name == "MANAGER"
                             select u.FullName).FirstOrDefault()
                    }).FirstOrDefault()
            }).ToListAsync(cancellationToken);

        return new(rows.Select(t =>
        {
            var progress = t.SubtaskCount > 0
                ? (int)Math.Round(t.CompletedSubtaskCount * 100.0 / t.SubtaskCount, MidpointRounding.AwayFromZero)
                : (t.Status == TaskState.Completed || t.Status == TaskState.Confirmed)
                    ? 100
                    : t.LatestReportPercent is not null
                        ? (int)t.LatestReportPercent.Value
                        : t.Status == TaskState.Submitted
                            ? 85
                            : (t.Status == TaskState.InProgress || t.Status == TaskState.Overdue)
                                ? 50
                                : t.Status == TaskState.Accepted
                                    ? 15
                                    : 0;
            var managerName = t.Assignment?.AssignedByName ??
                              t.CreatorName ??
                              t.Assignment?.DepartmentManagerName ??
                              (t.Assignment?.IsUserAnActualManager == true ? t.Assignment?.UserName : null);
            return new TaskListRow(
                t.Id, t.Title, TaskListCodes.State(t.Status), TaskListCodes.Priority(t.Priority),
                t.Deadline, t.CreatedAt, t.CreatorId, t.RequestId, t.Assignment?.UserId, t.Assignment?.UserName,
                t.Assignment?.DepartmentId ?? t.Assignment?.UserDepartmentId, t.Assignment?.DepartmentName ?? t.Assignment?.UserDepartmentName, t.ParentTaskId, t.SubtaskCount,
                t.CompletedSubtaskCount, progress, managerName, t.ParticipantCount,
                t.InProgressSubtaskCount, t.OverdueSubtaskCount, t.ParentTaskTitle, t.TaskKind);
        }).ToArray(), filter.Page, filter.PageSize, total);
    }
}
