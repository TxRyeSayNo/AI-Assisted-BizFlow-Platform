using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Domain.AI;

namespace BizFlow.Application.AI;

public sealed record LearnedChoiceDto(
    Guid Id,
    Guid TenantId,
    Guid? UserId,
    string SemanticKey,
    string ChoiceValue,
    string ChoiceLabel,
    string Scope,
    int UsageCount,
    DateTimeOffset LastUsedAt,
    DateTimeOffset CreatedAt);

public sealed record SaveLearnedChoiceCommand(
    string SemanticKey,
    string ChoiceValue,
    string ChoiceLabel,
    string Scope = "PERSONAL");

public interface IAiLearnedChoiceStore
{
    Task<AiLearnedChoice> RecordChoiceAsync(
        Guid tenantId,
        Guid? userId,
        string semanticKey,
        string choiceValue,
        string choiceLabel,
        string scope,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearnedChoiceDto>> GetRelevantChoicesAsync(
        Guid tenantId,
        Guid? userId,
        string semanticKey,
        int limit = 5,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LearnedChoiceDto>> ListChoicesAsync(
        Guid tenantId,
        Guid? userId,
        string? semanticKey = null,
        CancellationToken cancellationToken = default);

    Task<bool> DeleteChoiceAsync(
        Guid tenantId,
        Guid? userId,
        Guid choiceId,
        CancellationToken cancellationToken = default);

    Task<bool> PromoteToTenantScopeAsync(
        Guid tenantId,
        Guid choiceId,
        CancellationToken cancellationToken = default);
}

