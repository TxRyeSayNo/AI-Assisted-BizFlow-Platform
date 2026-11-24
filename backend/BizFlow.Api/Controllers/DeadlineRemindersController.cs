using BizFlow.Api.Configuration;
using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace BizFlow.Api.Controllers;

/// <summary>
/// REST surface for the deadline reminder module (F4, INT-032). It completes GAP-01 by exposing
/// the configuration side of <see cref="IDeadlineReminderService"/>, which already existed in the
/// application layer but had no controller. It lives on its own route, so the existing
/// <see cref="NotificationsController"/> — including its push subscribe and unsubscribe endpoints
/// — keeps every URL and response it already publishes.
/// </summary>
[ApiController, Authorize, Route("api/v1/notifications/reminders")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DeadlineRemindersController(
    IDeadlineReminderService reminders,
    IOptions<FeatureFlagOptions> features) : ControllerBase
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ConfigureInput(Guid TaskId, int[] LeadTimeMinutes, string[]? TriggerTypes = null);

    /// <summary>Replaces the caller's reminder marks for one task.</summary>
    [HttpPost("config")]
    public async Task<ActionResult<IReadOnlyList<DeadlineReminderDto>>> Configure(ConfigureInput input,
        CancellationToken cancellationToken)
    {
        Require();
        var result = await reminders.ConfigureRemindersAsync(
            new(input.TaskId, input.LeadTimeMinutes ?? [], input.TriggerTypes), cancellationToken);
        return Ok(result);
    }

    /// <summary>The reminder marks currently configured for one task.</summary>
    [HttpGet("config")]
    public async Task<ActionResult<IReadOnlyList<DeadlineReminderDto>>> Read([FromQuery] Guid taskId,
        CancellationToken cancellationToken)
    {
        Require();
        if (taskId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Validation, "REMINDER.TASK_REQUIRED", "A task id is required.");
        return Ok(await reminders.GetRemindersForTaskAsync(taskId, cancellationToken));
    }

    [HttpDelete("{reminderId:guid}")]
    public async Task<IActionResult> Cancel(Guid reminderId, CancellationToken cancellationToken)
    {
        Require();
        await reminders.CancelReminderAsync(reminderId, cancellationToken);
        return NoContent();
    }

    public sealed record VapidKeyView(string PublicKey);

    /// <summary>
    /// The VAPID public key a browser needs before it can create a push subscription. The private
    /// key never leaves the server.
    /// </summary>
    [HttpGet("vapid-public-key")]
    public ActionResult<VapidKeyView> VapidPublicKey()
    {
        Require();
        return Ok(new VapidKeyView(reminders.GetVapidPublicKey()));
    }

    private void Require()
    {
        if (!features.Value.DeadlineReminders) throw ApplicationFault.NotFound();
    }
}
