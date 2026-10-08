using BizFlow.Domain.Common;
using BizFlow.Domain.Workflows;

namespace BizFlow.Domain.Tasks;

public enum TaskPriority { Low, Medium, High, Critical }

public sealed class WorkTask
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? RequestId { get; private set; }
    public Guid CreatorId { get; private set; }
    public string Title { get; private set; } = "";
    public string? Description { get; private set; }
    public TaskPriority Priority { get; private set; }
    public DateTimeOffset? Deadline { get; private set; }
    public TaskState Status { get; private set; }
    public Guid? WorkflowVersionId { get; private set; }
    public Guid? SlaVersionId { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public TaskWorkflowMutation? WorkflowMutation { get; private set; }
    private WorkTask() { }

    internal void ApplyExecutionTransition(TaskWorkflowMutation mutation, DateTimeOffset now)
    {
        if (WorkflowMutation is not null || mutation.Before != Status || mutation.After != TaskState.InProgress || Status is not (TaskState.Accepted or TaskState.Overdue))
            throw new InvalidOperationException("A workflow mutation must match the current task state.");
        WorkflowMutation = mutation; Status = TaskState.InProgress; UpdatedAt = now.ToUniversalTime();
    }

    internal void ApplyAcceptanceTransition(TaskWorkflowMutation mutation, DateTimeOffset now)
    {
        if (WorkflowMutation is not null || mutation.Before != Status || mutation.After != TaskState.Accepted || Status != TaskState.Assigned)
            throw new InvalidOperationException("A workflow mutation must match the current task state.");
        WorkflowMutation = mutation; Status = TaskState.Accepted; UpdatedAt = now.ToUniversalTime();
    }

    internal void ApplyAssignmentTransition(TaskWorkflowMutation mutation, DateTimeOffset now)
    {
        if (WorkflowMutation is not null || mutation.Before != Status || mutation.After != TaskState.Assigned || Status is not (TaskState.Draft or TaskState.Rejected))
            throw new InvalidOperationException("A workflow mutation must match the current task state.");
        WorkflowMutation = mutation; Status = TaskState.Assigned; UpdatedAt = now.ToUniversalTime();
    }

    internal void ApplySubmissionTransition(TaskWorkflowMutation mutation, DateTimeOffset now)
    {
        if (WorkflowMutation is not null || mutation.Before != Status || mutation.After != TaskState.Submitted || Status != TaskState.InProgress)
            throw new InvalidOperationException("A workflow mutation must match the current task state.");
        WorkflowMutation = mutation; Status = TaskState.Submitted; UpdatedAt = now.ToUniversalTime();
    }

    internal void ApplyConfirmationTransition(TaskWorkflowMutation mutation, DateTimeOffset now)
    {
        if (WorkflowMutation is not null || mutation.Before != Status || (mutation.After != TaskState.Confirmed && mutation.After != TaskState.Completed && mutation.After != TaskState.InProgress) || Status != TaskState.Submitted)
            throw new InvalidOperationException("A workflow mutation must match the current task state.");
        WorkflowMutation = mutation; Status = mutation.After;
        if (mutation.After == TaskState.Completed) CompletedAt = now.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
    }

    internal void ApplyCompletionTransition(TaskWorkflowMutation mutation, DateTimeOffset now)
    {
        if (WorkflowMutation is not null || mutation.Before != Status || mutation.After != TaskState.Completed || Status is not (TaskState.Confirmed or TaskState.Submitted))
            throw new InvalidOperationException("A workflow mutation must match the current task state.");
        WorkflowMutation = mutation; Status = TaskState.Completed; CompletedAt = now.ToUniversalTime(); UpdatedAt = now.ToUniversalTime();
    }

    // No assignment, workflow selection or lifecycle side effect. The Application use case
    // must authorize the creator, validate source ownership and apply workflow/SLA deadline rules.
    public static WorkTask CreateDraft(Guid tenantId, Guid creatorId, string title, DateTimeOffset now,
        string? description = null, TaskPriority priority = TaskPriority.Medium,
        DateTimeOffset? deadline = null, Guid? requestId = null)
    {
        if (!Enum.IsDefined(priority)) throw new ArgumentException("Unknown task priority.", nameof(priority));
        if (requestId is { } request) EntityRules.Id(request, nameof(requestId));
        var normalizedTitle = EntityRules.Text(title, 300, nameof(title));
        if (normalizedTitle.Any(char.IsControl)) throw new ArgumentException("Title must be plain text.", nameof(title));
        if (description?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ArgumentException("Description must be plain text.", nameof(description));
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            CreatorId = EntityRules.Id(creatorId, nameof(creatorId)), RequestId = requestId,
            Title = normalizedTitle, Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim(),
            Priority = priority, Deadline = deadline?.ToUniversalTime(), Status = TaskState.Draft,
            CreatedAt = now.ToUniversalTime(), UpdatedAt = now.ToUniversalTime()
        };
    }

    public void Archive(DateTimeOffset now)
    {
        if (DeletedAt is not null)
            throw new InvalidOperationException("Task is already archived.");
        if (Status is not (TaskState.Completed or TaskState.Cancelled))
            throw new InvalidOperationException($"Only tasks in terminal state (Completed or Cancelled) can be archived. Current state: {Status}.");
        DeletedAt = now.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
    }
}

public sealed class TaskChecklistItem
{
    public Guid Id { get; private set; }
    public Guid TaskId { get; private set; }
    public string Title { get; private set; } = "";
    public int SortOrder { get; private set; }
    public bool IsCompleted { get; private set; }
    public Guid? CompletedBy { get; private set; }
    public DateTimeOffset? CompletedAt { get; private set; }
    private TaskChecklistItem() { }

    public static TaskChecklistItem Create(Guid taskId, string title, int sortOrder)
    {
        var text = EntityRules.Text(title, 300, nameof(title));
        if (text.Any(char.IsControl)) throw new ArgumentException("Checklist text must be plain text.", nameof(title));
        if (sortOrder < 1) throw new ArgumentOutOfRangeException(nameof(sortOrder));
        return new() { Id = Guid.CreateVersion7(), TaskId = EntityRules.Id(taskId, nameof(taskId)), Title = text, SortOrder = sortOrder };
    }
}
