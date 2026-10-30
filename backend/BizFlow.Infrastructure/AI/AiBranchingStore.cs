using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Services;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizFlow.Infrastructure.AI;

public sealed class AiBranchingStore(BizFlowDbContext dbContext) : IAiBranchingStore
{
    public async Task<IReadOnlyList<CreatedBranchRecord>> PersistBranchingPlanAsync(
        Guid tenantId,
        Guid userId,
        Guid planId,
        string sourceType,
        Guid? sourceId,
        IReadOnlyList<BranchPlanNode> branches,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var createdRecords = new List<CreatedBranchRecord>();

        var isInMemory = dbContext.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;
        IDbContextTransaction? tx = isInMemory ? null : await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            var defaultService = await dbContext.Services
                .FirstOrDefaultAsync(s => s.TenantId == tenantId && s.Status == ServiceStatus.Active, cancellationToken);
            var defaultCategory = defaultService != null
                ? await dbContext.ServiceCategories
                    .FirstOrDefaultAsync(c => c.ServiceId == defaultService.Id && c.Status == ServiceCategoryStatus.Active, cancellationToken)
                : null;

            foreach (var node in branches)
            {
                var isRequest = string.Equals(node.BranchType, "Request", StringComparison.OrdinalIgnoreCase);

                if (isRequest)
                {
                    var srvId = node.ServiceId ?? defaultService?.Id;
                    var catId = node.CategoryId ?? defaultCategory?.Id;

                    if (srvId == null || catId == null)
                    {
                        throw new ApplicationFault(FaultKind.Validation, "SERVICE.NOT_FOUND", "No active service and category available in workspace for branch request.");
                    }

                    var priority = Enum.TryParse<RequestPriority>(node.Priority, true, out var rp) ? rp : RequestPriority.Medium;

                    Guid? parentReqId = string.Equals(sourceType, "Request", StringComparison.OrdinalIgnoreCase)
                        ? sourceId
                        : null;

                    var req = WorkRequest.CreateDraft(
                        tenantId: tenantId,
                        requesterId: userId,
                        serviceId: srvId.Value,
                        categoryId: catId.Value,
                        title: node.Title.Trim(),
                        description: $"{node.Description}\n\n[Giai đoạn {node.ExecutionPhase} - Lý do: {node.Reasoning}]",
                        now: now,
                        priority: priority,
                        parentRequestId: parentReqId);

                    dbContext.Requests.Add(req);
                    var reqAudit = AuditLog.RequestCreated(req, now);
                    dbContext.AuditLogs.Add(reqAudit);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    createdRecords.Add(new CreatedBranchRecord(
                        BranchIndex: node.BranchIndex,
                        RecordType: "Request",
                        RecordId: req.Id,
                        Title: req.Title,
                        Status: req.Status.ToString(),
                        DepartmentId: node.DepartmentId,
                        DepartmentName: node.DepartmentName,
                        ServiceId: srvId,
                        CategoryId: catId));
                }
                else
                {
                    var taskPriority = Enum.TryParse<TaskPriority>(node.Priority, true, out var tp) ? tp : TaskPriority.Medium;
                    var deadline = now.AddDays(Math.Max(1, node.SuggestedDurationDays));

                    Guid? linkedReqId = string.Equals(sourceType, "Request", StringComparison.OrdinalIgnoreCase)
                        ? sourceId
                        : null;

                    var taskDesc = node.Description;
                    if (node.Checklist.Count > 0)
                    {
                        taskDesc += "\n\nDanh mục kiểm tra (Checklist):\n" + string.Join("\n", node.Checklist.Select(c => $"- [ ] {c}"));
                    }

                    var task = WorkTask.CreateDraft(
                        tenantId: tenantId,
                        creatorId: userId,
                        title: node.Title.Trim(),
                        now: now,
                        description: taskDesc,
                        priority: taskPriority,
                        deadline: deadline,
                        requestId: linkedReqId);

                    dbContext.WorkTasks.Add(task);
                    var taskAudit = AuditLog.TaskCreated(task, Array.Empty<TaskChecklistItem>(), now);
                    dbContext.AuditLogs.Add(taskAudit);
                    await dbContext.SaveChangesAsync(cancellationToken);

                    createdRecords.Add(new CreatedBranchRecord(
                        BranchIndex: node.BranchIndex,
                        RecordType: "Task",
                        RecordId: task.Id,
                        Title: task.Title,
                        Status: task.Status.ToString(),
                        DepartmentId: node.DepartmentId,
                        DepartmentName: node.DepartmentName,
                        ServiceId: null,
                        CategoryId: null));
                }
            }

            if (tx != null)
            {
                await tx.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (tx != null)
            {
                await tx.RollbackAsync(cancellationToken);
            }
            throw;
        }
        finally
        {
            tx?.Dispose();
        }

        return createdRecords;
    }
}
