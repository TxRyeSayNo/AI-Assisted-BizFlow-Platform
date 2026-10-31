using System;
using System.Text.Json;
using BizFlow.Domain.Common;

namespace BizFlow.Domain.AI;

public sealed class AiVectorMemory
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string MemoryScope { get; private set; } = "POLICY";
    public string Title { get; private set; } = "";
    public string Content { get; private set; } = "";
    public string? MetadataJson { get; private set; }
    public string EmbeddingJson { get; private set; } = "[]";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset? UpdatedAt { get; private set; }

    private AiVectorMemory() { }

    public static AiVectorMemory Create(
        Guid tenantId,
        string memoryScope,
        string title,
        string content,
        string? metadataJson,
        float[]? embedding,
        DateTimeOffset createdAt)
    {
        var item = new AiVectorMemory
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            MemoryScope = string.IsNullOrWhiteSpace(memoryScope) ? "POLICY" : memoryScope.Trim().ToUpperInvariant(),
            Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("Title is required.", nameof(title)) : title.Trim(),
            Content = string.IsNullOrWhiteSpace(content) ? throw new ArgumentException("Content is required.", nameof(content)) : content.Trim(),
            MetadataJson = metadataJson,
            CreatedAt = createdAt.ToUniversalTime()
        };

        if (embedding != null && embedding.Length > 0)
        {
            item.SetEmbedding(embedding);
        }

        return item;
    }

    public void SetEmbedding(float[] embedding)
    {
        EmbeddingJson = JsonSerializer.Serialize(embedding);
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public float[] GetEmbedding()
    {
        if (string.IsNullOrWhiteSpace(EmbeddingJson) || EmbeddingJson == "[]")
            return Array.Empty<float>();

        try
        {
            return JsonSerializer.Deserialize<float[]>(EmbeddingJson) ?? Array.Empty<float>();
        }
        catch
        {
            return Array.Empty<float>();
        }
    }

    public void Update(string title, string content, string? metadataJson, float[]? embedding, DateTimeOffset now)
    {
        Title = string.IsNullOrWhiteSpace(title) ? throw new ArgumentException("Title is required.", nameof(title)) : title.Trim();
        Content = string.IsNullOrWhiteSpace(content) ? throw new ArgumentException("Content is required.", nameof(content)) : content.Trim();
        MetadataJson = metadataJson;
        if (embedding != null && embedding.Length > 0)
        {
            EmbeddingJson = JsonSerializer.Serialize(embedding);
        }
        UpdatedAt = now.ToUniversalTime();
    }
}

