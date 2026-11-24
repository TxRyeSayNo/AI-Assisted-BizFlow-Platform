using BizFlow.Api.Configuration;
using BizFlow.Application.Common;
using BizFlow.Application.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BizFlow.Api.Controllers;

/// <summary>
/// Read-only Kanban projection of the task list. It lives on its own route so that
/// <see cref="TasksController"/> keeps every URL, DTO and status code it already publishes.
/// The board never changes a task's state: the client replays the existing lifecycle endpoints
/// (<c>/assign</c>, <c>/accept</c>, <c>/start</c>, <c>/result</c>, <c>/confirm</c>) on drop, which
/// keeps TaskLifecyclePolicy, authorization, audit and SLA handling exactly where they are.
/// </summary>
[ApiController, Authorize, Route("api/v1/tasks/board")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TaskBoardController(TaskBoardQuery board, IOptions<FeatureFlagOptions> features) : ControllerBase
{
    [HttpGet]
    public Task<TaskBoardView> Get(
        [FromQuery] string? search = null,
        [FromQuery] string? priority = null,
        [FromQuery] Guid? assignedUserId = null,
        [FromQuery] Guid? assignedDepartmentId = null,
        [FromQuery] DateTimeOffset? deadlineFrom = null,
        [FromQuery] DateTimeOffset? deadlineTo = null,
        [FromQuery] Guid? requestId = null,
        [FromQuery] bool? isParentOnly = null,
        [FromQuery] Guid? labelId = null,
        [FromQuery] int columnSize = 50,
        CancellationToken cancellationToken = default)
    {
        // A disabled feature must be indistinguishable from one that was never deployed.
        if (!features.Value.Kanban) throw ApplicationFault.NotFound();
        return board.GetAsync(new(search, priority, assignedUserId, assignedDepartmentId,
            deadlineFrom, deadlineTo, requestId, isParentOnly, labelId, columnSize), cancellationToken);
    }
}
