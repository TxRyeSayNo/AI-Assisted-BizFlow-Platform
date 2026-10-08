using System;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReportsController(ReportingService reportingService) : ControllerBase
{
    [HttpGet("company")]
    public async Task<ActionResult<CompanyReportResult>> GetCompanyReport(
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var result = await reportingService.GetCompanyReportAsync(from, to, cancellationToken);
        return Ok(result);
    }

    [HttpGet("workload")]
    public async Task<ActionResult<WorkloadReportResult>> GetWorkloadReport(
        [FromQuery] Guid? departmentId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var result = await reportingService.GetWorkloadReportAsync(departmentId, from, to, cancellationToken);
        return Ok(result);
    }

    [HttpGet("sla")]
    public async Task<ActionResult<SlaPerformanceReportResult>> GetSlaPerformanceReport(
        [FromQuery] Guid? serviceId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        CancellationToken cancellationToken = default)
    {
        var result = await reportingService.GetSlaPerformanceReportAsync(serviceId, from, to, cancellationToken);
        return Ok(result);
    }
}
