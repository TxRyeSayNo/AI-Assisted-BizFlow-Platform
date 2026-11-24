using BizFlow.Application.Tasks;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.Tasks;

internal static class TaskReadQueries
{
    internal static IQueryable<WorkTask> Visible(BizFlowDbContext db, TaskReadScope scope)
    {
        var managed = scope.ManagedDepartmentIds.ToArray();
        return db.WorkTasks.AsNoTracking().Where(t => t.TenantId == scope.TenantId &&
            (scope.Tenant || (scope.Own && t.CreatorId == scope.UserId) ||
             db.TaskAssignments.Any(a => a.TaskId == t.Id && a.EndedAt == null &&
                ((scope.Assigned && a.UserId == scope.UserId) ||
                 (scope.Managed && ((a.DepartmentId != null && managed.Contains(a.DepartmentId.Value)) ||
                    (a.DepartmentId == null && db.Users.Any(u => u.TenantId == scope.TenantId && u.Id == a.UserId &&
                        u.DepartmentId != null && managed.Contains(u.DepartmentId.Value)))))))));
    }
}

public sealed class TaskDetailReader(BizFlowDbContext db) : ITaskDetailReader
{
    public async Task<TaskDetailView?> GetAsync(TaskReadScope scope, Guid id, TaskDetailPages pages, CancellationToken cancellationToken)
    {
        // One consistent database snapshot prevents mixed assignment/history pages during concurrent changes.
        await using var transaction = await db.Database.BeginTransactionAsync(System.Data.IsolationLevel.RepeatableRead, cancellationToken);
        var task = await TaskReadQueries.Visible(db, scope).SingleOrDefaultAsync(t => t.Id == id, cancellationToken)
            ?? await db.WorkTasks.AsNoTracking().SingleOrDefaultAsync(t => t.TenantId == scope.TenantId && t.Id == id && t.DeletedAt == null &&
                TaskReadQueries.Visible(db, scope).Any(child => child.ParentTaskId == id), cancellationToken);
        if (task is null) return null;
        var creator = await db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == task.CreatorId)
            .Select(u => u.FullName).SingleOrDefaultAsync(cancellationToken);
        var assignment = await db.TaskAssignments.AsNoTracking().SingleOrDefaultAsync(a => a.TaskId == id && a.EndedAt == null, cancellationToken);
        string? userName = null, departmentName = null;
        Guid? assignedUserDepartmentId = null;
        bool isAssignedUserAnActualManager = false;
        if (assignment?.UserId is { } userId)
        {
            var userRow = await db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == userId)
                .Select(u => new
                {
                    u.FullName,
                    u.DepartmentId,
                    DeptName = db.Departments.Where(d => d.TenantId == scope.TenantId && d.Id == u.DepartmentId).Select(d => d.Name).FirstOrDefault(),
                    IsManager = db.UserRoles.Any(ur => ur.UserId == u.Id && db.Roles.Any(r => r.Id == ur.RoleId && (r.Name == "MANAGER" || r.Name == "COMPANY_ADMIN"))) ||
                                db.ManagementScopes.Any(ms => ms.TenantId == scope.TenantId && ms.UserId == u.Id)
                }).SingleOrDefaultAsync(cancellationToken);
            if (userRow is not null)
            {
                userName = userRow.FullName;
                assignedUserDepartmentId = userRow.DepartmentId;
                departmentName = userRow.DeptName;
                isAssignedUserAnActualManager = userRow.IsManager;
            }
        }
        if (assignment?.DepartmentId is { } departmentId) departmentName = await db.Departments.Where(d => d.TenantId == scope.TenantId && d.Id == departmentId).Select(d => d.Name).SingleOrDefaultAsync(cancellationToken);
        var checklist = db.TaskChecklistItems.AsNoTracking().Where(c => c.TaskId == id).OrderBy(c => c.SortOrder).ThenBy(c => c.Id)
            .Select(c => new TaskChecklistView(c.Id, c.Title, c.SortOrder, c.IsCompleted));
        var reports = db.TaskProgressReports.AsNoTracking().Where(r => r.TaskId == id).OrderByDescending(r => r.SubmittedAt).ThenBy(r => r.Id)
            .Select(r => new TaskReportView(r.Id, r.AuthorId, r.Percent, r.Content, r.SubmittedAt));
        var results = db.TaskResults.AsNoTracking().Where(r => r.TaskId == id).OrderByDescending(r => r.RevisionNo).ThenBy(r => r.Id)
            .Select(r => new TaskResultView(r.Id, r.AuthorId, r.RevisionNo, r.Content, r.SubmittedAt));
        async Task<TaskEvidencePage<T>> Page<T>(IQueryable<T> query, int page) => new(
            await query.Skip((page - 1) * pages.PageSize).Take(pages.PageSize).ToArrayAsync(cancellationToken),
            page, pages.PageSize, await query.LongCountAsync(cancellationToken));

        string? parentTitle = null;
        if (task.ParentTaskId is { } pId)
        {
            parentTitle = await db.WorkTasks.Where(t => t.TenantId == scope.TenantId && t.Id == pId).Select(t => t.Title).SingleOrDefaultAsync(cancellationToken);
        }

        var subtaskCount = await db.WorkTasks.CountAsync(st => st.TenantId == scope.TenantId && st.ParentTaskId == id && st.DeletedAt == null && st.Status != TaskState.Cancelled, cancellationToken);
        var completedSubtaskCount = await db.WorkTasks.CountAsync(st => st.TenantId == scope.TenantId && st.ParentTaskId == id && (st.Status == TaskState.Completed || st.Status == TaskState.Confirmed) && st.DeletedAt == null, cancellationToken);

        var subtaskEntities = await db.WorkTasks.Where(st => st.TenantId == scope.TenantId && st.ParentTaskId == id && st.DeletedAt == null)
            .OrderBy(st => st.CreatedAt)
            .Select(st => new
            {
                st.Id, st.Title, st.Description, st.Status, st.Priority, st.Deadline, st.CreatedAt, st.UpdatedAt, st.CreatorId, st.RequestId, st.ParentTaskId, st.TaskKind, st.Weight,
                Assignment = db.TaskAssignments.Where(a => a.TaskId == st.Id && a.EndedAt == null)
                    .Select(a => new
                    {
                        a.UserId, a.DepartmentId,
                        UserName = db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == a.UserId).Select(u => u.FullName).FirstOrDefault(),
                        EmployeeCode = db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == a.UserId).Select(u => u.EmployeeCode).FirstOrDefault(),
                        UserDepartmentName = db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == a.UserId)
                            .SelectMany(u => db.Departments.Where(d => d.TenantId == scope.TenantId && d.Id == u.DepartmentId).Select(d => d.Name))
                            .FirstOrDefault(),
                        DepartmentName = db.Departments.Where(d => d.TenantId == scope.TenantId && d.Id == a.DepartmentId).Select(d => d.Name).FirstOrDefault()
                    }).FirstOrDefault(),
                LatestReport = db.TaskProgressReports.Where(r => r.TaskId == st.Id)
                    .OrderByDescending(r => r.SubmittedAt)
                    .Select(r => new { r.Percent, r.Content, r.SubmittedAt })
                    .FirstOrDefault(),
                LatestResult = db.TaskResults.Where(r => r.TaskId == st.Id)
                    .OrderByDescending(r => r.RevisionNo)
                    .Select(r => new { r.RevisionNo, r.Content, r.SubmittedAt })
                    .FirstOrDefault()
            }).ToListAsync(cancellationToken);

        var now = DateTimeOffset.UtcNow;
        var activeSubtasks = subtaskEntities.Where(st => st.Status != TaskState.Cancelled).ToList();
        var activeTotal = activeSubtasks.Count;
        var equalWeight = activeTotal > 0 ? Math.Round(100m / activeTotal, 1, MidpointRounding.AwayFromZero) : 0m;

        // Load nested Level-3 child tasks under department work packages
        var activeSubtaskIds = activeSubtasks.Select(s => s.Id).ToList();
        var childTaskEntities = await db.WorkTasks
            .Where(ct => ct.TenantId == scope.TenantId && ct.ParentTaskId != null && activeSubtaskIds.Contains(ct.ParentTaskId.Value) && ct.DeletedAt == null && ct.Status != TaskState.Cancelled)
            .OrderBy(ct => ct.CreatedAt)
            .Select(ct => new
            {
                ct.Id,
                ct.ParentTaskId,
                ct.Title,
                ct.Description,
                ct.Status,
                ct.Priority,
                ct.Deadline,
                ct.CreatedAt,
                ct.UpdatedAt,
                ct.TaskKind,
                ct.Weight,
                Assignment = db.TaskAssignments.Where(a => a.TaskId == ct.Id && a.EndedAt == null)
                    .Select(a => new
                    {
                        a.UserId,
                        a.DepartmentId,
                        UserName = db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == a.UserId).Select(u => u.FullName).FirstOrDefault(),
                        EmployeeCode = db.Users.Where(u => u.TenantId == scope.TenantId && u.Id == a.UserId).Select(u => u.EmployeeCode).FirstOrDefault(),
                        DepartmentName = db.Departments.Where(d => d.TenantId == scope.TenantId && d.Id == a.DepartmentId).Select(d => d.Name).FirstOrDefault()
                    }).FirstOrDefault(),
                LatestReport = db.TaskProgressReports.Where(r => r.TaskId == ct.Id)
                    .OrderByDescending(r => r.SubmittedAt)
                    .Select(r => new { r.Percent, r.Content, r.SubmittedAt })
                    .FirstOrDefault(),
                LatestResult = db.TaskResults.Where(r => r.TaskId == ct.Id)
                    .OrderByDescending(r => r.RevisionNo)
                    .Select(r => new { r.RevisionNo, r.Content, r.SubmittedAt })
                    .FirstOrDefault()
            }).ToListAsync(cancellationToken);

        var isParticipantInTask = activeSubtasks.Any(st => st.Assignment?.UserId == scope.UserId) ||
                                  childTaskEntities.Any(ct => ct.Assignment?.UserId == scope.UserId);
        var canSeeAllSiblingEvidence = scope.Tenant || scope.Managed || task.CreatorId == scope.UserId || assignment?.UserId == scope.UserId || isParticipantInTask;

        var contributions = activeSubtasks.Select(st =>
        {
            var stChildren = childTaskEntities.Where(ct => ct.ParentTaskId == st.Id).ToList();
            var equalChildWeight = stChildren.Count > 0 ? Math.Round(100m / stChildren.Count, 1, MidpointRounding.AwayFromZero) : 0m;
            var childContributions = stChildren.Select(ct =>
            {
                var ctDone = ct.Status is TaskState.Completed or TaskState.Confirmed;
                var ctOverdue = ct.Status == TaskState.Overdue || (!ctDone && ct.Deadline is not null && ct.Deadline < now);
                var ctProgress = ctDone
                    ? 100
                    : ct.LatestReport is not null
                        ? (int)ct.LatestReport.Percent
                        : ct.Status == TaskState.Submitted
                            ? 85
                            : (ct.Status is TaskState.InProgress or TaskState.Overdue)
                                ? 50
                                : ct.Status == TaskState.Accepted
                                    ? 15
                                    : 0;
                var ctWeight = ct.Weight is > 0 ? ct.Weight.Value : equalChildWeight;
                var ctContributed = ctDone ? ctWeight : Math.Round(ctProgress * ctWeight / 100m, 1, MidpointRounding.AwayFromZero);
                var ctCanView = canSeeAllSiblingEvidence || ct.Assignment?.UserId == scope.UserId;
                return new TaskWorkPackageContributionView(
                    ct.Id, ct.Title, ct.Description, TaskListCodes.State(ct.Status), TaskListCodes.Priority(ct.Priority),
                    ct.Deadline, ct.Assignment?.UserId, ct.Assignment?.UserName, ct.Assignment?.EmployeeCode,
                    ct.Assignment?.DepartmentId, ct.Assignment?.DepartmentName,
                    ctProgress, ctWeight, ctContributed, ctDone, ctOverdue,
                    ctCanView ? ct.LatestReport?.Percent : null,
                    ctCanView ? ct.LatestReport?.Content : null,
                    ctCanView ? ct.LatestReport?.SubmittedAt : null,
                    ctCanView ? ct.LatestResult?.RevisionNo : null,
                    ctCanView ? ct.LatestResult?.Content : null,
                    ctCanView ? ct.LatestResult?.SubmittedAt : null,
                    ct.UpdatedAt, ct.TaskKind, null);
            }).ToList();

            var isDone = stChildren.Count > 0
                ? stChildren.All(ct => ct.Status is TaskState.Completed or TaskState.Confirmed)
                : st.Status is TaskState.Completed or TaskState.Confirmed;
            var isOverdue = st.Status == TaskState.Overdue || (!isDone && st.Deadline is not null && st.Deadline < now);

            var totalChildWeight = childContributions.Sum(c => c.WeightPercent);
            var weightedChildProgress = stChildren.Count > 0
                ? (totalChildWeight > 0
                    ? childContributions.Sum(c => c.IndividualProgressPercent * c.WeightPercent) / totalChildWeight
                    : childContributions.Average(c => (decimal)c.IndividualProgressPercent))
                : 0m;

            var indivProgress = isDone
                ? 100
                : stChildren.Count > 0
                    ? (int)Math.Round(weightedChildProgress, MidpointRounding.AwayFromZero)
                    : st.LatestReport is not null
                        ? (int)st.LatestReport.Percent
                        : st.Status == TaskState.Submitted
                            ? 85
                            : (st.Status == TaskState.InProgress || st.Status == TaskState.Overdue)
                                ? 50
                                : st.Status == TaskState.Accepted
                                    ? 15
                                    : 0;

            var weight = st.Weight ?? equalWeight;
            var contributed = isDone ? weight : Math.Round(indivProgress * weight / 100m, 1, MidpointRounding.AwayFromZero);
            var canViewEvidence = canSeeAllSiblingEvidence || st.Assignment?.UserId == scope.UserId;

            return new TaskWorkPackageContributionView(
                st.Id,
                st.Title,
                st.Description,
                TaskListCodes.State(st.Status),
                TaskListCodes.Priority(st.Priority),
                st.Deadline,
                st.Assignment?.UserId,
                st.Assignment?.UserName,
                st.Assignment?.EmployeeCode,
                st.Assignment?.DepartmentId,
                st.Assignment?.DepartmentName ?? st.Assignment?.UserDepartmentName,
                indivProgress,
                weight,
                contributed,
                isDone,
                isOverdue,
                canViewEvidence ? st.LatestReport?.Percent : null,
                canViewEvidence ? st.LatestReport?.Content : null,
                canViewEvidence ? st.LatestReport?.SubmittedAt : null,
                canViewEvidence ? st.LatestResult?.RevisionNo : null,
                canViewEvidence ? st.LatestResult?.Content : null,
                canViewEvidence ? st.LatestResult?.SubmittedAt : null,
                st.UpdatedAt,
                st.TaskKind,
                childContributions.Count > 0 ? childContributions : null);
        }).ToArray();

        var effectiveDeptId = assignment?.DepartmentId ?? assignedUserDepartmentId;
        string? accountableManagerName = null;

        // 1. "ai phân việc gì thì người đó là quản lý điều phối"
        // Ưu tiên 1: Người trực tiếp phân công nhiệm vụ (AssignedBy)
        if (assignment?.AssignedBy is { } assignerId && assignerId != Guid.Empty)
        {
            accountableManagerName = await db.Users
                .Where(u => u.TenantId == scope.TenantId && u.Id == assignerId && u.DeletedAt == null)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Ưu tiên 2: Người tạo nhiệm vụ (CreatorId) nếu chưa có phân công
        if (string.IsNullOrWhiteSpace(accountableManagerName) && task.CreatorId != Guid.Empty)
        {
            accountableManagerName = await db.Users
                .Where(u => u.TenantId == scope.TenantId && u.Id == task.CreatorId && u.DeletedAt == null)
                .Select(u => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);
        }

        // Dự phòng 1: Quản lý phụ trách của nhiệm vụ cha (nếu có nhiệm vụ cha)
        if (string.IsNullOrWhiteSpace(accountableManagerName) && task.ParentTaskId is { } pTaskId)
        {
            var parentAssignerId = await db.TaskAssignments
                .Where(a => a.TaskId == pTaskId && a.EndedAt == null)
                .Select(a => (Guid?)a.AssignedBy)
                .FirstOrDefaultAsync(cancellationToken);
            if (parentAssignerId is { } pAssigner && pAssigner != Guid.Empty)
            {
                accountableManagerName = await db.Users
                    .Where(u => u.TenantId == scope.TenantId && u.Id == pAssigner && u.DeletedAt == null)
                    .Select(u => u.FullName)
                    .FirstOrDefaultAsync(cancellationToken);
            }
        }

        // Dự phòng 2: Lãnh đạo phòng ban phụ trách theo ManagementScopes hoặc Role MANAGER
        if (string.IsNullOrWhiteSpace(accountableManagerName) && effectiveDeptId is { } targetDeptId)
        {
            accountableManagerName = await db.ManagementScopes
                .Where(ms => ms.TenantId == scope.TenantId && ms.DepartmentId == targetDeptId)
                .Join(db.Users.Where(u => u.DeletedAt == null && u.Status == UserStatus.Active), ms => ms.UserId, u => u.Id, (ms, u) => u.FullName)
                .FirstOrDefaultAsync(cancellationToken);

            if (string.IsNullOrWhiteSpace(accountableManagerName))
            {
                accountableManagerName = await (from u in db.Users
                                                join ur in db.UserRoles on u.Id equals ur.UserId
                                                join r in db.Roles on ur.RoleId equals r.Id
                                                where u.TenantId == scope.TenantId
                                                  && u.DepartmentId == targetDeptId
                                                  && u.DeletedAt == null
                                                  && u.Status == UserStatus.Active
                                                  && r.Name == "MANAGER"
                                                select u.FullName).FirstOrDefaultAsync(cancellationToken);
            }
        }

        if (string.IsNullOrWhiteSpace(accountableManagerName) && isAssignedUserAnActualManager)
        {
            accountableManagerName = userName;
        }

        var selfLatestReport = await db.TaskProgressReports
            .Where(r => r.TaskId == id)
            .OrderByDescending(r => r.SubmittedAt)
            .Select(r => (short?)r.Percent)
            .FirstOrDefaultAsync(cancellationToken);

        var isProjectTask = task.TaskKind == "PROJECT" || contributions.Any(c => c.TaskKind == "WORK_PACKAGE");
        var isIndependentTask = task.TaskKind == "INDEPENDENT" || (activeTotal > 0 && !isProjectTask);
        var effectiveTaskKind = isProjectTask ? "PROJECT" : (isIndependentTask ? "INDEPENDENT" : task.TaskKind);

        if (string.IsNullOrWhiteSpace(accountableManagerName))
        {
            accountableManagerName = creator;
        }

        if (string.IsNullOrWhiteSpace(departmentName) && isIndependentTask && activeSubtasks.Count > 0)
        {
            var primaryChild = activeSubtasks.FirstOrDefault(st => st.TaskKind == "PRIMARY_SUBTASK") ?? activeSubtasks[0];
            departmentName = primaryChild.Assignment?.DepartmentName ?? primaryChild.Assignment?.UserDepartmentName;
            if (string.IsNullOrWhiteSpace(userName) && primaryChild.Assignment?.UserName is { } leadName)
            {
                userName = $"{leadName} (Phụ trách chính)";
            }
        }

        int overallPercent;
        if (contributions.Length > 0)
        {
            var totalWeight = contributions.Sum(c => c.WeightPercent);
            var sumWeighted = totalWeight > 0
                ? contributions.Sum(c => c.IndividualProgressPercent * c.WeightPercent) / totalWeight
                : contributions.Average(c => (decimal)c.IndividualProgressPercent);
            overallPercent = (int)Math.Round(sumWeighted, MidpointRounding.AwayFromZero);
            if (contributions.All(c => c.IsCompleted)) overallPercent = 100;
        }
        else
        {
            overallPercent = (task.Status is TaskState.Completed or TaskState.Confirmed)
                ? 100
                : selfLatestReport is not null
                    ? (int)selfLatestReport.Value
                    : task.Status == TaskState.Submitted
                        ? 85
                        : (task.Status is TaskState.InProgress or TaskState.Overdue)
                            ? 50
                            : task.Status == TaskState.Accepted
                                ? 15
                                : 0;
        }

        var participatingEmployees = contributions
            .SelectMany(c => (c.ChildContributions ?? (IReadOnlyList<TaskWorkPackageContributionView>)Array.Empty<TaskWorkPackageContributionView>()).Concat(new[] { c }))
            .Where(c => c.AssignedUserId is not null)
            .Select(c => c.AssignedUserId!.Value)
            .Distinct()
            .Count();

        var participatingDepartmentsCount = contributions
            .SelectMany(c => (c.ChildContributions ?? (IReadOnlyList<TaskWorkPackageContributionView>)Array.Empty<TaskWorkPackageContributionView>()).Concat(new[] { c }))
            .Where(c => c.AssignedDepartmentId is not null)
            .Select(c => c.AssignedDepartmentId!.Value)
            .Distinct()
            .Count();

        var accountableManagersCount = contributions
            .Where(c => c.TaskKind == "WORK_PACKAGE" && c.AssignedUserId is not null)
            .Select(c => c.AssignedUserId!.Value)
            .Distinct()
            .Count();

        var inProgressPackages = contributions.Count(c => !c.IsCompleted && (c.Status == "ACCEPTED" || c.Status == "IN_PROGRESS" || c.Status == "SUBMITTED" || c.Status == "OVERDUE"));
        var completedPackages = contributions.Count(c => c.IsCompleted);
        var unfinishedPackages = activeTotal - completedPackages;
        var overduePackages = contributions.Count(c => c.IsOverdue);
        var pendingReviewPackages = contributions.Count(c => c.Status == "SUBMITTED");

        var progressOverview = new TaskProgressOverviewView(
            overallPercent,
            participatingEmployees,
            activeTotal,
            completedPackages,
            inProgressPackages,
            unfinishedPackages,
            overduePackages,
            pendingReviewPackages,
            departmentName,
            accountableManagerName,
            contributions,
            effectiveTaskKind,
            participatingDepartmentsCount,
            accountableManagersCount);

        var subtasks = subtaskEntities.Select(st =>
        {
            var match = contributions.FirstOrDefault(c => c.TaskId == st.Id);
            return new TaskListRow(
                st.Id, st.Title, TaskListCodes.State(st.Status), TaskListCodes.Priority(st.Priority), st.Deadline,
                st.CreatedAt, st.CreatorId, st.RequestId, st.Assignment?.UserId, st.Assignment?.UserName,
                st.Assignment?.DepartmentId, st.Assignment?.DepartmentName ?? st.Assignment?.UserDepartmentName,
                st.ParentTaskId, 0, 0, match?.IndividualProgressPercent ?? 0,
                TaskKind: st.TaskKind);
        }).ToList();

        var taskRow = new TaskListRow(task.Id, task.Title, TaskListCodes.State(task.Status), TaskListCodes.Priority(task.Priority), task.Deadline,
            task.CreatedAt, task.CreatorId, task.RequestId, assignment?.UserId, userName, assignment?.DepartmentId, departmentName,
            task.ParentTaskId, activeTotal, completedSubtaskCount, overallPercent, accountableManagerName,
            participatingEmployees, inProgressPackages, overduePackages, parentTitle, effectiveTaskKind);

        return new(taskRow,
            task.Description, creator, task.WorkflowVersionId, task.SlaVersionId, task.CompletedAt, task.UpdatedAt,
            await Page(checklist, pages.ChecklistPage), await Page(reports, pages.ReportPage), await Page(results, pages.ResultPage),
            subtasks, parentTitle, progressOverview);
    }
}
