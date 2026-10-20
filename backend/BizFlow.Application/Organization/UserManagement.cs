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

public sealed record UserOperationResult(
    Guid UserId,
    string EmployeeCode,
    string Email,
    string FullName,
    Guid? DepartmentId,
    string Status,
    IReadOnlyList<Guid> RoleIds,
    DateTimeOffset CreatedAt);

public interface IUserManagementStore
{
    Task<bool> EmployeeCodeExistsAsync(Guid tenantId, string employeeCode, CancellationToken cancellationToken);
    Task<bool> EmailExistsAsync(Guid tenantId, string email, CancellationToken cancellationToken);
    Task<bool> DepartmentExistsAsync(Guid tenantId, Guid departmentId, CancellationToken cancellationToken);
    Task<UserOperationResult> CreateUserAsync(UserAccount user, IReadOnlyList<Guid> roleIds, AuditLog audit, CancellationToken cancellationToken);
    Task<UserOperationResult> UpdateUserAsync(Guid tenantId, Guid userId, string fullName, Guid? departmentId, IReadOnlyList<Guid>? roleIds, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
    Task<UserOperationResult> UpdateStatusAsync(Guid tenantId, Guid userId, UserStatus newStatus, Guid actorId, DateTimeOffset now, CancellationToken cancellationToken);
}

public sealed class UserManagement(
    ITenantContext context,
    IResourceAuthorizer authorizer,
    ICredentialVerifier credentialVerifier,
    IUserManagementStore store,
    TimeProvider clock)
{
    public const string ManagePermission = "roles.configure";

    private async Task<Guid> AuthorizeAsync(CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync(ManagePermission, new ResourceScope(context.TenantId), cancellationToken: cancellationToken);
        return context.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "User management requires a tenant workspace.");
    }

    public async Task<UserOperationResult> CreateAsync(CreateUserRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(cancellationToken);

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
        var tenantId = await AuthorizeAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(request.FullName) || request.FullName.Trim().Length > 200)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Full name is required and must not exceed 200 characters.");

        if (request.DepartmentId is { } deptId && !await store.DepartmentExistsAsync(tenantId, deptId, cancellationToken))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "The specified department does not exist.");

        var now = clock.GetUtcNow();
        return await store.UpdateUserAsync(tenantId, userId, request.FullName.Trim(), request.DepartmentId, request.RoleIds, context.UserId!.Value, now, cancellationToken);
    }

    public async Task<UserOperationResult> UpdateStatusAsync(Guid userId, UpdateUserStatusRequest request, CancellationToken cancellationToken)
    {
        var tenantId = await AuthorizeAsync(cancellationToken);

        if (context.UserId == userId)
            throw new ApplicationFault(FaultKind.Conflict, "USER.CANNOT_CHANGE_OWN_STATUS", "You cannot modify the status of your own account.");

        if (!Enum.TryParse<UserStatus>(request.Status, true, out var newStatus))
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Valid status values are: ACTIVE, INACTIVE, LOCKED.");

        var now = clock.GetUtcNow();
        return await store.UpdateStatusAsync(tenantId, userId, newStatus, context.UserId!.Value, now, cancellationToken);
    }
}
