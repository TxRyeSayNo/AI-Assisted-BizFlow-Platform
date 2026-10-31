using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Domain.AI;
using BizFlow.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace BizFlow.Infrastructure.AI;

public sealed class AiSelfReflectionService : IAiSelfReflectionService
{
    private readonly BizFlowDbContext _dbContext;

    public AiSelfReflectionService(BizFlowDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ReflectionMetricsDto> GetReflectionMetricsAsync(Guid tenantId, CancellationToken cancellationToken = default)
    {
        var decidedRecs = await _dbContext.AIRecommendations
            .Where(r => r.TenantId == tenantId && r.HumanDecision != null)
            .OrderByDescending(r => r.DecidedAt)
            .Take(200)
            .ToListAsync(cancellationToken);

        var total = decidedRecs.Count;
        var accepted = decidedRecs.Count(r => r.HumanDecision == HumanDecision.Accepted);
        var overridden = decidedRecs.Count(r => r.HumanDecision == HumanDecision.Overridden);
        var dismissed = decidedRecs.Count(r => r.HumanDecision == HumanDecision.Dismissed);

        var acceptanceRate = total > 0 ? Math.Round((decimal)accepted / total, 4) : 1.0000m;
        var overrideRate = total > 0 ? Math.Round((decimal)overridden / total, 4) : 0.0000m;
        var accuracyScore = total > 0
            ? Math.Round(((accepted * 1.0m) + (overridden * 0.4m)) / total, 4)
            : 0.9500m;

        var critiqueRules = await ExtractCritiqueRulesInternalAsync(tenantId, decidedRecs, cancellationToken);

        return new ReflectionMetricsDto(
            TotalDecisions: total,
            AcceptedCount: accepted,
            OverriddenCount: overridden,
            DismissedCount: dismissed,
            AcceptanceRate: acceptanceRate,
            OverrideRate: overrideRate,
            AccuracyScore: accuracyScore,
            ActiveCritiqueRules: critiqueRules);
    }

    public async Task<IReadOnlyList<ReflectionCritiqueRule>> GetCritiqueRulesAsync(
        Guid tenantId,
        string? recommendationType = null,
        CancellationToken cancellationToken = default)
    {
        var query = _dbContext.AIRecommendations
            .Where(r => r.TenantId == tenantId && r.HumanDecision != null);

        if (!string.IsNullOrWhiteSpace(recommendationType))
        {
            var normType = recommendationType.Trim().ToUpperInvariant();
            query = query.Where(r => r.RecommendationType == normType);
        }

        var decidedRecs = await query
            .OrderByDescending(r => r.DecidedAt)
            .Take(150)
            .ToListAsync(cancellationToken);

        return await ExtractCritiqueRulesInternalAsync(tenantId, decidedRecs, cancellationToken);
    }

    public async Task<string> BuildReflectionDirectivesPromptAsync(
        Guid tenantId,
        string recommendationType,
        CancellationToken cancellationToken = default)
    {
        var rules = await GetCritiqueRulesAsync(tenantId, recommendationType, cancellationToken);
        if (rules.Count == 0)
        {
            return "HỌC TẬP & TỰ SOI CHIẾU: Chưa ghi nhận phản hồi tiêu cực từ quản lý. Hãy tiếp tục tuân thủ quy chuẩn chất lượng cao nhất.";
        }

        var sb = new StringBuilder();
        sb.AppendLine("### CHỈ THỊ TỰ SOI CHIẾU & HỌC TẬP (AI SELF-REFLECTION DIRECTIVES):");
        sb.AppendLine("Hệ thống đã phân tích các quyết định ghi đè/từ chối trước đây của người dùng/quản lý:");
        foreach (var rule in rules)
        {
            sb.AppendLine($"- [Bài học kinh nghiệm - {rule.RecommendationType}]: {rule.CritiqueSummary}");
        }
        sb.AppendLine("YÊU CẦU: Tự phê bình và điều chỉnh đề xuất để tránh lặp lại các điểm con người đã từ chối hoặc điều chỉnh!");

        return sb.ToString().TrimEnd();
    }

    private Task<IReadOnlyList<ReflectionCritiqueRule>> ExtractCritiqueRulesInternalAsync(
        Guid tenantId,
        List<AIRecommendation> decidedRecs,
        CancellationToken cancellationToken)
    {
        var rules = new List<ReflectionCritiqueRule>();

        // 1. Group by RecommendationType and analyze overrides & dismissals
        var byType = decidedRecs.GroupBy(r => r.RecommendationType);

        foreach (var group in byType)
        {
            var type = group.Key;
            var overrides = group.Where(r => r.HumanDecision == HumanDecision.Overridden).ToList();
            var dismissals = group.Where(r => r.HumanDecision == HumanDecision.Dismissed).ToList();

            if (overrides.Count > 0)
            {
                var summary = type switch
                {
                    "ASSIGNMENT" => $"Phát hiện {overrides.Count} lần nhân sự được đề xuất bị người quản lý thay đổi (ghi đè). Hãy cân nhắc kỹ tải công việc thực tế và kinh nghiệm phòng ban trước khi đề cử.",
                    "DRAFT" => $"Phát hiện {overrides.Count} bản thảo công việc bị điều chỉnh tiêu đề/thời hạn. Hãy chọn mức ưu tiên thực tế và dự toán thời hạn sát sao hơn.",
                    "BREAKDOWN" => $"Phát hiện {overrides.Count} lần phân rã công việc bị chỉnh sửa. Hãy đảm bảo các bước có đầu ra độc lập rõ ràng và không phụ thuộc chéo.",
                    "ROUTING" => $"Phát hiện {overrides.Count} đề xuất chuyển tiếp yêu cầu bị chuyển sang phòng ban khác. Cần phân tích sâu hơn mô tả chuyên môn.",
                    _ => $"Có {overrides.Count} đề xuất loại {type} bị con người ghi đè. Cần thận trọng hạ thấp mức độ tự tin mặc định."
                };

                rules.Add(new ReflectionCritiqueRule(
                    RuleId: $"CRITIQUE-{type}-OVERRIDE",
                    RecommendationType: type,
                    Scope: "TENANT_OVERRIDE_LEARNING",
                    CritiqueSummary: summary,
                    ConfidencePenalty: Math.Min(0.20m, overrides.Count * 0.05m),
                    DiscoveredAt: overrides.First().DecidedAt ?? DateTimeOffset.UtcNow));
            }

            if (dismissals.Count > 0)
            {
                var dismissalSummary = $"Có {dismissals.Count} đề xuất loại {type} bị người dùng bỏ qua/bác bỏ hoàn toàn. Hãy nâng cao ngưỡng tự tin trước khi đưa ra đề xuất tương tự.";
                rules.Add(new ReflectionCritiqueRule(
                    RuleId: $"CRITIQUE-{type}-DISMISSAL",
                    RecommendationType: type,
                    Scope: "TENANT_DISMISSAL_LEARNING",
                    CritiqueSummary: dismissalSummary,
                    ConfidencePenalty: Math.Min(0.25m, dismissals.Count * 0.08m),
                    DiscoveredAt: dismissals.First().DecidedAt ?? DateTimeOffset.UtcNow));
            }
        }

        // Add standard foundational self-reflection directive if none existed yet
        if (rules.Count == 0)
        {
            rules.Add(new ReflectionCritiqueRule(
                RuleId: "BASELINE-QUALITY",
                RecommendationType: "GENERAL",
                Scope: "AGENT_CORE_DIRECTIVE",
                CritiqueSummary: "Duy trì tiêu chuẩn phân rã nhiệm vụ dạng DAG không chu trình, kiểm soát chặt chẽ SLA và bảo vệ an toàn dữ liệu khách hàng.",
                ConfidencePenalty: 0.00m,
                DiscoveredAt: DateTimeOffset.UtcNow));
        }

        return Task.FromResult<IReadOnlyList<ReflectionCritiqueRule>>(rules);
    }
}
