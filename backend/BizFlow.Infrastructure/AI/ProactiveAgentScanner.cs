using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Domain.AI;
using BizFlow.Domain.Notifications;
using BizFlow.Domain.Tasks;
using BizFlow.Domain.Workflows;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace BizFlow.Infrastructure.AI;

public sealed class ProactiveAgentScanner : IProactiveAgentScanner
{
    private readonly BizFlowDbContext _dbContext;
    private readonly IAiStore _aiStore;
    private readonly TimeProvider _clock;
    private readonly ILogger<ProactiveAgentScanner>? _logger;

    public ProactiveAgentScanner(
        BizFlowDbContext dbContext,
        IAiStore aiStore,
        TimeProvider clock,
        ILogger<ProactiveAgentScanner>? logger = null)
    {
        _dbContext = dbContext;
        _aiStore = aiStore;
        _clock = clock;
        _logger = logger;
    }

    public async Task<ProactiveScanResult> RunProactiveScanAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var now = _clock.GetUtcNow();
        var slaRisksDetected = 0;
        var workloadImbalancesDetected = 0;
        var recommendationsCreated = 0;

        // 1. Proactive SLA & Deadline Breaches Scanner
        var activeTasks = await _dbContext.WorkTasks
            .Where(t => t.TenantId == tenantId &&
                        t.Status != TaskState.Completed &&
                        t.Status != TaskState.Cancelled &&
                        t.Deadline != null)
            .OrderBy(t => t.Deadline)
            .Take(100)
            .ToListAsync(cancellationToken);

        var taskIds = activeTasks.Select(t => t.Id).ToList();
        var currentAssignments = await _dbContext.Set<TaskAssignment>()
            .Where(a => taskIds.Contains(a.TaskId) && a.EndedAt == null)
            .ToListAsync(cancellationToken);
        var assignmentByTaskId = currentAssignments
            .GroupBy(a => a.TaskId)
            .ToDictionary(g => g.Key, g => g.First());

        foreach (var task in activeTasks)
        {
            var remainingHours = (task.Deadline!.Value - now).TotalHours;
            var isOverdue = remainingHours < 0;
            var isCriticalDeadline = remainingHours <= 24;

            if (isOverdue || isCriticalDeadline)
            {
                // Check if an unreviewed proactive alert was already created
                var recentAlertExists = await _dbContext.AIRecommendations
                    .AnyAsync(r => r.TenantId == tenantId &&
                                   r.ObjectId == task.Id &&
                                   r.RecommendationType == "PROACTIVE_RISK_ALERT" &&
                                   r.HumanDecision == null &&
                                   r.DecidedAt == null, cancellationToken);

                if (!recentAlertExists)
                {
                    slaRisksDetected++;
                    var riskLevel = isOverdue ? "CRITICAL" : "HIGH";
                    var rationale = isOverdue
                        ? $"[Cảnh báo chủ động] Công việc '{task.Title}' đã quá hạn {Math.Abs(Math.Round(remainingHours / 24, 1))} ngày mà chưa hoàn tất."
                        : $"[Cảnh báo chủ động] Công việc '{task.Title}' chỉ còn {Math.Round(remainingHours, 1)} giờ trước khi chạm hạn chót.";

                    assignmentByTaskId.TryGetValue(task.Id, out var assign);
                    var assigneeId = assign?.UserId;

                    var payload = JsonSerializer.Serialize(new
                    {
                        taskId = task.Id,
                        taskTitle = task.Title,
                        remainingHours = Math.Round(remainingHours, 1),
                        riskLevel = riskLevel,
                        assigneeId = assigneeId,
                        suggestedAction = isOverdue ? "ESCALATE_OR_REASSIGN" : "PRIORITIZE_OR_EXTEND",
                        message = rationale
                    });

                    // Autonomous AI Interaction & Recommendation
                    var interaction = await _aiStore.RecordInteractionAsync(
                        tenantId: tenantId,
                        userId: assigneeId ?? task.CreatorId,
                        feature: "ai.proactive.sla_risk",
                        modelName: "proactive-autonomous-daemon",
                        inputRef: task.Id.ToString(),
                        outputRef: null,
                        status: AIInteractionStatus.Succeeded,
                        latencyMs: 15,
                        tokenUsage: null,
                        cancellationToken: cancellationToken);

                    await _aiStore.RecordRecommendationAsync(
                        tenantId: tenantId,
                        aiInteractionId: interaction.Id,
                        objectType: RecommendationObjectType.Task,
                        objectId: task.Id,
                        recommendationType: "PROACTIVE_RISK_ALERT",
                        payloadJson: payload,
                        confidence: 0.96m,
                        cancellationToken: cancellationToken);

                    recommendationsCreated++;

                    // Proactively deliver in-app notification
                    var recipientId = assigneeId ?? task.CreatorId;
                    if (recipientId != Guid.Empty)
                    {
                        var eventType = isOverdue ? NotificationEvent.Overdue : NotificationEvent.DeadlineWarning;
                        var idempotencyKey = $"ai-alert-{task.Id}-{now:yyyyMMdd-HH}";
                        var notification = Notification.Create(
                            tenantId: tenantId,
                            recipientId: recipientId,
                            type: eventType,
                            title: $"⚡ AI Chủ động: Nguy cơ trễ hạn [{task.Title}]",
                            content: rationale,
                            idempotencyKey: idempotencyKey,
                            objectType: "Task",
                            objectId: task.Id);

                        _dbContext.Notifications.Add(notification);
                    }
                }
            }
        }

        // 2. Proactive Workload Imbalance & Auto-Rebalancing Detector
        var activeAssignments = await (
            from t in _dbContext.WorkTasks
            join a in _dbContext.Set<TaskAssignment>() on t.Id equals a.TaskId
            where t.TenantId == tenantId &&
                  t.Status != TaskState.Completed &&
                  t.Status != TaskState.Cancelled &&
                  a.EndedAt == null &&
                  a.UserId != null &&
                  a.DepartmentId != null
            select new { a.DepartmentId, UserId = a.UserId!.Value, TaskId = t.Id }
        ).ToListAsync(cancellationToken);

        var byDepartment = activeAssignments
            .GroupBy(a => a.DepartmentId)
            .ToList();

        foreach (var deptGroup in byDepartment)
        {
            var userCounts = deptGroup
                .GroupBy(a => a.UserId)
                .Select(g => new { UserId = g.Key, Count = g.Count() })
                .OrderByDescending(u => u.Count)
                .ToList();

            if (userCounts.Count >= 2)
            {
                var mostLoaded = userCounts.First();
                var leastLoaded = userCounts.Last();

                if (mostLoaded.Count >= 4 && leastLoaded.Count <= 1 && (mostLoaded.Count - leastLoaded.Count) >= 3)
                {
                    // Check if an unreviewed rebalance proposal exists
                    var recentRebalanceExists = await _dbContext.AIRecommendations
                        .AnyAsync(r => r.TenantId == tenantId &&
                                       r.RecommendationType == "PROACTIVE_REBALANCE_PROPOSAL" &&
                                       r.HumanDecision == null &&
                                       r.DecidedAt == null, cancellationToken);

                    if (!recentRebalanceExists)
                    {
                        workloadImbalancesDetected++;
                        var payload = JsonSerializer.Serialize(new
                        {
                            departmentId = deptGroup.Key,
                            overloadedUserId = mostLoaded.UserId,
                            overloadedCount = mostLoaded.Count,
                            availableUserId = leastLoaded.UserId,
                            availableCount = leastLoaded.Count,
                            suggestedAction = "REBALANCE_TASKS",
                            message = $"Phát hiện lệch tải công việc: Nhân sự ({mostLoaded.UserId}) đang xử lý {mostLoaded.Count} việc, trong khi nhân sự ({leastLoaded.UserId}) chỉ có {leastLoaded.Count} việc. Khuyến nghị điều phối lại."
                        });

                        var interaction = await _aiStore.RecordInteractionAsync(
                            tenantId: tenantId,
                            userId: mostLoaded.UserId,
                            feature: "ai.proactive.workload_rebalance",
                            modelName: "proactive-autonomous-daemon",
                            inputRef: deptGroup.Key?.ToString(),
                            outputRef: null,
                            status: AIInteractionStatus.Succeeded,
                            latencyMs: 20,
                            tokenUsage: null,
                            cancellationToken: cancellationToken);

                        await _aiStore.RecordRecommendationAsync(
                            tenantId: tenantId,
                            aiInteractionId: interaction.Id,
                            objectType: RecommendationObjectType.Task,
                            objectId: null,
                            recommendationType: "PROACTIVE_REBALANCE_PROPOSAL",
                            payloadJson: payload,
                            confidence: 0.91m,
                            cancellationToken: cancellationToken);

                        recommendationsCreated++;
                    }
                }
            }
        }

        await _dbContext.SaveChangesAsync(cancellationToken);

        var summary = $"Quét chủ động hoàn tất: {slaRisksDetected} nguy cơ SLA, {workloadImbalancesDetected} cảnh báo lệch tải, tạo mới {recommendationsCreated} kiến nghị chủ động.";
        _logger?.LogInformation("[ProactiveAgent] {Summary}", summary);

        return new ProactiveScanResult(
            SlaRisksDetected: slaRisksDetected,
            WorkloadImbalancesDetected: workloadImbalancesDetected,
            RecommendationsCreated: recommendationsCreated,
            ScannedAt: now,
            Summary: summary);
    }

    public async Task<IReadOnlyList<ProactiveInsightDto>> GetActiveProactiveInsightsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var rawRecommendations = await _dbContext.AIRecommendations
            .Where(r => r.TenantId == tenantId &&
                        r.HumanDecision == null &&
                        (r.RecommendationType == "PROACTIVE_RISK_ALERT" || r.RecommendationType == "PROACTIVE_REBALANCE_PROPOSAL"))
            .OrderByDescending(r => r.Id)
            .Take(20)
            .ToListAsync(cancellationToken);

        var list = new List<ProactiveInsightDto>();
        foreach (var r in rawRecommendations)
        {
            var title = r.RecommendationType == "PROACTIVE_RISK_ALERT" ? "Cảnh báo nguy cơ SLA trễ hạn" : "Đề xuất cân bằng tải công việc";
            var desc = r.PayloadJson;
            var suggestedAction = "REVIEW";

            try
            {
                using var doc = JsonDocument.Parse(r.PayloadJson);
                if (doc.RootElement.TryGetProperty("message", out var msgProp))
                {
                    desc = msgProp.GetString() ?? desc;
                }
                if (doc.RootElement.TryGetProperty("suggestedAction", out var actProp))
                {
                    suggestedAction = actProp.GetString() ?? suggestedAction;
                }
            }
            catch
            {
                // Fallback to raw json
            }

            list.Add(new ProactiveInsightDto(
                RecommendationId: r.Id,
                RecommendationType: r.RecommendationType,
                ObjectType: r.ObjectType.ToString(),
                ObjectId: r.ObjectId,
                Title: title,
                Description: desc,
                Confidence: r.Confidence ?? 0.90m,
                SuggestedAction: suggestedAction,
                CreatedAt: DateTimeOffset.UtcNow));
        }

        return list;
    }
}
