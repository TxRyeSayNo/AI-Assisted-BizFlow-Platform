using BizFlow.Application.Audit;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/audit-logs")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AuditController(AuditQuery audit) : ControllerBase
{
    [HttpGet]
    public Task<AuditPage> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25, [FromQuery] Guid? actorId = null,
        [FromQuery] string? actorType = null, [FromQuery] string? action = null, [FromQuery] string? objectType = null,
        [FromQuery] Guid? objectId = null, [FromQuery] string? from = null, [FromQuery] string? until = null, CancellationToken cancellationToken = default) =>
        audit.ListAsync(new(page, pageSize, actorId, actorType, action, objectType, objectId, from, until), cancellationToken);
}
