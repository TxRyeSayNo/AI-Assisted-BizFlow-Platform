using System;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.Common;
using BizFlow.Application.Security;

namespace BizFlow.Application.Reports;

public sealed class ReportingService(
    IReportingReader reader,
    ITenantContext tenantContext)
{
    private const int MaxWorkloadRangeDays = 366;

    public async Task<ManagerDashboardResult> GetManagerDashboardAsync(
        Guid? departmentId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var tenantId = GetRequiredTenantId();
        ValidateDateRange(from, to);

        return await reader.GetManagerDashboardAsync(tenantId, departmentId, from, to, ct);
    }

    public async Task<CompanyReportResult> GetCompanyReportAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var tenantId = GetRequiredTenantId();
        ValidateDateRange(from, to);

        return await reader.GetCompanyReportAsync(tenantId, from, to, ct);
    }

    public async Task<WorkloadReportResult> GetWorkloadReportAsync(
        Guid? departmentId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var tenantId = GetRequiredTenantId();
        ValidateDateRange(from, to);

        if (from.HasValue && to.HasValue && (to.Value - from.Value).TotalDays > MaxWorkloadRangeDays)
        {
            throw new ApplicationFault(
                FaultKind.Validation,
                "REPORT.RANGE_EXCEEDS_MAX",
                $"Workload report date range cannot exceed {MaxWorkloadRangeDays} days.");
        }

        return await reader.GetWorkloadReportAsync(tenantId, departmentId, from, to, ct);
    }

    public async Task<SlaPerformanceReportResult> GetSlaPerformanceReportAsync(
        Guid? serviceId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var tenantId = GetRequiredTenantId();
        ValidateDateRange(from, to);

        return await reader.GetSlaPerformanceReportAsync(tenantId, serviceId, from, to, ct);
    }

    private Guid GetRequiredTenantId()
    {
        if (tenantContext.TenantId is not { } tenantId || tenantId == Guid.Empty)
        {
            throw new ApplicationFault(FaultKind.Forbidden, "TENANT.REQUIRED", "Active tenant context is required.");
        }
        return tenantId;
    }

    private static void ValidateDateRange(DateTimeOffset? from, DateTimeOffset? to)
    {
        if (from.HasValue && to.HasValue && from.Value > to.Value)
        {
            throw new ApplicationFault(
                FaultKind.Validation,
                "REPORT.INVALID_DATE_RANGE",
                "The 'from' date must be earlier than or equal to the 'to' date.");
        }
    }
}
