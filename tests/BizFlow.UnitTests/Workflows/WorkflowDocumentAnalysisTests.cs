using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Application.Security;
using BizFlow.Application.Workflows;
using BizFlow.Domain.Audit;
using BizFlow.Domain.Security;
using BizFlow.Domain.Workflows;
using Xunit;

namespace BizFlow.UnitTests.Workflows;

public sealed class WorkflowDocumentAnalysisTests
{
    private sealed class FakeTenantContext(Guid? tenantId, Guid? userId) : ITenantContext
    {
        public Guid? UserId => userId;
        public Guid? TenantId => tenantId;
    }

    private sealed class FakeWorkflowStore : IWorkflowLibraryStore
    {
        public List<WorkflowLookupItem> Departments { get; } = [
            new(Guid.NewGuid(), "Phòng Hành chính - Nhân sự", "HR"),
            new(Guid.NewGuid(), "Phòng Công nghệ Thông tin", "IT"),
            new(Guid.NewGuid(), "Phòng Tài chính - Kế toán", "FIN")
        ];

        public List<WorkflowLookupItem> Roles { get; } = [
            new(Guid.NewGuid(), "Nhân viên", "EMPLOYEE"),
            new(Guid.NewGuid(), "Trưởng phòng", "MANAGER"),
            new(Guid.NewGuid(), "Quản trị viên", "COMPANY_ADMIN")
        ];

        public Task<IReadOnlyList<WorkflowLookupItem>> GetActiveDepartmentsAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkflowLookupItem>>(Departments);

        public Task<IReadOnlyList<WorkflowLookupItem>> GetActiveRolesAsync(Guid tenantId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkflowLookupItem>>(Roles);

        public List<WorkflowUserLookupItem> Users { get; } = [];

        public Task<IReadOnlyList<WorkflowUserLookupItem>> GetActiveUsersAsync(Guid tenantId, Guid? departmentId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<WorkflowUserLookupItem>>(Users);

        public Task<WorkflowPage> ListAsync(Guid tenantId, WorkflowFilter filter, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<WorkflowVersionView?> ReadVersionAsync(Guid tenantId, Guid versionId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<WorkflowRow> CreateAsync(WorkflowDefinition workflow, WorkflowVersion version, AuditLog audit, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IWorkflowVersionTransaction?> BeginNextVersionAsync(Guid tenantId, Guid workflowId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<WorkflowVersionView?> SaveDraftAsync(Guid tenantId, Guid versionId, string definitionJson, IReadOnlyList<WorkflowStepDraftInput>? steps, IReadOnlyList<WorkflowTransitionDraftInput>? transitions, string? name, AuditLog audit, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<WorkflowRow?> UpdateNameAsync(Guid tenantId, Guid workflowId, string name, AuditLog audit, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<WorkflowVersionView?> PublishVersionAsync(Guid tenantId, Guid versionId, Guid? associateServiceId, AuditLog audit, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<bool> DeleteWorkflowAsync(Guid tenantId, Guid workflowId, AuditLog audit, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<WorkflowRow?> UpdateStatusAsync(Guid tenantId, Guid workflowId, WorkflowStatus status, AuditLog audit, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<WorkflowServiceLookupItem>> GetServicesAsync(Guid tenantId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<IReadOnlyList<WorkflowVersionSummary>> GetWorkflowVersionsAsync(Guid tenantId, Guid workflowId, CancellationToken cancellationToken) => throw new NotImplementedException();
        public Task<WorkflowDefinition?> GetWorkflowByIdAsync(Guid tenantId, Guid workflowId, CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    private sealed class MockAiModelClient : IAiModelClient
    {
        public bool IsConfigured { get; set; } = true;
        public string? NextResponseContent { get; set; }

        public Task<AiModelResponse> CompleteAsync(string prompt, string systemInstructions, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new AiModelResponse(
                NextResponseContent ?? "{}",
                "mock-model",
                45,
                120,
                240));
        }
    }

    private readonly Guid _tenantId = Guid.NewGuid();
    private readonly Guid _userId = Guid.NewGuid();
    private readonly FakeWorkflowStore _store = new();
    private readonly MockAiModelClient _modelClient = new();

    private WorkflowDocumentAnalysisService CreateSut(bool useLlm = false)
    {
        return new WorkflowDocumentAnalysisService(
            new FakeTenantContext(_tenantId, _userId),
            _store,
            useLlm ? _modelClient : null);
    }

    [Fact]
    public async Task TestE1_AnalyzeText_Deterministic_ExtractsStepsRolesAndTransitions()
    {
        var sut = CreateSut(useLlm: false);

        var docText = """
        Quy trình thanh toán chi phí công tác
        Bước 1: Tiếp nhận đề nghị thanh toán và kiểm tra hóa đơn chứng từ
        Bước 2: Trưởng phòng Tài chính - Kế toán phê duyệt khoản chi
        Bước 3: Chi tiền mặt hoặc chuyển khoản ngân hàng cho nhân viên
        """;

        var result = await sut.AnalyzeTextAsync(docText, "REQUEST", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("thanh toán chi phí", result.WorkflowName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("REQUEST", result.BusinessType);
        Assert.True(result.Nodes.Count >= 4); // START, Step 1, Step 2 (Approval), Step 3, END

        // Check start and end nodes
        var startNode = result.Nodes.FirstOrDefault(n => n.Type == "START");
        var endNode = result.Nodes.FirstOrDefault(n => n.Type == "END");
        Assert.NotNull(startNode);
        Assert.NotNull(endNode);

        // Check approval step
        var approvalNode = result.Nodes.FirstOrDefault(n => n.Type == "APPROVAL");
        Assert.NotNull(approvalNode);
        Assert.Equal(12, approvalNode.SlaHours);

        // Check edges connect start to end sequentially
        Assert.NotEmpty(result.Edges);
        Assert.Contains(result.Edges, e => e.Source == startNode.Id);
        Assert.Contains(result.Edges, e => e.Target == endNode.Id);
    }

    [Fact]
    public async Task TestE2_AnalyzeDocument_DocxStream_ExtractsAndParsesWorkflow()
    {
        var sut = CreateSut(useLlm: false);

        // Generate a valid minimal in-memory .docx archive with word/document.xml
        using var ms = new MemoryStream();
        using (var zip = new ZipArchive(ms, ZipArchiveMode.Create, true))
        {
            var entry = zip.CreateEntry("word/document.xml");
            using var s = entry.Open();
            using var w = new StreamWriter(s, Encoding.UTF8);
            w.Write("""
            <?xml version="1.0" encoding="UTF-8" standalone="yes"?>
            <w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
              <w:body>
                <w:p><w:r><w:t>Quy trình onboarding nhân viên mới</w:t></w:r></w:p>
                <w:p><w:r><w:t>Bước 1: Chuẩn bị hợp đồng và tài liệu hội nhập</w:t></w:r></w:p>
                <w:p><w:r><w:t>Bước 2: Cấp phát trang thiết bị và tài khoản phần mềm</w:t></w:r></w:p>
                <w:p><w:r><w:t>Bước 3: Trưởng phòng phê duyệt hoàn tất tiếp nhận</w:t></w:r></w:p>
              </w:body>
            </w:document>
            """);
        }
        ms.Position = 0;

        var result = await sut.AnalyzeDocumentAsync(ms, "quy_trinh_onboarding.docx", "TASK", CancellationToken.None);

        Assert.NotNull(result);
        Assert.Contains("onboarding nhân viên", result.WorkflowName, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("TASK", result.BusinessType);
        Assert.True(result.Nodes.Count >= 4);
    }

    [Fact]
    public async Task TestE3_AnalyzeText_WithLlm_UsesLlmStructuredOutput()
    {
        var sut = CreateSut(useLlm: true);

        _modelClient.NextResponseContent = """
        {
          "workflowName": "Quy trình mua sắm trang thiết bị CNTT",
          "businessType": "REQUEST",
          "summaryExplanation": "Quy trình mua sắm từ lúc tạo đề xuất đến khi bàn giao và thanh toán nghiệm thu.",
          "nodes": [
            { "id": "node-start", "type": "START", "name": "Nộp đề xuất mua sắm", "x": 80, "y": 180 },
            { "id": "node-1", "type": "DEPARTMENT", "name": "Phòng CNTT thẩm định kỹ thuật", "departmentName": "Phòng Công nghệ Thông tin", "slaHours": 24, "x": 320, "y": 180 },
            { "id": "node-2", "type": "APPROVAL", "name": "Ban Giám đốc phê duyệt ngân sách", "roleName": "Quản trị viên", "slaHours": 12, "x": 560, "y": 180 },
            { "id": "node-end", "type": "END", "name": "Bàn giao thiết bị", "x": 800, "y": 180 }
          ],
          "edges": [
            { "id": "e-1", "source": "node-start", "target": "node-1", "label": "Gửi thẩm định" },
            { "id": "e-2", "source": "node-1", "target": "node-2", "label": "Chuyển phê duyệt" },
            { "id": "e-3", "source": "node-2", "target": "node-end", "label": "Phê duyệt thành công" }
          ]
        }
        """;

        var result = await sut.AnalyzeTextAsync("Quy trình mua sắm CNTT...", "REQUEST", CancellationToken.None);

        Assert.Equal("Quy trình mua sắm trang thiết bị CNTT", result.WorkflowName);
        Assert.Equal(4, result.Nodes.Count);
        Assert.Equal(3, result.Edges.Count);
        Assert.Equal("APPROVAL", result.Nodes[2].Type);
        Assert.NotNull(result.Nodes[1].DepartmentId); // Department ID mapped to store
        Assert.NotNull(result.Nodes[2].RoleId); // Role ID mapped to store
    }

    [Fact]
    public async Task TestE4_GeneratedDraftGraph_ConformsToWorkflowValidator()
    {
        var sut = CreateSut(useLlm: false);

        var docText = """
        Quy trình xin nghỉ phép
        Bước 1: Nhân viên nộp đơn xin nghỉ
        Bước 2: Trưởng phòng phê duyệt đơn nghỉ
        Bước 3: Phòng Hành chính - Nhân sự cập nhật chấm công
        """;

        var result = await sut.AnalyzeTextAsync(docText, "REQUEST", CancellationToken.None);

        // Convert the generated graph into JSON format expected by WorkflowValidator
        var graphJson = JsonSerializer.Serialize(new
        {
            nodes = result.Nodes.Select(n => new
            {
                id = n.Id,
                type = n.Type,
                name = n.Name,
                departmentId = n.DepartmentId?.ToString(),
                roleId = n.RoleId?.ToString()
            }),
            edges = result.Edges.Select(e => new
            {
                id = e.Id,
                source = e.Source,
                target = e.Target,
                label = e.Label
            })
        });

        var deptIds = _store.Departments.Select(d => d.Id).ToHashSet();
        var roleIds = _store.Roles.Select(r => r.Id).ToHashSet();

        var validationResult = WorkflowValidator.Validate(
            graphJson,
            WorkflowBusinessType.Request,
            deptIds,
            roleIds);

        Assert.True(validationResult.IsValid, $"Validation failed with errors: {string.Join("; ", validationResult.Errors)}");
        Assert.Empty(validationResult.Errors);
    }
}

