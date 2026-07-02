using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OneCup.Application.Dtos.Auth;
using OneCup.Application.Dtos.System;

namespace OneCup.IntegrationTests;

/// <summary>
/// 全局异常映射 + 输入校验集成测试。
/// 校验异常(Empty/弱密码)在 Service 层经 FluentValidation → EnsureValidAsync
/// 抛 DomainException,由全局异常处理器映射为 400 + { code: "DOMAIN_ERROR", message }。
/// </summary>
public class ExceptionValidationTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public ExceptionValidationTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.SeedAsync().GetAwaiter().GetResult();
    }

    /// <summary>登录提交空 username → FluentValidation 拦截 → DomainException → 400。</summary>
    [Fact]
    public async Task Login_empty_username_returns_400()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "", Password = "whatever" });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.NotNull(body);
        Assert.Equal("DOMAIN_ERROR", body!.Code);
        Assert.False(string.IsNullOrWhiteSpace(body.Message));
    }

    /// <summary>创建用户用弱密码(纯字母、无数字、不足规则)→ CreateUserRequestValidator → 400。</summary>
    [Fact]
    public async Task CreateUser_weak_password_returns_400()
    {
        // 受保护端点,需 admin token(user-manage 策略)。
        var token = await LoginAsync(IntegrationTestFactory.AdminUsername, IntegrationTestFactory.TestPassword);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var resp = await _client.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Username = "newuser1",
            DisplayName = "弱密码用户",
            Password = "weakpassword", // 缺数字 → 不满足 BeMediumStrength
            RoleIds = [IntegrationTestFactory.DeveloperRoleId],
        });

        Assert.Equal(HttpStatusCode.BadRequest, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.NotNull(body);
        Assert.Equal("DOMAIN_ERROR", body!.Code);
    }

    /// <summary>错误凭证(用户不存在)→ UnauthorizedException → 全局映射为 401 + UNAUTHORIZED。</summary>
    [Fact]
    public async Task Login_wrong_credentials_returns_401()
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = "admin", Password = "WrongPassword123" });

        Assert.Equal(HttpStatusCode.Unauthorized, resp.StatusCode);
        var body = await resp.Content.ReadFromJsonAsync<ErrorBody>();
        Assert.NotNull(body);
        Assert.Equal("UNAUTHORIZED", body!.Code);
    }

    private async Task<string> LoginAsync(string username, string password)
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = username, Password = password });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var token = await resp.Content.ReadFromJsonAsync<TokenResponse>();
        Assert.NotNull(token);
        return token!.AccessToken;
    }

    /// <summary>全局异常处理器返回的标准错误体(code/message)。</summary>
    private sealed class ErrorBody
    {
        public string Code { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
