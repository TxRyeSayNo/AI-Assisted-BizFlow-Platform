using System;

namespace BizFlow.Application.AI;

public sealed record AiPendingActionTokenPayload(
    Guid TenantId,
    Guid UserId,
    string ToolName,
    string ArgsHash,
    DateTimeOffset ExpiresAt,
    Guid Nonce);

public interface IAiConfirmationTokenService
{
    string CreateConfirmationToken(Guid tenantId, Guid userId, string toolName, string argsJson, TimeSpan? lifetime = null);
    AiPendingActionTokenPayload ValidateAndConsumeToken(string token, Guid tenantId, Guid userId, string toolName, string argsJson);
}
