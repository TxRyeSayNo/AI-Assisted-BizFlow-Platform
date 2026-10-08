using System.Text.Json.Serialization;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/attachments")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AttachmentsController(AttachmentService attachmentService) : ControllerBase
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record CreateUploadSessionInput(
        string ObjectType,
        Guid ObjectId,
        string FileName,
        string ContentType,
        long SizeBytes);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record FinalizeAttachmentInput(
        Guid AttachmentId,
        string? ClientHash);

    [HttpPost("upload-session")]
    public async Task<ActionResult<UploadSessionResult>> CreateUploadSession(
        CreateUploadSessionInput input,
        CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1)
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");
        var key = keys.Count == 0 ? null : keys[0];

        var result = await attachmentService.CreateUploadSessionAsync(
            new(input.ObjectType, input.ObjectId, input.FileName, input.ContentType, input.SizeBytes),
            key,
            cancellationToken);

        return Created($"/api/v1/attachments/{result.AttachmentId}", result);
    }

    [HttpPut("upload-session/{id:guid}/binary")]
    [RequestSizeLimit(524_288_000)]
    public async Task<IActionResult> UploadBinary(
        Guid id,
        CancellationToken cancellationToken)
    {
        await attachmentService.DirectBinaryUploadAsync(id, Request.Body, cancellationToken);
        return NoContent();
    }

    [HttpPost("finalize")]
    public async Task<ActionResult<AttachmentItemView>> FinalizeAttachment(
        FinalizeAttachmentInput input,
        CancellationToken cancellationToken)
    {
        var result = await attachmentService.FinalizeAttachmentAsync(
            new(input.AttachmentId, input.ClientHash),
            cancellationToken);

        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AttachmentItemView>>> List(
        [FromQuery] string objectType,
        [FromQuery] Guid objectId,
        CancellationToken cancellationToken)
    {
        var results = await attachmentService.ListAttachmentsAsync(objectType, objectId, cancellationToken);
        return Ok(results);
    }

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(
        Guid id,
        CancellationToken cancellationToken)
    {
        var descriptor = await attachmentService.GetDownloadAsync(id, cancellationToken);
        return File(descriptor.Content, descriptor.ContentType, descriptor.FileName, enableRangeProcessing: true);
    }

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(
        Guid id,
        CancellationToken cancellationToken)
    {
        await attachmentService.DeleteAttachmentAsync(id, cancellationToken);
        return NoContent();
    }
}
