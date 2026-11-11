using BizFlow.Application.Authentication;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;

namespace BizFlow.Application.Organization;

public sealed record CreateUserRequest(
    string EmployeeCode,
    string Email,
    string FullName,
    string Password,
    Guid? DepartmentId = null,
    IReadOnlyList<Guid>? RoleIds = null);

public sealed record UpdateUserRequest(
    string FullName,
    Guid? DepartmentId = null,
    IReadOnlyList<Guid>? RoleIds = null);

public sealed record UpdateUserStatusRequest(string Status);
public sealed record DemoteUserRequest(Guid? TransferToUserId = null);

public sealed record EligibleManagerView(Guid UserId, string FullName, string EmployeeCode);
public sealed record UserResponsibilitiesView(
    Guid UserId,
    string FullName,
    string EmployeeCode,
    Guid? DepartmentId,
    string? DepartmentName,
    int ActiveMainTasksCount,
    int ActiveChildTasksCount,
    int PendingApprovalsCount,
    bool IsSoleDepartmentManager,
    IReadOnlyList<EligibleManagerView> EligibleReplacementManagers);

public sealed record UserOperationResult(
    Guid UserId,
    string EmployeeCode,
    string Email,
    string FullName,
    Guid? DepartmentId,
    string Status,
    IReadOnlyList<Guid> RoleIds,
    DateTimeOffset CreatedAt);

public sealed record DepartmentLookup(Guid Id, string Code, string Name);
public sealed record RoleLookup(Guid Id, string Name);

public sealed record BatchImportUserRow(
    string EmployeeCode,
    string FullName,
    string Email,
    string? DepartmentCode = null,
    string? Role = null,
    string? Password = null);

public sealed record BatchImportUsersRequest(
    IReadOnlyList<BatchImportUserRow> Users,
    bool DryRun = false);

public sealed record BatchImportValidationItem(
    int RowIndex,
    string EmployeeCode,
    string Email,
    string FullName,
    string? DepartmentCode,
    string? Role,
    bool IsValid,
    IReadOnlyList<string> Errors,
    string? ActivationToken = null);

public sealed record BatchImportUsersResult(
    int TotalProcessed,
    int TotalValid,
    int TotalImported,
    bool IsDryRun,
    IReadOnlyList<BatchImportValidationItem> Items);

public sealed record TransferOwnershipRequest(Guid TargetUserId, Guid? DemoteToRoleId = null);

public interface IUserManagementStore
{
    Task<bool> EmployeeCodeExistsAsync(Guid tenantId, string employeeCode, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken cancellationToken);
    Task<bool> DepartmentExistsAsync(Guid tenantId, Guid departmentId, CancellationToken cancellationToken);
    Task<IReadOnlyList<DepartmentLookup>> GetDepartmentsAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<IReadOnlyList<RoleLookup>> GetRolesAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<UserOperationResult> CreateUserAsync(UserAccount user, IReadOnlyList<Guid> roleIds, AuditLog audit, CancellationToken cancellationToken);
    Task<int> BatchCreateUsersAsync(IReadOnlyList<(UserAccount user, IReadOnlyList<Guid> roleIds, AuditLog audit)> users, CancellationToken cancellationToken);
    Task<UserOperationResult> UpdateUserAsync(Guid tenantId, Guid userId, string fullName, Guid? departmentId, IReadOnlyList<Guid>? roleIds, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<UserOperationResult> UpdateStatusAsync(Guid tenantId, Guid userId, UserStatus newStatus, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<UserResponsibilitiesView> GetResponsibilitiesAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
    Task<UserOperationResult> PromoteToManagerAsync(Guid tenantId, Guid userId, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<UserOperationResult> DemoteToEmployeeAsync(Guid tenantId, Guid userId, Guid? transferToUserId, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<int> CountActiveAdminsAsync(Guid tenantId, CancellationToken cancellationToken);
    Task<bool> IsUserAdminAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken);
    Task<bool> HasAdminRoleAsync(Guid tenantId, IReadOnlyList<Guid> roleIds, CancellationToken cancellationToken);
    Task<UserOperationResult> TransferAdminOwnershipAsync(Guid tenantId, Guid currentOwnerId, Guid targetUserId, Guid? demoteToRoleId, DateTimeOffset now, CancellationToken cancellationToken);
}

public sealed class UserManagement(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    ICredentialVerifier credentialVerifier,
    IUserManagementStore store,
    TimeProvider clock)
{
    public const string ReadPermission = "users.read";
    public const string CreatePermission = "users.create";
    public const string ImportPermission = "users.import";
    public const string UpdatePermission = "users.update";
    public const string StatusPermission = "users.status.update";
    public const string RoleAssignPermission = "users.roles.assign";
    public const string RolesConfigurePermission = "roles.configure";
    public const string ManagePermission = "roles.configure";

    private async Task<Guid> AuthorizeOperationAsync(string permission, CancellationToken cancellationToken)
    {
        try
        {
            await authorizer.AuthorizeAsync(permission, new ResourceScope(context.TenantId), cancellationToken: cancellationToken);
        }
        catch (ApplicationFault)
        {
            if (permission != RolesConfigurePermission)
            {
                await authorizer.AuthorizeAsync(RolesConfigurePermission, new ResourceScope(context.TenantId), cancellationToken: cancellationToken);
            }
            else
            {
                throw;
            }
        }
        return context.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "User management requires a tenant workspace.");
    }

    private async Task CheckPrivilegeEscalationAsync(Guid tenantId, IReadOnlyList<Guid>? roleIds, CancellationToken cancellationToken)
    {
        if (roleIds is null || roleIds.Count == 0) return;
        var hasAdminRole = await store.HasAdminRoleAsync(tenantId, roleIds, cancellationToken);
        if (hasAdminRole)
        {
            await authorizer.AuthorizeAsync(RolesConfigurePermission, new ResourceScope(tenantId), cancellationToken: cancellationToken);
        }
    }

    private async Task EnsureNotLastActiveAdminAsync(Guid tenantId, Guid userId, CancellationToken cancellationToken)
    {
        var isTargetAdmin = await store.IsUserAdminAsync(tenantId, userId, cancellationToken);
        if (isTargetAdmin)
        {
            var activeAdminCount = await store.CountActiveAdminsAsync(tenantId, cancellationToken);
            if (activeAdminCount <= 1)
            {
                throw new ApplicationFault(FaultKind.Conflict, "ADMIN.CANNOT_REMOVE_LAST_ACTIVE_ADMIN", "Cannot remove or deactivate the only active company administrator.");
            }
        }
    }

    public async Task<UserOperationResult> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeOperationAsync(CreatePermission, cancellationToken);

        if (request.RoleIds is { Count: > 0 })
        {
            await AuthorizeOperationAsync(RoleAssignPermission, cancellationToken);
            await CheckPrivilegeEscalationAsync(tenantId, request.RoleIds, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(request.EmployeeCode) || request.EmployeeCode.Trim().Length > 50)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Employee code is required and must not exceed 50 characters.");
        if (string.IsNullOrWhiteSpace(request.Email) || !request.Email.Contains('@') || request.Email.Trim().Length > 200)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "A valid email address is required.");
        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 200)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Full name is required and must not exceed 200 characters.");
        if (string.IsNullOrWhiteSpace(request.Password) || request.Password.Length < 8)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Password must be at least 8 characters long.");

        var normalizedCode = request.EmployeeCode.Trim().ToUpperInvariant();
        var normalizedEmail = request.Email.Trim().ToUpperInvariant();

        if (await store.EmployeeCodeExistsAsync(tenantId, normalizedCode, cancellationToken))
            throw new ApplicationFault(FaultKind.Conflict, "USER.CODE_IN_USE", "A user with this employee code already exists in your company.");
        if (await store.EmailExistsAsync(tenantId, normalizedEmail, cancellationToken))
            throw new ApplicationFault(FaultKind.Conflict, "USER.EMAIL_IN_USE", "A user with this email address already exists in your company.");

        if (request.DepartmentId is { } deptId && !await store.DepartmentExistsAsync(tenantId, deptId, cancellationToken))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "The specified department does not exist.");

        var now = clock.GetUtcNow();
        var dummyUser = UserAccount.CreatePlatformAdministrator("TEMP", "temp@example.invalid", "Temp", "placeholder", now);
        var passwordHash = credentialVerifier.Hash(dummyUser, request.Password);

        var user = UserAccount.CreateTenantUser(tenantId, normalizedCode, request.Email.Trim(), request.FullName.Trim(), passwordHash, now, request.DepartmentId);
        var audit = AuditLog.UserCreated(tenantId, context.UserId!.Value, user.Id, user.EmployeeCode, user.Email, now);

        return await store.CreateUserAsync(user, request.RoleIds ?? [], audit, cancellationToken);
    }

    public async Task<UserOperationResult> UpdateAsync(Guid userId, UpdateUserRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeOperationAsync(UpdatePermission, cancellationToken);

        if (request.RoleIds is not null)
        {
            await AuthorizeOperationAsync(RoleAssignPermission, cancellationToken);
            await CheckPrivilegeEscalationAsync(tenantId, request.RoleIds, cancellationToken);

            var targetIsAdmin = await store.IsUserAdminAsync(tenantId, userId, cancellationToken);
            if (targetIsAdmin)
            {
                var stillHasAdmin = await store.HasAdminRoleAsync(tenantId, request.RoleIds, cancellationToken);
                if (!stillHasAdmin)
                {
                    await EnsureNotLastActiveAdminAsync(tenantId, userId, cancellationToken);
                }
            }
        }

        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 200)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Full name is required and must not exceed 200 characters.");

        if (request.DepartmentId is { } deptId && !await store.DepartmentExistsAsync(tenantId, deptId, cancellationToken))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "The specified department does not exist.");

        var now = clock.GetUtcNow();
        return await store.UpdateUserAsync(tenantId, userId, request.FullName.Trim(), request.DepartmentId, request.RoleIds, context.UserId!.Value, now, cancellationToken);
    }

    public async Task<UserOperationResult> UpdateStatusAsync(Guid userId, UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeOperationAsync(StatusPermission, cancellationToken);

        if (context.UserId == userId)
            throw new ApplicationFault(FaultKind.Conflict, "USER.CANNOT_CHANGE_OWN_STATUS", "You cannot modify the status of your own account.");

        if (!Enum.TryParse<UserStatus>(request.Status, true, out var newStatus))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Valid status values are: ACTIVE, INACTIVE, LOCKED.");

        if (newStatus != UserStatus.Active)
        {
            await EnsureNotLastActiveAdminAsync(tenantId, userId, cancellationToken);
        }

        var now = clock.GetUtcNow();
        return await store.UpdateStatusAsync(tenantId, userId, newStatus, context.UserId!.Value, now, cancellationToken);
    }

    public async Task<UserResponsibilitiesView> GetResponsibilitiesAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeOperationAsync(ReadPermission, cancellationToken);
        return await store.GetResponsibilitiesAsync(tenantId, userId, cancellationToken);
    }

    public async Task<UserOperationResult> PromoteToManagerAsync(Guid userId, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeOperationAsync(RoleAssignPermission, cancellationToken);
        if (context.UserId == userId)
            throw new ApplicationFault(FaultKind.Conflict, "USER.CANNOT_PROMOTE_SELF", "You cannot promote your own account.");
        var now = clock.GetUtcNow();
        return await store.PromoteToManagerAsync(tenantId, userId, context.UserId!.Value, now, cancellationToken);
    }

    public async Task<UserOperationResult> DemoteToEmployeeAsync(Guid userId, DemoteUserRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeOperationAsync(RoleAssignPermission, cancellationToken);
        if (context.UserId == userId)
            throw new ApplicationFault(FaultKind.Conflict, "USER.CANNOT_DEMOTE_SELF", "You cannot demote your own account.");

        await EnsureNotLastActiveAdminAsync(tenantId, userId, cancellationToken);

        var now = clock.GetUtcNow();
        return await store.DemoteToEmployeeAsync(tenantId, userId, request.TransferToUserId, context.UserId!.Value, now, cancellationToken);
    }

    public async Task<UserOperationResult> TransferAdminOwnershipAsync(TransferOwnershipRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeOperationAsync(RolesConfigurePermission, cancellationToken);
        var callerId = context.UserId ?? throw new ApplicationFault(FaultKind.Unauthenticated, "AUTH.UNAUTHENTICATED", "Authentication is required.");

        var isCallerAdmin = await store.IsUserAdminAsync(tenantId, callerId, cancellationToken);
        if (!isCallerAdmin)
            throw new ApplicationFault(FaultKind.Forbidden, "ADMIN.CALLER_NOT_ADMIN", "Only an active company administrator can transfer ownership.");

        if (request.TargetUserId == callerId)
            throw new ApplicationFault(FaultKind.Conflict, "ADMIN.CANNOT_TRANSFER_TO_SELF", "Cannot transfer ownership to yourself.");

        var now = clock.GetUtcNow();
        return await store.TransferAdminOwnershipAsync(tenantId, callerId, request.TargetUserId, request.DemoteToRoleId, now, cancellationToken);
    }

    public async Task<BatchImportUsersResult> BatchImportAsync(BatchImportUsersRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeOperationAsync(ImportPermission, cancellationToken);
        if (request.Users == null || request.Users.Count == 0)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "No user records provided for batch import.");

        if (request.Users.Count > 500)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Batch import cannot exceed 500 users at once.");

        var items = new List<BatchImportValidationItem>();
        var validUsers = new List<(BatchImportUserRow row, Guid? deptId, IReadOnlyList<Guid> roleIds, string passwordHash, bool isPendingActivation)>();

        var existingCodes = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var existingEmails = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var departments = await store.GetDepartmentsAsync(tenantId, cancellationToken);
        var deptByCode = departments.ToDictionary(d => d.Code, d => d.Id, StringComparer.OrdinalIgnoreCase);

        var roles = await store.GetRolesAsync(tenantId, cancellationToken);
        var managerRole = roles.FirstOrDefault(r => r.Name.Contains("MANAGER", StringComparison.OrdinalIgnoreCase) || r.Name.Contains("Quản lý", StringComparison.OrdinalIgnoreCase));
        var employeeRole = roles.FirstOrDefault(r => r.Name.Contains("EMPLOYEE", StringComparison.OrdinalIgnoreCase) || r.Name.Contains("Nhân viên", StringComparison.OrdinalIgnoreCase));

        for (var i = 0; i < request.Users.Count; i++)
        {
            var row = request.Users[i];
            var errors = new List<string>();

            if (string.IsNullOrWhiteSpace(row.EmployeeCode) || row.EmployeeCode.Trim().Length > 50)
                errors.Add("Mã nhân viên không được để trống và tối đa 50 ký tự.");

            if (string.IsNullOrWhiteSpace(row.Email) || !row.Email.Contains('@') || row.Email.Trim().Length > 200)
                errors.Add("Email không hợp lệ.");

            if (string.IsNullOrWhiteSpace(row.FullName) || row.FullName.Trim().Length > 200)
                errors.Add("Họ và tên không được để trống và tối đa 200 ký tự.");

            var normCode = row.EmployeeCode?.Trim().ToUpperInvariant() ?? "";
            var normEmail = row.Email?.Trim().ToUpperInvariant() ?? "";

            if (!string.IsNullOrEmpty(normCode))
            {
                if (existingCodes.Contains(normCode) || await store.EmployeeCodeExistsAsync(tenantId, normCode, cancellationToken))
                    errors.Add($"Mã nhân viên '{normCode}' đã tồn tại trong công ty.");
                else
                    existingCodes.Add(normCode);
            }

            if (!string.IsNullOrEmpty(normEmail))
            {
                if (existingEmails.Contains(normEmail) || await store.EmailExistsAsync(tenantId, normEmail, cancellationToken))
                    errors.Add($"Email '{normEmail}' đã tồn tại trong công ty.");
                else
                    existingEmails.Add(normEmail);
            }

            Guid? deptId = null;
            if (!string.IsNullOrWhiteSpace(row.DepartmentCode))
            {
                if (deptByCode.TryGetValue(row.DepartmentCode.Trim(), out var dId))
                    deptId = dId;
                else
                    errors.Add($"Mã phòng ban '{row.DepartmentCode}' không tồn tại.");
            }

            var roleIds = new List<Guid>();
            if (string.Equals(row.Role, "MANAGER", StringComparison.OrdinalIgnoreCase) && managerRole != null)
                roleIds.Add(managerRole.Id);
            else if (employeeRole != null)
                roleIds.Add(employeeRole.Id);

            var activationToken = string.IsNullOrWhiteSpace(row.Password) ? Guid.NewGuid().ToString("N")[..12] : null;
            var rawPassword = activationToken ?? row.Password!;
            if (rawPassword.Length < 8)
                errors.Add("Mật khẩu tối thiểu 8 ký tự.");

            var isValid = errors.Count == 0;
            items.Add(new BatchImportValidationItem(i + 1, row.EmployeeCode ?? "", row.Email ?? "", row.FullName ?? "", row.DepartmentCode, row.Role, isValid, errors, activationToken));

            if (isValid)
            {
                var dummyUser = UserAccount.CreatePlatformAdministrator("TEMP", "temp@example.invalid", "Temp", "placeholder", clock.GetUtcNow());
                var hash = credentialVerifier.Hash(dummyUser, rawPassword);
                validUsers.Add((row, deptId, roleIds, hash, activationToken != null));
            }
        }

        var totalImported = 0;
        if (!request.DryRun && validUsers.Count > 0)
        {
            var now = clock.GetUtcNow();
            var usersToCreate = validUsers.Select(v =>
            {
                var user = UserAccount.CreateTenantUser(
                    tenantId,
                    v.row.EmployeeCode.Trim().ToUpperInvariant(),
                    v.row.Email.Trim(),
                    v.row.FullName.Trim(),
                    v.passwordHash,
                    now,
                    v.deptId);
                if (v.isPendingActivation)
                {
                    user.RequirePasswordChange();
                }
                var audit = AuditLog.UserCreated(tenantId, context.UserId!.Value, user.Id, user.EmployeeCode, user.Email, now);
                return (user, v.roleIds, audit);
            }).ToList();

            totalImported = await store.BatchCreateUsersAsync(usersToCreate, cancellationToken);
        }

        return new BatchImportUsersResult(
            request.Users.Count,
            validUsers.Count,
            totalImported,
            request.DryRun,
            items
        );
    }
}
