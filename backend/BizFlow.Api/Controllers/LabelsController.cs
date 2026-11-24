using BizFlow.Api.Configuration;
using BizFlow.Application.Collaboration;
using BizFlow.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using System.Text.Json.Serialization;

namespace BizFlow.Api.Controllers;

/// <summary>
/// The per-tenant label catalogue (F6). Managing it is a tenant administration capability and
/// reuses the existing <c>roles.configure</c> permission; reading it needs only tenant membership,
/// because labels are shown on every item a person can already open.
/// </summary>
[ApiController, Authorize, Route("api/v1/labels")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class LabelsController(LabelService labels, IOptions<FeatureFlagOptions> features) : ControllerBase
{
    [HttpGet]
    public Task<IReadOnlyList<LabelView>> List([FromQuery] bool includeArchived = false,
        CancellationToken cancellationToken = default)
    {
        Require();
        return labels.ListAsync(includeArchived, cancellationToken);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record LabelInput(string Name, string Colour);

    [HttpPost]
    public async Task<ActionResult<LabelView>> Create(LabelInput input, CancellationToken cancellationToken)
    {
        Require();
        var created = await labels.CreateAsync(input.Name, input.Colour, cancellationToken);
        return Created($"/api/v1/labels/{created.LabelId}", created);
    }

    [HttpPut("{labelId:guid}")]
    public Task<LabelView> Update(Guid labelId, LabelInput input, CancellationToken cancellationToken)
    {
        Require();
        return labels.UpdateAsync(labelId, input.Name, input.Colour, cancellationToken);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record ArchiveInput(bool Archived);

    /// <summary>Retires or restores a label. Items that carry it keep their history either way.</summary>
    [HttpPut("{labelId:guid}/archived")]
    public Task<LabelView> Archive(Guid labelId, ArchiveInput input, CancellationToken cancellationToken)
    {
        Require();
        return labels.ArchiveAsync(labelId, input.Archived, cancellationToken);
    }

    private void Require()
    {
        if (!features.Value.Labels) throw ApplicationFault.NotFound();
    }
}

/// <summary>
/// The labels carried by one task or request (F6). Gated by that item's own read policy: a person
/// who cannot open the item cannot see or change its labels.
/// </summary>
[ApiController, Authorize, Route("api/v1/work-items/{objectType}/{objectId:guid}/labels")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class WorkItemLabelsController(LabelService labels, IOptions<FeatureFlagOptions> features) : ControllerBase
{
    [HttpGet]
    public Task<WorkItemLabelsView> Read(string objectType, Guid objectId, CancellationToken cancellationToken)
    {
        Require();
        return labels.ReadAsync(objectType, objectId, cancellationToken);
    }

    [JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
    public sealed record AttachInput;

    [HttpPut("{labelId:guid}")]
    public Task<WorkItemLabelsView> Attach(string objectType, Guid objectId, Guid labelId, AttachInput input,
        CancellationToken cancellationToken)
    {
        Require();
        return labels.AttachAsync(objectType, objectId, labelId, cancellationToken);
    }

    [HttpDelete("{labelId:guid}")]
    public Task<WorkItemLabelsView> Detach(string objectType, Guid objectId, Guid labelId,
        CancellationToken cancellationToken)
    {
        Require();
        return labels.DetachAsync(objectType, objectId, labelId, cancellationToken);
    }

    private void Require()
    {
        if (!features.Value.Labels) throw ApplicationFault.NotFound();
    }
}
