using BizFlow.Application.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json.Serialization;
using BizFlow.Application.Common;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/tasks")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TasksController(TaskListQuery tasks, TaskCreation creation, TaskDetailQuery detail, TaskAssignmentCommand assignment, TaskAcceptanceCommand acceptance) : ControllerBase
{
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
    public sealed record CreateInput(string Title, string? Description, string? Priority, string? Deadline, string[]? Checklist);

    [HttpPost]
    public async Task<IActionResult> Create(CreateInput input, CancellationToken cancellationToken)
    {
        var keys = Request.Headers["Idempotency-Key"];
        if (keys.Count > 1) throw new ApplicationFault(FaultKind.Validation, "IDEMPOTENCY.INVALID_KEY", "Supply at most one Idempotency-Key header.");
        var result = await creation.CreateAsync(new(input.Title, input.Description, input.Priority, input.Deadline, input.Checklist),
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
        CancellationToken cancellationToken = default) =>
        tasks.ListAsync(new(page, pageSize, search, status, priority), cancellationToken);
}
