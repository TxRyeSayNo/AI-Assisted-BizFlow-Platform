using BizFlow.Domain.Common;

namespace BizFlow.Domain.Sla;

public enum SlaProfileStatus { Draft, Active, Inactive }

public sealed class SlaProfile
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Name { get; private set; } = "";
    public SlaProfileStatus Status { get; private set; }
    private SlaProfile() { }
    public static SlaProfile CreateDraft(Guid tenantId, string name) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
        Name = EntityRules.Text(name, 200, nameof(name)), Status = SlaProfileStatus.Draft
    };
}
