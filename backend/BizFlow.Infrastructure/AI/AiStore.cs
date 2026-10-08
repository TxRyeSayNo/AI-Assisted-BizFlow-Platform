using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Domain.AI;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.AI;

public sealed class AiStore(BizFlowDbContext dbContext) : IAiStore
{
    public async Task<AIInteraction> RecordInteractionAsync(
        Guid tenantId,
        Guid userId,
        string feature,
        string modelName,
        string? inputRef,
        string? outputRef,
        AIInteractionStatus status,
        int? latencyMs,
        string? tokenUsage,
        CancellationToken cancellationToken = default)
    {
        var interaction = AIInteraction.Create(
            tenantId, userId, feature, modelName, inputRef, outputRef,
            status, latencyMs, tokenUsage, DateTimeOffset.UtcNow);

        dbContext.AIInteractions.Add(interaction);
        await dbContext.SaveChangesAsync(cancellationToken);
        return interaction;
    }

    public async Task<AIRecommendation> RecordRecommendationAsync(
        Guid tenantId,
        Guid aiInteractionId,
        RecommendationObjectType objectType,
        Guid? objectId,
        string recommendationType,
        string payloadJson,
        decimal? confidence,
        CancellationToken cancellationToken = default)
    {
        var recommendation = AIRecommendation.Create(
            tenantId, aiInteractionId, objectType, objectId,
            recommendationType, payloadJson, confidence);

        dbContext.AIRecommendations.Add(recommendation);
        await dbContext.SaveChangesAsync(cancellationToken);
        return recommendation;
    }

    public async Task<AIAgentAction> RecordActionAsync(
        Guid tenantId,
        Guid aiInteractionId,
        string toolName,
        string argsJson,
        AgentAuthorizationResult authorizationResult,
        AgentExecutionStatus executionStatus,
        string? resultRef,
        CancellationToken cancellationToken = default)
    {
        var action = AIAgentAction.Create(
            tenantId, aiInteractionId, toolName, argsJson,
            authorizationResult, executionStatus, resultRef, DateTimeOffset.UtcNow);

        dbContext.AIAgentActions.Add(action);
        await dbContext.SaveChangesAsync(cancellationToken);
        return action;
    }

    public async Task RecordDecisionAsync(
        Guid tenantId,
        Guid recommendationId,
        HumanDecision decision,
        Guid reviewerId,
        CancellationToken cancellationToken = default)
    {
        var recommendation = await dbContext.AIRecommendations
            .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == recommendationId, cancellationToken)
            ?? throw new ApplicationFault(FaultKind.NotFound, "AI.RECOMMENDATION_NOT_FOUND", "Recommendation not found.");

        recommendation.RecordDecision(decision, reviewerId, DateTimeOffset.UtcNow);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
