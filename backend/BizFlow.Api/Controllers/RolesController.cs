using System.ComponentModel.DataAnnotations;
using BizFlow.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/roles")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RolesController(RoleManagement roles) : ControllerBase
{
    [HttpGet]
    public Task<RolePage> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null, CancellationToken cancellationToken = default) =>
        roles.ListAsync(new(page, pageSize, search), cancellationToken);

    [HttpPost]
    public async Task<IActionResult> Create(CreateRoleInput input, CancellationToken cancellationToken)
    {
        var result = await roles.CreateAsync(input.Name, cancellationToken);
        Response.Headers.ETag = result.ETag;
        return StatusCode(StatusCodes.Status201Created, result);
    }

    [HttpPut("{id:guid}/permissions")]
    public async Task<RoleRow> SetPermissions(Guid id, SetRolePermissionsInput input, CancellationToken cancellationToken)
    {
        var result = await roles.SetPermissionsAsync(id, Request.Headers.IfMatch.ToString(), input.PermissionIds, cancellationToken);
        Response.Headers.ETag = result.ETag;
        return result;
    }
}
public sealed record CreateRoleInput([Required, StringLength(100)] string Name);
public sealed record SetRolePermissionsInput([Required, MaxLength(500)] Guid[] PermissionIds);
