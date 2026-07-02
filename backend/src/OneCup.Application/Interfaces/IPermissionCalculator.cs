using OneCup.Domain.Entities;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 权限计算的单一来源:admin 通配判断、角色权限聚合。
/// 消除 AuthService / JwtTokenService / WildcardHandler 三处重复。
/// </summary>
public interface IPermissionCalculator
{
    /// <summary>权限编码集合是否含通配 "*"。</summary>
    bool IsWildcard(IReadOnlyCollection<string> permCodes);

    /// <summary>
    /// 计算用户的生效权限编码:含 admin 角色返回 ["*"],
    /// 否则聚合所有角色的权限并去重。
    /// </summary>
    IReadOnlyList<string> GetEffective(User user);
}
