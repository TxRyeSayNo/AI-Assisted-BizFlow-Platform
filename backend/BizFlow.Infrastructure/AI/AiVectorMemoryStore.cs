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

public sealed class AiVectorMemoryStore : IAiVectorMemoryStore
{
    private readonly BizFlowDbContext _dbContext;
    private readonly IAiEmbeddingService _embeddingService;

    public AiVectorMemoryStore(BizFlowDbContext dbContext, IAiEmbeddingService embeddingService)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
    }

    public async Task<AiVectorMemory> StoreMemoryAsync(
        Guid tenantId,
        string scope,
        string title,
        string content,
        string? metadataJson,
        CancellationToken cancellationToken = default)
    {
        var embedding = await _embeddingService.GenerateEmbeddingAsync($"{title}\n{content}", cancellationToken);
        var memory = AiVectorMemory.Create(
            tenantId,
            scope,
            title,
            content,
            metadataJson,
            embedding,
            DateTimeOffset.UtcNow);

        _dbContext.AIVectorMemories.Add(memory);
        await _dbContext.SaveChangesAsync(cancellationToken);
        return memory;
    }

    public async Task<IReadOnlyList<AiMemorySearchResult>> SearchMemoriesAsync(
        Guid tenantId,
        string query,
        string? scope = null,
        int maxResults = 5,
        float minScore = 0.4f,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query))
            return Array.Empty<AiMemorySearchResult>();

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
        var normalizedScope = string.IsNullOrWhiteSpace(scope) ? null : scope.Trim().ToUpperInvariant();

        var queryable = _dbContext.AIVectorMemories
            .Where(m => m.TenantId == tenantId);

        if (normalizedScope != null)
        {
            queryable = queryable.Where(m => m.MemoryScope == normalizedScope);
        }

        var candidates = await queryable
            .OrderByDescending(m => m.CreatedAt)
            .Take(100)
            .ToListAsync(cancellationToken);

        var scored = new List<AiMemorySearchResult>();

        foreach (var item in candidates)
        {
            var itemEmbedding = item.GetEmbedding();
            if (itemEmbedding.Length == 0)
                continue;

            var similarity = ComputeCosineSimilarity(queryEmbedding, itemEmbedding);
            if (similarity >= minScore)
            {
                scored.Add(new AiMemorySearchResult(
                    Id: item.Id,
                    MemoryScope: item.MemoryScope,
                    Title: item.Title,
                    Content: item.Content,
                    SimilarityScore: similarity,
                    MetadataJson: item.MetadataJson));
            }
        }

        return scored
            .OrderByDescending(s => s.SimilarityScore)
            .Take(maxResults)
            .ToList();
    }

    public async Task<IReadOnlyList<AiVectorMemory>> GetRecentMemoriesAsync(
        Guid tenantId,
        string? scope = null,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var queryable = _dbContext.AIVectorMemories
            .Where(m => m.TenantId == tenantId);

        if (!string.IsNullOrWhiteSpace(scope))
        {
            var normScope = scope.Trim().ToUpperInvariant();
            queryable = queryable.Where(m => m.MemoryScope == normScope);
        }

        return await queryable
            .OrderByDescending(m => m.CreatedAt)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public static float ComputeCosineSimilarity(float[] a, float[] b)
    {
        if (a == null || b == null || a.Length == 0 || b.Length == 0)
            return 0f;

        var len = Math.Min(a.Length, b.Length);
        double dotProduct = 0.0;
        double normA = 0.0;
        double normB = 0.0;

        for (int i = 0; i < len; i++)
        {
            dotProduct += a[i] * b[i];
            normA += a[i] * a[i];
            normB += b[i] * b[i];
        }

        if (normA <= 0.0 || normB <= 0.0)
            return 0f;

        return (float)(dotProduct / (Math.Sqrt(normA) * Math.Sqrt(normB)));
    }
}

