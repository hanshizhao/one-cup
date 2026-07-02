namespace OneCup.Application.Options;

/// <summary>
/// JWT 相关配置，绑定 appsettings 的 Jwt 节。
/// </summary>
public class JwtOptions
{
    public const string SectionName = "Jwt";

    /// <summary>未配置时 appsettings 的占位符。启动校验拒绝该值。</summary>
    public const string PlaceholderSecret = "REPLACE_VIA_USER_SECRETS";

    public string Issuer { get; set; } = string.Empty;

    public string Audience { get; set; } = string.Empty;

    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Access Token 有效期（分钟）</summary>
    public int AccessTokenMinutes { get; set; } = 30;

    /// <summary>Refresh Token 有效期（天）</summary>
    public int RefreshTokenDays { get; set; } = 7;
}
