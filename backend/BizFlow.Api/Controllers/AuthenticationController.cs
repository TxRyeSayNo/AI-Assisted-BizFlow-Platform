using System.ComponentModel.DataAnnotations;
using BizFlow.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BizFlow.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
[EnableRateLimiting("authentication")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuthenticationController(AuthenticationService authentication) : ControllerBase
{
    [HttpPost("login")]
    [AllowAnonymous]
    public Task<AuthenticationResponse> Login(LoginInput input, CancellationToken cancellationToken) =>
        authentication.LoginAsync(input.Identifier, input.Password, input.TenantKey, cancellationToken);

    // The refresh credential authenticates this operation; an expired access token is not required.
    [HttpPost("refresh")]
    [AllowAnonymous]
    public Task<AuthenticationResponse> Refresh(RefreshInput input, CancellationToken cancellationToken) =>
        authentication.RefreshAsync(input.RefreshToken, cancellationToken);
}

public sealed record LoginInput(
    [Required, StringLength(320)] string Identifier,
    [Required, StringLength(1024)] string Password,
    [StringLength(80)] string? TenantKey)
{
    public override string ToString() => "[REDACTED LOGIN INPUT]";
}
public sealed record RefreshInput([Required, StringLength(128, MinimumLength = 128)] string RefreshToken)
{
    public override string ToString() => "[REDACTED REFRESH INPUT]";
}
