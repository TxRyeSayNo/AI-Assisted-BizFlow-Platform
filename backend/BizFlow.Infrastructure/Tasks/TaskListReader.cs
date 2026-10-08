using BizFlow.Application.Tasks;
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
        var total = await query.LongCountAsync(cancellationToken);
        var rows = await query.OrderByDescending(t => t.CreatedAt).ThenBy(t => t.Id)
            .Skip(checked((filter.Page - 1) * filter.PageSize)).Take(filter.PageSize)
            .Select(t => new
            {
                t.Id, t.Title, t.Status, t.Priority, t.Deadline, t.CreatedAt, t.CreatorId, t.RequestId,
                Assignment = db.TaskAssignments.Where(a => a.TaskId == t.Id && a.EndedAt == null)
                    .Select(a => new
                    {
                        a.UserId, a.DepartmentId,
                        UserName = db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == a.UserId).Select(u => u.FullName).FirstOrDefault(),
                        DepartmentName = db.Departments.Where(d => d.TenantId == scope.TenantId && d.Id == a.DepartmentId).Select(d => d.Name).FirstOrDefault()
                    }).FirstOrDefault()
            }).ToListAsync(cancellationToken);
        return new(rows.Select(t => new TaskListRow(t.Id, t.Title, TaskListCodes.State(t.Status), TaskListCodes.Priority(t.Priority),
            t.Deadline, t.CreatedAt, t.CreatorId, t.RequestId, t.Assignment?.UserId, t.Assignment?.UserName,
            t.Assignment?.DepartmentId, t.Assignment?.DepartmentName)).ToArray(), filter.Page, filter.PageSize, total);
    }
}
