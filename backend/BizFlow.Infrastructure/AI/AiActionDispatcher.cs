using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Domain.Collaboration;
using BizFlow.Domain.Organization;
using BizFlow.Domain.Requests;
using BizFlow.Domain.Tasks;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.AI;

public sealed class AiActionDispatcher(BizFlowDbContext dbContext) : IAiActionDispatcher
{
    public async Task<object?> DispatchToolAsync(
        Guid tenantId,
        Guid userId,
        string toolName,
        string argsJson,
        CancellationToken cancellationToken)
    {
        using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(argsJson) ? "{}" : argsJson);
        var root = doc.RootElement;

        switch (toolName.ToLowerInvariant())
        {
            case "create_task_draft":
            {
                var title = root.TryGetProperty("title", out var tp) ? tp.GetString() ?? "" : "Nhiệm vụ mới từ AI";
                var desc = root.TryGetProperty("description", out var dp) ? dp.GetString() : null;
                var priorityStr = root.TryGetProperty("priority", out var pp) ? pp.GetString() : "Medium";
                var priority = Enum.TryParse<TaskPriority>(priorityStr, true, out var p) ? p : TaskPriority.Medium;

                Guid? deptId = root.TryGetProperty("departmentId", out var dip) && dip.TryGetGuid(out var d) ? d : null;
                if (deptId.HasValue)
                {
                    var deptExists = await dbContext.Departments.AnyAsync(dept => dept.TenantId == tenantId && dept.Id == deptId.Value && dept.Status == RecordStatus.Active, cancellationToken);
                    if (!deptExists)
                        throw new ApplicationFault(FaultKind.Validation, "AI.DEPARTMENT_NOT_FOUND", "Department not found in workspace.");
                }

                Guid? assigneeId = root.TryGetProperty("assigneeId", out var aip) && aip.TryGetGuid(out var a) ? a : null;
                if (assigneeId.HasValue)
                {
                    var userExists = await dbContext.Users.AnyAsync(u => u.TenantId == tenantId && u.Id == assigneeId.Value && u.DeletedAt == null && u.Status == UserStatus.Active, cancellationToken);
                    if (!userExists)
                        throw new ApplicationFault(FaultKind.Validation, "AI.USER_NOT_FOUND", "Assignee user not found or inactive in workspace.");
                }

                var task = WorkTask.CreateDraft(tenantId, userId, title, DateTimeOffset.UtcNow, desc, priority, null, null);
                dbContext.WorkTasks.Add(task);
                await dbContext.SaveChangesAsync(cancellationToken);
                return new { taskId = task.Id, title = task.Title, status = task.Status.ToString() };
            }

            case "assign_task":
            {
                if (!root.TryGetProperty("taskId", out var tip) || !tip.TryGetGuid(out var taskId))
                    throw new ApplicationFault(FaultKind.Validation, "AI.ARG_REQUIRED", "Argument 'taskId' is required.");

                if (!root.TryGetProperty("userId", out var uip) || !uip.TryGetGuid(out var targetUserId))
                    throw new ApplicationFault(FaultKind.Validation, "AI.ARG_REQUIRED", "Argument 'userId' is required.");

                var task = await dbContext.WorkTasks
                    .FirstOrDefaultAsync(t => t.TenantId == tenantId && t.Id == taskId && t.DeletedAt == null, cancellationToken)
                    ?? throw new ApplicationFault(FaultKind.NotFound, "TASK.NOT_FOUND", "Task not found in tenant workspace.");

                if (task.Status == TaskState.Completed || task.Status == TaskState.Cancelled)
                    throw new ApplicationFault(FaultKind.Conflict, "TASK.TERMINAL_STATE", $"Cannot assign task in terminal state {task.Status}.");

                var targetUser = await dbContext.Users
                    .FirstOrDefaultAsync(u => u.TenantId == tenantId && u.Id == targetUserId && u.DeletedAt == null && u.Status == UserStatus.Active, cancellationToken)
                    ?? throw new ApplicationFault(FaultKind.Validation, "USER.NOT_FOUND", "Target assignee user not found or inactive in tenant workspace.");

                var assignment = TaskAssignment.Create(task.Id, userId, null, targetUserId, DateTimeOffset.UtcNow);
                dbContext.TaskAssignments.Add(assignment);
                await dbContext.SaveChangesAsync(cancellationToken);
                return new { taskId = task.Id, assignmentId = assignment.Id, assignedTo = targetUserId };
            }

            case "add_comment":
            {
                if (!root.TryGetProperty("objectId", out var oip) || !oip.TryGetGuid(out var objectId))
                    throw new ApplicationFault(FaultKind.Validation, "AI.ARG_REQUIRED", "Argument 'objectId' is required.");

                var content = root.TryGetProperty("content", out var cp) ? cp.GetString() ?? "" : "";
                if (string.IsNullOrWhiteSpace(content))
                    throw new ApplicationFault(FaultKind.Validation, "AI.ARG_REQUIRED", "Argument 'content' is required.");

                var objTypeStr = root.TryGetProperty("objectType", out var otp) ? otp.GetString() : "Task";
                var objType = Enum.TryParse<CommentObjectType>(objTypeStr, true, out var ot) ? ot : CommentObjectType.Task;

                if (objType == CommentObjectType.Task)
                {
                    var taskExists = await dbContext.WorkTasks.AnyAsync(t => t.TenantId == tenantId && t.Id == objectId && t.DeletedAt == null, cancellationToken);
                    if (!taskExists)
                        throw new ApplicationFault(FaultKind.NotFound, "TASK.NOT_FOUND", "Target task not found in tenant workspace.");
                }
                else if (objType == CommentObjectType.Request)
                {
                    var reqExists = await dbContext.Requests.AnyAsync(r => r.TenantId == tenantId && r.Id == objectId && r.DeletedAt == null, cancellationToken);
                    if (!reqExists)
                        throw new ApplicationFault(FaultKind.NotFound, "REQUEST.NOT_FOUND", "Target request not found in tenant workspace.");
                }

                var comment = Comment.Create(tenantId, objType, objectId, userId, content, DateTimeOffset.UtcNow);
                dbContext.Comments.Add(comment);
                await dbContext.SaveChangesAsync(cancellationToken);
                return new { commentId = comment.Id, objectId = comment.ObjectId, content = comment.Content };
            }

            case "route_request":
            {
                if (!root.TryGetProperty("requestId", out var rip) || !rip.TryGetGuid(out var reqId))
                    throw new ApplicationFault(FaultKind.Validation, "AI.ARG_REQUIRED", "Argument 'requestId' is required.");

                Guid? toDeptId = root.TryGetProperty("toDepartmentId", out var dip) && dip.TryGetGuid(out var d) ? d : null;
                Guid? toUserId = root.TryGetProperty("toUserId", out var uip) && uip.TryGetGuid(out var u) ? u : null;
                var reason = root.TryGetProperty("reason", out var rp) ? rp.GetString() : "Điều phối tự động qua AI Agent";

                var request = await dbContext.Requests
                    .FirstOrDefaultAsync(r => r.TenantId == tenantId && r.Id == reqId && r.DeletedAt == null, cancellationToken)
                    ?? throw new ApplicationFault(FaultKind.NotFound, "REQUEST.NOT_FOUND", "Request not found in tenant workspace.");

                if (request.Status is RequestState.Closed or RequestState.Cancelled or RequestState.Rejected)
                    throw new ApplicationFault(FaultKind.Conflict, "REQUEST.TERMINAL_STATE", $"Cannot route request in terminal state {request.Status}.");

                if (toDeptId.HasValue)
                {
                    var deptExists = await dbContext.Departments.AnyAsync(dept => dept.TenantId == tenantId && dept.Id == toDeptId.Value && dept.Status == RecordStatus.Active, cancellationToken);
                    if (!deptExists)
                        throw new ApplicationFault(FaultKind.Validation, "DEPARTMENT.NOT_FOUND", "Target department not found in workspace.");
                }

                if (toUserId.HasValue)
                {
                    var userExists = await dbContext.Users.AnyAsync(u => u.TenantId == tenantId && u.Id == toUserId.Value && u.DeletedAt == null && u.Status == UserStatus.Active, cancellationToken);
                    if (!userExists)
                        throw new ApplicationFault(FaultKind.Validation, "USER.NOT_FOUND", "Target user not found or inactive in workspace.");
                }

                var routing = RequestRouting.Create(
                    tenantId, request.Id, userId, DateTimeOffset.UtcNow,
                    toDeptId, toUserId, null, null, reason, RequestRoutingSource.Ai);

                var mutation = new RequestWorkflowMutation(userId, request.Status, RequestState.Routed, request.UpdatedAt);
                request.ApplyRoutingTransition(mutation, DateTimeOffset.UtcNow);

                dbContext.RequestRoutings.Add(routing);
                await dbContext.SaveChangesAsync(cancellationToken);
                return new { requestId = request.Id, routingId = routing.Id, status = request.Status.ToString() };
            }

            default:
                throw new ApplicationFault(FaultKind.Validation, "AI.UNKNOWN_TOOL", $"Tool '{toolName}' has no dispatcher implementation.");
        }
    }
}
