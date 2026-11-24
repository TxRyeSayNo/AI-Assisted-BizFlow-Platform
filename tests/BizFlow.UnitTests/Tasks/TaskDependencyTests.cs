using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Tasks;

/// <summary>
/// The dependency record and the two rules it carries (F7): what counts as settled, and when a new
/// link would close a loop.
/// </summary>
public sealed class TaskDependencyTests
{
    private static readonly Guid Tenant = Guid.CreateVersion7();
    private static readonly Guid Actor = Guid.CreateVersion7();
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public void A_link_records_both_ends_and_who_drew_it()
    {
        var waits = Guid.CreateVersion7();
        var first = Guid.CreateVersion7();

        var dependency = TaskDependency.Create(Tenant, waits, first, Actor, Now);

        Assert.NotEqual(Guid.Empty, dependency.Id);
        Assert.Equal(Tenant, dependency.TenantId);
        Assert.Equal(waits, dependency.TaskId);
        Assert.Equal(first, dependency.DependsOnTaskId);
        Assert.Equal(Actor, dependency.CreatedBy);
        Assert.Equal(Now, dependency.CreatedAt);
        Assert.Null(dependency.RemovedAt);
    }

    [Fact]
    public void A_task_cannot_wait_for_itself_and_ids_are_required()
    {
        var task = Guid.CreateVersion7();

        Assert.Throws<ArgumentException>(() => TaskDependency.Create(Tenant, task, task, Actor, Now));
        Assert.Throws<ArgumentException>(() => TaskDependency.Create(Tenant, Guid.Empty, task, Actor, Now));
        Assert.Throws<ArgumentException>(() => TaskDependency.Create(Tenant, task, Guid.Empty, Actor, Now));
        Assert.Throws<ArgumentException>(() => TaskDependency.Create(Guid.Empty, task, Guid.CreateVersion7(), Actor, Now));
    }

    [Fact]
    public void Unlinking_is_soft_idempotent_and_reversible_without_touching_the_stamps()
    {
        var dependency = TaskDependency.Create(Tenant, Guid.CreateVersion7(), Guid.CreateVersion7(), Actor, Now);

        Assert.True(dependency.Remove(Now));
        Assert.Equal(Now, dependency.RemovedAt);
        // A second removal changes nothing, so a repeated call is not an error.
        Assert.False(dependency.Remove(Now.AddHours(1)));
        Assert.Equal(Now, dependency.RemovedAt);

        Assert.True(dependency.Restore());
        Assert.Null(dependency.RemovedAt);
        Assert.False(dependency.Restore());
        // Identity and creation stamps are immutable, which the persistence boundary also enforces.
        Assert.Equal(Now, dependency.CreatedAt);
        Assert.Equal(Actor, dependency.CreatedBy);
    }

    [Theory]
    [InlineData(TaskState.Confirmed, true)]
    [InlineData(TaskState.Completed, true)]
    [InlineData(TaskState.Cancelled, true)]
    [InlineData(TaskState.Draft, false)]
    [InlineData(TaskState.Assigned, false)]
    [InlineData(TaskState.Accepted, false)]
    [InlineData(TaskState.InProgress, false)]
    [InlineData(TaskState.Submitted, false)]
    [InlineData(TaskState.Rejected, false)]
    [InlineData(TaskState.Overdue, false)]
    public void Only_a_finished_or_abandoned_predecessor_stops_holding_things_up(TaskState state, bool settled) =>
        Assert.Equal(settled, TaskDependencyRules.IsSettled(state));

    [Fact]
    public void A_direct_or_indirect_loop_is_detected_and_a_diamond_is_not()
    {
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();
        var c = Guid.CreateVersion7();
        var d = Guid.CreateVersion7();

        // b waits for a, c waits for b.
        var edges = Edges((b, [a]), (c, [b]));

        // a waiting for c would close a → b → c → a.
        Assert.True(TaskDependencyRules.WouldCycle(a, c, edges));
        // The immediate reverse of an existing link is a loop too.
        Assert.True(TaskDependencyRules.WouldCycle(a, b, edges));
        Assert.True(TaskDependencyRules.WouldCycle(a, a, edges));
        // d waiting for c is fine: nothing waits for d.
        Assert.False(TaskDependencyRules.WouldCycle(d, c, edges));
        // A diamond is not a cycle: d waits for both b and c, which both descend from a.
        Assert.False(TaskDependencyRules.WouldCycle(d, b, Edges((b, [a]), (c, [a]), (d, [c]))));
    }

    [Fact]
    public void Cycle_detection_terminates_on_data_that_already_contains_a_loop()
    {
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();
        var c = Guid.CreateVersion7();

        // Data that should not exist, but the walk must still finish rather than spin.
        var edges = Edges((a, [b]), (b, [a]), (c, [b]));

        Assert.True(TaskDependencyRules.WouldCycle(a, b, edges));
        Assert.False(TaskDependencyRules.WouldCycle(c, a, edges));
    }

    private static Dictionary<Guid, IReadOnlyList<Guid>> Edges(params (Guid Task, Guid[] DependsOn)[] rows) =>
        rows.ToDictionary(row => row.Task, row => (IReadOnlyList<Guid>)row.DependsOn);
}
