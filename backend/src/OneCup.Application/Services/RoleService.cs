using FluentValidation;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 角色管理服务实现。
/// 通过 IRepository + Specification 访问数据,不直接依赖 EF Core;
/// 写入操作通过 IUnitOfWork 提交事务。
/// </summary>
public class RoleService : IRoleService
{
    private readonly IRepository<Role> _roles;
    private readonly IRepository<Permission> _permissions;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<CreateRoleRequest> _createValidator;
    private readonly IValidator<UpdateRoleRequest> _updateValidator;

    public RoleService(
        IRepository<Role> roles,
        IRepository<Permission> permissions,
        IUnitOfWork uow,
        IValidator<CreateRoleRequest> createValidator,
        IValidator<UpdateRoleRequest> updateValidator)
    {
        _roles = roles;
        _permissions = permissions;
        _uow = uow;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    /// <summary>手动校验请求 DTO,失败抛 DomainException(全局映射 400)。</summary>
    private static async Task ValidateAsync<T>(IValidator<T> validator, T request, CancellationToken ct)
    {
        var result = await validator.ValidateAsync(request, ct);
        if (!result.IsValid)
        {
            throw new DomainException(string.Join("; ", result.Errors.Select(e => e.ErrorMessage)));
        }
    }

    public async Task<List<RoleListItemDto>> GetListAsync(CancellationToken ct = default)
    {
        // 一次性加载角色及其 Permissions 与 Users 集合;
        // UserCount / PermissionCount 在内存中统计,保持与原实现一致(避免多次往返)。
        var roles = await _roles.ListAsync(new RolesWithPermissionsSpec(), ct);

        return roles.Select(r => new RoleListItemDto
        {
            Id = r.Id,
            Name = r.Name,
            Code = r.Code,
            Description = r.Description,
            CreatedAt = r.CreatedAt,
            UserCount = r.Users.Count,
            PermissionCount = r.Permissions.Count,
        }).ToList();
    }

    public async Task<RoleDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _roles.FirstOrDefaultAsync(new RoleWithPermissionsSpec(id), ct);

        if (role is null) return null;

        return new RoleDto
        {
            Id = role.Id,
            Name = role.Name,
            Code = role.Code,
            Description = role.Description,
            CreatedAt = role.CreatedAt,
            PermissionIds = role.Permissions.Select(p => p.Id).ToList(),
        };
    }

    public async Task<RoleDto> CreateAsync(CreateRoleRequest request, CancellationToken ct = default)
    {
        await ValidateAsync(_createValidator, request, ct);

        // 编码唯一性校验
        if (await _roles.AnyAsync(new RoleByCodeSpec(request.Code), ct))
        {
            throw new DomainException($"角色编码「{request.Code}」已存在");
        }

        var role = new Role
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
        };

        await _roles.AddAsync(role, ct);
        await _uow.SaveChangesAsync(ct);

        return await GetByIdAsync(role.Id, ct) ?? throw new DomainException("角色创建失败");
    }

    public async Task<RoleDto> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default)
    {
        await ValidateAsync(_updateValidator, request, ct);

        // 加载需修改的角色(含 Permissions),tracked via FirstOrDefaultAsync(无 AsNoTracking)。
        var role = await _roles.FirstOrDefaultAsync(new RoleWithPermissionsSpec(id), ct)
            ?? throw new DomainException("角色不存在");

        // 按权限 Id 逐个加载:GetByIdAsync(FindAsync) 返回变更跟踪器已跟踪的实例,
        // 复用同一实例避免重复跟踪冲突(ListAsync 走 AsNoTracking 会带来 detached 实体,
        // 直接赋给 role.Permissions 时 InMemory/EF 会重复 Attach,触发主键冲突)。
        var permissions = new List<Permission>();
        foreach (var permissionId in request.PermissionIds)
        {
            var permission = await _permissions.GetByIdAsync(permissionId, ct);
            if (permission is not null) permissions.Add(permission);
        }

        role.Name = request.Name;
        role.Description = request.Description;
        role.Permissions = permissions;

        await _uow.SaveChangesAsync(ct);

        return await GetByIdAsync(role.Id, ct) ?? throw new DomainException("角色更新失败");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // 加载需删除的角色,tracked via FirstOrDefaultAsync(无 AsNoTracking)。
        var role = await _roles.FirstOrDefaultAsync(new RoleWithPermissionsSpec(id), ct)
            ?? throw new DomainException("角色不存在");

        // admin 角色保护:系统内置管理员角色不可删除。
        if (role.Code == SystemConstants.AdminRoleCode)
        {
            throw new DomainException("系统内置管理员角色不可删除");
        }

        // 有关联用户则拒绝:重新加载该角色的 Users 集合统计(原实现通过聚合查询)。
        // 这里 GetByIdAsync 返回 tracked 实体但不加载 Users 导航,故单独按 Id 再取一次含 Users 的角色。
        var roleWithUsers = await _roles.FirstOrDefaultAsync(new RoleWithUsersSpec(id), ct);
        var userCount = roleWithUsers?.Users.Count ?? 0;

        if (userCount > 0)
        {
            throw new DomainException($"该角色下还有 {userCount} 个用户，请先解除关联后再删除");
        }

        _roles.Remove(role);
        await _uow.SaveChangesAsync(ct);
    }
}
