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
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Workflows;
using BizFlow.Infrastructure.AI;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace BizFlow.UnitTests.AI;

public sealed class AiAutonomousAgentTests
{
    private sealed class FakeTenantContext(Guid? tenantId, Guid? userId) : ITenantContext
    {
        public Guid? UserId => userId;
        public Guid? TenantId => tenantId;
    }

    private static BizFlowDbContext CreateInMemoryDbContext(Guid? tenantId = null, Guid? userId = null)
    {
        var options = new DbContextOptionsBuilder<BizFlowDbContext>()
            .UseInMemoryDatabase(databaseName: $"BizFlow_AiTest_{Guid.NewGuid()}")
            .Options;

        return new BizFlowDbContext(options, new FakeTenantContext(tenantId, userId));
    }

    // =========================================================================
    // PILLAR 1: LONG-TERM VECTOR MEMORY & SEMANTIC RAG TESTS
    // =========================================================================

    [Fact]
    public void VectorMemory_CosineSimilarity_ReturnsHighForSemanticallyRelatedTexts()
    {
        // Arrange
        var vectorA = AiEmbeddingService.GenerateLocalSemanticEmbedding("Quy định phê duyệt ngân sách và thanh toán chi phí");
        var vectorB = AiEmbeddingService.GenerateLocalSemanticEmbedding("Hướng dẫn quy trình phê duyệt ngân sách tài chính nội bộ");
        var vectorC = AiEmbeddingService.GenerateLocalSemanticEmbedding("Bảo dưỡng định kỳ hệ thống máy chủ và cơ sở dữ liệu");

        // Act
        var similarityAB = AiVectorMemoryStore.ComputeCosineSimilarity(vectorA, vectorB);
        var similarityAC = AiVectorMemoryStore.ComputeCosineSimilarity(vectorA, vectorC);

        // Assert
        Assert.True(similarityAB > 0.40f, $"Related texts should have high similarity, got {similarityAB}");
        Assert.True(similarityAB > similarityAC, "Related texts should score higher than unrelated texts");
    }

    [Fact]
    public async Task VectorMemoryStore_StoreAndSearch_RetrievesRelevantCorporateMemory()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var dbContext = CreateInMemoryDbContext(tenantId, userId);
        var config = new ConfigurationBuilder().Build();
        var embeddingService = new AiEmbeddingService(config);
        var memoryStore = new AiVectorMemoryStore(dbContext, embeddingService);

        await memoryStore.StoreMemoryAsync(
            tenantId,
            scope: "POLICY",
            title: "Quy chuẩn an toàn bảo mật thông tin 2026",
            content: "Mọi nhân viên khi triển khai dự án công nghệ phải tuân thủ kiểm thử bảo mật và phân quyền vai trò RBAC chặt chẽ.",
            metadataJson: "{\"department\": \"IT\"}");

        await memoryStore.StoreMemoryAsync(
            tenantId,
            scope: "POLICY",
            title: "Quy chế nghỉ phép và công tác phí",
            content: "Nhân viên đi công tác cần nộp hóa đơn đỏ hợp lệ trong vòng 5 ngày làm việc kể từ ngày trở về.",
            metadataJson: "{\"department\": \"HR\"}");

        // Act - Query security policy
        var searchResults = await memoryStore.SearchMemoriesAsync(
            tenantId,
            query: "kiểm thử bảo mật dự án công nghệ và phân quyền",
            scope: "POLICY",
            maxResults: 3,
            minScore: 0.35f);

        // Assert
        Assert.NotEmpty(searchResults);
        var top = searchResults.First();
        Assert.Equal("Quy chuẩn an toàn bảo mật thông tin 2026", top.Title);
        Assert.True(top.SimilarityScore >= 0.35f);
    }

    [Fact]
    public async Task VectorMemoryStore_ScopeFiltering_ExcludesDifferentScopes()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        using var dbContext = CreateInMemoryDbContext(tenantId, userId);
        var config = new ConfigurationBuilder().Build();
        var embeddingService = new AiEmbeddingService(config);
        var memoryStore = new AiVectorMemoryStore(dbContext, embeddingService);

        await memoryStore.StoreMemoryAsync(
            tenantId,
            scope: "HR_POLICY",
            title: "Quy định giờ làm việc",
            content: "Giờ làm việc từ 8h00 đến 17h30 từ thứ 2 đến thứ 6.",
            metadataJson: null);

        // Act - Query with different scope
        var results = await memoryStore.SearchMemoriesAsync(
            tenantId,
            query: "giờ làm việc công ty",
            scope: "IT_POLICY",
            maxResults: 5,
            minScore: 0.1f);

        // Assert
        Assert.Empty(results);
    }

    // =========================================================================
    // PILLAR 2: PROACTIVE AUTONOMOUS AGENT DAEMON & SCANNER TESTS
    // =========================================================================

    [Fact]
    public async Task ProactiveScanner_DetectsImpendingSlaBreach_CreatesRecommendationAndNotification()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var clock = TimeProvider.System;
        var now = clock.GetUtcNow();

        var creator = UserAccount.CreateTenantUser(tenantId, "EMP001", "creator@test.com", "Creator", "hash", now);
        var assignee = UserAccount.CreateTenantUser(tenantId, "EMP002", "assignee@test.com", "Assignee", "hash", now);

        using var dbContext = CreateInMemoryDbContext(tenantId, creator.Id);
        dbContext.Users.AddRange(creator, assignee);
        await dbContext.SaveChangesAsync();

        var aiStore = new AiStore(dbContext);
        var scanner = new ProactiveAgentScanner(dbContext, aiStore, clock);

        var deadline = now.AddHours(5);

        // Create an active task with deadline in 5 hours (critical SLA risk)
        var task = WorkTask.CreateDraft(
            tenantId: tenantId,
            creatorId: creator.Id,
            title: "Chuẩn bị báo cáo tài chính quý",
            now: now,
            description: "Cần nộp gấp",
            priority: TaskPriority.High,
            deadline: deadline);

        dbContext.WorkTasks.Add(task);
        await dbContext.SaveChangesAsync();

        var assignment = TaskAssignment.Create(task.Id, creator.Id, null, assignee.Id, now);
        dbContext.Set<TaskAssignment>().Add(assignment);
        await dbContext.SaveChangesAsync();

        // Act - Run proactive scan
        var scanResult = await scanner.RunProactiveScanAsync(tenantId);

        // Assert
        Assert.True(scanResult.SlaRisksDetected >= 1, "Should detect impending SLA breach");
        Assert.True(scanResult.RecommendationsCreated >= 1, "Should autonomously record proactive recommendation");

        // Verify recommendation in DB
        var rec = await dbContext.AIRecommendations
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.ObjectId == task.Id && r.RecommendationType == "PROACTIVE_RISK_ALERT");
        Assert.NotNull(rec);
        Assert.True(rec.Confidence >= 0.90m);

        // Verify proactive notification delivered to assignee
        var notification = await dbContext.Notifications
            .IgnoreQueryFilters()
            .FirstOrDefaultAsync(n => n.TenantId == tenantId && n.RecipientId == assignee.Id);
        Assert.NotNull(notification);
        Assert.Contains(task.Title, notification.Title);
    }

    [Fact]
    public async Task ProactiveScanner_Deduplication_DoesNotCreateDuplicateAlertForSameTask()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var clock = TimeProvider.System;
        var now = clock.GetUtcNow();

        var creator = UserAccount.CreateTenantUser(tenantId, "EMP001", "creator@test.com", "Creator", "hash", now);

        using var dbContext = CreateInMemoryDbContext(tenantId, creator.Id);
        dbContext.Users.Add(creator);
        await dbContext.SaveChangesAsync();

        var aiStore = new AiStore(dbContext);
        var scanner = new ProactiveAgentScanner(dbContext, aiStore, clock);

        var deadline = now.AddHours(2);
        var task = WorkTask.CreateDraft(
            tenantId: tenantId,
            creatorId: creator.Id,
            title: "Nhiệm vụ kiểm toán độc lập",
            now: now,
            description: null,
            priority: TaskPriority.Critical,
            deadline: deadline);

        dbContext.WorkTasks.Add(task);
        await dbContext.SaveChangesAsync();

        // Act - First scan creates alert
        var scan1 = await scanner.RunProactiveScanAsync(tenantId);
        Assert.Equal(1, scan1.RecommendationsCreated);

        // Act - Second scan within window should NOT duplicate alert
        var scan2 = await scanner.RunProactiveScanAsync(tenantId);
        Assert.Equal(0, scan2.RecommendationsCreated);
    }

    // =========================================================================
    // PILLAR 3: SELF-REFLECTION & ADAPTIVE LEARNING LOOP TESTS
    // =========================================================================

    [Fact]
    public async Task SelfReflection_CalculatesAccuracyAndLearnsCritiqueRulesFromOverrides()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reviewerId = userId; // DecisionBy must match CurrentUserId in BizFlowDbContext boundary
        using var dbContext = CreateInMemoryDbContext(tenantId, userId);
        var aiStore = new AiStore(dbContext);
        var reflectionService = new AiSelfReflectionService(dbContext);

        // Record 1 Accepted recommendation
        var int1 = await aiStore.RecordInteractionAsync(tenantId, userId, "ai.test", "gpt-4o", null, null, AIInteractionStatus.Succeeded, 100, null);
        var rec1 = await aiStore.RecordRecommendationAsync(tenantId, int1.Id, RecommendationObjectType.Task, null, "DRAFT", "{}", 0.90m);
        await aiStore.RecordDecisionAsync(tenantId, rec1.Id, HumanDecision.Accepted, reviewerId);

        // Record 2 Overridden recommendations for ASSIGNMENT
        var int2 = await aiStore.RecordInteractionAsync(tenantId, userId, "ai.test", "gpt-4o", null, null, AIInteractionStatus.Succeeded, 100, null);
        var rec2 = await aiStore.RecordRecommendationAsync(tenantId, int2.Id, RecommendationObjectType.Task, null, "ASSIGNMENT", "{}", 0.90m);
        await aiStore.RecordDecisionAsync(tenantId, rec2.Id, HumanDecision.Overridden, reviewerId);

        var int3 = await aiStore.RecordInteractionAsync(tenantId, userId, "ai.test", "gpt-4o", null, null, AIInteractionStatus.Succeeded, 100, null);
        var rec3 = await aiStore.RecordRecommendationAsync(tenantId, int3.Id, RecommendationObjectType.Task, null, "ASSIGNMENT", "{}", 0.85m);
        await aiStore.RecordDecisionAsync(tenantId, rec3.Id, HumanDecision.Overridden, reviewerId);

        // Act - Analyze reflection metrics
        var metrics = await reflectionService.GetReflectionMetricsAsync(tenantId);

        // Assert
        Assert.Equal(3, metrics.TotalDecisions);
        Assert.Equal(1, metrics.AcceptedCount);
        Assert.Equal(2, metrics.OverriddenCount);
        Assert.Equal(0.3333m, metrics.AcceptanceRate);
        Assert.Equal(0.6667m, metrics.OverrideRate);

        // Verify learned critique rules
        Assert.NotEmpty(metrics.ActiveCritiqueRules);
        var assignmentCritique = metrics.ActiveCritiqueRules.FirstOrDefault(r => r.RecommendationType == "ASSIGNMENT");
        Assert.NotNull(assignmentCritique);
        Assert.True(assignmentCritique.ConfidencePenalty > 0, "Learned critique should carry confidence penalty");
        Assert.Contains("ghi đè", assignmentCritique.CritiqueSummary);

        // Verify generated prompt directives
        var directivesPrompt = await reflectionService.BuildReflectionDirectivesPromptAsync(tenantId, "ASSIGNMENT");
        Assert.Contains("CHỈ THỊ TỰ SOI CHIẾU", directivesPrompt);
        Assert.Contains("ASSIGNMENT", directivesPrompt);
    }

    [Fact]
    public async Task SelfReflection_DirectivesInjectedIntoAssistanceService_AppliesConfidenceAdjustment()
    {
        // Arrange
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var reviewerId = userId;
        using var dbContext = CreateInMemoryDbContext(tenantId, userId);
        var aiStore = new AiStore(dbContext);
        var reflectionService = new AiSelfReflectionService(dbContext);

        // Seed 3 overrides for DRAFT
        for (int i = 0; i < 3; i++)
        {
            var inter = await aiStore.RecordInteractionAsync(tenantId, userId, "ai.001", "mock-gpt-4o", null, null, AIInteractionStatus.Succeeded, 100, null);
            var rec = await aiStore.RecordRecommendationAsync(tenantId, inter.Id, RecommendationObjectType.Task, null, "DRAFT", "{}", 0.92m);
            await aiStore.RecordDecisionAsync(tenantId, rec.Id, HumanDecision.Overridden, reviewerId);
        }

        var tenantContext = new FakeTenantContext(tenantId, userId);
        var authorizer = new FakeResourceAuthorizer();
        var contextReader = new FakeAiContextReader();

        var assistanceService = new AiAssistanceService(
            tenantContext,
            authorizer,
            aiStore,
            contextReader,
            new AiToolRegistry(),
            new FakeRequestSplitStore(),
            new FakeActionDispatcher(),
            TimeProvider.System,
            modelClient: null,
            confirmationTokenService: null,
            vectorMemoryStore: null,
            selfReflectionService: reflectionService);

        // Act - Generate draft with reflection loop active
        var suggestion = await assistanceService.AssistTaskCreationAsync(new TaskAssistanceCommand("Chuẩn bị tài liệu kiểm toán năm"));

        // Assert
        // Standard baseline confidence is 0.92m. With 3 overrides, penalty applies (3 * 0.05m = 0.15m), so confidence should be 0.77m
        Assert.True(suggestion.Confidence < 0.90m, $"Confidence should be reduced due to past overrides, actual: {suggestion.Confidence}");
    }

    // Helpers
    private sealed class FakeResourceAuthorizer : IResourceAuthorizer
    {
        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }

    private sealed class FakeAiContextReader : IAiContextReader
    {
        public Task<AiTenantContextData> GetTenantContextAsync(Guid tenantId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiTenantContextData(
                TenantId: tenantId,
                Services: [new(Guid.NewGuid(), "IT-01", "Hỗ trợ kỹ thuật", "Dịch vụ IT", [])],
                Departments: [new(Guid.NewGuid(), "IT", "Phòng CNTT")],
                Users: [new(Guid.NewGuid(), "Nguyễn Văn A", "a@bizflow.vn", Guid.NewGuid(), "EMPLOYEE", 1)],
                Workflows: [],
                Slas: []));
        }

        public Task<AiTaskContextData?> GetTaskContextAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AiTaskContextData?>(null);

        public Task<AiRequestContextData?> GetRequestContextAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken = default) =>
            Task.FromResult<AiRequestContextData?>(null);
    }

    private sealed class FakeRequestSplitStore : IRequestSplitStore
    {
        public Task<WorkRequest?> GetParentRequestAsync(Guid tenantId, Guid parentRequestId, CancellationToken cancellationToken) =>
            Task.FromResult<WorkRequest?>(null);

        public Task<IReadOnlyList<WorkRequest>> CreateChildRequestsAsync(Guid tenantId, Guid parentRequestId, IReadOnlyList<WorkRequest> childRequests, AuditLog audit, CancellationToken cancellationToken) =>
            Task.FromResult(childRequests);
    }

    private sealed class FakeActionDispatcher : IAiActionDispatcher
    {
        public Task<object?> DispatchToolAsync(Guid tenantId, Guid userId, string toolName, string argsJson, CancellationToken cancellationToken) =>
            Task.FromResult<object?>("OK");
    }
}
