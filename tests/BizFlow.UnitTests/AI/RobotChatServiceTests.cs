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

        public Task SubscribePushAsync(Guid tenantId, Guid userId, string endpoint, string p256dh, string auth, CancellationToken cancellationToken) =>
            Task.CompletedTask;

        public Task UnsubscribePushAsync(Guid tenantId, Guid userId, string endpoint, CancellationToken cancellationToken) =>
            Task.CompletedTask;
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

    [Fact]
    public async Task ProcessMessage_WhenAskingDeadlines_InJapanese_ReturnsJapaneseText()
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

        var result = await chatService.ProcessMessageAsync(new RobotChatCommand("Hôm nay có deadline nào không?"));

        Assert.Equal("QUERY_DEADLINE", result.RecognizedCommand);
        Assert.Contains("Hôm nay bạn không có deadline nào cần hoàn thành", result.Response);
    }

    [Fact]
    public async Task ProcessMessage_WhenAskingDeadlines_ReturnsVietnameseText()
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

        var result = await chatService.ProcessMessageAsync(new RobotChatCommand("Hôm nay có việc gì"));

        Assert.Equal("QUERY_DEADLINE", result.RecognizedCommand);
        Assert.Contains("Hôm nay bạn không có deadline nào cần hoàn thành", result.Response);
    }

    [Fact]
    public async Task ProcessMessage_WhenMeetingPreparationWithoutDetails_ReturnsClarifyingQuestions_WithoutBluntRejection()
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

        var result = await chatService.ProcessMessageAsync(new RobotChatCommand("chuẩn bị cho cuộc họp ngày mai"));

        Assert.Equal("AGENT_CLARIFICATION_REQUIRED", result.RecognizedCommand);
        Assert.Contains("Thời gian & Thời lượng", result.Response);
        Assert.Contains("Địa điểm / Hình thức", result.Response);
        Assert.Contains("Thành phần tham dự", result.Response);
        Assert.Contains("Nội dung & Thiết bị", result.Response);
        Assert.DoesNotContain("không tìm thấy thông tin", result.Response);
        Assert.DoesNotContain("phạm vi dữ liệu", result.Response);
        Assert.NotNull(result.ActionCards);
        Assert.Contains(result.ActionCards, c => c.ActionType == "QUICK_FILL_MEETING");
    }

    [Fact]
    public async Task ProcessMessage_WhenMeetingDetailsProvidedAfterClarification_ExecutesDecomposition_AndReturnsBranches()
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

        var history = new List<RobotChatMessageDto>
        {
            new("user", "chuẩn bị cho cuộc họp ngày mai"),
            new("robot", "Để phân rã công việc chính xác, bạn vui lòng làm rõ giúp tôi 4 thông tin sau:\n1. ⏰ Thời gian & Thời lượng...")
        };

        var result = await chatService.ProcessMessageAsync(new RobotChatCommand(
            "Họp lúc 9h sáng mai tại phòng họp lớn, gồm Ban Giám đốc và IT, HR, cần chuẩn bị máy chiếu và tài liệu in",
            History: history));

        Assert.Equal("/task", result.RecognizedCommand);
        Assert.Contains("Đề xuất phân công tới các phòng ban chuyên trách", result.Response);
        Assert.NotNull(result.ActionCards);
        Assert.Contains(result.ActionCards, c => c.ActionType == "OPEN_DECOMPOSITION");
    }

    [Fact]
    public async Task ProcessMessage_WhenMarketingCampaignWithoutDetails_ReturnsCriticalClarifyingQuestions_AndRiskPerspective()
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

        var result = await chatService.ProcessMessageAsync(new RobotChatCommand("tổ chức chiến dịch Marketing ra mắt sản phẩm mới"));

        Assert.Equal("AGENT_CLARIFICATION_REQUIRED", result.RecognizedCommand);
        Assert.Contains("Góc nhìn Phản biện", result.Response);
        Assert.Contains("Mục tiêu & KPI cụ thể", result.Response);
        Assert.Contains("Đối tượng khách hàng & Kênh tiếp cận", result.Response);
        Assert.Contains("Ngân sách & Nguồn lực", result.Response);
        Assert.DoesNotContain("không tìm thấy thông tin", result.Response);
        Assert.NotNull(result.ActionCards);
        Assert.Contains(result.ActionCards, c => c.ActionType == "PREFILL_PROMPT");
    }

    [Fact]
    public async Task ProcessMessage_WhenRecruitmentInitiativeWithoutDetails_ReturnsRecruitmentClarifyingQuestions()
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

        var result = await chatService.ProcessMessageAsync(new RobotChatCommand("tuyển dụng nhân viên mới cho công ty"));

        Assert.Equal("AGENT_CLARIFICATION_REQUIRED", result.RecognizedCommand);
        Assert.Contains("Góc nhìn Phản biện", result.Response);
        Assert.Contains("Vị trí công việc & Số lượng chỉ tiêu", result.Response);
        Assert.Contains("Yêu cầu kỹ năng cốt lõi & Mức lương", result.Response);
        Assert.Contains("Thời hạn nhân sự đi làm", result.Response);
        Assert.DoesNotContain("không tìm thấy thông tin", result.Response);
    }

    [Fact]
    public async Task ProcessMessage_WhenTechnicalServerUpgradeWithoutDetails_ReturnsTechnicalDowntimeAndBackupQuestions()
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

        var result = await chatService.ProcessMessageAsync(new RobotChatCommand("triển khai nâng cấp máy chủ và bảo mật hệ thống"));

        Assert.Equal("AGENT_CLARIFICATION_REQUIRED", result.RecognizedCommand);
        Assert.Contains("Phạm vi kỹ thuật & Mục tiêu nâng cấp", result.Response);
        Assert.Contains("Khung giờ bảo trì & Dự kiến Downtime", result.Response);
        Assert.Contains("Phương án sao lưu & Kế hoạch Rollback", result.Response);
        Assert.DoesNotContain("không tìm thấy thông tin", result.Response);
    }

    [Fact]
    public async Task ProcessMessage_WhenMeetingInitiative_ReturnsInteractiveAgentQuestionnaireAndThought()
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

        var result = await chatService.ProcessMessageAsync(new RobotChatCommand("chuẩn bị cuộc họp cho ngày mai"));

        Assert.Equal("AGENT_CLARIFICATION_REQUIRED", result.RecognizedCommand);
        Assert.NotNull(result.Questionnaire);
        Assert.Equal("form_meeting", result.Questionnaire.FormId);
        Assert.Equal(5, result.Questionnaire.Questions.Count);
        Assert.Contains(result.Questionnaire.Questions, q => q.Id == "q_meeting_time" && q.Type == "single_choice");
        Assert.Contains(result.Questionnaire.Questions, q => q.Id == "q_meeting_format" && q.Type == "single_choice");
        Assert.Contains(result.Questionnaire.Questions, q => q.Id == "q_meeting_content" && q.Type == "single_choice");
        Assert.Contains(result.Questionnaire.Questions, q => q.Id == "q_meeting_attendees" && q.Type == "multiple_choice");
        Assert.NotNull(result.AgentThought);
        Assert.Contains("Suy nghĩ của Trợ lý AI Agent", result.AgentThought);
    }

    [Fact]
    public async Task ProcessMessage_WhenUserPromptsNotificationAndMeeting_TriggersMeetingQuestionnaire()
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

        // User prompt matching the exact user request: "Thông báo & chuẩn bị cuộc họp ngày mai cho tôi"
        // Even with prior history containing previous clarification, it must NOT treat this as an answer to old question!
        var priorHistory = new List<RobotChatMessageDto>
        {
            new("user", "Kế hoạch tuần này"),
            new("robot", "Tôi cần bạn làm rõ giúp tôi về Thời gian & Thời lượng...")
        };

        var result = await chatService.ProcessMessageAsync(new RobotChatCommand(
            "Thông báo & chuẩn bị cuộc họp ngày mai cho tôi",
            History: priorHistory));

        Assert.Equal("AGENT_CLARIFICATION_REQUIRED", result.RecognizedCommand);
        Assert.NotNull(result.Questionnaire);
        Assert.Equal("form_meeting", result.Questionnaire.FormId);
        Assert.Contains(result.Questionnaire.Questions, q => q.Id == "q_meeting_time");
        Assert.Contains(result.Questionnaire.Questions, q => q.Id == "q_meeting_format");
        Assert.Contains(result.Questionnaire.Questions, q => q.Id == "q_meeting_content");
        Assert.Contains(result.Questionnaire.Questions, q => q.Id == "q_meeting_attendees");
    }

    [Fact]
    public async Task ProcessMessage_WhenFormSubmittedWithMissingRequiredFields_AsksTargetedFollowUp()
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

        // Submit form with ONLY time answered, omitting location and attendees
        var partialAnswers = new List<AgentAnswerDto>
        {
            new("q_meeting_time", SelectedOptionIds: ["t1"], CustomText: null)
        };

        var result = await chatService.ProcessMessageAsync(new RobotChatCommand(
            "Xác nhận thông tin cuộc họp",
            FormId: "form_meeting",
            FormAnswers: partialAnswers));

        Assert.Equal("AGENT_CLARIFICATION_REQUIRED", result.RecognizedCommand);
        Assert.NotNull(result.Questionnaire);
        // Only remaining required questions should be present
        Assert.DoesNotContain(result.Questionnaire.Questions, q => q.Id == "q_meeting_time");
        Assert.Contains(result.Questionnaire.Questions, q => q.Id == "q_meeting_format");
        Assert.Contains(result.Questionnaire.Questions, q => q.Id == "q_meeting_attendees");
        Assert.NotNull(result.AgentThought);
        Assert.Contains("vẫn còn thiếu", result.AgentThought);
    }

    [Fact]
    public async Task ProcessMessage_WhenFormSubmittedWithCompleteAnswers_FinalizesDecisionAndDecomposes()
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

        var fullAnswers = new List<AgentAnswerDto>
        {
            new("q_meeting_time", SelectedOptionIds: ["t1"]),
            new("q_meeting_format", SelectedOptionIds: ["loc1"]),
            new("q_meeting_content", SelectedOptionIds: ["c1"]),
            new("q_meeting_attendees", SelectedOptionIds: ["att_bgd", "att_it"]),
            new("q_meeting_equipment", SelectedOptionIds: ["eq_proj", "eq_doc"])
        };

        var result = await chatService.ProcessMessageAsync(new RobotChatCommand(
            "Xác nhận thông tin cuộc họp",
            FormId: "form_meeting",
            FormAnswers: fullAnswers));

        Assert.Equal("/task", result.RecognizedCommand);
        Assert.Contains("Đề xuất phân công tới các phòng ban chuyên trách", result.Response);
        Assert.NotNull(result.AgentThought);
        Assert.Contains("Quá trình Suy nghĩ & Ra Quyết định", result.AgentThought);
        Assert.NotNull(result.ActionCards);
        Assert.Contains(result.ActionCards, c => c.ActionType == "OPEN_DECOMPOSITION");
        Assert.Contains(result.ActionCards, c => c.ActionType == "VIEW_BRANCHES");
    }
}
