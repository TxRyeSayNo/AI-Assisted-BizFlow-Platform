using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Collaboration;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace BizFlow.Infrastructure.AI;

public sealed class AiDocumentSummarizerStore(
    BizFlowDbContext dbContext,
    IObjectStorageProvider storageProvider) : IAiDocumentSummarizerStore
{
    public async Task<(string FileName, string? Content)?> GetAttachmentTextAsync(
        Guid tenantId,
        Guid attachmentId,
        CancellationToken cancellationToken)
    {
        var att = await dbContext.Attachments
            .FirstOrDefaultAsync(a => a.TenantId == tenantId && a.Id == attachmentId && a.DeletedAt == null, cancellationToken);

        if (att == null) return null;

        var stream = await storageProvider.OpenReadAsync(att.ObjectKey, cancellationToken);
        if (stream == null) return (att.FileName, null);

        using var reader = new StreamReader(stream);
        var buffer = new char[64_000];
        var read = await reader.ReadBlockAsync(buffer, 0, buffer.Length);
        var text = new string(buffer, 0, read);

        return (att.FileName, text);
    }

    public async Task<IReadOnlyList<CreatedSummaryTaskResult>> PersistSummaryTasksAsync(
        Guid tenantId,
        Guid userId,
        string sourceTitle,
        IReadOnlyList<ExtractedActionItem> items,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var departments = await dbContext.Departments
            .Where(d => d.TenantId == tenantId && d.Status == RecordStatus.Active)
            .ToListAsync(cancellationToken);

        var createdTasks = new List<CreatedSummaryTaskResult>();

        var isInMemory = dbContext.Database.ProviderName?.Contains("InMemory", StringComparison.OrdinalIgnoreCase) == true;
        IDbContextTransaction? tx = isInMemory ? null : await dbContext.Database.BeginTransactionAsync(cancellationToken);

        try
        {
            foreach (var item in items)
            {
                var priority = Enum.TryParse<TaskPriority>(item.SuggestedPriority, true, out var tp) ? tp : TaskPriority.Medium;
                var deadline = now.AddDays(Math.Max(1, item.SuggestedDurationDays ?? 3));

                var matchedDept = !string.IsNullOrWhiteSpace(item.SuggestedDepartment)
                    ? departments.FirstOrDefault(d => d.Name.Contains(item.SuggestedDepartment, StringComparison.OrdinalIgnoreCase))
                    : null;

                var desc = $"{item.Description}\n\n[Nguồn tóm tắt AI từ tài liệu: {sourceTitle}]";

                var task = WorkTask.CreateDraft(
                    tenantId: tenantId,
                    creatorId: userId,
                    title: item.Title.Trim(),
                    now: now,
                    description: desc,
                    priority: priority,
                    deadline: deadline);

                dbContext.WorkTasks.Add(task);
                var taskAudit = AuditLog.TaskCreated(task, Array.Empty<TaskChecklistItem>(), now);
                dbContext.AuditLogs.Add(taskAudit);
                await dbContext.SaveChangesAsync(cancellationToken);

                createdTasks.Add(new CreatedSummaryTaskResult(
                    TaskId: task.Id,
                    Title: task.Title,
                    Priority: task.Priority.ToString(),
                    DepartmentId: matchedDept?.Id,
                    Status: task.Status.ToString()));
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

        return createdTasks;
    }
}
