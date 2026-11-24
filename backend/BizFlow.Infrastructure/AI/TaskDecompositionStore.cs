using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Common;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Services;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Workflows;
using BizFlow.Domain.Notifications;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.AI;

public sealed class TaskDecompositionStore(
    BizFlowDbContext dbContext,
    ITenantContext context,
    IResourceAuthorizer authorizer,
    IAccessSnapshotProvider snapshots,
    IWorkflowEngine workflow) : ITaskDecompositionStore
{
    public async Task<IReadOnlyList<CreatedDecompositionItemDto>> PersistDecompositionBatchAsync(
        Guid tenantId,
        Guid actorId,
        IReadOnlyList<ConfirmTaskDecompositionItem> items,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        if (context.TenantId != tenantId || context.UserId != actorId || tenantId == Guid.Empty || actorId == Guid.Empty)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Task decomposition confirmation requires the current tenant and actor.");

        var created = new List<CreatedDecompositionItemDto>();
        // PostgreSQL stores microseconds; subsequent workflow checks compare this timestamp exactly.
        now = TaskCreationRules.DatabaseTime(now);

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var item in items)
            {
                var isSupportRequest = string.Equals(item.Type, "SupportRequest", StringComparison.OrdinalIgnoreCase);

                // Resolve target department ID by ID or by DepartmentName
                Guid? resolvedDeptId = item.DepartmentId;
                if (!resolvedDeptId.HasValue && !string.IsNullOrWhiteSpace(item.DepartmentName))
                {
                    var cleanName = item.DepartmentName.Trim().ToLowerInvariant();
                    resolvedDeptId = await dbContext.Departments
                        .Where(d => d.TenantId == tenantId && d.Status == RecordStatus.Active && d.Name.ToLower() == cleanName)
                        .Select(d => (Guid?)d.Id).SingleOrDefaultAsync(cancellationToken);
                    if (!resolvedDeptId.HasValue)
                    {
                        var matches = await dbContext.Departments
                            .Where(d => d.TenantId == tenantId && d.Status == RecordStatus.Active)
                            .Where(d => d.Name.ToLower().Contains(cleanName) || cleanName.Contains(d.Name.ToLower()))
                            .Select(d => (Guid?)d.Id)
                            .Take(2).ToListAsync(cancellationToken);
                        if (matches.Count > 1)
                            throw new ApplicationFault(FaultKind.Validation, "AI.DEPARTMENT_AMBIGUOUS", "Có nhiều phòng ban phù hợp. Vui lòng chọn phòng ban cụ thể trước khi tạo nhiệm vụ.");
                        resolvedDeptId = matches.SingleOrDefault();
                    }
                }

                if ((resolvedDeptId.HasValue && !await dbContext.Departments.AnyAsync(d => d.TenantId == tenantId &&
                    d.Id == resolvedDeptId && d.Status == RecordStatus.Active, cancellationToken)) ||
                    (!resolvedDeptId.HasValue && !string.IsNullOrWhiteSpace(item.DepartmentName)))
                    throw new ApplicationFault(FaultKind.Validation, "AI.DEPARTMENT_NOT_FOUND", "Không tìm thấy phòng ban nhận việc đang hoạt động. Vui lòng chọn lại phòng ban.");

                if (isSupportRequest)
                {
                    var service = await dbContext.Services
                        .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Status == ServiceStatus.Active, cancellationToken);
                    var category = service != null
                        ? await dbContext.ServiceCategories
                            .FirstOrDefaultAsync(c => c.ServiceId == service.Id && c.Status == ServiceCategoryStatus.Active, cancellationToken)
                        : null;

                    if (service != null && category != null)
                    {
                        var reqPriority = Enum.TryParse<RequestPriority>(item.Priority, true, out var rp)
                            ? rp
                            : RequestPriority.Medium;

                        var description = string.IsNullOrWhiteSpace(item.Description) ? item.Title : item.Description;
                        var request = WorkRequest.CreateSubmitted(
                            tenantId,
                            actorId,
                            service.Id,
                            category.Id,
                            item.Title,
                            description,
                            now,
                            reqPriority);

                        dbContext.Requests.Add(request);

                        var audit = AuditLog.RequestCreated(request, now);
                        dbContext.AuditLogs.Add(audit);

                        created.Add(new CreatedDecompositionItemDto(
                            request.Id,
                            "SupportRequest",
                            request.Title,
                            request.Status.ToString(),
                            resolvedDeptId));
                        continue;
                    }
                    // A request cannot silently become a task with different permission requirements.
                    throw new ApplicationFault(FaultKind.Validation, "AI.SERVICE_REQUIRED", "Chưa có dịch vụ và danh mục đang hoạt động để tạo yêu cầu hỗ trợ. Hãy chọn loại Giao việc trực tiếp hoặc cấu hình dịch vụ trước.");
                }

                var taskPriority = Enum.TryParse<TaskPriority>(item.Priority, true, out var tp)
                    ? tp
                    : TaskPriority.Medium;

                var deadline = item.SuggestedDurationDays is > 0
                    ? now.AddDays(item.SuggestedDurationDays.Value)
                    : (DateTimeOffset?)null;

                var task = WorkTask.CreateDraft(tenantId, actorId, item.Title, now, item.Description, taskPriority, deadline, null);

                dbContext.WorkTasks.Add(task);

                var checklist = new List<TaskChecklistItem>();
                if (item.ChecklistItems is { Count: > 0 })
                {
                    for (var i = 0; i < item.ChecklistItems.Count; i++)
                    {
                        var cl = TaskChecklistItem.Create(task.Id, item.ChecklistItems[i], i + 1);
                        checklist.Add(cl);
                        dbContext.TaskChecklistItems.Add(cl);
                    }
                }

                var taskAudit = AuditLog.TaskCreated(task, checklist, now);
                dbContext.AuditLogs.Add(taskAudit);

                // Creation, checklist and its DRAFT audit must exist before the authorized workflow
                // transition. Both saves remain inside the same transaction, including batch creation.
                await dbContext.SaveChangesAsync(cancellationToken);
                if (resolvedDeptId.HasValue)
                {
                    await authorizer.AuthorizeAsync("tasks.assign", new(tenantId, actorId), resolvedDeptId.Value, cancellationToken);
                    var access = await snapshots.ResolveAsync(actorId, tenantId, cancellationToken);
                    if (access == null || access.UserId != actorId || access.TenantId != tenantId)
                        throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Bạn không có quyền giao nhiệm vụ này.");
                    var recipients = await dbContext.Users.Where(u => u.TenantId == tenantId && u.DepartmentId == resolvedDeptId &&
                        u.Status == UserStatus.Active && u.DeletedAt == null).Select(u => u.Id).ToListAsync(cancellationToken);
                    if (recipients.Count == 0)
                        throw new ApplicationFault(FaultKind.Validation, "TASK.DEPARTMENT_MANAGER_REQUIRED", "Phòng ban nhận việc chưa có người dùng đang hoạt động. Vui lòng kiểm tra phòng ban trước khi giao nhiệm vụ.");
                    var decision = workflow.AssignTask(task, access, resolvedDeptId.Value, true, now);
                    if (!decision.Allowed)
                        throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Bạn không có quyền giao nhiệm vụ cho phòng ban đã chọn.");
                    var assignment = TaskAssignment.Create(task.Id, actorId, resolvedDeptId.Value, null, now);
                    dbContext.TaskAssignments.Add(assignment);
                    dbContext.AuditLogs.Add(AuditLog.TaskAssigned(task, assignment, "Tạo từ đề xuất phân chia nhiệm vụ AI", now, null, null));
                    dbContext.Notifications.AddRange(recipients.Select(recipient => Notification.Create(tenantId, recipient,
                        NotificationEvent.TaskAssigned, "Nhiệm vụ mới được giao", task.Title,
                        $"task-assigned/{assignment.Id:N}/{recipient:N}", "Task", task.Id)));
                    await dbContext.SaveChangesAsync(cancellationToken);
                }

                created.Add(new CreatedDecompositionItemDto(
                    task.Id,
                    "Assignment",
                    task.Title,
                    task.Status.ToString(),
                    resolvedDeptId));
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);

            return created;
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken);
            throw;
        }
    }
}
