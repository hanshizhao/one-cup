namespace OneCup.Infrastructure.Persistence;

/// <summary>
/// 种子数据常量。Guid 使用确定性值（HasData 要求主键固定）。
/// </summary>
internal static class SeedData
{
    // 固定 Guid（确定性）
    public static readonly Guid AdminUserId = Guid.Parse("00000000-0000-0000-0000-000000000001");
    public static readonly Guid AdminRoleId = Guid.Parse("00000000-0000-0000-0000-000000000002");
    public static readonly Guid DeveloperRoleId = Guid.Parse("00000000-0000-0000-0000-000000000003");

    // 权限 Guid：第 4 段从 101 开始递增
    public static readonly Guid PermFabricRead = Guid.Parse("00000000-0000-0000-0000-000000000101");
    public static readonly Guid PermFabricWrite = Guid.Parse("00000000-0000-0000-0000-000000000102");
    public static readonly Guid PermMaterialRead = Guid.Parse("00000000-0000-0000-0000-000000000103");
    public static readonly Guid PermMaterialWrite = Guid.Parse("00000000-0000-0000-0000-000000000104");
    public static readonly Guid PermEquipmentRead = Guid.Parse("00000000-0000-0000-0000-000000000105");
    public static readonly Guid PermEquipmentWrite = Guid.Parse("00000000-0000-0000-0000-000000000106");
    public static readonly Guid PermCustomerRead = Guid.Parse("00000000-0000-0000-0000-000000000107");
    public static readonly Guid PermCustomerWrite = Guid.Parse("00000000-0000-0000-0000-000000000108");
    public static readonly Guid PermColorRead = Guid.Parse("00000000-0000-0000-0000-000000000109");
    public static readonly Guid PermColorWrite = Guid.Parse("00000000-0000-0000-0000-000000000110");
    public static readonly Guid PermProductRead = Guid.Parse("00000000-0000-0000-0000-000000000111");
    public static readonly Guid PermSystemUserManage = Guid.Parse("00000000-0000-0000-0000-000000000112");
    public static readonly Guid PermSystemRoleManage = Guid.Parse("00000000-0000-0000-0000-000000000113");
    public static readonly Guid PermSystemNumberingView = Guid.Parse("00000000-0000-0000-0000-000000000114");
    public static readonly Guid PermSystemNumberingManage = Guid.Parse("00000000-0000-0000-0000-000000000115");

    /// <summary>
    /// admin 密码 Admin@123 的 BCrypt 哈希（workFactor 12，由 Task 4 Step 3 预计算）。
    /// </summary>
    public const string AdminPasswordHash = "$2a$12$Q.gT.FJroDeCmWFH6dHJcOdjxPIQgST/nEYCECypvJsLxj5wDQoSi";
}
