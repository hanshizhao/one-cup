using OneCup.Application.Dtos.Auth;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 认证服务接口，编排登录/刷新/登出/获取当前用户。
/// </summary>
public interface IAuthService
{
    // ipAddress/userAgent 为纯字符串（可选），用于登录日志采集；
    // 由 Api 层从 HttpContext 取值后传入，Application 层不依赖 AspNetCore。
    Task<TokenResponse> LoginAsync(LoginRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);

    Task<TokenResponse> RefreshAsync(RefreshRequest request, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);

    Task LogoutAsync(Guid userId, string? ipAddress = null, string? userAgent = null, CancellationToken ct = default);

    Task<CurrentUser?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
}
