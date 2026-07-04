using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 登录日志查询端点。需 system:audit:read 权限。
/// </summary>
[ApiController]
[Route("api/audit/login-logs")]
[Authorize(Policy = "system:audit:read")]
public class LoginLogsController : ControllerBase
{
    private readonly IAuditLogService _svc;

    public LoginLogsController(IAuditLogService svc)
    {
        _svc = svc;
    }

    /// <summary>分页查询登录日志。</summary>
    [HttpGet]
    public async Task<IActionResult> Search([FromQuery] LoginLogQuery query, CancellationToken ct)
    {
        var result = await _svc.SearchLoginsAsync(query, ct);
        return Ok(result);
    }

    /// <summary>登录日志详情。</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Get(Guid id, CancellationToken ct)
    {
        var detail = await _svc.GetLoginAsync(id, ct);
        return detail is null ? NotFound() : Ok(detail);
    }
}
