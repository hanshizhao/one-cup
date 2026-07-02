using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>
/// 认证场景查询规范:获取当前用户、刷新令牌轮换、登出批量吊销。
/// 登录用的 <see cref="UserByUsernameWithRolesSpec"/> 已在 UserSpecs 中定义(含 Roles+Permissions)。
/// </summary>

/// <summary>按 Id 查询用户(含 Roles.Permissions,获取当前用户用)。</summary>
public class UserByIdWithRolesPermissionsSpec : Specification<User>
{
    public UserByIdWithRolesPermissionsSpec(Guid id)
    {
        ApplyCriteria(u => u.Id == id);
        ApplyInclude("Roles");
        ApplyInclude("Roles.Permissions");
    }
}

/// <summary>
/// 按 token 字符串查询刷新令牌(含 User→Roles→Permissions 三级 Include)。
/// tracked via FirstOrDefaultAsync,以便后续轮换(置 IsRevoked)的修改随 SaveChanges 持久化。
/// </summary>
public class RefreshTokenByTokenSpec : Specification<RefreshToken>
{
    public RefreshTokenByTokenSpec(string token)
    {
        ApplyCriteria(rt => rt.Token == token);
        ApplyInclude("User");
        ApplyInclude("User.Roles");
        ApplyInclude("User.Roles.Permissions");
    }
}

/// <summary>某用户所有未吊销的刷新令牌(登出批量吊销用)。</summary>
public class ActiveRefreshTokensByUserSpec : Specification<RefreshToken>
{
    public ActiveRefreshTokensByUserSpec(Guid userId)
    {
        ApplyCriteria(rt => rt.UserId == userId && !rt.IsRevoked);
    }
}
