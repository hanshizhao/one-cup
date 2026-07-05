using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 物料管理端点。
/// 权限:material:read / material:create / material:update / material:delete(策略名 = 权限码)。
/// </summary>
[ApiController]
[Route("api/materials")]
public class MaterialController : ControllerBase
{
    private readonly IMaterialService _svc;

    public MaterialController(IMaterialService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    [Authorize(Policy = "material:read")]
    public async Task<IActionResult> GetMaterials(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null, [FromQuery] string? category = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _svc.GetMaterialsAsync(page, pageSize, keyword, category, isActive, ct);
        return Ok(result);
    }

    [HttpGet("all")]
    [Authorize(Policy = "material:read")]
    public async Task<IActionResult> GetAllActiveMaterials(CancellationToken ct)
    {
        var list = await _svc.GetAllActiveMaterialsAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "material:read")]
    public async Task<IActionResult> GetMaterial(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetMaterialAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = "material:create")]
    public async Task<IActionResult> CreateMaterial([FromBody] CreateMaterialRequest request, CancellationToken ct)
    {
        var dto = await _svc.CreateMaterialAsync(request, ct);
        return CreatedAtAction(nameof(GetMaterial), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "material:update")]
    public async Task<IActionResult> UpdateMaterial(Guid id, [FromBody] UpdateMaterialRequest request, CancellationToken ct)
    {
        await _svc.UpdateMaterialAsync(id, request, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "material:update")]
    public async Task<IActionResult> UpdateMaterialStatus(Guid id, [FromBody] UpdateMaterialStatusRequest request, CancellationToken ct)
    {
        await _svc.UpdateMaterialStatusAsync(id, request.IsActive, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "material:delete")]
    public async Task<IActionResult> DeleteMaterial(Guid id, CancellationToken ct)
    {
        await _svc.DeleteMaterialAsync(id, ct);
        return NoContent();
    }
}
