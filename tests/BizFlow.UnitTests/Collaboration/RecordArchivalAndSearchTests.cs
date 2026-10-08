using System.Text.Json;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Common;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;

namespace BizFlow.UnitTests.Collaboration;

public sealed class RecordArchivalAndSearchTests
{
    [Fact]
    public void WorkTask_Archive_succeeds_when_completed_or_cancelled()
    {
        var tenantId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var task = WorkTask.CreateDraft(tenantId, creatorId, "Complete report", now);
        // Simulate transition to Completed
        typeof(WorkTask).GetProperty(nameof(WorkTask.Status))!.SetValue(task, TaskState.Completed);

        var archiveTime = now.AddHours(2);
        task.Archive(archiveTime);

        Assert.NotNull(task.DeletedAt);
        Assert.Equal(archiveTime.ToUniversalTime(), task.DeletedAt);
        Assert.Equal(archiveTime.ToUniversalTime(), task.UpdatedAt);

        // Cannot archive again
        var ex = Assert.Throws<InvalidOperationException>(() => task.Archive(archiveTime.AddMinutes(5)));
        Assert.Contains("already archived", ex.Message);
    }

    [Fact]
    public void WorkTask_Archive_succeeds_when_cancelled()
    {
        var tenantId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var task = WorkTask.CreateDraft(tenantId, creatorId, "Cancel task", now);
        typeof(WorkTask).GetProperty(nameof(WorkTask.Status))!.SetValue(task, TaskState.Cancelled);

        var archiveTime = now.AddHours(1);
        task.Archive(archiveTime);

        Assert.NotNull(task.DeletedAt);
        Assert.Equal(archiveTime.ToUniversalTime(), task.DeletedAt);
    }

    [Theory]
    [InlineData(TaskState.Draft)]
    [InlineData(TaskState.Assigned)]
    [InlineData(TaskState.Accepted)]
    [InlineData(TaskState.InProgress)]
    [InlineData(TaskState.Submitted)]
    [InlineData(TaskState.Confirmed)]
    [InlineData(TaskState.Rejected)]
    [InlineData(TaskState.Overdue)]
    public void WorkTask_Archive_throws_when_not_in_terminal_state(TaskState nonTerminalState)
    {
        var tenantId = Guid.NewGuid();
        var creatorId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var task = WorkTask.CreateDraft(tenantId, creatorId, "Draft task", now);
        typeof(WorkTask).GetProperty(nameof(WorkTask.Status))!.SetValue(task, nonTerminalState);

        var ex = Assert.Throws<InvalidOperationException>(() => task.Archive(now));
        Assert.Contains("terminal state", ex.Message);
    }

    [Fact]
    public void WorkRequest_Archive_succeeds_when_closed_or_cancelled()
    {
        var tenantId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = WorkRequest.CreateDraft(tenantId, requesterId, serviceId, categoryId, "Request title", "Request desc", now);
        typeof(WorkRequest).GetProperty(nameof(WorkRequest.Status))!.SetValue(request, RequestState.Closed);

        var archiveTime = now.AddHours(3);
        request.Archive(archiveTime);

        Assert.NotNull(request.DeletedAt);
        Assert.Equal(archiveTime.ToUniversalTime(), request.DeletedAt);
        Assert.Equal(archiveTime.ToUniversalTime(), request.UpdatedAt);

        var ex = Assert.Throws<InvalidOperationException>(() => request.Archive(archiveTime.AddMinutes(1)));
        Assert.Contains("already archived", ex.Message);
    }

    [Fact]
    public void WorkRequest_Archive_succeeds_when_cancelled()
    {
        var tenantId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = WorkRequest.CreateDraft(tenantId, requesterId, serviceId, categoryId, "Request title", "Request desc", now);
        typeof(WorkRequest).GetProperty(nameof(WorkRequest.Status))!.SetValue(request, RequestState.Cancelled);

        var archiveTime = now.AddHours(1);
        request.Archive(archiveTime);

        Assert.NotNull(request.DeletedAt);
        Assert.Equal(archiveTime.ToUniversalTime(), request.DeletedAt);
    }

    [Theory]
    [InlineData(RequestState.Draft)]
    [InlineData(RequestState.Submitted)]
    [InlineData(RequestState.Routed)]
    [InlineData(RequestState.Received)]
    [InlineData(RequestState.InProgress)]
    [InlineData(RequestState.Resolved)]
    [InlineData(RequestState.Confirmed)]
    [InlineData(RequestState.WaitingForInformation)]
    [InlineData(RequestState.Rejected)]
    [InlineData(RequestState.Overdue)]
    public void WorkRequest_Archive_throws_when_not_in_terminal_state(RequestState nonTerminalState)
    {
        var tenantId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var serviceId = Guid.NewGuid();
        var categoryId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;

        var request = WorkRequest.CreateDraft(tenantId, requesterId, serviceId, categoryId, "Request title", "Request desc", now);
        typeof(WorkRequest).GetProperty(nameof(WorkRequest.Status))!.SetValue(request, nonTerminalState);

        var ex = Assert.Throws<InvalidOperationException>(() => request.Archive(now));
        Assert.Contains("terminal state", ex.Message);
    }

    [Fact]
    public void AuditLog_RecordArchived_creates_valid_audit_entry()
    {
        var tenantId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        var recordId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var deletedAt = now;

        var audit = AuditLog.RecordArchived("TASK", recordId, tenantId, actorId, "COMPLETED", deletedAt, now);

        Assert.NotEqual(Guid.Empty, audit.Id);
        Assert.Equal(tenantId, audit.TenantId);
        Assert.Equal(AuditActorType.User, audit.ActorType);
        Assert.Equal(actorId, audit.ActorId);
        Assert.Equal("Task", audit.ObjectType);
        Assert.Equal(recordId, audit.ObjectId);
        Assert.Equal("RECORD.ARCHIVED", audit.Action);
        Assert.Equal(now.ToUniversalTime(), audit.CreatedAt);

        using var beforeDoc = JsonDocument.Parse(audit.BeforeJson!);
        Assert.Equal("COMPLETED", beforeDoc.RootElement.GetProperty("status").GetString());
        Assert.Equal(JsonValueKind.Null, beforeDoc.RootElement.GetProperty("deletedAt").ValueKind);

        using var afterDoc = JsonDocument.Parse(audit.AfterJson!);
        Assert.Equal(recordId.ToString(), afterDoc.RootElement.GetProperty("recordId").GetString());
        Assert.Equal("TASK", afterDoc.RootElement.GetProperty("recordType").GetString());
        Assert.Equal("COMPLETED", afterDoc.RootElement.GetProperty("status").GetString());
        Assert.NotNull(afterDoc.RootElement.GetProperty("deletedAt").GetString());
    }

    [Fact]
    public async Task RecordService_ArchiveRecordAsync_fails_without_tenant_or_user_context()
    {
        var store = new FakeRecordArchivalStore();
        var reader = new FakeRecordSearchReader();

        var serviceNoTenant = new RecordService(new FakeTenantContext(null, Guid.NewGuid()), new FakeAuthorizer(), store, reader);
        var ex1 = await Assert.ThrowsAsync<ApplicationFault>(() => serviceNoTenant.ArchiveRecordAsync("task", Guid.NewGuid(), CancellationToken.None));
        Assert.Equal("ACCESS.DENIED", ex1.Code);

        var serviceNoUser = new RecordService(new FakeTenantContext(Guid.NewGuid(), null), new FakeAuthorizer(), store, reader);
        var ex2 = await Assert.ThrowsAsync<ApplicationFault>(() => serviceNoUser.ArchiveRecordAsync("task", Guid.NewGuid(), CancellationToken.None));
        Assert.Equal("ACCESS.DENIED", ex2.Code);
    }

    [Fact]
    public async Task RecordService_ArchiveRecordAsync_checks_permission()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var authorizer = new FakeAuthorizer { ShouldDeny = true };
        var service = new RecordService(new FakeTenantContext(tenantId, userId), authorizer, new FakeRecordArchivalStore(), new FakeRecordSearchReader());

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.ArchiveRecordAsync("task", Guid.NewGuid(), CancellationToken.None));
        Assert.Equal("ACCESS.DENIED", ex.Code);
        Assert.Equal("records.archive", authorizer.LastCheckedPermission);
    }

    [Fact]
    public async Task RecordService_ArchiveRecordAsync_validates_type_and_id()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var service = new RecordService(new FakeTenantContext(tenantId, userId), new FakeAuthorizer(), new FakeRecordArchivalStore(), new FakeRecordSearchReader());

        var exId = await Assert.ThrowsAsync<ApplicationFault>(() => service.ArchiveRecordAsync("task", Guid.Empty, CancellationToken.None));
        Assert.Equal("RECORD.ID_REQUIRED", exId.Code);

        var exType = await Assert.ThrowsAsync<ApplicationFault>(() => service.ArchiveRecordAsync("unknown", Guid.NewGuid(), CancellationToken.None));
        Assert.Equal("RECORD.INVALID_TYPE", exType.Code);
    }

    [Fact]
    public async Task RecordService_ArchiveRecordAsync_archives_completed_task()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var task = WorkTask.CreateDraft(tenantId, userId, "Test task", now);
        typeof(WorkTask).GetProperty(nameof(WorkTask.Status))!.SetValue(task, TaskState.Completed);

        var store = new FakeRecordArchivalStore();
        store.Tasks[task.Id] = task;

        var service = new RecordService(new FakeTenantContext(tenantId, userId), new FakeAuthorizer(), store, new FakeRecordSearchReader());

        var result = await service.ArchiveRecordAsync("task", task.Id, CancellationToken.None);

        Assert.Equal(task.Id, result.RecordId);
        Assert.Equal("TASK", result.RecordType);
        Assert.Equal("COMPLETED", result.Status);
        Assert.NotNull(task.DeletedAt);
        Assert.Single(store.SavedAudits);
        Assert.Equal("RECORD.ARCHIVED", store.SavedAudits[0].Action);
    }

    [Fact]
    public async Task RecordService_ArchiveRecordAsync_throws_conflict_when_already_archived()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var taskId = Guid.NewGuid();

        var store = new FakeRecordArchivalStore();
        store.ArchivedTaskIds.Add(taskId);

        var service = new RecordService(new FakeTenantContext(tenantId, userId), new FakeAuthorizer(), store, new FakeRecordSearchReader());

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.ArchiveRecordAsync("task", taskId, CancellationToken.None));
        Assert.Equal("RECORD.ALREADY_ARCHIVED", ex.Code);
    }

    [Fact]
    public async Task RecordService_ArchiveRecordAsync_archives_closed_request()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = DateTimeOffset.UtcNow;
        var request = WorkRequest.CreateDraft(tenantId, userId, Guid.NewGuid(), Guid.NewGuid(), "Req title", "Req desc", now);
        typeof(WorkRequest).GetProperty(nameof(WorkRequest.Status))!.SetValue(request, RequestState.Closed);

        var store = new FakeRecordArchivalStore();
        store.Requests[request.Id] = request;

        var service = new RecordService(new FakeTenantContext(tenantId, userId), new FakeAuthorizer(), store, new FakeRecordSearchReader());

        var result = await service.ArchiveRecordAsync("request", request.Id, CancellationToken.None);

        Assert.Equal(request.Id, result.RecordId);
        Assert.Equal("REQUEST", result.RecordType);
        Assert.Equal("CLOSED", result.Status);
        Assert.NotNull(request.DeletedAt);
        Assert.Single(store.SavedAudits);
        Assert.Equal("RECORD.ARCHIVED", store.SavedAudits[0].Action);
    }

    [Fact]
    public async Task RecordService_SearchRecordsAsync_checks_permission_and_normalizes_params()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var authorizer = new FakeAuthorizer();
        var reader = new FakeRecordSearchReader();
        var service = new RecordService(new FakeTenantContext(tenantId, userId), authorizer, new FakeRecordArchivalStore(), reader);

        var query = new RecordSearchQuery(
            Query: "  audit search  ",
            Type: "task",
            Status: "completed",
            Priority: "high",
            IncludeArchived: true,
            Page: 0,
            PageSize: 500);

        var result = await service.SearchRecordsAsync(query, CancellationToken.None);

        Assert.Equal("records.search", authorizer.LastCheckedPermission);
        Assert.NotNull(reader.LastQuery);
        Assert.Equal("audit search", reader.LastQuery.Query);
        Assert.Equal("TASK", reader.LastQuery.Type);
        Assert.Equal("COMPLETED", reader.LastQuery.Status);
        Assert.Equal("HIGH", reader.LastQuery.Priority);
        Assert.True(reader.LastQuery.IncludeArchived);
        Assert.Equal(1, reader.LastQuery.Page); // normalized from 0 to 1
        Assert.Equal(100, reader.LastQuery.PageSize); // clamped from 500 to 100
    }

    private sealed class FakeTenantContext(Guid? tenantId, Guid? userId) : ITenantContext
    {
        public Guid? TenantId => tenantId;
        public Guid? UserId => userId;
        public IReadOnlyCollection<Guid> DepartmentIds => Array.Empty<Guid>();
    }

    private sealed class FakeAuthorizer : IResourceAuthorizer
    {
        public bool ShouldDeny { get; set; }
        public string? LastCheckedPermission { get; private set; }

        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default)
        {
            LastCheckedPermission = permission;
            if (ShouldDeny) throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Access denied.");
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRecordArchivalStore : IRecordArchivalStore
    {
        public Dictionary<Guid, WorkTask> Tasks { get; } = new();
        public Dictionary<Guid, WorkRequest> Requests { get; } = new();
        public HashSet<Guid> ArchivedTaskIds { get; } = new();
        public HashSet<Guid> ArchivedRequestIds { get; } = new();
        public List<AuditLog> SavedAudits { get; } = new();

        public Task<WorkTask?> FindTaskForArchivalAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Tasks.GetValueOrDefault(taskId));
        }

        public Task<bool> IsTaskArchivedAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken)
        {
            return Task.FromResult(ArchivedTaskIds.Contains(taskId));
        }

        public Task ArchiveTaskAsync(WorkTask task, AuditLog audit, CancellationToken cancellationToken)
        {
            SavedAudits.Add(audit);
            return Task.CompletedTask;
        }

        public Task<WorkRequest?> FindRequestForArchivalAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken)
        {
            return Task.FromResult(Requests.GetValueOrDefault(requestId));
        }

        public Task<bool> IsRequestArchivedAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken)
        {
            return Task.FromResult(ArchivedRequestIds.Contains(requestId));
        }

        public Task ArchiveRequestAsync(WorkRequest request, AuditLog audit, CancellationToken cancellationToken)
        {
            SavedAudits.Add(audit);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeRecordSearchReader : IRecordSearchReader
    {
        public RecordSearchQuery? LastQuery { get; private set; }

        public Task<RecordSearchPagedResult> SearchRecordsAsync(Guid tenantId, RecordSearchQuery query, CancellationToken cancellationToken)
        {
            LastQuery = query;
            return Task.FromResult(new RecordSearchPagedResult(Array.Empty<RecordSearchResultItem>(), 0, query.Page, query.PageSize, 0));
        }
    }
}
