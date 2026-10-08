using System;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Domain.Collaboration;
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
                    ?? throw new ApplicationFault(FaultKind.NotFound, "TASK.NOT_FOUND", "Task not found.");

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
                    ?? throw new ApplicationFault(FaultKind.NotFound, "REQUEST.NOT_FOUND", "Request not found.");

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
