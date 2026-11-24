using BizFlow.Api.Configuration;
using BizFlow.Application.Common;
using BizFlow.Application.Messaging;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace BizFlow.Api.Controllers;

/// <summary>
/// Direct messages between colleagues (F13). Every endpoint is new; no existing controller is
/// touched. Reading or writing a conversation requires being one of its participants, which the
/// application layer re-checks on every call.
/// </summary>
[ApiController, Authorize, Route("api/v1/messages")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DirectMessagesController(
    DirectMessageService messages,
    IOptions<FeatureFlagOptions> features) : ControllerBase
{
    /// <summary>The caller's own conversations, most recent activity first.</summary>
    [HttpGet("conversations")]
    public Task<ConversationPage> Conversations([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        Require();
        return messages.ListConversationsAsync(new(page, pageSize), cancellationToken);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record OpenInput(Guid UserId);

    /// <summary>Opens the conversation with a colleague, creating it the first time.</summary>
    [HttpPost("conversations")]
    public Task<ConversationRow> Open(OpenInput input, CancellationToken cancellationToken)
    {
        Require();
        return messages.OpenDirectAsync(input.UserId, cancellationToken);
    }

    [HttpGet("conversations/{conversationId:guid}")]
    public Task<MessagePage> Messages(Guid conversationId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default)
    {
        Require();
        return messages.ListMessagesAsync(conversationId, new(page, pageSize), cancellationToken);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record SendInput(string Body);

    [HttpPost("conversations/{conversationId:guid}")]
    public async Task<ActionResult<MessageRow>> Send(Guid conversationId, SendInput input,
        CancellationToken cancellationToken)
    {
        Require();
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1)
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY",
                "Supply at most one Idempotency-Key header.");
        var sent = await messages.SendAsync(conversationId, input.Body,
            keys.Count == 0 ? null : keys[0], cancellationToken);
        return Created($"/api/v1/messages/conversations/{conversationId}", sent);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record EditInput(string Body);

    [HttpPut("conversations/{conversationId:guid}/{messageId:guid}")]
    public Task<MessageRow> Edit(Guid conversationId, Guid messageId, EditInput input,
        CancellationToken cancellationToken)
    {
        Require();
        return messages.EditAsync(conversationId, messageId, input.Body, cancellationToken);
    }

    /// <summary>Removes the words, keeps the row, so the conversation keeps its shape.</summary>
    [HttpDelete("conversations/{conversationId:guid}/{messageId:guid}")]
    public Task<MessageRow> Delete(Guid conversationId, Guid messageId, CancellationToken cancellationToken)
    {
        Require();
        return messages.DeleteAsync(conversationId, messageId, cancellationToken);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ReadInput;

    [HttpPut("conversations/{conversationId:guid}/read")]
    public async Task<IActionResult> MarkRead(Guid conversationId, ReadInput input, CancellationToken cancellationToken)
    {
        Require();
        await messages.MarkReadAsync(conversationId, cancellationToken);
        return NoContent();
    }

    private void Require()
    {
        if (!features.Value.DirectMessages) throw ApplicationFault.NotFound();
    }
}
