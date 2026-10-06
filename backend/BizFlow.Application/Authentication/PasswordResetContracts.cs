namespace BizFlow.Application.Authentication;

public sealed record PasswordResetTicket(Guid UserId, Guid? TenantId, string SecurityStamp, string NormalizedEmail, DateTimeOffset ExpiresAt)
{
    public override string ToString() => "[REDACTED PASSWORD RESET TICKET]";
}
public interface IPasswordResetTokenProvider
{
    string Issue(PasswordResetTicket ticket, DateTimeOffset requestedAt);
    Task<PasswordResetTicket?> ValidateAsync(string token, CancellationToken cancellationToken);
}
public interface IPasswordResetQueue
{
    void Enqueue(string normalizedIdentifier, string? tenantKey, DateTimeOffset requestedAt);
}
public interface IPasswordResetEmailSender
{
    Task SendAsync(string address, string token, CancellationToken cancellationToken);
}

public sealed class PasswordResetPolicy
{
    public int TokenLifetimeMinutes { get; init; } = 15;
    public int MinimumPasswordLength { get; init; } = 12;
    public int MinimumDistinctCharacters { get; init; } = 4;
    public bool IsValid => TokenLifetimeMinutes is >= 1 and <= 120 && MinimumPasswordLength is >= 12 and <= 128 && MinimumDistinctCharacters is >= 1 and <= 10;
    public bool Accepts(string? password) => !string.IsNullOrWhiteSpace(password) &&
        password.Length >= MinimumPasswordLength && password.Length <= 1024 && password.Distinct().Count() >= MinimumDistinctCharacters;
}
