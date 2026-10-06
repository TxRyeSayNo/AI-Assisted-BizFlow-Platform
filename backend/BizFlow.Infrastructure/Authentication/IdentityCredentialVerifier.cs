using System.Security.Cryptography;
using BizFlow.Application.Authentication;
using BizFlow.Domain.Organization;
using Microsoft.AspNetCore.Identity;

namespace BizFlow.Infrastructure.Authentication;

public sealed class IdentityCredentialVerifier : ICredentialVerifier
{
    private readonly PasswordHasher<UserAccount> hasher = new();
    private readonly UserAccount dummy;
    private readonly string dummyHash;

    public IdentityCredentialVerifier()
    {
        dummy = UserAccount.CreatePlatformAdministrator("DUMMY", "dummy@example.invalid", "Dummy", "placeholder", DateTimeOffset.UtcNow);
        dummyHash = hasher.HashPassword(dummy, Convert.ToHexString(RandomNumberGenerator.GetBytes(32)));
    }

    public PasswordCheck Verify(UserAccount user, string password)
    {
        PasswordVerificationResult result;
        try { result = hasher.VerifyHashedPassword(user, user.PasswordHash, password); }
        catch (FormatException) { VerifyDummy(password); return new(false); }
        return result switch
        {
            PasswordVerificationResult.Success => new(true),
            PasswordVerificationResult.SuccessRehashNeeded => new(true, hasher.HashPassword(user, password)),
            _ => new(false)
        };
    }
    public void VerifyDummy(string password) => hasher.VerifyHashedPassword(dummy, dummyHash, password);
    public string Hash(UserAccount user, string password) => hasher.HashPassword(user, password);
}
