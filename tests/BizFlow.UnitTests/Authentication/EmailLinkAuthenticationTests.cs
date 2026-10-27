using BizFlow.Api.Controllers;
using BizFlow.Application.Authentication;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Authentication;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;

namespace BizFlow.UnitTests.Authentication;

public sealed class EmailLinkAuthenticationTests
{
    private static readonly DateTimeOffset Now = new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("notanemail")]
    public async Task RequestLinkAsync_rejects_invalid_email(string? email)
    {
        var store = new FakeAuthenticationStore();
        var cache = new FakeCacheService();
        var authService = CreateAuthService(store);
        var service = new EmailLinkAuthenticationService(store, cache, authService);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            service.RequestLinkAsync(email!, default));

        Assert.Equal("VALIDATION.FAILED", fault.Code);
        Assert.Equal(FaultKind.Validation, fault.Kind);
    }

    [Fact]
    public async Task RequestLinkAsync_throws_UserNotFound_when_email_has_no_active_candidate()
    {
        var store = new FakeAuthenticationStore { CandidatesToReturn = [] };
        var cache = new FakeCacheService();
        var authService = CreateAuthService(store);
        var service = new EmailLinkAuthenticationService(store, cache, authService);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            service.RequestLinkAsync("unknown@example.com", default));

        Assert.Equal("AUTH.USER_NOT_FOUND", fault.Code);
        Assert.Equal(FaultKind.NotFound, fault.Kind);
        Assert.Contains("/register-company", fault.Message);
    }

    [Fact]
    public async Task RequestLinkAsync_generates_token_and_caches_with_15min_ttl()
    {
        var candidate = new AuthenticationCandidate(Guid.NewGuid(), Guid.NewGuid(), "Acme Corp", "acme");
        var store = new FakeAuthenticationStore { CandidatesToReturn = [candidate] };
        var cache = new FakeCacheService();
        var authService = CreateAuthService(store);
        var service = new EmailLinkAuthenticationService(store, cache, authService);

        var result = await service.RequestLinkAsync("user@example.com", default);

        Assert.Equal(15, result.ExpiresInMinutes);
        Assert.Contains("sign-in link has been sent", result.Message);
        Assert.Single(cache.Store);
        var cachedEntry = cache.Store.First();
        Assert.StartsWith("email_link:", cachedEntry.Key);
        Assert.Equal("USER@EXAMPLE.COM", cachedEntry.Value.Value);
        Assert.Equal(TimeSpan.FromMinutes(15), cachedEntry.Value.Ttl);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyLinkAsync_rejects_empty_token(string? token)
    {
        var store = new FakeAuthenticationStore();
        var cache = new FakeCacheService();
        var authService = CreateAuthService(store);
        var service = new EmailLinkAuthenticationService(store, cache, authService);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            service.VerifyLinkAsync(token!, default));

        Assert.Equal("AUTH.INVALID_TOKEN", fault.Code);
    }

    [Fact]
    public async Task VerifyLinkAsync_throws_TokenExpired_when_token_not_found_or_expired()
    {
        var store = new FakeAuthenticationStore();
        var cache = new FakeCacheService(); // Empty cache
        var authService = CreateAuthService(store);
        var service = new EmailLinkAuthenticationService(store, cache, authService);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            service.VerifyLinkAsync("nonexistent-token", default));

        Assert.Equal("AUTH.TOKEN_EXPIRED", fault.Code);
        Assert.Equal(FaultKind.Unauthenticated, fault.Kind);
    }

    [Fact]
    public async Task VerifyLinkAsync_returns_NoWorkspace_if_candidate_was_deactivated_or_deleted()
    {
        var cache = new FakeCacheService();
        cache.Store["email_link:validtoken"] = ("USER@EXAMPLE.COM", TimeSpan.FromMinutes(15));
        var store = new FakeAuthenticationStore { CandidatesToReturn = [] };
        var authService = CreateAuthService(store);
        var service = new EmailLinkAuthenticationService(store, cache, authService);

        var result = await service.VerifyLinkAsync("validtoken", default);

        Assert.Equal("NoWorkspace", result.Status);
        Assert.Equal("/register-company", result.RegisterCompanyUrl);
        // Token must be consumed
        Assert.False(cache.Store.ContainsKey("email_link:validtoken"));
    }

    [Fact]
    public async Task VerifyLinkAsync_single_workspace_authenticates_directly()
    {
        var tenantId = Guid.NewGuid();
        var candidate = new AuthenticationCandidate(Guid.NewGuid(), tenantId, "Acme Corp", "acme");
        var cache = new FakeCacheService();
        cache.Store["email_link:validtoken"] = ("USER@EXAMPLE.COM", TimeSpan.FromMinutes(15));
        var store = new FakeAuthenticationStore
        {
            CandidatesToReturn = [candidate],
            TransactionToReturn = new FakeTransaction(candidate)
        };
        var authService = CreateAuthService(store);
        var service = new EmailLinkAuthenticationService(store, cache, authService);

        var result = await service.VerifyLinkAsync("validtoken", default);

        Assert.Equal("Authenticated", result.Status);
        Assert.NotNull(result.Authentication);
        Assert.Equal("access-token", result.Authentication.AccessToken);
        Assert.False(cache.Store.ContainsKey("email_link:validtoken"));
    }

    [Fact]
    public async Task VerifyLinkAsync_multiple_workspaces_returns_RequiresWorkspaceSelection_and_ticket()
    {
        var c1 = new AuthenticationCandidate(Guid.NewGuid(), Guid.NewGuid(), "Company Alpha", "alpha");
        var c2 = new AuthenticationCandidate(Guid.NewGuid(), Guid.NewGuid(), "Company Beta", "beta");
        var cache = new FakeCacheService();
        cache.Store["email_link:validtoken"] = ("USER@EXAMPLE.COM", TimeSpan.FromMinutes(15));
        var store = new FakeAuthenticationStore { CandidatesToReturn = [c1, c2] };
        var authService = CreateAuthService(store);
        var service = new EmailLinkAuthenticationService(store, cache, authService);

        var result = await service.VerifyLinkAsync("validtoken", default);

        Assert.Equal("RequiresWorkspaceSelection", result.Status);
        Assert.NotNull(result.Ticket);
        Assert.NotNull(result.Workspaces);
        Assert.Equal(2, result.Workspaces.Count);
        Assert.Equal("alpha", result.Workspaces[0].TenantKey);
        Assert.Equal("beta", result.Workspaces[1].TenantKey);

        // Verification token consumed
        Assert.False(cache.Store.ContainsKey("email_link:validtoken"));
        // Workspace selection ticket created with 5min TTL
        Assert.True(cache.Store.ContainsKey($"ws_ticket:{result.Ticket}"));
        Assert.Equal(TimeSpan.FromMinutes(5), cache.Store[$"ws_ticket:{result.Ticket}"].Ttl);
    }

    [Fact]
    public async Task SelectWorkspaceAsync_rejects_expired_ticket()
    {
        var store = new FakeAuthenticationStore();
        var cache = new FakeCacheService();
        var authService = CreateAuthService(store);
        var service = new EmailLinkAuthenticationService(store, cache, authService);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            service.SelectWorkspaceAsync("expired-ticket", "alpha", default));

        Assert.Equal("AUTH.TICKET_EXPIRED", fault.Code);
    }

    [Fact]
    public async Task SelectWorkspaceAsync_rejects_workspace_when_user_not_member()
    {
        var cache = new FakeCacheService();
        cache.Store["ws_ticket:validticket"] = ("USER@EXAMPLE.COM", TimeSpan.FromMinutes(5));
        var store = new FakeAuthenticationStore { CandidatesToReturn = [] }; // No candidate for this tenant
        var authService = CreateAuthService(store);
        var service = new EmailLinkAuthenticationService(store, cache, authService);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            service.SelectWorkspaceAsync("validticket", "unauthorized-tenant", default));

        Assert.Equal("AUTH.INVALID_CREDENTIALS", fault.Code);
        // Ticket consumed
        Assert.False(cache.Store.ContainsKey("ws_ticket:validticket"));
    }

    [Fact]
    public async Task SelectWorkspaceAsync_issues_session_for_valid_selection()
    {
        var tenantId = Guid.NewGuid();
        var candidate = new AuthenticationCandidate(Guid.NewGuid(), tenantId, "Company Alpha", "alpha");
        var cache = new FakeCacheService();
        cache.Store["ws_ticket:validticket"] = ("USER@EXAMPLE.COM", TimeSpan.FromMinutes(5));
        var store = new FakeAuthenticationStore
        {
            CandidatesToReturn = [candidate],
            TransactionToReturn = new FakeTransaction(candidate)
        };
        var authService = CreateAuthService(store);
        var service = new EmailLinkAuthenticationService(store, cache, authService);

        var response = await service.SelectWorkspaceAsync("validticket", "alpha", default);

        Assert.NotNull(response);
        Assert.Equal("access-token", response.AccessToken);
        Assert.False(cache.Store.ContainsKey("ws_ticket:validticket"));
    }

    [Fact]
    public async Task EmailLinkAuthenticationController_delegates_to_service()
    {
        var candidate = new AuthenticationCandidate(Guid.NewGuid(), Guid.NewGuid(), "Alpha", "alpha");
        var store = new FakeAuthenticationStore
        {
            CandidatesToReturn = [candidate],
            TransactionToReturn = new FakeTransaction(candidate)
        };
        var cache = new FakeCacheService();
        var authService = CreateAuthService(store);
        var service = new EmailLinkAuthenticationService(store, cache, authService);
        var controller = new EmailLinkAuthenticationController(service);

        // 1. Request
        var reqResult = await controller.RequestLink(new EmailLinkRequestDto("user@example.com"), default);
        Assert.Equal(15, reqResult.ExpiresInMinutes);

        var token = cache.Store.Keys.First().Replace("email_link:", "");

        // 2. Verify
        var verifyResult = await controller.Verify(new EmailLinkVerifyDto(token), default);
        Assert.Equal("Authenticated", verifyResult.Status);
        Assert.NotNull(verifyResult.Authentication);
    }

    private static AuthenticationService CreateAuthService(FakeAuthenticationStore store)
    {
        return new AuthenticationService(
            store,
            new FakeCredentialVerifier(),
            new FakeRefreshTokenGenerator(),
            new FakeAccessTokenIssuer(),
            new FakeAttemptLimiter(),
            new AuthenticationPolicy(),
            TimeProvider.System);
    }

    private sealed class FakeCacheService : IBizFlowCacheService
    {
        public Dictionary<string, (string Value, TimeSpan? Ttl)> Store { get; } = new();

        public Task<T?> GetAsync<T>(string key, CancellationToken cancellationToken = default)
        {
            if (Store.TryGetValue(key, out var entry))
            {
                return Task.FromResult((T?)(object)entry.Value);
            }
            return Task.FromResult<T?>(default);
        }

        public Task SetAsync<T>(string key, T value, TimeSpan? expiration = null, CancellationToken cancellationToken = default)
        {
            Store[key] = (value?.ToString() ?? "", expiration);
            return Task.CompletedTask;
        }

        public Task RemoveAsync(string key, CancellationToken cancellationToken = default)
        {
            Store.Remove(key);
            return Task.CompletedTask;
        }

        public Task RemoveByPrefixAsync(string prefix, CancellationToken cancellationToken = default)
        {
            var keysToRemove = Store.Keys.Where(k => k.StartsWith(prefix)).ToList();
            foreach (var k in keysToRemove) Store.Remove(k);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeAuthenticationStore : IAuthenticationStore
    {
        public IReadOnlyList<AuthenticationCandidate> CandidatesToReturn { get; set; } = [];
        public FakeTransaction? TransactionToReturn { get; set; }

        public Task<IReadOnlyList<AuthenticationCandidate>> FindCandidatesAsync(
            string normalizedIdentifier, string? tenantKey, CancellationToken cancellationToken) =>
            Task.FromResult(CandidatesToReturn);

        public Task<AuthenticationCandidate?> FindSessionOwnerAsync(string tokenHash, CancellationToken cancellationToken) =>
            Task.FromResult<AuthenticationCandidate?>(null);

        public Task<IAuthenticationTransaction?> BeginAsync(AuthenticationCandidate candidate, CancellationToken cancellationToken) =>
            Task.FromResult<IAuthenticationTransaction?>(TransactionToReturn);

        public Task<bool> ValidateAccessAsync(AccessTokenRequest identity, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(true);
    }

    private sealed class FakeTransaction : IAuthenticationTransaction
    {
        public FakeTransaction(AuthenticationCandidate candidate)
        {
            User = UserAccount.CreateTenantUser(candidate.TenantId ?? Guid.NewGuid(), "EMP001", "test@example.com", "Test User", "hash", DateTimeOffset.UtcNow);
            typeof(UserAccount).GetProperty(nameof(UserAccount.Id))?.SetValue(User, candidate.UserId);
            TenantName = candidate.TenantName;
        }

        public UserAccount User { get; }
        public string? TenantName { get; }

        public Task<AccessSnapshot?> ResolveAccessAsync(CancellationToken cancellationToken) =>
            Task.FromResult<AccessSnapshot?>(new AccessSnapshot(
                UserId: User.Id,
                TenantId: User.TenantId,
                IsUserActive: true,
                IsTenantActive: true,
                Grants: [new PermissionGrant("roles.read", PermissionScope.Tenant), new PermissionGrant("tasks.read", PermissionScope.Tenant)],
                ManagedDepartmentIds: new HashSet<Guid>(),
                DepartmentIds: new HashSet<Guid>()));

        public Task<AuthenticationSession?> FindSessionAsync(string hash, CancellationToken cancellationToken) =>
            Task.FromResult<AuthenticationSession?>(null);

        public Task RevokeFamilyAsync(Guid familyId, DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public Task RevokeAllSessionsAsync(DateTimeOffset now, CancellationToken cancellationToken) => Task.CompletedTask;
        public void AddSession(AuthenticationSession session) { }
        public void Audit(AuthenticationEvent action, DateTimeOffset now, Guid? familyId = null) { }
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeCredentialVerifier : ICredentialVerifier
    {
        public PasswordCheck Verify(UserAccount user, string password) => new(true);
        public void VerifyDummy(string password) { }
        public string Hash(UserAccount user, string password) => "hash";
    }

    private sealed class FakeRefreshTokenGenerator : IRefreshTokenGenerator
    {
        public IssuedRefreshToken Generate() => new("token", new string('a', 64));
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
