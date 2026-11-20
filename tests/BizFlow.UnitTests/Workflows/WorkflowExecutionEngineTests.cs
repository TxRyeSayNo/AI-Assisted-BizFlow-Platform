using System.Text.Json;
using BizFlow.Application.Workflows;
using BizFlow.Domain.Workflows;

namespace BizFlow.UnitTests.Workflows;

public sealed class WorkflowExecutionEngineTests
{
    private static readonly Guid PurchasingDeptId = Guid.NewGuid();
    private static readonly Guid CfoRoleId = Guid.NewGuid();
    private static readonly Guid RequesterUserId = Guid.NewGuid();
    private static readonly Guid RequesterDeptId = Guid.NewGuid();

    private static readonly HashSet<Guid> ValidDepts = [PurchasingDeptId, RequesterDeptId];
    private static readonly HashSet<Guid> ValidRoles = [CfoRoleId];
    private static readonly HashSet<Guid> ValidUsers = [RequesterUserId];

    private static string GetProcurementWorkflowJson() => $$"""
    {
      "nodes": [
        {
          "id": "node-start",
          "type": "START",
          "name": "Nộp yêu cầu mua sắm thiết bị",
          "trigger": "REQUEST_CREATED"
        },
        {
          "id": "node-mgr-approval",
          "type": "APPROVAL",
          "name": "Trưởng bộ phận phê duyệt",
          "assignmentMethod": "DEPARTMENT_MANAGER",
          "slaHours": 24,
          "approvalCountPolicy": "SINGLE",
          "rejectionReasonRequired": true
        },
        {
          "id": "node-rework",
          "type": "DEPARTMENT",
          "name": "Điều chỉnh hồ sơ mua sắm",
          "assignmentMethod": "REQUESTER",
          "slaHours": 48,
          "requireNotes": true
        },
        {
          "id": "node-decision-budget",
          "type": "DECISION",
          "name": "Xét định mức ngân sách",
          "conditions": [
            {
              "id": "cond-high",
              "field": "amount",
              "operator": "GT",
              "value": "20000000",
              "label": "Chi phí > 20 triệu",
              "targetNodeId": "node-cfo-approval",
              "isDefault": false
            },
            {
              "id": "cond-default",
              "field": "default",
              "operator": "EQ",
              "value": "",
              "label": "Định mức tiêu chuẩn",
              "targetNodeId": "node-purchasing-dept",
              "isDefault": true
            }
          ]
        },
        {
          "id": "node-cfo-approval",
          "type": "APPROVAL",
          "name": "Giám đốc Tài chính (CFO) phê duyệt",
          "assignmentMethod": "BUSINESS_ROLE",
          "roleId": "{{CfoRoleId}}",
          "slaHours": 24,
          "rejectionReasonRequired": true
        },
        {
          "id": "node-purchasing-dept",
          "type": "DEPARTMENT",
          "name": "Phòng Mua sắm đặt hàng & giao nhận",
          "assignmentMethod": "DEPARTMENT_QUEUE",
          "departmentId": "{{PurchasingDeptId}}",
          "slaHours": 72,
          "requireAttachment": true,
          "requireDeliverable": true
        },
        {
          "id": "node-confirmation",
          "type": "CONFIRMATION",
          "name": "Nghiệm thu bàn giao thiết bị",
          "assignmentMethod": "REQUESTER",
          "milestone": "RESULT",
          "requireNotes": true
        },
        {
          "id": "node-end-success",
          "type": "END",
          "name": "Hoàn tất mua sắm",
          "completionState": "COMPLETED"
        },
        {
          "id": "node-end-reject",
          "type": "END",
          "name": "Từ chối mua sắm",
          "completionState": "REJECTED"
        }
      ],
      "edges": [
        { "id": "e1", "source": "node-start", "target": "node-mgr-approval" },
        { "id": "e2", "source": "node-mgr-approval", "target": "node-decision-budget", "outcomeCode": "APPROVED", "label": "Chấp thuận" },
        { "id": "e3", "source": "node-mgr-approval", "target": "node-end-reject", "outcomeCode": "REJECTED", "label": "Từ chối" },
        { "id": "e4", "source": "node-mgr-approval", "target": "node-rework", "outcomeCode": "REQUEST_CHANGES", "label": "Yêu cầu sửa đổi" },
        { "id": "e5", "source": "node-rework", "target": "node-mgr-approval", "outcomeCode": "COMPLETED", "label": "Nộp lại" },
        { "id": "e6", "source": "node-decision-budget", "target": "node-cfo-approval", "conditionId": "cond-high", "label": "amount > 20tr" },
        { "id": "e7", "source": "node-decision-budget", "target": "node-purchasing-dept", "conditionId": "cond-default", "isDefault": true, "label": "Mặc định <= 20tr" },
        { "id": "e8", "source": "node-cfo-approval", "target": "node-purchasing-dept", "outcomeCode": "APPROVED", "label": "CFO duyệt" },
        { "id": "e9", "source": "node-cfo-approval", "target": "node-end-reject", "outcomeCode": "REJECTED", "label": "CFO từ chối" },
        { "id": "e10", "source": "node-purchasing-dept", "target": "node-confirmation", "outcomeCode": "COMPLETED", "label": "Giao hàng xong" },
        { "id": "e11", "source": "node-confirmation", "target": "node-end-success", "outcomeCode": "CONFIRMED", "label": "Nghiệm thu đạt" }
      ]
    }
    """;

    [Fact]
    public void Procurement_workflow_passes_full_schema_validation()
    {
        var json = GetProcurementWorkflowJson();
        var validation = WorkflowValidator.Validate(json, WorkflowBusinessType.Request, ValidDepts, ValidRoles, ValidUsers);

        Assert.True(validation.IsValid, string.Join("; ", validation.Errors));
        Assert.Empty(validation.Errors);
    }

    [Fact]
    public void Execution_high_value_request_routes_through_cfo_approval()
    {
        var json = GetProcurementWorkflowJson();
        var definition = JsonDocument.Parse(json).RootElement;
        var engine = new WorkflowExecutionEngine(null!);

        var payload = JsonDocument.Parse("""{"amount": 50000000, "item": "MacBook Pro M3 Max"}""").RootElement;

        // Step 1: Start -> should resolve to Manager Approval
        var step1 = engine.ResolveStepFromGraph(definition, currentNodeId: null, actionOutcome: null, payload, RequesterUserId, RequesterDeptId);
        Assert.Equal("node-mgr-approval", step1.NextNodeId);
        Assert.Equal("APPROVAL", step1.NodeType);
        Assert.Equal("DEPARTMENT_MANAGER", step1.AssignmentMethod);
        Assert.False(step1.IsCompleted);

        // Step 2: Manager APPROVES -> Decision gate (> 20M) -> Routes to CFO Approval
        var step2 = engine.ResolveStepFromGraph(definition, currentNodeId: "node-mgr-approval", actionOutcome: "APPROVED", payload, RequesterUserId, RequesterDeptId);
        Assert.Equal("node-cfo-approval", step2.NextNodeId);
        Assert.Equal("APPROVAL", step2.NodeType);
        Assert.Equal("BUSINESS_ROLE", step2.AssignmentMethod);
        Assert.Equal(CfoRoleId, step2.RoleId);

        // Step 3: CFO APPROVES -> Routes to Purchasing Dept
        var step3 = engine.ResolveStepFromGraph(definition, currentNodeId: "node-cfo-approval", actionOutcome: "APPROVED", payload, RequesterUserId, RequesterDeptId);
        Assert.Equal("node-purchasing-dept", step3.NextNodeId);
        Assert.Equal("DEPARTMENT", step3.NodeType);
        Assert.Equal(PurchasingDeptId, step3.DepartmentId);
        Assert.True(step3.RequireAttachment);
        Assert.True(step3.RequireDeliverable);

        // Step 4: Purchasing COMPLETED -> Routes to Requester Confirmation
        var step4 = engine.ResolveStepFromGraph(definition, currentNodeId: "node-purchasing-dept", actionOutcome: "COMPLETED", payload, RequesterUserId, RequesterDeptId);
        Assert.Equal("node-confirmation", step4.NextNodeId);
        Assert.Equal("CONFIRMATION", step4.NodeType);
        Assert.Equal("REQUESTER", step4.AssignmentMethod);
        Assert.Equal(RequesterUserId, step4.AssignedUserId);

        // Step 5: Requester CONFIRMED -> Routes to Completed End node
        var step5 = engine.ResolveStepFromGraph(definition, currentNodeId: "node-confirmation", actionOutcome: "CONFIRMED", payload, RequesterUserId, RequesterDeptId);
        Assert.Equal("node-end-success", step5.NextNodeId);
        Assert.Equal("END", step5.NodeType);
        Assert.True(step5.IsCompleted);
        Assert.Equal("COMPLETED", step5.CompletionState);
    }

    [Fact]
    public void Execution_low_value_request_bypasses_cfo_directly_to_purchasing()
    {
        var json = GetProcurementWorkflowJson();
        var definition = JsonDocument.Parse(json).RootElement;
        var engine = new WorkflowExecutionEngine(null!);

        var payload = JsonDocument.Parse("""{"amount": 15000000, "item": "Dell Monitor"}""").RootElement;

        // Step 1: Start -> Manager Approval
        var step1 = engine.ResolveStepFromGraph(definition, currentNodeId: null, actionOutcome: null, payload, RequesterUserId, RequesterDeptId);
        Assert.Equal("node-mgr-approval", step1.NextNodeId);

        // Step 2: Manager APPROVES -> Decision gate (<= 20M: falls into default) -> Bypasses CFO directly to Purchasing
        var step2 = engine.ResolveStepFromGraph(definition, currentNodeId: "node-mgr-approval", actionOutcome: "APPROVED", payload, RequesterUserId, RequesterDeptId);
        Assert.Equal("node-purchasing-dept", step2.NextNodeId);
        Assert.Equal("DEPARTMENT", step2.NodeType);
        Assert.Equal(PurchasingDeptId, step2.DepartmentId);
    }

    [Fact]
    public void Execution_manager_rejection_routes_to_reject_end_node()
    {
        var json = GetProcurementWorkflowJson();
        var definition = JsonDocument.Parse(json).RootElement;
        var engine = new WorkflowExecutionEngine(null!);

        var payload = JsonDocument.Parse("""{"amount": 10000000}""").RootElement;

        var step = engine.ResolveStepFromGraph(definition, currentNodeId: "node-mgr-approval", actionOutcome: "REJECTED", payload, RequesterUserId, RequesterDeptId);
        Assert.Equal("node-end-reject", step.NextNodeId);
        Assert.Equal("END", step.NodeType);
        Assert.True(step.IsCompleted);
        Assert.Equal("REJECTED", step.CompletionState);
    }

    [Fact]
    public void Execution_manager_request_changes_routes_to_rework_step()
    {
        var json = GetProcurementWorkflowJson();
        var definition = JsonDocument.Parse(json).RootElement;
        var engine = new WorkflowExecutionEngine(null!);

        var payload = JsonDocument.Parse("""{"amount": 10000000}""").RootElement;

        // Manager asks for changes
        var reworkStep = engine.ResolveStepFromGraph(definition, currentNodeId: "node-mgr-approval", actionOutcome: "REQUEST_CHANGES", payload, RequesterUserId, RequesterDeptId);
        Assert.Equal("node-rework", reworkStep.NextNodeId);
        Assert.Equal("DEPARTMENT", reworkStep.NodeType);
        Assert.Equal("REQUESTER", reworkStep.AssignmentMethod);
        Assert.Equal(RequesterUserId, reworkStep.AssignedUserId);

        // Requester re-submits (COMPLETED) -> Goes back to Manager Approval
        var resubmittedStep = engine.ResolveStepFromGraph(definition, currentNodeId: "node-rework", actionOutcome: "COMPLETED", payload, RequesterUserId, RequesterDeptId);
        Assert.Equal("node-mgr-approval", resubmittedStep.NextNodeId);
    }
}

