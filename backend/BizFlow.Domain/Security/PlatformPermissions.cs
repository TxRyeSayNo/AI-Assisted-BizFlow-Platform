namespace BizFlow.Domain.Security;

// API-TEN-02 / A01 visibility. The code is authority only when paired with PLATFORM scope.
public static class PlatformPermissions
{
    public const string ReadCompanyRegistrations = "platform.company-registrations.read";
    public const string ReadAudit = "platform.audit.read";
}
