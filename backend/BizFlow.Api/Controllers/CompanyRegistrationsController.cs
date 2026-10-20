using BizFlow.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

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
}
