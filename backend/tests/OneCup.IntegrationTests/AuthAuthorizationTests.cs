using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OneCup.Application.Dtos.Auth;

namespace OneCup.IntegrationTests;

/// <summary>
/// 认证与授权集成测试:覆盖 JWT 管道 + 授权策略 + admin 通配放行。
/// 通过真实的 /api/auth/login 端点获取 token(InMemory 库已种子 admin/developer)。
/// </summary>
public class AuthAuthorizationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public AuthAuthorizationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        // 种子在构造时执行一次(IClassFixture 共享 factory,SeedAsync 内部对已有数据幂等)。
        _factory.SeedAsync().GetAwaiter().GetResult();
    }

    /// <summary>无 token 访问受保护端点(/api/users,需 user-manage 策略)→ 401。</summary>
    [Fact]
    public async Task No_token_protected_endpoint_returns_401()
    {
        var resp = await _client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    /// <summary>携带无效/伪造 token → 401(签名校验失败)。</summary>
    [Fact]
    public async Task Invalid_token_returns_401()
    {
        _client.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Bearer", "not.a.valid.jwt");

        var resp = await _client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }

    /// <summary>admin 登录成功 → 200 + 返回 accessToken;用该 token 访问 /api/users → 200。</summary>
    [Fact]
    public async Task Admin_login_returns_token_and_can_access_users()
    {
        var token = await LoginAsync(IntegrationTestFactory.AdminUsername, IntegrationTestFactory.TestPassword);
        Assert.False(string.IsNullOrWhiteSpace(token));

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
    }

    /// <summary>developer 登录成功,但无 system:user:manage 权限 → /api/users 返回 403。</summary>
    [Fact]
    public async Task Developer_token_users_endpoint_returns_403()
    {
        var token = await LoginAsync(IntegrationTestFactory.DeveloperUsername, IntegrationTestFactory.TestPassword);
        Assert.False(string.IsNullOrWhiteSpace(token));

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var resp = await _client.GetAsync("/api/users");
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    /// <summary>admin 通配(*):admin 角色持有 perm_codes="*",放行所有策略端点(含 role-manage)。</summary>
    [Fact]
    public async Task Admin_wildcard_passes_role_manage_policy()
    {
        var token = await LoginAsync(IntegrationTestFactory.AdminUsername, IntegrationTestFactory.TestPassword);

        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        // /api/users 需 user-manage,/api/roles 需 role-manage;两个都应放行(非 403)。
        var users = await _client.GetAsync("/api/users");
        var roles = await _client.GetAsync("/api/roles");
        Assert.Equal(HttpStatusCode.OK, users.StatusCode);
        Assert.Equal(HttpStatusCode.OK, roles.StatusCode);
    }

    /// <summary>登录拿真实 access token。</summary>
    private async Task<string> LoginAsync(string username, string password)
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = username, Password = password });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var token = await resp.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(token);
        return token!.AccessToken;
    }
}
