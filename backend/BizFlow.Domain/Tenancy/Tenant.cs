using System.Text.RegularExpressions;
using BizFlow.Domain.Common;

namespace BizFlow.Domain.Tenancy;

public enum TenantStatus { Provisioning, Active, Suspended, Inactive }

public sealed class Tenant
{
    public Guid Id { get; private set; }
    public Guid CompanyId { get; private set; }
    public string TenantKey { get; private set; } = "";
    public string Name { get; private set; } = "";
    public TenantStatus Status { get; private set; }
    public string TimeZone { get; private set; } = "UTC";
    public DateTimeOffset CreatedAt { get; private set; }
    public DateTimeOffset UpdatedAt { get; private set; }
    private Tenant() { }

    public static Tenant Create(Guid companyId, string key, string name, string timeZone, DateTimeOffset now)
    {
        if (!Regex.IsMatch(key, "^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant,
                TimeSpan.FromMilliseconds(100)) || key.Length > 80)
            throw new ArgumentException("A lowercase workspace slug is required.", nameof(key));
        if (timeZone != "UTC" && !TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZone, out _))
            throw new ArgumentException("An IANA timezone is required.", nameof(timeZone));
        return new Tenant
        {
            Id = Guid.CreateVersion7(), CompanyId = EntityRules.Id(companyId, nameof(companyId)),
            TenantKey = key, Name = EntityRules.Text(name, 200, nameof(name)), TimeZone = timeZone,
            Status = TenantStatus.Active, CreatedAt = now.ToUniversalTime(), UpdatedAt = now.ToUniversalTime()
        };
    }
}
