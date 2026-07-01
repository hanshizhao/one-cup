using OneCup.Domain.Entities;

namespace OneCup.Application.Interfaces;

/// <summary>
/// JWT 签发服务接口。
/// </summary>
public interface IJwtTokenService
{
    /// <summary>为指定用户签发 Access Token，返回 token 字符串。</summary>
    string GenerateAccessToken(User user);

    /// <summary>生成随机 opaque Refresh Token 字符串（不签 JWT）。</summary>
    string GenerateRefreshToken();
}
