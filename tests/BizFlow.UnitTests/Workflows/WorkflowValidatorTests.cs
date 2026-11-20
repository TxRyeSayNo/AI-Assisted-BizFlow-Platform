using BizFlow.Application.Workflows;
using BizFlow.Domain.Workflows;

namespace BizFlow.UnitTests.Workflows;

public sealed class WorkflowValidatorTests
{
    private static readonly Guid TenantDept = Guid.NewGuid();
    private static readonly Guid TenantRole = Guid.NewGuid();
    private static readonly HashSet<Guid> ValidDepts = [TenantDept];
    private static readonly HashSet<Guid> ValidRoles = [TenantRole];

    [Fact]
    public void Valid_simple_workflow_passes_validation()
    {
        var json = $$"""
        {
          "nodes": [
            { "id": "start", "type": "START", "name": "Bắt đầu" },
            { "id": "step1", "type": "DEPARTMENT", "name": "Phòng Nhân sự", "departmentId": "{{TenantDept}}", "roleId": "{{TenantRole}}" },
            { "id": "end", "type": "END", "name": "Hoàn tất" }
          ],
          "edges": [
            { "id": "e1", "source": "start", "target": "step1" },
            { "id": "e2", "source": "step1", "target": "end" }
          ]
        }
        """;

        var result = WorkflowValidator.Validate(json, WorkflowBusinessType.Request, ValidDepts, ValidRoles);
        Assert.True(result.IsValid);
        Assert.Empty(result.Errors);
    }

    [Fact]
    public void Missing_start_or_end_event_fails_validation()
    {
        var noStart = $$"""
        {
          "nodes": [
            { "id": "step1", "type": "DEPARTMENT", "name": "Phòng Nhân sự", "departmentId": "{{TenantDept}}" },
            { "id": "end", "type": "END", "name": "Hoàn tất" }
          ],
          "edges": [
            { "id": "e1", "source": "step1", "target": "end" }
          ]
        }
        """;
        var res1 = WorkflowValidator.Validate(noStart, WorkflowBusinessType.Request, ValidDepts, ValidRoles);
        Assert.False(res1.IsValid);
        Assert.Contains(res1.Errors, e => e.Contains("Bắt đầu"));

        var noEnd = $$"""
        {
          "nodes": [
            { "id": "start", "type": "START", "name": "Bắt đầu" },
            { "id": "step1", "type": "DEPARTMENT", "name": "Phòng Nhân sự", "departmentId": "{{TenantDept}}" }
          ],
          "edges": [
            { "id": "e1", "source": "start", "target": "step1" }
          ]
        }
        """;
        var res2 = WorkflowValidator.Validate(noEnd, WorkflowBusinessType.Request, ValidDepts, ValidRoles);
        Assert.False(res2.IsValid);
        Assert.Contains(res2.Errors, e => e.Contains("Kết thúc"));
    }

    [Fact]
    public void Department_referencing_other_tenant_or_empty_fails_validation()
    {
        var foreignDeptId = Guid.NewGuid();
        var json = $$"""
        {
          "nodes": [
            { "id": "start", "type": "START", "name": "Bắt đầu" },
            { "id": "step1", "type": "DEPARTMENT", "name": "Phòng lạ", "departmentId": "{{foreignDeptId}}" },
            { "id": "end", "type": "END", "name": "Hoàn tất" }
          ],
          "edges": [
            { "id": "e1", "source": "start", "target": "step1" },
            { "id": "e2", "source": "step1", "target": "end" }
          ]
        }
        """;

        var result = WorkflowValidator.Validate(json, WorkflowBusinessType.Request, ValidDepts, ValidRoles);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("không tồn tại hoặc không thuộc công ty"));
    }

    [Fact]
    public void Unreachable_node_and_dead_ends_fail_validation()
    {
        var json = $$"""
        {
          "nodes": [
            { "id": "start", "type": "START", "name": "Bắt đầu" },
            { "id": "step1", "type": "DEPARTMENT", "name": "Phòng Nhân sự", "departmentId": "{{TenantDept}}" },
            { "id": "isolated", "type": "DEPARTMENT", "name": "Bước cô lập", "departmentId": "{{TenantDept}}" },
            { "id": "end", "type": "END", "name": "Hoàn tất" }
          ],
          "edges": [
            { "id": "e1", "source": "start", "target": "step1" },
            { "id": "e2", "source": "step1", "target": "end" }
          ]
        }
        """;

        var result = WorkflowValidator.Validate(json, WorkflowBusinessType.Request, ValidDepts, ValidRoles);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("cô lập") || e.Contains("tiếp cận được từ sự kiện Bắt đầu"));
    }

    [Fact]
    public void Decision_gateway_requires_at_least_two_outgoing_branches()
    {
        var json = $$"""
        {
          "nodes": [
            { "id": "start", "type": "START", "name": "Bắt đầu" },
            { "id": "gw", "type": "DECISION", "name": "Phân loại" },
            { "id": "end", "type": "END", "name": "Hoàn tất" }
          ],
          "edges": [
            { "id": "e1", "source": "start", "target": "gw" },
            { "id": "e2", "source": "gw", "target": "end" }
          ]
        }
        """;

        var result = WorkflowValidator.Validate(json, WorkflowBusinessType.Request, ValidDepts, ValidRoles);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("phải có ít nhất 2 nhánh rẽ"));
    }
}

