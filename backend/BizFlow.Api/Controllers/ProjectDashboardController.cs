using BizFlow.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

/// <summary>
/// Read-only project dashboard. It owns its own route prefix so the existing
/// <c>api/v1/dashboard/manager</c> contract is untouched.
/// </summary>
[ApiController, Authorize, Route("api/v1/dashboard/projects")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ProjectDashboardController(ProjectDashboardQuery projects) : ControllerBase
{
    [HttpGet]
    public Task<ProjectDashboardPage> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? size = null,
        [FromQuery] Guid? departmentId = null,
        CancellationToken cancellationToken = default) =>
        projects.ListAsync(new(page, pageSize, search, size, departmentId), cancellationToken);

    [HttpGet("{id:guid}")]
    public Task<ProjectDashboardDetailView> Detail(Guid id, CancellationToken cancellationToken = default) =>
        projects.GetAsync(id, cancellationToken);
}
