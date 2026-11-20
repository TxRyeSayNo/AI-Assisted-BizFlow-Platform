using BizFlow.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

/// <summary>
/// The subordinate-management dashboard. Requires managed or company-wide task read; a
/// department outside the caller's own management scope is refused, not filtered.
/// </summary>
[ApiController, Authorize, Route("api/v1/dashboard/team")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TeamDashboardController(TeamDashboardQuery team) : ControllerBase
{
    [HttpGet]
    public Task<TeamDashboardView> Get(
        [FromQuery] Guid? departmentId = null,
        CancellationToken cancellationToken = default) =>
        team.GetAsync(new(departmentId), cancellationToken);
}
