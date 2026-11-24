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
        public Queue<string> Responses { get; } = new();

        public Task<AiModelResponse> CompleteAsync(string prompt, string systemInstructions, CancellationToken cancellationToken = default)
        {
            PromptsReceived.Add(prompt);
            return Task.FromResult(new AiModelResponse(
                Responses.Count > 0 ? Responses.Dequeue() : NextResponseContent ?? "{}",
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
            toolRegistry: new BizFlow.Infrastructure.AI.AiToolRegistry(),
            learnedChoiceStore: _learnedChoiceStore,
            conversationStateManager: _stateManager);
    }

    [Theory]
    [InlineData("Tôi đang mất tập trung, giúp tôi chọn cách làm việc")]
    [InlineData("Xin chào")]
    [InlineData("chuẩn bị cuộc họp")]
    [InlineData("Tôi cần thiết kế thí nghiệm cho ý tưởng mới")]
    public async Task EveryNaturalLanguageRequestUsesModelWithoutScenarioRouting(string message)
    {
        _modelClient.NextResponseContent = """{"reply":"Hãy bắt đầu từ mục tiêu của bạn."}""";
        var response = await CreateSut().ProcessMessageAsync(new(message));
        Assert.Equal("AGENT_REPLY", response.RecognizedCommand);
        Assert.Null(response.Questionnaire);
        Assert.Single(_modelClient.PromptsReceived);
        Assert.Contains(message, System.Text.Json.JsonDocument.Parse(_modelClient.PromptsReceived[0]).RootElement.GetProperty("message").GetString());
    }

    [Fact]
    public async Task Reply_preserves_headings_and_key_fact_emphasis_for_safe_chat_renderer()
    {
        const string formatted = "### Kết luận\n**Ưu tiên:** Hoàn thành báo cáo.\n- Kiểm tra chứng từ";
        _modelClient.NextResponseContent = System.Text.Json.JsonSerializer.Serialize(new { answerOnly = true, reply = formatted });
        var response = await CreateSut().ProcessMessageAsync(new("Tóm tắt công việc"));
        Assert.Equal(formatted, response.Response);
    }

    [Fact]
    public async Task ToolResultIsFedBackToModelBeforeFinalAnswer()
    {
        _modelClient.Responses.Enqueue("""{"toolCall":{"name":"list_tasks","arguments":{"page":1}}}""");
        _modelClient.Responses.Enqueue("""{"reply":"Danh sách hiện tại chưa có công việc."}""");
        var response = await CreateSut().ProcessMessageAsync(new("Tôi nên làm gì trước?"));
        Assert.Equal("AGENT_REPLY", response.RecognizedCommand);
        Assert.Equal(2, _modelClient.PromptsReceived.Count);
        Assert.Contains("\"total\":0", _modelClient.PromptsReceived[1]);
        Assert.Contains("list_tasks", _modelClient.PromptsReceived[1]);
    }

    [Fact]
    public async Task ConversationStateRetainsHistoryAndSeparatesConversations()
    {
        _modelClient.NextResponseContent = """{"reply":"Bạn cần thêm thông tin nào?"}""";
        var sut = CreateSut();
        await sut.ProcessMessageAsync(new("Mục tiêu Alpha", ConversationId: "a"));
        await sut.ProcessMessageAsync(new("Thêm chi tiết", ConversationId: "a"));
        Assert.Contains("Mục tiêu Alpha", System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>(_modelClient.PromptsReceived[1]).GetProperty("history")[0].GetProperty("text").GetString());
        await sut.ProcessMessageAsync(new("Mục tiêu Beta", ConversationId: "b"));
        Assert.DoesNotContain("Alpha", _modelClient.PromptsReceived[2]);
    }

    [Fact]
    public async Task RememberedPreferenceIsUsedOnLaterTurnsButOtherUsersAreExcluded()
    {
        await _learnedChoiceStore.RecordChoiceAsync(_tenantId, Guid.NewGuid(), "preference", "secret", "OTHER_USER_SECRET", "PERSONAL", _clock.GetUtcNow());
        var sut = CreateSut();
        await sut.ProcessMessageAsync(new("/remember Trả lời ngắn gọn"));
        _modelClient.NextResponseContent = """{"reply":"Được."}""";
        await sut.ProcessMessageAsync(new("Giúp tôi sắp xếp công việc"));
        var input = System.Text.Json.JsonDocument.Parse(_modelClient.PromptsReceived.Single()).RootElement;
        Assert.Equal("Trả lời ngắn gọn", input.GetProperty("preferences")[0].GetProperty("choiceValue").GetString());
        Assert.DoesNotContain("OTHER_USER_SECRET", _modelClient.PromptsReceived.Single());
    }

    [Fact]
    public async Task ModelCannotSilentlySaveInferredPreference()
    {
        _modelClient.NextResponseContent = """{"toolCall":{"name":"remember_preference","arguments":{"content":"Ưu tiên họp sáng"}}}""";
        var response = await CreateSut().ProcessMessageAsync(new("Tôi đang bận"));
        Assert.Equal("AGENT_MEMORY_PROPOSAL", response.RecognizedCommand);
        Assert.Empty(_learnedChoiceStore.Choices);
    }

    [Fact]
    public async Task DecompositionIsAnExplicitModelToolAndReturnsPreview()
    {
        _modelClient.NextResponseContent = """{"toolCall":{"name":"decompose","arguments":{"prompt":"Mục tiêu đã thống nhất"}}}""";
        var response = await CreateSut().ProcessMessageAsync(new("Lập kế hoạch giúp tôi"));
        Assert.Equal("AGENT_PLAN", response.RecognizedCommand);
        Assert.Equal("OPEN_DECOMPOSITION", Assert.Single(response.ActionCards!).ActionType);
    }

    [Fact]
    public async Task UnknownToolsAreRejectedAndLoopIsBounded()
    {
        _modelClient.NextResponseContent = """{"toolCall":{"name":"delete_database","arguments":{}}}""";
        var response = await CreateSut().ProcessMessageAsync(new("Làm giúp tôi"));
        Assert.Equal("AGENT_STEP_LIMIT", response.RecognizedCommand);
        Assert.Equal(5, _modelClient.PromptsReceived.Count);
        Assert.Contains("TOOL_NOT_ALLOWED", _modelClient.PromptsReceived[1]);
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("{}")]
    [InlineData("{\"reply\":\"\"}")]
    public async Task InvalidModelResponseNeverFallsBackToScript(string content)
    {
        _modelClient.NextResponseContent = content;
        var response = await CreateSut().ProcessMessageAsync(new("chuẩn bị họp"));
        Assert.Equal("AGENT_UNAVAILABLE", response.RecognizedCommand);
        Assert.Null(response.Questionnaire);
    }

    [Fact]
    public async Task MissingModelReportsUnavailable()
    {
        var response = await CreateSut(useLlm: false).ProcessMessageAsync(new("hôm nay tôi cần làm gì?"));
        Assert.Equal("AGENT_UNAVAILABLE", response.RecognizedCommand);
    }

    [Fact]
    public async Task Supplemental_facts_are_merged_and_questions_for_known_fields_are_suppressed()
    {
        _modelClient.Responses.Enqueue("""{"plan":{"goal":"Gửi thông báo họp ngày mai","toolName":"send_meeting_notification","arguments":{}}}""");
        _modelClient.Responses.Enqueue("""{"plan":{"goal":"Gửi thông báo họp ngày mai","toolName":"send_meeting_notification","arguments":{"topic":"Báo cáo tổng kết Quý 4","recipientScope":"ALL_COMPANY"}},"questions":[{"field":"topic","question":"Nội dung cụ thể là gì?"},{"field":"recipientScope","question":"Những phòng ban nào?"},{"field":"startsAt","question":"Họp lúc mấy giờ?"}]}""");
        var sut = CreateSut();
        await sut.ProcessMessageAsync(new("Gửi thông báo chuẩn bị họp ngày mai", ConversationId: "q4"));
        var response = await sut.ProcessMessageAsync(new("Nội dung về báo cáo tổng kết công ty Quý 4, toàn thể công ty", ConversationId: "q4"));
        Assert.Equal("AGENT_CLARIFICATION_REQUIRED", response.RecognizedCommand);
        Assert.Contains("Báo cáo tổng kết Quý 4", response.Response);
        Assert.Contains("Toàn thể công ty", response.Response);
        Assert.Contains("Họp lúc mấy giờ?", response.Response);
        Assert.DoesNotContain("Nội dung cụ thể là gì?", response.Response);
        Assert.DoesNotContain("Những phòng ban nào?", response.Response);
        var saved = await _stateManager.GetStateAsync(_tenantId, _userId, "q4");
        Assert.Contains("ALL_COMPANY", saved!.WorkPlan!.ArgumentsJson);
    }

    [Fact]
    public async Task Client_history_can_supply_newer_events_without_discarding_persisted_work_plan()
    {
        _modelClient.NextResponseContent = """{"reply":"Đã nhận thông tin"}""";
        var sut = CreateSut();
        await sut.ProcessMessageAsync(new("Lượt cũ", ConversationId: "history"));
        await sut.ProcessMessageAsync(new("Lượt mới", History: [new("user", "Thông tin mới hơn trong giao diện")], ConversationId: "history"));
        Assert.Contains("Thông tin mới hơn trong giao diện", System.Text.Json.JsonDocument.Parse(_modelClient.PromptsReceived[1]).RootElement.GetProperty("history")[0].GetProperty("text").GetString());
    }

    [Fact]
    public async Task Ignoring_supplemental_message_is_retried_before_returning_repeated_questions()
    {
        _modelClient.Responses.Enqueue("""{"plan":{"toolName":"send_meeting_notification","arguments":{}}}""");
        _modelClient.Responses.Enqueue("""{"reply":"Nội dung là gì?"}""");
        _modelClient.Responses.Enqueue("""{"plan":{"toolName":"send_meeting_notification","arguments":{"topic":"Báo cáo Quý 4","recipientScope":"ALL_COMPANY"}}}""");
        var sut = CreateSut();
        await sut.ProcessMessageAsync(new("Chuẩn bị họp ngày mai", ConversationId: "repair"));
        var response = await sut.ProcessMessageAsync(new("Báo cáo Quý 4, toàn công ty", ConversationId: "repair"));
        Assert.Equal(3, _modelClient.PromptsReceived.Count);
        Assert.Contains("WORK_PLAN_UPDATE_REQUIRED", _modelClient.PromptsReceived[2]);
        Assert.Contains("Báo cáo Quý 4", response.Response);
        Assert.DoesNotContain("Nội dung là gì?", response.Response);
    }

    [Fact]
    public async Task Model_cannot_invent_a_meeting_hour_or_use_an_assistant_summary_as_user_evidence()
    {
        _modelClient.NextResponseContent = """{"activityType":"MEETING","plan":{"toolName":"send_meeting_notification","arguments":{"topic":"Quý 4","recipientScope":"ALL_COMPANY","startsAt":"2030-01-02T09:00:00+07:00"}}}""";
        var response = await CreateSut().ProcessMessageAsync(new("Họp báo cáo Quý 4 ngày mai, toàn công ty",
            History: [new("robot", "Cuộc họp lúc 9h sáng")], ConversationId: "no-invented-hour"));
        Assert.Equal("AGENT_CLARIFICATION_REQUIRED", response.RecognizedCommand);
        Assert.Null(response.ActionCards);
        var state = await _stateManager.GetStateAsync(_tenantId, _userId, "no-invented-hour");
        Assert.DoesNotContain("startsAt", state!.WorkPlan!.ArgumentsJson);
    }

    [Fact]
    public async Task CancelledRequestPropagatesCancellation()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => CreateSut().ProcessMessageAsync(new("Xin chào"), cts.Token));
    }
}
