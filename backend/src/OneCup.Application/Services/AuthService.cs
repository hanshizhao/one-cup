using FluentValidation;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneCup.Application.Common;
using OneCup.Application.Dtos.Auth;
using OneCup.Application.Interfaces;
using OneCup.Application.Options;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 认证服务实现：编排登录/刷新/登出/获取当前用户。
/// 通过 IRepository + Specification 访问数据,不直接依赖 EF Core;
/// 写入操作通过 IUnitOfWork 提交事务。
/// 承载 Stage A 安全特性:失败锁定(lockout-before-DB)、审计日志、中性错误消息。
/// </summary>
public class AuthService : IAuthService
{
    private readonly IRepository<User> _users;
    private readonly IRepository<RefreshToken> _refreshTokens;
    private readonly IUnitOfWork _uow;
    private readonly IJwtTokenService _jwt;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtOptions _options;
    private readonly IPermissionCalculator _permCalc;
    private readonly ILockoutStore _lockout;
    private readonly ILogger<AuthService> _logger;
    private readonly IValidator<LoginRequest> _loginValidator;
    private readonly IValidator<RefreshRequest> _refreshValidator;

    public AuthService(
        IRepository<User> users,
        IRepository<RefreshToken> refreshTokens,
        IUnitOfWork uow,
        IJwtTokenService jwt,
        IPasswordHasher passwordHasher,
        IOptions<JwtOptions> options,
        IPermissionCalculator permCalc,
        ILockoutStore lockout,
        ILogger<AuthService> logger,
        IValidator<LoginRequest> loginValidator,
        IValidator<RefreshRequest> refreshValidator)
    {
        _users = users;
        _refreshTokens = refreshTokens;
        _uow = uow;
        _jwt = jwt;
        _passwordHasher = passwordHasher;
        _options = options.Value;
        _permCalc = permCalc;
        _lockout = lockout;
        _logger = logger;
        _loginValidator = loginValidator;
        _refreshValidator = refreshValidator;
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        // 入参校验先行(在 lockout 查询之前),避免畸形用户名污染锁定计数。
        await _loginValidator.EnsureValidAsync(request, ct);

        var lockoutKey = request.Username.ToLowerInvariant();

        // 1. 先查锁定(不查库、不校验密码)
        if (await _lockout.IsLockedAsync(lockoutKey, ct))
        {
            var remaining = await _lockout.GetRemainingLockoutAsync(lockoutKey, ct);
            _logger.LogWarning("登录被拒(账号锁定):Username={Username}, 剩余={Remaining}", request.Username, remaining);
            throw new AccountLockedException(remaining);
        }

        // 2. 查用户(含 Roles.Permissions,登录后权限聚合需要)
        var user = await _users.FirstOrDefaultAsync(new UserByUsernameWithRolesSpec(request.Username), ct);

        if (user is null || !user.IsActive || !_passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            // 失败:记录 + 计数(不泄露用户是否存在)
            await _lockout.RecordFailureAsync(lockoutKey, ct);
            _logger.LogWarning("登录失败:Username={Username}", request.Username);
            throw new UnauthorizedException("用户名或密码错误");
        }

        // 3. 成功:重置计数 + 日志
        await _lockout.ResetAsync(lockoutKey, ct);
        _logger.LogInformation("登录成功:UserId={UserId}, Username={Username}", user.Id, user.Username);

        return await IssueTokensAsync(user, ct);
    }

    public async Task<TokenResponse> RefreshAsync(RefreshRequest request, CancellationToken ct = default)
    {
        await _refreshValidator.EnsureValidAsync(request, ct);

        // 加载刷新令牌(含 User→Roles→Permissions 三级 Include)。
        // tracked via FirstOrDefaultAsync(无 AsNoTracking),后续轮换(置 IsRevoked)随 SaveChanges 持久化。
        var stored = await _refreshTokens.FirstOrDefaultAsync(new RefreshTokenByTokenSpec(request.RefreshToken), ct);

        if (stored is null || stored.IsRevoked || stored.ExpiresAt <= DateTime.UtcNow)
        {
            throw new UnauthorizedException("刷新令牌无效或已过期");
        }

        // 轮换：吊销旧 token
        stored.IsRevoked = true;
        stored.UpdatedAt = DateTime.UtcNow;
        _logger.LogInformation("Refresh token 轮换吊销:UserId={UserId}, Token={TokenMask}",
            stored.User.Id, MaskToken(stored.Token));

        return await IssueTokensAsync(stored.User, ct);
    }

    public async Task LogoutAsync(Guid userId, CancellationToken ct = default)
    {
        // 加载该用户所有未吊销的刷新令牌。
        // ListAsync 走 AsNoTracking 返回 detached 实体,修改后需逐个 Update 重新 Attach 为 Modified,
        // 随 SaveChanges 持久化(与原 _db 的 tracked 行为等价)。
        var tokens = await _refreshTokens.ListAsync(new ActiveRefreshTokensByUserSpec(userId), ct);

        foreach (var rt in tokens)
        {
            rt.IsRevoked = true;
            rt.UpdatedAt = DateTime.UtcNow;
            _refreshTokens.Update(rt);
        }

        _logger.LogInformation("登出吊销 refresh token:UserId={UserId}, 数量={Count}", userId, tokens.Count);

        await _uow.SaveChangesAsync(ct);
    }

    public async Task<CurrentUser?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _users.FirstOrDefaultAsync(new UserByIdWithRolesPermissionsSpec(userId), ct);

        if (user is null) return null;

        var roleCodes = user.Roles.Select(r => r.Code).ToList();
        var permCodes = _permCalc.GetEffective(user);

        return new CurrentUser
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Roles = roleCodes,
            Permissions = permCodes.ToList(),
        };
    }

    /// <summary>签发 access + refresh token 对并持久化 refresh token。</summary>
    private async Task<TokenResponse> IssueTokensAsync(User user, CancellationToken ct)
    {
        var accessToken = _jwt.GenerateAccessToken(user);
        var refreshToken = new RefreshToken
        {
            Token = _jwt.GenerateRefreshToken(),
            UserId = user.Id,
            ExpiresAt = DateTime.UtcNow.AddDays(_options.RefreshTokenDays),
            IsRevoked = false,
        };

        await _refreshTokens.AddAsync(refreshToken, ct);
        await _uow.SaveChangesAsync(ct);

        return new TokenResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken.Token,
            ExpiresIn = _options.AccessTokenMinutes * 60,
        };
    }

    /// <summary>掩码 token,只保留前 8 字符用于日志识别。</summary>
    private static string MaskToken(string token) =>
        token.Length <= 8 ? "****" : $"{token[..8]}****";
}
