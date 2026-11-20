using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Application.Tasks;
using BizFlow.Domain.AI;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using Xunit;

namespace BizFlow.UnitTests.AI;

public sealed class LlmConversationalAgentAndMemoryTests
{
    private sealed class FakeTenantContext(Guid? tenantId, Guid? userId) : ITenantContext
    {
        public Guid? UserId => userId;
        public Guid? TenantId => tenantId;
    }

    private sealed class FakeTaskListReader : ITaskListReader
    {
        public Task<TaskListPage> ListAsync(TaskReadScope scope, TaskListFilter filter, CancellationToken cancellationToken) =>
            Task.FromResult(new TaskListPage([], filter.Page, filter.PageSize, 0));
    }

    private sealed class FakeAccessSnapshotProvider : IAccessSnapshotProvider
    {
        public Task<AccessSnapshot?> ResolveAsync(Guid userId, Guid? tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<AccessSnapshot?>(new AccessSnapshot(userId, tenantId, true, true, [new PermissionGrant("tasks.read.tenant", PermissionScope.Tenant)], new HashSet<Guid>(), new HashSet<Guid>()));
    }

    private sealed class FakeSecurityAuditWriter : ISecurityAuditWriter
    {
        public Task RecordDeniedAccessAsync(Guid? userId, Guid? tenantId, string permission, AccessDenial reason, CancellationToken cancellationToken) =>
            Task.CompletedTask;
    }

    private sealed class FakeNotificationInboxStore : INotificationInboxStore
    {
        public Task<NotificationPage> ListAsync(Guid tenantId, Guid recipientId, NotificationFilter filter, CancellationToken cancellationToken) =>
            Task.FromResult(new NotificationPage([], 1, 10, 0, 0));
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
        public Task<TaskDecompositionPreviewResult> PreviewAsync(TaskDecompositionPreviewCommand command, CancellationToken cancellationToken = default) =>
            Task.FromResult(new TaskDecompositionPreviewResult(
                command.Prompt,
                [
                    new TaskDecompositionProposalDto("Tác vụ 1", "Mô tả 1", "Assignment", null, "Phòng IT", "HIGH", ["B1", "B2"], "Phân rã tự động")
                ]));

        public Task<ConfirmTaskDecompositionResponse> ConfirmAsync(ConfirmTaskDecompositionCommand command, CancellationToken cancellationToken = default) =>
            Task.FromResult(new ConfirmTaskDecompositionResponse(command.Items.Count, []));
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class MockAiModelClient : IAiModelClient
    {
        public bool IsConfigured { get; set; } = true;
        public string? NextResponseContent { get; set; }
        public List<string> PromptsReceived { get; } = [];

        public Task<AiModelResponse> CompleteAsync(string prompt, string systemInstructions, CancellationToken cancellationToken = default)
        {
            PromptsReceived.Add(prompt);
            return Task.FromResult(new AiModelResponse(
                NextResponseContent ?? "{}",
                "mock-gpt-4o",
                50,
                100,
                200));
        }
    }

    private sealed class FakeConversationStateManager : IConversationStateManager
    {
        private readonly Dictionary<string, ConversationState> _storage = new(StringComparer.OrdinalIgnoreCase);

        public Task<ConversationState?> GetStateAsync(Guid tenantId, Guid userId, string conversationId, CancellationToken cancellationToken = default)
        {
            var key = $"{tenantId}:{userId}:{conversationId}";
            _storage.TryGetValue(key, out var state);
            return Task.FromResult(state);
        }

        public Task SaveStateAsync(ConversationState state, CancellationToken cancellationToken = default)
        {
            var key = $"{state.TenantId}:{state.UserId}:{state.ConversationId}";
            _storage[key] = state;
            return Task.CompletedTask;
        }

        public Task ClearStateAsync(Guid tenantId, Guid userId, string conversationId, CancellationToken cancellationToken = default)
        {
            var key = $"{tenantId}:{userId}:{conversationId}";
            _storage.Remove(key);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeLearnedChoiceStore : IAiLearnedChoiceStore
    {
        public List<LearnedChoiceDto> Choices { get; } = [];

        public Task<AiLearnedChoice> RecordChoiceAsync(Guid tenantId, Guid? userId, string semanticKey, string choiceValue, string choiceLabel, string scope, DateTimeOffset now, CancellationToken cancellationToken = default)
        {
            var normKey = semanticKey.Trim().ToLowerInvariant();
            var normVal = choiceValue.Trim();
            var existing = Choices.FirstOrDefault(c => c.TenantId == tenantId && c.SemanticKey == normKey && c.ChoiceValue == normVal);
            if (existing != null)
            {
                var idx = Choices.IndexOf(existing);
                Choices[idx] = existing with { UsageCount = existing.UsageCount + 1, LastUsedAt = now };
            }
            else
            {
                Choices.Add(new LearnedChoiceDto(Guid.NewGuid(), tenantId, userId, normKey, normVal, choiceLabel, scope, 1, now, now));
            }

            var entity = AiLearnedChoice.Create(tenantId, userId, semanticKey, choiceValue, choiceLabel, scope, now);
            return Task.FromResult(entity);
        }

        public Task<IReadOnlyList<LearnedChoiceDto>> GetRelevantChoicesAsync(Guid tenantId, Guid? userId, string semanticKey, int limit = 5, CancellationToken cancellationToken = default)
        {
            var normKey = semanticKey.Trim().ToLowerInvariant();
            var results = Choices
                .Where(c => c.TenantId == tenantId && c.SemanticKey == normKey && (c.Scope == "TENANT" || c.UserId == userId))
                .OrderByDescending(c => c.UsageCount)
                .Take(limit)
                .ToList();
            return Task.FromResult<IReadOnlyList<LearnedChoiceDto>>(results);
        }

        public Task<IReadOnlyList<LearnedChoiceDto>> ListChoicesAsync(Guid tenantId, Guid? userId, string? semanticKey = null, CancellationToken cancellationToken = default)
        {
            var results = Choices
                .Where(c => c.TenantId == tenantId && (c.Scope == "TENANT" || c.UserId == userId))
                .ToList();
            return Task.FromResult<IReadOnlyList<LearnedChoiceDto>>(results);
        }

        public Task<bool> DeleteChoiceAsync(Guid tenantId, Guid? userId, Guid choiceId, CancellationToken cancellationToken = default)
        {
            var count = Choices.RemoveAll(c => c.Id == choiceId && c.TenantId == tenantId);
            return Task.FromResult(count > 0);
        }

        public Task<bool> PromoteToTenantScopeAsync(Guid tenantId, Guid choiceId, CancellationToken cancellationToken = default)
        {
            var existing = Choices.FirstOrDefault(c => c.Id == choiceId && c.TenantId == tenantId);
            if (existing == null) return Task.FromResult(false);
            var idx = Choices.IndexOf(existing);
            Choices[idx] = existing with { Scope = "TENANT", UserId = null };
            return Task.FromResult(true);
        }
    }

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 11, 11, 14, 0, 0, TimeSpan.Zero));
    private readonly FakeTaskListReader _taskReader = new();
    private readonly FakeNotificationInboxStore _notifStore = new();
    private readonly FakeDecompositionService _decomposition = new();
    private readonly MockAiModelClient _modelClient = new();
    private readonly FakeConversationStateManager _stateManager = new();
    private readonly FakeLearnedChoiceStore _learnedChoiceStore = new();

    private RobotChatService CreateSut(bool useLlm = true)
    {
        var tenantContext = new FakeTenantContext(_tenantId, _userId);
        var snapshotProvider = new FakeAccessSnapshotProvider();
        var auditWriter = new FakeSecurityAuditWriter();

        var taskList = new TaskListQuery(tenantContext, snapshotProvider, auditWriter, _taskReader);
        var membership = new TenantMembershipAuthorizer(tenantContext, snapshotProvider, auditWriter);
        var inbox = new NotificationInbox(tenantContext, membership, auditWriter, _notifStore, _clock, new FakeNotificationUpdates());

        return new RobotChatService(
            tenantContext,
            taskList,
            inbox,
            _decomposition,
            _clock,
            modelClient: useLlm ? _modelClient : null,
            learnedChoiceStore: _learnedChoiceStore,
            conversationStateManager: _stateManager);
    }

    [Fact]
    public async Task TestC1_WhenLlmReturnsDynamicQuestions_BuildsStructuredClarificationForm()
    {
        var sut = CreateSut(useLlm: true);

        _modelClient.NextResponseContent = """
        {
          "intent": "WORK_PLAN",
          "isComplete": false,
          "topic": "Xây dựng cổng thanh toán trực tuyến",
          "riskAnalysis": "Nếu tích hợp cổng thanh toán mà chưa xác định ngân hàng đối tác và tiêu chuẩn PCI-DSS, dự án sẽ có nguy cơ rủi ro bảo mật tài chính.",
          "agentThought": "Yêu cầu cần làm rõ đơn vị trung gian thanh toán và hạn định go-live.",
          "dynamicQuestions": [
            {
              "id": "q_payment_gateway",
              "title": "Cổng thanh toán hoặc ngân hàng đối tác",
              "type": "single_choice",
              "options": [
                { "id": "vnpay", "label": "VNPay QR", "isDefault": true },
                { "id": "momo", "label": "MoMo Business" },
                { "id": "stripe", "label": "Stripe International" }
              ],
              "required": true,
              "allowCustomInput": true
            },
            {
              "id": "q_target_golive",
              "title": "Hạn định hoàn thành (Go-live)",
              "type": "single_choice",
              "options": [
                { "id": "2w", "label": "Trong 2 tuần" },
                { "id": "1m", "label": "Trong 1 tháng", "isDefault": true }
              ],
              "required": true,
              "allowCustomInput": true
            }
          ]
        }
        """;

        var command = new RobotChatCommand("Lên kế hoạch tích hợp thanh toán trực tuyến cho website");
        var result = await sut.ProcessMessageAsync(command);

        Assert.Equal("AGENT_CLARIFICATION_REQUIRED", result.RecognizedCommand);
        Assert.NotNull(result.Questionnaire);
        Assert.Equal(2, result.Questionnaire.Questions.Count);
        Assert.Equal("q_payment_gateway", result.Questionnaire.Questions[0].Id);
        Assert.Equal("single_choice", result.Questionnaire.Questions[0].Type);
        Assert.True(result.Questionnaire.Questions[0].AllowCustomInput);
        Assert.Contains("PCI-DSS", result.Questionnaire.RiskAnalysis);
        Assert.Contains("đơn vị trung gian", result.AgentThought);
    }

    [Fact]
    public async Task TestC2_MultiTurnSessionState_RetainsExtractedSlotsAcrossTurns()
    {
        var sut = CreateSut(useLlm: false);
        var convId = "session_test_123";

        // Turn 1: Meeting prompt without full details -> triggers clarification and saves session
        var turn1 = await sut.ProcessMessageAsync(new RobotChatCommand("Chuẩn bị cuộc họp với các phòng ban", ConversationId: convId));
        Assert.Equal("AGENT_CLARIFICATION_REQUIRED", turn1.RecognizedCommand);

        var savedState = await _stateManager.GetStateAsync(_tenantId, _userId, convId);
        Assert.NotNull(savedState);
        Assert.NotNull(savedState.PendingClarification);

        // Turn 2: User provides follow-up details in natural language
        var turn2 = await sut.ProcessMessageAsync(new RobotChatCommand("Họp lúc 9h sáng mai tại phòng họp Lớn, có Ban Giám đốc và IT tham gia", ConversationId: convId));
        Assert.NotNull(turn2.ActionCards);
        Assert.Contains(turn2.ActionCards, c => c.ActionType == "OPEN_DECOMPOSITION");

        // Session state should now be cleared
        var clearedState = await _stateManager.GetStateAsync(_tenantId, _userId, convId);
        Assert.Null(clearedState);
    }

    [Fact]
    public async Task TestC3_WhenLlmDeterminesCompleteness_ExecutesDecompositionDirectly()
    {
        var sut = CreateSut(useLlm: true);

        _modelClient.NextResponseContent = """
        {
          "intent": "WORK_PLAN",
          "isComplete": true,
          "topic": "Chuyển đổi văn phòng mới",
          "extractedSlots": {
            "time": "Chủ nhật tuần này",
            "destination": "Tầng 5 Keangnam",
            "department": "Phòng Hành chính chủ trì"
          },
          "agentThought": "Thông tin đã đầy đủ về thời gian, địa điểm và phòng ban chủ trì. Tiến hành phân rã công việc."
        }
        """;

        var command = new RobotChatCommand("Chuyển văn phòng mới vào chủ nhật tuần này sang Tầng 5 Keangnam, Phòng Hành chính chủ trì");
        var result = await sut.ProcessMessageAsync(command);

        Assert.Equal("/task", result.RecognizedCommand);
        Assert.NotNull(result.ActionCards);
        Assert.Contains(result.ActionCards, c => c.ActionType == "OPEN_DECOMPOSITION");
    }

    [Fact]
    public async Task TestD1_WhenUserSubmitsQuestionnaireAnswers_RecordsChoicesInLearnedChoiceStore()
    {
        var sut = CreateSut(useLlm: false);

        var answers = new List<AgentAnswerDto>
        {
            new("q_meeting_format", ["loc1"], CustomText: "Phòng họp Ban Điều Hành Tầng 8"),
            new("q_meeting_time", ["t1"])
        };

        var command = new RobotChatCommand(
            "Xác nhận thông tin",
            FormId: "form_meeting",
            FormAnswers: answers);

        var result = await sut.ProcessMessageAsync(command);

        Assert.NotEmpty(_learnedChoiceStore.Choices);
        var recordedCustom = _learnedChoiceStore.Choices.FirstOrDefault(c => c.ChoiceValue.Contains("Phòng họp Ban Điều Hành"));
        Assert.NotNull(recordedCustom);
        Assert.Equal("meeting_format", recordedCustom.SemanticKey);
    }

    [Fact]
    public async Task TestD2_WhenLearnedChoicesExist_EnrichesOptionsWithFrequentlyUsedBadgeAndRecommendation()
    {
        // Pre-seed a frequently used choice in the memory store
        await _learnedChoiceStore.RecordChoiceAsync(
            _tenantId,
            _userId,
            "meeting_format",
            "Phòng VIP Tầng 10",
            "Phòng VIP Tầng 10",
            "PERSONAL",
            _clock.GetUtcNow());

        var sut = CreateSut(useLlm: false);

        var result = await sut.ProcessMessageAsync(new RobotChatCommand("Tổ chức cuộc họp ban giám đốc"));

        Assert.NotNull(result.Questionnaire);
        var formatQuestion = result.Questionnaire.Questions.FirstOrDefault(q => q.Id == "q_meeting_format");
        Assert.NotNull(formatQuestion);
        Assert.NotNull(formatQuestion.Options);

        var learnedOpt = formatQuestion.Options.FirstOrDefault(o => o.Label.Contains("Phòng VIP Tầng 10"));
        Assert.NotNull(learnedOpt);
        Assert.Contains("Thường dùng", learnedOpt.Label);
        Assert.True(learnedOpt.IsDefault);
    }

    [Fact]
    public async Task TestD3_LearnedChoices_PromoteToTenantScope_AllowsTenantWideReuse()
    {
        var otherUserId = Guid.NewGuid();

        // 1. User records a personal choice
        var choice = await _learnedChoiceStore.RecordChoiceAsync(
            _tenantId,
            _userId,
            "deliverable",
            "Tài liệu Kiến trúc Phần mềm (SAD)",
            "Tài liệu Kiến trúc Phần mềm (SAD)",
            "PERSONAL",
            _clock.GetUtcNow());

        // Other user cannot see personal choice
        var otherUserChoices = await _learnedChoiceStore.GetRelevantChoicesAsync(_tenantId, otherUserId, "deliverable");
        Assert.Empty(otherUserChoices);

        // 2. Admin promotes choice to tenant scope
        var promoted = await _learnedChoiceStore.PromoteToTenantScopeAsync(_tenantId, _learnedChoiceStore.Choices[0].Id);
        Assert.True(promoted);

        // Now other user can see the tenant-wide learned choice!
        var choicesAfterPromotion = await _learnedChoiceStore.GetRelevantChoicesAsync(_tenantId, otherUserId, "deliverable");
        Assert.NotEmpty(choicesAfterPromotion);
        Assert.Equal("TENANT", choicesAfterPromotion[0].Scope);
    }
}
