using BizFlow.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/users")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UsersController(UserDirectoryQuery directory) : ControllerBase
{
    [HttpGet]
    public Task<UserDirectoryPage> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] Guid? departmentId = null,
        CancellationToken cancellationToken = default) =>
        directory.ListAsync(new(page, pageSize, search, status, departmentId), cancellationToken);
}
