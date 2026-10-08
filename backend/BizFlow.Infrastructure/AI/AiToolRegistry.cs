using System;
using System.Collections.Generic;
using System.Linq;
using BizFlow.Application.AI;

namespace BizFlow.Infrastructure.AI;

public sealed class AiToolRegistry : IAiToolRegistry
{
    private static readonly Dictionary<string, AiToolDefinition> RegisteredTools = new(StringComparer.OrdinalIgnoreCase)
    {
        ["create_task_draft"] = new(
            Name: "create_task_draft",
            Description: "Creates a draft task proposal in the tenant workspace",
            RequiredPermission: "tasks.create",
            RequiresConfirmation: true),

        ["assign_task"] = new(
            Name: "assign_task",
            Description: "Assigns an existing task to an authorized assignee",
            RequiredPermission: "tasks.assign",
            RequiresConfirmation: true),

        ["add_comment"] = new(
            Name: "add_comment",
            Description: "Adds a collaboration comment to a task or request",
            RequiredPermission: "comments.create",
            RequiresConfirmation: false),

        ["route_request"] = new(
            Name: "route_request",
            Description: "Routes a submitted request to a department or specialist",
            RequiredPermission: "requests.route",
            RequiresConfirmation: true)
    };

    public AiToolDefinition? GetTool(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        return RegisteredTools.GetValueOrDefault(name);
    }

    public IReadOnlyList<AiToolDefinition> GetRegisteredTools() =>
        RegisteredTools.Values.ToList();
}
