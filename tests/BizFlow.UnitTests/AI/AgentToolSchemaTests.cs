using System.Text.Json.Nodes;
using BizFlow.Application.AI;

namespace BizFlow.UnitTests.AI;

public sealed class AgentToolSchemaTests
{
    [Theory]
    [InlineData("create_task_draft")]
    [InlineData("assign_task")]
    [InlineData("add_comment")]
    [InlineData("route_request")]
    [InlineData("send_meeting_notification")]
    [InlineData("send_department_notification")]
    public void Every_write_tool_declares_required_inputs(string tool)
    {
        Assert.NotNull(AgentToolSchemas.For(tool));
        Assert.NotEmpty(AgentToolSchemas.MissingOrInvalid(tool, new(), DateTimeOffset.UtcNow));
    }

    [Fact]
    public void Whole_company_scope_is_complete_without_a_department_list()
    {
        var args = JsonNode.Parse("""{"topic":"Báo cáo tổng kết Quý 4","recipientScope":"ALL_COMPANY"}""")!.AsObject();
        var missing = AgentToolSchemas.MissingOrInvalid("send_meeting_notification", args, DateTimeOffset.UtcNow);
        Assert.Equal(new[] { "location", "startsAt" }, missing.Keys.Order());
    }

    [Theory]
    [InlineData("ngày mai")]
    [InlineData("2030-01-01T09:00:00")]
    [InlineData("2020-01-01T09:00:00+07:00")]
    public void Meeting_requires_exact_future_time_with_timezone(string time)
    {
        var args = new JsonObject { ["topic"] = "Q4", ["recipientScope"] = "ALL_COMPANY", ["location"] = "Tầng 3", ["startsAt"] = time };
        Assert.Contains("startsAt", AgentToolSchemas.MissingOrInvalid("send_meeting_notification", args, DateTimeOffset.UtcNow).Keys);
    }

    [Fact]
    public void Meeting_time_must_have_explicit_user_evidence_not_just_tomorrow_or_a_generated_clock()
    {
        Assert.Null(AgentTimeEvidence.Find("2030-01-02T09:00:00+07:00", "Asia/Ho_Chi_Minh", ["Họp ngày mai", "Quý 4, toàn công ty"]));
        Assert.NotNull(AgentTimeEvidence.Find("2030-01-02T09:00:00+07:00", "Asia/Ho_Chi_Minh", ["9h sáng mai tại tầng 3"]));
        Assert.NotNull(AgentTimeEvidence.Find("2030-01-02T09:30:00+07:00", "Asia/Ho_Chi_Minh", ["Chín giờ rưỡi sáng mai"]));
        Assert.Null(AgentTimeEvidence.Find("2030-01-02T09:00:00+07:00", "Asia/Ho_Chi_Minh", ["Sửa thành 10h sáng", "9h sáng mai"]));
    }

    [Fact]
    public void Specific_department_scope_cannot_be_empty_or_contain_invalid_ids()
    {
        var args = JsonNode.Parse("""{"title":"Q4","content":"Chuẩn bị báo cáo","recipientScope":"DEPARTMENTS","departmentIds":[]}""")!.AsObject();
        Assert.Contains("departmentIds", AgentToolSchemas.MissingOrInvalid("send_department_notification", args, DateTimeOffset.UtcNow).Keys);
        args["departmentIds"] = new JsonArray("not-an-id");
        Assert.Contains("departmentIds", AgentToolSchemas.MissingOrInvalid("send_department_notification", args, DateTimeOffset.UtcNow).Keys);
    }
}
