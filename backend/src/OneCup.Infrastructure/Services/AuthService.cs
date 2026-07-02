using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneCup.Application.Dtos.Auth;
using OneCup.Application.Interfaces;
using OneCup.Application.Options;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;

namespace OneCup.Infrastructure.Services;

/// <summary>
/// 认证服务实现：编排登录/刷新/登出/获取当前用户。
/// </summary>
public class AuthService : IAuthService
{
    private readonly OneCupDbContext _db;
    private readonly IJwtTokenService _jwt;
    private readonly IPasswordHasher _passwordHasher;
    private readonly JwtOptions _options;
    private readonly IPermissionCalculator _permCalc;
    private readonly ILockoutStore _lockout;
    private readonly ILogger<AuthService> _logger;

    public AuthService(
        OneCupDbContext db,
        IJwtTokenService jwt,
        IPasswordHasher passwordHasher,
        IOptions<JwtOptions> options,
        IPermissionCalculator permCalc,
        ILockoutStore lockout,
        ILogger<AuthService> logger)
    {
        _db = db;
        _jwt = jwt;
        _passwordHasher = passwordHasher;
        _options = options.Value;
        _permCalc = permCalc;
        _lockout = lockout;
        _logger = logger;
    }

    public async Task<TokenResponse> LoginAsync(LoginRequest request, CancellationToken ct = default)
    {
        var lockoutKey = request.Username.ToLowerInvariant();

        // 1. 先查锁定(不查库、不校验密码)
        if (await _lockout.IsLockedAsync(lockoutKey, ct))
        {
            var remaining = await _lockout.GetRemainingLockoutAsync(lockoutKey, ct);
            _logger.LogWarning("登录被拒(账号锁定):Username={Username}, 剩余={Remaining}", request.Username, remaining);
            throw new AccountLockedException(remaining);
        }

        // 2. 查用户
        var user = await _db.Users
            .Include(u => u.Roles).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Username == request.Username, ct);

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
        var stored = await _db.RefreshTokens
            .Include(rt => rt.User).ThenInclude(u => u.Roles).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(rt => rt.Token == request.RefreshToken, ct);

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
        var tokens = await _db.RefreshTokens
            .Where(rt => rt.UserId == userId && !rt.IsRevoked)
            .ToListAsync(ct);

        foreach (var rt in tokens)
        {
            rt.IsRevoked = true;
            rt.UpdatedAt = DateTime.UtcNow;
        }

        _logger.LogInformation("登出吊销 refresh token:UserId={UserId}, 数量={Count}", userId, tokens.Count);

        await _db.SaveChangesAsync(ct);
    }

    public async Task<CurrentUser?> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.Roles).ThenInclude(r => r.Permissions)
            .FirstOrDefaultAsync(u => u.Id == userId, ct);

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

        _db.RefreshTokens.Add(refreshToken);
        await _db.SaveChangesAsync(ct);

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
