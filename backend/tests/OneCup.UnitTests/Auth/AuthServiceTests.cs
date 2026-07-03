using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneCup.Application.Dtos.Auth;
using OneCup.Application.Interfaces;
using OneCup.Application.Options;
using OneCup.Application.Services;
using OneCup.Application.Validators.Auth;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Auth;

/// <summary>
/// AuthService 单元测试：使用 EF Core InMemory + 手写 fake（IPasswordHasher / IJwtTokenService）。
/// 覆盖登录/刷新/登出/获取当前用户等核心认证编排逻辑。
/// </summary>
public class AuthServiceTests
{
    // 确定性 Guid（种子数据）
    private static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    private static readonly Guid DeveloperUserId = Guid.Parse("00000000-0000-0000-0000-000000000010");
    private static readonly Guid AdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    private static readonly Guid DeveloperRoleId = Guid.Parse("00000000-0000-0000-0000-000000000003");
    private static readonly Guid PermFabricRead = Guid.Parse("00000000-0000-0000-0000-000000000101");
    private static readonly Guid PermFabricWrite = Guid.Parse("00000000-0000-0000-0000-000000000102");
    private static readonly Guid PermMaterialRead = Guid.Parse("00000000-0000-0000-0000-000000000103");
    private static readonly Guid PermProductRead = Guid.Parse("00000000-0000-0000-0000-000000000111");

    private const string AdminPasswordHash = "hashed-admin-password";
    private const string DeveloperPasswordHash = "hashed-developer-password";
    private const string CorrectAdminPassword = "Admin@123";
    private const string CorrectDeveloperPassword = "Dev@123";

    private readonly JwtOptions _options = new()
    {
        Issuer = "test-issuer",
        Audience = "test-audience",
        SecretKey = "test-secret-key-that-is-at-least-32-chars-long!",
        AccessTokenMinutes = 30,
        RefreshTokenDays = 7,
    };

    /// <summary>
    /// 构建一个带种子数据（admin + developer 用户）的 in-memory DbContext。
    /// 每个测试用唯一数据库名以隔离状态。
    /// </summary>
    private async Task<OneCupDbContext> CreateContextAsync(string dbName)
    {
        var options = new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;

        var db = new OneCupDbContext(options);

        // ── 权限 ──
        var fabricRead = new Permission { Id = PermFabricRead, Code = "fabric:read", Name = "查看面料开发", CreatedAt = DateTime.UtcNow };
        var fabricWrite = new Permission { Id = PermFabricWrite, Code = "fabric:write", Name = "录入/编辑面料开发", CreatedAt = DateTime.UtcNow };
        var materialRead = new Permission { Id = PermMaterialRead, Code = "material:read", Name = "查看原料物料", CreatedAt = DateTime.UtcNow };
        var productRead = new Permission { Id = PermProductRead, Code = "product:read", Name = "查看产品", CreatedAt = DateTime.UtcNow };

        // ── 角色 ──
        var adminRole = new Role
        {
            Id = AdminRoleId,
            Name = "管理员",
            Code = "admin",
            Description = "系统超级管理员",
            Permissions = [], // admin 通配 *，不绑定权限
            CreatedAt = DateTime.UtcNow,
        };
        var developerRole = new Role
        {
            Id = DeveloperRoleId,
            Name = "开发员",
            Code = "developer",
            Description = "面料开发相关权限",
            Permissions = [fabricRead, fabricWrite, materialRead, productRead],
            CreatedAt = DateTime.UtcNow,
        };

        // ── 用户 ──
        var adminUser = new User
        {
            Id = AdminUserId,
            Username = "admin",
            PasswordHash = AdminPasswordHash,
            DisplayName = "管理员",
            IsActive = true,
            Roles = [adminRole],
            CreatedAt = DateTime.UtcNow,
        };
        var developerUser = new User
        {
            Id = DeveloperUserId,
            Username = "developer",
            PasswordHash = DeveloperPasswordHash,
            DisplayName = "开发员",
            IsActive = true,
            Roles = [developerRole],
            CreatedAt = DateTime.UtcNow,
        };

        db.Users.AddRange(adminUser, developerUser);
        await db.SaveChangesAsync();

        // 清空 ChangeTracker，确保后续查询从 store 重新加载（含导航属性）
        db.ChangeTracker.Clear();
        return db;
    }

    private AuthService CreateAuthService(
        OneCupDbContext db,
        FakePasswordHasher? passwordHasher = null,
        FakeJwtTokenService? jwt = null,
        ILockoutStore? lockout = null,
        IAuditLogWriter? auditWriter = null)
    {
        passwordHasher ??= new FakePasswordHasher();
        jwt ??= new FakeJwtTokenService();
        lockout ??= new FakeLockoutStore();
        auditWriter ??= new NoopAuditLogWriter();
        var permCalc = new PermissionCalculator();
        var logger = Microsoft.Extensions.Logging.Abstractions.NullLogger<AuthService>.Instance;
        return new AuthService(
            new Repository<User>(db), new Repository<RefreshToken>(db), new UnitOfWork(db), jwt, passwordHasher,
            Microsoft.Extensions.Options.Options.Create(_options), permCalc, lockout, logger, auditWriter,
            new LoginRequestValidator(), new RefreshRequestValidator());
    }

    // ════════════════════════════════════════════════════════════════
    // LoginAsync
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LoginAsync_WithCorrectCredentials_ReturnsTokenResponse()
    {
        // Arrange
        var db = await CreateContextAsync(nameof(LoginAsync_WithCorrectCredentials_ReturnsTokenResponse));
        var passwordHasher = new FakePasswordHasher
        {
            // admin 凭证校验通过
            VerifyResult = (password, hash) => hash == AdminPasswordHash && password == CorrectAdminPassword,
        };
        var jwt = new FakeJwtTokenService { AccessToken = "access-token-123" };
        var service = CreateAuthService(db, passwordHasher, jwt);

        // Act
        var result = await service.LoginAsync(new LoginRequest
        {
            Username = "admin",
            Password = CorrectAdminPassword,
        });

        // Assert
        Assert.NotNull(result);
        Assert.Equal("access-token-123", result.AccessToken);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.Equal(30 * 60, result.ExpiresIn);

        // refresh token 已持久化
        var stored = await db.RefreshTokens.SingleAsync(rt => rt.Token == result.RefreshToken);
        Assert.Equal(AdminUserId, stored.UserId);
        Assert.False(stored.IsRevoked);
    }

    [Fact]
    public async Task LoginAsync_WithWrongPassword_ThrowsUnauthorizedException()
    {
        // Arrange
        var db = await CreateContextAsync(nameof(LoginAsync_WithWrongPassword_ThrowsUnauthorizedException));
        var passwordHasher = new FakePasswordHasher { VerifyResult = (_, _) => false }; // 校验永不通过
        var service = CreateAuthService(db, passwordHasher);

        // Act + Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LoginAsync(new LoginRequest { Username = "admin", Password = "wrong-password" }));
        Assert.Contains("用户名或密码错误", ex.Message);
    }

    [Fact]
    public async Task LoginAsync_WithNonExistentUser_ThrowsUnauthorizedException()
    {
        // Arrange
        var db = await CreateContextAsync(nameof(LoginAsync_WithNonExistentUser_ThrowsUnauthorizedException));
        var service = CreateAuthService(db);

        // Act + Assert — 不存在的用户应抛 401 而非泄露用户不存在
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LoginAsync(new LoginRequest { Username = "ghost-user", Password = "any" }));
    }

    [Fact]
    public async Task LoginAsync_WithInactiveUser_ThrowsUnauthorizedException()
    {
        // Arrange — 植入一个已停用用户
        var dbName = nameof(LoginAsync_WithInactiveUser_ThrowsUnauthorizedException);
        var db = await CreateContextAsync(dbName);
        var inactiveUser = new User
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000020"),
            Username = "inactive",
            PasswordHash = "hashed-inactive",
            DisplayName = "停用用户",
            IsActive = false,
            Roles = [],
            CreatedAt = DateTime.UtcNow,
        };
        db.Users.Add(inactiveUser);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var passwordHasher = new FakePasswordHasher { VerifyResult = (_, _) => true }; // 即便密码对，停用也不允许
        var service = CreateAuthService(db, passwordHasher);

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.LoginAsync(new LoginRequest { Username = "inactive", Password = "correct-anyway" }));
    }

    [Fact]
    public async Task Login_locked_account_throws_AccountLockedException()
    {
        // 安排:存储报告该用户已锁定
        var lockedStore = new FakeLockoutStore { IsLockedResult = true, Remaining = TimeSpan.FromMinutes(10) };
        var db = await CreateContextAsync(nameof(Login_locked_account_throws_AccountLockedException));
        var sut = CreateAuthService(db, lockout: lockedStore);

        // 断言
        var ex = await Assert.ThrowsAsync<AccountLockedException>(
            () => sut.LoginAsync(new LoginRequest { Username = "admin", Password = "any" }, default));
        Assert.NotNull(ex.RetryAfter);
    }

    [Fact]
    public async Task Login_wrong_password_records_failure()
    {
        var store = new FakeLockoutStore();
        var db = await CreateContextAsync(nameof(Login_wrong_password_records_failure));
        var sut = CreateAuthService(db,
            passwordHasher: new FakePasswordHasher { VerifyResult = (_, _) => false },
            lockout: store);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => sut.LoginAsync(new LoginRequest { Username = "admin", Password = "wrong" }, default));
        Assert.Equal(1, store.FailureCount);
    }

    [Fact]
    public async Task Login_success_resets_failures()
    {
        var store = new FakeLockoutStore { FailureCount = 3 };
        var db = await CreateContextAsync(nameof(Login_success_resets_failures));
        var sut = CreateAuthService(db, lockout: store);

        await sut.LoginAsync(new LoginRequest { Username = "admin", Password = "pass" }, default);
        Assert.True(store.WasReset);
    }

    [Fact]
    public async Task Login_failure_at_threshold_enqueues_locked_audit_event()
    {
        // 安排：store 报告本次失败触发了锁定阈值
        var store = new FakeLockoutStore { LockTriggered = true };
        var spy = new LoginLogSpyWriter();
        var db = await CreateContextAsync(nameof(Login_failure_at_threshold_enqueues_locked_audit_event));
        var sut = CreateAuthService(db,
            passwordHasher: new FakePasswordHasher { VerifyResult = (_, _) => false }, // 密码错
            lockout: store,
            auditWriter: spy);

        // 断言：失败同时抛 UnauthorizedException
        await Assert.ThrowsAsync<UnauthorizedException>(
            () => sut.LoginAsync(new LoginRequest { Username = "admin", Password = "wrong" }, default));

        // 失败日志与锁定日志都写入（语义不同，都该记）
        var locked = spy.LoginLogs.SingleOrDefault(l => l.EventType == Domain.Enums.LoginEventType.Locked);
        Assert.NotNull(locked);
        Assert.Equal(Domain.Enums.OperationResult.Failed, locked!.Result);
        Assert.Equal("LockoutTriggered", locked.FailureReason);
        Assert.Equal(AdminUserId, locked.UserId); // 密码错场景，user 非 null

        // 同时仍有一条 Login/Failed 日志
        Assert.Contains(spy.LoginLogs, l => l.EventType == Domain.Enums.LoginEventType.Login
                                            && l.Result == Domain.Enums.OperationResult.Failed);
    }

    [Fact]
    public async Task Login_failure_below_threshold_does_not_enqueue_locked_audit_event()
    {
        // 安排：store 报告本次失败未触发锁定
        var store = new FakeLockoutStore { LockTriggered = false };
        var spy = new LoginLogSpyWriter();
        var db = await CreateContextAsync(nameof(Login_failure_below_threshold_does_not_enqueue_locked_audit_event));
        var sut = CreateAuthService(db,
            passwordHasher: new FakePasswordHasher { VerifyResult = (_, _) => false },
            lockout: store,
            auditWriter: spy);

        await Assert.ThrowsAsync<UnauthorizedException>(
            () => sut.LoginAsync(new LoginRequest { Username = "admin", Password = "wrong" }, default));

        // 未达阈值：只有 Login/Failed，无 Locked
        Assert.DoesNotContain(spy.LoginLogs, l => l.EventType == Domain.Enums.LoginEventType.Locked);
        Assert.Contains(spy.LoginLogs, l => l.EventType == Domain.Enums.LoginEventType.Login
                                            && l.Result == Domain.Enums.OperationResult.Failed);
    }

    // ════════════════════════════════════════════════════════════════
    // GetCurrentUserAsync
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetCurrentUserAsync_ForAdminUser_ReturnsWildcardPermissions()
    {
        // Arrange
        var db = await CreateContextAsync(nameof(GetCurrentUserAsync_ForAdminUser_ReturnsWildcardPermissions));
        var service = CreateAuthService(db);

        // Act
        var current = await service.GetCurrentUserAsync(AdminUserId);

        // Assert
        Assert.NotNull(current);
        Assert.Equal(AdminUserId, current!.Id);
        Assert.Equal("admin", current.Username);
        Assert.Contains("admin", current.Roles);
        // admin 角色 → 权限通配为 ["*"]
        Assert.Equal(["*"], current.Permissions);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ForNonAdminUser_ReturnsAggregatedDistinctPermissions()
    {
        // Arrange
        var db = await CreateContextAsync(nameof(GetCurrentUserAsync_ForNonAdminUser_ReturnsAggregatedDistinctPermissions));
        var service = CreateAuthService(db);

        // Act
        var current = await service.GetCurrentUserAsync(DeveloperUserId);

        // Assert — developer 角色聚合其绑定的 4 个权限编码（去重）
        Assert.NotNull(current);
        Assert.Contains("developer", current!.Roles);
        Assert.NotEmpty(current.Permissions);
        Assert.Contains("fabric:read", current.Permissions);
        Assert.Contains("fabric:write", current.Permissions);
        Assert.Contains("material:read", current.Permissions);
        Assert.Contains("product:read", current.Permissions);
        // 不含通配
        Assert.DoesNotContain("*", current.Permissions);
        // 去重：无重复
        Assert.Equal(current.Permissions.Distinct().Count(), current.Permissions.Count);
    }

    [Fact]
    public async Task GetCurrentUserAsync_ForNonExistentUser_ReturnsNull()
    {
        // Arrange
        var db = await CreateContextAsync(nameof(GetCurrentUserAsync_ForNonExistentUser_ReturnsNull));
        var service = CreateAuthService(db);

        // Act
        var current = await service.GetCurrentUserAsync(Guid.Parse("ffffffff-ffff-ffff-ffff-ffffffffffff"));

        // Assert
        Assert.Null(current);
    }

    // ════════════════════════════════════════════════════════════════
    // RefreshAsync
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task RefreshAsync_WithValidToken_ReturnsNewTokenPairAndRevokesOldToken()
    {
        // Arrange
        var db = await CreateContextAsync(nameof(RefreshAsync_WithValidToken_ReturnsNewTokenPairAndRevokesOldToken));
        const string oldRefreshToken = "old-valid-refresh-token";

        var storedToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = oldRefreshToken,
            UserId = AdminUserId,
            ExpiresAt = DateTime.UtcNow.AddDays(1), // 未过期
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
        };
        db.RefreshTokens.Add(storedToken);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var jwt = new FakeJwtTokenService { AccessToken = "new-access-token" };
        var service = CreateAuthService(db, jwt: jwt);

        // Act
        var result = await service.RefreshAsync(new RefreshRequest { RefreshToken = oldRefreshToken });

        // Assert — 返回新的令牌对
        Assert.NotNull(result);
        Assert.Equal("new-access-token", result.AccessToken);
        Assert.False(string.IsNullOrEmpty(result.RefreshToken));
        Assert.NotEqual(oldRefreshToken, result.RefreshToken);
        Assert.Equal(30 * 60, result.ExpiresIn);

        // 旧 token 已被吊销（轮换）
        var revokedOld = await db.RefreshTokens.AsNoTracking().FirstAsync(rt => rt.Token == oldRefreshToken);
        Assert.True(revokedOld.IsRevoked);
    }

    [Fact]
    public async Task RefreshAsync_WithRevokedToken_ThrowsUnauthorizedException()
    {
        // Arrange
        var db = await CreateContextAsync(nameof(RefreshAsync_WithRevokedToken_ThrowsUnauthorizedException));
        const string revokedToken = "already-revoked-token";

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = revokedToken,
            UserId = AdminUserId,
            ExpiresAt = DateTime.UtcNow.AddDays(1),
            IsRevoked = true, // 已吊销
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = CreateAuthService(db);

        // Act + Assert
        var ex = await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.RefreshAsync(new RefreshRequest { RefreshToken = revokedToken }));
        Assert.Contains("刷新令牌无效或已过期", ex.Message);
    }

    [Fact]
    public async Task RefreshAsync_WithExpiredToken_ThrowsUnauthorizedException()
    {
        // Arrange
        var db = await CreateContextAsync(nameof(RefreshAsync_WithExpiredToken_ThrowsUnauthorizedException));
        const string expiredToken = "expired-token";

        db.RefreshTokens.Add(new RefreshToken
        {
            Id = Guid.NewGuid(),
            Token = expiredToken,
            UserId = AdminUserId,
            ExpiresAt = DateTime.UtcNow.AddDays(-1), // 已过期
            IsRevoked = false,
            CreatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = CreateAuthService(db);

        // Act + Assert
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.RefreshAsync(new RefreshRequest { RefreshToken = expiredToken }));
    }

    [Fact]
    public async Task RefreshAsync_WithUnknownToken_ThrowsUnauthorizedException()
    {
        // Arrange
        var db = await CreateContextAsync(nameof(RefreshAsync_WithUnknownToken_ThrowsUnauthorizedException));
        var service = CreateAuthService(db);

        // Act + Assert — 数据库中不存在的 token
        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            service.RefreshAsync(new RefreshRequest { RefreshToken = "totally-unknown-token" }));
    }

    // ════════════════════════════════════════════════════════════════
    // LogoutAsync
    // ════════════════════════════════════════════════════════════════

    [Fact]
    public async Task LogoutAsync_RevokesAllNonRevokedTokensForUser()
    {
        // Arrange
        var db = await CreateContextAsync(nameof(LogoutAsync_RevokesAllNonRevokedTokensForUser));
        var now = DateTime.UtcNow;

        // 3 个 token：2 个有效 + 1 个已吊销
        var active1 = new RefreshToken { Id = Guid.NewGuid(), Token = "active-1", UserId = AdminUserId, ExpiresAt = now.AddDays(1), IsRevoked = false, CreatedAt = now };
        var active2 = new RefreshToken { Id = Guid.NewGuid(), Token = "active-2", UserId = AdminUserId, ExpiresAt = now.AddDays(1), IsRevoked = false, CreatedAt = now };
        var alreadyRevoked = new RefreshToken { Id = Guid.NewGuid(), Token = "revoked-1", UserId = AdminUserId, ExpiresAt = now.AddDays(1), IsRevoked = true, CreatedAt = now };

        // 另一用户的 token — 登出不应影响它
        var otherUserToken = new RefreshToken { Id = Guid.NewGuid(), Token = "other-user-active", UserId = DeveloperUserId, ExpiresAt = now.AddDays(1), IsRevoked = false, CreatedAt = now };

        db.RefreshTokens.AddRange(active1, active2, alreadyRevoked, otherUserToken);
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var service = CreateAuthService(db);

        // Act
        await service.LogoutAsync(AdminUserId);

        // Assert — admin 的所有有效 token 被吊销
        var tokens = await db.RefreshTokens.AsNoTracking().Where(rt => rt.UserId == AdminUserId).ToListAsync();
        Assert.All(tokens, t => Assert.True(t.IsRevoked));

        // 已吊销的保持吊销，不影响结果
        Assert.True(tokens.Single(t => t.Token == "revoked-1").IsRevoked);

        // 另一用户的 token 不受影响
        var other = await db.RefreshTokens.AsNoTracking().FirstAsync(rt => rt.Token == "other-user-active");
        Assert.False(other.IsRevoked);
    }

    // ════════════════════════════════════════════════════════════════
    // Fakes — 手写 fake 实现（项目未引入 Moq / NSubstitute）
    // ════════════════════════════════════════════════════════════════

    /// <summary>可控的密码哈希 fake：Hash 直接返回明文，Verify 走可注入委托。</summary>
    private sealed class FakePasswordHasher : IPasswordHasher
    {
        public Func<string, string, bool> VerifyResult { get; set; } = (_, _) => true;

        public string Hash(string password) => $"hashed-{password}";

        public bool Verify(string password, string hash) => VerifyResult(password, hash);
    }

    /// <summary>可控的 JWT fake：AccessToken 固定/可注入，RefreshToken 每次返回不同值。</summary>
    private sealed class FakeJwtTokenService : IJwtTokenService
    {
        public string AccessToken { get; set; } = "fake-access-token";
        private int _refreshCounter;

        public string GenerateAccessToken(User user) => AccessToken;

        public string GenerateRefreshToken() => $"fake-refresh-{++_refreshCounter}";
    }

    /// <summary>可控的锁定存储 fake。</summary>
    private sealed class FakeLockoutStore : ILockoutStore
    {
        public bool IsLockedResult { get; set; }
        public TimeSpan? Remaining { get; set; }
        public int FailureCount { get; set; }
        public bool WasReset { get; set; }
        /// <summary>RecordFailureAsync 的返回值：true 表示本次失败触发了锁定。</summary>
        public bool LockTriggered { get; set; }

        public Task<bool> IsLockedAsync(string key, CancellationToken ct = default) => Task.FromResult(IsLockedResult);
        public Task<bool> RecordFailureAsync(string key, CancellationToken ct = default) { FailureCount++; return Task.FromResult(LockTriggered); }
        public Task ResetAsync(string key, CancellationToken ct = default) { WasReset = true; return Task.CompletedTask; }
        public Task<TimeSpan?> GetRemainingLockoutAsync(string key, CancellationToken ct = default) => Task.FromResult(Remaining);
    }

    /// <summary>
    /// 空操作审计日志写入器：现 AuthServiceTests 不断言登录日志内容，
    /// 仅满足构造函数依赖。需断言登录日志采集时，注入 LoginLogSpyWriter 替代。
    /// </summary>
    private sealed class NoopAuditLogWriter : IAuditLogWriter
    {
        public void Enqueue(OperationLog log) { }
        public void Enqueue(LoginLog log) { }
    }

    /// <summary>采集 LoginLog 的 spy 写入器，用于断言登录审计事件。</summary>
    private sealed class LoginLogSpyWriter : IAuditLogWriter
    {
        public List<LoginLog> LoginLogs { get; } = [];
        public void Enqueue(OperationLog log) { }
        public void Enqueue(LoginLog log) => LoginLogs.Add(log);
    }
}
