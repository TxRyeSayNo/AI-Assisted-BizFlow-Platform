using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using Xunit;

namespace BizFlow.UnitTests.AI;

public sealed class RobotChatServiceTests
{
    private sealed class FakeTenantContext(Guid? tenantId, Guid? userId) : ITenantContext
    {
        public Guid? UserId => userId;
        public Guid? TenantId => tenantId;
    }

    private sealed class FakeTaskListReader(IReadOnlyList<TaskListRow> tasks) : ITaskListReader
    {
        public Task<TaskListPage> ListAsync(TaskReadScope scope, TaskListFilter filter, CancellationToken cancellationToken)
        {
            return Task.FromResult(new TaskListPage(tasks, filter.Page, filter.PageSize, tasks.Count));
        }
    }

    private sealed class FakeAccessSnapshotProvider : IAccessSnapshotProvider
    {
        public Task<AccessSnapshot?> ResolveAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken)
        {
            return Task.FromResult<AccessSnapshot?>(new AccessSnapshot(
                userId,
                tenantId,
                true,
                true,
                [
                    new PermissionGrant("tasks.read.tenant", PermissionScope.Tenant),
                    new PermissionGrant("notifications.read", PermissionScope.Tenant)
                ],
                new HashSet<Guid>(),
                new HashSet<Guid>()));
        }
    }

    private sealed class FakeSecurityAuditWriter : ISecurityAuditWriter
    {
        public Task RecordDeniedAccessAsync(Guid? userId, Guid? tenantId, string permission, AccessDenial reason, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeNotificationInboxStore : INotificationInboxStore
    {
        public Task<NotificationPage> ListAsync(Guid tenantId, Guid recipientId, NotificationFilter filter, CancellationToken cancellationToken)
        {
            var notifs = new List<NotificationRow>
            {
                new(Guid.NewGuid(), "DEADLINE_WARNING", "Task", Guid.NewGuid(), "Sắp đến hạn", "Task #1 sắp đến hạn trong 4 giờ", null, DateTimeOffset.UtcNow)
            };
            return Task.FromResult(new NotificationPage(notifs, 1, 10, 1, 1));
        }

        public Task<INotificationReceiptTransaction?> BeginReadAsync(Guid tenantId, Guid recipientId, Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<INotificationReceiptTransaction?>(null);
    }

    private sealed class FakeNotificationUpdates : INotificationUpdates
    {
        public Task PublishAsync(Guid tenantId, IReadOnlyCollection<Guid> recipientIds, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeDecompositionService : IAiTaskDecompositionService
    {
        public Task<TaskDecompositionPreviewResult> PreviewAsync(TaskDecompositionPreviewCommand command, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new TaskDecompositionPreviewResult(
                command.Prompt,
                [
                    new TaskDecompositionProposalDto("Cài đặt OS", "Cài đặt Windows", "Assignment", null, "Phòng IT", "HIGH", ["B1", "B2"], "Phù hợp onboarding"),
                    new TaskDecompositionProposalDto("Cấp thẻ", "Cấp thẻ vào cửa", "SupportRequest", null, "Phòng HC", "MEDIUM", [], "Thủ tục hành chính")
                ]));
        }

        public Task<ConfirmTaskDecompositionResponse> ConfirmAsync(ConfirmTaskDecompositionCommand command, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConfirmTaskDecompositionResponse(command.Items.Count, []));
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public async Task ProcessMessage_WhenTaskCommandWithPrompt_ReturnsStructuredPreviewWithoutDbWrites()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

        var taskList = new TaskListQuery(
            new FakeTenantContext(tenantId, userId),
            new FakeAccessSnapshotProvider(),
            new FakeSecurityAuditWriter(),
            new FakeTaskListReader([]));

        var membership = new TenantMembershipAuthorizer(
            new FakeTenantContext(tenantId, userId),
            new FakeAccessSnapshotProvider(),
            new FakeSecurityAuditWriter());

        var inbox = new NotificationInbox(
            new FakeTenantContext(tenantId, userId),
            membership,
            new FakeSecurityAuditWriter(),
            new FakeNotificationInboxStore(),
            new FakeClock(now),
            new FakeNotificationUpdates());

        var chatService = new RobotChatService(
            new FakeTenantContext(tenantId, userId),
            taskList,
            inbox,
            new FakeDecompositionService(),
            new FakeClock(now));

        var result = await chatService.ProcessMessageAsync(new RobotChatCommand("/task Onboarding nhân viên mới"));

        Assert.Equal("/task", result.RecognizedCommand);
        Assert.Contains("Cài đặt OS", result.Response);
        Assert.Contains("Cấp thẻ", result.Response);
        Assert.Contains("BR-028", result.Response);
        Assert.NotNull(result.ActionCards);
        Assert.Single(result.ActionCards);
        Assert.Equal("OPEN_DECOMPOSITION", result.ActionCards[0].ActionType);
    }

    [Fact]
    public async Task ProcessMessage_WhenNotificationsCommand_ReturnsUnreadCountAndActionCard()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

        var taskList = new TaskListQuery(
            new FakeTenantContext(tenantId, userId),
            new FakeAccessSnapshotProvider(),
            new FakeSecurityAuditWriter(),
            new FakeTaskListReader([]));

        var membership = new TenantMembershipAuthorizer(
            new FakeTenantContext(tenantId, userId),
            new FakeAccessSnapshotProvider(),
            new FakeSecurityAuditWriter());

        var inbox = new NotificationInbox(
            new FakeTenantContext(tenantId, userId),
            membership,
            new FakeSecurityAuditWriter(),
            new FakeNotificationInboxStore(),
            new FakeClock(now),
            new FakeNotificationUpdates());

        var chatService = new RobotChatService(
            new FakeTenantContext(tenantId, userId),
            taskList,
            inbox,
            new FakeDecompositionService(),
            new FakeClock(now));

        var result = await chatService.ProcessMessageAsync(new RobotChatCommand("/notifications"));

        Assert.Equal("/notifications", result.RecognizedCommand);
        Assert.Contains("1", result.Response); // 1 unread
        Assert.Contains("Web Push", result.Response);
        Assert.NotNull(result.ActionCards);
        Assert.Equal("OPEN_NOTIFICATIONS", result.ActionCards[0].ActionType);
    }

    [Fact]
    public async Task ProcessMessage_WhenAskingForDeadlinesToday_ReturnsOnlyTodayTasks_AntiHallucination()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

        var tasks = new List<TaskListRow>
        {
            new(Guid.NewGuid(), "Nhiệm vụ hạn hôm nay", "IN_PROGRESS", "HIGH", now.AddHours(4), now, userId, null, userId, "Tester", null, null),
            new(Guid.NewGuid(), "Nhiệm vụ tuần sau", "IN_PROGRESS", "LOW", now.AddDays(7), now, userId, null, userId, "Tester", null, null)
        };

        var taskList = new TaskListQuery(
            new FakeTenantContext(tenantId, userId),
            new FakeAccessSnapshotProvider(),
            new FakeSecurityAuditWriter(),
            new FakeTaskListReader(tasks));

        var membership = new TenantMembershipAuthorizer(
            new FakeTenantContext(tenantId, userId),
            new FakeAccessSnapshotProvider(),
            new FakeSecurityAuditWriter());

        var inbox = new NotificationInbox(
            new FakeTenantContext(tenantId, userId),
            membership,
            new FakeSecurityAuditWriter(),
            new FakeNotificationInboxStore(),
            new FakeClock(now),
            new FakeNotificationUpdates());

        var chatService = new RobotChatService(
            new FakeTenantContext(tenantId, userId),
            taskList,
            inbox,
            new FakeDecompositionService(),
            new FakeClock(now));

        var result = await chatService.ProcessMessageAsync(new RobotChatCommand("Hôm nay tôi có deadline nào không?"));

        Assert.Equal("QUERY_DEADLINE", result.RecognizedCommand);
        Assert.Contains("Nhiệm vụ hạn hôm nay", result.Response);
        Assert.DoesNotContain("Nhiệm vụ tuần sau", result.Response);
    }

    [Fact]
    public async Task ProcessMessage_WhenAskingForOverdue_WhenNone_ReportsAccuratelyWithoutHallucination()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var now = new DateTimeOffset(2026, 10, 21, 10, 0, 0, TimeSpan.Zero);

        var tasks = new List<TaskListRow>
        {
            new(Guid.NewGuid(), "Nhiệm vụ tương lai", "IN_PROGRESS", "HIGH", now.AddHours(24), now, userId, null, userId, "Tester", null, null)
        };

        var taskList = new TaskListQuery(
            new FakeTenantContext(tenantId, userId),
            new FakeAccessSnapshotProvider(),
            new FakeSecurityAuditWriter(),
            new FakeTaskListReader(tasks));

        var membership = new TenantMembershipAuthorizer(
            new FakeTenantContext(tenantId, userId),
            new FakeAccessSnapshotProvider(),
            new FakeSecurityAuditWriter());

        var inbox = new NotificationInbox(
            new FakeTenantContext(tenantId, userId),
            membership,
            new FakeSecurityAuditWriter(),
            new FakeNotificationInboxStore(),
            new FakeClock(now),
            new FakeNotificationUpdates());

        var chatService = new RobotChatService(
            new FakeTenantContext(tenantId, userId),
            taskList,
            inbox,
            new FakeDecompositionService(),
            new FakeClock(now));

        var result = await chatService.ProcessMessageAsync(new RobotChatCommand("Kiểm tra việc bị quá hạn"));

        Assert.Equal("QUERY_OVERDUE", result.RecognizedCommand);
        Assert.Contains("không có công việc nào bị quá hạn", result.Response);
    }
}
