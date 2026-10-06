using System.Security.Cryptography;
using System.Text;
using BizFlow.Application.Security;

namespace BizFlow.Infrastructure.Authentication;

public sealed class RefreshTokenGenerator : IRefreshTokenGenerator
{
    public IssuedRefreshToken Generate()
    {
        var token = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(64));
        return new IssuedRefreshToken(token, Hash(token));
    }

    public string Hash(string token)
    {
        if (token.Length != 128 || token.Any(character => !char.IsAsciiHexDigit(character)))
            throw new ArgumentException("Invalid refresh token format.", nameof(token));
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.ASCII.GetBytes(token)));
    }
}
