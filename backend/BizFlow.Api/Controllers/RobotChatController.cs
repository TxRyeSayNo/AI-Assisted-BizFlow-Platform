using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

/// <summary>
/// FR-AI-011 / FR-AI-012 / BR-027: Giao diện Trợ lý Robot Chatbot thông minh
/// </summary>
[ApiController, Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class RobotChatController(
    IRobotChatService chatService) : ControllerBase
{
    /// <summary>
    /// Gửi tin nhắn hỏi đáp ngôn ngữ tự nhiên hoặc khẩu lệnh (/task, /notifications) tới Trợ lý Robot
    /// </summary>
    [HttpPost("api/v1/ai/chat")]
    [HttpPost("api/v1/ai/robot/query")]
    public async Task<ActionResult<RobotChatResponse>> SendMessage(
        [FromBody] RobotChatCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await chatService.ProcessMessageAsync(command, cancellationToken);
        return Ok(result);
    }
}
