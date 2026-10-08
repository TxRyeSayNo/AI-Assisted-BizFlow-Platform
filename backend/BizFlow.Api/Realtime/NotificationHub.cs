using BizFlow.Api.Security;
using BizFlow.Application.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace BizFlow.Api.Realtime;

[Authorize]
public sealed class NotificationHub(NotificationConnections connections, TenantMembershipAuthorizer membership) : Hub
{
    public const string Path = "/api/v1/realtime/notifications";
    public override async Task OnConnectedAsync()
    {
        var identity = AuthenticationRegistration.ReadIdentity(Context.User);
        try
        {
            var tenantId = await membership.RequireAsync("notifications.connect", Context.ConnectionAborted);
            if (identity is null || identity.TenantId != tenantId) throw new HubException("Access denied.");
            await Groups.AddToGroupAsync(Context.ConnectionId, NotificationConnections.Group(tenantId, identity.UserId), Context.ConnectionAborted);
            connections.Add(Context.ConnectionId, identity);
            // SignalR's protocol handshake can finish before OnConnected. A ready hint after
            // registration closes the subscribe-versus-initial-read race without carrying data.
            await Clients.Caller.SendAsync("InboxReady", Context.ConnectionAborted);
            await base.OnConnectedAsync();
        }
        catch
        {
            connections.Remove(Context.ConnectionId);
            Context.Abort();
            throw new HubException("An active tenant session is required.");
        }
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        connections.Remove(Context.ConnectionId);
        await base.OnDisconnectedAsync(exception);
    }
}
