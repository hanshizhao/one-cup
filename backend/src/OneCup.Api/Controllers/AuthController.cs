using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.Auth;
using OneCup.Application.Interfaces;
using OneCup.Api.Services;
using static Microsoft.AspNetCore.Http.StatusCodes;

namespace OneCup.Api.Controllers;

/// <summary>
/// 认证相关端点。
/// </summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;
    private readonly CurrentUserService _current;

    public AuthController(IAuthService authService, CurrentUserService current)
    {
        _authService = authService;
        _current = current;
    }

    /// <summary>用户名密码登录。</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), Status200OK)]
    [ProducesResponseType(typeof(object), Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] LoginRequest request, CancellationToken ct)
    {
        var result = await _authService.LoginAsync(request, ct);
        return Ok(result);
    }

    /// <summary>用刷新令牌换新的访问令牌。</summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType(typeof(TokenResponse), Status200OK)]
    [ProducesResponseType(typeof(object), Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] RefreshRequest request, CancellationToken ct)
    {
        var result = await _authService.RefreshAsync(request, ct);
        return Ok(result);
    }

    /// <summary>登出，吊销当前用户的刷新令牌。</summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(Status204NoContent)]
    public async Task<IActionResult> Logout(CancellationToken ct)
    {
        if (_current.UserId is null) return Unauthorized();
        await _authService.LogoutAsync(_current.UserId.Value, ct);
        return NoContent();
    }

    /// <summary>获取当前登录用户信息（含角色与权限）。</summary>
    [HttpGet("me")]
    [Authorize]
    [ProducesResponseType(typeof(CurrentUser), Status200OK)]
    [ProducesResponseType(Status401Unauthorized)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        if (_current.UserId is null) return Unauthorized();
        var user = await _authService.GetCurrentUserAsync(_current.UserId.Value, ct);
        return user is null ? Unauthorized() : Ok(user);
    }
}
