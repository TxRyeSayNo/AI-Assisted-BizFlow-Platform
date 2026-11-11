using System;
using BizFlow.Domain.Common;

namespace BizFlow.Domain.AI;

public sealed class AiLearnedChoice
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public Guid? UserId { get; private set; }
    public string SemanticKey { get; private set; } = "";
    public string ChoiceValue { get; private set; } = "";
    public string ChoiceLabel { get; private set; } = "";
    public string Scope { get; private set; } = "PERSONAL"; // PERSONAL or TENANT
    public int UsageCount { get; private set; } = 1;
    public DateTimeOffset LastUsedAt { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }

    private AiLearnedChoice() { }

    public static AiLearnedChoice Create(
        Guid tenantId,
        Guid? userId,
        string semanticKey,
        string choiceValue,
        string choiceLabel,
        string scope,
        DateTimeOffset now)
    {
        if (string.IsNullOrWhiteSpace(semanticKey))
            throw new ArgumentException("SemanticKey is required.", nameof(semanticKey));
        if (string.IsNullOrWhiteSpace(choiceValue))
            throw new ArgumentException("ChoiceValue is required.", nameof(choiceValue));

        var cleanScope = string.Equals(scope, "TENANT", StringComparison.OrdinalIgnoreCase) ? "TENANT" : "PERSONAL";

        return new AiLearnedChoice
        {
            Id = Guid.CreateVersion7(),
            TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            UserId = cleanScope == "TENANT" ? null : userId,
            SemanticKey = semanticKey.Trim().ToLowerInvariant(),
            ChoiceValue = choiceValue.Trim(),
            ChoiceLabel = string.IsNullOrWhiteSpace(choiceLabel) ? choiceValue.Trim() : choiceLabel.Trim(),
            Scope = cleanScope,
            UsageCount = 1,
            LastUsedAt = now.ToUniversalTime(),
            CreatedAt = now.ToUniversalTime()
        };
    }

    public void RecordUsage(DateTimeOffset now)
    {
        UsageCount++;
        LastUsedAt = now.ToUniversalTime();
    }

    public void PromoteToTenantScope()
    {
        Scope = "TENANT";
        UserId = null;
    }
}

