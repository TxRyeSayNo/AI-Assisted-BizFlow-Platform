using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace BizFlow.Application.AI;

public sealed record ConversationState(
    string ConversationId,
    Guid TenantId,
    Guid UserId,
    string? ActiveIntent,
    Dictionary<string, string> ExtractedSlots,
    List<string> MissingSlots,
    AgentClarificationFormDto? PendingClarification,
    List<RobotChatMessageDto> MessageHistory,
    DateTimeOffset LastActiveAt)
{
    public static ConversationState CreateInitial(Guid tenantId, Guid userId, string conversationId, DateTimeOffset now) =>
        new(conversationId, tenantId, userId, null, new(StringComparer.OrdinalIgnoreCase), [], null, [], now);
}

public interface IConversationStateManager
{
    Task<ConversationState?> GetStateAsync(Guid tenantId, Guid userId, string conversationId, CancellationToken cancellationToken = default);
    Task SaveStateAsync(ConversationState state, CancellationToken cancellationToken = default);
    Task ClearStateAsync(Guid tenantId, Guid userId, string conversationId, CancellationToken cancellationToken = default);
}

