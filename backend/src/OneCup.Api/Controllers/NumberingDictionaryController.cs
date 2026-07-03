using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 编号字典管理端点（业务类型 + 分类）。
/// 复用 numbering-view / numbering-manage 权限。
/// </summary>
[ApiController]
[Route("api/numbering/dict")]
public class NumberingDictionaryController : ControllerBase
{
    private readonly INumberingDictionaryService _svc;

    public NumberingDictionaryController(INumberingDictionaryService svc)
    {
        _svc = svc;
    }

    // ── 业务类型 ──

    [HttpGet("target-types")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetTargetTypes(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null, [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _svc.GetTargetTypesAsync(page, pageSize, keyword, isActive, ct);
        return Ok(result);
    }

    [HttpGet("target-types/all")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetAllActiveTargetTypes(CancellationToken ct)
    {
        var list = await _svc.GetAllActiveTargetTypesAsync(ct);
        return Ok(list);
    }

    [HttpGet("target-types/{id:guid}")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetTargetType(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetTargetTypeAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("target-types")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> CreateTargetType([FromBody] CreateTargetTypeRequest request, CancellationToken ct)
    {
        var dto = await _svc.CreateTargetTypeAsync(request, ct);
        return CreatedAtAction(nameof(GetTargetType), new { id = dto.Id }, dto);
    }

    [HttpPut("target-types/{id:guid}")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> UpdateTargetType(Guid id, [FromBody] UpdateTargetTypeRequest request, CancellationToken ct)
    {
        await _svc.UpdateTargetTypeAsync(id, request, ct);
        return NoContent();
    }

    [HttpPut("target-types/{id:guid}/status")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> UpdateTargetTypeStatus(Guid id, [FromBody] UpdateDictStatusRequest request, CancellationToken ct)
    {
        await _svc.UpdateTargetTypeStatusAsync(id, request.IsActive, ct);
        return NoContent();
    }

    // ── 分类 ──

    [HttpGet("categories")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetCategories(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? targetTypeCode = null, [FromQuery] string? keyword = null,
        [FromQuery] bool? isActive = null, CancellationToken ct = default)
    {
        var result = await _svc.GetCategoriesAsync(page, pageSize, targetTypeCode, keyword, isActive, ct);
        return Ok(result);
    }

    [HttpGet("categories/all")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetActiveCategories([FromQuery] string targetTypeCode, CancellationToken ct)
    {
        var list = await _svc.GetActiveCategoriesAsync(targetTypeCode, ct);
        return Ok(list);
    }

    [HttpGet("categories/{id:guid}")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetCategory(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetCategoryAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("categories")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        var dto = await _svc.CreateCategoryAsync(request, ct);
        return CreatedAtAction(nameof(GetCategory), new { id = dto.Id }, dto);
    }

    [HttpPut("categories/{id:guid}")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        await _svc.UpdateCategoryAsync(id, request, ct);
        return NoContent();
    }

    [HttpPut("categories/{id:guid}/status")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> UpdateCategoryStatus(Guid id, [FromBody] UpdateDictStatusRequest request, CancellationToken ct)
    {
        await _svc.UpdateCategoryStatusAsync(id, request.IsActive, ct);
        return NoContent();
    }
}
