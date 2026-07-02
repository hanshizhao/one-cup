using OneCup.Application.Dtos.Auth;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 认证服务接口，编排登录/刷新/登出/获取当前用户。
/// </summary>
public interface IAuthService
{
    Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default);

    Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default);

    Task LogoutAsync(Guid userId, CancellationToken ct = default);

    Task<CurrentUser?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default);
}
