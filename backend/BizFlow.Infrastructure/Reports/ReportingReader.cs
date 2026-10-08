using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.Reports;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Reports;

public sealed class ReportingReader(BizFlowDbContext db) : IReportingReader
{
    public async Task<ManagerDashboardResult> GetManagerDashboardAsync(
        Guid tenantId,
        Guid? departmentId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var tenantTaskIdsQuery = db.WorkTasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.DeletedAt == null)
            .Select(t => t.Id);

        var activeAssignments = await db.TaskAssignments.AsNoTracking()
            .Where(a => a.EndedAt == null && tenantTaskIdsQuery.Contains(a.TaskId))
            .ToListAsync(ct);

        var tasksQuery = db.WorkTasks.AsNoTracking().Where(t => t.TenantId == tenantId && t.DeletedAt == null);
        if (departmentId.HasValue)
        {
            var deptTaskIds = activeAssignments
                .Where(a => a.DepartmentId == departmentId.Value)
                .Select(a => a.TaskId)
                .ToHashSet();
            tasksQuery = tasksQuery.Where(t => deptTaskIds.Contains(t.Id));
        }
        if (from.HasValue) tasksQuery = tasksQuery.Where(t => t.CreatedAt >= from.Value);
        if (to.HasValue) tasksQuery = tasksQuery.Where(t => t.CreatedAt <= to.Value);

        var taskRows = await tasksQuery.Select(t => new { t.Id, t.Status, t.Priority, t.Deadline }).ToListAsync(ct);

        int totalTasks = taskRows.Count;
        int completedTasks = taskRows.Count(t => t.Status == TaskState.Completed);
        int cancelledTasks = taskRows.Count(t => t.Status == TaskState.Cancelled);
        int overdueTasks = taskRows.Count(t => t.Status == TaskState.Overdue ||
            (t.Status != TaskState.Completed && t.Status != TaskState.Cancelled && t.Deadline.HasValue && t.Deadline.Value < now));
        int activeTasks = taskRows.Count(t => t.Status is TaskState.Assigned or TaskState.Accepted or TaskState.InProgress or TaskState.Submitted or TaskState.Confirmed);

        var tasksByStatus = taskRows
            .GroupBy(t => t.Status.ToString().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Count());

        var tasksByPriority = taskRows
            .GroupBy(t => t.Priority.ToString().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Count());

        var requestsQuery = db.Requests.AsNoTracking().Where(r => r.TenantId == tenantId && r.DeletedAt == null);
        if (from.HasValue) requestsQuery = requestsQuery.Where(r => r.CreatedAt >= from.Value);
        if (to.HasValue) requestsQuery = requestsQuery.Where(r => r.CreatedAt <= to.Value);

        var requestRows = await requestsQuery.Select(r => new { r.RequesterId, r.Status, r.Priority }).ToListAsync(ct);

        if (departmentId.HasValue)
        {
            var deptUserIds = await db.Users.AsNoTracking()
                .Where(u => u.TenantId == tenantId && u.DepartmentId == departmentId.Value)
                .Select(u => u.Id)
                .ToListAsync(ct);
            var deptUserSet = deptUserIds.ToHashSet();
            requestRows = requestRows.Where(r => deptUserSet.Contains(r.RequesterId)).ToList();
        }

        int totalRequests = requestRows.Count;
        int closedRequests = requestRows.Count(r => r.Status == RequestState.Closed);
        int rejectedRequests = requestRows.Count(r => r.Status == RequestState.Rejected);
        int cancelledRequests = requestRows.Count(r => r.Status == RequestState.Cancelled);
        int pendingRequests = requestRows.Count(r => r.Status is RequestState.Submitted or RequestState.InProgress or RequestState.Resolved or RequestState.Confirmed);

        var requestsByStatus = requestRows
            .GroupBy(r => r.Status.ToString().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Count());

        var requestsByPriority = requestRows
            .GroupBy(r => r.Priority.ToString().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.Count());

        var departments = await db.Departments.AsNoTracking().Where(d => d.TenantId == tenantId).ToListAsync(ct);
        var users = await db.Users.AsNoTracking().Where(u => u.TenantId == tenantId).Select(u => new { u.Id, u.DepartmentId }).ToListAsync(ct);
        var userDeptMap = users.Where(u => u.DepartmentId.HasValue).ToDictionary(u => u.Id, u => u.DepartmentId!.Value);

        var summaries = departments.Select(d =>
        {
            var deptTaskIds = activeAssignments.Where(a => a.DepartmentId == d.Id).Select(a => a.TaskId).ToHashSet();
            var dt = taskRows.Where(t => deptTaskIds.Contains(t.Id)).ToList();
            var reqCount = requestRows.Count(r => userDeptMap.TryGetValue(r.RequesterId, out var uDept) && uDept == d.Id);
            return new DepartmentWorkloadSummaryView(
                d.Id,
                d.Name,
                dt.Count(t => t.Status is TaskState.Assigned or TaskState.Accepted or TaskState.InProgress or TaskState.Submitted or TaskState.Confirmed),
                dt.Count(t => t.Status == TaskState.Completed),
                dt.Count(t => t.Status == TaskState.Overdue || (t.Status != TaskState.Completed && t.Status != TaskState.Cancelled && t.Deadline.HasValue && t.Deadline.Value < now)),
                reqCount);
        }).OrderBy(s => s.DepartmentName).ToList();

        return new ManagerDashboardResult(
            new TaskMetricsView(totalTasks, activeTasks, completedTasks, overdueTasks, cancelledTasks, tasksByStatus, tasksByPriority),
            new RequestMetricsView(totalRequests, pendingRequests, closedRequests, rejectedRequests, cancelledRequests, requestsByStatus, requestsByPriority),
            summaries,
            now);
    }

    public async Task<CompanyReportResult> GetCompanyReportAsync(
        Guid tenantId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var tenantTaskIdsQuery = db.WorkTasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.DeletedAt == null)
            .Select(t => t.Id);

        var activeAssignments = await db.TaskAssignments.AsNoTracking()
            .Where(a => a.EndedAt == null && tenantTaskIdsQuery.Contains(a.TaskId))
            .ToListAsync(ct);

        var tasksQuery = db.WorkTasks.AsNoTracking().Where(t => t.TenantId == tenantId && t.DeletedAt == null);
        if (from.HasValue) tasksQuery = tasksQuery.Where(t => t.CreatedAt >= from.Value);
        if (to.HasValue) tasksQuery = tasksQuery.Where(t => t.CreatedAt <= to.Value);
        var tasks = await tasksQuery.Select(t => new { t.Id, t.Status, t.CreatedAt, t.CompletedAt }).ToListAsync(ct);

        int totalTasksCreated = tasks.Count;
        int totalTasksCompleted = tasks.Count(t => t.Status == TaskState.Completed);
        double taskCompletionRatePercent = totalTasksCreated > 0 ? Math.Round((double)totalTasksCompleted / totalTasksCreated * 100.0, 1) : 0.0;

        var completedWithDates = tasks.Where(t => t.Status == TaskState.Completed && t.CompletedAt.HasValue).ToList();
        double? avgTaskCompletionHours = completedWithDates.Count > 0
            ? Math.Round(completedWithDates.Average(t => (t.CompletedAt!.Value - t.CreatedAt).TotalHours), 1)
            : null;

        var requestsQuery = db.Requests.AsNoTracking().Where(r => r.TenantId == tenantId && r.DeletedAt == null);
        if (from.HasValue) requestsQuery = requestsQuery.Where(r => r.CreatedAt >= from.Value);
        if (to.HasValue) requestsQuery = requestsQuery.Where(r => r.CreatedAt <= to.Value);
        var requests = await requestsQuery.Select(r => new { r.Id, r.RequesterId, r.Status, r.CreatedAt, r.ClosedAt }).ToListAsync(ct);

        int totalRequestsSubmitted = requests.Count;
        int totalRequestsClosed = requests.Count(r => r.Status == RequestState.Closed);
        double requestResolutionRatePercent = totalRequestsSubmitted > 0 ? Math.Round((double)totalRequestsClosed / totalRequestsSubmitted * 100.0, 1) : 0.0;

        var closedWithDates = requests.Where(r => r.Status == RequestState.Closed && r.ClosedAt.HasValue).ToList();
        double? avgRequestResolutionHours = closedWithDates.Count > 0
            ? Math.Round(closedWithDates.Average(r => (r.ClosedAt!.Value - r.CreatedAt).TotalHours), 1)
            : null;

        var departments = await db.Departments.AsNoTracking().Where(d => d.TenantId == tenantId).ToListAsync(ct);
        var users = await db.Users.AsNoTracking().Where(u => u.TenantId == tenantId).Select(u => new { u.Id, u.DepartmentId }).ToListAsync(ct);
        var userDeptMap = users.Where(u => u.DepartmentId.HasValue).ToDictionary(u => u.Id, u => u.DepartmentId!.Value);

        var deptMetrics = departments.Select(d =>
        {
            var deptTaskIds = activeAssignments.Where(a => a.DepartmentId == d.Id).Select(a => a.TaskId).ToHashSet();
            return new CompanyDepartmentMetricView(
                d.Id,
                d.Name,
                tasks.Count(t => deptTaskIds.Contains(t.Id)),
                tasks.Count(t => deptTaskIds.Contains(t.Id) && t.Status == TaskState.Completed),
                requests.Count(r => userDeptMap.TryGetValue(r.RequesterId, out var uDept) && uDept == d.Id),
                requests.Count(r => userDeptMap.TryGetValue(r.RequesterId, out var uDept) && uDept == d.Id && r.Status == RequestState.Closed)
            );
        }).OrderBy(d => d.DepartmentName).ToList();

        var auditCountQuery = db.AuditLogs.AsNoTracking().Where(a => a.TenantId == tenantId);
        if (from.HasValue) auditCountQuery = auditCountQuery.Where(a => a.CreatedAt >= from.Value);
        if (to.HasValue) auditCountQuery = auditCountQuery.Where(a => a.CreatedAt <= to.Value);
        int auditCount = await auditCountQuery.CountAsync(ct);

        return new CompanyReportResult(
            totalTasksCreated,
            totalTasksCompleted,
            taskCompletionRatePercent,
            avgTaskCompletionHours,
            totalRequestsSubmitted,
            totalRequestsClosed,
            requestResolutionRatePercent,
            avgRequestResolutionHours,
            deptMetrics,
            auditCount,
            now);
    }

    public async Task<WorkloadReportResult> GetWorkloadReportAsync(
        Guid tenantId,
        Guid? departmentId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var tenantTaskIdsQuery = db.WorkTasks.AsNoTracking()
            .Where(t => t.TenantId == tenantId && t.DeletedAt == null)
            .Select(t => t.Id);

        var activeAssignments = await db.TaskAssignments.AsNoTracking()
            .Where(a => a.EndedAt == null && tenantTaskIdsQuery.Contains(a.TaskId))
            .ToListAsync(ct);

        var usersQuery = db.Users.AsNoTracking().Where(u => u.TenantId == tenantId && u.Status == UserStatus.Active);
        if (departmentId.HasValue) usersQuery = usersQuery.Where(u => u.DepartmentId == departmentId.Value);
        var users = await usersQuery.ToListAsync(ct);

        var tasksQuery = db.WorkTasks.AsNoTracking().Where(t => t.TenantId == tenantId && t.DeletedAt == null);
        if (departmentId.HasValue)
        {
            var deptTaskIds = activeAssignments.Where(a => a.DepartmentId == departmentId.Value).Select(a => a.TaskId).ToHashSet();
            tasksQuery = tasksQuery.Where(t => deptTaskIds.Contains(t.Id));
        }
        if (from.HasValue) tasksQuery = tasksQuery.Where(t => t.CreatedAt >= from.Value);
        if (to.HasValue) tasksQuery = tasksQuery.Where(t => t.CreatedAt <= to.Value);
        var tasks = await tasksQuery.Select(t => new { t.Id, t.Status, t.Deadline }).ToListAsync(ct);

        var taskUserMap = activeAssignments.Where(a => a.UserId != null).ToDictionary(a => a.TaskId, a => a.UserId!.Value);

        var departments = await db.Departments.AsNoTracking().Where(d => d.TenantId == tenantId).ToDictionaryAsync(d => d.Id, d => d.Name, ct);

        var items = users.Select(u =>
        {
            var userTasks = tasks.Where(t => taskUserMap.TryGetValue(t.Id, out var uid) && uid == u.Id).ToList();
            int active = userTasks.Count(t => t.Status is TaskState.Assigned or TaskState.Accepted or TaskState.InProgress or TaskState.Submitted or TaskState.Confirmed);
            int completed = userTasks.Count(t => t.Status == TaskState.Completed);
            int overdue = userTasks.Count(t => t.Status == TaskState.Overdue || (t.Status != TaskState.Completed && t.Status != TaskState.Cancelled && t.Deadline.HasValue && t.Deadline.Value < now));
            departments.TryGetValue(u.DepartmentId ?? Guid.Empty, out var deptName);

            return new UserWorkloadItemView(
                u.Id,
                u.FullName,
                u.EmployeeCode,
                u.DepartmentId,
                deptName,
                active,
                completed,
                overdue,
                userTasks.Count(t => t.Status != TaskState.Cancelled));
        }).OrderByDescending(i => i.ActiveTasks).ThenBy(i => i.FullName).ToList();

        var deptBreakdown = departments.Select(kv =>
        {
            var deptId = kv.Key;
            var deptName = kv.Value;
            var deptUsers = users.Where(u => u.DepartmentId == deptId).ToList();
            var deptTaskIds = activeAssignments.Where(a => a.DepartmentId == deptId).Select(a => a.TaskId).ToHashSet();
            var deptTasks = tasks.Where(t => deptTaskIds.Contains(t.Id)).ToList();
            int active = deptTasks.Count(t => t.Status is TaskState.Assigned or TaskState.Accepted or TaskState.InProgress or TaskState.Submitted or TaskState.Confirmed);
            int members = deptUsers.Count;
            double avgPerMember = members > 0 ? Math.Round((double)active / members, 1) : 0.0;
            return new DepartmentWorkloadItemView(deptId, deptName, active, members, avgPerMember);
        }).OrderByDescending(d => d.ActiveTasks).ToList();

        return new WorkloadReportResult(items, deptBreakdown, now);
    }

    public async Task<SlaPerformanceReportResult> GetSlaPerformanceReportAsync(
        Guid tenantId,
        Guid? serviceId,
        DateTimeOffset? from,
        DateTimeOffset? to,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var requestsQuery = db.Requests.AsNoTracking().Where(r => r.TenantId == tenantId && r.DeletedAt == null);
        if (serviceId.HasValue) requestsQuery = requestsQuery.Where(r => r.ServiceId == serviceId.Value);
        if (from.HasValue) requestsQuery = requestsQuery.Where(r => r.CreatedAt >= from.Value);
        if (to.HasValue) requestsQuery = requestsQuery.Where(r => r.CreatedAt <= to.Value);
        var requests = await requestsQuery.Select(r => new { r.Id, r.ServiceId, r.Status, r.CreatedAt, r.ClosedAt }).ToListAsync(ct);

        var services = await db.Services.AsNoTracking().Where(s => s.TenantId == tenantId).ToListAsync(ct);

        int totalTracked = requests.Count;
        int breachedCount = requests.Count(r => r.Status == RequestState.Rejected || (r.Status != RequestState.Closed && (now - r.CreatedAt).TotalHours > 48));
        int compliantCount = totalTracked - breachedCount;
        double complianceRatePercent = totalTracked > 0 ? Math.Round((double)compliantCount / totalTracked * 100.0, 1) : 100.0;

        var closed = requests.Where(r => r.ClosedAt.HasValue).ToList();
        double? avgElapsedHours = closed.Count > 0 ? Math.Round(closed.Average(r => (r.ClosedAt!.Value - r.CreatedAt).TotalHours), 1) : null;

        var serviceMap = services.ToDictionary(s => s.Id, s => s.Name);
        var serviceBreakdown = requests.GroupBy(r => r.ServiceId).Select(g =>
        {
            var sId = g.Key;
            serviceMap.TryGetValue(sId, out var sName);
            int count = g.Count();
            int breached = g.Count(r => r.Status == RequestState.Rejected || (r.Status != RequestState.Closed && (now - r.CreatedAt).TotalHours > 48));
            int compliant = count - breached;
            double rate = count > 0 ? Math.Round((double)compliant / count * 100.0, 1) : 100.0;
            return new ServiceSlaMetricView(sId, sName ?? "Standard Service", count, compliant, breached, rate);
        }).OrderByDescending(s => s.TotalRequests).ToList();

        return new SlaPerformanceReportResult(
            totalTracked,
            compliantCount,
            breachedCount,
            complianceRatePercent,
            avgElapsedHours,
            serviceBreakdown,
            now);
    }
}
