using BizFlow.Domain.Common;

namespace BizFlow.Domain.Tasks;

// SSS Confirmation: polymorphic ownership is validated by the owning Application service
// and persistence guard. This factory exposes only the implemented Task receipt milestone.
public sealed class Confirmation
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string ObjectType { get; private set; } = "TASK";
    public Guid ObjectId { get; private set; }
    public string MilestoneType { get; private set; } = "RECEIVE";
    public Guid ActorId { get; private set; }
    public string Decision { get; private set; } = "CONFIRMED";
    public string? Note { get; private set; }
    public DateTimeOffset ConfirmedAt { get; private set; }
    private Confirmation() { }

    public static Confirmation TaskAccepted(WorkTask task, TaskAssignment assignment, Guid actorId, string? note)
    {
        if (task.Status != TaskState.Accepted || task.WorkflowMutation?.ActorId != actorId ||
            task.WorkflowMutation.Before != TaskState.Assigned || assignment.TaskId != task.Id ||
            assignment.UserId != actorId || assignment.AcceptedAt != task.UpdatedAt ||
            assignment.RejectedAt is not null || assignment.EndedAt is not null)
            throw new InvalidOperationException("Confirmation must match the authorized acceptance.");
        if (note?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ArgumentException("Use a plain-text confirmation note.", nameof(note));
        return new() { Id = Guid.CreateVersion7(), TenantId = task.TenantId, ObjectId = task.Id,
            ActorId = EntityRules.Id(actorId, nameof(actorId)), Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(), ConfirmedAt = task.UpdatedAt };
    }

    public static Confirmation TaskResultConfirmed(WorkTask task, Guid actorId, string? note)
    {
        if (task.Status is not (TaskState.Confirmed or TaskState.Completed) || task.WorkflowMutation?.ActorId != actorId ||
            (task.WorkflowMutation.Before != TaskState.Submitted && task.WorkflowMutation.Before != TaskState.Confirmed))
            throw new InvalidOperationException("Confirmation must match the authorized result confirmation.");
        if (note?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ArgumentException("Use a plain-text confirmation note.", nameof(note));
        return new() { Id = Guid.CreateVersion7(), TenantId = task.TenantId, ObjectId = task.Id, MilestoneType = "RESULT", Decision = "CONFIRMED",
            ActorId = EntityRules.Id(actorId, nameof(actorId)), Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(), ConfirmedAt = task.UpdatedAt };
    }

    public static Confirmation TaskResultRejected(WorkTask task, Guid actorId, string? note)
    {
        if (task.Status != TaskState.InProgress || task.WorkflowMutation?.ActorId != actorId ||
            task.WorkflowMutation.Before != TaskState.Submitted)
            throw new InvalidOperationException("Confirmation must match the authorized result rework.");
        if (note?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ArgumentException("Use a plain-text confirmation note.", nameof(note));
        return new() { Id = Guid.CreateVersion7(), TenantId = task.TenantId, ObjectId = task.Id, MilestoneType = "RESULT", Decision = "REJECTED",
            ActorId = EntityRules.Id(actorId, nameof(actorId)), Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(), ConfirmedAt = task.UpdatedAt };
    }

    public static Confirmation RequestReceived(BizFlow.Domain.Requests.WorkRequest request, Guid actorId, string? note)
    {
        if (request.Status != BizFlow.Domain.Requests.RequestState.Received || request.WorkflowMutation?.ActorId != actorId ||
            request.WorkflowMutation.Before != BizFlow.Domain.Requests.RequestState.Routed)
            throw new InvalidOperationException("Confirmation must match the authorized request receipt.");
        if (note?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ArgumentException("Use a plain-text confirmation note.", nameof(note));
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = request.TenantId,
            ObjectType = "REQUEST",
            ObjectId = request.Id,
            MilestoneType = "RECEIVE",
            Decision = "CONFIRMED",
            ActorId = EntityRules.Id(actorId, nameof(actorId)),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            ConfirmedAt = request.UpdatedAt
        };
    }

    public static Confirmation RequestResolutionConfirmed(BizFlow.Domain.Requests.WorkRequest request, Guid actorId, string? note)
    {
        if (request.Status is not (BizFlow.Domain.Requests.RequestState.Confirmed or BizFlow.Domain.Requests.RequestState.Closed) || request.WorkflowMutation?.ActorId != actorId ||
            (request.WorkflowMutation.Before != BizFlow.Domain.Requests.RequestState.Resolved && request.WorkflowMutation.Before != BizFlow.Domain.Requests.RequestState.Confirmed))
            throw new InvalidOperationException("Confirmation must match the authorized resolution confirmation.");
        if (note?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ArgumentException("Use a plain-text confirmation note.", nameof(note));
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = request.TenantId,
            ObjectType = "REQUEST",
            ObjectId = request.Id,
            MilestoneType = "RESOLUTION",
            Decision = "CONFIRMED",
            ActorId = EntityRules.Id(actorId, nameof(actorId)),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            ConfirmedAt = request.UpdatedAt
        };
    }

    public static Confirmation RequestResolutionRejected(BizFlow.Domain.Requests.WorkRequest request, Guid actorId, string? note)
    {
        if (request.Status != BizFlow.Domain.Requests.RequestState.InProgress || request.WorkflowMutation?.ActorId != actorId ||
            request.WorkflowMutation.Before != BizFlow.Domain.Requests.RequestState.Resolved)
            throw new InvalidOperationException("Confirmation must match the authorized resolution rework.");
        if (note?.Any(c => char.IsControl(c) && c is not ('\r' or '\n' or '\t')) == true)
            throw new ArgumentException("Use a plain-text confirmation note.", nameof(note));
        return new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = request.TenantId,
            ObjectType = "REQUEST",
            ObjectId = request.Id,
            MilestoneType = "RESOLUTION",
            Decision = "REJECTED",
            ActorId = EntityRules.Id(actorId, nameof(actorId)),
            Note = string.IsNullOrWhiteSpace(note) ? null : note.Trim(),
            ConfirmedAt = request.UpdatedAt
        };
    }
}
