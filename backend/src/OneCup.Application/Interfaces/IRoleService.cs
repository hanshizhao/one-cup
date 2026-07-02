using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

public interface IRoleService
{
    Task<List<RoleListItemDto>> GetListAsync(CancellationToken ct = default);

    Task<RoleDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<RoleDto> CreateAsync(CreateRoleRequest request, CancellationToken ct = default);

    Task<RoleDto> UpdateAsync(Guid id, UpdateRoleRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
