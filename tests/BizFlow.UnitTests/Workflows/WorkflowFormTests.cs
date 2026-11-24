using System.Text.Json;
using BizFlow.Application.Common;
using BizFlow.Application.Workflows;

namespace BizFlow.UnitTests.Workflows;

public sealed class WorkflowFormTests
{
    private static JsonElement Json(string value) => JsonSerializer.Deserialize<JsonElement>(value);
    private static JsonElement Definition => Json("""{"formFields":[{"key":"amount","label":"Dự toán","type":"number","required":true,"min":0,"max":1000},{"key":"kind","label":"Loại","type":"select","options":["A","B"]}]}""");
    [Theory]
    [InlineData("{}")]
    [InlineData("{\"amount\":-1}")]
    [InlineData("{\"amount\":1001}")]
    [InlineData("{\"amount\":\"100\"}")]
    [InlineData("{\"amount\":100,\"kind\":\"C\"}")]
    [InlineData("{\"amount\":100,\"unexpected\":true}")]
    [InlineData("{\"amount\":100,\"amount\":200}")]
    public void Rejects_missing_out_of_range_untyped_unknown_and_duplicate_values(string values) =>
        Assert.Throws<ApplicationFault>(() => WorkflowForm.Validate(Definition, Json(values)));
    [Fact]
    public void Accepts_typed_fields_and_optional_omissions() => WorkflowForm.Validate(Definition, Json("""{"amount":125,"kind":"A"}"""));
    [Theory]
    [InlineData("priority")]
    [InlineData("requesterId")]
    [InlineData("serviceId")]
    [InlineData("title")]
    public void Definitions_cannot_override_server_evidence(string key) => Assert.Throws<ApplicationFault>(() =>
        WorkflowForm.Fields(Json(JsonSerializer.Serialize(new { formFields = new[] {new {key,label="Không hợp lệ",type="text"}} }))));
}
