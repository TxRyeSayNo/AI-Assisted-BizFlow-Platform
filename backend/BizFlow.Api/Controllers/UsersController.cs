using BizFlow.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/users")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UsersController(
    UserDirectoryQuery directory,
    UserManagement management) : ControllerBase
{
    [HttpGet]
    public Task<UserDirectoryPage> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] Guid? departmentId = null,
        CancellationToken cancellationToken = default) =>
        directory.ListAsync(new(page, pageSize, search, status, departmentId), cancellationToken);

    [HttpPost]
    public Task<UserOperationResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken) =>
        management.CreateAsync(request, cancellationToken);

    [HttpPut("{id:guid}")]
    public Task<UserOperationResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken) =>
        management.UpdateAsync(id, request, cancellationToken);

    [HttpPatch("{id:guid}/status")]
    public Task<UserOperationResult> UpdateStatus(Guid id, [FromBody] UpdateUserStatusRequest request, CancellationToken cancellationToken) =>
        management.UpdateStatusAsync(id, request, cancellationToken);

    [HttpGet("{id:guid}/responsibilities")]
    public Task<UserResponsibilitiesView> GetResponsibilities(Guid id, CancellationToken cancellationToken) =>
        management.GetResponsibilitiesAsync(id, cancellationToken);

    [HttpPost("{id:guid}/promote-manager")]
    public Task<UserOperationResult> PromoteToManager(Guid id, CancellationToken cancellationToken) =>
        management.PromoteToManagerAsync(id, cancellationToken);

    [HttpPost("{id:guid}/demote-employee")]
    public Task<UserOperationResult> DemoteToEmployee(Guid id, [FromBody] DemoteUserRequest request, CancellationToken cancellationToken) =>
        management.DemoteToEmployeeAsync(id, request, cancellationToken);
}
