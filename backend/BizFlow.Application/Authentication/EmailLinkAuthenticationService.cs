using System.Security.Cryptography;
using BizFlow.Application.Common;

namespace BizFlow.Application.Authentication;

public sealed class EmailLinkAuthenticationService(
    IAuthenticationStore store,
    IBizFlowCacheService cache,
    AuthenticationService authService) : IEmailLinkAuthenticationService
{
    private static readonly TimeSpan EmailLinkTtl = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan WorkspaceTicketTtl = TimeSpan.FromMinutes(5);

    public async Task<EmailLinkRequestResponse> RequestLinkAsync(string email, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || email.Length > 320 || !email.Contains('@'))
        {
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "A valid email address is required.");
        }

        var normalizedEmail = email.Trim().ToUpperInvariant();
        var candidates = await store.FindCandidatesAsync(normalizedEmail, null, cancellationToken);
        if (candidates.Count == 0)
        {
            throw new ApplicationFault(
                FaultKind.NotFound,
                "AUTH.USER_NOT_FOUND",
                "No active account found for this email address. Please register a company at /register-company.");
        }

        var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)).ToLowerInvariant();
        await cache.SetAsync($"email_link:{token}", normalizedEmail, EmailLinkTtl, cancellationToken);

        return new EmailLinkRequestResponse(
            "A sign-in link has been sent to your email address.",
            (int)EmailLinkTtl.TotalMinutes);
    }

    public async Task<EmailLinkVerificationResponse> VerifyLinkAsync(string token, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(token) || token.Length > 128)
        {
            throw new ApplicationFault(FaultKind.Validation, "AUTH.INVALID_TOKEN", "The sign-in token is invalid.");
        }

        var cacheKey = $"email_link:{token.Trim().ToLowerInvariant()}";
        var normalizedEmail = await cache.GetAsync<string>(cacheKey, cancellationToken);
        if (string.IsNullOrEmpty(normalizedEmail))
        {
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.TOKEN_EXPIRED", "The sign-in link is invalid or has expired.");
        }

        // Single-use token: remove immediately upon successful verification
        await cache.RemoveAsync(cacheKey, cancellationToken);

        var candidates = await store.FindCandidatesAsync(normalizedEmail, null, cancellationToken);
        if (candidates.Count == 0)
        {
            return new EmailLinkVerificationResponse(
                Status: "NoWorkspace",
                Message: "No active workspace found for this email account.",
                RegisterCompanyUrl: "/register-company",
                Ticket: null,
                Workspaces: null,
                Authentication: null);
        }

        if (candidates.Count == 1)
        {
            var auth = await authService.CreateSessionForCandidateAsync(candidates[0], cancellationToken);
            return new EmailLinkVerificationResponse(
                Status: "Authenticated",
                Message: null,
                RegisterCompanyUrl: null,
                Ticket: null,
                Workspaces: null,
                Authentication: auth);
        }

        // Multiple workspaces: generate short-lived selection ticket (5m TTL)
        var ticket = Guid.NewGuid().ToString("N");
        await cache.SetAsync($"ws_ticket:{ticket}", normalizedEmail, WorkspaceTicketTtl, cancellationToken);

        var workspaces = candidates
            .Select(c => new WorkspaceCandidateDto(c.TenantId, c.TenantKey ?? "", c.TenantName ?? "Workspace"))
            .ToList();

        return new EmailLinkVerificationResponse(
            Status: "RequiresWorkspaceSelection",
            Message: "Multiple workspaces found. Please select a workspace to enter.",
            RegisterCompanyUrl: null,
            Ticket: ticket,
            Workspaces: workspaces,
            Authentication: null);
    }

    public async Task<AuthenticationResponse> SelectWorkspaceAsync(string ticket, string tenantKey, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(ticket) || ticket.Length > 128 ||
            string.IsNullOrWhiteSpace(tenantKey) || tenantKey.Length > 80)
        {
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Ticket and workspace key are required.");
        }

        var ticketKey = $"ws_ticket:{ticket.Trim()}";
        var normalizedEmail = await cache.GetAsync<string>(ticketKey, cancellationToken);
        if (string.IsNullOrEmpty(normalizedEmail))
        {
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.TICKET_EXPIRED", "The workspace selection session has expired. Please sign in again.");
        }

        // Single-use ticket
        await cache.RemoveAsync(ticketKey, cancellationToken);

        var candidates = await store.FindCandidatesAsync(normalizedEmail, tenantKey.Trim().ToLowerInvariant(), cancellationToken);
        if (candidates.Count != 1)
        {
            throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.INVALID_CREDENTIALS", "You do not have access to the selected workspace.");
        }

        return await authService.CreateSessionForCandidateAsync(candidates[0], cancellationToken);
    }
}
