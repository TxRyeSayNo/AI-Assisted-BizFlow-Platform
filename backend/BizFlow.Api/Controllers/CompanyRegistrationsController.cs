using BizFlow.Application.Tenancy;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/platform/company-registrations")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class CompanyRegistrationsController(CompanyRegistrationQuery registrations) : ControllerBase
{
    [HttpGet]
    public Task<CompanyRegistrationPage> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null, [FromQuery] string? search = null, CancellationToken cancellationToken = default) =>
        registrations.ListAsync(new(page, pageSize, status, search), cancellationToken);
}
