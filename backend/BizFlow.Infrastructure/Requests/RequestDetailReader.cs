using BizFlow.Application.Requests;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Requests;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Requests;

public sealed class RequestDetailReader(BizFlowDbContext db) : IRequestDetailReader
{
    public async Task<RequestDetailView?> GetAsync(RequestReadScope scope, Guid id, CancellationToken cancellationToken)
    {
        var request = await RequestReadQueries.Visible(db, scope).SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
        if (request is null) return null;

        var serviceName = await db.Services
            .Where(s => s.TenantId == scope.TenantId && s.Id == request.ServiceId)
            .Select(s => s.Name)
            .SingleOrDefaultAsync(cancellationToken);

        var categoryName = await db.ServiceCategories
            .Where(c => c.Id == request.CategoryId)
            .Select(c => c.Name)
            .SingleOrDefaultAsync(cancellationToken);

        var requesterName = await db.Users
            .Where(u => u.TenantId == scope.TenantId && u.Id == request.RequesterId)
            .Select(u => u.FullName)
            .SingleOrDefaultAsync(cancellationToken);

        var routing = await db.RequestRoutings
            .Where(r => r.TenantId == scope.TenantId && r.RequestId == request.Id)
            .OrderByDescending(r => r.RoutedAt)
            .FirstOrDefaultAsync(cancellationToken);

        string? routingDeptName = null;
        if (routing?.ToDepartmentId is { } deptId)
        {
            routingDeptName = await db.Departments
                .Where(d => d.TenantId == scope.TenantId && d.Id == deptId)
                .Select(d => d.Name)
                .SingleOrDefaultAsync(cancellationToken);
        }

        string? routingUserName = null;
        if (routing?.ToUserId is { } targetUserId)
        {
            routingUserName = await db.Users
                .Where(u => u.TenantId == scope.TenantId && u.Id == targetUserId)
                .Select(u => u.FullName)
                .SingleOrDefaultAsync(cancellationToken);
        }

        var receiptConfirmation = await db.Confirmations
            .Where(c => c.TenantId == scope.TenantId && c.ObjectType == "REQUEST" && c.ObjectId == request.Id && c.MilestoneType == "RECEIVE")
            .OrderByDescending(c => c.ConfirmedAt)
            .FirstOrDefaultAsync(cancellationToken);

        string? rejectionReason = null;
        if (request.Status == RequestState.Rejected)
        {
            var rejectionAudit = await db.AuditLogs
                .Where(a => a.TenantId == scope.TenantId && a.ObjectId == request.Id && a.Action == "REQUEST.REJECTED")
                .OrderByDescending(a => a.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
            if (rejectionAudit?.AfterJson is not null)
            {
                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(rejectionAudit.AfterJson);
                    if (doc.RootElement.TryGetProperty("reason", out var rp))
                    {
                        rejectionReason = rp.GetString();
                    }
                }
                catch { }
            }
        }

        var resolutions = await (
            from r in db.RequestResolutions
            where r.RequestId == request.Id
            join u in db.Users on r.ResolverId equals u.Id into users
            from u in users.DefaultIfEmpty()
            orderby r.RevisionNo ascending
            select new RequestResolutionItemView(
                r.Id,
                r.ResolverId,
                u != null ? u.FullName : null,
                r.Content,
                r.RevisionNo,
                r.CreatedAt)
        ).ToListAsync(cancellationToken);

        var confirmations = await (
            from c in db.Confirmations
            where c.TenantId == scope.TenantId && c.ObjectType == "REQUEST" && c.ObjectId == request.Id
            join u in db.Users on c.ActorId equals u.Id into users
            from u in users.DefaultIfEmpty()
            orderby c.ConfirmedAt ascending
            select new RequestConfirmationItemView(
                c.Id,
                c.ActorId,
                u != null ? u.FullName : null,
                c.MilestoneType,
                c.Decision,
                c.Note,
                c.ConfirmedAt)
        ).ToListAsync(cancellationToken);

        var linkedTasks = await (
            from t in db.WorkTasks
            where t.TenantId == scope.TenantId && t.RequestId == request.Id && t.DeletedAt == null
            orderby t.CreatedAt descending
            select new
            {
                t.Id,
                t.Title,
                t.Status,
                t.Priority,
                t.Deadline,
                t.CreatedAt,
                Assignment = db.TaskAssignments.Where(a => a.TaskId == t.Id && a.EndedAt == null)
                    .Select(a => new
                    {
                        a.UserId,
                        a.DepartmentId,
                        UserName = db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == a.UserId).Select(u => u.FullName).FirstOrDefault(),
                        DepartmentName = db.Departments.Where(d => d.TenantId == scope.TenantId && d.Id == a.DepartmentId).Select(d => d.Name).FirstOrDefault()
                    }).FirstOrDefault()
            }
        ).ToListAsync(cancellationToken);

        var taskViews = linkedTasks.Select(t => new RequestLinkedTaskItemView(
            t.Id,
            t.Title,
            TaskListCodes.State(t.Status),
            TaskListCodes.Priority(t.Priority),
            t.Deadline,
            t.CreatedAt,
            t.Assignment?.UserId,
            t.Assignment?.UserName,
            t.Assignment?.DepartmentId,
            t.Assignment?.DepartmentName)).ToList();

        return new(
            request.Id,
            request.Title,
            request.Description,
            RequestListCodes.State(request.Status),
            RequestListCodes.Priority(request.Priority),
            request.ServiceId,
            serviceName,
            request.CategoryId,
            categoryName,
            request.RequesterId,
            requesterName,
            request.ParentRequestId,
            request.RevisedFromRequestId,
            request.WorkflowVersionId,
            request.SlaVersionId,
            request.ResolvedAt,
            request.ClosedAt,
            request.CreatedAt,
            request.UpdatedAt,
            routing?.ToDepartmentId,
            routingDeptName,
            routing?.ToUserId,
            routingUserName,
            routing?.RoutedAt,
            receiptConfirmation?.ConfirmedAt,
            rejectionReason,
            resolutions,
            confirmations,
            taskViews);
    }
}
