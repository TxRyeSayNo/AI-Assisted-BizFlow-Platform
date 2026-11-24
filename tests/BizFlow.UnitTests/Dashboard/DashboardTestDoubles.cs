using BizFlow.Application.Dashboard;
using BizFlow.Application.Security;
using BizFlow.Domain.Dashboard;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Dashboard;

internal sealed record FakeContext(Guid? UserId, Guid? TenantId) : ITenantContext;

internal sealed class FakeSnapshots(AccessSnapshot? snapshot) : IAccessSnapshotProvider
{
    public Task<AccessSnapshot?> ResolveAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken) =>
        Task.FromResult(snapshot);
}

internal sealed class FakeAudit : ISecurityAuditWriter
{
    public int Calls { get; private set; }
    public Guid? User { get; private set; }
    public Guid? Tenant { get; private set; }
    public string? Operation { get; private set; }
    public AccessDenial Reason { get; private set; }

    public Task RecordDeniedAccessAsync(Guid? userId, Guid? tenantId, string permission, AccessDenial reason,
        CancellationToken cancellationToken)
    {
        Calls++; User = userId; Tenant = tenantId; Operation = permission; Reason = reason;
        return Task.CompletedTask;
    }
}

internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

internal sealed class FakeProjectReader(ProjectListSlice? slice = null, ProjectData? detail = null) : IProjectDashboardReader
{
    public int ListCalls { get; private set; }
    public int GetCalls { get; private set; }
    public ProjectDashboardFilter? Filter { get; private set; }
    public TaskReadScope? Scope { get; private set; }
    public Guid RequestedProjectId { get; private set; }

    public Task<ProjectListSlice> ListAsync(TaskReadScope scope, ProjectDashboardFilter filter,
        ProjectSizeThresholds thresholds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        ListCalls++; Filter = filter; Scope = scope;
        return Task.FromResult(slice ?? new ProjectListSlice([], 0));
    }

    public Task<ProjectData?> GetAsync(TaskReadScope scope, Guid projectId, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        GetCalls++; Scope = scope; RequestedProjectId = projectId;
        return Task.FromResult(detail);
    }

    public Task<IReadOnlyList<ProjectSummaryData>> SummariesAsync(TaskReadScope scope,
        IReadOnlyCollection<Guid> projectIds, DateTimeOffset now, CancellationToken cancellationToken)
    {
        Scope = scope;
        return Task.FromResult<IReadOnlyList<ProjectSummaryData>>(slice?.Items ?? []);
    }
}

internal sealed class FakeMyWorkReader(MyWorkSlice? slice = null) : IMyWorkDashboardReader
{
    public int Calls { get; private set; }
    public MyWorkFilter? Filter { get; private set; }
    public TaskReadScope? Scope { get; private set; }

    public Task<MyWorkSlice> GetAsync(TaskReadScope scope, MyWorkFilter filter, DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        Calls++; Filter = filter; Scope = scope;
        return Task.FromResult(slice ?? new MyWorkSlice([], 0, [], 0));
    }
}

internal sealed class FakeTeamReader(TeamSlice? slice = null) : ITeamDashboardReader
{
    public int Calls { get; private set; }
    public IReadOnlySet<Guid>? DepartmentIds { get; private set; }
    public bool DepartmentIdsWereNull { get; private set; }
    public TaskReadScope? Scope { get; private set; }
    public Guid TenantId => Scope?.TenantId ?? Guid.Empty;
    public int MemberLimit { get; private set; }

    public Task<TeamSlice> GetAsync(TaskReadScope scope, IReadOnlySet<Guid>? departmentIds, int memberLimit,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        Calls++; Scope = scope; DepartmentIds = departmentIds;
        DepartmentIdsWereNull = departmentIds is null; MemberLimit = memberLimit;
        return Task.FromResult(slice ?? new TeamSlice([], [], false));
    }
}

internal static class DashboardFixtures
{
    internal static readonly DateTimeOffset Now = new(2026, 11, 19, 8, 0, 0, TimeSpan.Zero);

    internal static AccessSnapshot Snapshot(Guid userId, Guid tenantId,
        IReadOnlyCollection<PermissionGrant>? grants = null, IReadOnlySet<Guid>? managedDepartments = null) =>
        new(userId, tenantId, true, true,
            grants ?? [new(TaskReadPolicy.TenantPermission, PermissionScope.Tenant)],
            new HashSet<Guid>(), managedDepartments ?? new HashSet<Guid>());

    internal static IReadOnlyCollection<PermissionGrant> TenantReader =>
        [new(TaskReadPolicy.TenantPermission, PermissionScope.Tenant)];

    internal static IReadOnlyCollection<PermissionGrant> ManagerReader =>
    [
        new(TaskReadPolicy.ManagedPermission, PermissionScope.Department),
        new(TaskReadPolicy.OwnPermission, PermissionScope.Self),
        new(TaskReadPolicy.AssignedPermission, PermissionScope.Assigned)
    ];

    internal static IReadOnlyCollection<PermissionGrant> EmployeeReader =>
    [
        new(TaskReadPolicy.OwnPermission, PermissionScope.Self),
        new(TaskReadPolicy.AssignedPermission, PermissionScope.Assigned)
    ];

    internal static ProjectHeaderData Header(Guid projectId, Guid? ownerUserId = null) =>
        new(projectId, "Dự án mẫu", TaskState.InProgress, TaskPriority.Medium,
            Now.AddDays(10), Now.AddDays(-20), Now.AddDays(-1),
            ownerUserId, ownerUserId is null ? null : "Quản lý dự án", null, "Phòng Kỹ thuật");

    internal static WorkItemProgressInput Progress(TaskState status, TaskPriority priority,
        Guid? user = null, Guid? department = null, bool overdue = false,
        int checklistTotal = 0, int checklistDone = 0, Guid? taskId = null) =>
        new(taskId ?? Guid.NewGuid(), status, priority, checklistTotal, checklistDone, user, department, overdue);

    internal static ProjectWorkItemData WorkItem(WorkItemProgressInput progress, bool visible,
        string title = "Phần việc", string? assigneeName = null) =>
        new(progress, title, Now.AddDays(5), Now, assigneeName, assigneeName is null ? null : "EMP001",
            "Phòng Kỹ thuật", visible);
}
