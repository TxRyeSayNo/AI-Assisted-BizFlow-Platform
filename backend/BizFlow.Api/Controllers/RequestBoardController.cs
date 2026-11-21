using BizFlow.Api.Configuration;
using BizFlow.Application.Common;
using BizFlow.Application.Requests;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BizFlow.Api.Controllers;

/// <summary>
/// Read-only board projection of the request list, on its own route so that
/// <see cref="RequestsController"/> keeps every URL, DTO and status code it already publishes.
/// Shares the Kanban feature flag with the task board.
/// </summary>
[ApiController, Authorize, Route("api/v1/requests/board")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RequestBoardController(RequestBoardQuery board, IOptions<FeatureFlagOptions> features) : ControllerBase
{
    [HttpGet]
    public Task<RequestBoardView> Get(
        [FromQuery] string? search = null,
        [FromQuery] string? priority = null,
        [FromQuery] Guid? serviceId = null,
        [FromQuery] Guid? categoryId = null,
        [FromQuery] Guid? requesterId = null,
        [FromQuery] int columnSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (!features.Value.Kanban) throw ApplicationFault.NotFound();
        return board.GetAsync(new(search, priority, serviceId, categoryId, requesterId, columnSize), cancellationToken);
    }
}
