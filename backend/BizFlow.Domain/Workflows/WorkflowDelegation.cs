using BizFlow.Domain.Common;

namespace BizFlow.Domain.Workflows;

/// <summary>One accountable candidate slot, temporarily exercised by a different person. Never erases the original snapshot.</summary>
public sealed class WorkflowDelegation
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InstanceId { get; private set; }
    public Guid StepExecutionId { get; private set; }
    public Guid PrincipalId { get; private set; }
    public Guid DelegateId { get; private set; }
    public Guid CreatedBy { get; private set; }
    public string Reason { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? RevokedBy { get; private set; }
    private WorkflowDelegation() { }
    public bool IsActive(DateTimeOffset now) => RevokedAt is null && CreatedAt <= now && now < ExpiresAt;
    public static WorkflowDelegation Create(WorkflowStepExecution step, Guid principal, Guid recipient, Guid actor,
        string reason, DateTimeOffset now, DateTimeOffset expiresAt)
    {
        if (step.Status != "PENDING" || !step.Candidates().Contains(principal) || step.Candidates().Contains(recipient) ||
            expiresAt <= now || expiresAt > now.AddDays(90)) throw new ArgumentException("Ủy quyền phải cho một người khác danh sách gốc, trong tối đa 90 ngày.");
        return new() { Id = Guid.CreateVersion7(), TenantId = step.TenantId, InstanceId = step.InstanceId, StepExecutionId = step.Id,
            PrincipalId = principal, DelegateId = EntityRules.Id(recipient, nameof(recipient)), CreatedBy = EntityRules.Id(actor, nameof(actor)),
            Reason = EntityRules.Text(reason, 1000, nameof(reason)), CreatedAt = now.ToUniversalTime(), ExpiresAt = expiresAt.ToUniversalTime() };
    }
    public void Revoke(Guid actor, DateTimeOffset now)
    {
        if (RevokedAt is not null || now < CreatedAt) throw new InvalidOperationException("Ủy quyền đã được thu hồi.");
        RevokedBy = EntityRules.Id(actor, nameof(actor)); RevokedAt = now.ToUniversalTime();
    }
}
