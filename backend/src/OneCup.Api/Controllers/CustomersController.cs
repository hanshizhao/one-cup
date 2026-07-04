using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Api.Filters;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 客户管理端点。类级需 customer:read；写操作叠加 customer:write。
/// </summary>
[ApiController]
[Route("api/customers")]
[Authorize(Policy = "customer-read")]
public class CustomersController : ControllerBase
{
    private readonly ICustomerService _customerService;

    public CustomersController(ICustomerService customerService)
    {
        _customerService = customerService;
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
        var result = await _customerService.GetListAsync(keyword, code, isActive, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var customer = await _customerService.GetByIdAsync(id, ct);
        return customer is null ? NotFound() : Ok(customer);
    }

    [Audit(Module = "Customer", Action = "Create", TargetType = "Customer")]
    [Authorize(Policy = "customer-write")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCustomerRequest request, CancellationToken ct)
    {
        var customer = await _customerService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = customer.Id }, customer);
    }

    [Audit(Module = "Customer", Action = "Update", TargetType = "Customer")]
    [Authorize(Policy = "customer-write")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateCustomerRequest request, CancellationToken ct)
    {
        var customer = await _customerService.UpdateAsync(id, request, ct);
        return Ok(customer);
    }

    [Audit(Module = "Customer", Action = "Delete", TargetType = "Customer")]
    [Authorize(Policy = "customer-write")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _customerService.DeleteAsync(id, ct);
        return NoContent();
    }
}
