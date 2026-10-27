using BizFlow.Application.Notifications;

namespace BizFlow.Api.Realtime;

public sealed class RealtimePresenceChecker(NotificationConnections connections) : IRealtimePresenceChecker
{
    public bool IsUserOnline(Guid tenantId, Guid userId)
    {
        return connections.For(tenantId, [userId]).Length > 0;
    }
}
