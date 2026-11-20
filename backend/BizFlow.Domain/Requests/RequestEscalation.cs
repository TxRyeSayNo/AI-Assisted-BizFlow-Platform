using BizFlow.Domain.Common;

namespace BizFlow.Domain.Requests;

public enum RequestEscalationState
{
    Pending,
    Approved,
    Rejected
}

// A parallel, additive approval record. It does NOT change the WorkRequest state machine:
// a manager escalates a request to a higher approval level, and an elevated approver
// (e.g. a Director role granted "requests.approve.elevated") approves or rejects it.
public sealed class RequestEscalation
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid RequestId { get; private set; }
    public Guid EscalatedById { get; private set; }
    public string Reason { get; private set; } = "";
    public RequestEscalationState State { get; private set; }
    public DateTimeOffset EscalatedAt { get; private set; }
    public Guid? DecidedById { get; private set; }
    public string? DecisionNote { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }

    private RequestEscalation() { }

    public static RequestEscalation Create(Guid tenantId, Guid requestId, Guid escalatedById, string reason, DateTimeOffset now)
    {
        var text = EntityRules.Text(reason, 2000, nameof(reason));
        if (text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ArgumentException("Reason must be plain text.", nameof(reason));
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            RequestId = EntityRules.Id(requestId, nameof(requestId)),
            EscalatedById = EntityRules.Id(escalatedById, nameof(escalatedById)),
            Reason = text,
            State = RequestEscalationState.Pending,
            EscalatedAt = now.ToUniversalTime(),
            UpdatedAt = now.ToUniversalTime()
        };
    }

    public void Decide(bool approve, Guid deciderId, string? note, DateTimeOffset now)
    {
        if (State != RequestEscalationState.Pending)
            throw new InvalidOperationException("This escalation has already been decided.");
        var text = note is null ? null : note.Trim();
        if (text is { Length: > 0 } && text.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')))
            throw new ArgumentException("Note must be plain text.", nameof(note));
        State = approve ? RequestEscalationState.Approved : RequestEscalationState.Rejected;
        DecidedById = EntityRules.Id(deciderId, nameof(deciderId));
        DecisionNote = string.IsNullOrWhiteSpace(text) ? null : text;
        DecidedAt = now.ToUniversalTime();
        UpdatedAt = now.ToUniversalTime();
    }
}
