using BizFlow.Domain.Common;

namespace BizFlow.Domain.Notifications;

// SSS §22 baseline event catalog. Application producers validate referenced resources.
public enum NotificationEvent
{
    TaskAssigned, TaskAccepted, TaskRejected, ProgressSubmitted, ResultSubmitted, TaskConfirmed,
    RequestSubmitted, RequestRouted, RequestReceived, RequestResolved, RequestRejected, RequestRevised,
    DeadlineWarning, Overdue, Escalation, ApprovalRequired
}

public sealed class Notification
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid RecipientId { get; private set; }
    public string Type { get; private set; } = "";
    public string? ObjectType { get; private set; }
    public Guid? ObjectId { get; private set; }
    public string Title { get; private set; } = "";
    public string Content { get; private set; } = "";
    public DateTimeOffset? ReadAt { get; private set; }
    public DateTimeOffset? SentAt { get; private set; }
    public string IdempotencyKey { get; private set; } = "";
    private Notification() { }

    public static Notification Create(Guid tenantId, Guid recipientId, NotificationEvent type, string title,
        string content, string idempotencyKey, string? objectType = null, Guid? objectId = null)
    {
        if (!Enum.IsDefined(type)) throw new ArgumentOutOfRangeException(nameof(type));
        if (objectId == Guid.Empty || (objectId.HasValue && string.IsNullOrWhiteSpace(objectType)))
            throw new ArgumentException("A related identifier requires an object type.", nameof(objectId));
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            RecipientId = EntityRules.Id(recipientId, nameof(recipientId)), Type = type.ToString(),
            Title = EntityRules.Text(title, 200, nameof(title)), Content = EntityRules.Text(content, int.MaxValue, nameof(content)),
            IdempotencyKey = EntityRules.Text(idempotencyKey, 180, nameof(idempotencyKey)),
            ObjectType = objectType is null ? null : EntityRules.Text(objectType, 40, nameof(objectType)), ObjectId = objectId
        };
    }

    public bool MarkRead(Guid tenantId, Guid recipientId, DateTimeOffset now)
    {
        if (tenantId != TenantId || recipientId != RecipientId) throw new InvalidOperationException("Only the tenant recipient can mark this notification read.");
        if (ReadAt.HasValue) return false;
        // Canonical microsecond precision keeps first and replayed responses identical
        // after PostgreSQL's timestamp round-trip (DateTimeOffset otherwise has 100ns ticks).
        var utc = now.ToUniversalTime();
        ReadAt = new DateTimeOffset(utc.Ticks - utc.Ticks % 10, TimeSpan.Zero);
        return true;
    }
}
