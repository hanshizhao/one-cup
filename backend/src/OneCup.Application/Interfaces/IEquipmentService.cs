using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Domain.Enums;

namespace OneCup.Application.Interfaces;

public interface IEquipmentService
{
    Task<PagedResult<EquipmentListItemDto>> GetListAsync(
        string? keyword, string? code, Guid? typeId, bool? isActive, EquipmentStatus? status,
        int page, int pageSize, CancellationToken ct = default);

    Task<EquipmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<EquipmentDto> CreateAsync(CreateEquipmentRequest request, CancellationToken ct = default);

    Task<EquipmentDto> UpdateAsync(Guid id, UpdateEquipmentRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
