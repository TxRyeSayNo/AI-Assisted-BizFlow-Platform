using BizFlow.Api.Configuration;
using BizFlow.Application.Common;
using BizFlow.Application.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BizFlow.Api.Controllers;

/// <summary>
/// Read-only deadline calendar (F2), on its own route so <see cref="TasksController"/> is
/// untouched. It never changes a task: cards link to the task detail page.
/// </summary>
[ApiController, Authorize, Route("api/v1/tasks/calendar")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TaskCalendarController(TaskCalendarQuery calendar, IOptions<FeatureFlagOptions> features) : ControllerBase
{
    [HttpGet]
    public Task<TaskCalendarView> Get(
        [FromQuery] DateTimeOffset from,
        [FromQuery] DateTimeOffset to,
        [FromQuery] string? status = null,
        [FromQuery] string? priority = null,
        [FromQuery] Guid? assignedUserId = null,
        [FromQuery] Guid? assignedDepartmentId = null,
        [FromQuery] int limit = 500,
        CancellationToken cancellationToken = default)
    {
        if (!features.Value.Calendar) throw ApplicationFault.NotFound();
        return calendar.GetAsync(new(from, to, status, priority, assignedUserId, assignedDepartmentId, limit),
            cancellationToken);
    }
}
