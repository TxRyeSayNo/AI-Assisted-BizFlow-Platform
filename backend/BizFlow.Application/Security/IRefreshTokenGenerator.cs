namespace BizFlow.Application.Security;

public interface IRefreshTokenGenerator
{
    IssuedRefreshToken Generate();
    string Hash(string token);
}

public sealed class IssuedRefreshToken(string secret, string hash)
{
    public string Secret { get; } = secret;
    public string Hash { get; } = hash;
    public override string ToString() => "[REDACTED REFRESH TOKEN]";
}
