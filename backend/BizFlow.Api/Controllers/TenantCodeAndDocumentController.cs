using System.Text.RegularExpressions;
using BizFlow.Application.Common;
using BizFlow.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BizFlow.Api.Controllers;

public sealed record CheckCodeResponse(bool Available, string Code, string? Message = null);
public sealed record GenerateCodeRequest(string? BaseName = null);
public sealed record GenerateCodeResponse(string Code);

[ApiController]
[AllowAnonymous]
[Route("api/v1/tenants")]
[EnableRateLimiting("authentication")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class TenantCodeAndDocumentController(ICompanyProvisioningStore store) : ControllerBase
{
    private static readonly Regex SlugRegex = new(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    [HttpGet("check-code")]
    public async Task<ActionResult<CheckCodeResponse>> CheckCode([FromQuery] string? code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return Ok(new CheckCodeResponse(false, "", "Workspace code is required."));
        }

        var trimmed = code.Trim();
        if (trimmed.Length < 3 || trimmed.Length > 50 || !SlugRegex.IsMatch(trimmed))
        {
            return Ok(new CheckCodeResponse(false, trimmed, "Code must be 3-50 lowercase alphanumeric characters and hyphens."));
        }

        var normalized = trimmed.ToLowerInvariant();

        var inTenants = await store.TenantKeyExistsAsync(normalized, cancellationToken);
        var inCompanies = await store.CompanyCodeExistsAsync(normalized.ToUpperInvariant(), cancellationToken);

        if (inTenants || inCompanies)
        {
            return Ok(new CheckCodeResponse(false, normalized, "Workspace code is already in use."));
        }

        return Ok(new CheckCodeResponse(true, normalized));
    }

    [HttpPost("generate-code")]
    public async Task<ActionResult<GenerateCodeResponse>> GenerateCode([FromBody] GenerateCodeRequest? request, CancellationToken cancellationToken)
    {
        var rawBase = request?.BaseName?.Trim().ToLowerInvariant();
        string prefix;
        if (!string.IsNullOrWhiteSpace(rawBase))
        {
            prefix = Regex.Replace(rawBase, @"[^a-z0-9]+", "-").Trim('-');
            if (prefix.Length > 20) prefix = prefix[..20].Trim('-');
            if (prefix.Length < 2) prefix = "bizflow";
        }
        else
        {
            prefix = "bizflow";
        }

        for (var attempt = 0; attempt < 10; attempt++)
        {
            var suffix = Random.Shared.Next(1000, 9999);
            var candidate = $"{prefix}-{suffix}";
            var inTenants = await store.TenantKeyExistsAsync(candidate, cancellationToken);
            var inCompanies = await store.CompanyCodeExistsAsync(candidate.ToUpperInvariant(), cancellationToken);
            if (!inTenants && !inCompanies)
            {
                return Ok(new GenerateCodeResponse(candidate));
            }
        }

        var fallback = $"bf-{Guid.NewGuid().ToString("N")[..8]}";
        return Ok(new GenerateCodeResponse(fallback));
    }
}
