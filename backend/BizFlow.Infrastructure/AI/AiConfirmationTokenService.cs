using System;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;

namespace BizFlow.Infrastructure.AI;

public sealed class AiConfirmationTokenService : IAiConfirmationTokenService
{
    private readonly byte[] _hmacKey;
    private readonly TimeProvider _clock;
    private readonly IMemoryCache _cache;

    public AiConfirmationTokenService(
        IConfiguration configuration,
        IMemoryCache cache,
        TimeProvider? clock = null)
    {
        _cache = cache;
        _clock = clock ?? TimeProvider.System;

        var secret = configuration["AI:ConfirmationSecret"]
            ?? configuration["AI_CONFIRMATION_SECRET"]
            ?? "BizFlow-AI-Action-Confirmation-Super-Secret-Key-2026-Strict-Security!";

        _hmacKey = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public string CreateConfirmationToken(
        Guid tenantId,
        Guid userId,
        string toolName,
        string argsJson,
        TimeSpan? lifetime = null)
    {
        var expiresAt = _clock.GetUtcNow().Add(lifetime ?? TimeSpan.FromMinutes(5));
        var nonce = Guid.NewGuid();
        var argsHash = ComputeArgsHash(argsJson);

        var payload = new AiPendingActionTokenPayload(
            tenantId,
            userId,
            toolName.Trim().ToLowerInvariant(),
            argsHash,
            expiresAt,
            nonce);

        var payloadBytes = JsonSerializer.SerializeToUtf8Bytes(payload);
        var payloadB64 = Base64UrlEncode(payloadBytes);

        var sigBytes = HMACSHA256.HashData(_hmacKey, Encoding.UTF8.GetBytes(payloadB64));
        var sigB64 = Base64UrlEncode(sigBytes);

        return $"{payloadB64}.{sigB64}";
    }

    public AiPendingActionTokenPayload ValidateAndConsumeToken(
        string token,
        Guid tenantId,
        Guid userId,
        string toolName,
        string argsJson)
    {
        if (string.IsNullOrWhiteSpace(token))
            throw new ApplicationFault(FaultKind.Validation, "AI.CONFIRMATION_TOKEN_REQUIRED", "Executing this action requires a valid confirmation token.");

        var parts = token.Split('.');
        if (parts.Length != 2)
            throw new ApplicationFault(FaultKind.Forbidden, "AI.CONFIRMATION_INVALID", "Malformed confirmation token.");

        var payloadB64 = parts[0];
        var sigB64 = parts[1];

        // 1. Verify signature
        var expectedSigBytes = HMACSHA256.HashData(_hmacKey, Encoding.UTF8.GetBytes(payloadB64));
        var actualSigBytes = Base64UrlDecode(sigB64);

        if (!CryptographicOperations.FixedTimeEquals(expectedSigBytes, actualSigBytes))
            throw new ApplicationFault(FaultKind.Forbidden, "AI.CONFIRMATION_TAMPERED", "Confirmation token signature is invalid.");

        // 2. Parse payload
        AiPendingActionTokenPayload payload;
        try
        {
            var payloadBytes = Base64UrlDecode(payloadB64);
            payload = JsonSerializer.Deserialize<AiPendingActionTokenPayload>(payloadBytes)
                ?? throw new FormatException();
        }
        catch
        {
            throw new ApplicationFault(FaultKind.Forbidden, "AI.CONFIRMATION_INVALID", "Malformed confirmation token payload.");
        }

        // 3. Expiration check
        if (payload.ExpiresAt <= _clock.GetUtcNow())
            throw new ApplicationFault(FaultKind.Validation, "AI.CONFIRMATION_EXPIRED", "Confirmation token has expired.");

        // 4. Tenant isolation check
        if (payload.TenantId != tenantId)
            throw new ApplicationFault(FaultKind.Forbidden, "AI.CROSS_TENANT_CONFIRMATION", "Token does not match active workspace.");

        // 5. Actor binding check
        if (payload.UserId != userId)
            throw new ApplicationFault(FaultKind.Forbidden, "AI.ACTOR_MISMATCH", "Token was issued for a different user.");

        // 6. Tool binding check
        if (!string.Equals(payload.ToolName, toolName.Trim(), StringComparison.OrdinalIgnoreCase))
            throw new ApplicationFault(FaultKind.Validation, "AI.TOOL_MISMATCH", "Token was issued for a different tool.");

        // 7. Argument tamper check
        var currentArgsHash = ComputeArgsHash(argsJson);
        if (!string.Equals(payload.ArgsHash, currentArgsHash, StringComparison.OrdinalIgnoreCase))
            throw new ApplicationFault(FaultKind.Validation, "AI.ARGS_TAMPERED", "Action arguments do not match the pending proposal.");

        // 8. Replay check (single-use token)
        var cacheKey = $"consumed_ai_action_token:{payload.Nonce}";
        if (_cache.TryGetValue(cacheKey, out _))
            throw new ApplicationFault(FaultKind.Conflict, "AI.CONFIRMATION_ALREADY_USED", "This confirmation token has already been executed.");

        var remainingTime = payload.ExpiresAt - _clock.GetUtcNow();
        if (remainingTime > TimeSpan.Zero)
        {
            _cache.Set(cacheKey, true, remainingTime);
        }

        return payload;
    }

    private static string ComputeArgsHash(string argsJson)
    {
        if (string.IsNullOrWhiteSpace(argsJson))
            argsJson = "{}";

        try
        {
            // Normalize JSON structure
            using var doc = JsonDocument.Parse(argsJson);
            var normalizedBytes = JsonSerializer.SerializeToUtf8Bytes(doc.RootElement);
            return Convert.ToHexString(SHA256.HashData(normalizedBytes));
        }
        catch
        {
            return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(argsJson.Trim())));
        }
    }

    private static string Base64UrlEncode(byte[] input) =>
        Convert.ToBase64String(input)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');

    private static byte[] Base64UrlDecode(string input)
    {
        var output = input.Replace("-", "+").Replace("_", "/");
        switch (output.Length % 4)
        {
            case 2: output += "=="; break;
            case 3: output += "="; break;
        }
        return Convert.FromBase64String(output);
    }
}
