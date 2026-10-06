using System.ComponentModel.DataAnnotations;
using BizFlow.Application.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BizFlow.Api.Controllers;

[ApiController, AllowAnonymous, Route("api/v1/auth"), EnableRateLimiting("authentication")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PasswordResetController(PasswordResetService reset) : ControllerBase
{
    [HttpPost("forgot-password")]
    public IActionResult Forgot(ForgotPasswordInput input)
    {
        reset.Request(input.Identifier, input.TenantKey);
        return Accepted(new { message = "If a matching account can receive email, reset instructions will be sent. Include your workspace key if your identifier is shared across companies." });
    }
    [HttpPost("reset-password")]
    public async Task<IActionResult> Reset(ResetPasswordInput input, CancellationToken cancellationToken)
    {
        await reset.ResetAsync(input.Identifier, input.TenantKey, input.ResetToken, input.NewPassword, cancellationToken);
        return Ok(new { message = "Your password has been changed. Sign in with your new password." });
    }
}
public sealed record ForgotPasswordInput([Required, StringLength(320)] string Identifier, [StringLength(80)] string? TenantKey);
public sealed record ResetPasswordInput([Required, StringLength(320)] string Identifier, [StringLength(80)] string? TenantKey,
    [Required, StringLength(4096)] string ResetToken, [Required, StringLength(1024)] string NewPassword)
{
    public override string ToString() => "[REDACTED PASSWORD RESET INPUT]";
}
