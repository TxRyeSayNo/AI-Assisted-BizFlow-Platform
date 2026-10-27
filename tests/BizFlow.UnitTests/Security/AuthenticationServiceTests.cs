using BizFlow.Application.Authentication;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Authentication;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;

namespace BizFlow.UnitTests.Security;

public sealed class AuthenticationServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 0, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task LoginAsync_requires_workspace_key_and_returns_invalid_credentials_when_missing(string? workspaceKey)
    {
        var store = new FakeAuthenticationStore
        {
            CandidatesToReturn = [new AuthenticationCandidate(Guid.NewGuid(), Guid.NewGuid(), "Corp", "bizflow-corp")]
        };
        var verifier = new FakeCredentialVerifier();
        var service = CreateService(store, verifier);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            service.LoginAsync("EMP001", "Password123!", workspaceKey, default));

        Assert.Equal("AUTH.INVALID_CREDENTIALS", fault.Code);
        Assert.Equal(FaultKind.Unauthenticated, fault.Kind);
    }

    [Theory]
    [InlineData(null, "Password123!", "bizflow-corp")]
    [InlineData("", "Password123!", "bizflow-corp")]
    [InlineData("EMP001", null, "bizflow-corp")]
    [InlineData("EMP001", "", "bizflow-corp")]
    public async Task LoginAsync_requires_identifier_and_password(string? identifier, string? password, string? workspaceKey)
    {
        var store = new FakeAuthenticationStore();
        var verifier = new FakeCredentialVerifier();
        var service = CreateService(store, verifier);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            service.LoginAsync(identifier!, password!, workspaceKey, default));

        Assert.Equal("VALIDATION.FAILED", fault.Code);
        Assert.Equal(FaultKind.Validation, fault.Kind);
    }

    [Fact]
    public async Task LoginAsync_returns_invalid_credentials_when_no_candidate_matches_workspace_and_identifier()
    {
        var store = new FakeAuthenticationStore { CandidatesToReturn = [] };
        var verifier = new FakeCredentialVerifier();
        var service = CreateService(store, verifier);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            service.LoginAsync("EMP001", "Password123!", "nonexistent-workspace", default));

        Assert.Equal("AUTH.INVALID_CREDENTIALS", fault.Code);
        Assert.Equal(FaultKind.Unauthenticated, fault.Kind);
        Assert.True(verifier.DummyVerified);
    }

    [Fact]
    public async Task LoginAsync_returns_invalid_credentials_without_disclosing_multiple_candidates()
    {
        var store = new FakeAuthenticationStore
        {
            CandidatesToReturn =
            [
                new AuthenticationCandidate(Guid.NewGuid(), Guid.NewGuid(), "Tenant 1"),
                new AuthenticationCandidate(Guid.NewGuid(), Guid.NewGuid(), "Tenant 2")
            ]
        };
        var verifier = new FakeCredentialVerifier();
        var service = CreateService(store, verifier);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            service.LoginAsync("EMP001", "Password123!", "some-workspace", default));

        Assert.Equal("AUTH.INVALID_CREDENTIALS", fault.Code);
        Assert.Equal(FaultKind.Unauthenticated, fault.Kind);
        Assert.True(verifier.DummyVerified);
        // Ensure no company names leak
        Assert.False(fault.Details?.ContainsKey("companyNames") == true);
    }

    private static AuthenticationService CreateService(
        IAuthenticationStore store,
        ICredentialVerifier verifier)
    {
        return new AuthenticationService(
            store,
            verifier,
            new FakeRefreshTokenGenerator(),
            new FakeAccessTokenIssuer(),
            new FakeAttemptLimiter(),
            new AuthenticationPolicy(),
            TimeProvider.System);
    }

    private sealed class FakeAuthenticationStore : IAuthenticationStore
    {
        public string? LastSearchedTenantKey { get; private set; }
        public IReadOnlyList<AuthenticationCandidate> CandidatesToReturn { get; set; } = [];

        public Task<IReadOnlyList<AuthenticationCandidate>> FindCandidatesAsync(
            string normalizedIdentifier, string? tenantKey, CancellationToken cancellationToken)
        {
            LastSearchedTenantKey = tenantKey;
            return Task.FromResult(CandidatesToReturn);
        }

        public Task<AuthenticationCandidate?> FindSessionOwnerAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult<AuthenticationCandidate?>(null);

        public Task<IAuthenticationTransaction?> BeginAsync(AuthenticationCandidate candidate, CancellationToken cancellationToken) =>
            Task.FromResult<IAuthenticationTransaction?>(null);

        public Task<bool> ValidateAccessAsync(AccessTokenRequest identity, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class FakeCredentialVerifier : ICredentialVerifier
    {
        public bool DummyVerified { get; private set; }

        public PasswordCheck Verify(UserAccount user, string password) => new(true);

        public void VerifyDummy(string password) => DummyVerified = true;

        public string Hash(UserAccount user, string password) => "hash";
    }

    private sealed class FakeRefreshTokenGenerator : IRefreshTokenGenerator
    {
        public IssuedRefreshToken Generate() => new("token", "hash");
        public string Hash(string secret) => "hash";
    }

    private sealed class FakeAccessTokenIssuer : IAccessTokenIssuer
    {
        public IssuedAccessToken Issue(AccessTokenRequest request, DateTimeOffset now) =>
            new("access-token", now.AddMinutes(15));
    }

    private sealed class FakeAttemptLimiter : IAuthenticationAttemptLimiter
    {
        public void Check(AuthenticationCandidate candidate) { }
    }
}
