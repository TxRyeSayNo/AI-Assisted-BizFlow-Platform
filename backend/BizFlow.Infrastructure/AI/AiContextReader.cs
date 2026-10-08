using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.AI;

public sealed class AiContextReader(BizFlowDbContext dbContext) : IAiContextReader
{
    public async Task<AiTenantContextData> GetTenantContextAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var services = await dbContext.Services
            .AsNoTracking()
            .Where(s => s.TenantId == tenantId)
            .Select(s => new AiServiceInfo(
                s.Id,
                s.Code,
                s.Name,
                s.Description ?? "",
                dbContext.ServiceCategories
                    .AsNoTracking()
                    .Where(c => c.ServiceId == s.Id)
                    .Select(c => new AiCategoryInfo(c.Id, c.Code, c.Name))
                    .ToList()))
            .ToListAsync(cancellationToken);

        var departments = await dbContext.Departments
            .AsNoTracking()
            .Where(d => d.TenantId == tenantId)
            .Select(d => new AiDepartmentInfo(d.Id, d.Code, d.Name))
            .ToListAsync(cancellationToken);

        // Compute active task counts per user
        var activeTaskCounts = await (
            from a in dbContext.TaskAssignments.AsNoTracking()
            join t in dbContext.WorkTasks.AsNoTracking() on a.TaskId equals t.Id
            where t.TenantId == tenantId && t.DeletedAt == null && a.EndedAt == null && a.UserId != null &&
                  t.Status != TaskState.Completed && t.Status != TaskState.Cancelled
            group a by a.UserId!.Value into g
            select new { UserId = g.Key, Count = g.Count() }
        ).ToDictionaryAsync(x => x.UserId, x => x.Count, cancellationToken);

        var users = await dbContext.Users
            .AsNoTracking()
            .Where(u => u.TenantId == tenantId && u.DeletedAt == null && u.Status == UserStatus.Active)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.Email,
                u.DepartmentId
            })
            .ToListAsync(cancellationToken);

        var userList = users.Select(u => new AiUserInfo(
            u.Id,
            u.FullName,
            u.Email,
            u.DepartmentId,
            "EMPLOYEE",
            activeTaskCounts.GetValueOrDefault(u.Id, 0)
        )).ToList();

        var workflows = await (
            from w in dbContext.Workflows.AsNoTracking()
            join v in dbContext.WorkflowVersions.AsNoTracking() on w.Id equals v.WorkflowId
            where w.TenantId == tenantId
            select new AiWorkflowInfo(v.Id, w.Id, w.Name, v.VersionNo)
        ).ToListAsync(cancellationToken);

        var slas = await (
            from p in dbContext.SlaProfiles.AsNoTracking()
            join v in dbContext.SlaVersions.AsNoTracking() on p.Id equals v.SlaProfileId
            where p.TenantId == tenantId
            select new AiSlaInfo(v.Id, p.Id, p.Name, 480)
        ).ToListAsync(cancellationToken);

        return new AiTenantContextData(tenantId, services, departments, userList, workflows, slas);
    }

    public async Task<AiTaskContextData?> GetTaskContextAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default)
    {
        var task = await dbContext.WorkTasks
            .AsNoTracking()
            .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == taskId && t.DeletedAt == null, cancellationToken);

        if (task is null) return null;

        var activeAssignment = await (
            from a in dbContext.TaskAssignments.AsNoTracking()
            join u in dbContext.Users.AsNoTracking() on a.UserId equals u.Id into ug
            from u in ug.DefaultIfEmpty()
            where a.TaskId == taskId && a.EndedAt == null
            select new { a.DepartmentId, a.UserId, FullName = u != null ? u.FullName : null }
        ).FirstOrDefaultAsync(cancellationToken);

        var checklistItems = await dbContext.TaskChecklistItems
            .AsNoTracking()
            .Where(c => c.TaskId == taskId)
            .ToListAsync(cancellationToken);

        var progressReports = await dbContext.TaskProgressReports
            .AsNoTracking()
            .Where(p => p.TaskId == taskId)
            .OrderBy(p => p.SubmittedAt)
            .ToListAsync(cancellationToken);

        var comments = await dbContext.Comments
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ObjectId == taskId && c.ObjectType == CommentObjectType.Task && c.DeletedAt == null)
            .OrderBy(c => c.CreatedAt)
            .Select(c => c.Content)
            .ToListAsync(cancellationToken);

        var lastProgress = progressReports.LastOrDefault();

        return new AiTaskContextData(
            TaskId: task.Id,
            TenantId: task.TenantId,
            Title: task.Title,
            Description: task.Description ?? "",
            Status: task.Status.ToString(),
            Priority: task.Priority.ToString(),
            DepartmentId: activeAssignment?.DepartmentId,
            AssigneeId: activeAssignment?.UserId,
            AssigneeName: activeAssignment?.FullName,
            CreatedAt: task.CreatedAt,
            Deadline: task.Deadline,
            ChecklistItemCount: checklistItems.Count,
            CompletedChecklistItemCount: checklistItems.Count(c => c.IsCompleted),
            ProgressReportCount: progressReports.Count,
            LastReportedPercent: lastProgress?.Percent ?? 0,
            ProgressNotes: progressReports.Select(p => p.Content ?? "").ToList(),
            Comments: comments);
    }

    public async Task<AiRequestContextData?> GetRequestContextAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken = default)
    {
        var request = await dbContext.Requests
            .AsNoTracking()
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == requestId && r.DeletedAt == null, cancellationToken);

        if (request is null) return null;

        var service = await dbContext.Services.AsNoTracking().FirstOrDefaultAsync(s => s.Id == request.ServiceId, cancellationToken);
        var category = await dbContext.ServiceCategories.AsNoTracking().FirstOrDefaultAsync(c => c.Id == request.CategoryId, cancellationToken);

        var comments = await dbContext.Comments
            .AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.ObjectId == requestId && c.ObjectType == CommentObjectType.Request && c.DeletedAt == null)
            .OrderBy(c => c.CreatedAt)
            .Select(c => c.Content)
            .ToListAsync(cancellationToken);

        return new AiRequestContextData(
            RequestId: request.Id,
            TenantId: request.TenantId,
            Title: request.Title,
            Description: request.Description,
            Status: request.Status.ToString(),
            Priority: request.Priority.ToString(),
            ServiceId: request.ServiceId,
            ServiceName: service?.Name ?? "Dịch vụ chung",
            CategoryId: request.CategoryId,
            CategoryName: category?.Name ?? "Danh mục chung",
            CreatedAt: request.CreatedAt,
            Comments: comments);
    }
}
