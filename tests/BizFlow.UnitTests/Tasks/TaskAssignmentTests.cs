using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskAssignmentTests
{
    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(true, true)]
    public void Initial_assignment_preserves_targets_actor_and_utc_without_claiming_acceptance(bool departmentTarget, bool userTarget)
    {
        var task = Guid.NewGuid(); var actor = Guid.NewGuid();
        Guid? department = departmentTarget ? Guid.NewGuid() : null; Guid? user = userTarget ? Guid.NewGuid() : null;
        var now = new DateTimeOffset(2026, 10, 4, 18, 0, 0, TimeSpan.FromHours(7));
        var assignment = TaskAssignment.Create(task, actor, department, user, now);
        Assert.NotEqual(Guid.Empty, assignment.Id); Assert.Equal(task, assignment.TaskId); Assert.Equal(actor, assignment.AssignedBy);
        Assert.Equal(department, assignment.DepartmentId); Assert.Equal(user, assignment.UserId);
        Assert.Equal(now.ToUniversalTime(), assignment.AssignedAt); Assert.Equal(TimeSpan.Zero, assignment.AssignedAt.Offset);
        Assert.Null(assignment.AcceptedAt); Assert.Null(assignment.RejectedAt); Assert.Null(assignment.RejectionReason); Assert.Null(assignment.EndedAt);
    }

    [Fact]
    public void Missing_targets_and_empty_identifiers_are_rejected()
    {
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => TaskAssignment.Create(id, id, null, null, now));
        Assert.Throws<ArgumentException>(() => TaskAssignment.Create(Guid.Empty, id, null, id, now));
        Assert.Throws<ArgumentException>(() => TaskAssignment.Create(id, Guid.Empty, null, id, now));
        Assert.Throws<ArgumentException>(() => TaskAssignment.Create(id, id, Guid.Empty, id, now));
        Assert.Throws<ArgumentException>(() => TaskAssignment.Create(id, id, id, Guid.Empty, now));
    }
}
