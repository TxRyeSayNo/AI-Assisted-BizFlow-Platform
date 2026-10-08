using System;
using BizFlow.Domain.Common;

namespace BizFlow.Domain.AI;

public enum AIInteractionStatus
{
    Succeeded,
    Failed,
    Timeout,
    Rejected
}

public enum RecommendationObjectType
{
    Task,
    Request
}

public enum HumanDecision
{
    Accepted,
    Overridden,
    Dismissed
}

public enum AgentAuthorizationResult
{
    Authorized,
    Denied
}

public enum AgentExecutionStatus
{
    NotExecuted,
    Succeeded,
    Failed
}

public sealed class AIInteraction
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Feature { get; private set; } = "";
    public string ModelName { get; private set; } = "";
    public string? InputRef { get; private set; }
    public string? OutputRef { get; private set; }
    public AIInteractionStatus Status { get; private set; }
    public int? LatencyMs { get; private set; }
    public string? TokenUsage { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private AIInteraction() { }

    public static AIInteraction Create(
        Guid tenantId,
        Guid userId,
        string feature,
        string modelName,
        string? inputRef,
        string? outputRef,
        AIInteractionStatus status,
        int? latencyMs,
        string? tokenUsage,
        DateTimeOffset createdAt)
    {
        return new AIInteraction
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            UserId = EntityRules.Id(userId, nameof(userId)),
            Feature = string.IsNullOrWhiteSpace(feature) ? throw new ArgumentException("Feature is required.", nameof(feature)) : feature.Trim(),
            ModelName = string.IsNullOrWhiteSpace(modelName) ? "mock-gpt-4o" : modelName.Trim(),
            InputRef = inputRef,
            OutputRef = outputRef,
            Status = status,
            LatencyMs = latencyMs is >= 0 ? latencyMs : null,
            TokenUsage = tokenUsage,
            CreatedAt = createdAt.ToUniversalTime()
        };
    }
}

public sealed class AIRecommendation
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid AIInteractionId { get; private set; }
    public RecommendationObjectType ObjectType { get; private set; }
    public Guid? ObjectId { get; private set; }
    public string RecommendationType { get; private set; } = "";
    public string PayloadJson { get; private set; } = "{}";
    public decimal? Confidence { get; private set; }
    public HumanDecision? HumanDecision { get; private set; }
    public Guid? DecisionBy { get; private set; }
    public DateTimeOffset? DecidedAt { get; private set; }

    private AIRecommendation() { }

    public static AIRecommendation Create(
        Guid tenantId,
        Guid aiInteractionId,
        RecommendationObjectType objectType,
        Guid? objectId,
        string recommendationType,
        string payloadJson,
        decimal? confidence)
    {
        if (confidence is < 0 or > 1)
            throw new ArgumentOutOfRangeException(nameof(confidence), "Confidence must be between 0 and 1.");

        return new AIRecommendation
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            AIInteractionId = EntityRules.Id(aiInteractionId, nameof(aiInteractionId)),
            ObjectType = objectType,
            ObjectId = objectId,
            RecommendationType = string.IsNullOrWhiteSpace(recommendationType) ? throw new ArgumentException("RecommendationType is required.", nameof(recommendationType)) : recommendationType.Trim(),
            PayloadJson = string.IsNullOrWhiteSpace(payloadJson) ? "{}" : payloadJson,
            Confidence = confidence
        };
    }

    public void RecordDecision(HumanDecision decision, Guid reviewerId, DateTimeOffset now)
    {
        HumanDecision = decision;
        DecisionBy = EntityRules.Id(reviewerId, nameof(reviewerId));
        DecidedAt = now.ToUniversalTime();
    }
}

public sealed class AIAgentAction
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid AIInteractionId { get; private set; }
    public string ToolName { get; private set; } = "";
    public string ArgsJson { get; private set; } = "{}";
    public AgentAuthorizationResult AuthorizationResult { get; private set; }
    public AgentExecutionStatus ExecutionStatus { get; private set; }
    public string? ResultRef { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private AIAgentAction() { }

    public static AIAgentAction Create(
        Guid tenantId,
        Guid aiInteractionId,
        string toolName,
        string argsJson,
        AgentAuthorizationResult authorizationResult,
        AgentExecutionStatus executionStatus,
        string? resultRef,
        DateTimeOffset createdAt)
    {
        return new AIAgentAction
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            AIInteractionId = EntityRules.Id(aiInteractionId, nameof(aiInteractionId)),
            ToolName = string.IsNullOrWhiteSpace(toolName) ? throw new ArgumentException("ToolName is required.", nameof(toolName)) : toolName.Trim(),
            ArgsJson = string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson,
            AuthorizationResult = authorizationResult,
            ExecutionStatus = executionStatus,
            ResultRef = resultRef,
            CreatedAt = createdAt.ToUniversalTime()
        };
    }
}
