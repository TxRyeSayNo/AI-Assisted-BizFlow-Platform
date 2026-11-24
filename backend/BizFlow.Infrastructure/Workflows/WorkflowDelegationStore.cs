using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Workflows;
using BizFlow.Application.Requests;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Workflows;
using BizFlow.Infrastructure.Requests;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Workflows;

public sealed partial class WorkflowRuntimeStore
{
    private async Task<(Guid Principal, WorkflowDelegation? Delegation)?> Representation(WorkflowStepExecution step, JsonElement node,
        WorkRequest request, Guid user, DateTimeOffset now, CancellationToken ct)
    {
        var active = await db.WorkflowDelegations.AsNoTracking().Where(d => d.StepExecutionId == step.Id && d.RevokedAt == null && d.CreatedAt <= now && d.ExpiresAt > now).ToListAsync(ct);
        var delegated = active.SingleOrDefault(d => d.DelegateId == user);
        if (delegated is not null && !(step.NodeType == "APPROVAL" && request.RequesterId == user)) return (delegated.PrincipalId, delegated);
        if (step.Candidates().Contains(user) && !active.Any(d => d.PrincipalId == user) && (await Eligible(node, request, ct)).Contains(user)) return (user, null);
        return null;
    }

    public async Task<WorkflowRuntimeView> DelegateAsync(Guid requestId, Guid stepId, WorkflowDelegationInput input, CancellationToken ct)
    {
        var (tenant, user) = await Identity(ct);
        await authorizer.AuthorizeAsync("workflows.configure", new(tenant), cancellationToken: ct);
        var now = RequestCreationRules.DatabaseTime(clock.GetUtcNow());
        if (string.IsNullOrWhiteSpace(input.Reason) || input.Reason.Trim().Length > 1000 ||
            input.Reason.Any(char.IsControl) || input.ExpiresAt <= now || input.ExpiresAt > now.AddDays(90)) throw Invalid("Cần lý do hợp lệ và thời hạn ủy quyền trong 90 ngày.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var request = await Lock(requestId, tenant, ct);
        var instance = await db.WorkflowInstances.SingleOrDefaultAsync(i => i.RequestId == requestId, ct) ?? throw ApplicationFault.NotFound();
        var step = await db.WorkflowExecutions.SingleOrDefaultAsync(s => s.Id == stepId && s.InstanceId == instance.Id, ct) ?? throw ApplicationFault.NotFound();
        if (instance.Status != "RUNNING" || instance.Revision != input.Revision || step.Status != "PENDING") throw Conflict();
        if (step.NodeType is not ("APPROVAL" or "DEPARTMENT") || !step.Candidates().Contains(input.PrincipalId) ||
            step.Candidates().Contains(input.DelegateId) || step.NodeType == "APPROVAL" && input.DelegateId == request.RequesterId)
            throw Invalid("Không thể ủy quyền cho người đề nghị, người trong danh sách gốc hoặc bước xác nhận.");
        if (!await db.Users.AnyAsync(u => u.Id == input.DelegateId && u.Status == UserStatus.Active && u.DeletedAt == null, ct)) throw ApplicationFault.NotFound();
        if (await db.WorkflowDecisions.AnyAsync(d => d.StepExecutionId == stepId && (d.PrincipalId == input.PrincipalId || d.ActorId == input.DelegateId), ct)) throw Conflict();
        // Revoke expired grants too: filtered unique indexes retain only one unrevoked grant for each slot/person.
        var previous = await db.WorkflowDelegations.Where(d => d.StepExecutionId == stepId && d.RevokedAt == null &&
            (d.PrincipalId == input.PrincipalId || d.DelegateId == input.DelegateId)).ToListAsync(ct);
        if (previous.Any(d => d.PrincipalId != input.PrincipalId && d.IsActive(now))) throw Invalid("Người nhận đang đại diện một lượt duyệt khác của bước này.");
        foreach (var grant in previous) grant.Revoke(user, now);
        await db.SaveChangesAsync(ct);
        var delegation = WorkflowDelegation.Create(step, input.PrincipalId, input.DelegateId, user, input.Reason, now, input.ExpiresAt);
        db.WorkflowDelegations.Add(delegation);
        await db.SaveChangesAsync(ct);
        if (!await db.WorkflowParticipants.AnyAsync(p => p.StepExecutionId == stepId && p.UserId == input.DelegateId, ct))
            db.WorkflowParticipants.Add(WorkflowParticipant.ForDelegate(step, delegation));
        instance.Advance(input.Revision, now);
        db.AuditLogs.Add(AuditLog.WorkflowRuntimeChanged(tenant, user, requestId, "WORKFLOW.DELEGATED",
            new { delegation.Id, stepId, input.PrincipalId, input.DelegateId, delegation.ExpiresAt, delegation.Reason, instance.Revision }, now));
        db.Notifications.Add(Notification.Create(tenant, input.DelegateId, NotificationEvent.ApprovalRequired,
            "Được ủy quyền xử lý bước", $"{request.Title}: {step.Name}", $"workflow-delegation/{delegation.Id:N}", "Request", requestId));
        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        await updates.PublishAsync(tenant, new[] { input.PrincipalId, input.DelegateId }, ct);
        return await View(instance, user, ct);
    }

    public async Task<WorkflowRuntimeView> RevokeDelegationAsync(Guid requestId, Guid delegationId, int revision, CancellationToken ct)
    {
        var (tenant, user) = await Identity(ct);
        await authorizer.AuthorizeAsync("workflows.configure", new(tenant), cancellationToken: ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        await Lock(requestId, tenant, ct);
        var instance = await db.WorkflowInstances.SingleOrDefaultAsync(i => i.RequestId == requestId, ct) ?? throw ApplicationFault.NotFound();
        var delegation = await db.WorkflowDelegations.SingleOrDefaultAsync(d => d.Id == delegationId && d.InstanceId == instance.Id, ct) ?? throw ApplicationFault.NotFound();
        if (instance.Status != "RUNNING" || instance.Revision != revision || delegation.RevokedAt is not null ||
            !await db.WorkflowExecutions.AnyAsync(s => s.Id == delegation.StepExecutionId && s.Status == "PENDING", ct)) throw Conflict();
        var now = RequestCreationRules.DatabaseTime(clock.GetUtcNow());
        delegation.Revoke(user, now); instance.Advance(revision, now);
        db.AuditLogs.Add(AuditLog.WorkflowRuntimeChanged(tenant, user, requestId, "WORKFLOW.DELEGATION_REVOKED", new { delegation.Id, instance.Revision }, now));
        await db.SaveChangesAsync(ct); await tx.CommitAsync(ct);
        await updates.PublishAsync(tenant, new[] { delegation.PrincipalId, delegation.DelegateId }, ct);
        return await View(instance, user, ct);
    }
}
