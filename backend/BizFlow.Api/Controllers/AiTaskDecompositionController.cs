using System.Threading;
using System.Threading.Tasks;
using BizFlow.Application.AI;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace BizFlow.Api.Controllers;

/// <summary>
/// INT-031 / IR-API-006: Giao diện API Phân rã Công việc bằng AI và Xác nhận Hàng loạt có Kiểm soát Con người
/// </summary>
[ApiController, Authorize]
[ResponseCache(NoStore = true, Location = ResponseCacheLocation.None)]
public sealed class AiTaskDecompositionController(
    IAiTaskDecompositionService decompositionService) : ControllerBase
{
    /// <summary>
    /// Bóc tách đoạn văn tự nhiên thành các đề xuất công việc. Tuyệt đối không ghi bản ghi nào vào DB (BR-028).
    /// </summary>
    [HttpPost("api/v1/tasks/ai-decomposition/preview")]
    [HttpPost("api/v1/ai/tasks/decompose")]
    public async Task<ActionResult<TaskDecompositionPreviewResult>> PreviewDecomposition(
        [FromBody] TaskDecompositionPreviewCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await decompositionService.PreviewAsync(command, cancellationToken);
        return Ok(result);
    }

    /// <summary>
    /// Xác nhận tạo việc hàng loạt sau khi con người đã xem xét, chỉnh sửa inline và chọn lọc (BR-028).
    /// </summary>
    [HttpPost("api/v1/tasks/ai-decomposition/confirm")]
    public async Task<ActionResult<ConfirmTaskDecompositionResponse>> ConfirmDecomposition(
        [FromBody] ConfirmTaskDecompositionCommand command,
        CancellationToken cancellationToken = default)
    {
        var result = await decompositionService.ConfirmAsync(command, cancellationToken);
        return Ok(result);
    }
}
