using System.Globalization;
using System.Text.Json;

namespace BizFlow.Application.Workflows;

public static class WorkflowConditionEvaluator
{
    public static readonly HashSet<string> ValidOperators = new(StringComparer.OrdinalIgnoreCase)
    {
        "EQ", "=",
        "NEQ", "!=", "<>",
        "GT", ">",
        "GTE", ">=",
        "LT", "<",
        "LTE", "<=",
        "IN",
        "IS_EMPTY",
        "IS_NOT_EMPTY"
    };

    public static bool Evaluate(string fieldKey, string op, string? expectedValue, JsonElement payload)
    {
        if (string.IsNullOrWhiteSpace(fieldKey) || string.IsNullOrWhiteSpace(op))
            return false;

        var normalizedOp = op.Trim().ToUpperInvariant();
        var actualElement = FindProperty(payload, fieldKey.Trim());

        if (!actualElement.HasValue || actualElement.Value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return normalizedOp switch
            {
                "IS_EMPTY" => true,
                "IS_NOT_EMPTY" => false,
                "NEQ" or "!=" or "<>" => !string.IsNullOrEmpty(expectedValue),
                _ => false
            };
        }

        var element = actualElement.Value;

        if (normalizedOp is "IS_EMPTY")
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => string.IsNullOrWhiteSpace(element.GetString()),
                JsonValueKind.Array => element.GetArrayLength() == 0,
                _ => false
            };
        }

        if (normalizedOp is "IS_NOT_EMPTY")
        {
            return element.ValueKind switch
            {
                JsonValueKind.String => !string.IsNullOrWhiteSpace(element.GetString()),
                JsonValueKind.Array => element.GetArrayLength() > 0,
                _ => true
            };
        }

        expectedValue ??= "";
        var trimmedExpected = expectedValue.Trim();

        // 1. Numeric Comparison
        if (TryGetDecimal(element, out var actualNum) && decimal.TryParse(trimmedExpected, NumberStyles.Any, CultureInfo.InvariantCulture, out var expectedNum))
        {
            return normalizedOp switch
            {
                "EQ" or "=" => actualNum == expectedNum,
                "NEQ" or "!=" or "<>" => actualNum != expectedNum,
                "GT" or ">" => actualNum > expectedNum,
                "GTE" or ">=" => actualNum >= expectedNum,
                "LT" or "<" => actualNum < expectedNum,
                "LTE" or "<=" => actualNum <= expectedNum,
                "IN" => trimmedExpected.Split(',', ';').Any(part => decimal.TryParse(part.Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out var p) && p == actualNum),
                _ => false
            };
        }

        // 2. Boolean Comparison
        if (element.ValueKind is JsonValueKind.True or JsonValueKind.False && bool.TryParse(trimmedExpected, out var expectedBool))
        {
            var actualBool = element.GetBoolean();
            return normalizedOp switch
            {
                "EQ" or "=" => actualBool == expectedBool,
                "NEQ" or "!=" or "<>" => actualBool != expectedBool,
                _ => false
            };
        }

        // 3. String Comparison
        var actualStr = element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? "",
            JsonValueKind.Number => element.GetRawText(),
            _ => element.ToString()
        };

        return normalizedOp switch
        {
            "EQ" or "=" => string.Equals(actualStr.Trim(), trimmedExpected, StringComparison.OrdinalIgnoreCase),
            "NEQ" or "!=" or "<>" => !string.Equals(actualStr.Trim(), trimmedExpected, StringComparison.OrdinalIgnoreCase),
            "IN" => trimmedExpected.Split(',', ';').Select(p => p.Trim()).Contains(actualStr.Trim(), StringComparer.OrdinalIgnoreCase),
            _ => false
        };
    }

    private static JsonElement? FindProperty(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Object) return null;

        var parts = path.Split('.');
        var current = element;

        foreach (var part in parts)
        {
            bool found = false;
            foreach (var prop in current.EnumerateObject())
            {
                if (string.Equals(prop.Name, part, StringComparison.OrdinalIgnoreCase))
                {
                    current = prop.Value;
                    found = true;
                    break;
                }
            }
            if (!found) return null;
        }

        return current;
    }

    private static bool TryGetDecimal(JsonElement element, out decimal result)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out result))
            return true;

        if (element.ValueKind == JsonValueKind.String && decimal.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            return true;

        result = 0;
        return false;
    }
}

