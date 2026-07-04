using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Api.Filters;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 用户管理端点。需要 system:user:* 权限（或 admin 通配）。
/// </summary>
[ApiController]
[Route("api/users")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly IUserService _userService;

    public UsersController(IUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    [Authorize(Policy = "system:user:read")]
    public async Task<IActionResult> GetList([FromQuery] int page = 1, [FromQuery] int pageSize = 10, [FromQuery] string? keyword = null, CancellationToken ct = default)
    {
        var result = await _userService.GetListAsync(page, pageSize, keyword, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "system:user:read")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var user = await _userService.GetByIdAsync(id, ct);
        return user is null ? NotFound() : Ok(user);
    }

    [Audit(Module = "User", Action = "Create", TargetType = "User")]
    [Authorize(Policy = "system:user:create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateUserRequest request, CancellationToken ct)
    {
        var user = await _userService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = user.Id }, user);
    }

    [Audit(Module = "User", Action = "Update", TargetType = "User")]
    [Authorize(Policy = "system:user:update")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUserRequest request, CancellationToken ct)
    {
        var user = await _userService.UpdateAsync(id, request, ct);
        return Ok(user);
    }

    [Audit(Module = "User", Action = "ResetPassword", TargetType = "User")]
    [Authorize(Policy = "system:user:reset-password")]
    [HttpPut("{id:guid}/password")]
    public async Task<IActionResult> ResetPassword(Guid id, [FromBody] ResetPasswordRequest request, CancellationToken ct)
    {
        await _userService.ResetPasswordAsync(id, request, ct);
        return NoContent();
    }

    [Audit(Module = "User", Action = "ChangeStatus", TargetType = "User")]
    [Authorize(Policy = "system:user:update")]
    [HttpPut("{id:guid}/status")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateStatusRequest request, CancellationToken ct)
    {
        await _userService.UpdateStatusAsync(id, request, ct);
        return NoContent();
    }

    /// <summary>删除用户(软删除;admin 账号受保护;同步吊销其 refresh token)。</summary>
    [Audit(Module = "User", Action = "Delete", TargetType = "User")]
    [Authorize(Policy = "system:user:delete")]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _userService.DeleteAsync(id, ct);
        return NoContent();
    }
}
