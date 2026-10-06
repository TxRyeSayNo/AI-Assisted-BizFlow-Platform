using System.Text.Json;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Tasks;

public sealed class TaskCreationAuditTests
{
    [Fact]
    public void Creation_audit_attributes_the_tenant_creator_and_preserves_structured_initial_state()
    {
        var now = new DateTimeOffset(2026, 10, 5, 13, 0, 0, TimeSpan.FromHours(7));
        var request = Guid.NewGuid();
        var task = WorkTask.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "<b>Plain title</b>", now,
            "Initial description", TaskPriority.Critical, now.AddDays(1), request);
        var second = TaskChecklistItem.Create(task.Id, "Second", 2);
        var first = TaskChecklistItem.Create(task.Id, "First", 1);
        var audit = AuditLog.TaskCreated(task, [second, first], now);
        Assert.Equal(task.TenantId, audit.TenantId); Assert.Equal(task.CreatorId, audit.ActorId);
        Assert.Equal(AuditActorType.User, audit.ActorType); Assert.Equal("Task", audit.ObjectType);
        Assert.Equal(task.Id, audit.ObjectId); Assert.Equal("TASK.CREATED", audit.Action);
        Assert.Null(audit.BeforeJson); Assert.Equal(TimeSpan.Zero, audit.CreatedAt.Offset);
        using var json = JsonDocument.Parse(audit.AfterJson!); var snapshot = json.RootElement;
        Assert.Equal(task.Id, snapshot.GetProperty("taskId").GetGuid());
        Assert.Equal(request, snapshot.GetProperty("requestId").GetGuid());
        Assert.Equal("<b>Plain title</b>", snapshot.GetProperty("title").GetString());
        Assert.Equal("Initial description", snapshot.GetProperty("description").GetString());
        Assert.Equal("CRITICAL", snapshot.GetProperty("priority").GetString());
        Assert.Equal("DRAFT", snapshot.GetProperty("status").GetString());
        Assert.Equal(task.Deadline, snapshot.GetProperty("deadline").GetDateTimeOffset());
        var items = snapshot.GetProperty("checklist").EnumerateArray().ToArray();
        Assert.Equal(first.Id, items[0].GetProperty("checklistItemId").GetGuid());
        Assert.Equal(second.Id, items[1].GetProperty("checklistItemId").GetGuid());
        Assert.All(items, item => Assert.False(item.GetProperty("isCompleted").GetBoolean()));
    }

    [Fact]
    public void Empty_initial_checklist_and_nullable_fields_are_preserved_without_inventing_values()
    {
        var task = WorkTask.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "Independent", DateTimeOffset.UtcNow);
        var audit = AuditLog.TaskCreated(task, [], DateTimeOffset.UtcNow);
        using var json = JsonDocument.Parse(audit.AfterJson!);
        foreach (var key in new[] { "requestId", "description", "deadline", "workflowVersionId", "slaVersionId" })
            Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty(key).ValueKind);
        Assert.Empty(json.RootElement.GetProperty("checklist").EnumerateArray());
        Assert.Equal("MEDIUM", json.RootElement.GetProperty("priority").GetString());
    }

    [Fact]
    public void Another_tasks_checklist_cannot_be_attributed_to_this_creation()
    {
        var task = WorkTask.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), "Task", DateTimeOffset.UtcNow);
        var foreign = TaskChecklistItem.Create(Guid.NewGuid(), "Other task", 1);
        Assert.Throws<ArgumentException>(() => AuditLog.TaskCreated(task, [foreign], DateTimeOffset.UtcNow));
    }
}
