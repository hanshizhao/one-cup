using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Api.Filters;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 设备类型管理端点。类级需 equipment-type:read；写操作需对应权限。
/// </summary>
[ApiController]
[Route("api/equipment-types")]
[Authorize(Policy = "equipment-type:read")]
public class EquipmentTypesController : ControllerBase
{
    private readonly IEquipmentTypeService _service;

    public EquipmentTypesController(IEquipmentTypeService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? keyword,
        [FromQuery] string? code,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _service.GetListAsync(keyword, code, isActive, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>启用类型列表（前端下拉用，不分页）。</summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken ct = default)
    {
        var result = await _service.GetActiveAsync(ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var type = await _service.GetByIdAsync(id, ct);
        return type is null ? NotFound() : Ok(type);
    }

    [Audit(Module = "EquipmentType", Action = "Create", TargetType = "EquipmentType")]
    [Authorize(Policy = "equipment-type:create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEquipmentTypeRequest request, CancellationToken ct)
    {
        var type = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = type.Id }, type);
    }

    [Audit(Module = "EquipmentType", Action = "Update", TargetType = "EquipmentType")]
    [Authorize(Policy = "equipment-type:update")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEquipmentTypeRequest request, CancellationToken ct)
    {
        var type = await _service.UpdateAsync(id, request, ct);
        return Ok(type);
    }

    [Audit(Module = "EquipmentType", Action = "Delete", TargetType = "EquipmentType")]
    [Authorize(Policy = "equipment-type:delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
