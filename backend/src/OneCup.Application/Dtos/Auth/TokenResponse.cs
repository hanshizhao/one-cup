namespace OneCup.Application.Dtos.Auth;

/// <summary>
/// 登录/刷新成功后返回的令牌对。
/// </summary>
public class TokenResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>Access Token 有效期（秒）</summary>
    public int ExpiresIn { get; set; }
}
