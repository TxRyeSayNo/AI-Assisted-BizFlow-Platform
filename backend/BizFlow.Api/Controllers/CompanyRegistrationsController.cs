using BizFlow.Application.Collaboration;
using BizFlow.Application.Security;
using BizFlow.Application.Tenancy;
using BizFlow.Domain.Security;
using BizFlow.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/platform/company-registrations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CompanyRegistrationsController(
    CompanyRegistrationQuery registrations,
    CompanyRegistrationManagement management) : ControllerBase
{
    [HttpGet]
    public Task<CompanyRegistrationPage> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null, [FromQuery] string? search = null, CancellationToken cancellationToken = default) =>
        registrations.ListAsync(new(page, pageSize, status, search), cancellationToken);

    [HttpPost("{id:guid}/approve")]
    public Task<ApproveCompanyRegistrationResult> Approve(Guid id, [FromBody] ApproveCompanyRegistrationRequest request, CancellationToken cancellationToken) =>
        management.ApproveAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/reject")]
    public Task<RejectCompanyRegistrationResult> Reject(Guid id, [FromBody] RejectCompanyRegistrationRequest request, CancellationToken cancellationToken) =>
        management.RejectAsync(id, request, cancellationToken);

    [HttpPatch("{id:guid}/status")]
    public Task<UpdateCompanyStatusResult> UpdateStatus(Guid id, [FromBody] UpdateCompanyStatusRequest request, CancellationToken cancellationToken) =>
        management.UpdateStatusAsync(id, request, cancellationToken);

    [HttpGet("{id:guid}/document-url")]
    public async Task<ActionResult<object>> GetDocumentUrl(
        Guid id,
        [FromServices] BizFlowDbContext db,
        [FromServices] IObjectStorageProvider storage,
        [FromServices] IResourceAuthorizer authorizer,
        CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync(PlatformPermissions.ReadCompanyRegistrations, new ResourceScope(null), cancellationToken: cancellationToken);
        var company = await db.Companies.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (company is null || company.EvidenceDocumentKey is null)
            return NotFound();

        var presignedUrl = storage.GeneratePresignedDownloadUrl(company.EvidenceDocumentKey, TimeSpan.FromMinutes(15));
        var downloadUrl = presignedUrl ?? $"/api/v1/platform/company-registrations/{id}/document";

        return Ok(new
        {
            companyId = id,
            fileName = company.EvidenceDocumentFileName ?? "legal-document",
            downloadUrl,
            expiresInSeconds = 900
        });
    }

    [HttpGet("{id:guid}/document")]
    public async Task<IActionResult> DownloadDocument(
        Guid id,
        [FromServices] BizFlowDbContext db,
        [FromServices] IObjectStorageProvider storage,
        [FromServices] IResourceAuthorizer authorizer,
        CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync(PlatformPermissions.ReadCompanyRegistrations, new ResourceScope(null), cancellationToken: cancellationToken);
        var company = await db.Companies.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
        if (company is null || company.EvidenceDocumentKey is null)
            return NotFound();

        var stream = await storage.OpenReadAsync(company.EvidenceDocumentKey, cancellationToken);
        if (stream is null)
            return NotFound();

        var ext = Path.GetExtension(company.EvidenceDocumentFileName ?? "").ToLowerInvariant();
        var contentType = ext switch
        {
            ".pdf" => "application/pdf",
            ".png" => "image/png",
            ".jpg" or ".jpeg" => "image/jpeg",
            _ => "application/octet-stream"
        };

        return File(stream, contentType, company.EvidenceDocumentFileName ?? "legal-document.pdf");
    }
}
