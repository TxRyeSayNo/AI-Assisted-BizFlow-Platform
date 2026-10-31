using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Domain.AI;

namespace BizFlow.Application.AI;

public sealed record AiMemoryItemDto(
    Guid Id,
    string MemoryScope,
    string Title,
    string Content,
    string? MetadataJson,
    DateTimeOffset CreatedAt);

public sealed record AiMemorySearchResult(
    Guid Id,
    string MemoryScope,
    string Title,
    string Content,
    float SimilarityScore,
    string? MetadataJson);

public sealed record StoreMemoryCommand(
    string MemoryScope,
    string Title,
    string Content,
    string? MetadataJson = null);

public sealed record SearchMemoryQuery(
    string Query,
    string? MemoryScope = null,
    int MaxResults = 5,
    float MinScore = 0.4f);

public interface IAiEmbeddingService
{
    Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default);
}

public interface IAiVectorMemoryStore
{
    Task<AiVectorMemory> StoreMemoryAsync(
        Guid tenantId,
        string scope,
        string title,
        string content,
        string? metadataJson,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiMemorySearchResult>> SearchMemoriesAsync(
        Guid tenantId,
        string query,
        string? scope = null,
        int maxResults = 5,
        float minScore = 0.4f,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AiVectorMemory>> GetRecentMemoriesAsync(
        Guid tenantId,
        string? scope = null,
        int limit = 20,
        CancellationToken cancellationToken = default);
}

