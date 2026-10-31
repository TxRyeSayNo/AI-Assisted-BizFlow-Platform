using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.AI;

namespace BizFlow.Infrastructure.AI;

public sealed class AiDocumentSummarizerService(
    ITenantContext tenantContext,
    IResourceAuthorizer authorizer,
    IAiStore store,
    IAiDocumentSummarizerStore summarizerStore,
    TimeProvider clock,
    IAiModelClient? modelClient = null) : IAiDocumentSummarizerService
{
    private Guid GetRequiredTenantId() =>
        tenantContext.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "TENANT.REQUIRED", "An active tenant context is required.");

    private Guid GetRequiredUserId() =>
        tenantContext.UserId ?? throw new ApplicationFault(FaultKind.Forbidden, "USER.REQUIRED", "An authenticated user is required.");

    public async Task<DocumentSummarizeResult> SummarizeDocumentAsync(
        DocumentSummarizeCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        var userId = GetRequiredUserId();

        await authorizer.AuthorizeAsync("ai.006", new(tenantId), cancellationToken: cancellationToken);

        string contentText;
        string sourceTitle;
        string sourceType;

        if (command.AttachmentId.HasValue)
        {
            var att = await summarizerStore.GetAttachmentTextAsync(tenantId, command.AttachmentId.Value, cancellationToken);
            if (att == null)
            {
                throw new ApplicationFault(FaultKind.NotFound, "ATTACHMENT.NOT_FOUND", "Attachment not found in workspace.");
            }

            sourceTitle = att.Value.FileName;
            sourceType = "Attachment";
            contentText = att.Value.Content ?? $"[Tài liệu đính kèm: {att.Value.FileName}]";
        }
        else if (!string.IsNullOrWhiteSpace(command.RawText))
        {
            sourceTitle = "Văn bản nhập trực tiếp";
            sourceType = "RawText";
            contentText = command.RawText.Trim();
            if (contentText.Length > 64_000)
            {
                contentText = contentText[..64_000];
            }
        }
        else
        {
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "Either RawText or AttachmentId must be supplied for summarization.");
        }

        var wordCount = contentText.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
        var mode = string.IsNullOrWhiteSpace(command.Mode) ? "COMPREHENSIVE" : command.Mode.ToUpperInvariant();
        var lang = string.IsNullOrWhiteSpace(command.Language) ? "vi" : command.Language.ToLowerInvariant();

        var executiveSummary = "";
        var keyPoints = new List<string>();
        var actionItems = new List<ExtractedActionItem>();
        var risksAndCompliance = new List<string>();
        decimal confidence = 0.90m;
        int? tokensUsed = null;

        if (modelClient?.IsConfigured == true)
        {
            try
            {
                var sysInstructions = $"""
                    You are BizFlow AI Document Intelligence & Summarization Agent.
                    Analyze the provided document text and produce a high-value summary tailored to mode: "{mode}" in language: "{lang}".

                    Return ONLY a JSON object with:
                    - "executiveSummary": concise narrative summary (2-4 paragraphs or key bullet points)
                    - "keyPoints": array of top 3 to 6 essential takeaways (strings)
                    - "actionItems": array of extracted actionable tasks:
                      - "orderIndex": integer (1, 2, 3...)
                      - "title": concise task title (string)
                      - "description": clear task description and deliverable
                      - "suggestedDepartment": suggested department name or null
                      - "suggestedPriority": "Low", "Medium", "High", or "Critical"
                      - "suggestedDurationDays": integer (1 to 14)
                    - "risksAndCompliance": array of risks, deadlines, legal obligations, or compliance alerts (strings)
                    - "confidence": decimal between 0.75 and 0.99
                    """;

                var aiResp = await modelClient.CompleteAsync(contentText, sysInstructions, cancellationToken);
                tokensUsed = (aiResp.PromptTokens ?? 0) + (aiResp.CompletionTokens ?? 0);

                using var doc = JsonDocument.Parse(aiResp.Content);
                var root = doc.RootElement;

                if (root.TryGetProperty("executiveSummary", out var esProp))
                {
                    executiveSummary = esProp.GetString() ?? "";
                }

                if (root.TryGetProperty("confidence", out var confProp) && confProp.TryGetDecimal(out var parsedConf))
                {
                    confidence = Math.Clamp(parsedConf, 0.50m, 0.99m);
                }

                if (root.TryGetProperty("keyPoints", out var kpArr) && kpArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in kpArr.EnumerateArray())
                    {
                        if (elem.GetString() is { } kp && !string.IsNullOrWhiteSpace(kp))
                            keyPoints.Add(kp.Trim());
                    }
                }

                if (root.TryGetProperty("risksAndCompliance", out var rcArr) && rcArr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var elem in rcArr.EnumerateArray())
                    {
                        if (elem.GetString() is { } rc && !string.IsNullOrWhiteSpace(rc))
                            risksAndCompliance.Add(rc.Trim());
                    }
                }

                if (root.TryGetProperty("actionItems", out var aiArr) && aiArr.ValueKind == JsonValueKind.Array)
                {
                    var itemIdx = 1;
                    foreach (var elem in aiArr.EnumerateArray())
                    {
                        var itemTitle = elem.TryGetProperty("title", out var itProp) ? itProp.GetString() : null;
                        if (string.IsNullOrWhiteSpace(itemTitle)) continue;

                        var itemDesc = elem.TryGetProperty("description", out var idProp) ? idProp.GetString() ?? "" : "";
                        var dept = elem.TryGetProperty("suggestedDepartment", out var deptProp) ? deptProp.GetString() : null;
                        var pri = elem.TryGetProperty("suggestedPriority", out var priProp) ? priProp.GetString() ?? "Medium" : "Medium";
                        var days = elem.TryGetProperty("suggestedDurationDays", out var durProp) && durProp.TryGetInt32(out var parsedDays) ? parsedDays : 3;

                        actionItems.Add(new ExtractedActionItem(
                            OrderIndex: itemIdx++,
                            Title: itemTitle.Trim(),
                            Description: itemDesc.Trim(),
                            SuggestedDepartment: dept,
                            SuggestedPriority: pri,
                            SuggestedDurationDays: Math.Clamp(days, 1, 30)));
                    }
                }
            }
            catch
            {
                // Fallback to deterministic logic
            }
        }

        if (string.IsNullOrWhiteSpace(executiveSummary))
        {
            var fallback = GenerateDeterministicSummary(contentText, sourceTitle, mode);
            executiveSummary = fallback.ExecutiveSummary;
            keyPoints = fallback.KeyPoints;
            actionItems = fallback.ActionItems;
            risksAndCompliance = fallback.RisksAndCompliance;
        }

        var now = clock.GetUtcNow();

        await store.RecordInteractionAsync(
            tenantId,
            userId,
            "ai.document_summary",
            modelClient?.IsConfigured == true ? "ai-agent-summarizer" : "heuristic-summarizer",
            sourceTitle,
            $"WordCount: {wordCount}, Mode: {mode}, Actions: {actionItems.Count}",
            AIInteractionStatus.Succeeded,
            null,
            tokensUsed?.ToString(),
            cancellationToken);

        return new DocumentSummarizeResult(
            SourceTitle: sourceTitle,
            SourceType: sourceType,
            Mode: mode,
            ExecutiveSummary: executiveSummary,
            KeyPoints: keyPoints,
            ActionItems: actionItems,
            RisksAndCompliance: risksAndCompliance,
            WordCount: wordCount,
            Confidence: confidence,
            TokensUsed: tokensUsed,
            SummarizedAt: now);
    }

    public async Task<CreateTasksFromSummaryResponse> CreateTasksFromSummaryAsync(
        CreateTasksFromSummaryCommand command,
        CancellationToken cancellationToken = default)
    {
        var tenantId = GetRequiredTenantId();
        var userId = GetRequiredUserId();

        await authorizer.AuthorizeAsync("tasks.create", new(tenantId), cancellationToken: cancellationToken);

        if (command.Items == null || command.Items.Count == 0)
        {
            throw new ApplicationFault(FaultKind.Validation, "VALIDATION.FAILED", "At least one action item must be selected to create tasks.");
        }

        var now = clock.GetUtcNow();
        var createdTasks = await summarizerStore.PersistSummaryTasksAsync(
            tenantId,
            userId,
            command.SourceTitle,
            command.Items,
            now,
            cancellationToken);

        return new CreateTasksFromSummaryResponse(
            TotalCreated: createdTasks.Count,
            CreatedTasks: createdTasks,
            CreatedAt: now);
    }

    private static (string ExecutiveSummary, List<string> KeyPoints, List<ExtractedActionItem> ActionItems, List<string> RisksAndCompliance)
        GenerateDeterministicSummary(string text, string sourceTitle, string mode)
    {
        var sentences = Regex.Split(text, @"(?<=[.!?\n])\s+")
            .Select(s => s.Trim())
            .Where(s => s.Length > 15)
            .ToList();

        if (sentences.Count == 0)
        {
            sentences.Add(text);
        }

        var keyPoints = sentences.Take(Math.Min(4, sentences.Count)).ToList();
        var execSummary = $"Tài liệu \"{sourceTitle}\" bao gồm các nội dung cốt lõi về quy trình nghiệp vụ, chỉ đạo công việc và phân bổ nguồn lực. " +
                          string.Join(" ", sentences.Take(2));

        var actionItems = new List<ExtractedActionItem>();
        var risks = new List<string>();

        var actionRegex = new Regex(@"\b(cần|phải|triển khai|hoàn thành|thực hiện|kiểm tra|báo cáo|gửi|lập|phê duyệt|deliver|submit|review|implement)\b", RegexOptions.IgnoreCase);
        var riskRegex = new Regex(@"\b(rủi ro|chậm trễ|phạt|nghiêm cấm|bồi thường|vi phạm|quá hạn|nguy cơ|chế tài|risk|penalty|breach|delay)\b", RegexOptions.IgnoreCase);

        var idx = 1;
        foreach (var s in sentences)
        {
            if (actionRegex.IsMatch(s) && actionItems.Count < 5)
            {
                var title = s.Length > 70 ? s[..70].Trim() + "…" : s;
                actionItems.Add(new ExtractedActionItem(
                    OrderIndex: idx++,
                    Title: title,
                    Description: s,
                    SuggestedDepartment: null,
                    SuggestedPriority: s.Contains("ngay") || s.Contains("khẩn") ? "High" : "Medium",
                    SuggestedDurationDays: 3));
            }

            if (riskRegex.IsMatch(s) && risks.Count < 4)
            {
                risks.Add(s);
            }
        }

        if (actionItems.Count == 0)
        {
            actionItems.Add(new ExtractedActionItem(
                OrderIndex: 1,
                Title: $"Nghiên cứu và phản hồi nội dung {sourceTitle}",
                Description: "Đọc kỹ toàn bộ văn bản và thống nhất các bước hành động tiếp theo.",
                SuggestedDepartment: null,
                SuggestedPriority: "Medium",
                SuggestedDurationDays: 3));
        }

        if (risks.Count == 0)
        {
            risks.Add("Cần lưu ý tuân thủ thời hạn chót và quy trình bảo mật thông tin nội bộ của doanh nghiệp.");
        }

        return (execSummary, keyPoints, actionItems, risks);
    }
}
