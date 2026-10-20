using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Tenancy;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tenancy;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Tenancy;

public sealed class CompanyProvisioningStore(BizFlowDbContext db) : ICompanyProvisioningStore
{
    public Task<bool> CompanyCodeExistsAsync(string code, CancellationToken cancellationToken) =>
        db.Companies.IgnoreQueryFilters().AnyAsync(c => c.Code == code, cancellationToken);

    public Task<bool> CompanyEmailExistsAsync(string email, CancellationToken cancellationToken) =>
        db.Companies.IgnoreQueryFilters().AnyAsync(c => c.ContactEmail.ToUpper() == email, cancellationToken);

    public Task<bool> TenantKeyExistsAsync(string tenantKey, CancellationToken cancellationToken) =>
        db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.TenantKey == tenantKey, cancellationToken);

    public async Task<SubmitCompanyRegistrationResult> SubmitRegistrationAsync(Company company, CancellationToken cancellationToken)
    {
        db.Companies.Add(company);
        await db.SaveChangesAsync(cancellationToken);
        return new(company.Id, company.Code, company.Name, company.ContactEmail, company.Status.ToString().ToUpperInvariant(), company.CreatedAt);
    }

    public async Task<ApproveCompanyRegistrationResult> ApproveAsync(
        Guid companyId,
        string tenantKey,
        string adminEmployeeCode,
        string adminEmail,
        string adminFullName,
        string passwordHash,
        string timeZone,
        Guid platformAdminId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var company = await db.Companies.IgnoreQueryFilters().SingleOrDefaultAsync(c => c.Id == companyId, cancellationToken);
        if (company is null) throw ApplicationFault.NotFound();
        if (company.Status != CompanyStatus.Pending)
            throw new ApplicationFault(FaultKind.Conflict, "TENANT.INVALID_STATUS", "Only Pending company registrations can be approved.");

        company.Approve(now);

        var tenant = Tenant.Create(company.Id, tenantKey, company.Name, timeZone, now);
        db.Tenants.Add(tenant);

        var adminUser = UserAccount.CreateTenantUser(tenant.Id, adminEmployeeCode, adminEmail, adminFullName, passwordHash, now, null);
        db.Users.Add(adminUser);

        var companyAdminRole = await db.Roles.IgnoreQueryFilters().SingleOrDefaultAsync(r => r.Name == "COMPANY_ADMIN" && r.IsSystem, cancellationToken)
            ?? throw new InvalidOperationException("System role COMPANY_ADMIN not found.");

        db.UserRoles.Add(new UserRole(adminUser.Id, companyAdminRole.Id));

        db.AuditLogs.Add(AuditLog.CompanyApproved(company.Id, platformAdminId, tenant.Id, tenant.TenantKey, now));

        await db.SaveChangesAsync(cancellationToken);

        return new(company.Id, tenant.Id, adminUser.Id, tenant.TenantKey, company.Status.ToString().ToUpperInvariant());
    }

    public async Task<RejectCompanyRegistrationResult> RejectAsync(
        Guid companyId,
        string reason,
        Guid platformAdminId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var company = await db.Companies.IgnoreQueryFilters().SingleOrDefaultAsync(c => c.Id == companyId, cancellationToken);
        if (company is null) throw ApplicationFault.NotFound();
        if (company.Status != CompanyStatus.Pending)
            throw new ApplicationFault(FaultKind.Conflict, "TENANT.INVALID_STATUS", "Only Pending company registrations can be rejected.");

        company.Reject(now);

        db.AuditLogs.Add(AuditLog.CompanyRejected(company.Id, platformAdminId, reason, now));

        await db.SaveChangesAsync(cancellationToken);

        return new(company.Id, company.Status.ToString().ToUpperInvariant());
    }

    public async Task<UpdateCompanyStatusResult> UpdateStatusAsync(
        Guid companyId,
        CompanyStatus status,
        Guid platformAdminId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var company = await db.Companies.IgnoreQueryFilters().SingleOrDefaultAsync(c => c.Id == companyId, cancellationToken);
        if (company is null) throw ApplicationFault.NotFound();

        var previousStatus = company.Status.ToString().ToUpperInvariant();
        company.UpdateStatus(status, now);

        db.AuditLogs.Add(AuditLog.CompanyStatusUpdated(company.Id, platformAdminId, previousStatus, status.ToString().ToUpperInvariant(), now));

        await db.SaveChangesAsync(cancellationToken);

        return new(company.Id, company.Status.ToString().ToUpperInvariant());
    }
}
