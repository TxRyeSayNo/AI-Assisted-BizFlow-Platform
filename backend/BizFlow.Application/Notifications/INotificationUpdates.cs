namespace BizFlow.Application.Notifications;

// Post-commit, best-effort invalidation only. Implementations must contain transport failures;
// persistence is authoritative and callers must never publish before a successful commit.
public interface INotificationUpdates
{
    Task PublishAsync(Guid tenantId, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken);
}
