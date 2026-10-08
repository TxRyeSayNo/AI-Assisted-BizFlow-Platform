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
using Xunit;

namespace BizFlow.UnitTests.AI;

public sealed class AiAssistanceServiceTests
{
    private sealed class FakeTenantContext(Guid? tenantId, Guid? userId) : ITenantContext
    {
        public Guid? UserId => userId;
        public Guid? TenantId => tenantId;
    }

    private sealed class FakeResourceAuthorizer : IResourceAuthorizer
    {
        public List<string> AuthorizedPermissions { get; } = [];

        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default)
        {
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

    private sealed class FakeAiContextReader : IAiContextReader
    {
        public AiTenantContextData TenantData { get; set; }
        public AiTaskContextData? TaskData { get; set; }
        public AiRequestContextData? RequestData { get; set; }

        public FakeAiContextReader(Guid tenantId)
        {
            var serviceId = Guid.NewGuid();
            var categoryId = Guid.NewGuid();
            var deptId = Guid.NewGuid();
            var userId1 = Guid.NewGuid();
            var userId2 = Guid.NewGuid();

            TenantData = new AiTenantContextData(
                tenantId,
                Services:
                [
                    new AiServiceInfo(serviceId, "IT_SUP", "Hỗ trợ công nghệ thông tin", "Dịch vụ hỗ trợ kỹ thuật IT",
                        [new AiCategoryInfo(categoryId, "HARDWARE", "Cấp phát và sửa chữa thiết bị")])
                ],
                Departments:
                [
                    new AiDepartmentInfo(deptId, "IT_DEPT", "Phòng Công nghệ thông tin")
                ],
                Users:
                [
                    new AiUserInfo(userId1, "Nguyen Van A", "a@bizflow.io", deptId, "EMPLOYEE", 1),
                    new AiUserInfo(userId2, "Tran Thi B", "b@bizflow.io", deptId, "EMPLOYEE", 4)
                ],
                Workflows:
                [
                    new AiWorkflowInfo(Guid.NewGuid(), Guid.NewGuid(), "Quy trình xử lý tiêu chuẩn", 1)
                ],
                Slas:
                [
                    new AiSlaInfo(Guid.NewGuid(), Guid.NewGuid(), "SLA 48 giờ", 2880)
                ]);
        }

        public Task<AiTenantContextData> GetTenantContextAsync(Guid tenantId, CancellationToken cancellationToken = default) =>
            Task.FromResult(TenantData);

        public Task<AiTaskContextData?> GetTaskContextAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default) =>
            Task.FromResult(TaskData);

        public Task<AiRequestContextData?> GetRequestContextAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult(RequestData);
    }

    private sealed class FakeAiToolRegistry : IAiToolRegistry
    {
        public AiToolDefinition? GetTool(string name) => name switch
        {
            "create_task_draft" => new("create_task_draft", "Create draft", "tasks.create", true),
            "assign_task" => new("assign_task", "Assign task", "tasks.assign", true),
            "add_comment" => new("add_comment", "Add comment", "comments.create", false),
            _ => null
        };

        public IReadOnlyList<AiToolDefinition> GetRegisteredTools() =>
        [
            new("create_task_draft", "Create draft", "tasks.create", true),
            new("assign_task", "Assign task", "tasks.assign", true),
            new("add_comment", "Add comment", "comments.create", false)
        ];
    }

    private sealed class FakeRequestSplitStore : IRequestSplitStore
    {
        public WorkRequest? ParentRequest { get; set; }
        public List<WorkRequest> CreatedChildren { get; } = [];

        public Task<WorkRequest?> GetParentRequestAsync(Guid tenantId, Guid parentRequestId, CancellationToken cancellationToken) =>
            Task.FromResult(ParentRequest);

        public Task<IReadOnlyList<WorkRequest>> CreateChildRequestsAsync(
            Guid tenantId, Guid parentRequestId, IReadOnlyList<WorkRequest> childRequests,
            AuditLog audit, CancellationToken cancellationToken)
        {
            CreatedChildren.AddRange(childRequests);
            return Task.FromResult(childRequests);
        }
    }

    private sealed class FakeAiActionDispatcher : IAiActionDispatcher
    {
        public Task<object?> DispatchToolAsync(
            Guid tenantId, Guid userId, string toolName,
            string argsJson, CancellationToken cancellationToken) =>
            Task.FromResult<object?>(new { dispatched = true, tool = toolName });
    }

    private static (AiAssistanceService Service, FakeAiStore Store, FakeAiContextReader Reader, FakeRequestSplitStore SplitStore, FakeResourceAuthorizer Authorizer) CreateTestContext()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var tenantContext = new FakeTenantContext(tenantId, userId);
        var authorizer = new FakeResourceAuthorizer();
        var store = new FakeAiStore();
        var reader = new FakeAiContextReader(tenantId);
        var toolRegistry = new FakeAiToolRegistry();
        var splitStore = new FakeRequestSplitStore();
        var dispatcher = new FakeAiActionDispatcher();
        var clock = TimeProvider.System;

        var service = new AiAssistanceService(
            tenantContext, authorizer, store, reader, toolRegistry,
            splitStore, dispatcher, clock);

        return (service, store, reader, splitStore, authorizer);
    }

    [Fact]
    public async Task AssistTaskCreationAsync_WithValidPrompt_ReturnsStructuredDraftAndPersistsRecommendation()
    {
        var (service, store, reader, _, authorizer) = CreateTestContext();

        var result = await service.AssistTaskCreationAsync(new("Cần cấp phát laptop mới khẩn cấp cho nhân viên IT mới tuyển"));

        Assert.NotNull(result);
        Assert.Contains("laptop", result.Title, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("HIGH", result.Priority);
        Assert.NotNull(result.DepartmentId);
        Assert.Equal("Nguyen Van A", result.AssigneeName); // Selected user with lowest active task count (1 vs 4)
        Assert.NotEmpty(result.ChecklistItems);
        Assert.True(result.Confidence >= 0.8m);

        Assert.Contains("ai.001", authorizer.AuthorizedPermissions);
        Assert.Single(store.Interactions);
        Assert.Equal("ai.001", store.Interactions[0].Feature);
        Assert.Single(store.Recommendations);
        Assert.Equal("DRAFT", store.Recommendations[0].RecommendationType);
    }

    [Fact]
    public async Task AssistTaskCreationAsync_EmptyPrompt_ThrowsValidationFault()
    {
        var (service, _, _, _, _) = CreateTestContext();

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.AssistTaskCreationAsync(new("")));
        Assert.Equal("AI.PROMPT_REQUIRED", ex.Code);
        Assert.Equal(FaultKind.Validation, ex.Kind);
    }

    [Fact]
    public async Task RecommendTaskAssignmentAsync_ReturnsRankedCandidatesWithOptimalWorkload()
    {
        var (service, store, reader, _, authorizer) = CreateTestContext();

        var result = await service.RecommendTaskAssignmentAsync(new(Guid.NewGuid(), "Sửa chữa màn hình", "Màn hình Dell bị chớp"));

        Assert.NotNull(result);
        Assert.NotEmpty(result.Candidates);
        Assert.Equal("Nguyen Van A", result.RecommendedAssigneeName); // user with 1 task ranks above user with 4 tasks
        Assert.Contains("ai.002", authorizer.AuthorizedPermissions);
        Assert.Single(store.Recommendations);
        Assert.Equal("ASSIGNMENT", store.Recommendations[0].RecommendationType);
    }

    [Fact]
    public async Task RecommendTaskParametersAsync_ReturnsGroundingParametersAndWorkflow()
    {
        var (service, store, _, _, authorizer) = CreateTestContext();

        var result = await service.RecommendTaskParametersAsync(new(Guid.NewGuid(), "Bảo trì máy chủ gấp", "Khẩn cấp kiểm tra hạ tầng"));

        Assert.Equal("CRITICAL", result.Priority);
        Assert.Equal(2, result.SuggestedDeadlineDays);
        Assert.NotNull(result.WorkflowVersionId);
        Assert.NotNull(result.SlaVersionId);
        Assert.NotEmpty(result.SuggestedChecklistItems);
        Assert.Contains("ai.003", authorizer.AuthorizedPermissions);
    }

    [Fact]
    public async Task BreakdownTaskAsync_ReturnsAcyclicSubtasks()
    {
        var (service, store, _, _, authorizer) = CreateTestContext();

        var result = await service.BreakdownTaskAsync(new(Guid.NewGuid(), "Nâng cấp hệ thống core", "Triển khai phiên bản 2.0", 4));

        Assert.Equal(4, result.Subtasks.Count);
        // Verify DAG: no cyclic dependencies (each depends on a strictly earlier order index)
        foreach (var sub in result.Subtasks)
        {
            if (sub.DependsOnOrderIndex.HasValue)
            {
                Assert.True(sub.DependsOnOrderIndex.Value < sub.OrderIndex);
            }
        }
        Assert.Contains("ai.004", authorizer.AuthorizedPermissions);
    }

    [Fact]
    public async Task EvaluateTaskRiskAsync_CompletedTask_ReturnsLowRiskWithoutMutatingState()
    {
        var (service, store, reader, _, authorizer) = CreateTestContext();
        var taskId = Guid.NewGuid();
        reader.TaskData = new AiTaskContextData(
            taskId, reader.TenantData.TenantId, "Task 1", "Desc", "Completed", "Medium",
            null, null, null, DateTimeOffset.UtcNow.AddDays(-2), null, 3, 3, 2, 100, [], []);

        var result = await service.EvaluateTaskRiskAsync(new(taskId));

        Assert.Equal("LOW", result.RiskLevel);
        Assert.True(result.RiskScore <= 0.1m);
        Assert.Contains("ai.005", authorizer.AuthorizedPermissions);
    }

    [Fact]
    public async Task EvaluateTaskRiskAsync_OverdueTask_ReturnsCriticalRiskAndMitigation()
    {
        var (service, store, reader, _, authorizer) = CreateTestContext();
        var taskId = Guid.NewGuid();
        var deadline = DateTimeOffset.UtcNow.AddDays(-2); // 2 days past deadline
        reader.TaskData = new AiTaskContextData(
            taskId, reader.TenantData.TenantId, "Task 1", "Desc", "InProgress", "Critical",
            null, null, null, DateTimeOffset.UtcNow.AddDays(-10), deadline, 5, 1, 1, 20, ["Bị vướng mắc"], []);

        var result = await service.EvaluateTaskRiskAsync(new(taskId));

        Assert.Equal("CRITICAL", result.RiskLevel);
        Assert.True(result.RiskScore >= 0.9m);
        Assert.NotEmpty(result.RiskFactors);
        Assert.NotEmpty(result.MitigationAdvice);
    }

    [Fact]
    public async Task SummarizeTaskProgressAsync_ReturnsFactualChronologicalSummary()
    {
        var (service, store, reader, _, authorizer) = CreateTestContext();
        var taskId = Guid.NewGuid();
        reader.TaskData = new AiTaskContextData(
            taskId, reader.TenantData.TenantId, "Thay thế ổ cứng", "Thay SSD máy chủ", "InProgress", "High",
            null, Guid.NewGuid(), "Nguyen Van A", DateTimeOffset.UtcNow.AddDays(-3), null, 4, 3, 2, 75,
            ["Đã sao lưu dữ liệu", "Đã lắp đặt ổ cứng mới"], ["Cần kiểm tra RAID"]);

        var result = await service.SummarizeTaskProgressAsync(new(taskId));

        Assert.NotNull(result);
        Assert.Contains("Nguyen Van A", result.Summary);
        Assert.Contains("75%", result.Summary);
        Assert.NotEmpty(result.KeyMilestones);
        Assert.NotEmpty(result.PendingActions);
        Assert.Contains("ai.006", authorizer.AuthorizedPermissions);
    }

    [Fact]
    public async Task AnalyzeRequestMultiIntentAsync_MultipleMarkers_DetectsMultiIntentAndProposesSplit()
    {
        var (service, store, _, _, authorizer) = CreateTestContext();

        var prompt = "Cần thay bàn phím máy tính đồng thời xin cấp tài khoản VPN làm việc từ xa";
        var result = await service.AnalyzeRequestMultiIntentAsync(new(Guid.NewGuid(), "Yêu cầu IT", prompt));

        Assert.True(result.IsMultiIntent);
        Assert.True(result.Intents.Count >= 2);
        Assert.Contains("ai.007", authorizer.AuthorizedPermissions);
    }

    [Fact]
    public async Task AnalyzeRequestMultiIntentAsync_SingleIntent_ReturnsSingleIntent()
    {
        var (service, store, _, _, authorizer) = CreateTestContext();

        var result = await service.AnalyzeRequestMultiIntentAsync(new(Guid.NewGuid(), "Đổi mật khẩu", "Tôi cần reset mật khẩu email"));

        Assert.False(result.IsMultiIntent);
        Assert.Single(result.Intents);
    }

    [Fact]
    public async Task SplitRequestAsync_ValidSplits_CreatesChildRequestsLinkedToParent()
    {
        var (service, store, reader, splitStore, authorizer) = CreateTestContext();
        var tenantId = reader.TenantData.TenantId;
        var parentId = Guid.NewGuid();
        var requesterId = Guid.NewGuid();
        var serviceId = reader.TenantData.Services[0].ServiceId;
        var categoryId = reader.TenantData.Services[0].Categories[0].CategoryId;

        splitStore.ParentRequest = WorkRequest.CreateSubmitted(
            tenantId, requesterId, serviceId, categoryId,
            "Yêu cầu tổng hợp", "Nội dung hỗn hợp", DateTimeOffset.UtcNow);

        var command = new SplitRequestCommand([
            new SplitRequestItem("Phần 1: Cấp thiết bị", "Chi tiết phần 1"),
            new SplitRequestItem("Phần 2: Mở tài khoản", "Chi tiết phần 2")
        ]);

        var result = await service.SplitRequestAsync(splitStore.ParentRequest.Id, command);

        Assert.Equal(2, result.SplitCount);
        Assert.Equal(2, result.Children.Count);
        Assert.Equal(2, splitStore.CreatedChildren.Count);
        Assert.All(splitStore.CreatedChildren, c => Assert.Equal(splitStore.ParentRequest.Id, c.ParentRequestId));

        Assert.Contains("ai.008", authorizer.AuthorizedPermissions);
        Assert.Single(store.Actions);
        Assert.Equal("split_request", store.Actions[0].ToolName);
    }

    [Fact]
    public async Task SplitRequestAsync_LessThanTwoSplits_ThrowsValidationFault()
    {
        var (service, _, _, _, _) = CreateTestContext();

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.SplitRequestAsync(Guid.NewGuid(), new([
            new SplitRequestItem("Chỉ 1 mục", "Desc")
        ])));

        Assert.Equal("AI.SPLIT_MIN_COUNT", ex.Code);
    }

    [Fact]
    public async Task RecommendRequestRoutingAsync_MatchesKeywordsToTenantServiceAndDept()
    {
        var (service, store, reader, _, authorizer) = CreateTestContext();

        var result = await service.RecommendRequestRoutingAsync(new(Guid.NewGuid(), "Hỏng màn hình máy tính", "Màn hình nhấp nháy liên tục"));

        Assert.NotNull(result);
        Assert.NotNull(result.RecommendedServiceId);
        Assert.NotNull(result.RecommendedDepartmentId);
        Assert.Contains("ai.009", authorizer.AuthorizedPermissions);
    }

    [Fact]
    public async Task ExecuteActionAsync_UnallowlistedTool_ThrowsValidationFault()
    {
        var (service, _, _, _, _) = CreateTestContext();

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.ExecuteActionAsync(new("unauthorized_tool", "{}")));
        Assert.Equal("AI.TOOL_NOT_ALLOWED", ex.Code);
    }

    [Fact]
    public async Task ExecuteActionAsync_RequiresConfirmation_ReturnsNotExecutedWhenUnconfirmed()
    {
        var (service, store, _, _, authorizer) = CreateTestContext();

        var result = await service.ExecuteActionAsync(new("create_task_draft", "{}", Confirmed: false));

        Assert.Equal("NOT_EXECUTED", result.ExecutionStatus);
        Assert.Contains("requires human confirmation", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("ai.010", authorizer.AuthorizedPermissions);
        Assert.Contains("tasks.create", authorizer.AuthorizedPermissions);
        Assert.Single(store.Actions);
        Assert.Equal(AgentExecutionStatus.NotExecuted, store.Actions[0].ExecutionStatus);
    }

    [Fact]
    public async Task ExecuteActionAsync_Confirmed_ExecutesToolAndReturnsSuccess()
    {
        var (service, store, _, _, authorizer) = CreateTestContext();

        var result = await service.ExecuteActionAsync(new("create_task_draft", "{}", Confirmed: true));

        Assert.Equal("SUCCEEDED", result.ExecutionStatus);
        Assert.Equal("OK", result.ResultRef);
        Assert.Single(store.Actions);
        Assert.Equal(AgentExecutionStatus.Succeeded, store.Actions[0].ExecutionStatus);
    }
}
