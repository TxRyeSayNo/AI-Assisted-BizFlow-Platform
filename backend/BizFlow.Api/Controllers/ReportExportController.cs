using BizFlow.Api.Configuration;
using BizFlow.Application.Common;
using BizFlow.Application.Reports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace BizFlow.Api.Controllers;

/// <summary>
/// Downloads for the existing reports (F5). It sits on its own route segment, so
/// <see cref="ReportsController"/> keeps every URL and payload it already publishes, and it reuses
/// the same <c>ReportingService</c> calls, which means the permission applied to a download is by
/// construction the permission applied to the matching view.
/// </summary>
[ApiController, Authorize, Route("api/v1/reports")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ReportExportController(
    ReportExportService exports,
    IOptions<FeatureFlagOptions> features) : ControllerBase
{
    [HttpGet("company/export")]
    public async Task<IActionResult> Company([FromQuery] string? format, [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken)
    {
        Require();
        return File(await exports.ExportCompanyAsync(from, to,
            ReportExportService.ParseFormat(format), cancellationToken));
    }

    [HttpGet("workload/export")]
    public async Task<IActionResult> Workload([FromQuery] string? format, [FromQuery] Guid? departmentId,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken)
    {
        Require();
        return File(await exports.ExportWorkloadAsync(departmentId, from, to,
            ReportExportService.ParseFormat(format), cancellationToken));
    }

    [HttpGet("sla/export")]
    public async Task<IActionResult> Sla([FromQuery] string? format, [FromQuery] Guid? serviceId,
        [FromQuery] DateTimeOffset? from, [FromQuery] DateTimeOffset? to, CancellationToken cancellationToken)
    {
        Require();
        return File(await exports.ExportSlaAsync(serviceId, from, to,
            ReportExportService.ParseFormat(format), cancellationToken));
    }

    private FileContentResult File(ReportExportFile file) =>
        base.File(file.Content, file.ContentType, file.FileName);

    private void Require()
    {
        if (!features.Value.ReportExport) throw ApplicationFault.NotFound();
    }
}
