using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.Authentication;
using BizFlow.Application.Common;
using BizFlow.Application.Organization;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using Xunit;

namespace BizFlow.UnitTests.Organization;

public sealed class BulkEmployeeImportTests
{
    private sealed class FakeTenantContext(Guid? tenantId, Guid? userId) : ITenantContext
    {
        public Guid? UserId => userId;
        public Guid? TenantId => tenantId;
    }

    private sealed class FakeResourceAuthorizer(HashSet<string>? allowedPermissions = null) : IResourceAuthorizer
    {
        public List<string> CheckedPermissions { get; } = [];

        public Task AuthorizeAsync(string permission, ResourceScope resource, Guid? managementTargetDepartmentId = null, CancellationToken cancellationToken = default)
        {
            CheckedPermissions.Add(permission);
            if (allowedPermissions != null && !allowedPermissions.Contains(permission))
            {
                throw new ApplicationFault(FaultKind.Forbidden, "SECURITY.PERMISSION_DENIED", $"Missing permission {permission}.");
            }
            return Task.CompletedTask;
        }
    }

    private sealed class FakeCredentialVerifier : ICredentialVerifier
    {
        public string Hash(UserAccount user, string password) => "hashed_" + password;
        public PasswordCheck Verify(UserAccount user, string password) => new(true);
        public void VerifyDummy(string password) { }
    }

    private sealed class FakeClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private sealed class FakeUserManagementStore : IUserManagementStore
    {
        public HashSet<string> ExistingEmployeeCodes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> ExistingEmails { get; } = new(StringComparer.OrdinalIgnoreCase);
        public Dictionary<string, Guid> Departments { get; } = new(StringComparer.OrdinalIgnoreCase);
        public List<(UserAccount user, IReadOnlyList<Guid> roleIds, AuditLog audit)> CreatedUsers { get; } = [];

        public Task<bool> EmployeeCodeExistsAsync(Guid tenantId, string employeeCode, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingEmployeeCodes.Contains(employeeCode));

        public Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken cancellationToken) =>
            Task.FromResult(ExistingEmails.Contains(email));

        public Task<bool> DepartmentExistsAsync(Guid tenantId, Guid departmentId, CancellationToken cancellationToken) =>
            Task.FromResult(Departments.ContainsValue(departmentId));

        public Task<IReadOnlyList<DepartmentLookup>> GetDepartmentsAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DepartmentLookup>>(
                Departments.Select(d => new DepartmentLookup(d.Value, d.Key, d.Key + " Name")).ToList()
            );

        public Task<IReadOnlyList<RoleLookup>> GetRolesAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RoleLookup>>([
                new RoleLookup(Guid.NewGuid(), "EMPLOYEE"),
                new RoleLookup(Guid.NewGuid(), "MANAGER"),
                new RoleLookup(Guid.NewGuid(), "COMPANY_ADMIN")
            ]);

        public Task<int> BatchCreateUsersAsync(IReadOnlyList<(UserAccount user, IReadOnlyList<Guid> roleIds, AuditLog audit)> users, CancellationToken cancellationToken)
        {
            CreatedUsers.AddRange(users);
            return Task.FromResult(users.Count);
        }

        public Task<UserOperationResult> CreateUserAsync(UserAccount user, IReadOnlyList<Guid> roleIds, AuditLog audit, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<UserOperationResult> UpdateUserAsync(Guid tenantId, Guid userId, string fullName, Guid? departmentId, IReadOnlyList<Guid>? roleIds, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<UserOperationResult> UpdateStatusAsync(Guid tenantId, Guid userId, UserStatus status, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<UserResponsibilitiesView> GetResponsibilitiesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<UserOperationResult> PromoteToManagerAsync(Guid tenantId, Guid userId, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<UserOperationResult> DemoteToEmployeeAsync(Guid tenantId, Guid userId, Guid? transferToUserId, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotImplementedException();

        public Task<int> CountActiveAdminsAsync(Guid tenantId, CancellationToken cancellationToken) => Task.FromResult(1);
        public Task<bool> IsUserAdminAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> HasAdminRoleAsync(Guid tenantId, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<UserOperationResult> TransferAdminOwnershipAsync(Guid tenantId, Guid currentAdminId, Guid targetUserId, Guid? demoteToRoleId, DateTimeOffset now, CancellationToken cancellationToken) =>
            throw new NotImplementedException();
    }

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _callerId = Guid.NewGuid();
    private readonly FakeUserManagementStore _store = new();
    private readonly FakeResourceAuthorizer _authorizer;
    private readonly FakeClock _clock = new(new DateTimeOffset(2026, 11, 11, 10, 0, 0, TimeSpan.Zero));
    private readonly UserManagement _sut;

    public BulkEmployeeImportTests()
    {
        _authorizer = new FakeResourceAuthorizer([UserManagement.ImportPermission, UserManagement.ReadPermission]);
        _store.Departments["IT"] = Guid.NewGuid();
        _store.Departments["HR"] = Guid.NewGuid();

        _sut = new UserManagement(
            new FakeTenantContext(_tenantId, _callerId),
            _authorizer,
            new FakeCredentialVerifier(),
            _store,
            _clock
        );
    }

    [Fact]
    public async Task TestB1_ValidRows_DryRun_ValidatesWithoutCreating()
    {
        var rows = new List<BatchImportUserRow>
        {
            new("EMP001", "Nguyễn Văn An", "an.nguyen@bizflow.local", "IT", "EMPLOYEE"),
            new("EMP002", "Trần Thị Bình", "binh.tran@bizflow.local", "HR", "MANAGER")
        };

        var result = await _sut.BatchImportAsync(new(rows, DryRun: true), CancellationToken.None);

        Assert.Equal(2, result.TotalProcessed);
        Assert.Equal(2, result.TotalValid);
        Assert.Equal(0, result.TotalImported);
        Assert.True(result.IsDryRun);
        Assert.Equal(2, result.Items.Count);
        Assert.All(result.Items, item => Assert.True(item.IsValid));
        Assert.Empty(_store.CreatedUsers);
        Assert.Contains(UserManagement.ImportPermission, _authorizer.CheckedPermissions);
    }

    [Fact]
    public async Task TestB1_ValidRows_Commit_CreatesUsersInDatabase()
    {
        var rows = new List<BatchImportUserRow>
        {
            new("EMP001", "Nguyễn Văn An", "an.nguyen@bizflow.local", "IT", "EMPLOYEE"),
            new("EMP002", "Trần Thị Bình", "binh.tran@bizflow.local", "HR", "MANAGER")
        };

        var result = await _sut.BatchImportAsync(new(rows, DryRun: false), CancellationToken.None);

        Assert.Equal(2, result.TotalProcessed);
        Assert.Equal(2, result.TotalValid);
        Assert.Equal(2, result.TotalImported);
        Assert.False(result.IsDryRun);
        Assert.Equal(2, _store.CreatedUsers.Count);

        var first = _store.CreatedUsers[0].user;
        Assert.Equal("EMP001", first.EmployeeCode);
        Assert.Equal("an.nguyen@bizflow.local", first.Email);
        Assert.True(first.MustChangePassword); // Generated activation token implies must change password
    }

    [Fact]
    public async Task TestB2_DuplicateEmployeeCodeAndEmail_AreFlaggedAsInvalid()
    {
        _store.ExistingEmployeeCodes.Add("EXISTING_CODE");
        _store.ExistingEmails.Add("existing@bizflow.local");

        var rows = new List<BatchImportUserRow>
        {
            new("EXISTING_CODE", "Nguyễn Văn X", "new1@bizflow.local", "IT", "EMPLOYEE"),
            new("EMP003", "Trần Thị Y", "existing@bizflow.local", "HR", "EMPLOYEE"),
            new("DUP_IN_BATCH", "Lê Văn Z1", "dup@bizflow.local", "IT", "EMPLOYEE"),
            new("DUP_IN_BATCH", "Lê Văn Z2", "dup@bizflow.local", "IT", "EMPLOYEE")
        };

        var result = await _sut.BatchImportAsync(new(rows, DryRun: true), CancellationToken.None);

        Assert.Equal(4, result.TotalProcessed);
        Assert.Equal(1, result.TotalValid);

        // First item fails on existing code
        Assert.False(result.Items[0].IsValid);
        Assert.Contains(result.Items[0].Errors, e => e.Contains("EXISTING_CODE"));

        // Second item fails on existing email
        Assert.False(result.Items[1].IsValid);
        Assert.Contains(result.Items[1].Errors, e => e.Contains("EXISTING@BIZFLOW.LOCAL", StringComparison.OrdinalIgnoreCase));

        // Fourth item fails on batch duplicate
        Assert.False(result.Items[3].IsValid);
    }

    [Fact]
    public async Task TestB3_NonExistentDepartmentCode_IsFlaggedWithDescriptiveError()
    {
        var rows = new List<BatchImportUserRow>
        {
            new("EMP004", "Phạm Văn D", "d.pham@bizflow.local", "NON_EXISTENT_DEPT", "EMPLOYEE")
        };

        var result = await _sut.BatchImportAsync(new(rows, DryRun: true), CancellationToken.None);

        Assert.Equal(1, result.TotalProcessed);
        Assert.Equal(0, result.TotalValid);
        Assert.False(result.Items[0].IsValid);
        Assert.Contains(result.Items[0].Errors, e => e.Contains("NON_EXISTENT_DEPT"));
    }

    [Fact]
    public async Task TestB4_MissingPassword_GeneratesSecureActivationTokenAndEnforcesPasswordChange()
    {
        var rows = new List<BatchImportUserRow>
        {
            new("EMP005", "Hoàng Văn E", "e.hoang@bizflow.local", "IT", "EMPLOYEE", Password: null)
        };

        var result = await _sut.BatchImportAsync(new(rows, DryRun: false), CancellationToken.None);

        Assert.Equal(1, result.TotalImported);
        var item = result.Items[0];
        Assert.NotNull(item.ActivationToken);
        Assert.True(item.ActivationToken.Length >= 8);

        var created = _store.CreatedUsers[0].user;
        Assert.True(created.MustChangePassword);
        Assert.StartsWith("hashed_", created.PasswordHash);
    }

    [Fact]
    public void TestB5_EmployeeFileProcessing_GeneratesValidTemplatesAndErrorReport()
    {
        // 1. XLSX template
        var xlsxBytes = EmployeeFileProcessing.GenerateXlsxTemplate();
        Assert.NotNull(xlsxBytes);
        Assert.True(xlsxBytes.Length > 0);

        // Verify parsing the generated XLSX template
        using var xlsxStream = new MemoryStream(xlsxBytes);
        var parsedXlsx = EmployeeFileProcessing.ParseXlsx(xlsxStream);
        Assert.NotEmpty(parsedXlsx);
        Assert.Equal("EMP001", parsedXlsx[0].EmployeeCode);
        Assert.Equal("Nguyễn Văn An", parsedXlsx[0].FullName);

        // 2. CSV template
        var csvBytes = EmployeeFileProcessing.GenerateCsvTemplate();
        Assert.NotNull(csvBytes);
        using var csvStream = new MemoryStream(csvBytes);
        var parsedCsv = EmployeeFileProcessing.ParseCsv(csvStream);
        Assert.NotEmpty(parsedCsv);
        Assert.Equal("EMP001", parsedCsv[0].EmployeeCode);

        // 3. Error report CSV
        var validationItems = new List<BatchImportValidationItem>
        {
            new(1, "EMP999", "invalid-email", "Test User", "UNKNOWN_DEPT", "EMPLOYEE", false, ["Email không hợp lệ.", "Mã phòng ban không tồn tại."])
        };
        var errorCsvBytes = EmployeeFileProcessing.GenerateErrorReportCsv(validationItems);
        var errorCsvText = Encoding.UTF8.GetString(errorCsvBytes);
        Assert.Contains("EMP999", errorCsvText);
        Assert.Contains("invalid-email", errorCsvText);
        Assert.Contains("Email không hợp lệ.", errorCsvText);
    }
}
