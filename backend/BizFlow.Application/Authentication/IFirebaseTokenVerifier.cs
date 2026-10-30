using System.Threading;
using System.Threading.Tasks;

namespace BizFlow.Application.Authentication;

public interface IFirebaseTokenVerifier
{
    Task<FirebaseVerifiedUser> VerifyIdTokenAsync(string idToken, CancellationToken cancellationToken = default);
}

public sealed record FirebaseVerifiedUser(
    string Uid,
    string Email,
    bool EmailVerified);
