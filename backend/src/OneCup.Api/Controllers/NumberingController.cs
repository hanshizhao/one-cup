using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Api.Filters;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 编号管理端点。
/// 规则 CRUD 需 system:numbering:manage；列表/日志/预览仅需登录或 view。
/// 生成接口（GenerateAsync）是内部服务调用，不在此暴露 HTTP。
/// </summary>
[ApiController]
[Route("api/numbering")]
public class NumberingController : ControllerBase
{
    private readonly INumberingRuleService _ruleService;
    private readonly INumberingService _numberingService;

    public NumberingController(INumberingRuleService ruleService, INumberingService numberingService)
    {
        _ruleService = ruleService;
        _numberingService = numberingService;
    }

    // ── 规则管理（view 可查，manage 可改）──

    [HttpGet("rules")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetRules(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null, [FromQuery] string? targetType = null,
        [FromQuery] bool? isActive = null, CancellationToken ct = default)
    {
        var result = await _ruleService.GetListAsync(page, pageSize, keyword, targetType, isActive, ct);
        return Ok(result);
    }

    [HttpGet("rules/{id:guid}")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetRule(Guid id, CancellationToken ct)
    {
        var rule = await _ruleService.GetAsync(id, ct);
        return rule is null ? NotFound() : Ok(rule);
    }

    [Audit(Module = "Numbering", Action = "Create", TargetType = "NumberingRule")]
    [HttpPost("rules")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> CreateRule([FromBody] CreateNumberingRuleRequest request, CancellationToken ct)
    {
        var rule = await _ruleService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetRule), new { id = rule.Id }, rule);
    }

    [Audit(Module = "Numbering", Action = "Update", TargetType = "NumberingRule")]
    [HttpPut("rules/{id:guid}")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateNumberingRuleRequest request, CancellationToken ct)
    {
        await _ruleService.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [Audit(Module = "Numbering", Action = "ChangeStatus", TargetType = "NumberingRule")]
    [HttpPut("rules/{id:guid}/status")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> UpdateRuleStatus(Guid id, [FromBody] UpdateRuleStatusRequest request, CancellationToken ct)
    {
        await _ruleService.UpdateStatusAsync(id, request.IsActive, ct);
        return NoContent();
    }

    // ── 预览（登录即可，不消耗计数）──

    [HttpGet("preview")]
    [Authorize]
    public async Task<IActionResult> Preview([FromQuery] string targetType, [FromQuery] string? categoryCode = null, CancellationToken ct = default)
    {
        var code = await _numberingService.PreviewAsync(targetType, categoryCode, ct);
        return Ok(new PreviewCodeResult { Code = code });
    }

    // ── 生成日志（view 可查）──

    [HttpGet("logs")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? targetType = null, [FromQuery] string? categoryCode = null,
        [FromQuery] Guid? ruleId = null, [FromQuery] string? code = null,
        [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null,
        CancellationToken ct = default)
    {
        var result = await _ruleService.GetLogsAsync(page, pageSize, targetType, categoryCode, ruleId, code, startDate, endDate, ct);
        return Ok(result);
    }
}
