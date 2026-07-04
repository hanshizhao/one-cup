using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 计量单位管理端点。复用 unit-view / unit-manage 权限。
/// </summary>
[ApiController]
[Route("api/measurement-units")]
public class MeasurementUnitsController : ControllerBase
{
    private readonly IMeasurementUnitService _svc;

    public MeasurementUnitsController(IMeasurementUnitService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    [Authorize(Policy = "unit-view")]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null, [FromQuery] string? category = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _svc.GetListAsync(page, pageSize, keyword, category, isActive, ct);
        return Ok(result);
    }

    [HttpGet("all")]
    [Authorize(Policy = "unit-view")]
    public async Task<IActionResult> GetAllActive(CancellationToken ct)
    {
        var list = await _svc.GetAllActiveAsync(ct);
        return Ok(list);
    }

    [HttpGet("categories")]
    [Authorize(Policy = "unit-view")]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var list = await _svc.GetCategoriesAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "unit-view")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = "unit-manage")]
    public async Task<IActionResult> Create([FromBody] CreateUnitRequest request, CancellationToken ct)
    {
        var dto = await _svc.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "unit-manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUnitRequest request, CancellationToken ct)
    {
        await _svc.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "unit-manage")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateUnitStatusRequest request, CancellationToken ct)
    {
        await _svc.UpdateStatusAsync(id, request.IsActive, ct);
        return NoContent();
    }

    [HttpPost("convert")]
    [Authorize(Policy = "unit-view")]
    public async Task<IActionResult> Convert([FromBody] ConvertUnitRequest request, CancellationToken ct)
    {
        var result = await _svc.ConvertAsync(request, ct);
        return Ok(result);
    }
}
