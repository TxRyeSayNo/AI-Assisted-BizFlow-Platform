using BizFlow.Application.Tasks;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Tasks;

/// <summary>
/// Groups the visible tasks into one lane per lifecycle state for the Kanban board. It starts from
/// the same <see cref="TaskReadQueries.Visible"/> gate the list and detail readers use, so the
/// board inherits both the resource scope and the fail-closed tenant query filter on
/// <see cref="WorkTask"/>; it never widens either and never calls IgnoreQueryFilters.
/// </summary>
public sealed class TaskBoardReader(BizFlowDbContext db) : ITaskBoardReader
{
    public async Task<IReadOnlyList<TaskBoardColumn>> ReadAsync(TaskReadScope scope, TaskBoardFilter filter,
        CancellationToken cancellationToken)
    {
        var narrowed = Narrow(TaskReadQueries.Visible(db, scope), filter);
        // One grouped count tells every lane its real size, so a capped lane can still report
        // "showing 50 of 214" without a follow-up request per column.
        var totals = await narrowed.GroupBy(t => t.Status)
            .Select(group => new { Status = group.Key, Total = group.LongCount() })
            .ToListAsync(cancellationToken);
        var columns = new List<TaskBoardColumn>();
        foreach (var state in Enum.GetValues<TaskState>())
        {
            var total = totals.SingleOrDefault(candidate => candidate.Status == state)?.Total ?? 0;
            if (total == 0)
            {
                columns.Add(new(TaskListCodes.State(state), [], 0));
                continue;
            }
            // Soonest deadline first and undated work last: the order a board is read in.
            var rows = await narrowed.Where(t => t.Status == state)
                .OrderBy(t => t.Deadline == null).ThenBy(t => t.Deadline)
                .ThenByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
                .Take(filter.ColumnSize)
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
            columns.Add(new(TaskListCodes.State(state),
                rows.Select(t => new TaskListRow(t.Id, t.Title, TaskListCodes.State(t.Status),
                    TaskListCodes.Priority(t.Priority), t.Deadline, t.CreatedAt, t.CreatorId, t.RequestId,
                    t.Assignment?.UserId, t.Assignment?.UserName, t.Assignment?.DepartmentId,
                    t.Assignment?.DepartmentName, t.ParentTaskId, t.SubtaskCount, t.CompletedSubtaskCount)).ToArray(),
                total));
        }
        return columns;
    }

    private IQueryable<WorkTask> Narrow(IQueryable<WorkTask> query, TaskBoardFilter filter)
    {
        if (filter.Search is { } search)
        {
            var normalized = search.ToUpperInvariant();
            query = query.Where(t => t.Title.ToUpper().Contains(normalized));
        }
        if (filter.Priority is { } priority)
        {
            var value = Enum.GetValues<TaskPriority>().Single(p => TaskListCodes.Priority(p) == priority);
            query = query.Where(t => t.Priority == value);
        }
        if (filter.RequestId is { } requestId) query = query.Where(t => t.RequestId == requestId);
        if (filter.IsParentOnly == true) query = query.Where(t => t.ParentTaskId == null);
        if (filter.DeadlineFrom is { } from) query = query.Where(t => t.Deadline != null && t.Deadline >= from);
        if (filter.DeadlineTo is { } to) query = query.Where(t => t.Deadline != null && t.Deadline <= to);
        // The live assignment is the one that has not ended, matching how the list reader resolves it.
        if (filter.AssignedUserId is { } assignedUser)
            query = query.Where(t => db.TaskAssignments.Any(a => a.TaskId == t.Id && a.EndedAt == null && a.UserId == assignedUser));
        if (filter.AssignedDepartmentId is { } assignedDepartment)
            query = query.Where(t => db.TaskAssignments.Any(a => a.TaskId == t.Id && a.EndedAt == null && a.DepartmentId == assignedDepartment));
        // F6: only tasks still carrying the label, so a detached label stops narrowing the board.
        if (filter.LabelId is { } labelId)
            query = query.Where(t => db.WorkItemLabels.Any(l => l.ObjectId == t.Id &&
                l.ObjectType == BizFlow.Domain.Collaboration.CommentObjectType.Task &&
                l.LabelId == labelId && l.RemovedAt == null));
        return query;
    }
}
