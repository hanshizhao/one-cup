using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>查询全部角色(含 Permissions,列表页用)。</summary>
public class RolesWithPermissionsSpec : Specification<Role>
{
    public RolesWithPermissionsSpec()
    {
        ApplyInclude("Permissions");
        ApplyInclude("Users");
    }
}

/// <summary>按 Id 查询角色(含 Permissions,详情/更新用,tracked via FirstOrDefaultAsync)。</summary>
public class RoleWithPermissionsSpec : Specification<Role>
{
    public RoleWithPermissionsSpec(Guid id)
    {
        ApplyCriteria(r => r.Id == id);
        ApplyInclude("Permissions");
    }
}

/// <summary>按 Id 查询角色(含 Users,删除前关联用户计数用)。</summary>
public class RoleWithUsersSpec : Specification<Role>
{
    public RoleWithUsersSpec(Guid id)
    {
        ApplyCriteria(r => r.Id == id);
        ApplyInclude("Users");
    }
}

/// <summary>按编码唯一性校验(仅条件,用于 AnyAsync)。</summary>
public class RoleByCodeSpec : Specification<Role>
{
    public RoleByCodeSpec(string code)
    {
        ApplyCriteria(r => r.Code == code);
    }
}
