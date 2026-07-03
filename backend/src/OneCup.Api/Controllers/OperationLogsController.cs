using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 操作日志查询端点。需 system:audit:view 权限。
/// </summary>
[ApiController]
[Route("api/audit/operation-logs")]
[Authorize(Policy = "audit-view")]
public class OperationLogsController : ControllerBase
{
    private readonly IAuditLogService _svc;

    public OperationLogsController(IAuditLogService svc)
    {
        _svc = svc;
    }

    /// <summary>分页查询操作日志。</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] OperationLogQuery query, CancellationToken ct)
    {
        var result = await _svc.SearchOperationsAsync(query, ct);
        return Ok(result);
    }

    /// <summary>操作日志详情（含 StackTrace / RequestPayload）。</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var detail = await _svc.GetOperationAsync(id, ct);
        return detail is null ? NotFound() : Ok(detail);
    }
}
