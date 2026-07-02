namespace OneCup.Application.Common;

/// <summary>
/// 系统级公开常量。供 Application 层 Service 做业务保护(admin 账号/角色),
/// 不依赖 Infrastructure 的 internal SeedData。
/// </summary>
public static class SystemConstants
{
    /// <summary>内置 admin 用户 Id(与 SeedData 一致)。</summary>
    public static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    /// <summary>内置 admin 角色 Id。</summary>
    public static readonly Guid AdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");

    /// <summary>admin 角色编码。</summary>
    public const string AdminRoleCode = "admin";
}
