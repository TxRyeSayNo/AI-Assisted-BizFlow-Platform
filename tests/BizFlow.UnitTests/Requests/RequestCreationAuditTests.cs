using System.Text.Json;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Requests;

namespace BizFlow.UnitTests.Requests;

public sealed class RequestCreationAuditTests
{
    [Fact]
    public void Creation_audit_attributes_the_tenant_requester_and_preserves_initial_state()
    {
        var now = new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.FromHours(7));
        var tenantId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var request = WorkRequest.CreateDraft(tenantId, requesterId, serviceId, categoryId,
            "Office equipment request", "Requesting ergonomic chair", now, RequestPriority.High);

        var audit = AuditLog.RequestCreated(request, now);

        Assert.Equal(tenantId, audit.TenantId);
        Assert.Equal(requesterId, audit.ActorId);
        Assert.Equal(AuditActorType.User, audit.ActorType);
        Assert.Equal("Request", audit.ObjectType);
        Assert.Equal(request.Id, audit.ObjectId);
        Assert.Equal("REQUEST.CREATED", audit.Action);
        Assert.Null(audit.BeforeJson);
        Assert.Equal(TimeSpan.Zero, audit.CreatedAt.Offset);

        using var json = JsonDocument.Parse(audit.AfterJson!);
        var snapshot = json.RootElement;
        Assert.Equal(request.Id, snapshot.GetProperty("requestId").GetGuid());
        Assert.Equal(requesterId, snapshot.GetProperty("requesterId").GetGuid());
        Assert.Equal(serviceId, snapshot.GetProperty("serviceId").GetGuid());
        Assert.Equal(categoryId, snapshot.GetProperty("categoryId").GetGuid());
        Assert.Equal("Office equipment request", snapshot.GetProperty("title").GetString());
        Assert.Equal("Requesting ergonomic chair", snapshot.GetProperty("description").GetString());
        Assert.Equal("HIGH", snapshot.GetProperty("priority").GetString());
        Assert.Equal("DRAFT", snapshot.GetProperty("status").GetString());
    }

    [Fact]
    public void Submission_audit_records_draft_to_submitted_mutation()
    {
        var now = new DateTimeOffset(2026, 10, 7, 10, 0, 0, TimeSpan.FromHours(7));
        var tenantId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();

        var request = WorkRequest.CreateDraft(tenantId, requesterId, serviceId, categoryId,
            "Equipment", "Details", now, RequestPriority.Medium);

        var submitTime = now.AddMinutes(5);
        request.ApplySubmissionTransition(new(requesterId, RequestState.Draft, RequestState.Submitted, request.UpdatedAt), submitTime);

        var audit = AuditLog.RequestSubmitted(request, submitTime);

        Assert.Equal(tenantId, audit.TenantId);
        Assert.Equal(requesterId, audit.ActorId);
        Assert.Equal(AuditActorType.User, audit.ActorType);
        Assert.Equal("Request", audit.ObjectType);
        Assert.Equal(request.Id, audit.ObjectId);
        Assert.Equal("REQUEST.SUBMITTED", audit.Action);

        using var before = JsonDocument.Parse(audit.BeforeJson!);
        Assert.Equal("DRAFT", before.RootElement.GetProperty("status").GetString());

        using var after = JsonDocument.Parse(audit.AfterJson!);
        Assert.Equal("SUBMITTED", after.RootElement.GetProperty("status").GetString());
    }
}
