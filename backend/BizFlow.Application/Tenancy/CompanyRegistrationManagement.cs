using System.Text.RegularExpressions;
using BizFlow.Application.Authentication;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Common;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tenancy;

namespace BizFlow.Application.Tenancy;

public sealed record SubmitCompanyRegistrationRequest(string Code, string Name, string ContactEmail);
public sealed record SubmitCompanyRegistrationResult(Guid CompanyId, string Code, string Name, string ContactEmail, string Status, DateTimeOffset CreatedAt);

public sealed record ApproveCompanyRegistrationRequest(
    string TenantKey,
    string AdminEmployeeCode,
    string AdminEmail,
    string AdminFullName,
    string AdminPassword,
    string TimeZone = "UTC");

public sealed record ApproveCompanyRegistrationResult(
    Guid CompanyId,
    Guid TenantId,
    Guid AdminUserId,
    string TenantKey,
    string Status);

public sealed record RejectCompanyRegistrationRequest(string Reason);
public sealed record RejectCompanyRegistrationResult(Guid CompanyId, string Status);

public sealed record UpdateCompanyStatusRequest(string Status);
public sealed record UpdateCompanyStatusResult(Guid CompanyId, string Status);

public interface ICompanyProvisioningStore
{
    Task<bool> CompanyCodeExistsAsync(string code, CancellationToken cancellationToken);
    Task<bool> CompanyEmailExistsAsync(string email, CancellationToken cancellationToken);
    Task<bool> TenantKeyExistsAsync(string tenantKey, CancellationToken cancellationToken);
    Task<SubmitCompanyRegistrationResult> SubmitRegistrationAsync(Company company, CancellationToken cancellationToken);
    Task<ApproveCompanyRegistrationResult> ApproveAsync(
        Guid companyId,
        string tenantKey,
        string adminEmployeeCode,
        string adminEmail,
        string adminFullName,
        string passwordHash,
        string timeZone,
        Guid platformAdminId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
    Task<RejectCompanyRegistrationResult> RejectAsync(
        Guid companyId,
        string reason,
        Guid platformAdminId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
    Task<UpdateCompanyStatusResult> UpdateStatusAsync(
        Guid companyId,
        CompanyStatus status,
        Guid platformAdminId,
        DateTimeOffset now,
        CancellationToken cancellationToken);
}

public sealed class CompanyRegistrationManagement(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    ICredentialVerifier credentialVerifier,
    ICompanyProvisioningStore store,
    TimeProvider clock)
{
    public async Task<SubmitCompanyRegistrationResult> SubmitAsync(SubmitCompanyRegistrationRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Code) || request.Code.Trim().Length > 50)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Company code is required and must not exceed 50 characters.");
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Trim().Length > 200)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Company name is required and must not exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(request.ContactEmail) || !request.ContactEmail.Contains('@'))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "A valid contact email is required.");

        var normalizedCode = request.Code.Trim().ToUpperInvariant();
        var normalizedEmail = request.ContactEmail.Trim().ToUpperInvariant();

        if (await store.CompanyCodeExistsAsync(normalizedCode, cancellationToken))
            throw new ApplicationFault(FaultKind.Conflict, "TENANT.CODE_IN_USE", "A company registration with this code already exists.");
        if (await store.CompanyEmailExistsAsync(normalizedEmail, cancellationToken))
            throw new ApplicationFault(FaultKind.Conflict, "TENANT.EMAIL_IN_USE", "A company registration with this contact email already exists.");

        var now = clock.GetUtcNow();
        var company = Company.Register(normalizedCode, request.Name.Trim(), request.ContactEmail.Trim(), now);
        return await store.SubmitRegistrationAsync(company, cancellationToken);
    }

    public async Task<ApproveCompanyRegistrationResult> ApproveAsync(Guid companyId, ApproveCompanyRegistrationRequest request, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync(PlatformPermissions.ReadCompanyRegistrations, new ResourceScope(null), cancellationToken: cancellationToken);
        if (context.UserId is not { } platformAdminId || context.TenantId is not null)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Only platform administrators can approve company registrations.");

        if (string.IsNullOrWhiteSpace(request.TenantKey) ||
            !Regex.IsMatch(request.TenantKey, "^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100)) ||
            request.TenantKey.Length > 80)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Tenant key must be a lowercase slug up to 80 characters (e.g. 'acme-corp').");

        if (string.IsNullOrWhiteSpace(request.AdminEmployeeCode) || request.AdminEmployeeCode.Trim().Length > 50)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Admin employee code is required and must not exceed 50 characters.");
        if (string.IsNullOrWhiteSpace(request.AdminEmail) || !request.AdminEmail.Contains('@'))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "A valid admin email is required.");
        if (string.IsNullOrWhiteSpace(request.AdminFullName) || request.AdminFullName.Trim().Length > 200)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Admin full name is required and must not exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(request.AdminPassword) || request.AdminPassword.Length < 8)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Admin password must be at least 8 characters long.");

        var timeZone = string.IsNullOrWhiteSpace(request.TimeZone) ? "UTC" : request.TimeZone.Trim();
        if (timeZone != "UTC" && !TimeZoneInfo.TryConvertIanaIdToWindowsId(timeZone, out _))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "An IANA timezone identifier is required (e.g. 'Asia/Ho_Chi_Minh').");

        var normalizedKey = request.TenantKey.Trim().ToLowerInvariant();
        if (await store.TenantKeyExistsAsync(normalizedKey, cancellationToken))
            throw new ApplicationFault(FaultKind.Conflict, "TENANT.KEY_IN_USE", "This workspace tenant key is already in use.");

        var dummyUser = BizFlow.Domain.Organization.UserAccount.CreatePlatformAdministrator("TEMP", "temp@example.invalid", "Temp", "placeholder", clock.GetUtcNow());
        var passwordHash = credentialVerifier.Hash(dummyUser, request.AdminPassword);

        var now = clock.GetUtcNow();
        return await store.ApproveAsync(
            companyId,
            normalizedKey,
            request.AdminEmployeeCode.Trim().ToUpperInvariant(),
            request.AdminEmail.Trim(),
            request.AdminFullName.Trim(),
            passwordHash,
            timeZone,
            platformAdminId,
            now,
            cancellationToken);
    }

    public async Task<RejectCompanyRegistrationResult> RejectAsync(Guid companyId, RejectCompanyRegistrationRequest request, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync(PlatformPermissions.ReadCompanyRegistrations, new ResourceScope(null), cancellationToken: cancellationToken);
        if (context.UserId is not { } platformAdminId || context.TenantId is not null)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Only platform administrators can reject company registrations.");

        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Trim().Length < 5 || request.Reason.Trim().Length > 1000)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "A rejection reason between 5 and 1000 characters is required (BR-022).");

        var now = clock.GetUtcNow();
        return await store.RejectAsync(companyId, request.Reason.Trim(), platformAdminId, now, cancellationToken);
    }

    public async Task<UpdateCompanyStatusResult> UpdateStatusAsync(Guid companyId, UpdateCompanyStatusRequest request, CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync(PlatformPermissions.ReadCompanyRegistrations, new ResourceScope(null), cancellationToken: cancellationToken);
        if (context.UserId is not { } platformAdminId || context.TenantId is not null)
            throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Only platform administrators can update company status.");

        if (!Enum.TryParse<CompanyStatus>(request.Status, true, out var status))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Valid status values are: ACTIVE, SUSPENDED, INACTIVE.");

        var now = clock.GetUtcNow();
        return await store.UpdateStatusAsync(companyId, status, platformAdminId, now, cancellationToken);
    }
}
