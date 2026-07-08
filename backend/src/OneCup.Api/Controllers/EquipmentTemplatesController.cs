using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 运行模板管理端点（嵌套在设备类型路由下）。
/// 类级需 equipment-type:read；写操作需 equipment-type:create/update/delete。
/// </summary>
[ApiController]
[Route("api/equipment-types/{typeId:guid}/templates")]
[Authorize(Policy = "equipment-type:read")]
public class EquipmentTemplatesController : ControllerBase
{
    private readonly IEquipmentTemplateService _service;

    public EquipmentTemplatesController(IEquipmentTemplateService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        Guid typeId,
        [FromQuery] Guid? processId,
        CancellationToken ct = default)
    {
        var result = await _service.GetListAsync(typeId, processId, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid typeId, Guid id, CancellationToken ct)
    {
        var template = await _service.GetByIdAsync(typeId, id, ct);
        return template is null ? NotFound() : Ok(template);
    }

    // ── 顶层端点（非嵌套：不带 typeId）── `~` 覆盖类级 Route 前缀。

    /// <summary>跨类型分页查询模板（顶层 Templates 标签页用）。</summary>
    [HttpGet("~/api/equipment-templates")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] Guid? typeId,
        [FromQuery] string? keyword,
        [FromQuery] Guid? processId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _service.GetPagedAsync(typeId, keyword, processId, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>顶层模板详情（编辑模式只知道模板 id 时用）。</summary>
    [HttpGet("~/api/equipment-templates/{id:guid}")]
    public async Task<IActionResult> GetByIdTopLevel(Guid id, CancellationToken ct)
    {
        var template = await _service.GetByIdAsync(id, ct);
        return template is null ? NotFound() : Ok(template);
    }

    [Authorize(Policy = "equipment-type:create")]
    [HttpPost]
    public async Task<IActionResult> Create(
        Guid typeId,
        [FromBody] CreateEquipmentTemplateRequest request,
        CancellationToken ct)
    {
        var template = await _service.CreateAsync(typeId, request, ct);
        return CreatedAtAction(nameof(GetById), new { typeId, id = template.Id }, template);
    }

    [Authorize(Policy = "equipment-type:update")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid typeId, Guid id,
        [FromBody] UpdateEquipmentTemplateRequest request,
        CancellationToken ct)
    {
        var template = await _service.UpdateAsync(typeId, id, request, ct);
        return Ok(template);
    }

    [Authorize(Policy = "equipment-type:delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid typeId, Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(typeId, id, ct);
        return NoContent();
    }
}
