using BizFlow.Application.Workflows;
using BizFlow.Domain.Workflows;

namespace BizFlow.UnitTests.Workflows;

public sealed class WorkflowRoleScopingTests
{
    private static readonly Guid TenantDept = Guid.NewGuid();
    private static readonly Guid TenantRole = Guid.NewGuid();
    private static readonly Guid TenantUser = Guid.NewGuid();
    private static readonly Guid PlatformRoleId = Guid.NewGuid();

    private static readonly HashSet<Guid> ValidDepts = [TenantDept];
    private static readonly HashSet<Guid> ValidRoles = [TenantRole]; // Does NOT contain PlatformRoleId
    private static readonly HashSet<Guid> ValidUsers = [TenantUser];

    [Fact]
    public void Platform_role_or_foreign_role_in_approval_step_is_rejected()
    {
        var json = $$"""
        {
          "nodes": [
            { "id": "start", "type": "START", "name": "Bắt đầu", "trigger": "REQUEST_CREATED" },
            { 
              "id": "appr1", 
              "type": "APPROVAL", 
              "name": "Duyệt cấp cao", 
              "assignmentMethod": "BUSINESS_ROLE", 
              "roleId": "{{PlatformRoleId}}" 
            },
            { "id": "end", "type": "END", "name": "Hoàn tất", "completionState": "COMPLETED" }
          ],
          "edges": [
            { "id": "e1", "source": "start", "target": "appr1" },
            { "id": "e2", "source": "appr1", "target": "end", "outcomeCode": "APPROVED" }
          ]
        }
        """;

        var result = WorkflowValidator.Validate(json, WorkflowBusinessType.Request, ValidDepts, ValidRoles, ValidUsers);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("không thuộc công ty hiện tại") || e.Contains("cấp nền tảng"));
    }

    [Fact]
    public void Valid_tenant_roles_and_assignment_methods_pass_validation()
    {
        var json = $$"""
        {
          "nodes": [
            { "id": "start", "type": "START", "name": "Bắt đầu", "trigger": "REQUEST_CREATED" },
            { 
              "id": "step1", 
              "type": "DEPARTMENT", 
              "name": "Xử lý phòng ban", 
              "assignmentMethod": "DEPARTMENT_QUEUE", 
              "departmentId": "{{TenantDept}}" 
            },
            { 
              "id": "appr1", 
              "type": "APPROVAL", 
              "name": "Duyệt chức vụ", 
              "assignmentMethod": "BUSINESS_ROLE", 
              "roleId": "{{TenantRole}}" 
            },
            { 
              "id": "appr2", 
              "type": "APPROVAL", 
              "name": "Duyệt trưởng phòng", 
              "assignmentMethod": "DEPARTMENT_MANAGER" 
            },
            { 
              "id": "appr3", 
              "type": "APPROVAL", 
              "name": "Duyệt đích danh", 
              "assignmentMethod": "SPECIFIC_USER", 
              "specificUserId": "{{TenantUser}}" 
            },
            { 
              "id": "conf1", 
              "type": "CONFIRMATION", 
              "name": "Nghiệm thu", 
              "assignmentMethod": "REQUESTER",
              "milestone": "RESULT"
            },
            { "id": "end", "type": "END", "name": "Hoàn tất", "completionState": "COMPLETED" }
          ],
          "edges": [
            { "id": "e1", "source": "start", "target": "step1" },
            { "id": "e2", "source": "step1", "target": "appr1" },
            { "id": "e3", "source": "appr1", "target": "appr2" },
            { "id": "e4", "source": "appr2", "target": "appr3" },
            { "id": "e5", "source": "appr3", "target": "conf1" },
            { "id": "e6", "source": "conf1", "target": "end" }
          ]
        }
        """;

        var result = WorkflowValidator.Validate(json, WorkflowBusinessType.Request, ValidDepts, ValidRoles, ValidUsers);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Human_in_the_loop_rework_cycle_passes_validation()
    {
        // Cycle: Step1 -> Approval -> (REQUEST_CHANGES) -> Step1 (rework)
        // AND Approval -> (APPROVED) -> End
        var json = $$"""
        {
          "nodes": [
            { "id": "start", "type": "START", "name": "Bắt đầu", "trigger": "REQUEST_CREATED" },
            { 
              "id": "step1", 
              "type": "DEPARTMENT", 
              "name": "Chuẩn bị hồ sơ", 
              "assignmentMethod": "REQUESTER" 
            },
            { 
              "id": "appr1", 
              "type": "APPROVAL", 
              "name": "Duyệt hồ sơ", 
              "assignmentMethod": "DEPARTMENT_MANAGER" 
            },
            { "id": "end", "type": "END", "name": "Hoàn tất", "completionState": "COMPLETED" }
          ],
          "edges": [
            { "id": "e1", "source": "start", "target": "step1" },
            { "id": "e2", "source": "step1", "target": "appr1" },
            { "id": "e3", "source": "appr1", "target": "step1", "label": "Yêu cầu sửa đổi", "outcomeCode": "REQUEST_CHANGES" },
            { "id": "e4", "source": "appr1", "target": "end", "label": "Chấp thuận", "outcomeCode": "APPROVED" }
          ]
        }
        """;

        var result = WorkflowValidator.Validate(json, WorkflowBusinessType.Request, ValidDepts, ValidRoles, ValidUsers);
        Assert.True(result.IsValid, string.Join("; ", result.Errors));
    }

    [Fact]
    public void Pure_automatic_decision_cycle_without_human_step_is_rejected()
    {
        // Cycle between decisions only (infinite auto-loop)
        var json = $$"""
        {
          "nodes": [
            { "id": "start", "type": "START", "name": "Bắt đầu", "trigger": "REQUEST_CREATED" },
            { 
              "id": "dec1", 
              "type": "DECISION", 
              "name": "Quyết định 1",
              "conditions": [
                { "id": "c1", "field": "x", "operator": "GT", "value": "10", "targetNodeId": "dec2" },
                { "id": "c2", "field": "default", "operator": "EQ", "value": "", "isDefault": true, "targetNodeId": "end" }
              ]
            },
            { 
              "id": "dec2", 
              "type": "DECISION", 
              "name": "Quyết định 2",
              "conditions": [
                { "id": "c3", "field": "y", "operator": "GT", "value": "10", "targetNodeId": "dec1" },
                { "id": "c4", "field": "default", "operator": "EQ", "value": "", "isDefault": true, "targetNodeId": "end" }
              ]
            },
            { "id": "end", "type": "END", "name": "Hoàn tất", "completionState": "COMPLETED" }
          ],
          "edges": [
            { "id": "e1", "source": "start", "target": "dec1" },
            { "id": "e2", "source": "dec1", "target": "dec2" },
            { "id": "e3", "source": "dec2", "target": "dec1" },
            { "id": "e4", "source": "dec1", "target": "end" },
            { "id": "e5", "source": "dec2", "target": "end" }
          ]
        }
        """;

        var result = WorkflowValidator.Validate(json, WorkflowBusinessType.Request, ValidDepts, ValidRoles, ValidUsers);
        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("vòng lặp rẽ nhánh tự động kín"));
    }
}
