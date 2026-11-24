using BizFlow.Api.Security;
using BizFlow.Application.Authentication;
using BizFlow.Application.Messaging;
using BizFlow.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BizFlow.Api.Realtime;

/// <summary>
/// Realtime hints for direct messages (F13). A separate hub from
/// <see cref="NotificationHub"/>, which is left exactly as it is: SignalR scopes groups per hub,
/// so the two never see each other's connections.
///
/// Deliberately not registered in <see cref="NotificationConnections"/>: that registry backs
/// <c>IRealtimePresenceChecker</c>, and letting a chat socket count as "online" would silently
/// change how the deadline reminder escalation (F4) chooses its channel.
/// </summary>
[Authorize]
public sealed class ChatHub(TenantMembershipAuthorizer membership) : Hub
{
    public const string Path = "/api/v1/realtime/messages";

    public override async Task OnConnectedAsync()
    {
        var identity = AuthenticationRegistration.ReadIdentity(Context.User);
        try
        {
            var tenantId = await membership.RequireAsync("messages.connect", Context.ConnectionAborted);
            if (identity is null || identity.TenantId != tenantId) throw new HubException("Access denied.");
            await Groups.AddToGroupAsync(Context.ConnectionId, Group(tenantId, identity.UserId),
                Context.ConnectionAborted);
            // Mirrors the inbox hub: a ready hint closes the subscribe-versus-initial-read race
            // without carrying any data.
            await Clients.Caller.SendAsync("MessagesReady", Context.ConnectionAborted);
            await base.OnConnectedAsync();
        }
        catch
        {
            Context.Abort();
            throw new HubException("An active tenant session is required.");
        }
    }

    /// <summary>Same naming convention as the inbox hub, in this hub's own group namespace.</summary>
    internal static string Group(Guid tenantId, Guid userId) => NotificationConnections.Group(tenantId, userId);
}

/// <summary>
/// Pushes the conversation id and nothing else. The recipient then refetches over authenticated
/// HTTP, where participation is checked again — so a session that has been revoked since it
/// connected can never receive message content over the socket.
/// </summary>
public sealed class SignalRDirectMessageUpdates(
    IHubContext<ChatHub> hub,
    ILogger<SignalRDirectMessageUpdates> logger) : IDirectMessageUpdates
{
    public async Task PublishAsync(Guid tenantId, IReadOnlyCollection<Guid> recipientIds, Guid conversationId,
        CancellationToken cancellationToken)
    {
        // Bound latency and never turn a committed message into an HTTP failure.
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        try
        {
            foreach (var recipientId in recipientIds)
                await hub.Clients.Group(ChatHub.Group(tenantId, recipientId))
                    .SendAsync("ConversationChanged", conversationId, timeout.Token);
        }
        catch (Exception)
        {
            // No exception object: transport errors may contain a credential-bearing URL.
            logger.LogWarning("Realtime message hint was not delivered; the stored conversation remains authoritative.");
        }
    }
}
