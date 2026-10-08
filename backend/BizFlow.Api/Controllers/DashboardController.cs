using System;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/dashboard")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DashboardController(ReportingService reportingService) : ControllerBase
{
    [HttpGet("manager")]
    public async Task<ActionResult<ManagerDashboardResult>> GetManagerDashboard(
        [FromQuery] Guid? departmentId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var result = await reportingService.GetManagerDashboardAsync(departmentId, from, to, cancellationToken);
        return Ok(result);
    }
}
