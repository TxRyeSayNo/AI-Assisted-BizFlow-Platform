using System.Text.Json.Serialization;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/comments")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CommentsController(CommentService commentService) : ControllerBase
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record CreateCommentInput(string ObjectType, Guid ObjectId, string Content);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record EditCommentInput(string Content);

    [HttpPost]
    public async Task<ActionResult<CommentItemView>> Create(CreateCommentInput input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1) throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");
        var key = keys.Count == 0 ? null : keys[0];

        var result = await commentService.CreateAsync(new(input.ObjectType, input.ObjectId, input.Content), key, cancellationToken);
        return Created($"/api/v1/comments/{result.CommentId}", result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CommentItemView>>> List(
        [FromQuery] string objectType,
        [FromQuery] Guid objectId,
        CancellationToken cancellationToken)
    {
        var results = await commentService.ListAsync(objectType, objectId, cancellationToken);
        return Ok(results);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<CommentItemView>> Edit(Guid id, EditCommentInput input, CancellationToken cancellationToken)
    {
        var result = await commentService.EditAsync(id, new(input.Content), cancellationToken);
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken cancellationToken)
    {
        await commentService.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
