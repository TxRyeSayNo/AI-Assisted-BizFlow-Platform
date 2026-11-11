using BizFlow.Application.Common;
using BizFlow.Application.Organization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

[ApiController, Authorize, Route("api/v1/users")]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class UsersController(
    UserDirectoryQuery directory,
    UserManagement management) : ControllerBase
{
    [HttpGet]
    public Task<UserDirectoryPage> List([FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null, [FromQuery] string? status = null, [FromQuery] Guid? departmentId = null,
        CancellationToken cancellationToken = default) =>
        directory.ListAsync(new(page, pageSize, search, status, departmentId), cancellationToken);

    [HttpPost]
    public Task<UserOperationResult> Create([FromBody] CreateUserRequest request, CancellationToken cancellationToken) =>
        management.CreateAsync(request, cancellationToken);

    [HttpPost("batch-import")]
    public Task<BatchImportUsersResult> BatchImport([FromBody] BatchImportUsersRequest request, CancellationToken cancellationToken) =>
        management.BatchImportAsync(request, cancellationToken);

    [HttpGet("import-template")]
    public IActionResult DownloadImportTemplate([FromQuery] string format = "xlsx")
    {
        if (string.Equals(format, "csv", StringComparison.OrdinalIgnoreCase))
        {
            var csvBytes = EmployeeFileProcessing.GenerateCsvTemplate();
            return File(csvBytes, "text/csv; charset=utf-8", "bizflow_employee_template.csv");
        }

        var xlsxBytes = EmployeeFileProcessing.GenerateXlsxTemplate();
        return File(xlsxBytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "bizflow_employee_template.xlsx");
    }

    [HttpPost("upload-import")]
    public async Task<BatchImportUsersResult> UploadImport(
        [FromForm] IFormFile file,
        [FromForm] bool dryRun = false,
        CancellationToken cancellationToken = default)
    {
        if (file == null || file.Length == 0)
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.EMPTY_FILE", "Vui lòng chọn file Excel (.xlsx) hoặc CSV để import.");

        using var stream = file.OpenReadStream();
        var rows = EmployeeFileProcessing.ParseFile(stream, file.FileName);
        return await management.BatchImportAsync(new(rows, dryRun), cancellationToken);
    }

    [HttpPost("import-errors")]
    public IActionResult DownloadErrorReport([FromBody] IReadOnlyList<BatchImportValidationItem> items)
    {
        var csvBytes = EmployeeFileProcessing.GenerateErrorReportCsv(items ?? Array.Empty<BatchImportValidationItem>());
        return File(csvBytes, "text/csv; charset=utf-8", "bizflow_import_errors.csv");
    }

    [HttpPut("{id:guid}")]
    public Task<UserOperationResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken cancellationToken) =>
        management.UpdateAsync(id, request, cancellationToken);

    [HttpPatch("{id:guid}/status")]
    public Task<UserOperationResult> UpdateStatus(Guid id, [FromBody] UpdateUserStatusRequest request, CancellationToken cancellationToken) =>
        management.UpdateStatusAsync(id, request, cancellationToken);

    [HttpGet("{id:guid}/responsibilities")]
    public Task<UserResponsibilitiesView> GetResponsibilities(Guid id, CancellationToken cancellationToken) =>
        management.GetResponsibilitiesAsync(id, cancellationToken);

    [HttpPost("{id:guid}/promote-manager")]
    public Task<UserOperationResult> PromoteToManager(Guid id, CancellationToken cancellationToken) =>
        management.PromoteToManagerAsync(id, cancellationToken);

    [HttpPost("{id:guid}/demote-employee")]
    public Task<UserOperationResult> DemoteToEmployee(Guid id, [FromBody] DemoteUserRequest request, CancellationToken cancellationToken) =>
        management.DemoteToEmployeeAsync(id, request, cancellationToken);

    [HttpPost("{id:guid}/transfer-ownership")]
    public Task<UserOperationResult> TransferOwnership(Guid id, [FromBody] TransferOwnershipRequest? request, CancellationToken cancellationToken) =>
        management.TransferAdminOwnershipAsync(request is null ? new(id) : request with { TargetUserId = id }, cancellationToken);
}
