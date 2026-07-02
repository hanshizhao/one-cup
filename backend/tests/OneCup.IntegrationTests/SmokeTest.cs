using System.Net;

namespace OneCup.IntegrationTests;

/// <summary>
/// 冒烟测试:确认 WebApplicationFactory<Program> 能成功启动应用,
/// 中间件管道(auth/exception)就绪。受保护端点无 token 应返回 401。
/// </summary>
public class SmokeTest : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;

    public SmokeTest(IntegrationTestFactory factory) => _factory = factory;

    [Fact]
    public async Task Factory_starts_app_and_protected_endpoint_returns_401()
    {
        var client = _factory.CreateClient();

        // /api/auth/me 需 [Authorize],无 token → 401(说明 auth 管道活着)。
        var resp = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
    }
}
