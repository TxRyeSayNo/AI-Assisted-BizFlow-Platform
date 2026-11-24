using System.Text.Json;
using System.Text.Json.Nodes;
using BizFlow.Application.Common;
using BizFlow.Application.Workflows;

namespace BizFlow.UnitTests.Workflows;

public sealed class WorkflowApprovalMatrixTests
{
    private static readonly Guid Low = Guid.NewGuid();
    private static readonly Guid High = Guid.NewGuid();
    private static JsonElement Definition() => JsonSerializer.SerializeToElement(new {
        formFields=new[]{new {key="amount",label="Giá trị",type="number",required=true}},
        nodes=new[]{new {id="approve",type="APPROVAL",assignmentMethod="MATRIX",approvalMatrix=new WorkflowApprovalMatrixConfig("amount",
            [new(1000,Low,"ANY"),new(null,High,"ALL")])}}
    },new JsonSerializerOptions(JsonSerializerDefaults.Web));
    [Theory]
    [InlineData(0,false)] [InlineData(1000,false)] [InlineData(1000.01,true)] [InlineData(999999,true)]
    public void Inclusive_limits_choose_one_role_and_policy_without_changing_the_published_node(decimal amount,bool high)
    {
        var definition=Definition();WorkflowApprovalMatrix.Validate(definition,new HashSet<Guid>{Low,High});
        var node=definition.GetProperty("nodes")[0];
        var resolved=WorkflowApprovalMatrix.Resolve(node,JsonSerializer.SerializeToElement(new{amount}));
        Assert.Equal(high?High:Low,resolved.GetProperty("roleId").GetGuid());
        Assert.Equal(high?"ALL":"ANY",resolved.GetProperty("approvalCountPolicy").GetString());
        Assert.Equal("MATRIX",node.GetProperty("assignmentMethod").GetString());
    }
    [Theory]
    [InlineData("negative")] [InlineData("unordered")] [InlineData("no-unlimited")] [InlineData("missing-role")]
    [InlineData("optional-field")] [InlineData("unknown-field")] [InlineData("wrong-type")] [InlineData("bad-policy")]
    public void Invalid_or_incomplete_matrix_fails_closed(string change)
    {
        var graph=JsonNode.Parse(Definition().GetRawText())!;
        var matrix=graph["nodes"]![0]!["approvalMatrix"]!;
        switch(change)
        {
            case "negative":matrix["tiers"]![0]!["upperLimit"]=-1;break;
            case "unordered":matrix["tiers"]!.AsArray().Insert(1,JsonSerializer.SerializeToNode(new WorkflowApprovalTier(500,High)));break;
            case "no-unlimited":matrix["tiers"]![1]!["upperLimit"]=5000;break;
            case "missing-role":matrix["tiers"]![0]!["roleId"]=Guid.NewGuid().ToString();break;
            case "optional-field":graph["formFields"]![0]!["required"]=false;break;
            case "unknown-field":matrix["amountField"]="missing";break;
            case "wrong-type":graph["nodes"]![0]!["type"]="DEPARTMENT";break;
            case "bad-policy":matrix["tiers"]![0]!["policy"]="AUTO_APPROVE";break;
        }
        var error=Assert.Throws<ApplicationFault>(()=>WorkflowApprovalMatrix.Validate(JsonSerializer.SerializeToElement(graph),new HashSet<Guid>{Low,High}));
        Assert.Equal("WORKFLOW.MATRIX_INVALID",error.Code);
    }
    [Theory]
    [InlineData("{}")] [InlineData("{\"amount\":-1}")] [InlineData("{\"amount\":\"100\"}")]
    public void Missing_negative_or_non_numeric_input_never_falls_through_to_an_approver(string payload)
    {
        using var values=JsonDocument.Parse(payload);
        Assert.Throws<ApplicationFault>(()=>WorkflowApprovalMatrix.Resolve(Definition().GetProperty("nodes")[0],values.RootElement));
    }
}
