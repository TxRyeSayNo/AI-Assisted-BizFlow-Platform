using BizFlow.Domain.Common;
using BizFlow.Domain.Organization;

namespace BizFlow.Domain.Security;

public sealed class Role
{
    public Guid Id { get; private set; }
    public Guid? TenantId { get; private set; }
    public string Name { get; private set; } = "";
    public bool IsSystem { get; private set; }
    public RecordStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private Role() { }

    public void ConfigurePermissions(IReadOnlyCollection<PermissionScope> scopes, DateTimeOffset now)
    {
        if (IsSystem || TenantId is null) throw new InvalidOperationException("System roles are protected reference data.");
        if (scopes.Any(scope => scope == PermissionScope.Platform || !Enum.IsDefined(scope)))
            throw new InvalidOperationException("Custom roles cannot grant platform authority.");
        UpdatedAt = now.ToUniversalTime();
    }

    public static Role CreateCustom(Guid tenantId, string name, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
        Name = EntityRules.Text(name, 100, nameof(name)), IsSystem = false, Status = RecordStatus.Active,
        CreatedAt = now.ToUniversalTime(), UpdatedAt = now.ToUniversalTime()
    };
}
