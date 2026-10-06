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
}
