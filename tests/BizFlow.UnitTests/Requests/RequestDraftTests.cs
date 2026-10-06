using BizFlow.Domain.Requests;

namespace BizFlow.UnitTests.Requests;

public sealed class RequestDraftTests
{
    [Fact]
    public void Draft_preserves_the_dictionary_and_does_not_invent_runtime_bindings()
    {
        var tenant = Guid.NewGuid(); var user = Guid.NewGuid(); var service = Guid.NewGuid(); var category = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 3, 15, 0, 0, TimeSpan.FromHours(7));
        var draft = WorkRequest.CreateDraft(tenant, user, service, category, "  Help  ", "  <script>literal</script>\nDetails  ", now);
        Assert.NotEqual(Guid.Empty, draft.Id); Assert.Equal(tenant, draft.TenantId); Assert.Equal(user, draft.RequesterId);
        Assert.Equal(service, draft.ServiceId); Assert.Equal(category, draft.CategoryId);
        Assert.Equal("Help", draft.Title); Assert.Equal("<script>literal</script>\nDetails", draft.Description);
        Assert.Equal(RequestPriority.Medium, draft.Priority); Assert.Equal(RequestState.Draft, draft.Status);
        Assert.Null(draft.ParentRequestId); Assert.Null(draft.RevisedFromRequestId);
        Assert.Null(draft.WorkflowVersionId); Assert.Null(draft.SlaVersionId);
        Assert.Null(draft.ResolvedAt); Assert.Null(draft.ClosedAt); Assert.Null(draft.DeletedAt);
        Assert.Equal(now.ToUniversalTime(), draft.CreatedAt); Assert.Equal(draft.CreatedAt, draft.UpdatedAt);
    }
    [Theory]
    [InlineData("", "Details")]
    [InlineData("Title", " ")]
    [InlineData("bad\u0001title", "Details")]
    [InlineData("Title", "bad\0description")]
    public void Invalid_text_is_rejected(string title, string description) => Assert.Throws<ArgumentException>(() =>
        WorkRequest.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), title, description, DateTimeOffset.UtcNow));

    [Fact]
    public void Identifiers_lengths_and_enum_values_fail_closed()
    {
        var id = Guid.NewGuid(); var now = DateTimeOffset.UtcNow;
        foreach (var index in Enumerable.Range(0, 4))
        {
            var ids = new[] { id, id, id, id }; ids[index] = Guid.Empty;
            Assert.Throws<ArgumentException>(() => WorkRequest.CreateDraft(ids[0], ids[1], ids[2], ids[3], "Title", "Details", now));
        }
        Assert.Throws<ArgumentException>(() => WorkRequest.CreateDraft(id, id, id, id, new string('x', 301), "Details", now));
        Assert.Throws<ArgumentException>(() => WorkRequest.CreateDraft(id, id, id, id, "Title", "Details", now, (RequestPriority)999));
        Assert.Throws<ArgumentException>(() => WorkRequest.CreateDraft(id, id, id, id, "Title", "Details", now, parentRequestId: Guid.Empty));
        Assert.Equal(12, Enum.GetValues<RequestState>().Length);
        Assert.Equal(4, Enum.GetValues<RequestPriority>().Length);
    }
    [Fact]
    public void Revision_requires_rejected_source_and_never_changes_it()
    {
        var now = DateTimeOffset.UtcNow;
        var source = WorkRequest.CreateDraft(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Old", "Old description", now);
        Assert.Throws<InvalidOperationException>(() => WorkRequest.ReviseRejected(source, source.RequesterId, source.ServiceId, source.CategoryId, "New", "New description", now));
        // Simulates a persisted historical source; no production status setter is exposed.
        typeof(WorkRequest).GetProperty(nameof(WorkRequest.Status))!.SetValue(source, RequestState.Rejected);
        var revision = WorkRequest.ReviseRejected(source, source.RequesterId, source.ServiceId, source.CategoryId, " New ", "New description", now.AddHours(1), RequestPriority.High);
        Assert.NotEqual(source.Id, revision.Id); Assert.Equal(source.Id, revision.RevisedFromRequestId);
        Assert.Equal(source.TenantId, revision.TenantId); Assert.Equal(RequestState.Draft, revision.Status);
        Assert.Equal(RequestPriority.High, revision.Priority); Assert.Equal("New", revision.Title);
        Assert.Equal(RequestState.Rejected, source.Status); Assert.Equal("Old", source.Title);
        Assert.Equal("Old description", source.Description); Assert.Equal(now, source.UpdatedAt);
        Assert.Null(revision.ParentRequestId); Assert.Null(revision.WorkflowVersionId); Assert.Null(revision.SlaVersionId);
    }
}
