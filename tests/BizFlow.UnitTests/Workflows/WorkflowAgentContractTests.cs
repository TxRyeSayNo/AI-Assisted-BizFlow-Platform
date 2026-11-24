using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Workflows;

namespace BizFlow.UnitTests.Workflows;

public sealed class WorkflowAgentContractTests
{
    [Theory]
    [InlineData("APPROVAL","REVIEW",false,"SINGLE")]
    [InlineData("CONFIRMATION","AUTO",false,"SINGLE")]
    [InlineData("DEPARTMENT","AUTO",true,"SINGLE")]
    [InlineData("DEPARTMENT","AUTO",false,"ALL")]
    [InlineData("DEPARTMENT","UNSAFE",false,"SINGLE")]
    public void Rejects_policies_that_bypass_human_approval_or_required_attachments(string type,string mode,bool attachment,string count)
    {
        using var node=JsonDocument.Parse(JsonSerializer.Serialize(new{type,requireAttachment=attachment,approvalCountPolicy=count,agent=new WorkflowAgentPolicy(mode,"Analyze",[])}));
        Assert.Throws<ApplicationFault>(()=>WorkflowAgentContract.Policy(node.RootElement));
    }
    [Theory]
    [InlineData("AUTO")]
    [InlineData("REVIEW")]
    public void Allows_explicit_department_processing_with_selected_fields(string mode)
    {
        using var node=JsonDocument.Parse(JsonSerializer.Serialize(new{type="DEPARTMENT",agent=new WorkflowAgentPolicy(mode,"Prepare a handover",["budget"])}));
        Assert.Equal(mode,WorkflowAgentContract.Policy(node.RootElement)!.Mode);
    }
    [Theory]
    [InlineData("fields.other","1200")]
    [InlineData("fields.budget","invented")]
    [InlineData("fields.budget","")]
    public void Requires_evidence_from_exact_allowed_sources(string source,string quote)
    {
        using var result=JsonDocument.Parse(JsonSerializer.Serialize(new WorkflowAgentResult("Summary","Deliverable",false,[],[new(source,quote)]),WorkflowAgentContract.Json));
        Assert.Throws<ApplicationFault>(()=>WorkflowAgentContract.Result(result.RootElement,new Dictionary<string,string>{{"fields.budget","1200"}}));
    }
    [Fact]
    public void Missing_review_flag_is_not_interpreted_as_permission_for_autonomous_completion()
    {
        using var result=JsonDocument.Parse("""{"summary":"S","deliverable":"D","findings":[],"evidence":[{"source":"title","quote":"A"}]}""");
        Assert.Throws<ApplicationFault>(()=>WorkflowAgentContract.Result(result.RootElement,new Dictionary<string,string>{{"title","A"}}));
    }
    [Fact]
    public void Unknown_input_fields_and_task_workflows_are_rejected()
    {
        using var graph=JsonDocument.Parse(JsonSerializer.Serialize(new{nodes=new[]{new{type="DEPARTMENT",agent=new WorkflowAgentPolicy("AUTO","Analyze",["secret"])}},formFields=Array.Empty<object>()}));
        Assert.Throws<ApplicationFault>(()=>WorkflowAgentContract.Validate(graph.RootElement));
        Assert.Throws<ApplicationFault>(()=>WorkflowAgentContract.Validate(graph.RootElement,false));
    }
}
