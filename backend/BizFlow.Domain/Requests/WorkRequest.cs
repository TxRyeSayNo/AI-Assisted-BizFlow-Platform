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
    private WorkRequest() { }

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
}
