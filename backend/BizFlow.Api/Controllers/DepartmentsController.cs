using BizFlow.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/departments")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DepartmentsController(DepartmentQuery departments) : ControllerBase
{
    [HttpGet]
    public Task<DepartmentPage> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null, [FromQuery] string? search = null, CancellationToken cancellationToken = default) =>
        departments.ListAsync(new(page, pageSize, status, search), cancellationToken);
}
