using FluentValidation;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 用户管理服务实现。
/// 通过 IRepository + Specification 访问数据,不直接依赖 EF Core;
/// 写入操作通过 IUnitOfWork 提交事务。
/// </summary>
public class UserService : IUserService
{
    private readonly IRepository<User> _users;
    private readonly IRepository<Role> _roles;
    private readonly IRepository<RefreshToken> _refreshTokens;
    private readonly IUnitOfWork _uow;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IValidator<CreateUserRequest> _createValidator;
    private readonly IValidator<UpdateUserRequest> _updateValidator;
    private readonly IValidator<ResetPasswordRequest> _resetValidator;

    public UserService(
        IRepository<User> users,
        IRepository<Role> roles,
        IRepository<RefreshToken> refreshTokens,
        IUnitOfWork uow,
        IPasswordHasher passwordHasher,
        IValidator<CreateUserRequest> createValidator,
        IValidator<UpdateUserRequest> updateValidator,
        IValidator<ResetPasswordRequest> resetValidator)
    {
        _users = users;
        _roles = roles;
        _refreshTokens = refreshTokens;
        _uow = uow;
        _passwordHasher = passwordHasher;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
        _resetValidator = resetValidator;
    }

    public async Task<PagedResult<UserListItemDto>> GetListAsync(int page, int pageSize, string? keyword, CancellationToken ct = default)
    {
        // 关键:总数用仅含过滤条件的 UserFilterSpec 统计,绝不能用带分页的 UserPagedSpec,
        // 否则 Repository.CountAsync 会应用 Skip/Take,只统计当前页子集。
        var total = await _users.CountAsync(new UserFilterSpec(keyword), ct);

        var users = await _users.ListAsync(new UserPagedSpec(keyword, page, pageSize), ct);

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
        var user = await _users.FirstOrDefaultAsync(new UserByIdWithRolesSpec(id), ct);

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
        await _createValidator.EnsureValidAsync(request, ct);

        // 用户名唯一校验
        if (await _users.AnyAsync(new UserByUsernameSpec(request.Username), ct))
        {
            throw new DomainException($"用户名「{request.Username}」已存在");
        }

        // 按角色 Id 逐个加载:Role 无 Include 需求,GetByIdAsync(FindAsync) 返回
        // 变更跟踪器已跟踪的实体,后续 AddAsync(user) 复用同一实例,避免重复跟踪冲突。
        var roles = new List<Role>();
        foreach (var roleId in request.RoleIds)
        {
            var role = await _roles.GetByIdAsync(roleId, ct);
            if (role is not null) roles.Add(role);
        }

        var user = new User
        {
            Username = request.Username,
            DisplayName = request.DisplayName,
            Email = request.Email,
            PasswordHash = _passwordHasher.Hash(request.Password),
            IsActive = true,
            Roles = roles,
        };

        await _users.AddAsync(user, ct);
        await _uow.SaveChangesAsync(ct);

        return await GetByIdAsync(user.Id, ct) ?? throw new DomainException("用户创建失败");
    }

    public async Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default)
    {
        await _updateValidator.EnsureValidAsync(request, ct);

        var user = await _users.FirstOrDefaultAsync(new UserByIdWithRolesSpec(id), ct)
            ?? throw new DomainException("用户不存在");

        // admin 保护：不能禁用 admin 用户
        if (!request.IsActive && user.Id == SystemConstants.AdminUserId)
        {
            throw new DomainException("不能禁用系统管理员账号");
        }

        // admin 保护：如果用户当前有 admin 角色，保留它（不能被移除）
        var hasAdminRole = user.Roles.Any(r => r.Code == SystemConstants.AdminRoleCode);

        // 按角色 Id 逐个加载:GetByIdAsync(FindAsync) 返回变更跟踪器已跟踪的实例,
        // 复用同一实例避免重复跟踪冲突(ListAsync 走 AsNoTracking 会带来 detached 实体)。
        var roles = new List<Role>();
        foreach (var roleId in request.RoleIds)
        {
            var role = await _roles.GetByIdAsync(roleId, ct);
            if (role is not null) roles.Add(role);
        }

        // admin 保护：如果用户原来是 admin，确保 admin 角色仍在列表中
        if (hasAdminRole)
        {
            var adminRole = await _roles.GetByIdAsync(SystemConstants.AdminRoleId, ct)
                ?? throw new DomainException("管理员角色不存在");
            if (!roles.Any(r => r.Code == SystemConstants.AdminRoleCode))
            {
                roles.Add(adminRole);
            }
        }

        user.DisplayName = request.DisplayName;
        user.Email = request.Email;
        user.IsActive = request.IsActive;
        user.Roles = roles;

        await _uow.SaveChangesAsync(ct);

        return await GetByIdAsync(user.Id, ct) ?? throw new DomainException("用户更新失败");
    }

    public async Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct = default)
    {
        await _resetValidator.EnsureValidAsync(request, ct);

        var user = await _users.GetByIdAsync(id, ct)
            ?? throw new DomainException("用户不存在");

        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);
        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdateStatusAsync(Guid id, UpdateStatusRequest request, CancellationToken ct = default)
    {
        var user = await _users.GetByIdAsync(id, ct)
            ?? throw new DomainException("用户不存在");

        // admin 保护：不能禁用 admin 用户
        if (!request.IsActive && user.Id == SystemConstants.AdminUserId)
        {
            throw new DomainException("不能禁用系统管理员账号");
        }

        user.IsActive = request.IsActive;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // GetByIdAsync 走 FindAsync(tracked,不受 QueryFilter 影响)。
        // 注意:已软删除的用户也会被找到(幂等重删 → 重复设置 IsDeleted=true,返回 204)。
        var user = await _users.GetByIdAsync(id, ct)
            ?? throw new DomainException("用户不存在");

        // admin 保护：不能删除系统内置管理员账号
        if (user.Id == SystemConstants.AdminUserId)
        {
            throw new DomainException("不能删除系统管理员账号");
        }

        user.IsDeleted = true;

        // 同步吊销该用户所有未吊销的 refresh token(ActiveRefreshTokensByUserSpec 已在 AuthSpecs 定义)。
        // ListAsync 走 AsNoTracking 返回 detached 实体,修改后需逐个 Update 重新 Attach 为 Modified,
        // 随 SaveChanges 持久化(与 AuthService.LogoutAsync 一致的处理方式)。
        var activeTokens = await _refreshTokens.ListAsync(new ActiveRefreshTokensByUserSpec(id), ct);
        foreach (var token in activeTokens)
        {
            token.IsRevoked = true;
            _refreshTokens.Update(token);
        }

        await _uow.SaveChangesAsync(ct);
    }
}
