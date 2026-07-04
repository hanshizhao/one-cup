using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Api.Filters;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 工序管理端点。类级需 process:read；写操作需 process:create / process:update / process:delete。
/// </summary>
[ApiController]
[Route("api/processes")]
[Authorize(Policy = "process:read")]
public class ProcessesController : ControllerBase
{
    private readonly IProcessService _processService;

    public ProcessesController(IProcessService processService)
    {
        _processService = processService;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? keyword,
        [FromQuery] string? category,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _processService.GetListAsync(keyword, category, isActive, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var process = await _processService.GetByIdAsync(id, ct);
        return process is null ? NotFound() : Ok(process);
    }

    [Audit(Module = "Process", Action = "Create", TargetType = "Process")]
    [Authorize(Policy = "process:create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProcessRequest request, CancellationToken ct)
    {
        var process = await _processService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = process.Id }, process);
    }

    [Audit(Module = "Process", Action = "Update", TargetType = "Process")]
    [Authorize(Policy = "process:update")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateProcessRequest request, CancellationToken ct)
    {
        var process = await _processService.UpdateAsync(id, request, ct);
        return Ok(process);
    }

    [Audit(Module = "Process", Action = "Delete", TargetType = "Process")]
    [Authorize(Policy = "process:delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _processService.DeleteAsync(id, ct);
        return NoContent();
    }
}
