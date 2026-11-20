using System.Text.Json.Serialization;
using BizFlow.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/notifications")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class NotificationsController(NotificationInbox inbox) : ControllerBase
{
    [HttpGet]
    public Task<NotificationPage> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] bool unreadOnly = false, CancellationToken cancellationToken = default) =>
        inbox.ListAsync(new(page, pageSize, unreadOnly), cancellationToken);

    [HttpPatch("{id:guid}/read")]
    public Task<NotificationRow> MarkRead(Guid id, MarkNotificationReadInput input, CancellationToken cancellationToken) => inbox.MarkReadAsync(id, cancellationToken);

    [HttpPost("push/subscribe")]
    public async Task<IActionResult> SubscribePush([FromBody] SubscribePushInput input, CancellationToken cancellationToken)
    {
        await inbox.SubscribePushAsync(input.Endpoint, input.P256dh, input.Auth, cancellationToken);
        return Ok(new { success = true });
    }

    [HttpDelete("push/subscribe")]
    public async Task<IActionResult> UnsubscribePush([FromBody] UnsubscribePushInput input, CancellationToken cancellationToken)
    {
        await inbox.UnsubscribePushAsync(input.Endpoint, cancellationToken);
        return NoContent();
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarkNotificationReadInput;

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record SubscribePushInput(string Endpoint, string P256dh, string Auth);

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UnsubscribePushInput(string Endpoint);
