using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

public interface IProcessService
{
    Task<PagedResult<ProcessListItemDto>> GetListAsync(
        string? keyword, string? category, bool? isActive, int page, int pageSize, CancellationToken ct = default);

    Task<ProcessDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<ProcessDto> CreateAsync(CreateProcessRequest request, CancellationToken ct = default);

    Task<ProcessDto> UpdateAsync(Guid id, UpdateProcessRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
