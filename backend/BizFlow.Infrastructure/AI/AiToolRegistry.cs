using System;
using System.Collections.Generic;
using System.Linq;
using BizFlow.Application.AI;

namespace BizFlow.Infrastructure.AI;

public sealed class AiToolRegistry : IAiToolRegistry
{
    private static readonly Dictionary<string, AiToolDefinition> RegisteredTools = new(StringComparer.OrdinalIgnoreCase)
    {
        ["send_meeting_notification"] = new("send_meeting_notification",
            "Send any in-app communication organizing a planned meeting: attendance invitations, requests to prepare materials, or meeting-related reports. Use this specialized contract for communications tied to a meeting even when the user only asks people to prepare. Requires actual topic, exact future startsAt with UTC offset, location and recipientScope; missing logistics must be clarified. Does not create calendar events, send email or schedule delivery.",
            "notifications.send", true),
        ["send_department_notification"] = new("send_department_notification",
            "Send a general in-app announcement unrelated to a planned meeting, to active company members or explicitly selected departments. Requires title, content and recipientScope. Excludes meeting attendance, preparation and meeting reporting requests: those require send_meeting_notification and its logistics. Never use this tool to avoid required fields of a more specialized tool. Does not send email or schedule delivery.",
            "notifications.send", true),
        ["create_task_draft"] = new(
            Name: "create_task_draft",
            Description: "Creates one draft task with title, description and priority only. Does not assign an employee, set a deadline, publish or start workflow. Do not claim these unsupported effects.",
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
            RequiresConfirmation: true),

        ["route_request"] = new(
            Name: "route_request",
            Description: "Routes a submitted request to a department or specialist",
            RequiredPermission: "requests.route",
            RequiresConfirmation: true)
    };

    public AiToolDefinition? GetTool(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        var tool = RegisteredTools.GetValueOrDefault(name);
        return tool == null ? null : tool with { Parameters = AgentToolSchemas.For(tool.Name) };
    }

    public IReadOnlyList<AiToolDefinition> GetRegisteredTools() =>
        RegisteredTools.Values.Select(t => t with { Parameters = AgentToolSchemas.For(t.Name) }).ToList();
}
