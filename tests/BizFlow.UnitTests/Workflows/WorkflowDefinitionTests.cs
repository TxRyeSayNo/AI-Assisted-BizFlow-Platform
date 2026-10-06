using System.Text.Json;
using BizFlow.Domain.Workflows;

namespace BizFlow.UnitTests.Workflows;

public sealed class WorkflowDefinitionTests
{
    [Fact]
    public void Draft_metadata_preserves_approved_fields_and_UTC_without_claiming_publication()
    {
        var tenant = Guid.NewGuid(); var now = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.FromHours(7));
        var workflow = WorkflowDefinition.CreateDraft(tenant, " Task lifecycle ", WorkflowBusinessType.Task, now);
        Assert.Equal(tenant, workflow.TenantId); Assert.Equal("Task lifecycle", workflow.Name);
        Assert.Equal(WorkflowStatus.Draft, workflow.Status); Assert.Equal(TimeSpan.Zero, workflow.CreatedAt.Offset);
        var version = WorkflowVersion.CreateDraft(workflow.Id, 1);
        Assert.Equal(WorkflowVersionStatus.Draft, version.Status); Assert.Null(version.PublishedAt);
        Assert.Equal("{}", version.DefinitionJson); Assert.Equal(workflow.Id, version.WorkflowId);
    }

    [Fact]
    public void Draft_steps_and_transitions_validate_structural_fields_not_publish_semantics()
    {
        var version = Guid.NewGuid();
        var step = WorkflowStep.CreateDraft(version, " review-result ", "Review result", WorkflowStepType.Confirmation, 2);
        Assert.Equal("REVIEW-RESULT", step.StepCode); Assert.Equal(2, step.OrderNo);
        Assert.Equal(version, step.WorkflowVersionId); Assert.Equal("{}", step.ConfigJson);
        var transition = WorkflowTransition.CreateDraft(version, "SUBMITTED", "CONFIRMED");
        Assert.Null(transition.GuardJson); Assert.Equal("SUBMITTED", transition.FromState);
        Assert.Throws<ArgumentException>(() => WorkflowDefinition.CreateDraft(Guid.Empty, "Name", WorkflowBusinessType.Task, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkflowDefinition.CreateDraft(Guid.NewGuid(), "Name", (WorkflowBusinessType)99, DateTimeOffset.UtcNow));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkflowVersion.CreateDraft(version, 0));
        Assert.Throws<ArgumentOutOfRangeException>(() => WorkflowStep.CreateDraft(version, "STEP", "Step", WorkflowStepType.Action, 0));
        Assert.Throws<ArgumentException>(() => WorkflowStep.CreateDraft(version, "not a code", "Step", WorkflowStepType.Action, 1));
        Assert.Throws<ArgumentException>(() => WorkflowStep.CreateDraft(version, "STEP", "Step", WorkflowStepType.Action, 1, "[]"));
        Assert.ThrowsAny<JsonException>(() => WorkflowTransition.CreateDraft(version, "DRAFT", "ASSIGNED", "not-json"));
        Assert.Throws<ArgumentException>(() => WorkflowTransition.CreateDraft(version, "DRAFT", "ASSIGNED", "true"));
    }
}
