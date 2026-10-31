using BizFlow.Application.Authentication;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Tenancy;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tenancy;
using Xunit;

namespace BizFlow.UnitTests.Tenancy;

public sealed class CompanyRegistrationManagementTests
{
    private sealed class StubTenantContext : ITenantContext
    {
        public Guid? UserId { get; set; }
        public Guid? TenantId { get; set; }
    }

    private sealed class StubAuthorizer : IResourceAuthorizer
    {
        public bool ShouldAllow { get; set; } = true;
        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default)
        {
            if (!ShouldAllow) throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Access denied.");
            return Task.CompletedTask;
        }
    }

    private sealed class StubCredentialVerifier : ICredentialVerifier
    {
        public PasswordCheck Verify(UserAccount user, string password) => new(true);
        public void VerifyDummy(string password) { }
        public string Hash(UserAccount user, string password) => "HASHED_" + password;
    }

    private sealed class StubCompanyStore : ICompanyProvisioningStore
    {
        public bool CodeExists { get; set; }
        public bool EmailExists { get; set; }
        public bool KeyExists { get; set; }

        public Task<bool> CompanyCodeExistsAsync(string code, CancellationToken cancellationToken) => Task.FromResult(CodeExists);
        public Task<bool> CompanyEmailExistsAsync(string email, CancellationToken cancellationToken) => Task.FromResult(EmailExists);
        public Task<bool> TenantKeyExistsAsync(string tenantKey, CancellationToken cancellationToken) => Task.FromResult(KeyExists);

        public Task<SubmitCompanyRegistrationResult> SubmitRegistrationAsync(Company company, CancellationToken cancellationToken) =>
            Task.FromResult(new SubmitCompanyRegistrationResult(
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
                company.EvidenceDocumentFileName));

        public Task<ApproveCompanyRegistrationResult> ApproveAsync(
            Guid companyId, string tenantKey, string adminEmployeeCode, string adminEmail,
            string adminFullName, string passwordHash, string timeZone, Guid platformAdminId,
            DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(new ApproveCompanyRegistrationResult(companyId, Guid.NewGuid(), Guid.NewGuid(), tenantKey, "ACTIVE"));

        public Task<RejectCompanyRegistrationResult> RejectAsync(Guid companyId, string reason, Guid platformAdminId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(new RejectCompanyRegistrationResult(companyId, "INACTIVE"));

        public Task<UpdateCompanyStatusResult> UpdateStatusAsync(Guid companyId, CompanyStatus status, Guid platformAdminId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(new UpdateCompanyStatusResult(companyId, status.ToString().ToUpperInvariant()));
    }

    [Fact]
    public void Company_entity_lifecycle_enforces_pending_transition_rules()
    {
        var now = DateTimeOffset.UtcNow;
        var company = Company.Register("ACME", "Acme Corporation", "0101234567", "John Doe", "contact@acme.test", "0901234567", now);

        Assert.Equal("ACME", company.Code);
        Assert.Equal("Acme Corporation", company.Name);
        Assert.Equal("0101234567", company.TaxId);
        Assert.Equal("John Doe", company.ContactPersonName);
        Assert.Equal("contact@acme.test", company.ContactEmail);
        Assert.Equal("0901234567", company.ContactPhone);
        Assert.Equal(CompanyStatus.Pending, company.Status);

        company.Approve(now.AddMinutes(5));
        Assert.Equal(CompanyStatus.Active, company.Status);

        // Cannot re-approve an already active company
        Assert.Throws<InvalidOperationException>(() => company.Approve(now.AddMinutes(10)));
    }

    [Fact]
    public void Company_reject_requires_pending_state()
    {
        var now = DateTimeOffset.UtcNow;
        var company = Company.Register("TEST", "Test Co", "0109876543", "Jane Doe", "info@test.test", "0912345678", now);
        company.Reject(now.AddMinutes(1));
        Assert.Equal(CompanyStatus.Inactive, company.Status);

        Assert.Throws<InvalidOperationException>(() => company.Reject(now.AddMinutes(2)));
    }

    [Theory]
    [InlineData("", "Company Name", "0101234567", "John Doe", "contact@acme.test", "0901234567")]
    [InlineData("ACME", "", "0101234567", "John Doe", "contact@acme.test", "0901234567")]
    [InlineData("ACME", "Company Name", "", "John Doe", "contact@acme.test", "0901234567")]
    [InlineData("ACME", "Company Name", "0101234567", "", "contact@acme.test", "0901234567")]
    [InlineData("ACME", "Company Name", "0101234567", "John Doe", "invalid-email", "0901234567")]
    [InlineData("ACME", "Company Name", "0101234567", "John Doe", "contact@acme.test", "")]
    public async Task SubmitAsync_validates_all_six_mandatory_fields(
        string code, string name, string taxId, string contactPerson, string contactEmail, string contactPhone)
    {
        var store = new StubCompanyStore();
        var management = new CompanyRegistrationManagement(new StubTenantContext(), new StubAuthorizer(), new StubCredentialVerifier(), store, TimeProvider.System);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            management.SubmitAsync(new SubmitCompanyRegistrationRequest(code, name, taxId, contactPerson, contactEmail, contactPhone), CancellationToken.None));

        Assert.Equal("VALIDATION.FAILED", fault.Code);
        Assert.Equal(FaultKind.Validation, fault.Kind);
    }

    [Fact]
    public async Task SubmitAsync_rejects_duplicate_code_or_email()
    {
        var store = new StubCompanyStore { CodeExists = true };
        var management = new CompanyRegistrationManagement(new StubTenantContext(), new StubAuthorizer(), new StubCredentialVerifier(), store, TimeProvider.System);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            management.SubmitAsync(new SubmitCompanyRegistrationRequest("EXISTING", "Company Name", "0101234567", "John", "test@company.test", "0901234567"), CancellationToken.None));

        Assert.Equal("TENANT.CODE_IN_USE", fault.Code);
    }

    [Fact]
    public async Task ApproveAsync_requires_platform_plane_context_and_valid_slug()
    {
        var store = new StubCompanyStore();
        // Context with tenant ID (not platform admin)
        var context = new StubTenantContext { UserId = Guid.NewGuid(), TenantId = Guid.NewGuid() };
        var management = new CompanyRegistrationManagement(context, new StubAuthorizer(), new StubCredentialVerifier(), store, TimeProvider.System);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            management.ApproveAsync(Guid.NewGuid(), new ApproveCompanyRegistrationRequest("invalid key", "ADMIN01", "admin@corp.test", "Admin", "Secret123!"), CancellationToken.None));

        Assert.Equal("ACCESS.DENIED", fault.Code);
    }

    [Fact]
    public async Task RejectAsync_enforces_mandatory_rejection_reason_BR_022()
    {
        var store = new StubCompanyStore();
        var context = new StubTenantContext { UserId = Guid.NewGuid(), TenantId = null };
        var management = new CompanyRegistrationManagement(context, new StubAuthorizer(), new StubCredentialVerifier(), store, TimeProvider.System);

        var fault = await Assert.ThrowsAsync<ApplicationFault>(() =>
            management.RejectAsync(Guid.NewGuid(), new RejectCompanyRegistrationRequest(""), CancellationToken.None));

        Assert.Equal("VALIDATION.FAILED", fault.Code);
    }

    [Fact]
    public void Department_update_rejects_self_parent()
    {
        var dept = Department.Create(Guid.NewGuid(), "IT", "Information Tech", null, DateTimeOffset.UtcNow);
        Assert.Throws<ArgumentException>(() => dept.Update("IT Updated", dept.Id, RecordStatus.Active, DateTimeOffset.UtcNow));
    }

    [Fact]
    public void UserAccount_update_profile_and_status_rotates_security_stamp_when_inactive()
    {
        var now = DateTimeOffset.UtcNow;
        var user = UserAccount.CreateTenantUser(Guid.NewGuid(), "EMP01", "emp01@test.com", "Employee 1", "hash", now, null);
        var initialStamp = user.SecurityStamp;

        user.UpdateProfile("Employee Updated", null, now.AddMinutes(1));
        Assert.Equal("Employee Updated", user.FullName);
        Assert.Equal(initialStamp, user.SecurityStamp);

        user.SetStatus(UserStatus.Inactive, now.AddMinutes(2));
        Assert.Equal(UserStatus.Inactive, user.Status);
        Assert.NotEqual(initialStamp, user.SecurityStamp);
    }
}
