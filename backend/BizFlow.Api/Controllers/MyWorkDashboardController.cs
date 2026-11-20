using BizFlow.Application.Dashboard;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

/// <summary>
/// The employee dashboard. No user identifier is accepted: the subject is always the
/// authenticated caller, resolved from the validated principal.
/// </summary>
[ApiController, Authorize, Route("api/v1/dashboard/my-work")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class MyWorkDashboardController(MyWorkDashboardQuery myWork) : ControllerBase
{
    [HttpGet]
    public Task<MyWorkDashboardView> Get(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null,
        CancellationToken cancellationToken = default) =>
        myWork.GetAsync(new(page, pageSize, status), cancellationToken);
}
