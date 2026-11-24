using System.Text.Json;
using BizFlow.Domain.Common;

namespace BizFlow.Domain.Workflows;

/// <summary>A durable execution, pinned to the definition published when the request starts.</summary>
public sealed class WorkflowInstance
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid WorkflowVersionId { get; private set; }
    public Guid RequesterId { get; private set; }
    public string Status { get; private set; } = "RUNNING";
    public string PayloadJson { get; private set; } = "{}";
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public int Revision { get; private set; }
    private WorkflowInstance() { }

    public static WorkflowInstance Start(Guid tenantId, Guid requestId, Guid versionId, Guid requesterId,
        string payloadJson, DateTimeOffset now)
    {
        using var payload = JsonDocument.Parse(payloadJson);
        if (payload.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Dữ liệu biểu mẫu phải là đối tượng.");
        return new() { Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            RequestId = EntityRules.Id(requestId, nameof(requestId)), WorkflowVersionId = EntityRules.Id(versionId, nameof(versionId)),
            RequesterId = EntityRules.Id(requesterId, nameof(requesterId)), PayloadJson = payload.RootElement.GetRawText(),
            StartedAt = now.ToUniversalTime(), UpdatedAt = now.ToUniversalTime(), Revision = 1 };
    }

    public void Advance(int expectedRevision, DateTimeOffset now, string? terminalState = null)
    {
        if (Status != "RUNNING" || Revision != expectedRevision || now < UpdatedAt)
            throw new InvalidOperationException("Quy trình đã thay đổi. Vui lòng tải lại.");
        if (terminalState is not (null or "COMPLETED" or "REJECTED" or "CANCELLED" or "FAILED"))
            throw new ArgumentException("Trạng thái kết thúc không hợp lệ.");
        Revision++; UpdatedAt = now.ToUniversalTime();
        if (terminalState is not null) { Status = terminalState; CompletedAt = UpdatedAt; }
    }
}

public sealed class WorkflowStepExecution
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InstanceId { get; private set; }
    public string NodeId { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string NodeType { get; private set; } = "";
    public string Status { get; private set; } = "PENDING";
    public string Policy { get; private set; } = "SINGLE";
    public string CandidatesJson { get; private set; } = "[]";
    public DateTimeOffset StartedAt { get; private set; }
    public DateTimeOffset? DueAt { get; private set; }
    public DateTimeOffset? PausedAt { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public string? Outcome { get; private set; }
    private WorkflowStepExecution() { }

    public static WorkflowStepExecution Open(WorkflowInstance instance, string nodeId, string name, string nodeType,
        string? policy, IReadOnlyCollection<Guid> candidates, DateTimeOffset now, DateTimeOffset? dueAt)
    {
        if (nodeType is not ("DEPARTMENT" or "APPROVAL" or "CONFIRMATION")) throw new ArgumentException("Loại bước chưa được hỗ trợ.");
        if (candidates.Count == 0 || candidates.Any(x => x == Guid.Empty)) throw new ArgumentException("Bước phải có người chịu trách nhiệm.");
        var normalizedPolicy = policy ?? "SINGLE";
        if (normalizedPolicy is not ("SINGLE" or "ANY" or "ALL")) throw new ArgumentException("Chính sách duyệt không hợp lệ.");
        return new() { Id = Guid.CreateVersion7(), TenantId = instance.TenantId, InstanceId = instance.Id,
            NodeId = EntityRules.Text(nodeId, 100, nameof(nodeId)), Name = EntityRules.Text(name, 200, nameof(name)),
            NodeType = nodeType, Policy = normalizedPolicy, CandidatesJson = JsonSerializer.Serialize(candidates.Distinct().Order()),
            StartedAt = now.ToUniversalTime(), DueAt = dueAt?.ToUniversalTime() };
    }

    public IReadOnlyList<Guid> Candidates() => JsonSerializer.Deserialize<Guid[]>(CandidatesJson)!;
    public static WorkflowStepExecution Gateway(WorkflowInstance instance, string nodeId, string name, string type, DateTimeOffset now)
    {
        if (type is not ("PARALLEL_SPLIT" or "PARALLEL_JOIN")) throw new ArgumentException("Loại cổng không hợp lệ.");
        return new() { Id = Guid.CreateVersion7(), TenantId = instance.TenantId, InstanceId = instance.Id,
            NodeId = nodeId, Name = name, NodeType = type, StartedAt = now.ToUniversalTime() };
    }
    public void Complete(string outcome, DateTimeOffset now)
    {
        if (Status != "PENDING" || PausedAt is not null || now < StartedAt) throw new InvalidOperationException("Bước đã được xử lý hoặc đang tạm dừng.");
        Outcome = EntityRules.Text(outcome, 50, nameof(outcome)); Status = "COMPLETED"; CompletedAt = now.ToUniversalTime();
    }
    public void Pause(DateTimeOffset now)
    {
        if (Status != "PENDING" || DueAt is null || PausedAt is not null || now < StartedAt) throw new InvalidOperationException("Bước không thể tạm dừng SLA.");
        PausedAt = now.ToUniversalTime();
    }
    public void Resume(DateTimeOffset now, DateTimeOffset dueAt)
    {
        if (Status != "PENDING" || PausedAt is null || now < PausedAt || dueAt < DueAt) throw new InvalidOperationException("Bước không thể tiếp tục SLA.");
        DueAt = dueAt.ToUniversalTime(); PausedAt = null;
    }
}

public sealed class WorkflowDecision
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid InstanceId { get; private set; }
    public Guid StepExecutionId { get; private set; }
    public Guid ActorId { get; private set; }
    public Guid PrincipalId { get; private set; }
    public string Outcome { get; private set; } = "";
    public string? Note { get; private set; }
    public DateTimeOffset DecidedAt { get; private set; }
    private WorkflowDecision() { }
    public static WorkflowDecision Record(WorkflowStepExecution step, Guid actorId, string outcome, string? note, DateTimeOffset now,
        WorkflowDelegation? delegation = null)
    {
        var principal = delegation?.PrincipalId ?? actorId;
        if (step.Status != "PENDING" || !step.Candidates().Contains(principal) ||
            delegation is not null && (delegation.StepExecutionId != step.Id || delegation.DelegateId != actorId || !delegation.IsActive(now)))
            throw new InvalidOperationException("Không phải người xử lý bước này.");
        if (outcome is not ("COMPLETE" or "APPROVE" or "REJECT")) throw new ArgumentException("Quyết định không hợp lệ.");
        return new() { Id = Guid.CreateVersion7(), TenantId = step.TenantId, InstanceId = step.InstanceId,
            StepExecutionId = step.Id, ActorId = actorId, PrincipalId = principal, Outcome = outcome,
            Note = string.IsNullOrWhiteSpace(note) ? null : EntityRules.Text(note, 4000, nameof(note)), DecidedAt = now.ToUniversalTime() };
    }
}

public sealed class WorkflowParticipant
{
    public Guid TenantId { get; private set; }
    public Guid InstanceId { get; private set; }
    public Guid StepExecutionId { get; private set; }
    public Guid UserId { get; private set; }
    private WorkflowParticipant() { }
    public static WorkflowParticipant Create(WorkflowStepExecution step, Guid userId)
    {
        if (!step.Candidates().Contains(userId)) throw new ArgumentException("Người nhận không thuộc bước.");
        return new() { TenantId = step.TenantId, InstanceId = step.InstanceId, StepExecutionId = step.Id, UserId = userId };
    }
    public static WorkflowParticipant ForDelegate(WorkflowStepExecution step, WorkflowDelegation delegation)
    {
        if (delegation.StepExecutionId != step.Id || delegation.TenantId != step.TenantId)
            throw new ArgumentException("Ủy quyền không thuộc bước.");
        return new() { TenantId = step.TenantId, InstanceId = step.InstanceId, StepExecutionId = step.Id, UserId = delegation.DelegateId };
    }
}
