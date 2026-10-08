using BizFlow.Application.Authentication;
using BizFlow.Application.Notifications;
using Microsoft.AspNetCore.SignalR;

namespace BizFlow.Api.Realtime;

public sealed class SignalRNotificationUpdates(NotificationConnections connections, IAuthenticationStore authentication,
    IHubContext<NotificationHub> hub, TimeProvider clock, ILogger<SignalRNotificationUpdates> logger) : INotificationUpdates
{
    public async Task PublishAsync(Guid tenantId, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken)
    {
        // Bound latency and do not turn a successfully committed mutation into an HTTP failure.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            foreach (var entry in connections.For(tenantId, recipientIds))
            {
                if (!await authentication.ValidateAccessAsync(entry.Value, clock.GetUtcNow(), timeout.Token))
                {
                    connections.Remove(entry.Key);
                    continue;
                }
                await hub.Clients.Client(entry.Key).SendAsync("InboxChanged", timeout.Token);
            }
        }
        catch (Exception)
        {
            // No exception object: transport errors may contain a credential-bearing URL.
            logger.LogWarning("Realtime inbox hint was not delivered; persisted inbox remains authoritative.");
        }
    }
}
