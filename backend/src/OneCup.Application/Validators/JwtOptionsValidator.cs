using Microsoft.Extensions.Options;
using OneCup.Application.Options;

namespace OneCup.Application.Validators;

/// <summary>
/// 启动时校验 JWT 配置:SecretKey 必须非空、非占位符、≥32 字节(HS256 要求)。
/// </summary>
public class JwtOptionsValidator : IValidateOptions<JwtOptions>
{
    public ValidateOptionsResult Validate(string? name, JwtOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.SecretKey))
        {
            return ValidateOptionsResult.Fail("Jwt:SecretKey 未配置。请通过 user-secrets 或环境变量提供。");
        }

        if (options.SecretKey == JwtOptions.PlaceholderSecret)
        {
            return ValidateOptionsResult.Fail("Jwt:SecretKey 仍为占位符,请通过 user-secrets 或环境变量配置真实密钥。");
        }

        // UTF-8 字节数(HS256 要求 ≥256 bit = 32 字节)
        if (global::System.Text.Encoding.UTF8.GetByteCount(options.SecretKey) < 32)
        {
            return ValidateOptionsResult.Fail("Jwt:SecretKey 长度不足,HS256 要求至少 32 字节(256 bit)。");
        }

        return ValidateOptionsResult.Success;
    }
}
