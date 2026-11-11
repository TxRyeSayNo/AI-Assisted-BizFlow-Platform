using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Domain.AI;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.AI;

public sealed class AiLearnedChoiceStore(BizFlowDbContext dbContext) : IAiLearnedChoiceStore
{
    public async Task<AiLearnedChoice> RecordChoiceAsync(
        Guid tenantId,
        Guid? userId,
        string semanticKey,
        string choiceValue,
        string choiceLabel,
        string scope,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        var normKey = semanticKey.Trim().ToLowerInvariant();
        var normValue = choiceValue.Trim();
        var isTenantScope = string.Equals(scope, "TENANT", StringComparison.OrdinalIgnoreCase);

        var existing = await dbContext.AiLearnedChoices
            .FirstOrDefaultAsync(c =>
                c.TenantId == tenantId &&
                c.SemanticKey == normKey &&
                c.ChoiceValue == normValue &&
                (c.Scope == "TENANT" || c.UserId == userId),
                cancellationToken);

        if (existing != null)
        {
            existing.RecordUsage(now);
            await dbContext.SaveChangesAsync(cancellationToken);
            return existing;
        }

        var newChoice = AiLearnedChoice.Create(
            tenantId,
            userId,
            normKey,
            normValue,
            choiceLabel,
            scope,
            now);

        dbContext.AiLearnedChoices.Add(newChoice);
        await dbContext.SaveChangesAsync(cancellationToken);
        return newChoice;
    }

    public async Task<IReadOnlyList<LearnedChoiceDto>> GetRelevantChoicesAsync(
        Guid tenantId,
        Guid? userId,
        string semanticKey,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var normKey = semanticKey.Trim().ToLowerInvariant();

        var query = dbContext.AiLearnedChoices.AsNoTracking()
            .Where(c => c.TenantId == tenantId && c.SemanticKey == normKey);

        if (userId.HasValue)
        {
            query = query.Where(c => c.Scope == "TENANT" || c.UserId == userId.Value);
        }
        else
        {
            query = query.Where(c => c.Scope == "TENANT");
        }

        var results = await query
            .OrderByDescending(c => c.UsageCount)
            .ThenByDescending(c => c.LastUsedAt)
            .Take(limit)
            .Select(c => new LearnedChoiceDto(
                c.Id,
                c.TenantId,
                c.UserId,
                c.SemanticKey,
                c.ChoiceValue,
                c.ChoiceLabel,
                c.Scope,
                c.UsageCount,
                c.LastUsedAt,
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        return results;
    }

    public async Task<IReadOnlyList<LearnedChoiceDto>> ListChoicesAsync(
        Guid tenantId,
        Guid? userId,
        string? semanticKey = null,
        CancellationToken cancellationToken = default)
    {
        var query = dbContext.AiLearnedChoices.AsNoTracking()
            .Where(c => c.TenantId == tenantId);

        if (userId.HasValue)
        {
            query = query.Where(c => c.Scope == "TENANT" || c.UserId == userId.Value);
        }
        else
        {
            query = query.Where(c => c.Scope == "TENANT");
        }

        if (!string.IsNullOrWhiteSpace(semanticKey))
        {
            var normKey = semanticKey.Trim().ToLowerInvariant();
            query = query.Where(c => c.SemanticKey == normKey);
        }

        var results = await query
            .OrderByDescending(c => c.LastUsedAt)
            .Select(c => new LearnedChoiceDto(
                c.Id,
                c.TenantId,
                c.UserId,
                c.SemanticKey,
                c.ChoiceValue,
                c.ChoiceLabel,
                c.Scope,
                c.UsageCount,
                c.LastUsedAt,
                c.CreatedAt))
            .ToListAsync(cancellationToken);

        return results;
    }

    public async Task<bool> DeleteChoiceAsync(
        Guid tenantId,
        Guid? userId,
        Guid choiceId,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.AiLearnedChoices
            .FirstOrDefaultAsync(c => c.Id == choiceId && c.TenantId == tenantId, cancellationToken);

        if (item == null) return false;

        // If it's a personal choice, caller must match UserId
        if (item.Scope == "PERSONAL" && userId.HasValue && item.UserId != userId.Value)
        {
            return false;
        }

        dbContext.AiLearnedChoices.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<bool> PromoteToTenantScopeAsync(
        Guid tenantId,
        Guid choiceId,
        CancellationToken cancellationToken = default)
    {
        var item = await dbContext.AiLearnedChoices
            .FirstOrDefaultAsync(c => c.Id == choiceId && c.TenantId == tenantId, cancellationToken);

        if (item == null) return false;

        item.PromoteToTenantScope();
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }
}

