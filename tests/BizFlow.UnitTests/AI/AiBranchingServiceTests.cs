using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.AI;
using BizFlow.Domain.Security;
using BizFlow.Infrastructure.AI;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BizFlow.UnitTests.AI;

public sealed class AiBranchingServiceTests
{
    private sealed class FakeTenantContext(Guid? tenantId, Guid? userId) : ITenantContext
    {
        public Guid? UserId => userId;
        public Guid? TenantId => tenantId;
    }

    private sealed class FakeResourceAuthorizer : IResourceAuthorizer
    {
        public List<string> CheckedPermissions { get; } = [];

        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default)
        {
            CheckedPermissions.Add(permission);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAiContextReader : IAiContextReader
    {
        public Task<AiTenantContextData> GetTenantContextAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            var deptIt = new AiDepartmentInfo(Guid.NewGuid(), "IT", "Phòng Công nghệ Thông tin");
            var deptHr = new AiDepartmentInfo(Guid.NewGuid(), "HR", "Phòng Nhân sự");
            var service = new AiServiceInfo(Guid.NewGuid(), "SRV_ONBOARD", "Onboarding", "Tiếp nhận nhân sự", [
                new AiCategoryInfo(Guid.NewGuid(), "CAT_LAPTOP", "Cấp phát laptop")
            ]);

            return Task.FromResult(new AiTenantContextData(
                tenantId,
                [service],
                [deptIt, deptHr],
                [],
                [],
                []));
        }

        public Task<AiTaskContextData?> GetTaskContextAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AiTaskContextData?>(null);

        public Task<AiRequestContextData?> GetRequestContextAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AiRequestContextData?>(null);
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

    private sealed class FakeBranchingStore : IAiBranchingStore
    {
        public List<CreatedBranchRecord> PersistedRecords { get; } = [];

        public Task<IReadOnlyList<CreatedBranchRecord>> PersistBranchingPlanAsync(
            Guid tenantId,
            Guid userId,
            Guid planId,
            string sourceType,
            Guid? sourceId,
            IReadOnlyList<BranchPlanNode> branches,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var records = branches.Select(b => new CreatedBranchRecord(
                BranchIndex: b.BranchIndex,
                RecordType: b.BranchType,
                RecordId: Guid.NewGuid(),
                Title: b.Title,
                Status: "DRAFT",
                DepartmentId: b.DepartmentId,
                DepartmentName: b.DepartmentName,
                ServiceId: b.ServiceId,
                CategoryId: b.CategoryId
            )).ToList();

            PersistedRecords.AddRange(records);
            return Task.FromResult<IReadOnlyList<CreatedBranchRecord>>(records);
        }
    }

    private static AiConfirmationTokenService CreateTokenService()
    {
        var inMemoryConfig = new Dictionary<string, string?>
        {
            ["AI:ConfirmationSecret"] = "Test-Secret-Key-Must-Be-At-Least-32-Characters-Long!"
        };
        var config = new ConfigurationBuilder().AddInMemoryCollection(inMemoryConfig).Build();
        return new AiConfirmationTokenService(config, new MemoryCache(new MemoryCacheOptions()), TimeProvider.System);
    }

    [Fact]
    public async Task PlanBranchingAsync_ValidInput_GeneratesMultiBranchPlanWithDependenciesAndToken()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var authorizer = new FakeResourceAuthorizer();
        var context = new FakeTenantContext(tenantId, userId);
        var contextReader = new FakeAiContextReader();
        var store = new FakeAiStore();
        var branchingStore = new FakeBranchingStore();
        var tokenService = CreateTokenService();

        var service = new AiBranchingService(
            context,
            authorizer,
            contextReader,
            store,
            branchingStore,
            tokenService,
            TimeProvider.System,
            modelClient: null);

        var result = await service.PlanBranchingAsync(new BranchPlanCommand(
            SourceType: "Request",
            SourceId: Guid.NewGuid(),
            Title: "Tiếp nhận nhân sự mới",
            Description: "1. Cấp laptop và email từ IT\n2. Ký hợp đồng lao động từ phòng Nhân sự\n3. Hướng dẫn đào tạo nội bộ"));

        Assert.NotNull(result);
        Assert.NotEmpty(result.Branches);
        Assert.True(result.TotalBranches >= 2);
        Assert.NotEmpty(result.ConfirmationToken);
        Assert.True(result.EstimatedTotalDays > 0);
        Assert.Contains("ai.007", authorizer.CheckedPermissions);
    }

    [Fact]
    public async Task PlanBranchingAsync_EmptyInput_ThrowsValidationException()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var authorizer = new FakeResourceAuthorizer();
        var context = new FakeTenantContext(tenantId, userId);
        var contextReader = new FakeAiContextReader();
        var store = new FakeAiStore();
        var branchingStore = new FakeBranchingStore();
        var tokenService = CreateTokenService();

        var service = new AiBranchingService(
            context,
            authorizer,
            contextReader,
            store,
            branchingStore,
            tokenService,
            TimeProvider.System);

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() =>
            service.PlanBranchingAsync(new BranchPlanCommand(
                SourceType: "FreeText",
                SourceId: null,
                Title: "   ",
                Description: "")));

        Assert.Equal("VALIDATION.FAILED", ex.Code);
    }

    [Fact]
    public async Task ExecuteBranchingPlanAsync_ValidToken_CreatesChildRequestsAndTasksAtomically()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var authorizer = new FakeResourceAuthorizer();
        var context = new FakeTenantContext(tenantId, userId);
        var contextReader = new FakeAiContextReader();
        var store = new FakeAiStore();
        var branchingStore = new FakeBranchingStore();
        var tokenService = CreateTokenService();

        var service = new AiBranchingService(
            context,
            authorizer,
            contextReader,
            store,
            branchingStore,
            tokenService,
            TimeProvider.System);

        var parentReqId = Guid.NewGuid();

        // 1. First generate a valid plan
        var plan = await service.PlanBranchingAsync(new BranchPlanCommand(
            SourceType: "Request",
            SourceId: parentReqId,
            Title: "Triển khai phần mềm",
            Description: "1. Yêu cầu hỗ trợ hạ tầng từ IT\n2. Triển khai cấu hình hệ thống"));

        // 2. Execute plan
        var execResult = await service.ExecuteBranchingPlanAsync(new ExecuteBranchingPlanCommand(
            PlanId: plan.PlanId,
            SourceType: "Request",
            SourceId: parentReqId,
            Branches: plan.Branches,
            ConfirmationToken: plan.ConfirmationToken));

        Assert.NotNull(execResult);
        Assert.Equal(plan.Branches.Count, execResult.TotalCreated);
        Assert.Equal(plan.Branches.Count, execResult.CreatedRecords.Count);
        Assert.Equal(plan.Branches.Count, branchingStore.PersistedRecords.Count);
    }
}
