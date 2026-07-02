using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using OneCup.Application.Interfaces;
using OneCup.Application.Options;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Services;

/// <summary>
/// JWT 签发服务实现（HS256）。
/// </summary>
public class JwtTokenService : IJwtTokenService
{
    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public string GenerateAccessToken(User user)
    {
        var roleCodes = user.Roles.Select(r => r.Code).ToList();
        // admin 角色通配为 *，其他角色聚合权限编码
        var permCodes = roleCodes.Contains("admin")
            ? new List<string> { "*" }
            : user.Roles.SelectMany(r => r.Permissions).Select(p => p.Code).Distinct().ToList();

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(ClaimTypes.Name, user.Username),
            new("username", user.Username),
        };
        claims.AddRange(roleCodes.Select(c => new Claim("role_codes", c)));
        claims.AddRange(permCodes.Select(p => new Claim("perm_codes", p)));

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(_options.AccessTokenMinutes),
            signingCredentials: creds);

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        // 32 字节随机数 → Base64URL（约 43 字符）
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(bytes)
            .Replace("+", "-")
            .Replace("/", "_")
            .TrimEnd('=');
    }

    /// <summary>获取 token 验证参数，供 Api 层 JwtBearer 复用。</summary>
    public TokenValidationParameters GetValidationParameters()
    {
        return new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _options.Issuer,
            ValidateAudience = true,
            ValidAudience = _options.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.SecretKey)),
            ClockSkew = TimeSpan.Zero,
        };
    }
}
