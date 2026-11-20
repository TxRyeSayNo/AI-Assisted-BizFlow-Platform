using System;
using System.Collections.Generic;
using System.Linq;
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

public sealed class UserManagementRbacTests
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

    private sealed class FakeUserManagementStore : IUserManagementStore
    {
        public int ActiveAdminCount { get; set; } = 1;
        public HashSet<Guid> AdminUserIds { get; } = [];
        public Guid AdminRoleId { get; set; } = Guid.NewGuid();
        public bool OwnershipTransferred { get; private set; }

        public Task<bool> EmployeeCodeExistsAsync(Guid tenantId, string employeeCode, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken cancellationToken) => Task.FromResult(false);
        public Task<bool> DepartmentExistsAsync(Guid tenantId, Guid departmentId, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<IReadOnlyList<DepartmentLookup>> GetDepartmentsAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<DepartmentLookup>>([]);

        public Task<IReadOnlyList<RoleLookup>> GetRolesAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RoleLookup>>([]);

        public Task<UserOperationResult> CreateUserAsync(UserAccount user, IReadOnlyList<Guid> roleIds, AuditLog audit, CancellationToken cancellationToken) =>
            Task.FromResult(new UserOperationResult(user.Id, user.EmployeeCode, user.Email, user.FullName, user.DepartmentId, "ACTIVE", roleIds, DateTimeOffset.UtcNow));

        public Task<int> BatchCreateUsersAsync(IReadOnlyList<(UserAccount user, IReadOnlyList<Guid> roleIds, AuditLog audit)> users, CancellationToken cancellationToken) =>
            Task.FromResult(users.Count);

        public Task<UserOperationResult> UpdateUserAsync(Guid tenantId, Guid userId, string fullName, Guid? departmentId, IReadOnlyList<Guid>? roleIds, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(new UserOperationResult(userId, "EMP01", "emp01@test.com", fullName, departmentId, "ACTIVE", roleIds ?? [], DateTimeOffset.UtcNow));

        public Task<UserOperationResult> UpdateStatusAsync(Guid tenantId, Guid userId, UserStatus newStatus, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(new UserOperationResult(userId, "EMP01", "emp01@test.com", "Test User", null, newStatus.ToString().ToUpperInvariant(), [], DateTimeOffset.UtcNow));

        public Task<UserResponsibilitiesView> GetResponsibilitiesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(new UserResponsibilitiesView(userId, "Test User", "EMP01", null, null, 0, 0, 0, false, []));

        public Task<UserOperationResult> PromoteToManagerAsync(Guid tenantId, Guid userId, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(new UserOperationResult(userId, "EMP01", "emp01@test.com", "Test User", null, "ACTIVE", [], DateTimeOffset.UtcNow));

        public Task<UserOperationResult> DemoteToEmployeeAsync(Guid tenantId, Guid userId, Guid? transferToUserId, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken) =>
            Task.FromResult(new UserOperationResult(userId, "EMP01", "emp01@test.com", "Test User", null, "ACTIVE", [], DateTimeOffset.UtcNow));

        public Task<int> CountActiveAdminsAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult(ActiveAdminCount);

        public Task<bool> IsUserAdminAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken) =>
            Task.FromResult(AdminUserIds.Contains(userId));

        public Task<bool> HasAdminRoleAsync(Guid tenantId, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken) =>
            Task.FromResult(roleIds.Contains(AdminRoleId));

        public Task<UserOperationResult> TransferAdminOwnershipAsync(Guid tenantId, Guid currentOwnerId, Guid targetUserId, Guid? demoteToRoleId, DateTimeOffset now, CancellationToken cancellationToken)
        {
            OwnershipTransferred = true;
            AdminUserIds.Remove(currentOwnerId);
            AdminUserIds.Add(targetUserId);
            return Task.FromResult(new UserOperationResult(targetUserId, "EMP02", "target@test.com", "Target Admin", null, "ACTIVE", [AdminRoleId], DateTimeOffset.UtcNow));
        }
    }

    [Fact]
    public async Task A1_WhenUserHasOnlyUsersRead_CreateThrowsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var authorizer = new FakeResourceAuthorizer([UserManagement.ReadPermission]);
        var store = new FakeUserManagementStore();

        var service = new UserManagement(
            new FakeTenantContext(tenantId, userId),
            authorizer,
            new FakeCredentialVerifier(),
            store,
            TimeProvider.System);

        var request = new CreateUserRequest("EMP01", "emp01@test.com", "Employee 1", "Password123!");

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.CreateAsync(request, default));
        Assert.Equal(FaultKind.Forbidden, ex.Kind);
    }

    [Fact]
    public async Task A2_WhenUserHasUsersCreate_CanCreateUserWithoutRolesConfigure()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        // Has users.create, but NOT roles.configure
        var authorizer = new FakeResourceAuthorizer([UserManagement.CreatePermission]);
        var store = new FakeUserManagementStore();

        var service = new UserManagement(
            new FakeTenantContext(tenantId, userId),
            authorizer,
            new FakeCredentialVerifier(),
            store,
            TimeProvider.System);

        var request = new CreateUserRequest("EMP01", "emp01@test.com", "Employee 1", "Password123!");

        var result = await service.CreateAsync(request, default);
        Assert.NotNull(result);
        Assert.Equal("EMP01", result.EmployeeCode);
        Assert.Contains(UserManagement.CreatePermission, authorizer.CheckedPermissions);
        Assert.DoesNotContain(UserManagement.RolesConfigurePermission, authorizer.CheckedPermissions);
    }

    [Fact]
    public async Task A3_PrivilegeEscalationGuard_WhenAssigningAdminRoleWithoutRolesConfigure_ThrowsForbidden()
    {
        var tenantId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var store = new FakeUserManagementStore();
        // Caller has users.create and users.roles.assign, but NOT roles.configure
        var authorizer = new FakeResourceAuthorizer([UserManagement.CreatePermission, UserManagement.RoleAssignPermission]);

        var service = new UserManagement(
            new FakeTenantContext(tenantId, userId),
            authorizer,
            new FakeCredentialVerifier(),
            store,
            TimeProvider.System);

        var request = new CreateUserRequest("EMP01", "emp01@test.com", "Employee 1", "Password123!", RoleIds: [store.AdminRoleId]);

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.CreateAsync(request, default));
        Assert.Equal(FaultKind.Forbidden, ex.Kind);
    }

    [Fact]
    public async Task A4_LastActiveAdminProtection_DeactivatingLastAdmin_ThrowsConflict()
    {
        var tenantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var store = new FakeUserManagementStore
        {
            ActiveAdminCount = 1
        };
        store.AdminUserIds.Add(adminId);

        var authorizer = new FakeResourceAuthorizer([UserManagement.StatusPermission]);
        var service = new UserManagement(
            new FakeTenantContext(tenantId, callerId),
            authorizer,
            new FakeCredentialVerifier(),
            store,
            TimeProvider.System);

        var request = new UpdateUserStatusRequest("INACTIVE");

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.UpdateStatusAsync(adminId, request, default));
        Assert.Equal(FaultKind.Conflict, ex.Kind);
        Assert.Equal("ADMIN.CANNOT_REMOVE_LAST_ACTIVE_ADMIN", ex.Code);
    }

    [Fact]
    public async Task A5_LastActiveAdminProtection_DemotingLastAdminViaRoleUpdate_ThrowsConflict()
    {
        var tenantId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var callerId = Guid.NewGuid();
        var store = new FakeUserManagementStore
        {
            ActiveAdminCount = 1
        };
        store.AdminUserIds.Add(adminId);

        // Caller has update, role assign, and configure permissions
        var authorizer = new FakeResourceAuthorizer([UserManagement.UpdatePermission, UserManagement.RoleAssignPermission, UserManagement.RolesConfigurePermission]);
        var service = new UserManagement(
            new FakeTenantContext(tenantId, callerId),
            authorizer,
            new FakeCredentialVerifier(),
            store,
            TimeProvider.System);

        // Non-admin role assigned to drop admin role from target user
        var employeeRoleId = Guid.NewGuid();
        var request = new UpdateUserRequest("Admin Name", null, RoleIds: [employeeRoleId]);

        var ex = await Assert.ThrowsAsync<ApplicationFault>(() => service.UpdateAsync(adminId, request, default));
        Assert.Equal(FaultKind.Conflict, ex.Kind);
        Assert.Equal("ADMIN.CANNOT_REMOVE_LAST_ACTIVE_ADMIN", ex.Code);
    }

    [Fact]
    public async Task A6_AdminOwnershipTransfer_WhenAdminTransfers_SucceedsAndUpdatesStore()
    {
        var tenantId = Guid.NewGuid();
        var callerAdminId = Guid.NewGuid();
        var targetUserId = Guid.NewGuid();
        var store = new FakeUserManagementStore();
        store.AdminUserIds.Add(callerAdminId);

        var authorizer = new FakeResourceAuthorizer([UserManagement.RolesConfigurePermission]);
        var service = new UserManagement(
            new FakeTenantContext(tenantId, callerAdminId),
            authorizer,
            new FakeCredentialVerifier(),
            store,
            TimeProvider.System);

        var request = new TransferOwnershipRequest(targetUserId);

        var result = await service.TransferAdminOwnershipAsync(request, default);
        Assert.NotNull(result);
        Assert.True(store.OwnershipTransferred);
        Assert.Contains(targetUserId, store.AdminUserIds);
        Assert.DoesNotContain(callerAdminId, store.AdminUserIds);
    }
}
