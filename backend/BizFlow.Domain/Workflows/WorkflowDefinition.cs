using System.Text.Json;
using BizFlow.Domain.Common;

namespace BizFlow.Domain.Workflows;

public enum WorkflowBusinessType { Task, Request }
public enum WorkflowStatus { Draft, Active, Inactive }
public enum WorkflowVersionStatus { Draft, Published, Retired }
public enum WorkflowStepType { Action, Approval, Confirmation }

public sealed class WorkflowDefinition
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = "";
    public WorkflowBusinessType BusinessType { get; private set; }
    public WorkflowStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private WorkflowDefinition() { }

    public static WorkflowDefinition CreateDraft(Guid tenantId, string name, WorkflowBusinessType businessType, DateTimeOffset now)
    {
        if (!Enum.IsDefined(businessType)) throw new ArgumentOutOfRangeException(nameof(businessType));
        return new() { Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            Name = EntityRules.Text(name, 200, nameof(name)), BusinessType = businessType, Status = WorkflowStatus.Draft,
            CreatedAt = now.ToUniversalTime(), UpdatedAt = now.ToUniversalTime() };
    }

    public void UpdateName(string name, DateTimeOffset now)
    {
        Name = EntityRules.Text(name, 200, nameof(name));
        UpdatedAt = now.ToUniversalTime();
    }

    public void UpdateStatus(WorkflowStatus status, DateTimeOffset now)
    {
        if (!Enum.IsDefined(status)) throw new ArgumentOutOfRangeException(nameof(status));
        Status = status;
        UpdatedAt = now.ToUniversalTime();
    }

    public void Activate(DateTimeOffset now) => UpdateStatus(WorkflowStatus.Active, now);
    public void Deactivate(DateTimeOffset now) => UpdateStatus(WorkflowStatus.Inactive, now);
}

public sealed class WorkflowVersion
{
    public Guid Id { get; private set; }
    public Guid WorkflowId { get; private set; }
    public int VersionNo { get; private set; }
    public WorkflowVersionStatus Status { get; private set; }
    public DateTimeOffset? PublishedAt { get; private set; }
    public string DefinitionJson { get; private set; } = "{}";
    private WorkflowVersion() { }

    public static WorkflowVersion CreateDraft(Guid workflowId, int versionNo)
    {
        if (versionNo < 1) throw new ArgumentOutOfRangeException(nameof(versionNo));
        return new() { Id = Guid.CreateVersion7(), WorkflowId = EntityRules.Id(workflowId, nameof(workflowId)),
            VersionNo = versionNo, Status = WorkflowVersionStatus.Draft };
    }

    public void UpdateDefinition(string definitionJson)
    {
        if (Status != WorkflowVersionStatus.Draft)
            throw new InvalidOperationException("Only draft versions can have their definition updated.");
        DefinitionJson = WorkflowJson.Object(definitionJson);
    }

    public void Publish(DateTimeOffset now)
    {
        if (Status != WorkflowVersionStatus.Draft)
            throw new InvalidOperationException("Only draft versions can be published.");
        Status = WorkflowVersionStatus.Published;
        PublishedAt = now.ToUniversalTime();
    }

    public void Retire()
    {
        if (Status != WorkflowVersionStatus.Published)
            throw new InvalidOperationException("Only published versions can be retired.");
        Status = WorkflowVersionStatus.Retired;
    }
}

public sealed class WorkflowStep
{
    public Guid Id { get; private set; }
    public Guid WorkflowVersionId { get; private set; }
    public string StepCode { get; private set; } = "";
    public string Name { get; private set; } = "";
    public WorkflowStepType Type { get; private set; }
    public int OrderNo { get; private set; }
    public string ConfigJson { get; private set; } = "{}";
    private WorkflowStep() { }

    public static WorkflowStep CreateDraft(Guid versionId, string stepCode, string name, WorkflowStepType type, int orderNo, string configJson = "{}")
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (orderNo < 1) throw new ArgumentOutOfRangeException(nameof(orderNo));
        var code = EntityRules.Text(stepCode, 80, nameof(stepCode)).ToUpperInvariant();
        if (code.Any(c => !char.IsAsciiLetterOrDigit(c) && c is not ('_' or '-' or '.')))
            throw new ArgumentException("Step codes contain only letters, digits, underscores, hyphens and periods.", nameof(stepCode));
        return new() { Id = Guid.CreateVersion7(), WorkflowVersionId = EntityRules.Id(versionId, nameof(versionId)),
            StepCode = code, Name = EntityRules.Text(name, 200, nameof(name)), Type = type, OrderNo = orderNo,
            ConfigJson = WorkflowJson.Object(configJson) };
    }
}

public sealed class WorkflowTransition
{
    public Guid Id { get; private set; }
    public Guid WorkflowVersionId { get; private set; }
    public string FromState { get; private set; } = "";
    public string ToState { get; private set; } = "";
    public string? GuardJson { get; private set; }
    private WorkflowTransition() { }

    public static WorkflowTransition CreateDraft(Guid versionId, string fromState, string toState, string? guardJson = null) => new()
    {
        Id = Guid.CreateVersion7(), WorkflowVersionId = EntityRules.Id(versionId, nameof(versionId)),
        FromState = EntityRules.Text(fromState, 50, nameof(fromState)), ToState = EntityRules.Text(toState, 50, nameof(toState)),
        GuardJson = guardJson is null ? null : WorkflowJson.Object(guardJson)
    };
}

internal static class WorkflowJson
{
    internal static string Object(string json)
    {
        using var parsed = JsonDocument.Parse(json);
        if (parsed.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Workflow configuration must be a JSON object.", nameof(json));
        return parsed.RootElement.GetRawText();
    }
}
