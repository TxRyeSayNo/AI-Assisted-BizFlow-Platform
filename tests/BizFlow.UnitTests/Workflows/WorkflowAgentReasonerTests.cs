using System.Text.Json;
using BizFlow.Application.AI;
using BizFlow.Application.Common;
using BizFlow.Application.Workflows;
using BizFlow.Infrastructure.Workflows;

namespace BizFlow.UnitTests.Workflows;

public sealed class WorkflowAgentReasonerTests
{
    private sealed class Model(params string[] responses) : IAiModelClient
    {
        public bool IsConfigured => true;
        private readonly Queue<string> queue = new(responses);
        public Task<AiModelResponse> CompleteAsync(string prompt,string system,CancellationToken ct=default) => Task.FromResult(new AiModelResponse(queue.Dequeue(),"test",1,10,10));
    }
    private static WorkflowAgentRow Job(string fields="{\"budget\":1200}") => new()
    {
        Instructions="Prepare a handover", Mode="AUTO", InputJson="{\"title\":\"Request\",\"step\":\"Processing\",\"fields\":"+fields+",\"requiredFields\":[\"budget\"]}"
    };
    private static string Result(string source="fields.budget", string quote="1200") => JsonSerializer.Serialize(new {result=new WorkflowAgentResult("Summary","Deliverable",false,[],[new(source,quote)])},WorkflowAgentContract.Json);

    [Theory]
    [InlineData("approve_request")]
    [InlineData("send_email")]
    [InlineData("read_previous_results")]
    public async Task Refuses_tools_outside_the_published_snapshot(string tool)
    {
        var error=await Assert.ThrowsAsync<ApplicationFault>(()=>new WorkflowAgentReasoner(new Model(JsonSerializer.Serialize(new{tool}))).EvaluateAsync(Job(),default));
        Assert.Equal("AI.TOOL_DENIED",error.Code);
    }
    [Fact]
    public async Task Does_not_accept_a_deliverable_before_reading_input()
    {
        var error=await Assert.ThrowsAsync<ApplicationFault>(()=>new WorkflowAgentReasoner(new Model(Result())).EvaluateAsync(Job(),default));
        Assert.Equal("AI.INVALID_OUTPUT",error.Code);
    }
    [Fact]
    public async Task Required_missing_data_forces_human_review_despite_the_models_claim()
    {
        var evaluation=await new WorkflowAgentReasoner(new Model("""{"tool":"read_inputs"}""",Result("title","Request"))).EvaluateAsync(Job("{\"budget\":null}"),default);
        Assert.True(evaluation.Result.NeedsHumanReview);
        Assert.Contains("prepare_deliverable",evaluation.TraceJson);
    }
    [Fact]
    public async Task Repeated_tool_calls_stop_instead_of_looping_forever()
    {
        var error=await Assert.ThrowsAsync<ApplicationFault>(()=>new WorkflowAgentReasoner(new Model("""{"tool":"read_inputs"}""","""{"tool":"read_inputs"}""")).EvaluateAsync(Job(),default));
        Assert.Equal("AI.TOOL_DENIED",error.Code);
    }
}
