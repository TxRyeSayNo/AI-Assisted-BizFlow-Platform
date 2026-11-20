using BizFlow.Application.Dashboard;
using BizFlow.Domain.Dashboard;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using BizFlow.Infrastructure.Tasks;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Dashboard;

/// <summary>
/// Reads the members of the departments the caller manages, together with the work they hold.
/// The department set is resolved and authorized by <see cref="TeamDashboardQuery"/> from the
/// caller's live management scope; this reader never widens it and never falls back to "all"
/// when the set is empty.
/// </summary>
public sealed class TeamDashboardReader(BizFlowDbContext db) : ITeamDashboardReader
{
    public async Task<TeamSlice> GetAsync(TaskReadScope scope, IReadOnlySet<Guid>? departmentIds, int memberLimit,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(scope);
        var tenantId = scope.TenantId;
        if (tenantId == Guid.Empty) throw new ArgumentOutOfRangeException(nameof(scope));
        if (memberLimit < 1) throw new ArgumentOutOfRangeException(nameof(memberLimit));

        // An authorized but empty scope manages nobody; it must not degrade into a tenant read.
        if (departmentIds is { Count: 0 }) return new TeamSlice([], [], false);

        var scoped = departmentIds?.ToArray();

        var departments = await db.Departments.AsNoTracking()
            .Where(d => d.TenantId == tenantId && (scoped == null || scoped.Contains(d.Id)))
            .Select(d => new DepartmentRow(d.Id, d.Name, d.ParentDepartmentId,
                db.Users.Count(u => u.TenantId == tenantId && u.DepartmentId == d.Id)))
            .ToListAsync(cancellationToken);

        var memberQuery = db.Users.AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.DepartmentId != null &&
                (scoped == null || scoped.Contains(u.DepartmentId!.Value)));

        var memberCount = await memberQuery.CountAsync(cancellationToken);
        var members = await memberQuery
            .OrderBy(u => u.FullName).ThenBy(u => u.Id)
            .Take(memberLimit)
            .Select(u => new MemberRow(u.Id, u.FullName, u.EmployeeCode, u.DepartmentId, u.Status))
            .ToListAsync(cancellationToken);

        if (members.Count == 0)
        {
            return new TeamSlice(
                departments.Select(Department).ToArray(),
                [],
                Truncated: memberCount > memberLimit);
        }

        var memberIds = members.Select(x => x.Id).ToArray();
        // Intersected with the caller's own task-read scope: a team total can only be made of
        // tasks this caller is already allowed to read.
        var work = await db.TaskAssignments.AsNoTracking()
            .Where(a => a.EndedAt == null && a.UserId != null && memberIds.Contains(a.UserId!.Value))
            .Join(TaskReadQueries.Visible(db, scope).Where(t => t.TenantId == tenantId),
                a => a.TaskId,
                t => t.Id,
                (a, t) => new WorkRow(
                    a.UserId!.Value,
                    t.Id,
                    t.Status,
                    t.Priority,
                    t.Deadline,
                    a.DepartmentId,
                    db.TaskChecklistItems.Count(c => c.TaskId == t.Id),
                    db.TaskChecklistItems.Count(c => c.TaskId == t.Id && c.IsCompleted)))
            .ToListAsync(cancellationToken);

        var byMember = work.GroupBy(x => x.UserId).ToDictionary(x => x.Key, x => x.ToArray());
        var departmentNames = departments.ToDictionary(x => x.Id, x => x.Name);

        var memberData = members
            .Select(member => new TeamMemberData(
                member.Id,
                member.FullName,
                member.EmployeeCode,
                member.DepartmentId,
                member.DepartmentId is { } id && departmentNames.TryGetValue(id, out var name) ? name : null,
                member.Status.ToString().ToUpperInvariant(),
                (byMember.TryGetValue(member.Id, out var rows) ? rows : Array.Empty<WorkRow>())
                    .Select(row => new WorkItemProgressInput(
                        row.TaskId,
                        row.Status,
                        row.Priority,
                        row.ChecklistTotal,
                        row.ChecklistDone,
                        row.UserId,
                        row.DepartmentId,
                        ProjectDashboardReader.IsOverdue(row.Status, row.Deadline, now)))
                    .ToArray()))
            .ToArray();

        return new TeamSlice(
            departments.Select(Department).ToArray(),
            memberData,
            Truncated: memberCount > memberLimit);
    }

    private static TeamDepartmentData Department(DepartmentRow row) =>
        new(row.Id, row.Name, row.ParentDepartmentId, row.MemberCount);

    private sealed record DepartmentRow(Guid Id, string Name, Guid? ParentDepartmentId, int MemberCount);

    private sealed record MemberRow(Guid Id, string FullName, string EmployeeCode, Guid? DepartmentId, UserStatus Status);

    private sealed record WorkRow(
        Guid UserId, Guid TaskId, TaskState Status, TaskPriority Priority, DateTimeOffset? Deadline,
        Guid? DepartmentId, int ChecklistTotal, int ChecklistDone);
}
