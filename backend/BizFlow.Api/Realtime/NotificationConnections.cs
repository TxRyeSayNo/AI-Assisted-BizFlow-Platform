using System.Collections.Concurrent;
using BizFlow.Application.Authentication;

namespace BizFlow.Api.Realtime;

// Process-local transport registry, not business/session persistence. Removed on disconnect.
public sealed class NotificationConnections
{
    private readonly ConcurrentDictionary<string, AccessTokenRequest> connections = new();
    public void Add(string connectionId, AccessTokenRequest identity) => connections[connectionId] = identity;
    public void Remove(string connectionId) => connections.TryRemove(connectionId, out _);
    public KeyValuePair<string, AccessTokenRequest>[] For(Guid tenantId, IReadOnlyCollection<Guid> recipients) =>
        connections.Where(entry => entry.Value.TenantId == tenantId && recipients.Contains(entry.Value.UserId)).ToArray();
    public static string Group(Guid tenantId, Guid userId) => $"tenant:{tenantId:N}/user:{userId:N}";
}
