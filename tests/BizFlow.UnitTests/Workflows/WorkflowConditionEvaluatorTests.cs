using System.Text.Json;
using BizFlow.Application.Workflows;

namespace BizFlow.UnitTests.Workflows;

public sealed class WorkflowConditionEvaluatorTests
{
    private static JsonElement Payload(string json) =>
        JsonDocument.Parse(json).RootElement;

    [Theory]
    [InlineData("amount", "GT", "20000000", """{"amount": 50000000}""", true)]
    [InlineData("amount", "GT", "20000000", """{"amount": 20000000}""", false)]
    [InlineData("amount", "GT", "20000000", """{"amount": 10000000}""", false)]
    [InlineData("amount", "GTE", "20000000", """{"amount": 20000000}""", true)]
    [InlineData("amount", "GTE", "20000000", """{"amount": 19999999}""", false)]
    [InlineData("amount", "LT", "20000000", """{"amount": 15000000}""", true)]
    [InlineData("amount", "LT", "20000000", """{"amount": 25000000}""", false)]
    [InlineData("amount", "LTE", "20000000", """{"amount": 20000000}""", true)]
    [InlineData("amount", "LTE", "20000000", """{"amount": 20000001}""", false)]
    public void Numeric_comparison_evaluates_correctly(string field, string op, string expected, string json, bool expectedResult)
    {
        var payload = Payload(json);
        var result = WorkflowConditionEvaluator.Evaluate(field, op, expected, payload);
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData("status", "EQ", "HIGH", """{"status": "HIGH"}""", true)]
    [InlineData("status", "EQ", "HIGH", """{"status": "high"}""", true)]
    [InlineData("status", "EQ", "HIGH", """{"status": "LOW"}""", false)]
    [InlineData("status", "NEQ", "HIGH", """{"status": "LOW"}""", true)]
    [InlineData("status", "NEQ", "HIGH", """{"status": "HIGH"}""", false)]
    public void String_comparison_evaluates_case_insensitively(string field, string op, string expected, string json, bool expectedResult)
    {
        var payload = Payload(json);
        var result = WorkflowConditionEvaluator.Evaluate(field, op, expected, payload);
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData("category", "IN", "IT,HARDWARE,SOFTWARE", """{"category": "HARDWARE"}""", true)]
    [InlineData("category", "IN", "IT,HARDWARE,SOFTWARE", """{"category": "FINANCE"}""", false)]
    [InlineData("code", "IN", "10,20,30", """{"code": 20}""", true)]
    [InlineData("code", "IN", "10,20,30", """{"code": 40}""", false)]
    public void In_operator_matches_list_membership(string field, string op, string expected, string json, bool expectedResult)
    {
        var payload = Payload(json);
        var result = WorkflowConditionEvaluator.Evaluate(field, op, expected, payload);
        Assert.Equal(expectedResult, result);
    }

    [Theory]
    [InlineData("notes", "IS_EMPTY", null, """{"notes": ""}""", true)]
    [InlineData("notes", "IS_EMPTY", null, """{"notes": "   "}""", true)]
    [InlineData("notes", "IS_EMPTY", null, """{"notes": null}""", true)]
    [InlineData("notes", "IS_EMPTY", null, """{}""", true)]
    [InlineData("notes", "IS_EMPTY", null, """{"notes": "some notes"}""", false)]
    [InlineData("notes", "IS_NOT_EMPTY", null, """{"notes": "some notes"}""", true)]
    [InlineData("notes", "IS_NOT_EMPTY", null, """{"notes": ""}""", false)]
    [InlineData("items", "IS_EMPTY", null, """{"items": []}""", true)]
    [InlineData("items", "IS_NOT_EMPTY", null, """{"items": [1, 2]}""", true)]
    public void Empty_and_not_empty_operators_handle_strings_arrays_and_nulls(string field, string op, string? expected, string json, bool expectedResult)
    {
        var payload = Payload(json);
        var result = WorkflowConditionEvaluator.Evaluate(field, op, expected, payload);
        Assert.Equal(expectedResult, result);
    }

    [Fact]
    public void Invalid_operator_returns_false()
    {
        var payload = Payload("""{"amount": 100}""");
        var result = WorkflowConditionEvaluator.Evaluate("amount", "INVALID_OP", "100", payload);
        Assert.False(result);
    }
}

