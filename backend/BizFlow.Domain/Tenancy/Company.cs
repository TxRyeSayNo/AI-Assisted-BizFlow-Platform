using BizFlow.Domain.Common;

namespace BizFlow.Domain.Tenancy;

public enum CompanyStatus { Pending, Active, Suspended, Inactive }

public sealed class Company
{
    public Guid Id { get; private set; }
    public string Code { get; private set; } = "";
    public string Name { get; private set; } = "";
    public string ContactEmail { get; private set; } = "";
    public CompanyStatus Status { get; private set; }
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private Company() { }

    public static Company Register(string code, string name, string contactEmail, DateTimeOffset now) => new()
    {
        Id = Guid.CreateVersion7(), Code = EntityRules.Text(code, 50, nameof(code)).ToUpperInvariant(),
        Name = EntityRules.Text(name, 200, nameof(name)), ContactEmail = EntityRules.Email(contactEmail),
        Status = CompanyStatus.Pending, CreatedAt = now.ToUniversalTime(), UpdatedAt = now.ToUniversalTime()
    };
}
