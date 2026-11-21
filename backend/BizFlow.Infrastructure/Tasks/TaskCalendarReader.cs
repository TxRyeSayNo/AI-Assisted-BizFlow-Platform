using BizFlow.Application.Tasks;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Tasks;

/// <summary>
/// Reads the dated tasks inside a calendar window, ordered by deadline. Starts from the same
/// <see cref="TaskReadQueries.Visible"/> gate as the list, detail and board readers, so it
/// inherits the resource scope and the fail-closed tenant filter on <see cref="WorkTask"/>.
/// </summary>
public sealed class TaskCalendarReader(BizFlowDbContext db) : ITaskCalendarReader
{
    public async Task<(IReadOnlyList<TaskListRow> Items, bool Truncated)> ReadAsync(TaskReadScope scope,
        TaskCalendarFilter filter, CancellationToken cancellationToken)
    {
        var query = TaskReadQueries.Visible(db, scope)
            .Where(t => t.Deadline != null && t.Deadline >= filter.From && t.Deadline <= filter.To);
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
        if (filter.AssignedUserId is { } assignedUser)
            query = query.Where(t => db.TaskAssignments.Any(a => a.TaskId == t.Id && a.EndedAt == null && a.UserId == assignedUser));
        if (filter.AssignedDepartmentId is { } assignedDepartment)
            query = query.Where(t => db.TaskAssignments.Any(a => a.TaskId == t.Id && a.EndedAt == null && a.DepartmentId == assignedDepartment));

        // One extra row reveals truncation without a second COUNT over the same window.
        var rows = await query.OrderBy(t => t.Deadline).ThenBy(t => t.Id)
            .Take(filter.Limit + 1)
            .Select(t => new
            {
                t.Id, t.Title, t.Status, t.Priority, t.Deadline, t.CreatedAt, t.CreatorId, t.RequestId, t.ParentTaskId,
                SubtaskCount = db.WorkTasks.Count(st => st.TenantId == scope.TenantId && st.ParentTaskId == t.Id && st.DeletedAt == null),
                CompletedSubtaskCount = db.WorkTasks.Count(st => st.TenantId == scope.TenantId && st.ParentTaskId == t.Id && (st.Status == TaskState.Completed || st.Status == TaskState.Confirmed) && st.DeletedAt == null),
                Assignment = db.TaskAssignments.Where(a => a.TaskId == t.Id && a.EndedAt == null)
                    .Select(a => new
                    {
                        a.UserId, a.DepartmentId,
                        UserName = db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == a.UserId).Select(u => u.FullName).FirstOrDefault(),
                        DepartmentName = db.Departments.Where(d => d.TenantId == scope.TenantId && d.Id == a.DepartmentId).Select(d => d.Name).FirstOrDefault()
                    }).FirstOrDefault()
            }).ToListAsync(cancellationToken);

        var truncated = rows.Count > filter.Limit;
        return (rows.Take(filter.Limit).Select(t => new TaskListRow(t.Id, t.Title, TaskListCodes.State(t.Status),
            TaskListCodes.Priority(t.Priority), t.Deadline, t.CreatedAt, t.CreatorId, t.RequestId,
            t.Assignment?.UserId, t.Assignment?.UserName, t.Assignment?.DepartmentId,
            t.Assignment?.DepartmentName, t.ParentTaskId, t.SubtaskCount, t.CompletedSubtaskCount)).ToArray(), truncated);
    }
}
