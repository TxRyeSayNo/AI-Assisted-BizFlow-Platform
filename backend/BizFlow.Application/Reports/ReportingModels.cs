using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BizFlow.Application.Reports;

public sealed record TaskMetricsView(
    int TotalTasks,
    int ActiveTasks,
    int CompletedTasks,
    int OverdueTasks,
    int CancelledTasks,
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyDictionary<string, int> ByPriority);

public sealed record RequestMetricsView(
    int TotalRequests,
    int PendingRequests,
    int ClosedRequests,
    int RejectedRequests,
    int CancelledRequests,
    IReadOnlyDictionary<string, int> ByStatus,
    IReadOnlyDictionary<string, int> ByPriority);

public sealed record DepartmentWorkloadSummaryView(
    Guid DepartmentId,
    string DepartmentName,
    int ActiveTasks,
    int CompletedTasks,
    int OverdueTasks,
    int TotalRequests);

public sealed record ManagerDashboardResult(
    TaskMetricsView TaskMetrics,
    RequestMetricsView RequestMetrics,
    IReadOnlyList<DepartmentWorkloadSummaryView> DepartmentSummaries,
    DateTimeOffset GeneratedAt);

public sealed record CompanyDepartmentMetricView(
    Guid DepartmentId,
    string DepartmentName,
    int TotalTasks,
    int CompletedTasks,
    int TotalRequests,
    int ClosedRequests);

public sealed record CompanyReportResult(
    int TotalTasksCreated,
    int TotalTasksCompleted,
    double TaskCompletionRatePercent,
    double? AverageTaskCompletionTimeHours,
    int TotalRequestsSubmitted,
    int TotalRequestsClosed,
    double RequestResolutionRatePercent,
    double? AverageRequestResolutionTimeHours,
    IReadOnlyList<CompanyDepartmentMetricView> DepartmentMetrics,
    int RecentAuditActivityCount,
    DateTimeOffset GeneratedAt);

public sealed record UserWorkloadItemView(
    Guid UserId,
    string FullName,
    string EmployeeCode,
    Guid? DepartmentId,
    string? DepartmentName,
    int ActiveTasks,
    int CompletedTasks,
    int OverdueTasks,
    int TotalAssigned);

public sealed record DepartmentWorkloadItemView(
    Guid DepartmentId,
    string DepartmentName,
    int ActiveTasks,
    int TotalMembers,
    double AvgTasksPerMember);

public sealed record WorkloadReportResult(
    IReadOnlyList<UserWorkloadItemView> Items,
    IReadOnlyList<DepartmentWorkloadItemView> DepartmentBreakdown,
    DateTimeOffset GeneratedAt);

public sealed record ServiceSlaMetricView(
    Guid ServiceId,
    string ServiceName,
    int TotalRequests,
    int CompliantRequests,
    int BreachedRequests,
    double ComplianceRatePercent);

public sealed record SlaPerformanceReportResult(
    int TotalTracked,
    int CompliantCount,
    int BreachedCount,
    double ComplianceRatePercent,
    double? AvgElapsedHours,
    IReadOnlyList<ServiceSlaMetricView> ServiceBreakdown,
    DateTimeOffset GeneratedAt);

public interface IReportingReader
{
    Task<ManagerDashboardResult> GetManagerDashboardAsync(
        Guid tenantId,
        Guid? departmentId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default);

    Task<CompanyReportResult> GetCompanyReportAsync(
        Guid tenantId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default);

    Task<WorkloadReportResult> GetWorkloadReportAsync(
        Guid tenantId,
        Guid? departmentId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default);

    Task<SlaPerformanceReportResult> GetSlaPerformanceReportAsync(
        Guid tenantId,
        Guid? serviceId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default);
}
