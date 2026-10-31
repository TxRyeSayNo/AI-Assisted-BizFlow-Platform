using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BizFlow.Application.AI;

public sealed record ReflectionCritiqueRule(
    string RuleId,
    string RecommendationType,
    string Scope,
    string CritiqueSummary,
    decimal ConfidencePenalty,
    DateTimeOffset DiscoveredAt);

public sealed record ReflectionMetricsDto(
    int TotalDecisions,
    int AcceptedCount,
    int OverriddenCount,
    int DismissedCount,
    decimal AcceptanceRate,
    decimal OverrideRate,
    decimal AccuracyScore,
    IReadOnlyList<ReflectionCritiqueRule> ActiveCritiqueRules);

public interface IAiSelfReflectionService
{
    Task<ReflectionMetricsDto> GetReflectionMetricsAsync(Guid tenantId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReflectionCritiqueRule>> GetCritiqueRulesAsync(Guid tenantId, string? recommendationType = null, CancellationToken cancellationToken = default);
    Task<string> BuildReflectionDirectivesPromptAsync(Guid tenantId, string recommendationType, CancellationToken cancellationToken = default);
}

