using BizFlow.Application.Collaboration;
using BizFlow.Application.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using BizFlow.Application.Common;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/tasks")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TasksController(TaskListQuery tasks, TaskCreation creation, TaskDetailQuery detail, TaskAssignmentCommand assignment, TaskAcceptanceCommand acceptance, TaskExecutionCommand execution, TaskSubmissionCommand submission, TaskConfirmationCommand confirmation, CommentService commentService, AttachmentService attachmentService) : ControllerBase
{
    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record SubmissionInput(string? Content);

    [HttpPost("{id:guid}/result")]
    public Task<TaskSubmittedView> SubmitResult(Guid id, SubmissionInput input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1) throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");
        return submission.SubmitAsync(id, input.Content, keys.Count == 0 ? null : keys[0], cancellationToken);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ConfirmationInput(string Decision, string? Note);

    [HttpPost("{id:guid}/confirmation")]
    public Task<TaskConfirmedView> ConfirmResult(Guid id, ConfirmationInput input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1) throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");
        return confirmation.ConfirmAsync(id, input.Decision, input.Note, keys.Count == 0 ? null : keys[0], cancellationToken);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record StartInput;

    [HttpPost("{id:guid}/start")]
    public Task<TaskStartedView> Start(Guid id, StartInput input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1) throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");
        return execution.StartAsync(id, keys.Count == 0 ? null : keys[0], cancellationToken);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record AcceptanceInput(string? Note);

    [HttpPost("{id:guid}/accept")]
    public Task<TaskAcceptedView> Accept(Guid id, AcceptanceInput input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1) throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");
        return acceptance.AcceptAsync(id, input.Note, keys.Count == 0 ? null : keys[0], cancellationToken);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record AssignmentInput(string TargetType, Guid TargetId, string? Note);

    [HttpPost("{id:guid}/assign")]
    public Task<TaskAssignedView> Assign(Guid id, AssignmentInput input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1) throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");
        return assignment.AssignAsync(id, new(input.TargetType, input.TargetId, input.Note), keys.Count == 0 ? null : keys[0], cancellationToken);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record CreateInput(string Title, string? Description, string? Priority, string? Deadline, string[]? Checklist, Guid? RequestId = null);

    [HttpPost]
    public async Task<IActionResult> Create(CreateInput input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1) throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");
        var result = await creation.CreateAsync(new(input.Title, input.Description, input.Priority, input.Deadline, input.Checklist, input.RequestId),
            keys.Count == 0 ? null : keys[0], cancellationToken);
        return Created($"/api/v1/tasks/{result.TaskId}", result);
    }

    [HttpGet("{id:guid}")]
    public Task<TaskDetailView> Detail(Guid id, [FromQuery] int checklistPage = 1, [FromQuery] int reportPage = 1,
        [FromQuery] int resultPage = 1, [FromQuery] int pageSize = 25, CancellationToken cancellationToken = default) =>
        detail.GetAsync(id, new(checklistPage, reportPage, resultPage, pageSize), cancellationToken);

    [HttpGet]
    public Task<TaskListPage> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] string? priority = null,
        [FromQuery] Guid? requestId = null,
        CancellationToken cancellationToken = default) =>
        tasks.ListAsync(new(page, pageSize, search, status, priority, requestId), cancellationToken);

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record TaskCommentInput(string Content);

    [HttpPost("{id:guid}/comments")]
    public async Task<ActionResult<CommentItemView>> AddComment(Guid id, TaskCommentInput input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1) throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");
        var key = keys.Count == 0 ? null : keys[0];
        var result = await commentService.CreateAsync(new("TASK", id, input.Content), key, cancellationToken);
        return Created($"/api/v1/comments/{result.CommentId}", result);
    }

    [HttpGet("{id:guid}/comments")]
    public async Task<ActionResult<IReadOnlyList<CommentItemView>>> GetComments(Guid id, CancellationToken cancellationToken)
    {
        var results = await commentService.ListAsync("TASK", id, cancellationToken);
        return Ok(results);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record TaskUploadSessionInput(string FileName, string ContentType, long SizeBytes);

    [HttpPost("{id:guid}/attachments/upload-session")]
    public async Task<ActionResult<UploadSessionResult>> CreateAttachmentUploadSession(
        Guid id, TaskUploadSessionInput input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1) throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");
        var key = keys.Count == 0 ? null : keys[0];
        var result = await attachmentService.CreateUploadSessionAsync(
            new("TASK", id, input.FileName, input.ContentType, input.SizeBytes), key, cancellationToken);
        return Created($"/api/v1/attachments/{result.AttachmentId}", result);
    }

    [HttpGet("{id:guid}/attachments")]
    public async Task<ActionResult<IReadOnlyList<AttachmentItemView>>> GetAttachments(Guid id, CancellationToken cancellationToken)
    {
        var results = await attachmentService.ListAttachmentsAsync("TASK", id, cancellationToken);
        return Ok(results);
    }
}
