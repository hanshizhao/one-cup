using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Api.Filters;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Domain.Enums;

namespace OneCup.Api.Controllers;

/// <summary>
/// 设备实例管理端点。类级需 equipment:read；写操作需对应权限。
/// </summary>
[ApiController]
[Route("api/equipment")]
[Authorize(Policy = "equipment:read")]
public class EquipmentsController : ControllerBase
{
    private readonly IEquipmentService _service;

    public EquipmentsController(IEquipmentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? keyword,
        [FromQuery] string? code,
        [FromQuery] Guid? typeId,
        [FromQuery] bool? isActive,
        [FromQuery] EquipmentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _service.GetListAsync(keyword, code, typeId, isActive, status, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var equipment = await _service.GetByIdAsync(id, ct);
        return equipment is null ? NotFound() : Ok(equipment);
    }

    [Audit(Module = "Equipment", Action = "Create", TargetType = "Equipment")]
    [Authorize(Policy = "equipment:create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEquipmentRequest request, CancellationToken ct)
    {
        var equipment = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = equipment.Id }, equipment);
    }

    [Audit(Module = "Equipment", Action = "Update", TargetType = "Equipment")]
    [Authorize(Policy = "equipment:update")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEquipmentRequest request, CancellationToken ct)
    {
        var equipment = await _service.UpdateAsync(id, request, ct);
        return Ok(equipment);
    }

    [Audit(Module = "Equipment", Action = "Delete", TargetType = "Equipment")]
    [Authorize(Policy = "equipment:delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
