using System.ComponentModel.DataAnnotations;
using BizFlow.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BizFlow.Api.Controllers;

[ApiController]
[Route("api/v1/auth/email-link")]
[EnableRateLimiting("authentication")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class EmailLinkAuthenticationController(IEmailLinkAuthenticationService emailLinkAuth) : ControllerBase
{
    [HttpPost("request")]
    [AllowAnonymous]
    public Task<EmailLinkRequestResponse> RequestLink([FromBody] EmailLinkRequestDto input, CancellationToken cancellationToken) =>
        emailLinkAuth.RequestLinkAsync(input.Email, cancellationToken);

    [HttpPost("verify")]
    [AllowAnonymous]
    public Task<EmailLinkVerificationResponse> Verify([FromBody] EmailLinkVerifyDto input, CancellationToken cancellationToken) =>
        emailLinkAuth.VerifyLinkAsync(input.Token, cancellationToken);

    [HttpPost("select-workspace")]
    [AllowAnonymous]
    public Task<AuthenticationResponse> SelectWorkspace([FromBody] EmailLinkSelectWorkspaceDto input, CancellationToken cancellationToken) =>
        emailLinkAuth.SelectWorkspaceAsync(input.Ticket, input.TenantKey, cancellationToken);
}

public sealed record EmailLinkRequestDto([Required, EmailAddress, StringLength(320)] string Email);
public sealed record EmailLinkVerifyDto([Required, StringLength(128)] string Token);
public sealed record EmailLinkSelectWorkspaceDto(
    [Required, StringLength(128)] string Ticket,
    [Required, StringLength(80)] string TenantKey);
