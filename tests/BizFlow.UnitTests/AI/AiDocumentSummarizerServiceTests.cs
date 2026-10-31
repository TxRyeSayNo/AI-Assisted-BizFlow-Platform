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
using Xunit;

namespace BizFlow.UnitTests.AI;

public sealed class AiDocumentSummarizerServiceTests
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

    private sealed class FakeDocumentSummarizerStore : IAiDocumentSummarizerStore
    {
        public Dictionary<Guid, (string FileName, string? Content)> Attachments { get; } = [];
        public List<CreatedSummaryTaskResult> PersistedTasks { get; } = [];

        public Task<(string FileName, string? Content)?> GetAttachmentTextAsync(
            Guid tenantId,
            Guid attachmentId,
            CancellationToken cancellationToken)
        {
            if (Attachments.TryGetValue(attachmentId, out var val))
            {
                return Task.FromResult<(string FileName, string? Content)?>(val);
            }
            return Task.FromResult<(string FileName, string? Content)?>(null);
        }

        public Task<IReadOnlyList<CreatedSummaryTaskResult>> PersistSummaryTasksAsync(
            Guid tenantId,
            Guid userId,
            string sourceTitle,
            IReadOnlyList<ExtractedActionItem> items,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            var results = items.Select(item => new CreatedSummaryTaskResult(
                TaskId: Guid.NewGuid(),
                Title: item.Title,
                Priority: item.SuggestedPriority ?? "Medium",
                DepartmentId: Guid.NewGuid(),
                Status: "DRAFT"
            )).ToList();

            PersistedTasks.AddRange(results);
            return Task.FromResult<IReadOnlyList<CreatedSummaryTaskResult>>(results);
        }
    }

    [Fact]
    public async Task SummarizeDocumentAsync_RawTextInput_GeneratesExecutiveSummaryActionItemsAndRisks()
    {
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

        var text = """
            Kế hoạch bảo trì hệ thống quý 4.
            Đội ngũ kỹ thuật cần hoàn thành sao lưu toàn bộ cơ sở dữ liệu.
            Phải kiểm tra các bản cập nhật bảo mật trên máy chủ chính.
            Nếu chậm trễ sẽ có nguy cơ rủi ro gián đoạn dịch vụ của khách hàng.
            """;

        var result = await service.SummarizeDocumentAsync(new DocumentSummarizeCommand(
            RawText: text,
            AttachmentId: null,
            Mode: "COMPREHENSIVE",
            Language: "vi"));

        Assert.NotNull(result);
        Assert.NotEmpty(result.ExecutiveSummary);
        Assert.NotEmpty(result.KeyPoints);
        Assert.NotEmpty(result.ActionItems);
        Assert.NotEmpty(result.RisksAndCompliance);
        Assert.Equal("RawText", result.SourceType);
        Assert.Contains("ai.006", authorizer.CheckedPermissions);
    }

    [Fact]
    public async Task SummarizeDocumentAsync_AttachmentInput_ReadsFromStorageAndSummarizes()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var authorizer = new FakeResourceAuthorizer();
        var context = new FakeTenantContext(tenantId, userId);
        var store = new FakeAiStore();
        var summarizerStore = new FakeDocumentSummarizerStore();

        var attId = Guid.NewGuid();
        summarizerStore.Attachments[attId] = (
            "BienBanNghiemThu.txt",
            "Biên bản nghiệm thu dự án phần mềm.\nCần hoàn thành kiểm thử hiệu năng trước ngày 15.\nPhải chuyển giao tài liệu hướng dẫn sử dụng.\nLưu ý rủi ro chậm trễ sẽ chịu phạt hợp đồng 5% theo điều khoản cam kết."
        );

        var service = new AiDocumentSummarizerService(
            context,
            authorizer,
            store,
            summarizerStore,
            TimeProvider.System,
            modelClient: null);

        var result = await service.SummarizeDocumentAsync(new DocumentSummarizeCommand(
            RawText: null,
            AttachmentId: attId,
            Mode: "COMPREHENSIVE",
            Language: "vi"));

        Assert.NotNull(result);
        Assert.Equal("BienBanNghiemThu.txt", result.SourceTitle);
        Assert.Equal("Attachment", result.SourceType);
        Assert.NotEmpty(result.ActionItems);
        Assert.NotEmpty(result.RisksAndCompliance);
    }

    [Fact]
    public async Task SummarizeDocumentAsync_MissingInput_ThrowsValidationException()
    {
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
            TimeProvider.System);

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() =>
            service.SummarizeDocumentAsync(new DocumentSummarizeCommand(
                RawText: null,
                AttachmentId: null)));

        Assert.Equal("VALIDATION.FAILED", ex.Code);
    }

    [Fact]
    public async Task CreateTasksFromSummaryAsync_ValidItems_PersistsWorkTasksInDb()
    {
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
            TimeProvider.System);

        var response = await service.CreateTasksFromSummaryAsync(new CreateTasksFromSummaryCommand(
            SourceTitle: "Biên bản họp",
            SourceReference: "HopTuan.docx",
            Items: new List<ExtractedActionItem>
            {
                new(1, "Kiểm thử API", "Thực hiện kiểm thử các endpoint mới", "ENG", "High", 3),
                new(2, "Viết tài liệu Swagger", "Cập nhật mô tả tham số API", "ENG", "Medium", 2)
            }));

        Assert.NotNull(response);
        Assert.Equal(2, response.TotalCreated);
        Assert.Equal(2, response.CreatedTasks.Count);
        Assert.Equal(2, summarizerStore.PersistedTasks.Count);
        Assert.Contains("tasks.create", authorizer.CheckedPermissions);
    }
}
