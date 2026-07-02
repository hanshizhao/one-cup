using Microsoft.EntityFrameworkCore;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;

namespace OneCup.Infrastructure.Services;

/// <summary>
/// 用户管理服务实现。
/// </summary>
public class UserService : IUserService
{
    private readonly OneCupDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public UserService(OneCupDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    public async Task<PagedResult<UserListItemDto>> GetListAsync(int page, int pageSize, string? keyword, CancellationToken ct = default)
    {
        var query = _db.Users
            .Include(u => u.Roles)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim();
            query = query.Where(u => u.Username.Contains(keyword) || u.DisplayName.Contains(keyword));
        }

        var total = await query.CountAsync(ct);
        var users = await query
            .OrderByDescending(u => u.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<UserListItemDto>
        {
            Items = users.Select(u => new UserListItemDto
            {
                Id = u.Id,
                Username = u.Username,
                DisplayName = u.DisplayName,
                Email = u.Email,
                IsActive = u.IsActive,
                CreatedAt = u.CreatedAt,
                RoleNames = u.Roles.Select(r => r.Name).ToList(),
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == id, ct);

        if (user is null) return null;

        return new UserDto
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Email = user.Email,
            IsActive = user.IsActive,
            CreatedAt = user.CreatedAt,
            RoleIds = user.Roles.Select(r => r.Id).ToList(),
            RoleNames = user.Roles.Select(r => r.Name).ToList(),
        };
    }

    public async Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default)
    {
        // 用户名唯一校验
        if (await _db.Users.AnyAsync(u => u.Username == request.Username, ct))
        {
            throw new DomainException($"用户名「{request.Username}」已存在");
        }

        var roles = await _db.Roles
            .Where(r => request.RoleIds.Contains(r.Id))
            .ToListAsync(ct);

        var user = new User
        {
            Username = request.Username,
            DisplayName = request.DisplayName,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            IsActive = true,
            Roles = roles,
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(user.Id, ct) ?? throw new DomainException("用户创建失败");
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users
            .Include(u => u.Roles)
            .FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new DomainException("用户不存在");

        // admin 保护：不能禁用 admin 用户
        if (!request.IsActive && user.Id == SystemConstants.AdminUserId)
        {
            throw new DomainException("不能禁用系统管理员账号");
        }

        // admin 保护：如果用户当前有 admin 角色，保留它（不能被移除）
        var hasAdminRole = user.Roles.Any(r => r.Code == SystemConstants.AdminRoleCode);

        var roles = await _db.Roles
            .Where(r => request.RoleIds.Contains(r.Id))
            .ToListAsync(ct);

        // admin 保护：如果用户原来是 admin，确保 admin 角色仍在列表中
        if (hasAdminRole)
        {
            var adminRole = await _db.Roles.FirstAsync(r => r.Code == SystemConstants.AdminRoleCode, ct);
            if (!roles.Any(r => r.Code == SystemConstants.AdminRoleCode))
            {
                roles.Add(adminRole);
            }
        }

        user.DisplayName = request.DisplayName;
        user.Email = request.Email;
        user.IsActive = request.IsActive;
        user.Roles = roles;

        await _db.SaveChangesAsync(ct);

        return await GetByIdAsync(user.Id, ct) ?? throw new DomainException("用户更新失败");
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new DomainException("用户不存在");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateStatusAsync(Guid id, UpdateStatusRequest request, CancellationToken ct = default)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.Id == id, ct)
            ?? throw new DomainException("用户不存在");

        // admin 保护：不能禁用 admin 用户
        if (!request.IsActive && user.Id == SystemConstants.AdminUserId)
        {
            throw new DomainException("不能禁用系统管理员账号");
        }

        user.IsActive = request.IsActive;
        await _db.SaveChangesAsync(ct);
    }
}
