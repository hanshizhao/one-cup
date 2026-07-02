using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>
/// 仅按 keyword 过滤的用户查询规范(不含分页/排序/Include)。
/// 专用于 CountAsync 统计符合条件的总数——若用带分页的规范统计,
/// Repository.CountAsync 会应用 Skip/Take,导致只统计当前页子集而非总数。
/// 与 <see cref="UserPagedSpec"/> 共享相同的 Where 条件,保证计数与数据一致。
/// </summary>
public class UserFilterSpec : Specification<User>
{
    public UserFilterSpec(string? keyword)
    {
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim();
            ApplyCriteria(u => u.Username.Contains(keyword) || u.DisplayName.Contains(keyword));
        }
    }
}

/// <summary>用户分页查询(含 keyword 过滤、按创建时间倒序、Include Roles)。</summary>
public class UserPagedSpec : Specification<User>
{
    public UserPagedSpec(string? keyword, int page, int pageSize)
    {
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim();
            ApplyCriteria(u => u.Username.Contains(keyword) || u.DisplayName.Contains(keyword));
        }
        ApplyInclude("Roles");
        ApplyOrderByDescending(u => u.CreatedAt);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>按 Id 查询用户(含 Roles)。</summary>
public class UserByIdWithRolesSpec : Specification<User>
{
    public UserByIdWithRolesSpec(Guid id)
    {
        ApplyCriteria(u => u.Id == id);
        ApplyInclude("Roles");
    }
}

/// <summary>按用户名查询用户(含 Roles+Permissions,登录/当前用户场景使用)。</summary>
public class UserByUsernameWithRolesSpec : Specification<User>
{
    public UserByUsernameWithRolesSpec(string username)
    {
        ApplyCriteria(u => u.Username == username);
        ApplyInclude("Roles");
        ApplyInclude("Roles.Permissions");
    }
}

/// <summary>按用户名精确匹配(仅条件,用于唯一性 AnyAsync 校验)。</summary>
public class UserByUsernameSpec : Specification<User>
{
    public UserByUsernameSpec(string username)
    {
        ApplyCriteria(u => u.Username == username);
    }
}

/// <summary>按 Id 集合批量加载角色(创建/更新用户时按 RoleIds 回填)。</summary>
public class RolesByIdsSpec : Specification<Role>
{
    public RolesByIdsSpec(IEnumerable<Guid> ids)
    {
        var idList = ids.ToList();
        ApplyCriteria(r => idList.Contains(r.Id));
    }
}
