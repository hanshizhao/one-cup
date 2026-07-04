using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 颜色主数据管理端点。
/// 权限：color:read / color:create / color:update（策略名 = 权限码）。
/// </summary>
[ApiController]
[Route("api/colors")]
public class ColorController : ControllerBase
{
    private readonly IColorService _svc;

    public ColorController(IColorService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    [Authorize(Policy = "color:read")]
    public async Task<IActionResult> GetColors(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null, [FromQuery] string? colorFamily = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _svc.GetColorsAsync(page, pageSize, keyword, colorFamily, isActive, ct);
        return Ok(result);
    }

    [HttpGet("all")]
    [Authorize(Policy = "color:read")]
    public async Task<IActionResult> GetAllActiveColors(CancellationToken ct)
    {
        var list = await _svc.GetAllActiveColorsAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "color:read")]
    public async Task<IActionResult> GetColor(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetColorAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = "color:create")]
    public async Task<IActionResult> CreateColor([FromBody] CreateColorRequest request, CancellationToken ct)
    {
        var dto = await _svc.CreateColorAsync(request, ct);
        return CreatedAtAction(nameof(GetColor), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "color:update")]
    public async Task<IActionResult> UpdateColor(Guid id, [FromBody] UpdateColorRequest request, CancellationToken ct)
    {
        await _svc.UpdateColorAsync(id, request, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "color:update")]
    public async Task<IActionResult> UpdateColorStatus(Guid id, [FromBody] UpdateColorStatusRequest request, CancellationToken ct)
    {
        await _svc.UpdateColorStatusAsync(id, request.IsActive, ct);
        return NoContent();
    }
}
