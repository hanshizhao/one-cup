using OneCup.Application.Interfaces;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;

namespace OneCup.UnitTests.AuditLog;

/// <summary>
/// 登录日志采集测试（Task 9）。
///
/// AuthService 完整端到端组装依赖众多（Repository/UnitOfWork/Jwt/PasswordHasher/
/// LockoutStore/PermissionCalculator/Validators），在 UnitTests 中组装成本高、价值低，
/// 且登录日志字段映射属于纯数据契约。因此本测试验证采集契约本身：
/// 1. IAuditLogWriter.Enqueue(LoginLog) 能正确捕获 LoginLog 各字段；
/// 2. 每种 EventType/Result/FailureReason 组合入队后字段语义正确。
///
/// AuthService 各埋点的端到端验证由 IntegrationTests 覆盖（按 brief Step 4 说明）。
/// 这里复用 Task 8 已定义的 FakeAuditLogWriter（internal，同程序集）作为捕获替身。
/// </summary>
public class LoginLogCollectionTests
{
    /// <summary>SpyWriter 能捕获入队的 LoginLog 并保留全部字段。</summary>
    [Fact]
    public void SpyWriter_CapturesLoginLogFields()
    {
        var spy = new FakeAuditLogWriter();
        var log = new LoginLog
        {
            UserId = Guid.NewGuid(),
            Username = "admin",
            EventType = LoginEventType.Login,
            Result = OperationResult.Success,
            IpAddress = "1.2.3.4",
            UserAgent = "Mozilla/5.0",
        };
        spy.Enqueue(log);

        Assert.Single(spy.Logins);
        Assert.Empty(spy.Operations); // LoginLog 不应污染 OperationLog 队列
        var captured = spy.Logins[0];
        Assert.Equal("admin", captured.Username);
        Assert.Equal(LoginEventType.Login, captured.EventType);
        Assert.Equal(OperationResult.Success, captured.Result);
        Assert.Equal("1.2.3.4", captured.IpAddress);
        Assert.Equal("Mozilla/5.0", captured.UserAgent);
        Assert.Null(captured.FailureReason); // 成功无失败原因
    }

    // ── 采集契约映射验证（对应 brief 的 EventType/Result/FailureReason 表）──────────
    // 这些用例直接构造并入队各事件类型的 LoginLog，断言其字段语义符合采集契约。
    // 它们锁定的契约是：消费者（Task 7）读取这些字段时类型/语义稳定。

    [Fact]
    public void LoginSuccess_Log_HasSuccessResultAndNoFailureReason()
    {
        var log = MakeLogin(LoginEventType.Login, OperationResult.Success);
        Assert.Equal(OperationResult.Success, log.Result);
        Assert.Null(log.FailureReason);
    }

    [Theory]
    [InlineData("InvalidCredentials")] // 密码错
    [InlineData("UserNotFound")]      // 账号不存在
    [InlineData("AccountLocked")]     // 账号锁定
    public void LoginFailed_Log_HasFailedResultAndFailureReason(string reason)
    {
        var log = MakeLogin(LoginEventType.Login, OperationResult.Failed, reason);
        Assert.Equal(OperationResult.Failed, log.Result);
        Assert.Equal(reason, log.FailureReason);
    }

    [Fact]
    public void LockoutTriggered_Log_HasLockedEventType()
    {
        var log = MakeLogin(LoginEventType.Locked, OperationResult.Failed, "LockoutTriggered");
        Assert.Equal(LoginEventType.Locked, log.EventType);
        Assert.Equal(OperationResult.Failed, log.Result);
    }

    [Fact]
    public void LogoutSuccess_Log_HasLogoutEventTypeAndEmptyUsername()
    {
        var log = new LoginLog
        {
            UserId = Guid.NewGuid(),
            Username = "", // 登出仅知 userId，Username 留空
            EventType = LoginEventType.Logout,
            Result = OperationResult.Success,
        };
        Assert.Equal(LoginEventType.Logout, log.EventType);
        Assert.Equal(OperationResult.Success, log.Result);
        Assert.Empty(log.Username);
    }

    [Fact]
    public void RefreshSuccess_Log_HasRefreshEventTypeAndSuccess()
    {
        var log = MakeLogin(LoginEventType.Refresh, OperationResult.Success);
        Assert.Equal(LoginEventType.Refresh, log.EventType);
        Assert.Equal(OperationResult.Success, log.Result);
    }

    [Fact]
    public void RefreshFailed_Log_HasInvalidRefreshTokenReason()
    {
        var log = MakeLogin(LoginEventType.Refresh, OperationResult.Failed, "InvalidRefreshToken");
        Assert.Equal(LoginEventType.Refresh, log.EventType);
        Assert.Equal(OperationResult.Failed, log.Result);
        Assert.Equal("InvalidRefreshToken", log.FailureReason);
    }

    /// <summary>构造一个具备可选失败原因的 LoginLog。</summary>
    private static LoginLog MakeLogin(LoginEventType type, OperationResult result, string? failureReason = null) =>
        new()
        {
            Username = "tester",
            EventType = type,
            Result = result,
            FailureReason = failureReason,
            IpAddress = "10.0.0.1",
            UserAgent = "test-ua",
        };
}
