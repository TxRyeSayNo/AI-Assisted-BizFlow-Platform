using System.Text.Json;
using BizFlow.Domain.Common;
using BizFlow.Domain.Authentication;
using BizFlow.Domain.Sla;
using BizFlow.Domain.Services;
using BizFlow.Domain.Tasks;

namespace BizFlow.Domain.Audit;

public enum AuditActorType { User, System, AiAgent }

public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public AuditActorType ActorType { get; private set; }
    public Guid? ActorId { get; private set; }
    public string Action { get; private set; } = "";
    public string? ObjectType { get; private set; }
    public Guid? ObjectId { get; private set; }
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }
    public string? MetadataJson { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    private AuditLog() { }

    public static AuditLog TaskAccepted(WorkTask task, TaskAssignment assignment, Confirmation confirmation, string? keyHash, string? fingerprint)
    {
        if (task.Status != TaskState.Accepted || task.WorkflowMutation?.Before != TaskState.Assigned ||
            confirmation.ObjectId != task.Id || confirmation.ActorId != task.WorkflowMutation.ActorId ||
            assignment.UserId != confirmation.ActorId || assignment.TaskId != task.Id || assignment.AcceptedAt != confirmation.ConfirmedAt)
            throw new ArgumentException("Acceptance audit requires matching workflow and confirmation evidence.");
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((keyHash is not null || fingerprint is not null) && (!Hash(keyHash) || !Hash(fingerprint))) throw new ArgumentException("Invalid replay hashes.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = task.TenantId, ActorType = AuditActorType.User, ActorId = confirmation.ActorId,
            ObjectType = "Task", ObjectId = task.Id, Action = "TASK.ACCEPTED", CreatedAt = confirmation.ConfirmedAt,
            BeforeJson = JsonSerializer.Serialize(new { status = "ASSIGNED" }),
            AfterJson = JsonSerializer.Serialize(new { taskId = task.Id, assignmentId = assignment.Id, confirmationId = confirmation.Id,
                status = "ACCEPTED", acceptedAt = assignment.AcceptedAt, userId = assignment.UserId, departmentId = assignment.DepartmentId, note = confirmation.Note }),
            MetadataJson = keyHash is null ? null : JsonSerializer.Serialize(new { idempotency = new { operation = "TASK.ACCEPT", keyHash, fingerprint } })
        };
    }

    public static AuditLog TaskAssigned(WorkTask task, TaskAssignment assignment, string? note, DateTimeOffset now,
        string? keyHash, string? fingerprint)
    {
        if (task.WorkflowMutation is not { } mutation || task.Status != TaskState.Assigned || assignment.TaskId != task.Id ||
            assignment.AssignedBy != mutation.ActorId) throw new ArgumentException("Assignment audit requires the authorized workflow mutation.");
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((keyHash is not null || fingerprint is not null) && (!Hash(keyHash) || !Hash(fingerprint))) throw new ArgumentException("Invalid replay hashes.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = task.TenantId, ActorType = AuditActorType.User, ActorId = assignment.AssignedBy,
            ObjectType = "Task", ObjectId = task.Id, Action = "TASK.ASSIGNED", CreatedAt = now,
            BeforeJson = JsonSerializer.Serialize(new { status = mutation.Before.ToString().ToUpperInvariant() }),
            AfterJson = JsonSerializer.Serialize(new { taskId = task.Id, assignmentId = assignment.Id, status = "ASSIGNED",
                assignedAt = assignment.AssignedAt, userId = assignment.UserId, departmentId = assignment.DepartmentId, note }),
            MetadataJson = keyHash is null ? null : JsonSerializer.Serialize(new { idempotency = new { operation = "TASK.ASSIGN", keyHash, fingerprint } })
        };
    }

    // FR-TASK-001 / BR-020: creation and a subsequent assignment are separate audit events.
    // The Application transaction must persist this snapshot with the draft and initial checklist.
    public static AuditLog TaskCreated(WorkTask task, IReadOnlyList<TaskChecklistItem> checklist, DateTimeOffset now,
        string? idempotencyKeyHash = null, string? requestFingerprint = null)
    {
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((idempotencyKeyHash is not null || requestFingerprint is not null) &&
            (!Hash(idempotencyKeyHash) || !Hash(requestFingerprint)))
            throw new ArgumentException("Replay metadata requires two SHA-256 hashes.");
        if (task.Status != TaskState.Draft || checklist.Any(item => item.TaskId != task.Id || item.IsCompleted))
            throw new ArgumentException("Task creation audit requires a draft and its initial checklist.", nameof(checklist));
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = task.TenantId, ActorType = AuditActorType.User,
            ActorId = task.CreatorId, ObjectType = "Task", ObjectId = task.Id, Action = "TASK.CREATED",
            MetadataJson = idempotencyKeyHash is null ? null : JsonSerializer.Serialize(new
            { idempotency = new { operation = "TASK.CREATE", keyHash = idempotencyKeyHash, fingerprint = requestFingerprint } }),
            AfterJson = JsonSerializer.Serialize(new
            {
                taskId = task.Id, creatorId = task.CreatorId, requestId = task.RequestId,
                title = task.Title, description = task.Description, priority = task.Priority.ToString().ToUpperInvariant(),
                deadline = task.Deadline, status = "DRAFT", workflowVersionId = task.WorkflowVersionId,
                slaVersionId = task.SlaVersionId, createdAt = task.CreatedAt,
                checklist = checklist.OrderBy(item => item.SortOrder).ThenBy(item => item.Id).Select(item => new
                { checklistItemId = item.Id, title = item.Title, sortOrder = item.SortOrder, isCompleted = item.IsCompleted })
            }),
            CreatedAt = now.ToUniversalTime()
        };
    }

    public static AuditLog ServiceMetadataUpdated(Guid tenantId, Guid actorId, InternalService service, ServiceMetadata before, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)), ActorType = AuditActorType.User,
        ActorId = EntityRules.Id(actorId, nameof(actorId)), ObjectType = "Service", ObjectId = service.Id, Action = "SERVICE.UPDATED",
        BeforeJson = ServiceMetadataJson(before), AfterJson = ServiceMetadataJson(service.Metadata()), CreatedAt = now.ToUniversalTime()
    };
    private static string ServiceMetadataJson(ServiceMetadata value) => JsonSerializer.Serialize(new {
        code = value.Code, name = value.Name, description = value.Description, status = value.Status.ToString().ToUpperInvariant() });

    public static AuditLog ServiceCategoryCreated(Guid tenantId, Guid actorId, ServiceCategory category, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)), ActorType = AuditActorType.User,
        ActorId = EntityRules.Id(actorId, nameof(actorId)), ObjectType = "ServiceCategory", ObjectId = category.Id, Action = "SERVICE.CATEGORY_CREATED",
        AfterJson = JsonSerializer.Serialize(new { serviceId = category.ServiceId, code = category.Code, name = category.Name,
            status = category.Status.ToString().ToUpperInvariant() }), CreatedAt = now.ToUniversalTime()
    };

    public static AuditLog ServiceCreated(Guid tenantId, Guid actorId, InternalService service, IReadOnlyList<ServiceCategory> categories, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)), ActorType = AuditActorType.User,
        ActorId = EntityRules.Id(actorId, nameof(actorId)), ObjectType = "Service", ObjectId = service.Id, Action = "SERVICE.CREATED",
        AfterJson = JsonSerializer.Serialize(new { code = service.Code, name = service.Name, description = service.Description,
            status = service.Status.ToString().ToUpperInvariant(), categories = categories.Select(c => new { serviceCategoryId = c.Id,
                code = c.Code, name = c.Name, status = c.Status.ToString().ToUpperInvariant() }) }), CreatedAt = now.ToUniversalTime()
    };

    public static AuditLog SlaVersionCreated(Guid tenantId, Guid actorId, SlaVersion version, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)), ActorType = AuditActorType.User,
        ActorId = EntityRules.Id(actorId, nameof(actorId)), ObjectType = "SLAVersion", ObjectId = version.Id, Action = "SLA.VERSION_CREATED",
        AfterJson = JsonSerializer.Serialize(new { slaProfileId = version.SlaProfileId, versionNo = version.VersionNo,
            targetMinutes = version.TargetMinutes, warningMinutes = version.WarningMinutes, calendarId = version.CalendarId,
            escalationConfig = version.EscalationConfigJson is null ? (JsonElement?)null : JsonSerializer.Deserialize<JsonElement>(version.EscalationConfigJson) }),
        CreatedAt = now.ToUniversalTime()
    };

    public static AuditLog SlaProfileCreated(Guid tenantId, Guid actorId, Guid profileId, string name, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)), ActorType = AuditActorType.User,
        ActorId = EntityRules.Id(actorId, nameof(actorId)), ObjectType = "SLAProfile", ObjectId = EntityRules.Id(profileId, nameof(profileId)),
        Action = "SLA.PROFILE_CREATED", AfterJson = JsonSerializer.Serialize(new { name, status = "DRAFT" }), CreatedAt = now.ToUniversalTime()
    };

    public static AuditLog RoleConfiguration(Guid tenantId, Guid actorId, Guid roleId, string roleName,
        Guid[]? beforePermissions, Guid[] afterPermissions, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
        ActorType = AuditActorType.User, ActorId = EntityRules.Id(actorId, nameof(actorId)),
        ObjectType = "Role", ObjectId = EntityRules.Id(roleId, nameof(roleId)),
        Action = beforePermissions is null ? "ROLE.CREATED" : "ROLE.PERMISSIONS_CONFIGURED",
        BeforeJson = beforePermissions is null ? null : JsonSerializer.Serialize(new { name = roleName, permissionIds = beforePermissions.Order().ToArray() }),
        AfterJson = JsonSerializer.Serialize(new { name = roleName, permissionIds = afterPermissions.Order().ToArray() }),
        CreatedAt = now.ToUniversalTime()
    };

    public static AuditLog WorkflowDraftCreated(Guid tenantId, Guid actorId, Guid workflowId, string name,
        string businessType, Guid versionId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
        ActorType = AuditActorType.User, ActorId = EntityRules.Id(actorId, nameof(actorId)),
        ObjectType = "Workflow", ObjectId = EntityRules.Id(workflowId, nameof(workflowId)), Action = "WORKFLOW.DRAFT_CREATED",
        AfterJson = JsonSerializer.Serialize(new { name, businessType, status = "DRAFT", versionId, versionNo = 1 }),
        CreatedAt = now.ToUniversalTime()
    };

    public static AuditLog WorkflowVersionDraftCreated(Guid tenantId, Guid actorId, Guid workflowId,
        Guid versionId, int versionNo, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
        ActorType = AuditActorType.User, ActorId = EntityRules.Id(actorId, nameof(actorId)),
        ObjectType = "WorkflowVersion", ObjectId = EntityRules.Id(versionId, nameof(versionId)),
        Action = "WORKFLOW.VERSION_DRAFT_CREATED",
        AfterJson = JsonSerializer.Serialize(new { workflowId, versionId, versionNo, status = "DRAFT" }),
        CreatedAt = now.ToUniversalTime()
    };

    public static AuditLog NotificationRead(Guid tenantId, Guid recipientId, Guid notificationId, DateTimeOffset readAt) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
        ActorType = AuditActorType.User, ActorId = EntityRules.Id(recipientId, nameof(recipientId)),
        Action = "NOTIFICATION.READ", ObjectType = "Notification", ObjectId = EntityRules.Id(notificationId, nameof(notificationId)),
        AfterJson = JsonSerializer.Serialize(new { readAt = readAt.ToUniversalTime() }), CreatedAt = readAt.ToUniversalTime()
    };

    public static AuditLog Authentication(Guid? tenantId, Guid userId, AuthenticationEvent action, DateTimeOffset now, Guid? familyId) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = tenantId, ActorType = AuditActorType.System,
        // Failed credential attempts are not attributed to an authenticated human.
        ActorId = null, ObjectType = "User", ObjectId = EntityRules.Id(userId, nameof(userId)),
        Action = action switch
        {
            AuthenticationEvent.LoginSucceeded => "AUTH.LOGIN_SUCCEEDED",
            AuthenticationEvent.LoginFailed => "AUTH.LOGIN_FAILED",
            AuthenticationEvent.RefreshSucceeded => "AUTH.REFRESH_SUCCEEDED",
            AuthenticationEvent.RefreshDenied => "AUTH.REFRESH_DENIED",
            AuthenticationEvent.ReplayDetected => "AUTH.REPLAY_DETECTED",
            AuthenticationEvent.PasswordResetRequested => "AUTH.PASSWORD_RESET_REQUESTED",
            AuthenticationEvent.PasswordResetSucceeded => "AUTH.PASSWORD_RESET_SUCCEEDED",
            _ => throw new ArgumentOutOfRangeException(nameof(action))
        },
        MetadataJson = JsonSerializer.Serialize(new { familyId }), CreatedAt = now.ToUniversalTime()
    };

    // Callers supply allowlisted metadata, never serialized credential-bearing entities.
    public static AuditLog DeniedAccess(Guid? tenantId, Guid? userId, string permission, string reason,
        DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = tenantId, ActorId = userId,
        ActorType = AuditActorType.User, Action = "SECURITY.ACCESS_DENIED",
        MetadataJson = JsonSerializer.Serialize(new { permission = EntityRules.Text(permission, 120, nameof(permission)), reason }),
        CreatedAt = now.ToUniversalTime()
    };
}
