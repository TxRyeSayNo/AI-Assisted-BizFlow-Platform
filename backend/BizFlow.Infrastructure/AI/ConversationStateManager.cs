using System;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;

namespace BizFlow.Infrastructure.AI;

public sealed class ConversationStateManager(IBizFlowCacheService cacheService) : IConversationStateManager
{
    private static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(24);

    public Task<ConversationState?> GetStateAsync(Guid tenantId, Guid userId, string conversationId, CancellationToken cancellationToken = default)
    {
        var key = BuildCacheKey(tenantId, userId, conversationId);
        return cacheService.GetAsync<ConversationState>(key, cancellationToken);
    }

    public Task SaveStateAsync(ConversationState state, CancellationToken cancellationToken = default)
    {
        var key = BuildCacheKey(state.TenantId, state.UserId, state.ConversationId);
        return cacheService.SetAsync(key, state, DefaultTtl, cancellationToken);
    }

    public Task ClearStateAsync(Guid tenantId, Guid userId, string conversationId, CancellationToken cancellationToken = default)
    {
        var key = BuildCacheKey(tenantId, userId, conversationId);
        return cacheService.RemoveAsync(key, cancellationToken);
    }

    private static string BuildCacheKey(Guid tenantId, Guid userId, string conversationId) =>
        $"ai:conv:{tenantId:N}:{userId:N}:{conversationId.Trim().ToLowerInvariant()}";
}

