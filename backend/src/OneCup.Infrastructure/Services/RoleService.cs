using Microsoft.EntityFrameworkCore;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;

namespace OneCup.Infrastructure.Services;

/// <summary>
/// 角色管理服务实现。
/// </summary>
public class RoleService : IRoleService
{
    private readonly OneCupDbContext _db;

    public RoleService(OneCupDbContext db)
    {
        _db = db;
    }

    public async Task<List<RoleListItemDto>> GetListAsync(CancellationToken ct = default)
    {
        var roles = await _db.Roles
            .Include(r => r.Permissions)
            .ToListAsync(ct);

        // 批量统计每个角色的用户数：通过角色导航按 Id 分组计数（可被任意 provider 翻译）
        var userCounts = await _db.Roles
            .Select(r => new { RoleId = r.Id, Count = r.Users.Count })
            .ToDictionaryAsync(x => x.RoleId, x => x.Count, ct);

        return roles.Select(r => new RoleListItemDto
        {
            Id = r.Id,
            Name = r.Name,
            Code = r.Code,
            Description = r.Description,
            CreatedAt = r.CreatedAt,
            UserCount = userCounts.GetValueOrDefault(r.Id, 0),
            PermissionCount = r.Permissions.Count,
        }).ToList();
    }

    public async Task<RoleDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _db.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, ct);

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
        if (await _db.Roles.AnyAsync(r => r.Code == request.Code, ct))
        {
            throw new DomainException($"角色编码「{request.Code}」已存在");
        }

        var role = new Role
        {
            Name = request.Name,
            Code = request.Code,
            Description = request.Description,
        };

        _db.Roles.Add(role);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(role.Id, ct) ?? throw new DomainException("角色创建失败");
    }

    public async Task<RoleDto> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default)
    {
        var role = await _db.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new DomainException("角色不存在");

        var permissions = await _db.Permissions
            .Where(p => request.PermissionIds.Contains(p.Id))
            .ToListAsync(ct);

        role.Name = request.Name;
        role.Description = request.Description;
        role.Permissions = permissions;

        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(role.Id, ct) ?? throw new DomainException("角色更新失败");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var role = await _db.Roles
            .Include(r => r.Permissions)
            .FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new DomainException("角色不存在");

        // admin 角色不可删除
        if (role.Code == "admin")
        {
            throw new DomainException("系统内置管理员角色不可删除");
        }

        // 有关联用户则拒绝
        var userCount = await _db.Users
            .SelectMany(u => u.Roles.Select(r => r.Id))
            .CountAsync(rid => rid == id, ct);

        if (userCount > 0)
        {
            throw new DomainException($"该角色下还有 {userCount} 个用户，请先解除关联后再删除");
        }

        _db.Roles.Remove(role);
        await _db.SaveChangesAsync(ct);
    }
}
