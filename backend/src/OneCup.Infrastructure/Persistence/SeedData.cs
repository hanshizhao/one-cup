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

    // 权限 Guid：第 4 段从 301 开始递增（细化后不复用旧 101-122；且避开业务字典 TargetType* 的 201-206，两者不同表但分离 Guid 空间更清晰）
    // 业务模块（6 个 × read/create/update/delete = 24）
    public static readonly Guid PermFabricRead = Guid.Parse("00000000-0000-0000-0000-000000000301");
    public static readonly Guid PermFabricCreate = Guid.Parse("00000000-0000-0000-0000-000000000302");
    public static readonly Guid PermFabricUpdate = Guid.Parse("00000000-0000-0000-0000-000000000303");
    public static readonly Guid PermFabricDelete = Guid.Parse("00000000-0000-0000-0000-000000000304");
    public static readonly Guid PermMaterialRead = Guid.Parse("00000000-0000-0000-0000-000000000305");
    public static readonly Guid PermMaterialCreate = Guid.Parse("00000000-0000-0000-0000-000000000306");
    public static readonly Guid PermMaterialUpdate = Guid.Parse("00000000-0000-0000-0000-000000000307");
    public static readonly Guid PermMaterialDelete = Guid.Parse("00000000-0000-0000-0000-000000000308");
    public static readonly Guid PermEquipmentRead = Guid.Parse("00000000-0000-0000-0000-000000000309");
    public static readonly Guid PermEquipmentCreate = Guid.Parse("00000000-0000-0000-0000-00000000030a");
    public static readonly Guid PermEquipmentUpdate = Guid.Parse("00000000-0000-0000-0000-00000000030b");
    public static readonly Guid PermEquipmentDelete = Guid.Parse("00000000-0000-0000-0000-00000000030c");
    public static readonly Guid PermCustomerRead = Guid.Parse("00000000-0000-0000-0000-00000000030d");
    public static readonly Guid PermCustomerCreate = Guid.Parse("00000000-0000-0000-0000-00000000030e");
    public static readonly Guid PermCustomerUpdate = Guid.Parse("00000000-0000-0000-0000-00000000030f");
    public static readonly Guid PermCustomerDelete = Guid.Parse("00000000-0000-0000-0000-000000000310");
    public static readonly Guid PermColorRead = Guid.Parse("00000000-0000-0000-0000-000000000311");
    public static readonly Guid PermColorCreate = Guid.Parse("00000000-0000-0000-0000-000000000312");
    public static readonly Guid PermColorUpdate = Guid.Parse("00000000-0000-0000-0000-000000000313");
    public static readonly Guid PermColorDelete = Guid.Parse("00000000-0000-0000-0000-000000000314");
    public static readonly Guid PermProductRead = Guid.Parse("00000000-0000-0000-0000-000000000315");
    public static readonly Guid PermProductCreate = Guid.Parse("00000000-0000-0000-0000-000000000316");
    public static readonly Guid PermProductUpdate = Guid.Parse("00000000-0000-0000-0000-000000000317");
    public static readonly Guid PermProductDelete = Guid.Parse("00000000-0000-0000-0000-000000000318");
    // 系统模块
    public static readonly Guid PermSystemUserRead = Guid.Parse("00000000-0000-0000-0000-000000000319");
    public static readonly Guid PermSystemUserCreate = Guid.Parse("00000000-0000-0000-0000-00000000031a");
    public static readonly Guid PermSystemUserUpdate = Guid.Parse("00000000-0000-0000-0000-00000000031b");
    public static readonly Guid PermSystemUserDelete = Guid.Parse("00000000-0000-0000-0000-00000000031c");
    public static readonly Guid PermSystemUserResetPassword = Guid.Parse("00000000-0000-0000-0000-00000000031d");
    public static readonly Guid PermSystemRoleRead = Guid.Parse("00000000-0000-0000-0000-00000000031e");
    public static readonly Guid PermSystemRoleCreate = Guid.Parse("00000000-0000-0000-0000-00000000031f");
    public static readonly Guid PermSystemRoleUpdate = Guid.Parse("00000000-0000-0000-0000-000000000320");
    public static readonly Guid PermSystemRoleDelete = Guid.Parse("00000000-0000-0000-0000-000000000321");
    public static readonly Guid PermSystemNumberingRead = Guid.Parse("00000000-0000-0000-0000-000000000322");
    public static readonly Guid PermSystemNumberingCreate = Guid.Parse("00000000-0000-0000-0000-000000000323");
    public static readonly Guid PermSystemNumberingUpdate = Guid.Parse("00000000-0000-0000-0000-000000000324");
    public static readonly Guid PermSystemNumberingDelete = Guid.Parse("00000000-0000-0000-0000-000000000325");
    public static readonly Guid PermSystemUnitRead = Guid.Parse("00000000-0000-0000-0000-000000000326");
    public static readonly Guid PermSystemUnitCreate = Guid.Parse("00000000-0000-0000-0000-000000000327");
    public static readonly Guid PermSystemUnitUpdate = Guid.Parse("00000000-0000-0000-0000-000000000328");
    public static readonly Guid PermSystemUnitDelete = Guid.Parse("00000000-0000-0000-0000-000000000329");
    public static readonly Guid PermSystemAuditRead = Guid.Parse("00000000-0000-0000-0000-00000000032a");

    /// <summary>
    /// admin 密码 Admin@123 的 BCrypt 哈希（workFactor 12，由 Task 4 Step 3 预计算）。
    /// </summary>
    public const string AdminPasswordHash = "$2a$12$Q.gT.FJroDeCmWFH6dHJcOdjxPIQgST/nEYCECypvJsLxj5wDQoSi";

    // 业务类型字典种子 Guid：第 4 段从 201 开始递增
    public static readonly Guid TargetTypeFabric = Guid.Parse("00000000-0000-0000-0000-000000000201");
    public static readonly Guid TargetTypeMaterial = Guid.Parse("00000000-0000-0000-0000-000000000202");
    public static readonly Guid TargetTypeEquipment = Guid.Parse("00000000-0000-0000-0000-000000000203");
    public static readonly Guid TargetTypeCustomer = Guid.Parse("00000000-0000-0000-0000-000000000204");
    public static readonly Guid TargetTypeColor = Guid.Parse("00000000-0000-0000-0000-000000000205");
    public static readonly Guid TargetTypeProduct = Guid.Parse("00000000-0000-0000-0000-000000000206");
}
