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
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record MarkNotificationReadInput;
