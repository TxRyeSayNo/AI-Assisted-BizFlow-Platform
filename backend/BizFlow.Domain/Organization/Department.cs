using BizFlow.Domain.Common;

namespace BizFlow.Domain.Organization;

public enum RecordStatus { Active, Inactive }

public sealed class Department
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public Guid? ParentDepartmentId { get; private set; }
    public RecordStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private Department() { }

    public static Department Create(Guid tenantId, string code, string name, Guid? parentId, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
        Code = EntityRules.Text(code, 50, nameof(code)).ToUpperInvariant(),
        Name = EntityRules.Text(name, 200, nameof(name)),
        ParentDepartmentId = parentId is { } id ? EntityRules.Id(id, nameof(parentId)) : null,
        Status = RecordStatus.Active, CreatedAt = now.ToUniversalTime(), UpdatedAt = now.ToUniversalTime()
    };
}
