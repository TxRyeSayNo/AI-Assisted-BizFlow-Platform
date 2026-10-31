using BizFlow.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/departments")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class DepartmentsController(
    DepartmentQuery departments,
    DepartmentManagement management) : ControllerBase
{
    [HttpGet]
    public Task<DepartmentPage> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? status = null, [FromQuery] string? search = null, CancellationToken cancellationToken = default) =>
        departments.ListAsync(new(page, pageSize, status, search), cancellationToken);

    [HttpPost]
    public Task<DepartmentOperationResult> Create([FromBody] CreateDepartmentRequest request, CancellationToken cancellationToken) =>
        management.CreateAsync(request, cancellationToken);

    [HttpPut("{id:guid}")]
    public Task<DepartmentOperationResult> Update(Guid id, [FromBody] UpdateDepartmentRequest request, CancellationToken cancellationToken) =>
        management.UpdateAsync(id, request, cancellationToken);
}
