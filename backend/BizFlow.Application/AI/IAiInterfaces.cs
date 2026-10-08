using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Domain.AI;

namespace BizFlow.Application.AI;

public sealed record AiModelResponse(
    string Content,
    string ModelName,
    int? LatencyMs,
    int? PromptTokens,
    int? CompletionTokens);

public interface IAiModelClient
{
    Task<AiModelResponse> CompleteAsync(
        string prompt,
        string systemInstructions,
        CancellationToken cancellationToken = default);
}

public interface IAiStore
{
    Task<AIInteraction> RecordInteractionAsync(
        Guid tenantId,
        Guid userId,
        string feature,
        string modelName,
        string? inputRef,
        string? outputRef,
        AIInteractionStatus status,
        int? latencyMs,
        string? tokenUsage,
        CancellationToken cancellationToken = default);

    Task<AIRecommendation> RecordRecommendationAsync(
        Guid tenantId,
        Guid aiInteractionId,
        RecommendationObjectType objectType,
        Guid? objectId,
        string recommendationType,
        string payloadJson,
        decimal? confidence,
        CancellationToken cancellationToken = default);

    Task<AIAgentAction> RecordActionAsync(
        Guid tenantId,
        Guid aiInteractionId,
        string toolName,
        string argsJson,
        AgentAuthorizationResult authorizationResult,
        AgentExecutionStatus executionStatus,
        string? resultRef,
        CancellationToken cancellationToken = default);

    Task RecordDecisionAsync(
        Guid tenantId,
        Guid recommendationId,
        HumanDecision decision,
        Guid reviewerId,
        CancellationToken cancellationToken = default);
}

public interface IAiContextReader
{
    Task<AiTenantContextData> GetTenantContextAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<AiTaskContextData?> GetTaskContextAsync(Guid tenantId, Guid taskId, CancellationToken cancellationToken = default);
    Task<AiRequestContextData?> GetRequestContextAsync(Guid tenantId, Guid requestId, CancellationToken cancellationToken = default);
}

public sealed record AiToolDefinition(
    string Name,
    string Description,
    string RequiredPermission,
    bool RequiresConfirmation);

public interface IAiToolRegistry
{
    AiToolDefinition? GetTool(string name);
    IReadOnlyList<AiToolDefinition> GetRegisteredTools();
}
