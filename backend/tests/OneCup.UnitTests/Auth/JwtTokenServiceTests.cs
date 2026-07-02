using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Options;
using OneCup.Application.Options;
using OneCup.Domain.Entities;
using OneCup.Infrastructure.Services;

namespace OneCup.UnitTests.Auth;

public class JwtTokenServiceTests
{
    private readonly JwtOptions _testOptions = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SecretKey = "test-secret-key-that-is-at-least-32-chars-long!",
        AccessTokenMinutes = 30,
        RefreshTokenDays = 7,
    };

    private JwtTokenService CreateService() => new(Options.Create(_testOptions));

    private static User CreateUserWithRoles(List<Role> roles) => new()
    {
        Id = Guid.Parse("00000000-0000-0000-0000-000000000001"),
        Username = "testuser",
        DisplayName = "测试用户",
        IsActive = true,
        Roles = roles,
    };

    private static Role CreateRole(string code, List<Permission>? permissions = null) => new()
    {
        Id = Guid.NewGuid(),
        Name = code,
        Code = code,
        Permissions = permissions ?? [],
    };

    private static Permission CreatePermission(string code) => new()
    {
        Id = Guid.NewGuid(),
        Code = code,
        Name = code,
    };

    [Fact]
    public void GenerateAccessToken_ReturnsNonEmptyString_WithCorrectClaims()
    {
        // Arrange
        var service = CreateService();
        var perms = new List<Permission>
        {
            CreatePermission("fabric:read"),
            CreatePermission("fabric:write"),
        };
        var role = CreateRole("developer", perms);
        var user = CreateUserWithRoles([role]);

        // Act
        var token = service.GenerateAccessToken(user);

        // Assert — non-empty JWT
        Assert.False(string.IsNullOrEmpty(token));

        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        // sub claim = user.Id
        var sub = jwt.Claims.First(c => c.Type == JwtRegisteredClaimNames.Sub).Value;
        Assert.Equal(user.Id.ToString(), sub);

        // username claim present
        Assert.Contains(jwt.Claims, c => c.Type == "username" && c.Value == user.Username);

        // role_codes claim(s) present
        var roleClaims = jwt.Claims.Where(c => c.Type == "role_codes").Select(c => c.Value).ToList();
        Assert.Contains("developer", roleClaims);

        // perm_codes claim(s) present
        var permClaims = jwt.Claims.Where(c => c.Type == "perm_codes").Select(c => c.Value).ToList();
        Assert.Contains("fabric:read", permClaims);
        Assert.Contains("fabric:write", permClaims);
    }

    [Fact]
    public void GenerateAccessToken_ForAdminRole_HasPermCodesExactlyWildcard()
    {
        // Arrange — admin 角色通过通配 * 拥有全部权限，不绑定权限
        var adminRole = CreateRole("admin", permissions: []);
        var user = CreateUserWithRoles([adminRole]);
        var service = CreateService();

        // Act
        var token = service.GenerateAccessToken(user);

        // Assert
        var handler = new JwtSecurityTokenHandler();
        var jwt = handler.ReadJwtToken(token);

        var permClaims = jwt.Claims.Where(c => c.Type == "perm_codes").Select(c => c.Value).ToList();
        Assert.Single(permClaims);
        Assert.Equal("*", permClaims[0]);
    }

    [Fact]
    public void GenerateAccessToken_IncludesIssuerAudienceAndExpiration()
    {
        // Arrange
        var service = CreateService();
        var user = CreateUserWithRoles([CreateRole("developer", [CreatePermission("fabric:read")])]);

        // Act
        var token = service.GenerateAccessToken(user);
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(token);

        // Assert
        Assert.Equal(_testOptions.Issuer, jwt.Issuer);
        Assert.Contains(_testOptions.Audience, jwt.Audiences);
        // 过期时间 = 现在 + 30 分钟（允许几秒误差）
        Assert.True(jwt.ValidTo > DateTime.UtcNow.AddMinutes(25));
        Assert.True(jwt.ValidTo <= DateTime.UtcNow.AddMinutes(31));
    }

    [Fact]
    public void GenerateRefreshToken_ReturnsNonEmptyString()
    {
        // Arrange
        var service = CreateService();

        // Act
        var token = service.GenerateRefreshToken();

        // Assert — 32 字节 → base64url 约 43 字符
        Assert.False(string.IsNullOrEmpty(token));
        Assert.True(token.Length >= 40);
    }

    [Fact]
    public void GenerateRefreshToken_GeneratesDifferentToken_EachCall()
    {
        // Arrange
        var service = CreateService();

        // Act
        var token1 = service.GenerateRefreshToken();
        var token2 = service.GenerateRefreshToken();
        var token3 = service.GenerateRefreshToken();

        // Assert — 每次调用结果不同（随机性）
        Assert.NotEqual(token1, token2);
        Assert.NotEqual(token1, token3);
        Assert.NotEqual(token2, token3);
    }

    [Fact]
    public void GenerateRefreshToken_IsBase64UrlSafe_NoPaddingOrSpecialChars()
    {
        // Arrange
        var service = CreateService();

        // Act — 多次取样提高覆盖面
        var samples = Enumerable.Range(0, 20).Select(_ => service.GenerateRefreshToken()).ToList();

        // Assert — base64url 不含 + / = 等非 URL 安全字符
        Assert.All(samples, t =>
        {
            Assert.DoesNotContain('+', t);
            Assert.DoesNotContain('/', t);
            Assert.DoesNotContain('=', t);
        });
    }
}
