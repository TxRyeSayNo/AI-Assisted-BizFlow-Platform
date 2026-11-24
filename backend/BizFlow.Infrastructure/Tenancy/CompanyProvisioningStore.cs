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

public sealed class CompanyProvisioningStore(BizFlowDbContext db, ICompanyRegistrationLinks? links = null) : ICompanyProvisioningStore
{
    public Task<bool> CompanyCodeExistsAsync(string code, CancellationToken cancellationToken) =>
        db.Companies.IgnoreQueryFilters().AnyAsync(c => c.Code == code, cancellationToken);

    public Task<bool> CompanyEmailExistsAsync(string email, CancellationToken cancellationToken) =>
        db.Companies.IgnoreQueryFilters().AnyAsync(c => c.ContactEmail.ToUpper() == email, cancellationToken);

    public Task<bool> TenantKeyExistsAsync(string tenantKey, CancellationToken cancellationToken) =>
        db.Tenants.IgnoreQueryFilters().AnyAsync(t => t.TenantKey == tenantKey, cancellationToken);

    public async Task<SubmitCompanyRegistrationResult> SubmitRegistrationAsync(Company company, CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        db.Companies.Add(company);
        await db.SaveChangesAsync(cancellationToken);
        await CompanyRegistrationEmailQueue.EnqueueAsync(db, company, "RECEIVED", null, null, null, cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return new(
            company.Id,
            company.Code,
            company.Name,
            company.TaxId,
            company.ContactPersonName,
            company.ContactEmail,
            company.ContactPhone,
            company.Status.ToString().ToUpperInvariant(),
            company.CreatedAt,
            company.EvidenceDocumentKey,
            company.EvidenceDocumentFileName);
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
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var company = await db.Companies.FromSqlInterpolated($"SELECT *, xmin FROM \"Company\" WHERE \"CompanyId\"={companyId} FOR UPDATE").IgnoreQueryFilters().SingleOrDefaultAsync(cancellationToken);
        if (company is null) throw ApplicationFault.NotFound();
        if (company.Status != CompanyStatus.Pending)
            throw new ApplicationFault(FaultKind.Conflict, "TENANT.INVALID_STATUS", "Only Pending company registrations can be approved.");

        company.Approve(now);

        var tenant = Tenant.Create(company.Id, tenantKey, company.Name, timeZone, now);
        db.Tenants.Add(tenant);

        var adminUser = UserAccount.CreateTenantUser(tenant.Id, adminEmployeeCode, adminEmail, adminFullName, passwordHash, now, null);
        adminUser.RequirePasswordChange();
        db.Users.Add(adminUser);

        var companyAdminRole = await db.Roles.IgnoreQueryFilters().SingleOrDefaultAsync(r => r.Name == "COMPANY_ADMIN" && r.IsSystem, cancellationToken)
            ?? throw new InvalidOperationException("System role COMPANY_ADMIN not found.");

        db.UserRoles.Add(new UserRole(adminUser.Id, companyAdminRole.Id));

        db.AuditLogs.Add(AuditLog.CompanyApproved(company.Id, platformAdminId, tenant.Id, tenant.TenantKey, now));

        await db.SaveChangesAsync(cancellationToken);

        await CompanyRegistrationEmailQueue.EnqueueAsync(db, company, "APPROVED", tenant.Id, adminUser.Id, null, cancellationToken);
        if (!company.ContactEmail.Equals(adminUser.Email,StringComparison.OrdinalIgnoreCase))
            await CompanyRegistrationEmailQueue.EnqueueAsync(db, company, "APPROVED_CONTACT", tenant.Id, adminUser.Id, null, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new(company.Id, tenant.Id, adminUser.Id, tenant.TenantKey, company.Status.ToString().ToUpperInvariant());
    }

    public async Task<RejectCompanyRegistrationResult> RejectAsync(
        Guid companyId,
        string reason,
        Guid platformAdminId,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var tx = await db.Database.BeginTransactionAsync(cancellationToken);
        var company = await db.Companies.FromSqlInterpolated($"SELECT *, xmin FROM \"Company\" WHERE \"CompanyId\"={companyId} FOR UPDATE").IgnoreQueryFilters().SingleOrDefaultAsync(cancellationToken);
        if (company is null) throw ApplicationFault.NotFound();
        if (company.Status != CompanyStatus.Pending)
            throw new ApplicationFault(FaultKind.Conflict, "TENANT.INVALID_STATUS", "Only Pending company registrations can be rejected.");

        company.Reject(now);

        db.AuditLogs.Add(AuditLog.CompanyRejected(company.Id, platformAdminId, reason, now));

        await db.SaveChangesAsync(cancellationToken);

        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"CompanyRegistrationEmail\" SET \"ConsumedAt\"={now} WHERE \"CompanyId\"={company.Id} AND \"Payload\"->>'Kind'='REJECTED' AND \"ConsumedAt\" IS NULL",cancellationToken);
        await CompanyRegistrationEmailQueue.EnqueueAsync(db, company, "REJECTED", null, null, reason, cancellationToken);
        await tx.CommitAsync(cancellationToken);

        return new(company.Id, company.Status.ToString().ToUpperInvariant());
    }

    private static SubmitCompanyRegistrationResult View(Company company) => new(company.Id, company.Code, company.Name, company.TaxId,
        company.ContactPersonName, company.ContactEmail, company.ContactPhone, company.Status.ToString().ToUpperInvariant(), company.CreatedAt,
        company.EvidenceDocumentKey, company.EvidenceDocumentFileName);

    private async Task<Company> Rejected(string token, bool locked, CancellationToken ct)
    {
        var ticket = links is null ? null : await links.ReadAsync(token, ct);
        if (ticket is null) throw ApplicationFault.NotFound();
        var company = locked
            ? await db.Companies.FromSqlInterpolated($"SELECT *, xmin FROM \"Company\" WHERE \"CompanyId\"={ticket.CompanyId} FOR UPDATE").IgnoreQueryFilters().SingleOrDefaultAsync(ct)
            : await db.Companies.IgnoreQueryFilters().SingleOrDefaultAsync(c=>c.Id==ticket.CompanyId,ct);
        if (company is null || company.Status!=CompanyStatus.Inactive || company.ContactEmail.ToUpperInvariant()!=ticket.Email ||
            company.UpdatedAt.UtcTicks/10!=ticket.UpdatedTicks/10 || await db.Tenants.IgnoreQueryFilters().AnyAsync(t=>t.CompanyId==company.Id,ct)) throw ApplicationFault.NotFound();
        var issued=await db.Database.SqlQuery<int>($"SELECT count(*)::integer AS \"Value\" FROM \"CompanyRegistrationEmail\" WHERE \"Id\"={ticket.DecisionId} AND \"CompanyId\"={company.Id} AND \"Payload\"->>'Kind'='REJECTED' AND \"ConsumedAt\" IS NULL").SingleAsync(ct);
        if(issued!=1)throw ApplicationFault.NotFound();
        return company;
    }

    public async Task<SubmitCompanyRegistrationResult> GetResubmissionAsync(string token,CancellationToken ct) => View(await Rejected(token,false,ct));

    public async Task<SubmitCompanyRegistrationResult> ResubmitRegistrationAsync(Company submitted,string token,CancellationToken ct)
    {
        await using var tx=await db.Database.BeginTransactionAsync(ct);
        var previous=await Rejected(token,true,ct);
        if(previous.Code!=submitted.Code || !previous.ContactEmail.Equals(submitted.ContactEmail,StringComparison.OrdinalIgnoreCase)) throw ApplicationFault.NotFound();
        // The signed, version-bound link authorizes only these registration fields. Never update an existing tenant.
        await db.Database.ExecuteSqlInterpolatedAsync($"""
            UPDATE "Company" SET "Name"={submitted.Name},"TaxId"={submitted.TaxId},"ContactPersonName"={submitted.ContactPersonName},
              "ContactPhone"={submitted.ContactPhone},"EvidenceDocumentKey"={submitted.EvidenceDocumentKey},"EvidenceDocumentFileName"={submitted.EvidenceDocumentFileName},
              "Status"='PENDING',"UpdatedAt"={submitted.UpdatedAt}
            WHERE "CompanyId"={previous.Id} AND "Status"='INACTIVE'
            """,ct);
        await db.Entry(previous).ReloadAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE \"CompanyRegistrationEmail\" SET \"ConsumedAt\"={submitted.UpdatedAt} WHERE \"CompanyId\"={previous.Id} AND \"Payload\"->>'Kind'='REJECTED' AND \"ConsumedAt\" IS NULL",ct);
        await CompanyRegistrationEmailQueue.EnqueueAsync(db,previous,"RECEIVED",null,null,null,ct);
        await tx.CommitAsync(ct);
        return View(previous);
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
