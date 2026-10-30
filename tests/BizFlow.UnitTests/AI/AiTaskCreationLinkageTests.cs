using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.AI;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.AI;
using Xunit;

namespace BizFlow.UnitTests.AI;

/// <summary>
/// Master Test Suite for AI Agent capabilities, heuristic fallbacks,
/// and seamless end-to-end integration linkage with Task and Request creation.
/// </summary>
public sealed class AiTaskCreationLinkageTests
{
    private sealed class FakeTenantContext(Guid? tenantId, Guid? userId) : ITenantContext
    {
        public Guid? UserId => userId;
        public Guid? TenantId => tenantId;
    }

    private sealed class FakeResourceAuthorizer : IResourceAuthorizer
    {
        public List<string> AuthorizedPermissions { get; } = [];
        public bool DenyTasksCreate { get; set; }

        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default)
        {
            if (DenyTasksCreate && permission == "tasks.create")
            {
                throw new ApplicationFault(FaultKind.Forbidden, "FORBIDDEN", "Permission tasks.create denied.");
            }
            AuthorizedPermissions.Add(permission);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAiStore : IAiStore
    {
        public List<AIInteraction> Interactions { get; } = [];
        public List<AIRecommendation> Recommendations { get; } = [];
        public List<AIAgentAction> Actions { get; } = [];
        public Dictionary<Guid, HumanDecision> Decisions { get; } = [];

        public Task<AIInteraction> RecordInteractionAsync(
            Guid tenantId, Guid userId, string feature, string modelName,
            string? inputRef, string? outputRef, AIInteractionStatus status,
            int? latencyMs, string? tokenUsage, CancellationToken cancellationToken = default)
        {
            var interaction = AIInteraction.Create(tenantId, userId, feature, modelName, inputRef, outputRef, status, latencyMs, tokenUsage, DateTimeOffset.UtcNow);
            Interactions.Add(interaction);
            return Task.FromResult(interaction);
        }

        public Task<AIRecommendation> RecordRecommendationAsync(
            Guid tenantId, Guid aiInteractionId, RecommendationObjectType objectType,
            Guid? objectId, string recommendationType, string payloadJson,
            decimal? confidence, CancellationToken cancellationToken = default)
        {
            var recommendation = AIRecommendation.Create(tenantId, aiInteractionId, objectType, objectId, recommendationType, payloadJson, confidence);
            Recommendations.Add(recommendation);
            return Task.FromResult(recommendation);
        }

        public Task<AIAgentAction> RecordActionAsync(
            Guid tenantId, Guid aiInteractionId, string toolName,
            string argsJson, AgentAuthorizationResult authorizationResult,
            AgentExecutionStatus executionStatus, string? resultRef,
            CancellationToken cancellationToken = default)
        {
            var action = AIAgentAction.Create(tenantId, aiInteractionId, toolName, argsJson, authorizationResult, executionStatus, resultRef, DateTimeOffset.UtcNow);
            Actions.Add(action);
            return Task.FromResult(action);
        }

        public Task RecordDecisionAsync(
            Guid tenantId, Guid recommendationId, HumanDecision decision,
            Guid reviewerId, CancellationToken cancellationToken = default)
        {
            Decisions[recommendationId] = decision;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAiContextReader(Guid tenantId) : IAiContextReader
    {
        public Task<AiTenantContextData> GetTenantContextAsync(Guid tId, CancellationToken cancellationToken = default)
        {
            var deptIt = new AiDepartmentInfo(Guid.NewGuid(), "IT", "Phòng Công nghệ");
            var service = new AiServiceInfo(Guid.NewGuid(), "SRV_SEC", "Security", "An toàn thông tin", [
                new AiCategoryInfo(Guid.NewGuid(), "CAT_AUDIT", "Kiểm toán an ninh")
            ]);
            return Task.FromResult(new AiTenantContextData(tenantId, [service], [deptIt], [], [], []));
        }

        public Task<AiTaskContextData?> GetTaskContextAsync(Guid tId, Guid taskId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AiTaskContextData?>(null);

        public Task<AiRequestContextData?> GetRequestContextAsync(Guid tId, Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AiRequestContextData?>(null);
    }

    private sealed class FakeAiToolRegistry : IAiToolRegistry
    {
        public AiToolDefinition? GetTool(string name) => new(name, name, "tasks.create", true);
        public IReadOnlyList<AiToolDefinition> GetRegisteredTools() => [];
    }

    private sealed class FakeRequestSplitStore : IRequestSplitStore
    {
        public Task<WorkRequest?> GetParentRequestAsync(Guid tenantId, Guid parentRequestId, CancellationToken cancellationToken) =>
            Task.FromResult<WorkRequest?>(null);

        public Task<IReadOnlyList<WorkRequest>> CreateChildRequestsAsync(
            Guid tenantId, Guid parentRequestId, IReadOnlyList<WorkRequest> childRequests,
            AuditLog audit, CancellationToken cancellationToken) =>
            Task.FromResult(childRequests);
    }

    private sealed class FakeAiActionDispatcher : IAiActionDispatcher
    {
        public Task<object?> DispatchToolAsync(Guid tenantId, Guid userId, string toolName, string argsJson, CancellationToken cancellationToken) =>
            Task.FromResult<object?>(new { dispatched = true });
    }

    private sealed class FakeDocumentSummarizerStore : IAiDocumentSummarizerStore
    {
        public List<CreatedSummaryTaskResult> PersistedTasks { get; } = [];

        public Task<(string FileName, string? Content)?> GetAttachmentTextAsync(Guid tenantId, Guid attachmentId, CancellationToken cancellationToken) =>
            Task.FromResult<(string FileName, string? Content)?>(("BienBan.txt", "Nội dung cần thực thi: Cấu hình backup dữ liệu hàng ngày."));

        public Task<IReadOnlyList<CreatedSummaryTaskResult>> PersistSummaryTasksAsync(
            Guid tenantId, Guid userId, string sourceTitle, IReadOnlyList<ExtractedActionItem> items, DateTimeOffset now, CancellationToken cancellationToken)
        {
            var results = items.Select(item => new CreatedSummaryTaskResult(
                TaskId: Guid.NewGuid(),
                Title: item.Title,
                Priority: item.SuggestedPriority ?? "MEDIUM",
                DepartmentId: Guid.NewGuid(),
                Status: "DRAFT"
            )).ToList();
            PersistedTasks.AddRange(results);
            return Task.FromResult<IReadOnlyList<CreatedSummaryTaskResult>>(results);
        }
    }

    private sealed class FakeTaskDecompositionStore : ITaskDecompositionStore
    {
        public List<CreatedDecompositionItemDto> CreatedTasks { get; } = [];

        public Task<IReadOnlyList<CreatedDecompositionItemDto>> PersistDecompositionBatchAsync(
            Guid tenantId,
            Guid actorId,
            IReadOnlyList<ConfirmTaskDecompositionItem> items,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var results = items.Select((it, idx) => new CreatedDecompositionItemDto(
                Id: Guid.NewGuid(),
                Type: it.Type,
                Title: it.Title,
                Status: "Draft",
                DepartmentId: it.DepartmentId
            )).ToList();
            CreatedTasks.AddRange(results);
            return Task.FromResult<IReadOnlyList<CreatedDecompositionItemDto>>(results);
        }
    }

    [Fact]
    public async Task AiTaskDraft_To_DomainTaskCreation_Linkage_PreservesAllInvariants()
    {
        // 1. Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var authorizer = new FakeResourceAuthorizer();
        var context = new FakeTenantContext(tenantId, userId);
        var store = new FakeAiStore();
        var contextReader = new FakeAiContextReader(tenantId);
        var toolRegistry = new FakeAiToolRegistry();
        var splitStore = new FakeRequestSplitStore();
        var dispatcher = new FakeAiActionDispatcher();

        var service = new AiAssistanceService(
            context,
            authorizer,
            store,
            contextReader,
            toolRegistry,
            splitStore,
            dispatcher,
            TimeProvider.System,
            modelClient: null);

        // 2. Act: AI assists task creation from prompt
        var prompt = "Cần triển khai tường lửa WAF và cấu hình chống DDoS cho cổng thanh toán";
        var suggestion = await service.AssistTaskCreationAsync(new TaskAssistanceCommand(prompt));

        // 3. Assert AI Output
        Assert.NotNull(suggestion);
        Assert.NotEmpty(suggestion.Title);
        Assert.True(suggestion.Title.Length <= 300);
        Assert.NotEmpty(suggestion.Description);
        Assert.NotEmpty(suggestion.ChecklistItems);
        Assert.True(suggestion.Confidence > 0);

        // 4. Linkage: Validate AI Output directly creates a valid Domain Task entity
        var priority = Enum.Parse<TaskPriority>(suggestion.Priority, ignoreCase: true);

        var domainTask = WorkTask.CreateDraft(
            tenantId: tenantId,
            creatorId: userId,
            title: suggestion.Title,
            now: DateTimeOffset.UtcNow,
            description: suggestion.Description,
            priority: priority,
            deadline: DateTimeOffset.UtcNow.AddDays(suggestion.SuggestedDurationDays ?? 3),
            requestId: null);

        Assert.NotNull(domainTask);
        Assert.Equal(suggestion.Title, domainTask.Title);
        Assert.Equal(priority, domainTask.Priority);
        Assert.Equal(TaskState.Draft, domainTask.Status);
    }

    [Fact]
    public async Task AiDecomposition_To_Repository_BulkTaskCreation_BR028()
    {
        // 1. Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var authorizer = new FakeResourceAuthorizer();
        var context = new FakeTenantContext(tenantId, userId);
        var store = new FakeAiStore();
        var contextReader = new FakeAiContextReader(tenantId);
        var decompStore = new FakeTaskDecompositionStore();

        var service = new AiTaskDecompositionService(
            context,
            authorizer,
            contextReader,
            decompStore,
            TimeProvider.System,
            modelClient: null);

        var prompt = "1. Kiểm tra cấu hình router\n2. Cập nhật firmware thiết bị mạng\n3. Báo cáo đánh giá";

        // 2. Act Phase 1: Preview without writing to DB (BR-028)
        var preview = await service.PreviewAsync(new TaskDecompositionPreviewCommand(prompt));

        Assert.NotNull(preview);
        Assert.Equal(3, preview.Proposals.Count);
        Assert.Empty(decompStore.CreatedTasks); // Zero DB writes in preview!

        // 3. Act Phase 2: Human reviews & confirms bulk creation
        var itemsToConfirm = preview.Proposals.Select(p => new ConfirmTaskDecompositionItem(
            Title: p.Title,
            Description: p.Description,
            Type: p.Type,
            DepartmentId: p.DepartmentId,
            Priority: p.Priority,
            ChecklistItems: p.ChecklistItems
        )).ToList();

        var confirmCommand = new ConfirmTaskDecompositionCommand(Items: itemsToConfirm);
        var confirmResult = await service.ConfirmAsync(confirmCommand);

        // 4. Assert
        Assert.NotNull(confirmResult);
        Assert.Equal(3, confirmResult.TotalCreated);
        Assert.Equal(3, decompStore.CreatedTasks.Count);
        Assert.All(decompStore.CreatedTasks, t => Assert.Equal("Draft", t.Status));
        Assert.Contains("tasks.create", authorizer.AuthorizedPermissions);
    }

    [Fact]
    public async Task AiDocumentSummarizer_To_DraftTaskSpawning_Linkage()
    {
        // 1. Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var authorizer = new FakeResourceAuthorizer();
        var context = new FakeTenantContext(tenantId, userId);
        var store = new FakeAiStore();
        var summarizerStore = new FakeDocumentSummarizerStore();

        var service = new AiDocumentSummarizerService(
            context,
            authorizer,
            store,
            summarizerStore,
            TimeProvider.System,
            modelClient: null);

        // 2. Act: Summarize Directive Document
        var documentText = """
            Quyết định số 105 về an toàn bảo mật thông tin.
            Yêu cầu đội ngũ kiểm toán lại toàn bộ phân quyền vai trò người dùng trước ngày 15.
            Bộ phận CNTT tiến hành mã hóa dữ liệu cơ sở dữ liệu khi lưu trữ (at-rest encryption).
            Lưu ý nguy cơ rủi ro rò rỉ dữ liệu khách hàng nếu không tuân thủ.
            """;

        var summary = await service.SummarizeDocumentAsync(new DocumentSummarizeCommand(
            RawText: documentText,
            AttachmentId: null,
            Mode: "COMPREHENSIVE",
            Language: "vi"));

        Assert.NotNull(summary);
        Assert.NotEmpty(summary.ActionItems);

        // 3. Linkage: Spawn tasks directly into database from extracted action items
        var spawnCommand = new CreateTasksFromSummaryCommand(
            SourceTitle: summary.SourceTitle,
            SourceReference: "QuyetDinh105.pdf",
            Items: summary.ActionItems.ToList());

        var spawnResponse = await service.CreateTasksFromSummaryAsync(spawnCommand);

        // 4. Assert: Tasks persisted with correct initial status
        Assert.NotNull(spawnResponse);
        Assert.True(spawnResponse.TotalCreated >= 1);
        Assert.Equal(spawnResponse.TotalCreated, summarizerStore.PersistedTasks.Count);
        Assert.All(summarizerStore.PersistedTasks, t => Assert.Equal("DRAFT", t.Status));
    }

    [Fact]
    public async Task AiTaskCreation_WhenMissingPermission_ThrowsForbiddenException_SecurityGuarantee()
    {
        // Arrange: User has NO tasks.create permission
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var authorizer = new FakeResourceAuthorizer { DenyTasksCreate = true };
        var context = new FakeTenantContext(tenantId, userId);
        var store = new FakeAiStore();
        var contextReader = new FakeAiContextReader(tenantId);
        var decompStore = new FakeTaskDecompositionStore();

        var service = new AiTaskDecompositionService(
            context,
            authorizer,
            contextReader,
            decompStore,
            TimeProvider.System);

        var preview = await service.PreviewAsync(new TaskDecompositionPreviewCommand("1. Task A\n2. Task B"));

        var itemsToConfirm = preview.Proposals.Select(p => new ConfirmTaskDecompositionItem(
            Title: p.Title,
            Description: p.Description,
            Type: p.Type,
            DepartmentId: p.DepartmentId,
            Priority: p.Priority,
            ChecklistItems: p.ChecklistItems
        )).ToList();

        // Act & Assert: Attempting to confirm task creation without authority is strictly blocked
        var ex = await Assert.ThrowsAsync<ApplicationFault>(() =>
            service.ConfirmAsync(new ConfirmTaskDecompositionCommand(Items: itemsToConfirm)));

        Assert.Equal(FaultKind.Forbidden, ex.Kind);
        Assert.Empty(decompStore.CreatedTasks); // Zero tasks written!
    }

    [Fact]
    public async Task AiServices_WhenOfflineOrNoApiKey_FallbackGracefullyWithoutCrashing()
    {
        // Arrange: Model client is null (offline/dev environment)
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var authorizer = new FakeResourceAuthorizer();
        var context = new FakeTenantContext(tenantId, userId);
        var store = new FakeAiStore();
        var contextReader = new FakeAiContextReader(tenantId);
        var toolRegistry = new FakeAiToolRegistry();
        var splitStore = new FakeRequestSplitStore();
        var dispatcher = new FakeAiActionDispatcher();

        var service = new AiAssistanceService(
            context,
            authorizer,
            store,
            contextReader,
            toolRegistry,
            splitStore,
            dispatcher,
            TimeProvider.System,
            modelClient: null);

        // Act 1: Task assistance fallback
        var taskDraft = await service.AssistTaskCreationAsync(new TaskAssistanceCommand("Khởi tạo API microservice"));
        Assert.NotNull(taskDraft);
        Assert.NotEmpty(taskDraft.Title);

        // Act 2: Multi-intent fallback
        var multiIntent = await service.AnalyzeRequestMultiIntentAsync(new RequestMultiIntentCommand(
            RequestId: null,
            Title: "Sửa máy in phòng họp đồng thời cấp phát laptop",
            Description: "Cần bảo trì máy in phòng họp tầng 3 và ngoài ra cấp phát laptop mới cho nhân sự."));
        Assert.NotNull(multiIntent);
        Assert.True(multiIntent.IsMultiIntent);

        // Act 3: Request routing fallback
        var routing = await service.RecommendRequestRoutingAsync(new RequestRoutingRecommendationCommand(
            RequestId: null,
            Title: "Kiểm toán bảo mật",
            Description: "Rà soát lỗ hổng an ninh mạng"));
        Assert.NotNull(routing);
        Assert.NotNull(routing.Priority);
        Assert.True(routing.Confidence > 0);
    }
}
