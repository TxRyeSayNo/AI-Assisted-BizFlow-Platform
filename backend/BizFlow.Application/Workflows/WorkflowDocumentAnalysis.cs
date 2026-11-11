using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Domain.Workflows;

namespace BizFlow.Application.Workflows;

public sealed record WorkflowGraphNodeDto(
    string Id,
    string Type, // "START" | "DEPARTMENT" | "APPROVAL" | "DECISION" | "END"
    string Name,
    int X,
    int Y,
    Guid? DepartmentId = null,
    string? DepartmentName = null,
    Guid? RoleId = null,
    string? RoleName = null,
    int? SlaHours = null,
    string? Description = null,
    string? Trigger = null,
    string? CompletionState = null);

public sealed record WorkflowGraphEdgeDto(
    string Id,
    string Source,
    string Target,
    string? Label = null);

public sealed record WorkflowDraftGraphDto(
    string WorkflowName,
    string BusinessType, // "REQUEST" | "TASK"
    string SummaryExplanation,
    IReadOnlyList<WorkflowGraphNodeDto> Nodes,
    IReadOnlyList<WorkflowGraphEdgeDto> Edges);

public sealed record AnalyzeWorkflowDocumentRequest(
    string? TextContent,
    string BusinessType = "REQUEST");

public interface IWorkflowDocumentAnalysisService
{
    Task<WorkflowDraftGraphDto> AnalyzeDocumentAsync(
        Stream stream,
        string fileName,
        string businessType,
        CancellationToken cancellationToken = default);

    Task<WorkflowDraftGraphDto> AnalyzeTextAsync(
        string textContent,
        string businessType,
        CancellationToken cancellationToken = default);
}

public sealed class WorkflowDocumentAnalysisService(
    ITenantContext context,
    IWorkflowLibraryStore store,
    IAiModelClient? modelClient = null) : IWorkflowDocumentAnalysisService
{
    private static readonly XNamespace WNs = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";

    public async Task<WorkflowDraftGraphDto> AnalyzeDocumentAsync(
        Stream stream,
        string fileName,
        string businessType,
        CancellationToken cancellationToken = default)
    {
        var text = ExtractTextFromStream(stream, fileName);
        if (string.IsNullOrWhiteSpace(text))
        {
            throw new ApplicationFault(FaultKind.Validation, "DOCUMENT.EMPTY", "Không thể trích xuất nội dung văn bản từ tệp tin đã chọn.");
        }

        return await AnalyzeTextAsync(text, businessType, cancellationToken);
    }

    public async Task<WorkflowDraftGraphDto> AnalyzeTextAsync(
        string textContent,
        string businessType,
        CancellationToken cancellationToken = default)
    {
        var cleanText = textContent.Trim();
        if (string.IsNullOrWhiteSpace(cleanText))
        {
            throw new ApplicationFault(FaultKind.Validation, "DOCUMENT.EMPTY", "Nội dung quy trình không được để trống.");
        }

        var tenantId = context.TenantId ?? throw new ApplicationFault(FaultKind.Forbidden, "AUTH.REQUIRED", "Authentication required.");
        var departments = await store.GetActiveDepartmentsAsync(tenantId, cancellationToken);
        var roles = await store.GetActiveRolesAsync(tenantId, cancellationToken);

        if (modelClient != null && modelClient.IsConfigured)
        {
            try
            {
                var llmGraph = await AnalyzeWithLlmAsync(cleanText, businessType, departments, roles, cancellationToken);
                if (llmGraph != null && llmGraph.Nodes.Count >= 3)
                {
                    return llmGraph;
                }
            }
            catch
            {
                // Fallback to deterministic parser
            }
        }

        return DecomposeTextDeterministically(cleanText, businessType, departments, roles);
    }

    private static string ExtractTextFromStream(Stream stream, string fileName)
    {
        if (fileName.EndsWith(".docx", StringComparison.OrdinalIgnoreCase))
        {
            return ExtractTextFromDocx(stream);
        }

        using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: true);
        return reader.ReadToEnd();
    }

    private static string ExtractTextFromDocx(Stream stream)
    {
        try
        {
            using var archive = new ZipArchive(stream, ZipArchiveMode.Read, leaveOpen: true);
            var entry = archive.GetEntry("word/document.xml");
            if (entry == null) return "";

            using var entryStream = entry.Open();
            var doc = XDocument.Load(entryStream);
            var paragraphs = doc.Descendants(WNs + "p")
                .Select(p => string.Concat(p.Descendants(WNs + "t").Select(t => t.Value)).Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p));
            return string.Join("\n", paragraphs);
        }
        catch
        {
            return "";
        }
    }

    private async Task<WorkflowDraftGraphDto?> AnalyzeWithLlmAsync(
        string text,
        string businessType,
        IReadOnlyList<WorkflowLookupItem> departments,
        IReadOnlyList<WorkflowLookupItem> roles,
        CancellationToken cancellationToken)
    {
        var systemInstructions = """
        Bạn là Chuyên gia Kiến trúc Quy trình Doanh nghiệp (Enterprise Workflow Architect).
        Nhiệm vụ của bạn là phân tích tài liệu/văn bản mô tả quy trình của doanh nghiệp và thiết kế sơ đồ quy trình hoàn chỉnh.
        Yêu cầu:
        1. Tạo các bước tuần tự từ START -> các bước thực hiện/phê duyệt -> END.
        2. Các loại node hợp lệ:
           - "START": Điểm bắt đầu
           - "DEPARTMENT": Bước thực thi tại phòng ban
           - "APPROVAL": Bước kiểm duyệt, ký duyệt của cấp quản lý
           - "DECISION": Điểm rẽ nhánh điều kiện
           - "END": Hoàn tất quy trình
        3. Tọa độ (x, y) bố trí từ trái sang phải:
           - node-start: x=80, y=180
           - Các bước trung gian: x tăng dần (+240 cho mỗi bước), y=180-220
           - node-end: x = cuối cùng, y=180
        4. Liên kết (edges): kết nối liên tục từ start -> step 1 -> step 2 -> ... -> end.

        Định dạng JSON bắt buộc:
        {
          "workflowName": "string",
          "businessType": "REQUEST | TASK",
          "summaryExplanation": "string",
          "nodes": [
            {
              "id": "string",
              "type": "START | DEPARTMENT | APPROVAL | DECISION | END",
              "name": "string",
              "x": 80,
              "y": 180,
              "departmentName": "string",
              "roleName": "string",
              "slaHours": 24,
              "description": "string"
            }
          ],
          "edges": [
            {
              "id": "e-1",
              "source": "string",
              "target": "string",
              "label": "string"
            }
          ]
        }
        """;

        var promptBuilder = new StringBuilder();
        promptBuilder.AppendLine($"Loại quy trình mục tiêu: {businessType}");
        promptBuilder.AppendLine($"Các phòng ban hiện có trong công ty: {string.Join(", ", departments.Select(d => d.Name))}");
        promptBuilder.AppendLine($"Các vai trò hiện có trong công ty: {string.Join(", ", roles.Select(r => r.Name))}");
        promptBuilder.AppendLine();
        promptBuilder.AppendLine("Nội dung tài liệu quy trình:");
        promptBuilder.AppendLine(text);

        var response = await modelClient!.CompleteAsync(promptBuilder.ToString(), systemInstructions, cancellationToken);
        var content = response.Content?.Trim();
        if (string.IsNullOrWhiteSpace(content)) return null;

        if (content.StartsWith("```json", StringComparison.OrdinalIgnoreCase))
        {
            content = content[7..];
            if (content.EndsWith("```")) content = content[..^3];
        }
        else if (content.StartsWith("```"))
        {
            content = content[3..];
            if (content.EndsWith("```")) content = content[..^3];
        }

        using var doc = JsonDocument.Parse(content.Trim());
        var root = doc.RootElement;

        var name = root.TryGetProperty("workflowName", out var np) ? np.GetString() ?? "Quy trình phân tích từ tài liệu" : "Quy trình phân tích từ tài liệu";
        var bType = root.TryGetProperty("businessType", out var btp) ? btp.GetString() ?? businessType : businessType;
        var summary = root.TryGetProperty("summaryExplanation", out var sp) ? sp.GetString() ?? "Sơ đồ được thiết kế tự động từ nội dung tài liệu." : "Sơ đồ được thiết kế tự động từ nội dung tài liệu.";

        var nodes = new List<WorkflowGraphNodeDto>();
        if (root.TryGetProperty("nodes", out var nArr) && nArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var nElem in nArr.EnumerateArray())
            {
                var id = nElem.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                var type = nElem.TryGetProperty("type", out var tProp) ? tProp.GetString()?.ToUpperInvariant() ?? "DEPARTMENT" : "DEPARTMENT";
                var nodeName = nElem.TryGetProperty("name", out var nmProp) ? nmProp.GetString() ?? "" : "";
                var x = nElem.TryGetProperty("x", out var xProp) ? xProp.GetInt32() : 100;
                var y = nElem.TryGetProperty("y", out var yProp) ? yProp.GetInt32() : 180;
                var dName = nElem.TryGetProperty("departmentName", out var dnProp) ? dnProp.GetString() : null;
                var rName = nElem.TryGetProperty("roleName", out var rnProp) ? rnProp.GetString() : null;
                int? sla = nElem.TryGetProperty("slaHours", out var slaProp) ? slaProp.GetInt32() : 24;
                var desc = nElem.TryGetProperty("description", out var dscProp) ? dscProp.GetString() : null;

                // Match department and role IDs
                Guid? dId = departments.FirstOrDefault(d => dName != null && d.Name.Contains(dName, StringComparison.OrdinalIgnoreCase))?.Id;
                Guid? rId = roles.FirstOrDefault(r => rName != null && r.Name.Contains(rName, StringComparison.OrdinalIgnoreCase))?.Id;

                nodes.Add(new WorkflowGraphNodeDto(id, type, nodeName, x, y, dId, dName, rId, rName, sla, desc));
            }
        }

        var edges = new List<WorkflowGraphEdgeDto>();
        if (root.TryGetProperty("edges", out var eArr) && eArr.ValueKind == JsonValueKind.Array)
        {
            foreach (var eElem in eArr.EnumerateArray())
            {
                var id = eElem.TryGetProperty("id", out var idProp) ? idProp.GetString() ?? "" : "";
                var src = eElem.TryGetProperty("source", out var srcProp) ? srcProp.GetString() ?? "" : "";
                var tgt = eElem.TryGetProperty("target", out var tgtProp) ? tgtProp.GetString() ?? "" : "";
                var lbl = eElem.TryGetProperty("label", out var lblProp) ? lblProp.GetString() : null;

                if (!string.IsNullOrWhiteSpace(src) && !string.IsNullOrWhiteSpace(tgt))
                {
                    edges.Add(new WorkflowGraphEdgeDto(id, src, tgt, lbl));
                }
            }
        }

        return new WorkflowDraftGraphDto(name, bType, summary, nodes, edges);
    }

    private static WorkflowDraftGraphDto DecomposeTextDeterministically(
        string text,
        string businessType,
        IReadOnlyList<WorkflowLookupItem> departments,
        IReadOnlyList<WorkflowLookupItem> roles)
    {
        var firstLine = text.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault()?.Trim() ?? "Quy trình nghiệp vụ mới";
        if (firstLine.Length > 80) firstLine = firstLine[..80] + "...";

        var lines = text.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries)
            .Select(l => l.Trim())
            .Where(l => l.Length > 3)
            .ToList();

        var nodes = new List<WorkflowGraphNodeDto>();
        var edges = new List<WorkflowGraphEdgeDto>();

        // 1. Start node
        var startNode = new WorkflowGraphNodeDto(
            "node-start",
            "START",
            businessType == "REQUEST" ? "Nộp yêu cầu" : "Khởi tạo công việc",
            80,
            180,
            Trigger: businessType == "REQUEST" ? "REQUEST_SUBMITTED" : "TASK_CREATED");
        nodes.Add(startNode);

        // Extract steps
        var stepLines = lines
            .Where(l => Regex.IsMatch(l, @"^(bước|\d+[\.:\)]|-|\*|giai đoạn)", RegexOptions.IgnoreCase))
            .ToList();

        if (stepLines.Count == 0)
        {
            stepLines = lines.Take(4).ToList();
        }

        var defaultDept = departments.FirstOrDefault();
        var defaultRole = roles.FirstOrDefault();

        string prevNodeId = startNode.Id;
        int currentX = 320;
        int stepIdx = 1;

        foreach (var rawLine in stepLines.Take(5))
        {
            var cleanLine = Regex.Replace(rawLine, @"^(bước\s*\d*[\.:\-]?|\d+[\.:\)]|-|\*)\s*", "", RegexOptions.IgnoreCase).Trim();
            if (string.IsNullOrWhiteSpace(cleanLine)) continue;

            var nodeId = $"node-step-{stepIdx}";
            bool isApproval = cleanLine.Contains("duyệt", StringComparison.OrdinalIgnoreCase) ||
                              cleanLine.Contains("phê chuẩn", StringComparison.OrdinalIgnoreCase) ||
                              cleanLine.Contains("ký", StringComparison.OrdinalIgnoreCase);

            var matchedDept = departments.FirstOrDefault(d => cleanLine.Contains(d.Name, StringComparison.OrdinalIgnoreCase)) ?? defaultDept;
            var matchedRole = roles.FirstOrDefault(r => cleanLine.Contains(r.Name, StringComparison.OrdinalIgnoreCase)) ?? defaultRole;

            var nodeType = isApproval ? "APPROVAL" : "DEPARTMENT";
            var sla = isApproval ? 12 : 24;

            var node = new WorkflowGraphNodeDto(
                nodeId,
                nodeType,
                cleanLine.Length > 40 ? cleanLine[..40] + "..." : cleanLine,
                currentX,
                180,
                DepartmentId: matchedDept?.Id,
                DepartmentName: matchedDept?.Name,
                RoleId: matchedRole?.Id,
                RoleName: matchedRole?.Name,
                SlaHours: sla,
                Description: cleanLine);

            nodes.Add(node);
            edges.Add(new WorkflowGraphEdgeDto($"edge-{stepIdx}", prevNodeId, nodeId, isApproval ? "Chuyển phê duyệt" : "Chuyển bước"));

            prevNodeId = nodeId;
            currentX += 240;
            stepIdx++;
        }

        // If no intermediate steps were parsed, add a default step
        if (nodes.Count == 1)
        {
            var fallbackNode = new WorkflowGraphNodeDto(
                "node-step-1",
                "DEPARTMENT",
                "Tiếp nhận & Xử lý hồ sơ",
                320,
                180,
                DepartmentId: defaultDept?.Id,
                DepartmentName: defaultDept?.Name ?? "Phòng ban chuyên trách",
                SlaHours: 24,
                Description: "Xử lý hồ sơ theo quy định");
            nodes.Add(fallbackNode);
            edges.Add(new WorkflowGraphEdgeDto("edge-1", startNode.Id, fallbackNode.Id, "Bắt đầu"));
            prevNodeId = fallbackNode.Id;
            currentX += 240;
        }

        // End node
        var endNode = new WorkflowGraphNodeDto(
            "node-end",
            "END",
            "Hoàn tất quy trình",
            currentX,
            180,
            CompletionState: "COMPLETED");
        nodes.Add(endNode);
        edges.Add(new WorkflowGraphEdgeDto($"edge-{stepIdx}", prevNodeId, endNode.Id, "Hoàn tất"));

        return new WorkflowDraftGraphDto(
            firstLine,
            businessType,
            $"Quy trình được phân tích từ tài liệu gồm {nodes.Count - 2} bước trung gian và {edges.Count} liên kết chuyển trạng thái.",
            nodes,
            edges);
    }
}
