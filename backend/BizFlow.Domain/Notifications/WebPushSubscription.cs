using BizFlow.Domain.Common;

namespace BizFlow.Domain.Notifications;

public sealed class WebPushSubscription
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Endpoint { get; private set; } = "";
    public string P256dh { get; private set; } = "";
    public string Auth { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset LastActiveAt { get; private set; }

    private WebPushSubscription() { }

    public static WebPushSubscription Create(
        Guid tenantId,
        Guid userId,
        string endpoint,
        string p256dh,
        string auth,
        DateTimeOffset now)
    {
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            UserId = EntityRules.Id(userId, nameof(userId)),
            Endpoint = EntityRules.Text(endpoint, 1000, nameof(endpoint)),
            P256dh = EntityRules.Text(p256dh, 250, nameof(p256dh)),
            Auth = EntityRules.Text(auth, 150, nameof(auth)),
            CreatedAt = now.ToUniversalTime(),
            LastActiveAt = now.ToUniversalTime()
        };
    }

    public void UpdateActivity(DateTimeOffset now) => LastActiveAt = now.ToUniversalTime();
}
