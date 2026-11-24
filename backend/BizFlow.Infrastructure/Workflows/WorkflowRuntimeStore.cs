using System.Text.Json;
using System.Text.Json.Nodes;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Requests;
using BizFlow.Application.Workflows;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Workflows;
using BizFlow.Infrastructure.Persistence;
using BizFlow.Infrastructure.Requests;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Workflows;

/// <summary>Serializes each request on its database row. Version, participants and evidence survive restarts.</summary>
public sealed partial class WorkflowRuntimeStore(BizFlowDbContext db, ITenantContext context,
    IAccessSnapshotProvider snapshots, IResourceAuthorizer authorizer, IWorkflowRuntimeEngine engine, TimeProvider clock,
    BizFlow.Application.Notifications.INotificationUpdates updates) : IWorkflowRuntimeStore
{
    private async Task<(Guid Tenant, Guid User)> Identity(CancellationToken ct)
    {
        if (context.TenantId is not { } tenant || context.UserId is not { } user ||
            await snapshots.ResolveAsync(user, tenant, ct) is not { IsUserActive: true, IsTenantActive: true }) throw ApplicationFault.NotFound();
        return (tenant, user);
    }
    private static ApplicationFault Conflict() => new(FaultKind.Conflict, "WORKFLOW.STALE", "Quy trình đã thay đổi hoặc bước đã được xử lý. Vui lòng tải lại.");
    private static ApplicationFault Invalid(string message) => new(FaultKind.Validation, "WORKFLOW.INVALID", message);
    private async Task<WorkRequest> Lock(Guid requestId, Guid tenant, CancellationToken ct) =>
        await db.Requests.FromSqlInterpolated($"SELECT *, xmin FROM \"Request\" WHERE \"RequestId\"={requestId} AND \"TenantId\"={tenant} AND \"DeletedAt\" IS NULL FOR UPDATE")
            .SingleOrDefaultAsync(ct) ?? throw ApplicationFault.NotFound();

    public async Task<WorkflowRuntimeView?> GetAsync(Guid requestId, CancellationToken ct)
    {
        var (tenant, user) = await Identity(ct);
        var access = await snapshots.ResolveAsync(user, tenant, ct);
        var (_, scope) = RequestReadPolicy.Resolve(access!);
        if (scope is null || !await RequestReadQueries.Visible(db, scope).AnyAsync(r => r.Id == requestId, ct)) throw ApplicationFault.NotFound();
        var instance = await db.WorkflowInstances.AsNoTracking().SingleOrDefaultAsync(x => x.RequestId == requestId, ct);
        return instance is null ? null : await View(instance, user, ct);
    }

    public async Task<WorkflowRuntimeView> StartAsync(Guid requestId, CancellationToken ct)
    {
        var (tenant, user) = await Identity(ct);
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var request = await Lock(requestId, tenant, ct);
        if (request.RequesterId != user) throw ApplicationFault.NotFound();
        await authorizer.AuthorizeAsync("requests.submit", new(tenant, user), cancellationToken: ct);
        var existing = await db.WorkflowInstances.SingleOrDefaultAsync(x => x.RequestId == request.Id, ct);
        var instance = existing ?? await StartSubmittedAsync(request, ct) ?? throw Invalid("Dịch vụ chưa có quy trình đang phát hành.");
        await tx.CommitAsync(ct);
        await PublishPendingAsync(requestId, ct);
        return await View(instance, user, ct);
    }

    // Called from submission/creation stores inside their existing transaction: never commits independently.
    public async Task<WorkflowInstance?> StartSubmittedAsync(WorkRequest request, CancellationToken ct)
    {
        var (tenant, user) = await Identity(ct);
        if (db.Database.CurrentTransaction is null || request.TenantId != tenant || request.RequesterId != user)
            throw ApplicationFault.NotFound();
        if (await db.WorkflowInstances.AnyAsync(x => x.RequestId == request.Id, ct)) throw Conflict();
        var service = await db.Services.SingleOrDefaultAsync(x => x.Id == request.ServiceId, ct);
        if (service?.ActiveWorkflowVersionId is not { } versionId) return null;
        if (request.Status != RequestState.Submitted) throw Conflict();
        var version = await db.WorkflowVersions.SingleOrDefaultAsync(x => x.Id == versionId && x.Status == WorkflowVersionStatus.Published, ct)
            ?? throw Invalid("Phiên bản quy trình chưa được phát hành.");
        if (!await db.Workflows.AnyAsync(x => x.Id == version.WorkflowId && x.BusinessType == WorkflowBusinessType.Request, ct)) throw ApplicationFault.NotFound();
        using var definition = JsonDocument.Parse(version.DefinitionJson);
        using var values = JsonDocument.Parse(request.FormDataJson);
        WorkflowParallelGraph.Validate(definition.RootElement);
        WorkflowForm.Validate(definition.RootElement, values.RootElement);
        WorkflowApprovalMatrix.Validate(definition.RootElement);
        WorkflowAgentContract.Validate(definition.RootElement);
        var payload = JsonNode.Parse(request.FormDataJson)!.AsObject();
        payload["priority"] = RequestListCodes.Priority(request.Priority); payload["title"] = request.Title;
        payload["requesterId"] = request.RequesterId.ToString(); payload["serviceId"] = request.ServiceId.ToString(); payload["categoryId"] = request.CategoryId.ToString();
        var now = RequestCreationRules.DatabaseTime(clock.GetUtcNow());
        var instance = WorkflowInstance.Start(tenant, request.Id, versionId, user, payload.ToJsonString(), now);
        db.WorkflowInstances.Add(instance);
        using var data = JsonDocument.Parse(instance.PayloadJson);
        var requester = await db.Users.SingleAsync(x => x.Id == user, ct);
        var next = engine.ResolveStepFromGraph(definition.RootElement, null, null, data.RootElement, user, requester.DepartmentId);
        await Enter(instance, request, next, now, ct);
        request.ApplyRuntime(instance, now);
        db.AuditLogs.Add(AuditLog.WorkflowRuntimeChanged(tenant, user, request.Id, "WORKFLOW.STARTED",
            new { instance.Id, instance.WorkflowVersionId, instance.Status, instance.Revision }, now));
        await db.SaveChangesAsync(ct);
        await EnqueueAgents(instance, request, definition.RootElement, now, ct);
        return instance;
    }

    public async Task<WorkflowRuntimeView> DecideAsync(Guid requestId, Guid stepId, WorkflowDecisionInput input, CancellationToken ct)
    {
        var (tenant, user) = await Identity(ct);
        if (input.Outcome is not ("COMPLETE" or "APPROVE" or "REJECT") || input.Note?.Length > 4000 ||
            input.Note?.Any(c => char.IsControl(c) && c is not ('\n' or '\r' or '\t')) == true) throw Invalid("Quyết định hoặc ghi chú không hợp lệ.");
        await using var tx = await db.Database.BeginTransactionAsync(ct);
        var request = await Lock(requestId, tenant, ct);
        var instance = await db.WorkflowInstances.SingleOrDefaultAsync(x => x.RequestId == requestId, ct) ?? throw ApplicationFault.NotFound();
        var step = await db.WorkflowExecutions.SingleOrDefaultAsync(x => x.Id == stepId && x.InstanceId == instance.Id, ct) ?? throw ApplicationFault.NotFound();
        var version = await db.WorkflowVersions.SingleAsync(x => x.Id == instance.WorkflowVersionId, ct);
        using var definition = JsonDocument.Parse(version.DefinitionJson);
        var node = definition.RootElement.GetProperty("nodes").EnumerateArray().Single(x => x.GetProperty("id").GetString() == step.NodeId);
        var now = RequestCreationRules.DatabaseTime(clock.GetUtcNow());
        var representation = await Representation(step, node, request, user, now, ct);
        if (representation is null) throw ApplicationFault.NotFound();
        var principal = representation.Value.Principal;
        if (instance.Status != "RUNNING" || instance.Revision != input.Revision || step.Status != "PENDING" || step.PausedAt is not null ||
            await db.WorkflowDecisions.AnyAsync(x => x.StepExecutionId == stepId && (x.ActorId == user || x.PrincipalId == principal), ct)) throw Conflict();
        if (step.NodeType is "APPROVAL" or "CONFIRMATION" && input.Outcome == "COMPLETE" ||
            step.NodeType == "DEPARTMENT" && input.Outcome != "COMPLETE") throw Invalid("Quyết định không phù hợp với loại bước.");
        var note = input.Note?.Trim();
        if ((Flag(node, "requireNotes") || input.Outcome == "REJECT") && string.IsNullOrWhiteSpace(note)) throw Invalid("Vui lòng nhập lý do hoặc kết quả xử lý.");
        if (Flag(node, "requireAttachment") && !await db.Attachments.AnyAsync(a => a.ObjectType == BizFlow.Domain.Collaboration.AttachmentObjectType.Request && a.ObjectId == requestId && a.DeletedAt == null && a.Status == BizFlow.Domain.Collaboration.AttachmentStatus.Ready, ct))
            throw Invalid("Bước này yêu cầu tệp đính kèm đã hoàn tất tải lên.");
        if (Flag(node, "requireDeliverable") && string.IsNullOrWhiteSpace(note)) throw Invalid("Vui lòng mô tả kết quả bàn giao.");
        var decision = WorkflowDecision.Record(step, user, input.Outcome, note, now, representation.Value.Delegation);
        db.WorkflowDecisions.Add(decision);
        var previous = await db.WorkflowDecisions.CountAsync(x => x.StepExecutionId == stepId && x.Outcome != "REJECT", ct);
        var finished = input.Outcome == "REJECT" || step.Policy != "ALL" || previous + 1 >= step.Candidates().Count;
        if (finished)
        {
            step.Complete(input.Outcome, now);
            if (input.Outcome == "REJECT" && !HasOutcome(definition.RootElement, step.NodeId, "REJECT"))
                instance.Advance(input.Revision, now, "REJECTED");
            else
            {
                using var data = JsonDocument.Parse(instance.PayloadJson);
                var requester = await db.Users.SingleAsync(x => x.Id == request.RequesterId, ct);
                var next = engine.ResolveStepFromGraph(definition.RootElement, step.NodeId, input.Outcome, data.RootElement, requester.Id, requester.DepartmentId);
                instance.Advance(input.Revision, now);
                await Enter(instance, request, next, now, ct);
            }
        }
        else instance.Advance(input.Revision, now);
        request.ApplyRuntime(instance, now);
        db.AuditLogs.Add(AuditLog.WorkflowRuntimeChanged(tenant, user, requestId, "WORKFLOW.STEP_DECIDED",
            new { instance.Id, stepId, principalId = principal, input.Outcome, note, instance.Revision, instance.Status }, now));
        await db.SaveChangesAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "WorkflowAgentRun" SET "Status"='HUMAN_COMPLETED',"CompletedAt"={now},"LeaseToken"=NULL,"LeaseUntil"=NULL
            WHERE "TenantId"={tenant} AND "StepId"={stepId} AND "Status" IN ('QUEUED','RETRY','RUNNING','REVIEW_REQUIRED','FAILED')
            """, ct);
        await EnqueueAgents(instance, request, definition.RootElement, now, ct);
        await tx.CommitAsync(ct);
        await PublishPendingAsync(requestId, ct);
        return await View(instance, user, ct);
    }

    private async Task Enter(WorkflowInstance instance, WorkRequest request, WorkflowStepResolution next, DateTimeOffset now, CancellationToken ct)
    {
        if (next.IsCompleted)
        {
            instance.Advance(instance.Revision, now, next.CompletionState?.ToUpperInvariant() ?? "COMPLETED"); return;
        }
        var version = await db.WorkflowVersions.AsNoTracking().SingleAsync(x => x.Id == instance.WorkflowVersionId, ct);
        using var definition = JsonDocument.Parse(version.DefinitionJson);
        var node = definition.RootElement.GetProperty("nodes").EnumerateArray().Single(x => x.GetProperty("id").GetString() == next.NextNodeId);
        if (next.NodeType is "PARALLEL_SPLIT" or "PARALLEL_JOIN")
        {
            var executions = await db.WorkflowExecutions.Where(s => s.InstanceId == instance.Id).ToListAsync(ct);
            executions = executions.Concat(db.WorkflowExecutions.Local.Where(s => s.InstanceId == instance.Id)).DistinctBy(s => s.Id).ToList();
            var gateway = executions.SingleOrDefault(s => s.NodeId == next.NextNodeId);
            if (gateway?.Status == "COMPLETED") return;
            if (gateway is null) { gateway = WorkflowStepExecution.Gateway(instance, next.NextNodeId, next.Name, next.NodeType, now); db.WorkflowExecutions.Add(gateway); }
            var edges = definition.RootElement.GetProperty("edges").EnumerateArray().ToArray();
            if (next.NodeType == "PARALLEL_JOIN")
            {
                var predecessors = edges.Where(e => e.GetProperty("target").GetString() == next.NextNodeId).Select(e => e.GetProperty("source").GetString()).ToArray();
                if (predecessors.Any(id => !executions.Any(s => s.NodeId == id && s.Status == "COMPLETED"))) return;
            }
            gateway.Complete("CONTINUE", now);
            var requester = await db.Users.SingleAsync(u => u.Id == request.RequesterId, ct);
            foreach (var edge in edges.Where(e => e.GetProperty("source").GetString() == next.NextNodeId))
            {
                var target = definition.RootElement.GetProperty("nodes").EnumerateArray().Single(n => n.GetProperty("id").GetString() == edge.GetProperty("target").GetString());
                await Enter(instance, request, WorkflowExecutionEngine.DescribeNode(target, requester.Id, requester.DepartmentId), now, ct);
            }
            return;
        }
        var effectiveNode = await EffectiveNode(node, request, ct);
        var candidates = await Eligible(effectiveNode, request, ct);
        if (candidates.Count == 0) throw Invalid($"Bước '{next.Name}' chưa có người xử lý hợp lệ; người đề nghị không được tự phê duyệt.");
        if (next.SlaHours is <= 0 or > 8760) throw Invalid("Thời hạn bước phải từ 1 đến 8760 giờ.");
        DateTimeOffset? dueAt = next.SlaHours.HasValue ? now.AddHours(next.SlaHours.Value) : null;
        if (definition.RootElement.TryGetProperty("calendarId", out var calendarProperty) && calendarProperty.ValueKind is not JsonValueKind.Null)
        {
            var calendarId = Id(definition.RootElement, "calendarId") ?? throw Invalid("Lịch làm việc không hợp lệ.");
            var calendar = await db.BusinessCalendars.AsNoTracking().SingleOrDefaultAsync(c => c.Id == calendarId, ct) ?? throw Invalid("Lịch làm việc không thuộc doanh nghiệp.");
            if (next.SlaHours.HasValue)
            {
                try { dueAt = BizFlow.Domain.Sla.BusinessTimeCalculator.AddMinutes(calendar, now, checked(next.SlaHours.Value * 60)); }
                catch (ArgumentException) { throw Invalid("Không tính được hạn xử lý từ lịch làm việc đã chọn."); }
            }
        }
        var step = WorkflowStepExecution.Open(instance, next.NextNodeId, next.Name, next.NodeType,
            Text(effectiveNode, "approvalCountPolicy") ?? next.ApprovalCountPolicy, candidates, now, dueAt);
        db.WorkflowExecutions.Add(step);
        foreach (var candidate in candidates)
        {
            db.WorkflowParticipants.Add(WorkflowParticipant.Create(step, candidate));
            db.Notifications.Add(Notification.Create(instance.TenantId, candidate, NotificationEvent.ApprovalRequired,
                "Có bước quy trình cần xử lý", $"{request.Title}: {step.Name}", $"workflow-step/{step.Id:N}/{candidate:N}", "Request", request.Id));
        }
    }

    private async Task<List<Guid>> Eligible(JsonElement node, WorkRequest request, CancellationToken ct)
    {
        node = await EffectiveNode(node, request, ct);
        var type = Text(node, "type");
        var method = Text(node, "assignmentMethod") ?? Text(node, "approverType") ?? (type == "CONFIRMATION" ? "REQUESTER" : type == "APPROVAL" ? "BUSINESS_ROLE" : "DEPARTMENT_QUEUE");
        if (method == "ROLE") method = "BUSINESS_ROLE";
        var users = db.Users.Where(u => u.Status == UserStatus.Active && u.DeletedAt == null);
        if (type == "APPROVAL") users = users.Where(u => u.Id != request.RequesterId);
        if (method == "REQUESTER") users = users.Where(u => u.Id == request.RequesterId);
        else if (method == "SPECIFIC_USER" && Id(node, "specificUserId") is { } user) users = users.Where(u => u.Id == user);
        else if (method == "BUSINESS_ROLE" && Id(node, "roleId") is { } role)
            users = users.Where(u => db.UserRoles.Any(r => r.UserId == u.Id && r.RoleId == role && db.Roles.Any(rr => rr.Id == r.RoleId && rr.Status == RecordStatus.Active)));
        else if (method == "DEPARTMENT_QUEUE" && Id(node, "departmentId") is { } dept) users = users.Where(u => u.DepartmentId == dept && db.Departments.Any(d => d.Id == dept && d.Status == RecordStatus.Active));
        else if (method is "DEPARTMENT_MANAGER" or "DIRECT_MANAGER" or "DEPARTMENT_LEAD")
        {
            var targetDepartment = Id(node, "departmentId") ?? await db.Users.Where(u => u.Id == request.RequesterId).Select(u => u.DepartmentId).SingleAsync(ct);
            if (targetDepartment is null) throw Invalid("Người đề nghị chưa có phòng ban để xác định quản lý.");
            var hierarchy = await db.Departments.Where(d => d.Status == RecordStatus.Active).Select(d => new DepartmentHierarchyNode(d.Id, d.ParentDepartmentId)).ToListAsync(ct);
            var roots = await db.ManagementScopes.ToListAsync(ct);
            var managers = roots.GroupBy(x => x.UserId).Where(g => ManagementScopeExpansion.Expand(hierarchy,
                g.Select(x => new ManagementScopeRoot(x.DepartmentId, x.IncludeDescendants)).ToArray()).Contains(targetDepartment.Value)).Select(g => g.Key).ToArray();
            users = users.Where(u => managers.Contains(u.Id));
        }
        else throw Invalid("Chưa cấu hình cách xác định người xử lý bước.");
        if (Id(node, "departmentId") is { } department && method != "REQUESTER") users = users.Where(u => u.DepartmentId == department);
        return await users.Select(u => u.Id).Order().ToListAsync(ct);
    }
    private async Task<JsonElement> EffectiveNode(JsonElement node, WorkRequest request, CancellationToken ct)
    {
        if (Text(node, "assignmentMethod") != "MATRIX") return node;
        var payload = db.WorkflowInstances.Local.FirstOrDefault(i => i.RequestId == request.Id)?.PayloadJson ??
            await db.WorkflowInstances.Where(i => i.RequestId == request.Id).Select(i => i.PayloadJson).SingleAsync(ct);
        return WorkflowApprovalMatrix.Resolve(node, JsonSerializer.Deserialize<JsonElement>(payload));
    }

    public async Task<WorkflowInboxPage> InboxAsync(int page, int pageSize, CancellationToken ct)
    {
        var (_, user) = await Identity(ct);
        if (page < 1 || pageSize is < 1 or > 100 || (long)(page - 1) * pageSize > int.MaxValue) throw Invalid("Phân trang không hợp lệ.");
        var now = clock.GetUtcNow();
        var candidateJson = JsonSerializer.Serialize(new[] { user });
        var query = from p in db.WorkflowParticipants join s in db.WorkflowExecutions on p.StepExecutionId equals s.Id
            join i in db.WorkflowInstances on s.InstanceId equals i.Id join r in db.Requests on i.RequestId equals r.Id
            where p.UserId == user && s.Status == "PENDING" && i.Status == "RUNNING" &&
                !db.WorkflowDecisions.Any(d => d.StepExecutionId == s.Id && d.ActorId == user) &&
                ((EF.Functions.JsonContains(s.CandidatesJson, candidateJson) &&
                  !db.WorkflowDecisions.Any(d => d.StepExecutionId == s.Id && d.PrincipalId == user) &&
                  !db.WorkflowDelegations.Any(d => d.StepExecutionId == s.Id && d.PrincipalId == user && d.RevokedAt == null && d.CreatedAt <= now && d.ExpiresAt > now)) ||
                 db.WorkflowDelegations.Any(d => d.StepExecutionId == s.Id && d.DelegateId == user && d.RevokedAt == null && d.CreatedAt <= now && d.ExpiresAt > now &&
                    !db.WorkflowDecisions.Any(v => v.StepExecutionId == s.Id && v.PrincipalId == d.PrincipalId)))
            select new {RequestId=r.Id, StepId=s.Id, r.Title, StepName=s.Name, s.NodeType, s.StartedAt, s.DueAt, i.Revision, s.PausedAt};
        var total = await query.LongCountAsync(ct);
        var rows = await query.OrderBy(x => x.DueAt).ThenBy(x => x.StepId).Skip((page - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(rows.Select(x => new WorkflowInboxItem(x.RequestId, x.StepId, x.Title, x.StepName, x.NodeType, x.StartedAt, x.DueAt, x.Revision, x.PausedAt)).ToArray(), page, pageSize, total);
    }

    public async Task<IReadOnlyList<WorkflowFormField>> FormAsync(Guid serviceId, CancellationToken ct)
    {
        await Identity(ct);
        var service = await db.Services.AsNoTracking().SingleOrDefaultAsync(x => x.Id == serviceId, ct) ?? throw ApplicationFault.NotFound();
        if (service.ActiveWorkflowVersionId is not { } id) return [];
        var version = await db.WorkflowVersions.AsNoTracking().SingleAsync(x => x.Id == id, ct);
        using var definition = JsonDocument.Parse(version.DefinitionJson);
        return WorkflowForm.Fields(definition.RootElement);
    }
    public async Task<IReadOnlyList<WorkflowCalendarOption>> CalendarsAsync(CancellationToken ct)
    {
        var (tenant, _) = await Identity(ct);
        await authorizer.AuthorizeAsync("workflows.read", new(tenant), cancellationToken: ct);
        return await db.BusinessCalendars.AsNoTracking().Select(c => new WorkflowCalendarOption(c.Id,
            db.SlaProfiles.Where(p => db.SlaVersions.Any(v => v.SlaProfileId == p.Id && v.CalendarId == c.Id)).Select(p => p.Name).FirstOrDefault() ?? c.TimeZone,
            c.TimeZone)).ToListAsync(ct);
    }
    public async Task<WorkflowMetrics> MetricsAsync(int days, CancellationToken ct)
    {
        var (tenant, user) = await Identity(ct);
        await authorizer.AuthorizeAsync("reports.manager", new(tenant), cancellationToken: ct);
        if (days is < 1 or > 366) throw Invalid("Khoảng báo cáo phải từ 1 đến 366 ngày.");
        var access = await snapshots.ResolveAsync(user, tenant, ct);
        var (_, scope) = RequestReadPolicy.Resolve(access!);
        if (scope is null) throw ApplicationFault.NotFound();
        var visible = RequestReadQueries.Visible(db, scope).Select(r => r.Id);
        var now = clock.GetUtcNow(); var since = now.AddDays(-days);
        var rows = await (from s in db.WorkflowExecutions join i in db.WorkflowInstances on s.InstanceId equals i.Id
            join v in db.WorkflowVersions on i.WorkflowVersionId equals v.Id join w in db.Workflows on v.WorkflowId equals w.Id
            where visible.Contains(i.RequestId) && (s.StartedAt >= since || s.Status == "PENDING" && i.Status == "RUNNING") &&
                (s.NodeType == "DEPARTMENT" || s.NodeType == "APPROVAL" || s.NodeType == "CONFIRMATION")
            select new {WorkflowId=w.Id,Workflow=w.Name,v.VersionNo,s.NodeId,s.Name,s.Status,s.StartedAt,s.CompletedAt,s.DueAt,s.PausedAt,InstanceStatus=i.Status})
            .ToListAsync(ct);
        var metrics = rows.GroupBy(x => new {x.WorkflowId,x.Workflow,x.VersionNo,x.NodeId,x.Name}).Select(g =>
        {
            var durations = g.Where(x => x.CompletedAt.HasValue).Select(x => (x.CompletedAt!.Value-x.StartedAt).TotalMinutes).Order().ToArray();
            return new WorkflowStepMetric(g.Key.Workflow,g.Key.VersionNo,g.Key.Name,g.Count(x => x.Status == "PENDING" && x.InstanceStatus == "RUNNING"),durations.Length,
                g.Count(x => x.DueAt.HasValue && (x.CompletedAt ?? x.PausedAt ?? now) > x.DueAt && (x.Status != "PENDING" || x.InstanceStatus == "RUNNING")),
                durations.Length == 0 ? null : Math.Round(durations.Average(),1),durations.Length == 0 ? null : Math.Round(durations[(int)Math.Ceiling(durations.Length*.9)-1],1));
        }).OrderByDescending(x => x.Breached).ThenByDescending(x => x.Waiting).ToArray();
        return new(since,now,metrics);
    }

    private async Task<WorkflowRuntimeView> View(WorkflowInstance instance, Guid user, CancellationToken ct)
    {
        var version = await db.WorkflowVersions.AsNoTracking().SingleAsync(x => x.Id == instance.WorkflowVersionId, ct);
        var workflow = await db.Workflows.AsNoTracking().SingleAsync(x => x.Id == version.WorkflowId, ct);
        var steps = await db.WorkflowExecutions.AsNoTracking().Where(x => x.InstanceId == instance.Id).OrderBy(x => x.StartedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var decisions = await db.WorkflowDecisions.AsNoTracking().Where(x => x.InstanceId == instance.Id).OrderBy(x => x.DecidedAt).ToListAsync(ct);
        var delegations = await db.WorkflowDelegations.AsNoTracking().Where(x => x.InstanceId == instance.Id).OrderBy(x => x.CreatedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var pauses = await db.WorkflowSlaPauses.AsNoTracking().Where(x => x.InstanceId == instance.Id).OrderBy(x => x.PausedAt).ThenBy(x => x.Id).ToListAsync(ct);
        var agents = (await WorkflowAgentPersistence.ReadAsync(db, instance.TenantId, instance.Id, ct)).ToDictionary(a => a.StepId);
        var canConfigure = (await snapshots.ResolveAsync(user, instance.TenantId, ct))!.Grants.Any(g => g.Code == "workflows.configure");
        var now = clock.GetUtcNow();
        var names = await db.Users.AsNoTracking().ToDictionaryAsync(x => x.Id, x => x.FullName, ct);
        using var definition = JsonDocument.Parse(version.DefinitionJson);
        var request = await db.Requests.AsNoTracking().SingleAsync(x => x.Id == instance.RequestId, ct);
        var views = new List<WorkflowExecutionView>();
        foreach (var s in steps)
        {
            var node = definition.RootElement.GetProperty("nodes").EnumerateArray().Single(x => x.GetProperty("id").GetString() == s.NodeId);
            var representation = s.NodeType is "PARALLEL_SPLIT" or "PARALLEL_JOIN" ? null : await Representation(s, node, request, user, now, ct);
            var canRepresent = representation is not null &&
                !decisions.Any(d => d.StepExecutionId == s.Id && (d.ActorId == user || d.PrincipalId == representation.Value.Principal));
            var canAct = instance.Status == "RUNNING" && s.Status == "PENDING" && s.PausedAt is null && canRepresent;
            views.Add(new(s.Id, s.NodeId, s.Name, s.NodeType, s.Status, s.Policy, s.StartedAt, s.DueAt, s.CompletedAt, s.Outcome, canAct,
                s.Candidates().Select(id => names.GetValueOrDefault(id, "Người dùng đã ngừng hoạt động")).ToArray(),
                decisions.Where(d => d.StepExecutionId == s.Id).Select(d => new WorkflowDecisionView(d.ActorId, names.GetValueOrDefault(d.ActorId, "Người dùng"), d.Outcome, d.Note, d.DecidedAt,
                    d.PrincipalId, names.GetValueOrDefault(d.PrincipalId, "Người dùng"))).ToArray(),
                s.Candidates().Select(id => new WorkflowCandidateView(id, names.GetValueOrDefault(id, "Người dùng"), decisions.Any(d => d.StepExecutionId == s.Id && d.PrincipalId == id))).ToArray(),
                delegations.Where(d => d.StepExecutionId == s.Id).Select(d => new WorkflowDelegationView(d.Id, d.PrincipalId, names.GetValueOrDefault(d.PrincipalId, "Người dùng"),
                    d.DelegateId, names.GetValueOrDefault(d.DelegateId, "Người dùng"), names.GetValueOrDefault(d.CreatedBy, "Người dùng"), d.Reason, d.CreatedAt, d.ExpiresAt, d.RevokedAt, d.IsActive(now))).ToArray(),
                s.PausedAt, instance.Status == "RUNNING" && s.Status == "PENDING" && s.DueAt is not null && (canConfigure || canRepresent),
                pauses.Where(p => p.StepExecutionId == s.Id).Select(p => new WorkflowSlaPauseView(p.Id,names.GetValueOrDefault(p.PausedBy,"Người dùng"),p.Reason,p.PausedAt,p.DueAtBefore,
                    p.ResumedAt,p.ResumedBy is { } actor ? names.GetValueOrDefault(actor,"Người dùng") : null,p.DueAtAfter)).ToArray(), agents.GetValueOrDefault(s.Id)?.View()));
        }
        return new(instance.Id, instance.RequestId, version.Id, workflow.Name, version.VersionNo, instance.Status, instance.Revision,
            instance.StartedAt, instance.CompletedAt, JsonSerializer.Deserialize<JsonElement>(request.FormDataJson), views, WorkflowForm.Fields(definition.RootElement),
            canConfigure);
    }
    private static string? Text(JsonElement node, string key) => node.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()?.ToUpperInvariant() : null;
    public async Task PublishPendingAsync(Guid requestId, CancellationToken ct)
    {
        var recipients = await (from p in db.WorkflowParticipants join s in db.WorkflowExecutions on p.StepExecutionId equals s.Id
            join i in db.WorkflowInstances on s.InstanceId equals i.Id where i.RequestId == requestId && i.Status == "RUNNING" && s.Status == "PENDING"
            select p.UserId).Distinct().ToArrayAsync(ct);
        if (context.TenantId is { } tenant && recipients.Length > 0) await updates.PublishAsync(tenant, recipients, ct);
    }
    private static Guid? Id(JsonElement node, string key) => node.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String && Guid.TryParse(v.GetString(), out var id) ? id : null;
    private static bool Flag(JsonElement node, string key) => node.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.True;
    private static bool HasOutcome(JsonElement graph, string nodeId, string outcome) => graph.TryGetProperty("edges", out var edges) &&
        edges.EnumerateArray().Any(e => e.GetProperty("source").GetString() == nodeId && (Text(e, "outcomeCode") == outcome || Text(e, "label") == outcome));
}
