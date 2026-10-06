namespace BizFlow.Domain.Authentication;

/// <summary>
/// ADR-0002 / FR-AUTH-003. Persistence must atomically save the consumed session and its successor
/// with optimistic concurrency, and revoke the entire family when a consumed token is replayed.
/// </summary>
public sealed class AuthenticationSession
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public Guid FamilyId { get; private set; }
    public string TokenHash { get; private set; } = "";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset ExpiresAt { get; private set; }
    public DateTimeOffset? RevokedAt { get; private set; }
    public Guid? ReplacedById { get; private set; }

    private AuthenticationSession() { }

    public static AuthenticationSession Create(Guid userId, string tokenHash,
        DateTimeOffset now, DateTimeOffset expiresAt)
    {
        if (userId == Guid.Empty) throw new ArgumentException("A session must belong to a user.", nameof(userId));
        ValidateHash(tokenHash);
        if (expiresAt <= now) throw new ArgumentException("Session expiry must follow creation.", nameof(expiresAt));
        var id = Guid.CreateVersion7();
        return new AuthenticationSession
        {
            Id = id, UserId = userId, FamilyId = id, TokenHash = tokenHash.ToLowerInvariant(),
            CreatedAt = now.ToUniversalTime(), ExpiresAt = expiresAt.ToUniversalTime()
        };
    }

    public bool CanRefresh(DateTimeOffset now) => RevokedAt is null && now >= CreatedAt && now < ExpiresAt;

    public AuthenticationSession Rotate(string newTokenHash, DateTimeOffset now)
    {
        if (!CanRefresh(now)) throw new InvalidOperationException("The session is no longer refreshable.");
        ValidateHash(newTokenHash);
        if (string.Equals(newTokenHash, TokenHash, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Rotation requires a new token.", nameof(newTokenHash));
        var replacement = Create(UserId, newTokenHash, now, ExpiresAt);
        replacement.FamilyId = FamilyId;
        ReplacedById = replacement.Id;
        Revoke(now);
        return replacement;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (now < CreatedAt) throw new ArgumentException("Revocation cannot precede creation.", nameof(now));
        RevokedAt ??= now.ToUniversalTime();
    }

    private static void ValidateHash(string hash)
    {
        if (hash.Length != 64 || hash.Any(character => !char.IsAsciiHexDigit(character)))
            throw new ArgumentException("A SHA-256 token hash is required.", nameof(hash));
    }
}
