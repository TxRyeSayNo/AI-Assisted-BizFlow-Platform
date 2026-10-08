using System.Text.Json;
using BizFlow.Domain.Common;
using BizFlow.Domain.Authentication;
using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Sla;
using BizFlow.Domain.Services;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Requests;

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

    public static AuditLog TaskResultSubmitted(WorkTask task, TaskAssignment assignment, TaskResult result, string? keyHash, string? fingerprint)
    {
        if (task.Status != TaskState.Submitted || task.WorkflowMutation is not { } mutation ||
            mutation.Before != TaskState.InProgress || assignment.TaskId != task.Id ||
            assignment.UserId != mutation.ActorId || result.TaskId != task.Id || result.AuthorId != mutation.ActorId)
            throw new ArgumentException("Result submission audit requires the authorized performer and workflow mutation.");
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((keyHash is not null || fingerprint is not null) && (!Hash(keyHash) || !Hash(fingerprint))) throw new ArgumentException("Invalid replay hashes.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = task.TenantId, ActorType = AuditActorType.User, ActorId = mutation.ActorId,
            ObjectType = "Task", ObjectId = task.Id, Action = "TASK.RESULT_SUBMITTED", CreatedAt = task.UpdatedAt,
            BeforeJson = JsonSerializer.Serialize(new { status = "IN_PROGRESS" }),
            AfterJson = JsonSerializer.Serialize(new { taskId = task.Id, assignmentId = assignment.Id, taskResultId = result.Id, revisionNo = result.RevisionNo, status = "SUBMITTED", submittedAt = result.SubmittedAt }),
            MetadataJson = keyHash is null ? null : JsonSerializer.Serialize(new { idempotency = new { operation = "TASK.RESULT_SUBMIT", keyHash, fingerprint } })
        };
    }

    public static AuditLog TaskResultConfirmed(WorkTask task, TaskAssignment assignment, Confirmation confirmation, string? keyHash, string? fingerprint)
    {
        if (task.Status is not (TaskState.Confirmed or TaskState.Completed) || task.WorkflowMutation is not { } mutation ||
            confirmation.ObjectId != task.Id || confirmation.ActorId != mutation.ActorId)
            throw new ArgumentException("Result confirmation audit requires matching workflow and confirmation evidence.");
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((keyHash is not null || fingerprint is not null) && (!Hash(keyHash) || !Hash(fingerprint))) throw new ArgumentException("Invalid replay hashes.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = task.TenantId, ActorType = AuditActorType.User, ActorId = mutation.ActorId,
            ObjectType = "Task", ObjectId = task.Id, Action = "TASK.RESULT_CONFIRMED", CreatedAt = confirmation.ConfirmedAt,
            BeforeJson = JsonSerializer.Serialize(new { status = "SUBMITTED" }),
            AfterJson = JsonSerializer.Serialize(new { taskId = task.Id, assignmentId = assignment.Id, confirmationId = confirmation.Id, status = task.Status.ToString().ToUpperInvariant(), confirmedAt = confirmation.ConfirmedAt, note = confirmation.Note }),
            MetadataJson = keyHash is null ? null : JsonSerializer.Serialize(new { idempotency = new { operation = "TASK.RESULT_CONFIRM", keyHash, fingerprint } })
        };
    }

    public static AuditLog TaskResultRework(WorkTask task, TaskAssignment assignment, Confirmation confirmation, string? keyHash, string? fingerprint)
    {
        if (task.Status != TaskState.InProgress || task.WorkflowMutation is not { } mutation ||
            mutation.Before != TaskState.Submitted || confirmation.ObjectId != task.Id || confirmation.ActorId != mutation.ActorId)
            throw new ArgumentException("Result rework audit requires matching workflow and confirmation evidence.");
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((keyHash is not null || fingerprint is not null) && (!Hash(keyHash) || !Hash(fingerprint))) throw new ArgumentException("Invalid replay hashes.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = task.TenantId, ActorType = AuditActorType.User, ActorId = mutation.ActorId,
            ObjectType = "Task", ObjectId = task.Id, Action = "TASK.RESULT_REWORK", CreatedAt = confirmation.ConfirmedAt,
            BeforeJson = JsonSerializer.Serialize(new { status = "SUBMITTED" }),
            AfterJson = JsonSerializer.Serialize(new { taskId = task.Id, assignmentId = assignment.Id, confirmationId = confirmation.Id, status = "IN_PROGRESS", reworkedAt = confirmation.ConfirmedAt, note = confirmation.Note }),
            MetadataJson = keyHash is null ? null : JsonSerializer.Serialize(new { idempotency = new { operation = "TASK.RESULT_REWORK", keyHash, fingerprint } })
        };
    }

    public static AuditLog TaskStarted(WorkTask task, TaskAssignment assignment, string? keyHash, string? fingerprint)
    {
        if (task.Status != TaskState.InProgress || task.WorkflowMutation is not { } mutation ||
            mutation.Before is not (TaskState.Accepted or TaskState.Overdue) || assignment.TaskId != task.Id ||
            assignment.UserId != mutation.ActorId || assignment.AcceptedAt is null || assignment.RejectedAt is not null || assignment.EndedAt is not null)
            throw new ArgumentException("Execution audit requires the authorized performer and workflow mutation.");
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((keyHash is not null || fingerprint is not null) && (!Hash(keyHash) || !Hash(fingerprint))) throw new ArgumentException("Invalid replay hashes.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = task.TenantId, ActorType = AuditActorType.User, ActorId = mutation.ActorId,
            ObjectType = "Task", ObjectId = task.Id, Action = "TASK.STARTED", CreatedAt = task.UpdatedAt,
            BeforeJson = JsonSerializer.Serialize(new { status = mutation.Before.ToString().ToUpperInvariant() }),
            AfterJson = JsonSerializer.Serialize(new { taskId = task.Id, assignmentId = assignment.Id, status = "IN_PROGRESS", startedAt = task.UpdatedAt }),
            MetadataJson = keyHash is null ? null : JsonSerializer.Serialize(new { idempotency = new { operation = "TASK.START", keyHash, fingerprint } })
        };
    }

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

    public static AuditLog RequestCreated(WorkRequest request, DateTimeOffset now,
        string? idempotencyKeyHash = null, string? requestFingerprint = null)
    {
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((idempotencyKeyHash is not null || requestFingerprint is not null) &&
            (!Hash(idempotencyKeyHash) || !Hash(requestFingerprint)))
            throw new ArgumentException("Replay metadata requires two SHA-256 hashes.");
        if (request.Status is not (RequestState.Draft or RequestState.Submitted))
            throw new ArgumentException("Request creation audit requires a draft or initially submitted request.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = request.TenantId, ActorType = AuditActorType.User,
            ActorId = request.RequesterId, ObjectType = "Request", ObjectId = request.Id, Action = "REQUEST.CREATED",
            MetadataJson = idempotencyKeyHash is null ? null : JsonSerializer.Serialize(new
            { idempotency = new { operation = "REQUEST.CREATE", keyHash = idempotencyKeyHash, fingerprint = requestFingerprint } }),
            AfterJson = JsonSerializer.Serialize(new
            {
                requestId = request.Id, requesterId = request.RequesterId, serviceId = request.ServiceId,
                categoryId = request.CategoryId, parentRequestId = request.ParentRequestId,
                revisedFromRequestId = request.RevisedFromRequestId, title = request.Title,
                description = request.Description, priority = request.Priority.ToString().ToUpperInvariant(),
                status = request.Status.ToString().ToUpperInvariant(), createdAt = request.CreatedAt
            }),
            CreatedAt = now.ToUniversalTime()
        };
    }

    public static AuditLog RequestSubmitted(WorkRequest request, DateTimeOffset now,
        string? idempotencyKeyHash = null, string? requestFingerprint = null)
    {
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((idempotencyKeyHash is not null || requestFingerprint is not null) &&
            (!Hash(idempotencyKeyHash) || !Hash(requestFingerprint)))
            throw new ArgumentException("Replay metadata requires two SHA-256 hashes.");
        if (request.Status != RequestState.Submitted || request.WorkflowMutation is not { } mutation ||
            mutation.Before != RequestState.Draft || mutation.ActorId != request.RequesterId)
            throw new ArgumentException("Request submission audit requires an active workflow mutation from Draft to Submitted by the requester.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = request.TenantId, ActorType = AuditActorType.User,
            ActorId = mutation.ActorId, ObjectType = "Request", ObjectId = request.Id, Action = "REQUEST.SUBMITTED",
            MetadataJson = idempotencyKeyHash is null ? null : JsonSerializer.Serialize(new
            { idempotency = new { operation = "REQUEST.SUBMIT", keyHash = idempotencyKeyHash, fingerprint = requestFingerprint } }),
            BeforeJson = JsonSerializer.Serialize(new { status = "DRAFT" }),
            AfterJson = JsonSerializer.Serialize(new
            {
                requestId = request.Id, requesterId = request.RequesterId,
                status = "SUBMITTED", submittedAt = request.UpdatedAt
            }),
            CreatedAt = now.ToUniversalTime()
        };
    }

    public static AuditLog RequestRouted(WorkRequest request, RequestRouting routing, DateTimeOffset now,
        string? idempotencyKeyHash = null, string? requestFingerprint = null)
    {
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((idempotencyKeyHash is not null || requestFingerprint is not null) &&
            (!Hash(idempotencyKeyHash) || !Hash(requestFingerprint)))
            throw new ArgumentException("Replay metadata requires two SHA-256 hashes.");
        if (request.Status != RequestState.Routed || request.WorkflowMutation is not { } mutation ||
            mutation.Before is not (RequestState.Submitted or RequestState.Routed))
            throw new ArgumentException("Request routing audit requires an active workflow mutation to Routed.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = request.TenantId, ActorType = AuditActorType.User,
            ActorId = mutation.ActorId, ObjectType = "Request", ObjectId = request.Id, Action = "REQUEST.ROUTED",
            MetadataJson = idempotencyKeyHash is null ? null : JsonSerializer.Serialize(new
            { idempotency = new { operation = "REQUEST.ROUTE", keyHash = idempotencyKeyHash, fingerprint = requestFingerprint } }),
            BeforeJson = JsonSerializer.Serialize(new { status = mutation.Before.ToString().ToUpperInvariant() }),
            AfterJson = JsonSerializer.Serialize(new
            {
                requestId = request.Id, routingId = routing.Id, toDepartmentId = routing.ToDepartmentId,
                toUserId = routing.ToUserId, routedBy = routing.RoutedBy, routedAt = routing.RoutedAt,
                reason = routing.Reason, source = routing.Source.ToString().ToUpperInvariant(),
                status = "ROUTED"
            }),
            CreatedAt = now.ToUniversalTime()
        };
    }

    public static AuditLog RequestReceived(WorkRequest request, Confirmation confirmation, DateTimeOffset now,
        string? idempotencyKeyHash = null, string? requestFingerprint = null)
    {
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((idempotencyKeyHash is not null || requestFingerprint is not null) &&
            (!Hash(idempotencyKeyHash) || !Hash(requestFingerprint)))
            throw new ArgumentException("Replay metadata requires two SHA-256 hashes.");
        if (request.Status != RequestState.Received || request.WorkflowMutation is not { } mutation ||
            mutation.Before != RequestState.Routed)
            throw new ArgumentException("Request receipt audit requires an active workflow mutation to Received.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = request.TenantId, ActorType = AuditActorType.User,
            ActorId = mutation.ActorId, ObjectType = "Request", ObjectId = request.Id, Action = "REQUEST.RECEIVED",
            MetadataJson = idempotencyKeyHash is null ? null : JsonSerializer.Serialize(new
            { idempotency = new { operation = "REQUEST.RECEIVE", keyHash = idempotencyKeyHash, fingerprint = requestFingerprint } }),
            BeforeJson = JsonSerializer.Serialize(new { status = "ROUTED" }),
            AfterJson = JsonSerializer.Serialize(new
            {
                requestId = request.Id, confirmationId = confirmation.Id, receivedBy = confirmation.ActorId,
                receivedAt = confirmation.ConfirmedAt, note = confirmation.Note, status = "RECEIVED"
            }),
            CreatedAt = now.ToUniversalTime()
        };
    }

    public static AuditLog RequestExecutionStarted(WorkRequest request, DateTimeOffset now,
        string? idempotencyKeyHash = null, string? requestFingerprint = null)
    {
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((idempotencyKeyHash is not null || requestFingerprint is not null) &&
            (!Hash(idempotencyKeyHash) || !Hash(requestFingerprint)))
            throw new ArgumentException("Replay metadata requires two SHA-256 hashes.");
        if (request.Status != RequestState.InProgress || request.WorkflowMutation is not { } mutation ||
            mutation.Before is not (RequestState.Received or RequestState.WaitingForInformation or RequestState.Overdue or RequestState.Resolved))
            throw new ArgumentException("Request execution audit requires an active workflow mutation to InProgress.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = request.TenantId, ActorType = AuditActorType.User,
            ActorId = mutation.ActorId, ObjectType = "Request", ObjectId = request.Id, Action = "REQUEST.STARTED",
            MetadataJson = idempotencyKeyHash is null ? null : JsonSerializer.Serialize(new
            { idempotency = new { operation = "REQUEST.START", keyHash = idempotencyKeyHash, fingerprint = requestFingerprint } }),
            BeforeJson = JsonSerializer.Serialize(new { status = mutation.Before.ToString().ToUpperInvariant() }),
            AfterJson = JsonSerializer.Serialize(new
            {
                requestId = request.Id, startedBy = mutation.ActorId, status = "IN_PROGRESS", startedAt = request.UpdatedAt
            }),
            CreatedAt = now.ToUniversalTime()
        };
    }

    public static AuditLog RequestRejected(WorkRequest request, string reason, DateTimeOffset now,
        string? idempotencyKeyHash = null, string? requestFingerprint = null)
    {
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((idempotencyKeyHash is not null || requestFingerprint is not null) &&
            (!Hash(idempotencyKeyHash) || !Hash(requestFingerprint)))
            throw new ArgumentException("Replay metadata requires two SHA-256 hashes.");
        if (request.Status != RequestState.Rejected || request.WorkflowMutation is not { } mutation ||
            mutation.Before is not (RequestState.Submitted or RequestState.Routed))
            throw new ArgumentException("Request rejection audit requires an active workflow mutation to Rejected.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = request.TenantId, ActorType = AuditActorType.User,
            ActorId = mutation.ActorId, ObjectType = "Request", ObjectId = request.Id, Action = "REQUEST.REJECTED",
            MetadataJson = idempotencyKeyHash is null ? null : JsonSerializer.Serialize(new
            { idempotency = new { operation = "REQUEST.REJECT", keyHash = idempotencyKeyHash, fingerprint = requestFingerprint } }),
            BeforeJson = JsonSerializer.Serialize(new { status = mutation.Before.ToString().ToUpperInvariant() }),
            AfterJson = JsonSerializer.Serialize(new
            {
                requestId = request.Id, rejectedBy = mutation.ActorId, reason, status = "REJECTED", rejectedAt = request.UpdatedAt
            }),
            CreatedAt = now.ToUniversalTime()
        };
    }

    public static AuditLog RequestCancelled(WorkRequest request, DateTimeOffset now,
        string? idempotencyKeyHash = null, string? requestFingerprint = null)
    {
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((idempotencyKeyHash is not null || requestFingerprint is not null) &&
            (!Hash(idempotencyKeyHash) || !Hash(requestFingerprint)))
            throw new ArgumentException("Replay metadata requires two SHA-256 hashes.");
        if (request.Status != RequestState.Cancelled || request.WorkflowMutation is not { } mutation)
            throw new ArgumentException("Request cancellation audit requires an active workflow mutation to Cancelled.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = request.TenantId, ActorType = AuditActorType.User,
            ActorId = mutation.ActorId, ObjectType = "Request", ObjectId = request.Id, Action = "REQUEST.CANCELLED",
            MetadataJson = idempotencyKeyHash is null ? null : JsonSerializer.Serialize(new
            { idempotency = new { operation = "REQUEST.CANCEL", keyHash = idempotencyKeyHash, fingerprint = requestFingerprint } }),
            BeforeJson = JsonSerializer.Serialize(new { status = mutation.Before.ToString().ToUpperInvariant() }),
            AfterJson = JsonSerializer.Serialize(new
            {
                requestId = request.Id, cancelledBy = mutation.ActorId, status = "CANCELLED", cancelledAt = request.UpdatedAt
            }),
            CreatedAt = now.ToUniversalTime()
        };
    }

    public static AuditLog RequestResolved(WorkRequest request, RequestResolution resolution, DateTimeOffset now,
        string? idempotencyKeyHash = null, string? requestFingerprint = null)
    {
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((idempotencyKeyHash is not null || requestFingerprint is not null) &&
            (!Hash(idempotencyKeyHash) || !Hash(requestFingerprint)))
            throw new ArgumentException("Replay metadata requires two SHA-256 hashes.");
        if (request.Status != RequestState.Resolved || request.WorkflowMutation is not { } mutation ||
            mutation.Before != RequestState.InProgress)
            throw new ArgumentException("Request resolution audit requires an active workflow mutation to Resolved.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = request.TenantId, ActorType = AuditActorType.User,
            ActorId = mutation.ActorId, ObjectType = "Request", ObjectId = request.Id, Action = "REQUEST.RESOLVED",
            MetadataJson = idempotencyKeyHash is null ? null : JsonSerializer.Serialize(new
            { idempotency = new { operation = "REQUEST.RESOLVE", keyHash = idempotencyKeyHash, fingerprint = requestFingerprint } }),
            BeforeJson = JsonSerializer.Serialize(new { status = "IN_PROGRESS" }),
            AfterJson = JsonSerializer.Serialize(new
            {
                requestId = request.Id, resolutionId = resolution.Id, resolverId = resolution.ResolverId,
                revisionNo = resolution.RevisionNo, content = resolution.Content, status = "RESOLVED", resolvedAt = request.ResolvedAt
            }),
            CreatedAt = now.ToUniversalTime()
        };
    }

    public static AuditLog RequestConfirmed(WorkRequest request, Confirmation confirmation, DateTimeOffset now,
        string? idempotencyKeyHash = null, string? requestFingerprint = null)
    {
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((idempotencyKeyHash is not null || requestFingerprint is not null) &&
            (!Hash(idempotencyKeyHash) || !Hash(requestFingerprint)))
            throw new ArgumentException("Replay metadata requires two SHA-256 hashes.");
        if (request.Status is not (RequestState.Confirmed or RequestState.Closed) || request.WorkflowMutation is not { } mutation)
            throw new ArgumentException("Request confirmation audit requires an active workflow mutation.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = request.TenantId, ActorType = AuditActorType.User,
            ActorId = mutation.ActorId, ObjectType = "Request", ObjectId = request.Id, Action = "REQUEST.CONFIRMED",
            MetadataJson = idempotencyKeyHash is null ? null : JsonSerializer.Serialize(new
            { idempotency = new { operation = "REQUEST.CONFIRM", keyHash = idempotencyKeyHash, fingerprint = requestFingerprint } }),
            BeforeJson = JsonSerializer.Serialize(new { status = "RESOLVED" }),
            AfterJson = JsonSerializer.Serialize(new
            {
                requestId = request.Id, confirmationId = confirmation.Id, confirmedBy = confirmation.ActorId,
                status = request.Status.ToString().ToUpperInvariant(), note = confirmation.Note, confirmedAt = confirmation.ConfirmedAt
            }),
            CreatedAt = now.ToUniversalTime()
        };
    }

    public static AuditLog RequestRework(WorkRequest request, Confirmation confirmation, DateTimeOffset now,
        string? idempotencyKeyHash = null, string? requestFingerprint = null)
    {
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((idempotencyKeyHash is not null || requestFingerprint is not null) &&
            (!Hash(idempotencyKeyHash) || !Hash(requestFingerprint)))
            throw new ArgumentException("Replay metadata requires two SHA-256 hashes.");
        if (request.Status != RequestState.InProgress || request.WorkflowMutation is not { } mutation ||
            mutation.Before != RequestState.Resolved)
            throw new ArgumentException("Request rework audit requires an active workflow mutation to InProgress.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = request.TenantId, ActorType = AuditActorType.User,
            ActorId = mutation.ActorId, ObjectType = "Request", ObjectId = request.Id, Action = "REQUEST.REWORK",
            MetadataJson = idempotencyKeyHash is null ? null : JsonSerializer.Serialize(new
            { idempotency = new { operation = "REQUEST.REWORK", keyHash = idempotencyKeyHash, fingerprint = requestFingerprint } }),
            BeforeJson = JsonSerializer.Serialize(new { status = "RESOLVED" }),
            AfterJson = JsonSerializer.Serialize(new
            {
                requestId = request.Id, confirmationId = confirmation.Id, requestedBy = confirmation.ActorId,
                reason = confirmation.Note, status = "IN_PROGRESS", requestedAt = confirmation.ConfirmedAt
            }),
            CreatedAt = now.ToUniversalTime()
        };
    }

    public static AuditLog RequestClosed(WorkRequest request, DateTimeOffset now)
    {
        if (request.Status != RequestState.Closed || request.WorkflowMutation is not { } mutation)
            throw new ArgumentException("Request closure audit requires an active workflow mutation to Closed.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = request.TenantId, ActorType = AuditActorType.User,
            ActorId = mutation.ActorId, ObjectType = "Request", ObjectId = request.Id, Action = "REQUEST.CLOSED",
            BeforeJson = JsonSerializer.Serialize(new { status = mutation.Before.ToString().ToUpperInvariant() }),
            AfterJson = JsonSerializer.Serialize(new
            {
                requestId = request.Id, closedBy = mutation.ActorId, status = "CLOSED", closedAt = request.ClosedAt
            }),
            CreatedAt = now.ToUniversalTime()
        };
    }

    public static AuditLog RequestRevised(WorkRequest source, WorkRequest revision, DateTimeOffset now,
        string? idempotencyKeyHash = null, string? requestFingerprint = null)
    {
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((idempotencyKeyHash is not null || requestFingerprint is not null) &&
            (!Hash(idempotencyKeyHash) || !Hash(requestFingerprint)))
            throw new ArgumentException("Replay metadata requires two SHA-256 hashes.");
        return new()
        {
            Id = Guid.CreateVersion7(), TenantId = revision.TenantId, ActorType = AuditActorType.User,
            ActorId = revision.RequesterId, ObjectType = "Request", ObjectId = revision.Id, Action = "REQUEST.REVISED",
            MetadataJson = idempotencyKeyHash is null ? null : JsonSerializer.Serialize(new
            { idempotency = new { operation = "REQUEST.REVISE", keyHash = idempotencyKeyHash, fingerprint = requestFingerprint } }),
            BeforeJson = JsonSerializer.Serialize(new { sourceRequestId = source.Id, status = "REJECTED" }),
            AfterJson = JsonSerializer.Serialize(new
            {
                requestId = revision.Id, sourceRequestId = source.Id, requesterId = revision.RequesterId,
                title = revision.Title, status = revision.Status.ToString().ToUpperInvariant(), createdAt = revision.CreatedAt
            }),
            CreatedAt = now.ToUniversalTime()
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

    public static AuditLog CommentCreated(Comment comment, DateTimeOffset now,
        string? idempotencyKeyHash = null, string? requestFingerprint = null)
    {
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((idempotencyKeyHash is not null || requestFingerprint is not null) &&
            (!Hash(idempotencyKeyHash) || !Hash(requestFingerprint)))
            throw new ArgumentException("Replay metadata requires two SHA-256 hashes.");

        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = comment.TenantId,
            ActorType = AuditActorType.User,
            ActorId = comment.AuthorId,
            ObjectType = "Comment",
            ObjectId = comment.Id,
            Action = "COMMENT.CREATED",
            MetadataJson = idempotencyKeyHash is null ? null : JsonSerializer.Serialize(new
            {
                idempotency = new { operation = "COMMENT.CREATE", keyHash = idempotencyKeyHash, fingerprint = requestFingerprint }
            }),
            AfterJson = JsonSerializer.Serialize(new
            {
                commentId = comment.Id,
                objectType = comment.ObjectType.ToString().ToUpperInvariant(),
                objectId = comment.ObjectId,
                authorId = comment.AuthorId,
                content = comment.Content,
                createdAt = comment.CreatedAt
            }),
            CreatedAt = now.ToUniversalTime()
        };
    }

    public static AuditLog CommentEdited(Comment comment, string previousContent, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = comment.TenantId,
        ActorType = AuditActorType.User,
        ActorId = comment.AuthorId,
        ObjectType = "Comment",
        ObjectId = comment.Id,
        Action = "COMMENT.EDITED",
        BeforeJson = JsonSerializer.Serialize(new { content = previousContent }),
        AfterJson = JsonSerializer.Serialize(new
        {
            commentId = comment.Id,
            content = comment.Content,
            editedAt = comment.EditedAt
        }),
        CreatedAt = now.ToUniversalTime()
    };

    public static AuditLog CommentDeleted(Comment comment, Guid actorId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = comment.TenantId,
        ActorType = AuditActorType.User,
        ActorId = actorId,
        ObjectType = "Comment",
        ObjectId = comment.Id,
        Action = "COMMENT.DELETED",
        BeforeJson = JsonSerializer.Serialize(new { deletedAt = (DateTimeOffset?)null }),
        AfterJson = JsonSerializer.Serialize(new
        {
            commentId = comment.Id,
            deletedAt = comment.DeletedAt
        }),
        CreatedAt = now.ToUniversalTime()
    };

    public static AuditLog AttachmentUploadSessionCreated(
        Attachment attachment,
        DateTimeOffset now,
        string? idempotencyKeyHash = null,
        string? requestFingerprint = null)
    {
        static bool Hash(string? value) => value is { Length: 64 } && value.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');
        if ((idempotencyKeyHash is not null || requestFingerprint is not null) &&
            (!Hash(idempotencyKeyHash) || !Hash(requestFingerprint)))
            throw new ArgumentException("Replay metadata requires two SHA-256 hashes.");

        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = attachment.TenantId,
            ActorType = AuditActorType.User,
            ActorId = attachment.UploadedBy,
            ObjectType = "Attachment",
            ObjectId = attachment.Id,
            Action = "ATTACHMENT.UPLOAD_SESSION_CREATED",
            MetadataJson = idempotencyKeyHash is null ? null : JsonSerializer.Serialize(new
            {
                idempotency = new { operation = "ATTACHMENT.CREATE_UPLOAD_SESSION", keyHash = idempotencyKeyHash, fingerprint = requestFingerprint }
            }),
            AfterJson = JsonSerializer.Serialize(new
            {
                attachmentId = attachment.Id,
                objectType = attachment.ObjectType.ToString().ToUpperInvariant(),
                objectId = attachment.ObjectId,
                uploadedBy = attachment.UploadedBy,
                fileName = attachment.FileName,
                contentType = attachment.ContentType,
                sizeBytes = attachment.SizeBytes,
                objectKey = attachment.ObjectKey,
                status = attachment.Status.ToString().ToUpperInvariant(),
                createdAt = attachment.CreatedAt
            }),
            CreatedAt = now.ToUniversalTime()
        };
    }

    public static AuditLog AttachmentFinalized(Attachment attachment, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = attachment.TenantId,
        ActorType = AuditActorType.User,
        ActorId = attachment.UploadedBy,
        ObjectType = "Attachment",
        ObjectId = attachment.Id,
        Action = "ATTACHMENT.FINALIZED",
        BeforeJson = JsonSerializer.Serialize(new { status = "UPLOADING" }),
        AfterJson = JsonSerializer.Serialize(new
        {
            attachmentId = attachment.Id,
            status = attachment.Status.ToString().ToUpperInvariant(),
            hash = attachment.Hash,
            sizeBytes = attachment.SizeBytes
        }),
        CreatedAt = now.ToUniversalTime()
    };

    public static AuditLog AttachmentDeleted(Attachment attachment, Guid actorId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(),
        TenantId = attachment.TenantId,
        ActorType = AuditActorType.User,
        ActorId = actorId,
        ObjectType = "Attachment",
        ObjectId = attachment.Id,
        Action = "ATTACHMENT.DELETED",
        BeforeJson = JsonSerializer.Serialize(new { status = "READY", deletedAt = (DateTimeOffset?)null }),
        AfterJson = JsonSerializer.Serialize(new
        {
            attachmentId = attachment.Id,
            status = attachment.Status.ToString().ToUpperInvariant(),
            deletedAt = attachment.DeletedAt
        }),
        CreatedAt = now.ToUniversalTime()
    };

    public static AuditLog RecordArchived(string recordType, Guid recordId, Guid tenantId, Guid actorId, string status, DateTimeOffset deletedAt, DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(recordType)) throw new ArgumentException("Record type is required.", nameof(recordType));
        if (recordId == Guid.Empty) throw new ArgumentException("Record ID is required.", nameof(recordId));
        if (tenantId == Guid.Empty) throw new ArgumentException("Tenant ID is required.", nameof(tenantId));
        if (actorId == Guid.Empty) throw new ArgumentException("Actor ID is required.", nameof(actorId));

        var normalizedType = recordType.Trim().ToUpperInvariant();
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            ActorType = AuditActorType.User,
            ActorId = actorId,
            ObjectType = normalizedType switch
            {
                "TASK" => "Task",
                "REQUEST" => "Request",
                _ => recordType.Trim()
            },
            ObjectId = recordId,
            Action = "RECORD.ARCHIVED",
            BeforeJson = JsonSerializer.Serialize(new { status, deletedAt = (DateTimeOffset?)null }),
            AfterJson = JsonSerializer.Serialize(new
            {
                recordId,
                recordType = normalizedType,
                status,
                deletedAt
            }),
            CreatedAt = now.ToUniversalTime()
        };
    }

    public static AuditLog RecordArchived(WorkTask task, Guid actorId, DateTimeOffset now) =>
        RecordArchived("TASK", task.Id, task.TenantId, actorId, task.Status.ToString().ToUpperInvariant(), task.DeletedAt ?? now, now);

    public static AuditLog RecordArchived(WorkRequest request, Guid actorId, DateTimeOffset now) =>
        RecordArchived("REQUEST", request.Id, request.TenantId, actorId, request.Status.ToString().ToUpperInvariant(), request.DeletedAt ?? now, now);
}
