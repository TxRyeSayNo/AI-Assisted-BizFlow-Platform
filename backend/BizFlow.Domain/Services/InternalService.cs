using BizFlow.Domain.Common;

namespace BizFlow.Domain.Services;

public enum ServiceStatus { Draft, Active, Inactive }
public enum ServiceCategoryStatus { Active, Inactive }
public sealed record ServiceMetadata(string Code, string Name, string? Description, ServiceStatus Status);

public sealed class InternalService
{
    public Guid Id { get; private set; }
    public Guid TenantId { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string? Description { get; private set; }
    public ServiceStatus Status { get; private set; }
    public Guid? ActiveWorkflowVersionId { get; private set; }
    public Guid? ActiveSlaVersionId { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private InternalService() { }

    public ServiceMetadata Metadata() => new(Code, Name, Description, Status);

    public void UpdateMetadata(string code, string name, string? description, bool active, DateTimeOffset now)
    {
        // Validate all fields before mutating any of the aggregate.
        var normalizedCode = ServiceText.Required(code, 80, nameof(code));
        var normalizedName = ServiceText.Required(name, 200, nameof(name));
        var text = string.IsNullOrWhiteSpace(description) ? null : ServiceText.Plain(description.Trim(), nameof(description), multiline: true);
        Code = normalizedCode; Name = normalizedName; Description = text;
        Status = active ? ServiceStatus.Active : ServiceStatus.Inactive; UpdatedAt = now.ToUniversalTime();
    }

    public void RecordCategoryAdded(ServiceCategory category, DateTimeOffset now)
    {
        ArgumentNullException.ThrowIfNull(category);
        if (category.ServiceId != Id) throw new ArgumentException("Category must belong to this service.", nameof(category));
        UpdatedAt = now.ToUniversalTime();
    }

    public static InternalService Create(Guid tenantId, string code, string name, string? description, bool active, DateTimeOffset now)
    {
        var text = string.IsNullOrWhiteSpace(description) ? null : ServiceText.Plain(description.Trim(), nameof(description), multiline: true);
        return new() { Id = Guid.CreateVersion7(), TenantId = EntityRules.Id(tenantId, nameof(tenantId)),
            Code = ServiceText.Required(code, 80, nameof(code)), Name = ServiceText.Required(name, 200, nameof(name)),
            Description = text, Status = active ? ServiceStatus.Active : ServiceStatus.Inactive,
            CreatedAt = now.ToUniversalTime(), UpdatedAt = now.ToUniversalTime() };
    }
}

public sealed class ServiceCategory
{
    public Guid Id { get; private set; }
    public Guid ServiceId { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public ServiceCategoryStatus Status { get; private set; }
    private ServiceCategory() { }

    public static ServiceCategory Create(Guid serviceId, string code, string name, bool active = true) => new()
    {
        Id = Guid.CreateVersion7(), ServiceId = EntityRules.Id(serviceId, nameof(serviceId)),
        Code = ServiceText.Required(code, 80, nameof(code)), Name = ServiceText.Required(name, 200, nameof(name)),
        Status = active ? ServiceCategoryStatus.Active : ServiceCategoryStatus.Inactive
    };
}

internal static class ServiceText
{
    internal static string Required(string value, int maximum, string field) => Plain(EntityRules.Text(value, maximum, field), field);
    // Plain text, never HTML. Consumers must use contextual escaping, not innerHTML.
    internal static string Plain(string value, string field, bool multiline = false)
    {
        if (value.Any(c => char.IsControl(c) && !(multiline && c is '\r' or '\n' or '\t')))
            throw new ArgumentException("Unsupported control characters in text.", field);
        return value;
    }
}
