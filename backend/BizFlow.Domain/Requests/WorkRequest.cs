using BizFlow.Domain.Common;

namespace BizFlow.Domain.Requests;

// Exactly the twelve SSS Request states. AI analysis is not a business state.
public enum RequestState { Draft, Submitted, Routed, Received, InProgress, Resolved, Confirmed, Closed, WaitingForInformation, Rejected, Cancelled, Overdue }
public enum RequestPriority { Low, Medium, High, Critical }

public sealed class WorkRequest
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid RequesterId { get; private set; }
    public Guid ServiceId { get; private set; }
    public Guid CategoryId { get; private set; }
    public Guid? ParentRequestId { get; private set; }
    public Guid? RevisedFromRequestId { get; private set; }
    public string Title { get; private set; } = "";
    public string Description { get; private set; } = "";
    public RequestPriority Priority { get; private set; }
    public RequestState Status { get; private set; }
    public Guid? WorkflowVersionId { get; private set; }
    public Guid? SlaVersionId { get; private set; }
    public DateTimeOffset? ResolvedAt { get; private set; }
    public DateTimeOffset? ClosedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    public DateTimeOffset? DeletedAt { get; private set; }
    public RequestWorkflowMutation? WorkflowMutation { get; private set; }
    public void ResetWorkflowMutation() => WorkflowMutation = null;
    private WorkRequest() { }

    public void ApplySubmissionTransition(RequestWorkflowMutation mutation, DateTimeOffset now)
    {
        if (WorkflowMutation is not null || mutation.Before != Status || mutation.After != RequestState.Submitted || Status != RequestState.Draft)
            throw new InvalidOperationException("A workflow mutation must match the current request state.");
        WorkflowMutation = mutation; Status = RequestState.Submitted; UpdatedAt = now.ToUniversalTime();
    }

    public void ApplyRoutingTransition(RequestWorkflowMutation mutation, DateTimeOffset now)
    {
        if (WorkflowMutation is not null || mutation.Before != Status || mutation.After != RequestState.Routed || Status != RequestState.Submitted)
            throw new InvalidOperationException("A workflow mutation must match the current request state.");
        WorkflowMutation = mutation; Status = RequestState.Routed; UpdatedAt = now.ToUniversalTime();
    }

    public void ApplyReceiptTransition(RequestWorkflowMutation mutation, DateTimeOffset now)
    {
        if (WorkflowMutation is not null || mutation.Before != Status || mutation.After != RequestState.Received || Status != RequestState.Routed)
            throw new InvalidOperationException("A workflow mutation must match the current request state.");
        WorkflowMutation = mutation; Status = RequestState.Received; UpdatedAt = now.ToUniversalTime();
    }

    public void ApplyExecutionTransition(RequestWorkflowMutation mutation, DateTimeOffset now)
    {
        if (WorkflowMutation is not null || mutation.Before != Status || mutation.After != RequestState.InProgress || Status is not (RequestState.Received or RequestState.WaitingForInformation or RequestState.Overdue or RequestState.Resolved))
            throw new InvalidOperationException("A workflow mutation must match the current request state.");
        WorkflowMutation = mutation; Status = RequestState.InProgress; UpdatedAt = now.ToUniversalTime();
    }

    public void ApplyWaitingTransition(RequestWorkflowMutation mutation, DateTimeOffset now)
    {
        if (WorkflowMutation is not null || mutation.Before != Status || mutation.After != RequestState.WaitingForInformation || Status != RequestState.InProgress)
            throw new InvalidOperationException("A workflow mutation must match the current request state.");
        WorkflowMutation = mutation; Status = RequestState.WaitingForInformation; UpdatedAt = now.ToUniversalTime();
    }

    public void ApplyResolutionTransition(RequestWorkflowMutation mutation, DateTimeOffset now)
    {
        if (WorkflowMutation is not null || mutation.Before != Status || mutation.After != RequestState.Resolved || Status != RequestState.InProgress)
            throw new InvalidOperationException("A workflow mutation must match the current request state.");
        WorkflowMutation = mutation; Status = RequestState.Resolved; ResolvedAt = now.ToUniversalTime(); UpdatedAt = now.ToUniversalTime();
    }

    public void ApplyConfirmationTransition(RequestWorkflowMutation mutation, DateTimeOffset now)
    {
        if (WorkflowMutation is not null || mutation.Before != Status || (mutation.After != RequestState.Confirmed && mutation.After != RequestState.InProgress) || Status != RequestState.Resolved)
            throw new InvalidOperationException("A workflow mutation must match the current request state.");
        WorkflowMutation = mutation; Status = mutation.After; UpdatedAt = now.ToUniversalTime();
    }

    public void ApplyClosureTransition(RequestWorkflowMutation mutation, DateTimeOffset now)
    {
        if (WorkflowMutation is not null || mutation.Before != Status || mutation.After != RequestState.Closed || Status != RequestState.Confirmed)
            throw new InvalidOperationException("A workflow mutation must match the current request state.");
        WorkflowMutation = mutation; Status = RequestState.Closed; ClosedAt = now.ToUniversalTime(); UpdatedAt = now.ToUniversalTime();
    }

    public void ApplyRejectionTransition(RequestWorkflowMutation mutation, DateTimeOffset now)
    {
        if (WorkflowMutation is not null || mutation.Before != Status || mutation.After != RequestState.Rejected || Status is not (RequestState.Submitted or RequestState.Routed))
            throw new InvalidOperationException("A workflow mutation must match the current request state.");
        WorkflowMutation = mutation; Status = RequestState.Rejected; UpdatedAt = now.ToUniversalTime();
    }

    public void ApplyCancellationTransition(RequestWorkflowMutation mutation, DateTimeOffset now)
    {
        if (WorkflowMutation is not null || mutation.Before != Status || mutation.After != RequestState.Cancelled || Status is RequestState.Closed or RequestState.Cancelled or RequestState.Rejected)
            throw new InvalidOperationException("A workflow mutation must match the current request state.");
        WorkflowMutation = mutation; Status = RequestState.Cancelled; UpdatedAt = now.ToUniversalTime();
    }

    // A validated draft is not a submitted request. Application services must authorize
    // the actor and resolve service/category/parent ownership before persistence.
    public static WorkRequest CreateDraft(Guid tenantId, Guid requesterId, Guid serviceId, Guid categoryId,
        string title, string description, DateTimeOffset now, RequestPriority priority = RequestPriority.Medium,
        Guid? parentRequestId = null)
    {
        if (!Enum.IsDefined(priority)) throw new ArgumentException("Unknown request priority.", nameof(priority));
        if (parentRequestId is { } parent) EntityRules.Id(parent, nameof(parentRequestId));
        ArgumentNullException.ThrowIfNull(description);
        var text = description.Trim();
        if (text.Length == 0 || text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ArgumentException("A plain-text description is required.", nameof(description));
        var normalizedTitle = EntityRules.Text(title, 300, nameof(title));
        if (normalizedTitle.Any(char.IsControl)) throw new ArgumentException("Title must be plain text.", nameof(title));
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            RequesterId = EntityRules.Id(requesterId, nameof(requesterId)), ServiceId = EntityRules.Id(serviceId, nameof(serviceId)),
            CategoryId = EntityRules.Id(categoryId, nameof(categoryId)), ParentRequestId = parentRequestId,
            Title = normalizedTitle, Description = text, Priority = priority, Status = RequestState.Draft,
            CreatedAt = now.ToUniversalTime(), UpdatedAt = now.ToUniversalTime()
        };
    }

    public static WorkRequest CreateSubmitted(Guid tenantId, Guid requesterId, Guid serviceId, Guid categoryId,
        string title, string description, DateTimeOffset now, RequestPriority priority = RequestPriority.Medium,
        Guid? parentRequestId = null)
    {
        var draft = CreateDraft(tenantId, requesterId, serviceId, categoryId, title, description, now, priority, parentRequestId);
        draft.Status = RequestState.Submitted;
        return draft;
    }

    // BR-008: a revision has a new identity and independent lifecycle. Copy no runtime
    // versions, timestamps, resolution, assignments or evidence implicitly.
    public static WorkRequest ReviseRejected(WorkRequest source, Guid requesterId, Guid serviceId, Guid categoryId,
        string title, string description, DateTimeOffset now, RequestPriority priority = RequestPriority.Medium)
    {
        ArgumentNullException.ThrowIfNull(source);
        if (source.Status != RequestState.Rejected) throw new InvalidOperationException("Only a rejected request can be revised.");
        var revision = CreateDraft(source.TenantId, requesterId, serviceId, categoryId, title, description, now, priority);
        revision.RevisedFromRequestId = source.Id;
        return revision;
    }

    public void Archive(DateTimeOffset now)
    {
        if (DeletedAt is not null)
            throw new InvalidOperationException("Request is already archived.");
        if (Status is not (RequestState.Closed or RequestState.Cancelled))
            throw new InvalidOperationException($"Only requests in terminal state (Closed or Cancelled) can be archived. Current state: {Status}.");
        DeletedAt = now.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
    }
}

public sealed class RequestWorkflowMutation
{
    public Guid ActorId { get; }
    public RequestState Before { get; }
    public RequestState After { get; }
    public DateTimeOffset BeforeUpdatedAt { get; }
    public RequestWorkflowMutation(Guid actorId, RequestState before, RequestState after, DateTimeOffset beforeUpdatedAt)
    {
        ActorId = actorId;
        Before = before;
        After = after;
        BeforeUpdatedAt = beforeUpdatedAt;
    }
}
