using System.Globalization;
using BizFlow.Application.Common;
using BizFlow.Application.Notifications;
using BizFlow.Application.Security;
using BizFlow.Application.Tasks;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Security;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Workflows;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.Tasks;

public sealed class CompanyAssignmentService(
    BizFlowDbContext db,
    ITenantContext context,
    IResourceAuthorizer authorizer,
    TimeProvider clock,
    INotificationUpdates updates,
    ILogger<CompanyAssignmentService> logger) : ICompanyAssignmentService
{
    private async Task<(Guid TenantId, Guid ActorId)> RequireTaskCreatorAsync(CancellationToken cancellationToken)
    {
        await authorizer.AuthorizeAsync("tasks.create", new(context.TenantId), cancellationToken: cancellationToken);
        var tenantId = context.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "A tenant workspace is required.");
        var actorId = context.UserId ?? throw new ApplicationFault(FaultKind.Forbidden, "ACCESS.DENIED", "Authentication required.");
        return (tenantId, actorId);
    }

    public async Task<AssignableRecipientsView> GetAssignableRecipientsAsync(CancellationToken cancellationToken)
    {
        var (tenantId, actorId) = await RequireTaskCreatorAsync(cancellationToken);

        var activeDepts = await db.Departments
            .Where(d => d.TenantId == tenantId && d.Status == RecordStatus.Active)
            .OrderBy(d => d.Name)
            .Select(d => new { d.Id, d.Name, d.Code })
            .ToListAsync(cancellationToken);

        var activeUsers = await db.Users
            .Where(u => u.TenantId == tenantId && u.DeletedAt == null && u.Status == UserStatus.Active)
            .OrderBy(u => u.FullName)
            .Select(u => new
            {
                u.Id,
                u.FullName,
                u.EmployeeCode,
                u.DepartmentId,
                IsManager = db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && (r.Name == "MANAGER" || r.Name == "COMPANY_ADMIN"))) ||
                            db.ManagementScopes.Any(ms => ms.TenantId == tenantId && ms.UserId == u.Id),
                RoleTitle = db.UserRoles.Where(ur => ur.UserId == u.Id)
                    .SelectMany(ur => db.Roles.Where(r => r.Id == ur.RoleId).Select(r => r.Name))
                    .FirstOrDefault() ?? "EMPLOYEE"
            })
            .ToListAsync(cancellationToken);

        var allEmployees = activeUsers.Select(u => new AssignableEmployeeDto(
            u.Id,
            u.FullName,
            u.EmployeeCode,
            u.DepartmentId,
            activeDepts.FirstOrDefault(d => d.Id == u.DepartmentId)?.Name,
            u.IsManager ? (u.RoleTitle == "COMPANY_ADMIN" ? "Quản trị viên" : "Trưởng phòng") : "Thành viên"
        )).ToList();

        var fallbackManagers = activeUsers
            .Where(u => u.IsManager || u.Id == actorId)
            .OrderByDescending(u => u.Id == actorId)
            .Select(u => new AssignableManagerDto(
                u.Id,
                u.FullName,
                u.EmployeeCode,
                u.RoleTitle == "COMPANY_ADMIN" ? "Quản trị viên" : (u.IsManager ? "Trưởng phòng" : "Người điều phối")
            ))
            .ToList();

        var deptDtos = new List<AssignableDepartmentDto>();
        foreach (var d in activeDepts)
        {
            var managers = activeUsers
                .Where(u => u.IsManager && (u.DepartmentId == d.Id ||
                    db.ManagementScopes.Any(ms => ms.TenantId == tenantId && ms.UserId == u.Id && ms.DepartmentId == d.Id)))
                .Select(u => new AssignableManagerDto(
                    u.Id,
                    u.FullName,
                    u.EmployeeCode,
                    u.RoleTitle == "COMPANY_ADMIN" ? "Quản trị viên" : "Trưởng phòng"
                )).ToList();

            if (managers.Count == 0)
            {
                managers = fallbackManagers;
            }

            var employees = activeUsers
                .Where(u => u.DepartmentId == d.Id)
                .Select(u => new AssignableEmployeeDto(
                    u.Id,
                    u.FullName,
                    u.EmployeeCode,
                    d.Id,
                    d.Name,
                    u.IsManager ? "Trưởng phòng" : "Thành viên"
                )).ToList();

            deptDtos.Add(new AssignableDepartmentDto(d.Id, d.Name, d.Code, managers, employees));
        }

        return new AssignableRecipientsView(deptDtos, allEmployees);
    }

    private static string[] NormalizeChecklist(string[]? rawChecklist) =>
        (rawChecklist ?? [])
            .Where(item => !string.IsNullOrWhiteSpace(item))
            .Select(item => item.Trim())
            .Where(item => item.Length <= 300)
            .ToArray();

    public async Task<CompanyAssignmentResultView> AssignIndependentTaskAsync(AssignIndependentTaskCommand command, CancellationToken cancellationToken)
    {
        var (tenantId, actorId) = await RequireTaskCreatorAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(command.Title))
            throw new ApplicationFault(FaultKind.Validation, "TASK.INVALID_TITLE", "Tiêu đề nhiệm vụ không được để trống.");

        if (command.Recipients is null || command.Recipients.Count == 0)
            throw new ApplicationFault(FaultKind.Validation, "TASK.RECIPIENT_REQUIRED", "Cần chọn ít nhất một nhân viên để phân công nhiệm vụ.");

        var recipientUserIds = command.Recipients.Select(r => r.UserId).Distinct().ToList();
        var recipientUsers = await db.Users
            .Where(u => u.TenantId == tenantId && recipientUserIds.Contains(u.Id) && u.DeletedAt == null && u.Status == UserStatus.Active)
            .ToListAsync(cancellationToken);

        if (recipientUsers.Count != recipientUserIds.Count)
            throw new ApplicationFault(FaultKind.Validation, "TASK.INVALID_RECIPIENT", "Một số nhân viên được chọn không hợp lệ hoặc đã bị vô hiệu hóa.");

        WorkTask? parentTask = null;
        if (command.ParentTaskId is { } parentTaskId)
        {
            parentTask = await db.WorkTasks
                .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == parentTaskId && t.DeletedAt == null, cancellationToken);
            if (parentTask is null || parentTask.Status is TaskState.Completed or TaskState.Cancelled)
                throw new ApplicationFault(FaultKind.Validation, "TASK.INVALID_PARENT", "Nhiệm vụ cha không tồn tại hoặc đã kết thúc.");
        }

        var now = clock.GetUtcNow();
        var priorityCode = command.Priority?.Trim() ?? "MEDIUM";
        var priority = Enum.GetValues<TaskPriority>().FirstOrDefault(p => TaskListCodes.Priority(p) == priorityCode);
        var rootDeadline = TaskCreationRules.ParseDeadline(command.Deadline);
        var checklistTitles = NormalizeChecklist(command.Checklist);
        var equalWeight = Math.Round(100m / command.Recipients.Count, 1, MidpointRounding.AwayFromZero);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var notifications = new List<Notification>();

            // Case 1: Subtask / Work Package WBS Decomposition under a Parent Task (S2.1 & S2.3)
            if (parentTask is not null)
            {
                foreach (var rec in command.Recipients)
                {
                    var user = recipientUsers.Single(u => u.Id == rec.UserId);
                    var recWeight = rec.Weight is > 0 ? Math.Round(rec.Weight.Value, 1, MidpointRounding.AwayFromZero) : equalWeight;
                    var childDeadline = TaskCreationRules.ParseDeadline(rec.Deadline) ?? rootDeadline ?? parentTask.Deadline;

                    string childTitle;
                    string? childDesc;
                    if (command.Recipients.Count == 1)
                    {
                        childTitle = command.Title.Trim();
                        childDesc = !string.IsNullOrWhiteSpace(rec.Responsibility) && !string.IsNullOrWhiteSpace(command.Description)
                            ? $"{command.Description.Trim()}\n\nTrách nhiệm cụ thể: {rec.Responsibility.Trim()}"
                            : (!string.IsNullOrWhiteSpace(rec.Responsibility) ? rec.Responsibility.Trim() : command.Description);
                    }
                    else
                    {
                        var suffix = !string.IsNullOrWhiteSpace(rec.Responsibility)
                            ? $"{user.FullName} ({rec.Responsibility.Trim()})"
                            : user.FullName;
                        childTitle = $"{command.Title.Trim()} — {suffix}";
                        if (childTitle.Length > 300) childTitle = childTitle[..300];
                        childDesc = !string.IsNullOrWhiteSpace(rec.Responsibility) ? rec.Responsibility.Trim() : command.Description;
                    }

                    var childTask = WorkTask.CreateAssigned(
                        tenantId, actorId, childTitle, now, childDesc, priority, childDeadline,
                        command.RequestId ?? parentTask.RequestId, parentTask.Id, "SUBTASK", recWeight);
                    db.WorkTasks.Add(childTask);

                    for (var i = 0; i < checklistTitles.Length; i++)
                    {
                        db.TaskChecklistItems.Add(TaskChecklistItem.Create(childTask.Id, checklistTitles[i], i + 1));
                    }

                    var childAssignment = TaskAssignment.Create(childTask.Id, actorId, user.DepartmentId, user.Id, now);
                    db.TaskAssignments.Add(childAssignment);

                    var notif = Notification.Create(tenantId, user.Id, NotificationEvent.TaskAssigned,
                        "Phân công nhiệm vụ con", childTitle, $"task-assigned/{childAssignment.Id:N}/{user.Id:N}", "Task", childTask.Id);
                    notifications.Add(notif);
                    db.Notifications.Add(notif);
                }

                parentTask.SyncParentRollupState(allChildrenCompleted: false, now);

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                await updates.PublishAsync(tenantId, notifications.Select(n => n.RecipientId).Distinct().ToArray(), cancellationToken);

                return new CompanyAssignmentResultView(parentTask.Id, "SUBTASK", parentTask.Title,
                    TaskListCodes.State(parentTask.Status), command.Recipients.Count, now,
                    $"Đã phân rã và giao {command.Recipients.Count} nhiệm vụ con thành công!");
            }

            // Case 2: Single-recipient independent task
            if (command.Recipients.Count == 1)
            {
                var single = command.Recipients[0];
                var user = recipientUsers.Single(u => u.Id == single.UserId);
                var deadline = TaskCreationRules.ParseDeadline(single.Deadline) ?? rootDeadline;
                var singleDesc = !string.IsNullOrWhiteSpace(single.Responsibility) && !string.IsNullOrWhiteSpace(command.Description)
                    ? $"{command.Description.Trim()}\n\nTrách nhiệm cụ thể: {single.Responsibility.Trim()}"
                    : (!string.IsNullOrWhiteSpace(single.Responsibility) ? single.Responsibility.Trim() : command.Description);

                var task = WorkTask.CreateAssigned(tenantId, actorId, command.Title.Trim(), now, singleDesc, priority, deadline, command.RequestId, null, "INDEPENDENT", 100m);
                db.WorkTasks.Add(task);

                for (var i = 0; i < checklistTitles.Length; i++)
                {
                    db.TaskChecklistItems.Add(TaskChecklistItem.Create(task.Id, checklistTitles[i], i + 1));
                }

                var assignment = TaskAssignment.Create(task.Id, actorId, user.DepartmentId, user.Id, now);
                db.TaskAssignments.Add(assignment);

                var notif = Notification.Create(tenantId, user.Id, NotificationEvent.TaskAssigned,
                    "Phân công nhiệm vụ độc lập", task.Title, $"task-assigned/{assignment.Id:N}/{user.Id:N}", "Task", task.Id);
                notifications.Add(notif);
                db.Notifications.Add(notif);

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                await updates.PublishAsync(tenantId, notifications.Select(n => n.RecipientId).Distinct().ToArray(), cancellationToken);

                return new CompanyAssignmentResultView(task.Id, "INDEPENDENT", task.Title, "ASSIGNED", 1, task.CreatedAt,
                    "Đã phân công nhiệm vụ độc lập thành công!");
            }
            else
            {
                // Case 3: Multi-recipient independent task (S1.2 & S1.4):
                // 1. Root task is an active coordination container (no duplicate personal TaskAssignment on rootTask).
                var primaryRecipient = command.Recipients.FirstOrDefault(r => r.IsPrimary) ?? command.Recipients[0];
                var primaryUserId = primaryRecipient.UserId;

                var rootTask = WorkTask.CreateCoordinating(tenantId, actorId, command.Title.Trim(), now, command.Description, priority, rootDeadline, command.RequestId, null, "INDEPENDENT", 100m);
                db.WorkTasks.Add(rootTask);

                for (var i = 0; i < checklistTitles.Length; i++)
                {
                    db.TaskChecklistItems.Add(TaskChecklistItem.Create(rootTask.Id, checklistTitles[i], i + 1));
                }

                // 2. Individual deliverable child task for each selected employee with custom weight %
                foreach (var rec in command.Recipients)
                {
                    var user = recipientUsers.Single(u => u.Id == rec.UserId);
                    var isPrimary = rec.UserId == primaryUserId;
                    var rolePrefix = isPrimary ? "[Phụ trách chính] " : "";
                    var childTitle = $"{rolePrefix}{command.Title.Trim()} — {user.FullName}";
                    if (childTitle.Length > 300) childTitle = childTitle[..300];
                    var childKind = isPrimary ? "PRIMARY_SUBTASK" : "SUBTASK";
                    var childDesc = string.IsNullOrWhiteSpace(rec.Responsibility) ? command.Description : rec.Responsibility.Trim();
                    var childDeadline = TaskCreationRules.ParseDeadline(rec.Deadline) ?? rootDeadline;
                    var memberWeight = rec.Weight is > 0 ? Math.Round(rec.Weight.Value, 1, MidpointRounding.AwayFromZero) : equalWeight;

                    var childTask = WorkTask.CreateAssigned(tenantId, actorId, childTitle, now, childDesc, priority, childDeadline, command.RequestId, rootTask.Id, childKind, memberWeight);
                    db.WorkTasks.Add(childTask);

                    var childAssignment = TaskAssignment.Create(childTask.Id, actorId, user.DepartmentId, user.Id, now);
                    db.TaskAssignments.Add(childAssignment);

                    var notif = Notification.Create(tenantId, user.Id, NotificationEvent.TaskAssigned,
                        isPrimary ? "Phân công nhiệm vụ độc lập (Phụ trách chính)" : "Phân công nhiệm vụ độc lập",
                        childTitle, $"task-assigned/{childAssignment.Id:N}/{user.Id:N}", "Task", childTask.Id);
                    notifications.Add(notif);
                    db.Notifications.Add(notif);
                }

                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                await updates.PublishAsync(tenantId, notifications.Select(n => n.RecipientId).Distinct().ToArray(), cancellationToken);

                return new CompanyAssignmentResultView(rootTask.Id, "INDEPENDENT", rootTask.Title, "IN_PROGRESS", command.Recipients.Count, rootTask.CreatedAt,
                    $"Đã phân công nhiệm vụ độc lập thành công cho {command.Recipients.Count} nhân sự!");
            }
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, "Failed to assign independent task: {Title}", command.Title);
            throw;
        }
    }

    public async Task<CompanyAssignmentResultView> AssignProjectAsync(AssignProjectCommand command, CancellationToken cancellationToken)
    {
        var (tenantId, actorId) = await RequireTaskCreatorAsync(cancellationToken);

        if (string.IsNullOrWhiteSpace(command.Title))
            throw new ApplicationFault(FaultKind.Validation, "TASK.INVALID_TITLE", "Tiêu đề dự án không được để trống.");

        if (command.DepartmentPackages is null || command.DepartmentPackages.Count == 0)
            throw new ApplicationFault(FaultKind.Validation, "TASK.DEPARTMENTS_REQUIRED", "Cần chọn ít nhất một phòng ban để khởi tạo dự án.");

        var deptIds = command.DepartmentPackages.Select(p => p.DepartmentId).Distinct().ToList();
        var depts = await db.Departments
            .Where(d => d.TenantId == tenantId && deptIds.Contains(d.Id) && d.Status == RecordStatus.Active)
            .ToListAsync(cancellationToken);

        if (depts.Count != deptIds.Count)
            throw new ApplicationFault(FaultKind.Validation, "TASK.INVALID_DEPARTMENT", "Một số phòng ban được chọn không hợp lệ hoặc không hoạt động.");

        var managerIds = command.DepartmentPackages.Select(p => p.ManagerId).Distinct().ToList();
        var managers = await db.Users
            .Where(u => u.TenantId == tenantId && managerIds.Contains(u.Id) && u.DeletedAt == null && u.Status == UserStatus.Active)
            .ToListAsync(cancellationToken);

        if (managers.Count != managerIds.Count)
            throw new ApplicationFault(FaultKind.Validation, "TASK.INVALID_MANAGER", "Một số Trưởng phòng được chọn không hợp lệ.");

        var now = clock.GetUtcNow();
        var priorityCode = command.Priority?.Trim() ?? "HIGH";
        var priority = Enum.GetValues<TaskPriority>().FirstOrDefault(p => TaskListCodes.Priority(p) == priorityCode);
        var projectDeadline = TaskCreationRules.ParseDeadline(command.Deadline);
        var checklistTitles = NormalizeChecklist(command.Checklist);

        // Normalize weights
        var packageCount = command.DepartmentPackages.Count;
        var equalWeight = Math.Round(100m / packageCount, 1, MidpointRounding.AwayFromZero);

        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            // 1. Root Project Task (S2.2: Creator is Project Sponsor, not self-assigned as an employee executor)
            var rootProject = WorkTask.CreateCoordinating(tenantId, actorId, command.Title.Trim(), now, command.Description, priority, projectDeadline, null, null, "PROJECT", 100m);
            db.WorkTasks.Add(rootProject);

            for (var i = 0; i < checklistTitles.Length; i++)
            {
                db.TaskChecklistItems.Add(TaskChecklistItem.Create(rootProject.Id, checklistTitles[i], i + 1));
            }

            var notifications = new List<Notification>();

            // 2. Department Work Packages
            foreach (var pkg in command.DepartmentPackages)
            {
                var dept = depts.Single(d => d.Id == pkg.DepartmentId);
                var mgr = managers.Single(m => m.Id == pkg.ManagerId);
                var pkgTitle = string.IsNullOrWhiteSpace(pkg.PackageName)
                    ? $"[Gói việc] {command.Title.Trim()} — {dept.Name}"
                    : pkg.PackageName.Trim();
                if (pkgTitle.Length > 300) pkgTitle = pkgTitle[..300];
                var pkgDesc = string.IsNullOrWhiteSpace(pkg.ScopeDescription) ? command.Description : pkg.ScopeDescription.Trim();
                var pkgDeadline = TaskCreationRules.ParseDeadline(pkg.Deadline) ?? projectDeadline;
                var pkgWeight = pkg.Weight is > 0 ? Math.Round(pkg.Weight.Value, 1, MidpointRounding.AwayFromZero) : equalWeight;

                var pkgTask = WorkTask.CreateAssigned(tenantId, actorId, pkgTitle, now, pkgDesc, priority, pkgDeadline, null, rootProject.Id, "WORK_PACKAGE", pkgWeight);
                db.WorkTasks.Add(pkgTask);

                var pkgAssignment = TaskAssignment.Create(pkgTask.Id, actorId, dept.Id, mgr.Id, now);
                db.TaskAssignments.Add(pkgAssignment);

                // Notify ONLY the accountable Manager
                var notif = Notification.Create(tenantId, mgr.Id, NotificationEvent.TaskAssigned,
                    "Phân công gói việc dự án", $"Dự án: {command.Title.Trim()} — {pkgTitle}",
                    $"task-assigned/{pkgAssignment.Id:N}/{mgr.Id:N}", "Task", pkgTask.Id);
                notifications.Add(notif);
                db.Notifications.Add(notif);
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            await updates.PublishAsync(tenantId, notifications.Select(n => n.RecipientId).Distinct().ToArray(), cancellationToken);

            return new CompanyAssignmentResultView(rootProject.Id, "PROJECT", rootProject.Title, "IN_PROGRESS", packageCount, rootProject.CreatedAt,
                $"Đã khởi tạo và phân công dự án liên phòng ban thành công cho {packageCount} phòng ban!");
        }
        catch (Exception ex)
        {
            await transaction.RollbackAsync(cancellationToken);
            logger.LogError(ex, "Failed to assign project: {Title}", command.Title);
            throw;
        }
    }
}
