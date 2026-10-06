using BizFlow.Domain.Common;
using BizFlow.Domain.Organization;

namespace BizFlow.Domain.Tasks;

public sealed class TaskAssignment
{
    public Guid Id { get; private set; }
    public Guid TaskId { get; private set; }
    public Guid? DepartmentId { get; private set; }
    public Guid? UserId { get; private set; }
    public Guid AssignedBy { get; private set; }
    public DateTimeOffset AssignedAt { get; private set; }
    public DateTimeOffset? AcceptedAt { get; private set; }
    public DateTimeOffset? RejectedAt { get; private set; }
    public string? RejectionReason { get; private set; }
    public DateTimeOffset? EndedAt { get; private set; }
    private TaskAssignment() { }

    // An initial responsibility record, not an assignment command. Application must authorize
    // tasks.assign/reassign, validate live management scope and orchestrate the workflow/audit.
    public static TaskAssignment Create(Guid taskId, Guid assignedBy, Guid? departmentId, Guid? userId, DateTimeOffset now)
    {
        if (departmentId is null && userId is null) throw new ArgumentException("An assignment target is required.");
        if (departmentId is { } department) EntityRules.Id(department, nameof(departmentId));
        if (userId is { } user) EntityRules.Id(user, nameof(userId));
        return new()
        {
            Id = Guid.CreateVersion7(), TaskId = EntityRules.Id(taskId, nameof(taskId)),
            AssignedBy = EntityRules.Id(assignedBy, nameof(assignedBy)), DepartmentId = departmentId,
            UserId = userId, AssignedAt = now.ToUniversalTime()
        };
    }

    // Server-loaded parent/recipient facts; permissions, tenant eligibility, confirmation and
    // audit still belong to the Application transaction. A department claim never rewrites origin.
    public void Accept(WorkTask task, UserAccount recipient, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(task); ArgumentNullException.ThrowIfNull(recipient);
        if (task.Id != TaskId || task.Status != TaskState.Assigned || task.DeletedAt is not null ||
            AcceptedAt is not null || RejectedAt is not null || EndedAt is not null)
            throw new InvalidOperationException("Only an undecided current assignment on an assigned task can be accepted.");
        if (recipient.TenantId != task.TenantId || recipient.Status != UserStatus.Active || recipient.DeletedAt is not null ||
            (UserId is { } target ? target != recipient.Id : DepartmentId is null || recipient.DepartmentId != DepartmentId))
            throw new InvalidOperationException("The active assigned user or department member must accept.");
        var acceptedAt = now.ToUniversalTime();
        if (acceptedAt < AssignedAt) throw new ArgumentOutOfRangeException(nameof(now), "Acceptance cannot precede assignment.");
        UserId ??= recipient.Id;
        AcceptedAt = acceptedAt;
    }
}
