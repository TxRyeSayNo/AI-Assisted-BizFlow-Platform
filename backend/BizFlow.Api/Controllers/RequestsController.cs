using System.Text.Json.Serialization;
using BizFlow.Application.AI;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Common;
using BizFlow.Application.Requests;
using BizFlow.Application.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/requests")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RequestsController(
    RequestListQuery requests,
    RequestCreation creation,
    RequestSubmissionCommand submission,
    RequestRoutingCommand routingCommand,
    RequestReceiptCommand receiptCommand,
    RequestProcessingCommand processingCommand,
    RequestRejectionCommand rejectionCommand,
    RequestCancellationCommand cancellationCommand,
    RequestResolutionCommand resolutionCommand,
    RequestConfirmationCommand confirmationCommand,
    RequestReviseCommand reviseCommand,
    RequestDetailQuery detail,
    TaskCreation taskCreation,
    TaskListQuery taskListQuery,
    CommentService commentService,
    AttachmentService attachmentService,
    AiAssistanceService aiService) : ControllerBase
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record CreateRequestInput(
        Guid ServiceId,
        Guid CategoryId,
        string Title,
        string Description,
        string? Priority,
        Guid? ParentRequestId,
        bool? SubmitImmediately);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record RouteRequestDto(
        Guid? ToDepartmentId,
        Guid? ToUserId,
        string? Reason,
        string? Source);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ReceiveRequestDto(string? Note);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record RejectRequestDto(string Reason);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ResolveRequestDto(string Content);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ConfirmRequestDto(string Decision, string? Note);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ReviseRequestDto(
        string? Title,
        string? Description,
        string? Priority,
        bool? SubmitImmediately);

    [HttpPost]
    public async Task<IActionResult> Create(CreateRequestInput input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1)
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");

        var result = await creation.CreateAsync(
            new(input.ServiceId, input.CategoryId, input.Title, input.Description, input.Priority, input.ParentRequestId, input.SubmitImmediately ?? false),
            keys.Count == 0 ? null : keys[0],
            cancellationToken);

        return Created($"/api/v1/requests/{result.RequestId}", result);
    }

    [HttpPost("{id:guid}/submit")]
    public async Task<IActionResult> Submit(Guid id, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1)
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");

        var result = await submission.SubmitAsync(id, keys.Count == 0 ? null : keys[0], cancellationToken);
        return Ok(result);
    }

    [HttpPost("{id:guid}/route")]
    public async Task<IActionResult> Route(Guid id, RouteRequestDto input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1)
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");

        var source = BizFlow.Domain.Requests.RequestRoutingSource.Manual;
        if (!string.IsNullOrWhiteSpace(input.Source))
        {
            if (string.Equals(input.Source, "AI", StringComparison.OrdinalIgnoreCase))
                source = BizFlow.Domain.Requests.RequestRoutingSource.Ai;
            else if (string.Equals(input.Source, "RULE", StringComparison.OrdinalIgnoreCase))
                source = BizFlow.Domain.Requests.RequestRoutingSource.Rule;
            else if (!string.Equals(input.Source, "MANUAL", StringComparison.OrdinalIgnoreCase))
                throw new ApplicationFault(FaultKind.Validation, "REQUEST.INVALID_SOURCE", "Invalid routing source.");
        }

        var result = await routingCommand.RouteAsync(
            id,
            new(input.ToDepartmentId, input.ToUserId, input.Reason, source),
            keys.Count == 0 ? null : keys[0],
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:guid}/receive")]
    public async Task<IActionResult> Receive(Guid id, ReceiveRequestDto? input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1)
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");

        var result = await receiptCommand.ReceiveAsync(
            id,
            new(input?.Note),
            keys.Count == 0 ? null : keys[0],
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:guid}/start")]
    public async Task<IActionResult> Start(Guid id, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1)
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");

        var result = await processingCommand.StartAsync(
            id,
            keys.Count == 0 ? null : keys[0],
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:guid}/reject")]
    public async Task<IActionResult> Reject(Guid id, RejectRequestDto input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1)
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");

        var result = await rejectionCommand.RejectAsync(
            id,
            new(input.Reason),
            keys.Count == 0 ? null : keys[0],
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:guid}/cancel")]
    public async Task<IActionResult> Cancel(Guid id, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1)
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");

        var result = await cancellationCommand.CancelAsync(
            id,
            keys.Count == 0 ? null : keys[0],
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:guid}/resolve")]
    public async Task<IActionResult> Resolve(Guid id, ResolveRequestDto input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1)
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");

        var result = await resolutionCommand.ResolveAsync(
            id,
            new(input.Content),
            keys.Count == 0 ? null : keys[0],
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:guid}/confirm")]
    [HttpPost("{id:guid}/confirmation")]
    public async Task<IActionResult> Confirm(Guid id, ConfirmRequestDto input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1)
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");

        var result = await confirmationCommand.ConfirmAsync(
            id,
            new(input.Decision, input.Note),
            keys.Count == 0 ? null : keys[0],
            cancellationToken);

        return Ok(result);
    }

    [HttpPost("{id:guid}/revise")]
    public async Task<IActionResult> Revise(Guid id, ReviseRequestDto input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1)
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");

        var result = await reviseCommand.ReviseAsync(
            id,
            new(input.Title, input.Description, input.Priority, input.SubmitImmediately ?? false),
            keys.Count == 0 ? null : keys[0],
            cancellationToken);

        return Created($"/api/v1/requests/{result.RequestId}", result);
    }

    [HttpGet("{id:guid}")]
    public Task<RequestDetailView> Detail(Guid id, CancellationToken cancellationToken = default) =>
        detail.GetAsync(id, cancellationToken);

    [HttpGet]
    public Task<RequestListPage> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] Guid? serviceId = null,
        [FromQuery] Guid? categoryId = null,
        CancellationToken cancellationToken = default) =>
        requests.ListAsync(new(page, pageSize, search, status, priority, serviceId, categoryId), cancellationToken);

    [HttpGet("{id:guid}/tasks")]
    public Task<TaskListPage> GetTasks(Guid id, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken cancellationToken = default) =>
        taskListQuery.ListAsync(new(page, pageSize, null, null, null, id), cancellationToken);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record CreateTaskForRequestInput(
        string Title,
        string? Description = null,
        string? Priority = null,
        string? Deadline = null,
        string[]? Checklist = null);

    [HttpPost("{id:guid}/tasks")]
    public async Task<IActionResult> CreateTask(Guid id, CreateTaskForRequestInput input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1)
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");

        var result = await taskCreation.CreateAsync(
            new(input.Title, input.Description, input.Priority, input.Deadline, input.Checklist, id),
            keys.Count == 0 ? null : keys[0],
            cancellationToken);

        return Created($"/api/v1/tasks/{result.TaskId}", result);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record RequestCommentInput(string Content);

    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<CommentItemView>> AddComment(Guid id, RequestCommentInput input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1)
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");

        var key = keys.Count == 0 ? null : keys[0];
        var result = await commentService.CreateAsync(new("REQUEST", id, input.Content), key, cancellationToken);
        return Created($"/api/v1/comments/{result.CommentId}", result);
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<CommentItemView>>> GetComments(Guid id, CancellationToken cancellationToken)
    {
        var results = await commentService.ListAsync("REQUEST", id, cancellationToken);
        return Ok(results);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record RequestUploadSessionInput(string FileName, string ContentType, long SizeBytes);

    [HttpPost("{id:guid}/attachments/upload-session")]
    public async Task<ActionResult<UploadSessionResult>> CreateAttachmentUploadSession(
        Guid id, RequestUploadSessionInput input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1)
            throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");

        var key = keys.Count == 0 ? null : keys[0];
        var result = await attachmentService.CreateUploadSessionAsync(
            new("REQUEST", id, input.FileName, input.ContentType, input.SizeBytes), key, cancellationToken);
        return Created($"/api/v1/attachments/{result.AttachmentId}", result);
    }

    [HttpGet("{id:guid}/attachments")]
    public async Task<ActionResult<IReadOnlyList<AttachmentItemView>>> GetAttachments(Guid id, CancellationToken cancellationToken)
    {
        var results = await attachmentService.ListAttachmentsAsync("REQUEST", id, cancellationToken);
        return Ok(results);
    }

    [HttpPost("{id:guid}/split")]
    public async Task<ActionResult<SplitRequestResult>> SplitRequest(
        Guid id,
        [FromBody] SplitRequestCommand command,
        CancellationToken cancellationToken)
    {
        var result = await aiService.SplitRequestAsync(id, command, cancellationToken);
        return Ok(result);
    }
}
