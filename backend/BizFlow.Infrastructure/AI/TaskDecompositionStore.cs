using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Common;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Services;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.AI;

public sealed class TaskDecompositionStore(
    BizFlowDbContext dbContext,
    ITenantContext context) : ITaskDecompositionStore
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

        using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            foreach (var item in items)
            {
                var isSupportRequest = string.Equals(item.Type, "SupportRequest", StringComparison.OrdinalIgnoreCase);

                if (isSupportRequest)
                {
                    var service = await dbContext.Services
                        .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Status == ServiceStatus.Active, cancellationToken);
                    var category = service != null
                        ? await dbContext.ServiceCategories
                            .FirstOrDefaultAsync(c => c.ServiceId == service.Id && c.Status == ServiceCategoryStatus.Active, cancellationToken)
                        : null;

                    if (service == null || category == null)
                    {
                        throw new ApplicationFault(
                            FaultKind.Validation,
                            "AI.NO_SERVICE_AVAILABLE",
                            "Không tìm thấy dịch vụ hoặc danh mục hợp lệ trong hệ thống để tạo Yêu cầu hỗ trợ.");
                    }

                    var reqPriority = Enum.TryParse<RequestPriority>(item.Priority, true, out var rp)
                        ? rp
                        : RequestPriority.Medium;

                    var description = string.IsNullOrWhiteSpace(item.Description) ? item.Title : item.Description;
                    var request = WorkRequest.CreateDraft(
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
                        item.DepartmentId));
                }
                else
                {
                    var taskPriority = Enum.TryParse<TaskPriority>(item.Priority, true, out var tp)
                        ? tp
                        : TaskPriority.Medium;

                    var task = WorkTask.CreateDraft(
                        tenantId,
                        actorId,
                        item.Title,
                        now,
                        item.Description,
                        taskPriority,
                        null,
                        null);

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

                    var audit = AuditLog.TaskCreated(task, checklist, now);
                    dbContext.AuditLogs.Add(audit);

                    if (item.DepartmentId.HasValue)
                    {
                        var deptExists = await dbContext.Departments
                            .AnyAsync(d => d.TenantId == tenantId && d.Id == item.DepartmentId.Value && d.Status == RecordStatus.Active, cancellationToken);

                        if (deptExists)
                        {
                            var assignment = TaskAssignment.Create(task.Id, actorId, item.DepartmentId.Value, null, now);
                            dbContext.TaskAssignments.Add(assignment);
                        }
                    }

                    created.Add(new CreatedDecompositionItemDto(
                        task.Id,
                        "Assignment",
                        task.Title,
                        task.Status.ToString(),
                        item.DepartmentId));
                }
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
