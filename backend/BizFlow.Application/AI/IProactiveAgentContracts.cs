using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Domain.AI;

namespace BizFlow.Application.AI;

public sealed record ProactiveScanResult(
    int SlaRisksDetected,
    int WorkloadImbalancesDetected,
    int RecommendationsCreated,
    DateTimeOffset ScannedAt,
    string Summary);

public sealed record ProactiveInsightDto(
    Guid RecommendationId,
    string RecommendationType,
    string ObjectType,
    Guid? ObjectId,
    string Title,
    string Description,
    decimal Confidence,
    string? SuggestedAction,
    DateTimeOffset CreatedAt);

public interface IProactiveAgentScanner
{
    Task<ProactiveScanResult> RunProactiveScanAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ProactiveInsightDto>> GetActiveProactiveInsightsAsync(Guid tenantId, CancellationToken cancellationToken = default);
}

