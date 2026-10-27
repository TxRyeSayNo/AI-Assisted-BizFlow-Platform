using System.Text.RegularExpressions;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Common;
using BizFlow.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace BizFlow.Api.Controllers;

public sealed record UploadCompanyDocumentResult(string DocumentKey, string FileName, long SizeBytes);

[ApiController]
[AllowAnonymous]
[Route("api/v1/company-registrations")]
[EnableRateLimiting("authentication")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicCompanyRegistrationsController(
    CompanyRegistrationManagement management,
    ICompanyProvisioningStore store,
    IObjectStorageProvider storage) : ControllerBase
{
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".png", ".jpg", ".jpeg"
    };

    private static readonly Regex SlugRegex = new(@"^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.Compiled);

    [HttpPost]
    public Task<SubmitCompanyRegistrationResult> Submit([FromBody] SubmitCompanyRegistrationRequest request, CancellationToken cancellationToken) =>
        management.SubmitAsync(request, cancellationToken);

    [HttpGet("check-code")]
    public async Task<ActionResult<CheckCodeResponse>> CheckCode([FromQuery] string? code, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(code))
            return Ok(new CheckCodeResponse(false, "", "Workspace code is required."));

        var normalized = code.Trim().ToLowerInvariant();
        if (normalized.Length < 3 || normalized.Length > 50 || !SlugRegex.IsMatch(normalized))
            return Ok(new CheckCodeResponse(false, normalized, "Code must be 3-50 lowercase alphanumeric characters and hyphens."));

        var inTenants = await store.TenantKeyExistsAsync(normalized, cancellationToken);
        var inCompanies = await store.CompanyCodeExistsAsync(normalized.ToUpperInvariant(), cancellationToken);

        if (inTenants || inCompanies)
            return Ok(new CheckCodeResponse(false, normalized, "Workspace code is already in use."));

        return Ok(new CheckCodeResponse(true, normalized));
    }

    [HttpPost("upload-document")]
    [RequestSizeLimit(10_485_760)] // 10 MB maximum
    public async Task<ActionResult<UploadCompanyDocumentResult>> UploadDocument(
        IFormFile? file,
        CancellationToken cancellationToken)
    {
        if (file is null || file.Length == 0)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "A non-empty document file is required.");

        if (file.Length > 10_485_760)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Document file size exceeds the 10 MB limit.");

        var ext = Path.GetExtension(file.FileName);
        if (string.IsNullOrEmpty(ext) || !AllowedExtensions.Contains(ext))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Only PDF, PNG, JPG, and JPEG documents are permitted.");

        var safeFileName = Path.GetFileName(file.FileName);
        var objectKey = $"company-documents/{Guid.CreateVersion7():D}-{safeFileName}";

        await using var stream = file.OpenReadStream();
        await storage.SaveBinaryDirectAsync(objectKey, stream, cancellationToken);

        return Ok(new UploadCompanyDocumentResult(objectKey, safeFileName, file.Length));
    }
}
