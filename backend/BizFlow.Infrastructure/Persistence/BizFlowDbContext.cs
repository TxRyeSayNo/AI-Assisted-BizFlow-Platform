using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.AI;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Authentication;
using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Security;
using BizFlow.Domain.Sla;
using BizFlow.Domain.Services;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Tenancy;
using BizFlow.Domain.Workflows;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Persistence;

public sealed class BizFlowDbContext(DbContextOptions<BizFlowDbContext> options, ITenantContext context) : DbContext(options)
{
    private Guid? CurrentTenantId => context.TenantId;
    private Guid? CurrentUserId => context.UserId;
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<Tenant> Tenants => Set<Tenant>();
    public DbSet<TenantSetting> TenantSettings => Set<TenantSetting>();
    public DbSet<SlaProfile> SlaProfiles => Set<SlaProfile>();
    public DbSet<BusinessCalendar> BusinessCalendars => Set<BusinessCalendar>();
    public DbSet<SlaVersion> SlaVersions => Set<SlaVersion>();
    public DbSet<InternalService> Services => Set<InternalService>();
    public DbSet<ServiceCategory> ServiceCategories => Set<ServiceCategory>();
    public DbSet<WorkRequest> Requests => Set<WorkRequest>();
    public DbSet<RequestRouting> RequestRoutings => Set<RequestRouting>();
    public DbSet<RequestResolution> RequestResolutions => Set<RequestResolution>();
    public DbSet<WorkTask> WorkTasks => Set<WorkTask>();
    public DbSet<TaskChecklistItem> TaskChecklistItems => Set<TaskChecklistItem>();
    public DbSet<TaskProgressReport> TaskProgressReports => Set<TaskProgressReport>();
    public DbSet<TaskResult> TaskResults => Set<TaskResult>();
    public DbSet<TaskAssignment> TaskAssignments => Set<TaskAssignment>();
    public DbSet<Confirmation> Confirmations => Set<Confirmation>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();
    public DbSet<AIInteraction> AIInteractions => Set<AIInteraction>();
    public DbSet<AIRecommendation> AIRecommendations => Set<AIRecommendation>();
    public DbSet<AIAgentAction> AIAgentActions => Set<AIAgentAction>();
    public DbSet<UserAccount> Users => Set<UserAccount>();
    public DbSet<Department> Departments => Set<Department>();
    public DbSet<ManagementScope> ManagementScopes => Set<ManagementScope>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<Permission> Permissions => Set<Permission>();
    public DbSet<UserRole> UserRoles => Set<UserRole>();
    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();
    public DbSet<AuthenticationSession> AuthenticationSessions => Set<AuthenticationSession>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<WorkflowDefinition> Workflows => Set<WorkflowDefinition>();
    public DbSet<WorkflowVersion> WorkflowVersions => Set<WorkflowVersion>();
    public DbSet<WorkflowStep> WorkflowSteps => Set<WorkflowStep>();
    public DbSet<WorkflowTransition> WorkflowTransitions => Set<WorkflowTransition>();

    protected override void OnModelCreating(ModelBuilder model)
    {
        model.ApplyConfigurationsFromAssembly(typeof(BizFlowDbContext).Assembly);
        // Default queries fail closed without both authenticated user and tenant context.
        model.Entity<Tenant>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.Id == CurrentTenantId);
        model.Entity<TenantSetting>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId);
        model.Entity<SlaProfile>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId);
        model.Entity<BusinessCalendar>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId);
        model.Entity<SlaVersion>().HasQueryFilter(x => SlaProfiles.Any(p => p.Id == x.SlaProfileId));
        model.Entity<InternalService>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId);
        model.Entity<ServiceCategory>().HasQueryFilter(x => Services.Any(s => s.Id == x.ServiceId));
        model.Entity<WorkRequest>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId && x.DeletedAt == null);
        model.Entity<RequestRouting>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId);
        model.Entity<RequestResolution>().HasQueryFilter(x => Requests.Any(r => r.Id == x.RequestId));
        model.Entity<WorkTask>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId && x.DeletedAt == null);
        model.Entity<TaskChecklistItem>().HasQueryFilter(x => WorkTasks.Any(t => t.Id == x.TaskId));
        model.Entity<TaskProgressReport>().HasQueryFilter(x => WorkTasks.Any(t => t.Id == x.TaskId));
        model.Entity<TaskResult>().HasQueryFilter(x => WorkTasks.Any(t => t.Id == x.TaskId));
        model.Entity<TaskAssignment>().HasQueryFilter(x => WorkTasks.Any(t => t.Id == x.TaskId));
        model.Entity<Confirmation>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId);
        model.Entity<Comment>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId && x.DeletedAt == null);
        model.Entity<Attachment>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId && x.DeletedAt == null);
        model.Entity<AIInteraction>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId);
        model.Entity<AIRecommendation>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId);
        model.Entity<AIAgentAction>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId);
        model.Entity<Company>().HasQueryFilter(x => Tenants.Any(t => t.CompanyId == x.Id));
        model.Entity<UserAccount>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId && x.DeletedAt == null);
        model.Entity<Department>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId);
        model.Entity<ManagementScope>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId);
        model.Entity<Role>().HasQueryFilter(x => CurrentUserId != null && (x.IsSystem || (CurrentTenantId != null && x.TenantId == CurrentTenantId)));
        model.Entity<Permission>().HasQueryFilter(x => CurrentUserId != null && (CurrentTenantId == null || x.ScopeType != PermissionScope.Platform));
        model.Entity<UserRole>().HasQueryFilter(x => Users.Any(u => u.Id == x.UserId));
        model.Entity<RolePermission>().HasQueryFilter(x => Roles.Any(r => r.Id == x.RoleId) && Permissions.Any(p => p.Id == x.PermissionId));
        model.Entity<AuthenticationSession>().HasQueryFilter(x => CurrentUserId != null && x.UserId == CurrentUserId);
        model.Entity<AuditLog>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId);
        model.Entity<Notification>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId && x.RecipientId == CurrentUserId);
        model.Entity<WorkflowDefinition>().HasQueryFilter(x => CurrentUserId != null && CurrentTenantId != null && x.TenantId == CurrentTenantId);
        model.Entity<WorkflowVersion>().HasQueryFilter(x => Workflows.Any(w => w.Id == x.WorkflowId));
        model.Entity<WorkflowStep>().HasQueryFilter(x => WorkflowVersions.Any(v => v.Id == x.WorkflowVersionId));
        model.Entity<WorkflowTransition>().HasQueryFilter(x => WorkflowVersions.Any(v => v.Id == x.WorkflowVersionId));
    }

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use SaveChangesAsync so tenant and ownership checks cannot be bypassed.");

    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        ChangeTracker.DetectChanges();
        var entries = ChangeTracker.Entries().Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted).ToArray();
        bool? platformAccount = null;
        async Task AssertPlatformAccount()
        {
            // Plane guard only; the calling Application use case must also authorize the exact action.
            platformAccount ??= CurrentTenantId is null && CurrentUserId is { } userId && userId != Guid.Empty &&
                await Users.IgnoreQueryFilters().AnyAsync(u => u.Id == userId && u.IsPlatformAdministrator &&
                    u.TenantId == null && u.Status == UserStatus.Active && u.DeletedAt == null, cancellationToken);
            if (platformAccount != true) throw Denied();
        }

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Deleted && entry.Entity is not UserRole and not RolePermission)
                throw new ApplicationFault(FaultKind.Conflict, "PERSISTENCE.HARD_DELETE_DENIED", "Historical records cannot be deleted.");

            if (entry.State == EntityState.Modified)
            {
                foreach (var property in new[] { "TenantId", "UserId", "FamilyId", "TokenHash", "CreatedAt", "IsPlatformAdministrator", "IsSystem", "WorkflowId", "WorkflowVersionId", "VersionNo", "BusinessType", "ObjectType", "ObjectId", "AuthorId", "UploadedBy", "ObjectKey" })
                {
                    if (entry.Metadata.FindProperty(property) is not null && entry.Property(property).IsModified)
                        throw new ApplicationFault(FaultKind.Conflict, "PERSISTENCE.IMMUTABLE_FIELD", "Record ownership and identity cannot be changed.");
                }
            }

            switch (entry.Entity)
            {
                case AuditLog audit:
                    var authenticationEvent = audit.ActorType == AuditActorType.System && audit.ActorId is null &&
                        CurrentUserId is not null && audit.ObjectType == "User" && audit.ObjectId == CurrentUserId &&
                        audit.Action is "AUTH.LOGIN_SUCCEEDED" or "AUTH.LOGIN_FAILED" or "AUTH.REFRESH_SUCCEEDED" or "AUTH.REFRESH_DENIED" or "AUTH.REPLAY_DETECTED" or "AUTH.PASSWORD_RESET_REQUESTED" or "AUTH.PASSWORD_RESET_SUCCEEDED";
                    if (entry.State != EntityState.Added || audit.TenantId != CurrentTenantId || (audit.ActorId != CurrentUserId && !authenticationEvent))
                        throw Denied();
                    break;
                case Department department:
                    AssertTenant(department.TenantId);
                    break;
                case TenantSetting setting:
                    AssertTenant(setting.TenantId);
                    if (setting.UpdatedBy != CurrentUserId || !await Users.AnyAsync(u => u.Id == setting.UpdatedBy, cancellationToken)) throw Denied();
                    if (entry.State == EntityState.Modified &&
                        (entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not (nameof(TenantSetting.ValueJson) or nameof(TenantSetting.UpdatedBy) or nameof(TenantSetting.UpdatedAt))) ||
                        !await TenantSettings.AsNoTracking().AnyAsync(s => s.Id == setting.Id && s.Key == setting.Key, cancellationToken))) throw Denied();
                    break;
                case Notification notification:
                    AssertTenant(notification.TenantId);
                    if (!await Users.AnyAsync(u => u.Id == notification.RecipientId, cancellationToken)) throw Denied();
                    if (entry.State == EntityState.Modified)
                    {
                        if (notification.RecipientId != CurrentUserId ||
                            !await Notifications.AsNoTracking().AnyAsync(n => n.Id == notification.Id, cancellationToken) ||
                            entry.Properties.Any(p => p.IsModified && p.Metadata.Name != nameof(Notification.ReadAt))) throw Denied();
                    }
                    break;
                case SlaProfile profile:
                    AssertTenant(profile.TenantId);
                    if (entry.State != EntityState.Added || profile.Status != SlaProfileStatus.Draft) throw Denied();
                    break;
                case WorkTask task:
                    AssertTenant(task.TenantId);
                    if (entry.State == EntityState.Modified)
                    {
                        if (task.DeletedAt is not null &&
                            (task.Status is TaskState.Completed or TaskState.Cancelled) &&
                            task.WorkflowMutation is null)
                        {
                            if (entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not (nameof(WorkTask.DeletedAt) or nameof(WorkTask.UpdatedAt))))
                                throw Denied();
                            var originalDeletedAt = (DateTimeOffset?)entry.OriginalValues[nameof(WorkTask.DeletedAt)];
                            if (originalDeletedAt is not null) throw Denied();
                            break;
                        }

                        var mutation = task.WorkflowMutation;
                        if (mutation is null || mutation.ActorId != CurrentUserId || task.Status != mutation.After ||
                            task.Status is not (TaskState.Assigned or TaskState.Accepted or TaskState.InProgress or TaskState.Submitted or TaskState.Confirmed or TaskState.Completed) ||
                            task.WorkflowVersionId is not null || task.SlaVersionId is not null ||
                            entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not (nameof(WorkTask.Status) or nameof(WorkTask.UpdatedAt) or nameof(WorkTask.CompletedAt))) ||
                            !await WorkTasks.AsNoTracking().AnyAsync(t => t.Id == task.Id && t.Status == mutation.Before &&
                                t.UpdatedAt == mutation.BeforeUpdatedAt && t.WorkflowVersionId == null && t.SlaVersionId == null, cancellationToken)) throw Denied();
                        if (task.Status == TaskState.Completed && (task.CompletedAt is null || task.CompletedAt != task.UpdatedAt)) throw Denied();
                        break;
                    }
                    // Draft persistence only; the workflow engine remains the sole future state mutator.
                    if (entry.State != EntityState.Added || task.Status != TaskState.Draft ||
                        task.WorkflowVersionId is not null || task.SlaVersionId is not null ||
                        task.CompletedAt is not null || task.DeletedAt is not null ||
                        !await Users.AnyAsync(u => u.Id == task.CreatorId, cancellationToken) ||
                        (task.RequestId is { } requestId && !await Requests.AnyAsync(r => r.Id == requestId, cancellationToken))) throw Denied();
                    break;
                case TaskChecklistItem checklist:
                    if (entry.State != EntityState.Added || checklist.IsCompleted || checklist.CompletedBy is not null || checklist.CompletedAt is not null) throw Denied();
                    var owningTask = WorkTasks.Local.FirstOrDefault(t => t.Id == checklist.TaskId && Entry(t).State == EntityState.Added) ??
                        await WorkTasks.AsNoTracking().SingleOrDefaultAsync(t => t.Id == checklist.TaskId, cancellationToken);
                    if (owningTask is null || owningTask.Status != TaskState.Draft || owningTask.DeletedAt is not null) throw Denied();
                    AssertTenant(owningTask.TenantId);
                    break;
                case TaskAssignment assignment:
                    // Initial records only until the authorized assignment/receipt/end use cases exist.
                    // Persisted ownership and active targets cannot be replaced by tracked forgeries.
                    if (entry.State != EntityState.Added || assignment.AcceptedAt is not null || assignment.RejectedAt is not null ||
                        assignment.RejectionReason is not null || assignment.EndedAt is not null ||
                        !await WorkTasks.AnyAsync(t => t.Id == assignment.TaskId && t.Status != TaskState.Completed && t.Status != TaskState.Cancelled, cancellationToken) ||
                        !await Users.AnyAsync(u => u.Id == assignment.AssignedBy, cancellationToken) ||
                        (assignment.UserId is { } targetUser && !await Users.AnyAsync(u => u.Id == targetUser && u.Status == UserStatus.Active, cancellationToken)) ||
                        (assignment.DepartmentId is { } targetDepartment && !await Departments.AnyAsync(d => d.Id == targetDepartment && d.Status == RecordStatus.Active, cancellationToken))) throw Denied();
                    break;
                case Confirmation confirmation:
                    AssertTenant(confirmation.TenantId);
                    if (entry.State != EntityState.Added || confirmation.ActorId != CurrentUserId) throw Denied();
                    if (confirmation.ObjectType == "TASK")
                    {
                        var confirmedTask = WorkTasks.Local.FirstOrDefault(t => t.Id == confirmation.ObjectId);
                        if (confirmation.MilestoneType == "RECEIVE")
                        {
                            if (confirmation.Decision != "CONFIRMED" ||
                                confirmedTask?.Status != TaskState.Accepted || confirmedTask.WorkflowMutation?.ActorId != CurrentUserId ||
                                confirmedTask.UpdatedAt != confirmation.ConfirmedAt ||
                                !await TaskAssignments.AnyAsync(a => a.TaskId == confirmation.ObjectId && a.UserId == confirmation.ActorId &&
                                    a.AcceptedAt == confirmation.ConfirmedAt && a.RejectedAt == null && a.EndedAt == null, cancellationToken)) throw Denied();
                        }
                        else if (confirmation.MilestoneType == "RESULT")
                        {
                            if (confirmation.Decision == "CONFIRMED")
                            {
                                if (confirmedTask?.Status is not (TaskState.Confirmed or TaskState.Completed) ||
                                    confirmedTask.WorkflowMutation?.ActorId != CurrentUserId || confirmedTask.UpdatedAt != confirmation.ConfirmedAt) throw Denied();
                            }
                            else if (confirmation.Decision == "REJECTED")
                            {
                                if (confirmedTask?.Status != TaskState.InProgress ||
                                    confirmedTask.WorkflowMutation?.ActorId != CurrentUserId || confirmedTask.UpdatedAt != confirmation.ConfirmedAt) throw Denied();
                            }
                            else throw Denied();
                        }
                        else throw Denied();
                    }
                    else if (confirmation.ObjectType == "REQUEST")
                    {
                        var confirmedRequest = Requests.Local.FirstOrDefault(r => r.Id == confirmation.ObjectId) ??
                            await Requests.AsNoTracking().SingleOrDefaultAsync(r => r.Id == confirmation.ObjectId, cancellationToken);
                        if (confirmedRequest is null) throw Denied();

                        if (confirmation.MilestoneType == "RECEIVE")
                        {
                            if (confirmation.Decision != "CONFIRMED" ||
                                confirmedRequest.Status != RequestState.Received ||
                                confirmedRequest.UpdatedAt != confirmation.ConfirmedAt) throw Denied();
                        }
                        else if (confirmation.MilestoneType == "RESOLUTION")
                        {
                            if (confirmation.Decision == "CONFIRMED")
                            {
                                if (confirmedRequest.Status is not (RequestState.Confirmed or RequestState.Closed) ||
                                    confirmedRequest.UpdatedAt != confirmation.ConfirmedAt) throw Denied();
                            }
                            else if (confirmation.Decision == "REJECTED")
                            {
                                if (confirmedRequest.Status != RequestState.InProgress ||
                                    confirmedRequest.UpdatedAt != confirmation.ConfirmedAt) throw Denied();
                            }
                            else throw Denied();
                        }
                        else throw Denied();
                    }
                    else throw Denied();
                    break;
                case Comment comment:
                    AssertTenant(comment.TenantId);
                    if (!await Users.AnyAsync(u => u.Id == comment.AuthorId, cancellationToken)) throw Denied();
                    if (entry.State == EntityState.Added)
                    {
                        if (comment.AuthorId != CurrentUserId) throw Denied();
                        if (comment.ObjectType == CommentObjectType.Task)
                        {
                            if (!await WorkTasks.AnyAsync(t => t.Id == comment.ObjectId, cancellationToken)) throw Denied();
                        }
                        else if (comment.ObjectType == CommentObjectType.Request)
                        {
                            if (!await Requests.AnyAsync(r => r.Id == comment.ObjectId, cancellationToken)) throw Denied();
                        }
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        if (entry.Property(nameof(Comment.Content)).IsModified && comment.AuthorId != CurrentUserId)
                            throw Denied();
                        if (comment.AuthorId != CurrentUserId)
                        {
                            var isAdmin = await UserRoles.AnyAsync(ur => ur.UserId == CurrentUserId &&
                                Roles.Any(r => r.Id == ur.RoleId && r.Name == "COMPANY_ADMIN"), cancellationToken);
                            if (!isAdmin) throw Denied();
                        }
                    }
                    break;
                case Attachment attachment:
                    AssertTenant(attachment.TenantId);
                    if (!await Users.AnyAsync(u => u.Id == attachment.UploadedBy, cancellationToken)) throw Denied();
                    if (entry.State == EntityState.Added)
                    {
                        if (attachment.UploadedBy != CurrentUserId) throw Denied();
                        var validTarget = attachment.ObjectType switch
                        {
                            AttachmentObjectType.Task => await WorkTasks.AnyAsync(t => t.Id == attachment.ObjectId, cancellationToken),
                            AttachmentObjectType.Request => await Requests.AnyAsync(r => r.Id == attachment.ObjectId, cancellationToken),
                            AttachmentObjectType.Comment => await Comments.AnyAsync(c => c.Id == attachment.ObjectId, cancellationToken),
                            AttachmentObjectType.Result => await TaskResults.AnyAsync(r => r.Id == attachment.ObjectId, cancellationToken),
                            AttachmentObjectType.Progress => await TaskProgressReports.AnyAsync(p => p.Id == attachment.ObjectId, cancellationToken),
                            _ => false
                        };
                        if (!validTarget) throw Denied();
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        if (attachment.UploadedBy != CurrentUserId)
                        {
                            var isAdmin = await UserRoles.AnyAsync(ur => ur.UserId == CurrentUserId &&
                                Roles.Any(r => r.Id == ur.RoleId && r.Name == "COMPANY_ADMIN"), cancellationToken);
                            if (!isAdmin) throw Denied();
                        }
                    }
                    break;
                case TaskProgressReport progress:
                    // State eligibility, current-percent correction rules, participant permission
                    // and audit are Application use-case gates, not inferred by this storage guard.
                    if (entry.State != EntityState.Added ||
                        !await WorkTasks.AnyAsync(t => t.Id == progress.TaskId, cancellationToken) ||
                        !await Users.AnyAsync(u => u.Id == progress.AuthorId, cancellationToken)) throw Denied();
                    break;
                case TaskResult result:
                    var submittingTask = WorkTasks.Local.FirstOrDefault(t => t.Id == result.TaskId);
                    var validTask = submittingTask is not null && Entry(submittingTask).State == EntityState.Modified
                        ? (submittingTask.Status == TaskState.Submitted && submittingTask.WorkflowMutation?.Before == TaskState.InProgress)
                        : await WorkTasks.AnyAsync(t => t.Id == result.TaskId && t.Status == TaskState.InProgress, cancellationToken);
                    if (entry.State != EntityState.Added || !validTask || !await Users.AnyAsync(u => u.Id == result.AuthorId, cancellationToken)) throw Denied();
                    break;
                case WorkRequest request:
                    AssertTenant(request.TenantId);
                    if (entry.State == EntityState.Modified)
                    {
                        if (request.DeletedAt is not null &&
                            (request.Status is RequestState.Closed or RequestState.Cancelled) &&
                            request.WorkflowMutation is null)
                        {
                            if (entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not (nameof(WorkRequest.DeletedAt) or nameof(WorkRequest.UpdatedAt))))
                                throw Denied();
                            var originalDeletedAt = (DateTimeOffset?)entry.OriginalValues[nameof(WorkRequest.DeletedAt)];
                            if (originalDeletedAt is not null) throw Denied();
                            break;
                        }

                        var mutation = request.WorkflowMutation;
                        if (mutation is null || mutation.ActorId != CurrentUserId || request.Status != mutation.After ||
                            request.WorkflowVersionId is not null || request.SlaVersionId is not null ||
                            entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not (nameof(WorkRequest.Status) or nameof(WorkRequest.UpdatedAt) or nameof(WorkRequest.ResolvedAt) or nameof(WorkRequest.ClosedAt))) ||
                            !await Requests.AsNoTracking().AnyAsync(r => r.Id == request.Id && r.Status == mutation.Before &&
                                r.UpdatedAt == mutation.BeforeUpdatedAt && r.WorkflowVersionId == null && r.SlaVersionId == null, cancellationToken)) throw Denied();
                        if (request.Status == RequestState.Resolved && (request.ResolvedAt is null || request.ResolvedAt != request.UpdatedAt)) throw Denied();
                        if (request.Status == RequestState.Closed && (request.ClosedAt is null || request.ClosedAt != request.UpdatedAt)) throw Denied();
                        break;
                    }
                    if (entry.State != EntityState.Added ||
                        request.Status is not (RequestState.Draft or RequestState.Submitted) ||
                        request.WorkflowVersionId is not null || request.SlaVersionId is not null ||
                        request.ResolvedAt is not null || request.ClosedAt is not null || request.DeletedAt is not null ||
                        !await Users.AnyAsync(u => u.Id == request.RequesterId, cancellationToken) ||
                        !await Services.AnyAsync(s => s.Id == request.ServiceId, cancellationToken) ||
                        !await ServiceCategories.AnyAsync(c => c.Id == request.CategoryId && c.ServiceId == request.ServiceId, cancellationToken) ||
                        (request.ParentRequestId is { } parentId && !await Requests.AnyAsync(r => r.Id == parentId, cancellationToken)) ||
                        (request.RevisedFromRequestId is { } sourceId && !await Requests.AnyAsync(r => r.Id == sourceId && r.Status == RequestState.Rejected, cancellationToken))) throw Denied();
                    break;
                case RequestResolution resolution:
                    // Persisted parent ownership/status: attached forged parents cannot authorize evidence.
                    // Exact action permission, assignment, revision numbering and audit belong to the use case.
                    var resolvingRequest = Requests.Local.FirstOrDefault(r => r.Id == resolution.RequestId);
                    var validParent = resolvingRequest is not null && Entry(resolvingRequest).State == EntityState.Modified
                        ? (resolvingRequest.Status == RequestState.Resolved && resolvingRequest.WorkflowMutation?.Before == RequestState.InProgress)
                        : await Requests.AnyAsync(r => r.Id == resolution.RequestId && r.Status == RequestState.InProgress, cancellationToken);
                    if (entry.State != EntityState.Added || !validParent ||
                        !await Users.AnyAsync(u => u.Id == resolution.ResolverId, cancellationToken)) throw Denied();
                    break;
                case RequestRouting routing:
                    AssertTenant(routing.TenantId);
                    if (entry.State != EntityState.Added ||
                        routing.RoutedBy != CurrentUserId ||
                        (routing.ToDepartmentId is null && routing.ToUserId is null) ||
                        !await Requests.AnyAsync(r => r.Id == routing.RequestId, cancellationToken) ||
                        !await Users.AnyAsync(u => u.Id == routing.RoutedBy, cancellationToken) ||
                        (routing.ToDepartmentId is { } toDept && !await Departments.AnyAsync(d => d.Id == toDept, cancellationToken)) ||
                        (routing.ToUserId is { } toUser && !await Users.AnyAsync(u => u.Id == toUser, cancellationToken))) throw Denied();
                    break;
                case InternalService service:
                    AssertTenant(service.TenantId);
                    // Metadata updates and category-add touches preserve identity and bindings.
                    if (entry.State == EntityState.Added)
                    { if (service.ActiveWorkflowVersionId is not null || service.ActiveSlaVersionId is not null) throw Denied(); }
                    else if (entry.State != EntityState.Modified ||
                        entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not (nameof(InternalService.Code) or nameof(InternalService.Name) or
                            nameof(InternalService.Description) or nameof(InternalService.Status) or nameof(InternalService.UpdatedAt))) ||
                        !await Services.AsNoTracking().AnyAsync(s => s.Id == service.Id && s.TenantId == service.TenantId, cancellationToken)) throw Denied();
                    break;
                case ServiceCategory category:
                    if (entry.State != EntityState.Added) throw Denied();
                    var owningService = Services.Local.FirstOrDefault(s => s.Id == category.ServiceId && Entry(s).State == EntityState.Added) ??
                        await Services.AsNoTracking().SingleOrDefaultAsync(s => s.Id == category.ServiceId, cancellationToken);
                    if (owningService is null) throw Denied();
                    AssertTenant(owningService.TenantId);
                    break;
                case BusinessCalendar calendar:
                    AssertTenant(calendar.TenantId);
                    // No calendar-edit use case is exposed yet; changed configurations create records.
                    if (entry.State != EntityState.Added) throw SlaImmutable();
                    break;
                case SlaVersion slaVersion:
                    if (entry.State != EntityState.Added) throw SlaImmutable();
                    await ValidateSlaReferencesAsync(slaVersion, cancellationToken);
                    break;
                case WorkflowDefinition workflow:
                    AssertTenant(workflow.TenantId);
                    break;
                case WorkflowVersion version:
                    await ValidateWorkflowOwnerAsync(version.WorkflowId, cancellationToken);
                    if (version.Status != WorkflowVersionStatus.Draft || version.PublishedAt is not null ||
                        (entry.State != EntityState.Added && !await WorkflowVersions.AsNoTracking().AnyAsync(v => v.Id == version.Id && v.Status == WorkflowVersionStatus.Draft, cancellationToken)))
                        throw WorkflowImmutable();
                    break;
                case WorkflowStep step:
                    await ValidateDraftWorkflowVersionAsync(step.WorkflowVersionId, cancellationToken);
                    break;
                case WorkflowTransition transition:
                    await ValidateDraftWorkflowVersionAsync(transition.WorkflowVersionId, cancellationToken);
                    break;
                case ManagementScope scope:
                    AssertTenant(scope.TenantId);
                    if (entry.State != EntityState.Added || scope.CreatedBy != CurrentUserId ||
                        !await Users.AnyAsync(u => u.Id == scope.UserId, cancellationToken) ||
                        !await Users.AnyAsync(u => u.Id == scope.CreatedBy, cancellationToken) ||
                        !await Departments.AnyAsync(d => d.Id == scope.DepartmentId, cancellationToken))
                        throw Denied();
                    break;
                case UserAccount user when user.TenantId is { } tenantId:
                    AssertTenant(tenantId);
                    break;
                case UserAccount:
                case Company:
                case Tenant:
                case Permission:
                    await AssertPlatformAccount();
                    break;
                case Role role when !role.IsSystem && role.TenantId is { } tenantId:
                    AssertTenant(tenantId);
                    break;
                case Role:
                    // System-role definitions are migration-owned reference data.
                    throw Denied();
                case UserRole link:
                    await ValidateUserRoleAsync(link, cancellationToken);
                    break;
                case RolePermission link:
                    await ValidateRolePermissionAsync(link, cancellationToken);
                    break;
                case AuthenticationSession session:
                    if (CurrentUserId is null || session.UserId != CurrentUserId) throw Denied();
                    break;
                case AIInteraction interaction:
                    AssertTenant(interaction.TenantId);
                    if (entry.State != EntityState.Added || interaction.UserId != CurrentUserId) throw Denied();
                    break;
                case AIRecommendation recommendation:
                    AssertTenant(recommendation.TenantId);
                    if (entry.State == EntityState.Added)
                    {
                        if (!await AIInteractions.AnyAsync(i => i.Id == recommendation.AIInteractionId, cancellationToken)) throw Denied();
                    }
                    else if (entry.State == EntityState.Modified)
                    {
                        if (entry.Properties.Any(p => p.IsModified && p.Metadata.Name is not (nameof(AIRecommendation.HumanDecision) or nameof(AIRecommendation.DecisionBy) or nameof(AIRecommendation.DecidedAt))))
                            throw Denied();
                        if (recommendation.DecisionBy != CurrentUserId) throw Denied();
                    }
                    else throw Denied();
                    break;
                case AIAgentAction action:
                    AssertTenant(action.TenantId);
                    if (entry.State != EntityState.Added ||
                        !await AIInteractions.AnyAsync(i => i.Id == action.AIInteractionId, cancellationToken)) throw Denied();
                    break;
                default:
                    // New entities must explicitly declare their persistence boundary.
                    throw new InvalidOperationException($"No persistence boundary registered for {entry.Metadata.ClrType.Name}.");
            }
        }
        return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
    }

    private void AssertTenant(Guid tenantId)
    {
        if (CurrentUserId is null || CurrentUserId == Guid.Empty || tenantId == Guid.Empty || tenantId != CurrentTenantId)
            throw Denied();
    }

    private async Task ValidateWorkflowOwnerAsync(Guid workflowId, CancellationToken cancellationToken)
    {
        var root = Workflows.Local.FirstOrDefault(w => w.Id == workflowId && Entry(w).State == EntityState.Added) ??
            await Workflows.AsNoTracking().SingleOrDefaultAsync(w => w.Id == workflowId, cancellationToken);
        if (root is null) throw Denied();
        AssertTenant(root.TenantId);
    }

    private async Task ValidateSlaReferencesAsync(SlaVersion version, CancellationToken cancellationToken)
    {
        var profile = SlaProfiles.Local.FirstOrDefault(p => p.Id == version.SlaProfileId && Entry(p).State == EntityState.Added) ??
            await SlaProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.Id == version.SlaProfileId, cancellationToken);
        var calendar = BusinessCalendars.Local.FirstOrDefault(c => c.Id == version.CalendarId && Entry(c).State == EntityState.Added) ??
            await BusinessCalendars.AsNoTracking().SingleOrDefaultAsync(c => c.Id == version.CalendarId, cancellationToken);
        if (profile is null || calendar is null || profile.TenantId != calendar.TenantId) throw Denied();
        AssertTenant(profile.TenantId);
        if (!calendar.HasWorkingTime()) throw new ApplicationFault(FaultKind.Validation, "SLA.INVALID_CALENDAR", "An SLA version requires a calendar with working time.");
        var recipients = version.EscalationRecipientIds();
        if (recipients.Count != 0 && await Users.CountAsync(u => recipients.Contains(u.Id) && u.Status == UserStatus.Active, cancellationToken) != recipients.Count)
            throw new ApplicationFault(FaultKind.Validation, "SLA.INVALID_ESCALATION_TARGET", "Escalation recipients must be active users in this tenant.");
    }

    private static ApplicationFault SlaImmutable() => new(FaultKind.Conflict, "SLA.SNAPSHOT_IMMUTABLE", "Saved SLA snapshots cannot be changed; create a new version and calendar configuration.");

    private async Task ValidateDraftWorkflowVersionAsync(Guid versionId, CancellationToken cancellationToken)
    {
        // Fresh persisted status prevents tracked/detached DRAFT snapshots bypassing publication.
        var version = await WorkflowVersions.AsNoTracking().SingleOrDefaultAsync(v => v.Id == versionId, cancellationToken)
            ?? WorkflowVersions.Local.FirstOrDefault(v => v.Id == versionId && Entry(v).State == EntityState.Added);
        if (version is null) throw Denied();
        await ValidateWorkflowOwnerAsync(version.WorkflowId, cancellationToken);
        if (version.Status != WorkflowVersionStatus.Draft || version.PublishedAt is not null) throw WorkflowImmutable();
    }

    private static ApplicationFault WorkflowImmutable() => new(FaultKind.Conflict, "WORKFLOW.VERSION_IMMUTABLE", "Only draft workflow configuration can be changed; published versions require a new version.");

    private async Task ValidateUserRoleAsync(UserRole link, CancellationToken cancellationToken)
    {
        var user = Users.Local.FirstOrDefault(x => x.Id == link.UserId) ??
            await Users.SingleOrDefaultAsync(x => x.Id == link.UserId, cancellationToken);
        var role = Roles.Local.FirstOrDefault(x => x.Id == link.RoleId) ??
            await Roles.SingleOrDefaultAsync(x => x.Id == link.RoleId, cancellationToken);
        if (user?.TenantId is not { } tenantId || role is null) throw Denied();
        AssertTenant(tenantId);
        if ((!role.IsSystem && role.TenantId != tenantId) || role.Status != RecordStatus.Active ||
            await RolePermissions.IgnoreQueryFilters().Join(Permissions.IgnoreQueryFilters(),
                rp => rp.PermissionId, p => p.Id, (rp, p) => new { rp.RoleId, p.ScopeType })
                .AnyAsync(x => x.RoleId == role.Id && x.ScopeType == PermissionScope.Platform, cancellationToken))
            throw Denied();
    }

    private async Task ValidateRolePermissionAsync(RolePermission link, CancellationToken cancellationToken)
    {
        var role = Roles.Local.FirstOrDefault(x => x.Id == link.RoleId) ??
            await Roles.SingleOrDefaultAsync(x => x.Id == link.RoleId, cancellationToken);
        var permission = await Permissions.SingleOrDefaultAsync(x => x.Id == link.PermissionId, cancellationToken);
        if (role is null || role.IsSystem || role.TenantId is not { } tenantId || permission is null || permission.ScopeType == PermissionScope.Platform)
            throw Denied();
        AssertTenant(tenantId);
    }

    private static ApplicationFault Denied() => new(FaultKind.Forbidden, "PERSISTENCE.SCOPE_DENIED", "This change is outside the authorized persistence scope.");
}
