using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.Common;
using BizFlow.Application.Reports;
using BizFlow.Application.Security;
using Xunit;

namespace BizFlow.UnitTests.Reports;

public sealed class ReportingServiceTests
{
    private sealed class FakeTenantContext(Guid? tenantId) : ITenantContext
    {
        public Guid? UserId => Guid.NewGuid();
        public Guid? TenantId => tenantId;
    }

    private sealed class FakeReportingReader : IReportingReader
    {
        public ManagerDashboardResult? DashboardResult { get; set; }
        public CompanyReportResult? CompanyResult { get; set; }
        public WorkloadReportResult? WorkloadResult { get; set; }
        public SlaPerformanceReportResult? SlaResult { get; set; }

        public Task<ManagerDashboardResult> GetManagerDashboardAsync(Guid tenantId, Guid? departmentId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default) =>
            Task.FromResult(DashboardResult ?? new ManagerDashboardResult(
                new TaskMetricsView(10, 5, 3, 1, 1, new Dictionary<string, int>(), new Dictionary<string, int>()),
                new RequestMetricsView(8, 4, 3, 1, 0, new Dictionary<string, int>(), new Dictionary<string, int>()),
                new List<DepartmentWorkloadSummaryView>(),
                DateTimeOffset.UtcNow));

        public Task<CompanyReportResult> GetCompanyReportAsync(Guid tenantId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default) =>
            Task.FromResult(CompanyResult ?? new CompanyReportResult(
                20, 15, 75.0, 12.5, 10, 8, 80.0, 24.0,
                new List<CompanyDepartmentMetricView>(), 50, DateTimeOffset.UtcNow));

        public Task<WorkloadReportResult> GetWorkloadReportAsync(Guid tenantId, Guid? departmentId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default) =>
            Task.FromResult(WorkloadResult ?? new WorkloadReportResult(
                new List<UserWorkloadItemView>(), new List<DepartmentWorkloadItemView>(), DateTimeOffset.UtcNow));

        public Task<SlaPerformanceReportResult> GetSlaPerformanceReportAsync(Guid tenantId, Guid? serviceId, DateTimeOffset? from, DateTimeOffset? to, CancellationToken ct = default) =>
            Task.FromResult(SlaResult ?? new SlaPerformanceReportResult(
                25, 23, 2, 92.0, 18.2, new List<ServiceSlaMetricView>(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public async Task ReportingService_requires_tenant_context()
    {
        var reader = new FakeReportingReader();
        var service = new ReportingService(reader, new FakeTenantContext(null));

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.GetManagerDashboardAsync(null, null, null));
        Assert.Equal("TENANT.REQUIRED", ex.Code);
        Assert.Equal(FaultKind.Forbidden, ex.Kind);
    }

    [Fact]
    public async Task ReportingService_validates_date_range_order()
    {
        var tenantId = Guid.NewGuid();
        var reader = new FakeReportingReader();
        var service = new ReportingService(reader, new FakeTenantContext(tenantId));

        var from = DateTimeOffset.UtcNow;
        var to = from.AddDays(-1);

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.GetManagerDashboardAsync(null, from, to));
        Assert.Equal("REPORT.INVALID_DATE_RANGE", ex.Code);
        Assert.Equal(FaultKind.Validation, ex.Kind);
    }

    [Fact]
    public async Task ReportingService_validates_workload_max_range()
    {
        var tenantId = Guid.NewGuid();
        var reader = new FakeReportingReader();
        var service = new ReportingService(reader, new FakeTenantContext(tenantId));

        var from = DateTimeOffset.UtcNow.AddDays(-400);
        var to = DateTimeOffset.UtcNow;

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.GetWorkloadReportAsync(null, from, to));
        Assert.Equal("REPORT.RANGE_EXCEEDS_MAX", ex.Code);
        Assert.Equal(FaultKind.Validation, ex.Kind);
        Assert.Contains("366", ex.Message);
    }

    [Fact]
    public async Task ReportingService_delegates_manager_dashboard_to_reader()
    {
        var tenantId = Guid.NewGuid();
        var reader = new FakeReportingReader();
        var service = new ReportingService(reader, new FakeTenantContext(tenantId));

        var result = await service.GetManagerDashboardAsync(null, null, null);
        Assert.NotNull(result);
        Assert.Equal(10, result.TaskMetrics.TotalTasks);
        Assert.Equal(8, result.RequestMetrics.TotalRequests);
    }

    [Fact]
    public async Task ReportingService_delegates_company_report_to_reader()
    {
        var tenantId = Guid.NewGuid();
        var reader = new FakeReportingReader();
        var service = new ReportingService(reader, new FakeTenantContext(tenantId));

        var result = await service.GetCompanyReportAsync(null, null);
        Assert.NotNull(result);
        Assert.Equal(20, result.TotalTasksCreated);
        Assert.Equal(75.0, result.TaskCompletionRatePercent);
    }

    [Fact]
    public async Task ReportingService_delegates_workload_report_to_reader()
    {
        var tenantId = Guid.NewGuid();
        var reader = new FakeReportingReader();
        var service = new ReportingService(reader, new FakeTenantContext(tenantId));

        var from = DateTimeOffset.UtcNow.AddDays(-30);
        var to = DateTimeOffset.UtcNow;

        var result = await service.GetWorkloadReportAsync(null, from, to);
        Assert.NotNull(result);
    }

    [Fact]
    public async Task ReportingService_delegates_sla_report_to_reader()
    {
        var tenantId = Guid.NewGuid();
        var reader = new FakeReportingReader();
        var service = new ReportingService(reader, new FakeTenantContext(tenantId));

        var result = await service.GetSlaPerformanceReportAsync(null, null, null);
        Assert.NotNull(result);
        Assert.Equal(25, result.TotalTracked);
        Assert.Equal(92.0, result.ComplianceRatePercent);
    }
}
