using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

public interface IUserService
{
    Task<PagedResult<UserListItemDto>> GetListAsync(int page, int pageSize, string? keyword, CancellationToken ct = default);

    Task<UserDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<UserDto> CreateAsync(CreateUserRequest request, CancellationToken ct = default);

    Task<UserDto> UpdateAsync(Guid id, UpdateUserRequest request, CancellationToken ct = default);

    Task ResetPasswordAsync(Guid id, ResetPasswordRequest request, CancellationToken ct = default);

    Task UpdateStatusAsync(Guid id, UpdateStatusRequest request, CancellationToken ct = default);
}
