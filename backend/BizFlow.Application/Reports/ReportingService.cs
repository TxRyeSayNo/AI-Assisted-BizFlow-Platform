using System;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.Common;
using BizFlow.Application.Security;

namespace BizFlow.Application.Reports;

public sealed class ReportingService(
    IReportingReader reader,
    ITenantContext tenantContext,
    IBizFlowCacheService? cache = null)
{
    private const int MaxWorkloadRangeDays = 366;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromSeconds(30);

    public async Task<ManagerDashboardResult> GetManagerDashboardAsync(
        Guid? departmentId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var tenantId = GetRequiredTenantId();
        ValidateDateRange(from, to);

        if (cache is not null)
        {
            var cacheKey = $"tenant:{tenantId}:report:manager:{departmentId}:{from?.UtcTicks}:{to?.UtcTicks}";
            var cached = await cache.GetAsync<ManagerDashboardResult>(cacheKey, ct);
            if (cached is not null)
                return cached;

            var fresh = await reader.GetManagerDashboardAsync(tenantId, departmentId, from, to, ct);
            await cache.SetAsync(cacheKey, fresh, CacheDuration, ct);
            return fresh;
        }

        return await reader.GetManagerDashboardAsync(tenantId, departmentId, from, to, ct);
    }

    public async Task<CompanyReportResult> GetCompanyReportAsync(
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var tenantId = GetRequiredTenantId();
        ValidateDateRange(from, to);

        if (cache is not null)
        {
            var cacheKey = $"tenant:{tenantId}:report:company:{from?.UtcTicks}:{to?.UtcTicks}";
            var cached = await cache.GetAsync<CompanyReportResult>(cacheKey, ct);
            if (cached is not null)
                return cached;

            var fresh = await reader.GetCompanyReportAsync(tenantId, from, to, ct);
            await cache.SetAsync(cacheKey, fresh, CacheDuration, ct);
            return fresh;
        }

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

        if (cache is not null)
        {
            var cacheKey = $"tenant:{tenantId}:report:workload:{departmentId}:{from?.UtcTicks}:{to?.UtcTicks}";
            var cached = await cache.GetAsync<WorkloadReportResult>(cacheKey, ct);
            if (cached is not null)
                return cached;

            var fresh = await reader.GetWorkloadReportAsync(tenantId, departmentId, from, to, ct);
            await cache.SetAsync(cacheKey, fresh, CacheDuration, ct);
            return fresh;
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

        if (cache is not null)
        {
            var cacheKey = $"tenant:{tenantId}:report:sla:{serviceId}:{from?.UtcTicks}:{to?.UtcTicks}";
            var cached = await cache.GetAsync<SlaPerformanceReportResult>(cacheKey, ct);
            if (cached is not null)
                return cached;

            var fresh = await reader.GetSlaPerformanceReportAsync(tenantId, serviceId, from, to, ct);
            await cache.SetAsync(cacheKey, fresh, CacheDuration, ct);
            return fresh;
        }

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
