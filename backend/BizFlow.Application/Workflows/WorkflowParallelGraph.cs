using System.Text.Json;
using BizFlow.Application.Common;

namespace BizFlow.Application.Workflows;

/// <summary>First parallel contract: independent human steps split and join in one acyclic group.</summary>
public static class WorkflowParallelGraph
{
    public static void Validate(JsonElement graph)
    {
        var nodes = graph.GetProperty("nodes").EnumerateArray().ToDictionary(n => n.GetProperty("id").GetString()!, n => n);
        if (!nodes.Values.Any(n => Type(n) is "PARALLEL_SPLIT" or "PARALLEL_JOIN")) return;
        var edges = graph.GetProperty("edges").EnumerateArray().Select(e => (From:e.GetProperty("source").GetString()!, To:e.GetProperty("target").GetString()!)).ToArray();
        var degree = nodes.Keys.ToDictionary(id => id, id => edges.Count(e => e.To == id));
        var queue = new Queue<string>(degree.Where(x => x.Value == 0).Select(x => x.Key)); var count = 0;
        while (queue.TryDequeue(out var current)) { count++; foreach (var e in edges.Where(e => e.From == current)) if (--degree[e.To] == 0) queue.Enqueue(e.To); }
        if (count != nodes.Count) throw Invalid("Quy trình có nhánh song song hiện cần đồ thị không chu trình.");
        var usedJoins = new HashSet<string>();
        foreach (var split in nodes.Where(n => Type(n.Value) == "PARALLEL_SPLIT"))
        {
            var branches = edges.Where(e => e.From == split.Key).Select(e => e.To).ToArray();
            if (branches.Length < 2 || branches.Distinct().Count() != branches.Length) throw Invalid("Cổng tách cần ít nhất hai nhánh độc lập.");
            string? join = null;
            foreach (var branch in branches)
            {
                if (!nodes.TryGetValue(branch, out var node) || Type(node) is not ("DEPARTMENT" or "APPROVAL" or "CONFIRMATION") || edges.Count(e => e.To == branch) != 1)
                    throw Invalid("Mỗi nhánh song song cần một bước xử lý độc lập.");
                var outgoing = edges.Where(e => e.From == branch).ToArray();
                if (outgoing.Length != 1 || !nodes.TryGetValue(outgoing[0].To, out var target) || Type(target) != "PARALLEL_JOIN")
                    throw Invalid("Các bước song song phải cùng đi vào một cổng gộp.");
                join ??= outgoing[0].To;
                if (join != outgoing[0].To) throw Invalid("Các nhánh phải có cùng cổng gộp.");
            }
            if (!usedJoins.Add(join!) || edges.Count(e => e.To == join) != branches.Length || edges.Count(e => e.From == join) != 1)
                throw Invalid("Cổng gộp phải nhận đủ các nhánh của một cổng tách và có một bước tiếp theo.");
        }
        if (nodes.Count(n => Type(n.Value) == "PARALLEL_JOIN") != usedJoins.Count) throw Invalid("Có cổng gộp chưa gắn với cổng tách.");
    }
    private static string? Type(JsonElement n) => n.GetProperty("type").GetString()?.ToUpperInvariant();
    private static ApplicationFault Invalid(string text) => new(FaultKind.Validation,"WORKFLOW.PARALLEL_INVALID",text);
}
