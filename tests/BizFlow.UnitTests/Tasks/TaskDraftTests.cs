using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskDraftTests
{
    [Fact]
    public void Independent_or_request_linked_drafts_preserve_fields_without_assignment_or_workflow_side_effects()
    {
        var tenant = Guid.NewGuid(); var creator = Guid.NewGuid(); var request = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 4, 12, 0, 0, TimeSpan.FromHours(7));
        var independent = WorkTask.CreateDraft(tenant, creator, "  Task  ", now);
        Assert.Null(independent.RequestId); Assert.Null(independent.Description); Assert.Null(independent.Deadline);
        Assert.Equal(TaskPriority.Medium, independent.Priority); Assert.Equal("Task", independent.Title);
        Assert.Equal(TaskState.Draft, independent.Status); Assert.Null(independent.CompletedAt); Assert.Null(independent.DeletedAt);
        Assert.Null(independent.WorkflowVersionId); Assert.Null(independent.SlaVersionId);
        var linked = WorkTask.CreateDraft(tenant, creator, "Linked", now, "  <b>Literal</b>\nDetails  ", TaskPriority.Low, now.AddDays(1), request);
        Assert.NotEqual(independent.Id, linked.Id); Assert.Equal(request, linked.RequestId);
        Assert.Equal(tenant, linked.TenantId); Assert.Equal(creator, linked.CreatorId); Assert.Equal(TaskPriority.Low, linked.Priority);
        Assert.Equal("<b>Literal</b>\nDetails", linked.Description); Assert.Equal(TimeSpan.Zero, linked.Deadline!.Value.Offset);
        Assert.Equal(now.ToUniversalTime(), linked.CreatedAt); Assert.Equal(linked.CreatedAt, linked.UpdatedAt);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" \t ")]
    [InlineData("bad\ntext")]
    [InlineData("bad\u0001text")]
    public void Task_and_checklist_titles_require_plain_nonblank_text(string title)
    {
        Assert.Throws<ArgumentException>(() => WorkTask.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), title, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentException>(() => TaskChecklistItem.Create(Guid.NewGuid(), title, 1));
    }

    [Fact]
    public void Draft_rejects_invalid_ids_priority_and_overlong_or_control_character_text()
    {
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        Assert.Throws<ArgumentException>(() => WorkTask.CreateDraft(Guid.Empty, id, "Task", now));
        Assert.Throws<ArgumentException>(() => WorkTask.CreateDraft(id, Guid.Empty, "Task", now));
        Assert.Throws<ArgumentException>(() => WorkTask.CreateDraft(id, id, "Task", now, requestId: Guid.Empty));
        Assert.Throws<ArgumentException>(() => WorkTask.CreateDraft(id, id, "Task", now, priority: (TaskPriority)999));
        Assert.Throws<ArgumentException>(() => WorkTask.CreateDraft(id, id, new string('x', 301), now));
        Assert.Throws<ArgumentException>(() => WorkTask.CreateDraft(id, id, "Task", now, "Bad\u007ftext"));
        Assert.Null(WorkTask.CreateDraft(id, id, "Task", now, " \r\n\t ").Description);
    }

    [Fact]
    public void Checklist_starts_uncompleted_with_positive_order_and_valid_task_reference()
    {
        var task = Guid.NewGuid(); var item = TaskChecklistItem.Create(task, "  Verify  ", 2);
        Assert.NotEqual(Guid.Empty, item.Id); Assert.Equal(task, item.TaskId); Assert.Equal("Verify", item.Title);
        Assert.Equal(2, item.SortOrder); Assert.False(item.IsCompleted); Assert.Null(item.CompletedBy); Assert.Null(item.CompletedAt);
        Assert.Throws<ArgumentException>(() => TaskChecklistItem.Create(Guid.Empty, "Verify", 1));
        Assert.Throws<ArgumentException>(() => TaskChecklistItem.Create(task, new string('x', 301), 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => TaskChecklistItem.Create(task, "Verify", 0));
    }
}
