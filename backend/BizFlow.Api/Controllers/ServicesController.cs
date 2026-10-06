using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;
using BizFlow.Application.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/services")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class ServicesController(ServiceCatalog catalog, ServiceCategoryCreation categories, ServiceMetadataUpdate metadata) : ControllerBase
{
    [HttpGet]
    public Task<ServicePage> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null, [FromQuery] string? status = null, CancellationToken cancellationToken = default) =>
        catalog.ListAsync(new(page, pageSize, search, status), cancellationToken);
    [HttpPost]
    public async Task<IActionResult> Create(CreateServiceInput input, CancellationToken cancellationToken)
    {
        var result = await catalog.CreateAsync(new(input.Code, input.Name, input.Description, input.Active,
            input.Categories?.Select(c => c is null ? null! : new CreateCategory(c.Code, c.Name, c.Active)).ToArray()), cancellationToken);
        Response.Headers.ETag = result.ETag;
        return StatusCode(StatusCodes.Status201Created, result);
    }
    [HttpPost("{id:guid}/categories")]
    public async Task<IActionResult> CreateCategory(Guid id, CreateServiceCategoryInput input, CancellationToken cancellationToken)
    {
        var result = await categories.CreateAsync(id, Request.Headers.IfMatch.ToString(), new(input.Code, input.Name, input.Active), cancellationToken);
        Response.Headers.ETag = result.ETag;
        return StatusCode(StatusCodes.Status201Created, result);
    }
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, UpdateServiceInput input, CancellationToken cancellationToken)
    {
        var result = await metadata.UpdateAsync(id, Request.Headers.IfMatch.ToString(),
            new(input.Code, input.Name, input.Description, input.Active!.Value), cancellationToken);
        Response.Headers.ETag = result.ETag;
        return Ok(result);
    }
}

[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateServiceInput([Required, StringLength(80)] string Code, [Required, StringLength(200)] string Name,
    string? Description = null, bool Active = true, IReadOnlyList<CreateServiceCategoryInput>? Categories = null);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record CreateServiceCategoryInput([Required, StringLength(80)] string Code, [Required, StringLength(200)] string Name, bool Active = true);
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateServiceInput([Required, StringLength(80)] string Code, [Required, StringLength(200)] string Name,
    string? Description = null, [Required] bool? Active = null);
