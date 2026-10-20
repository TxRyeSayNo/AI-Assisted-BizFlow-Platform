using BizFlow.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, AllowAnonymous, Route("api/v1/company-registrations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class PublicCompanyRegistrationsController(CompanyRegistrationManagement management) : ControllerBase
{
    [HttpPost]
    public Task<SubmitCompanyRegistrationResult> Submit([FromBody] SubmitCompanyRegistrationRequest request, CancellationToken cancellationToken) =>
        management.SubmitAsync(request, cancellationToken);
}
