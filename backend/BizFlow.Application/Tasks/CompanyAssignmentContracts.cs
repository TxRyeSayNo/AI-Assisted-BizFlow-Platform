namespace BizFlow.Application.Tasks;

public sealed record IndependentTaskRecipientInput(
    Guid UserId,
    string? Responsibility = null,
    string? Deadline = null,
    bool IsPrimary = false,
    decimal? Weight = null);

public sealed record AssignIndependentTaskCommand(
    string Title,
    string? Description = null,
    string? Priority = null,
    string? Deadline = null,
    string[]? Checklist = null,
    IReadOnlyList<IndependentTaskRecipientInput>? Recipients = null,
    Guid? ParentTaskId = null,
    Guid? RequestId = null);

public sealed record ProjectDepartmentPackageInput(
    Guid DepartmentId,
    Guid ManagerId,
    string PackageName,
    string? ScopeDescription = null,
    string? Deadline = null,
    decimal? Weight = null);

public sealed record AssignProjectCommand(
    string Title,
    string? Description = null,
    string? Priority = null,
    string? Deadline = null,
    string[]? Checklist = null,
    IReadOnlyList<ProjectDepartmentPackageInput>? DepartmentPackages = null);

public sealed record AssignableManagerDto(
    Guid UserId,
    string FullName,
    string EmployeeCode,
    string RoleTitle);

public sealed record AssignableEmployeeDto(
    Guid UserId,
    string FullName,
    string EmployeeCode,
    Guid? DepartmentId,
    string? DepartmentName,
    string RoleTitle);

public sealed record AssignableDepartmentDto(
    Guid DepartmentId,
    string Name,
    string Code,
    IReadOnlyList<AssignableManagerDto> Managers,
    IReadOnlyList<AssignableEmployeeDto> Employees);

public sealed record AssignableRecipientsView(
    IReadOnlyList<AssignableDepartmentDto> Departments,
    IReadOnlyList<AssignableEmployeeDto> AllEmployees);

public sealed record CompanyAssignmentResultView(
    Guid TaskId,
    string TaskKind,
    string Title,
    string Status,
    int AssignedCount,
    DateTimeOffset CreatedAt,
    string Message);

public interface ICompanyAssignmentService
{
    Task<AssignableRecipientsView> GetAssignableRecipientsAsync(CancellationToken cancellationToken);
    Task<CompanyAssignmentResultView> AssignIndependentTaskAsync(AssignIndependentTaskCommand command, CancellationToken cancellationToken);
    Task<CompanyAssignmentResultView> AssignProjectAsync(AssignProjectCommand command, CancellationToken cancellationToken);
}
