using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

public interface IEquipmentTypeService
{
    Task<PagedResult<EquipmentTypeListItemDto>> GetListAsync(
        string? keyword, string? code, bool? isActive, int page, int pageSize, CancellationToken ct = default);

    Task<EquipmentTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>获取启用类型列表（前端下拉用）。</summary>
    Task<List<EquipmentTypeListItemDto>> GetActiveAsync(CancellationToken ct = default);

    Task<EquipmentTypeDto> CreateAsync(CreateEquipmentTypeRequest request, CancellationToken ct = default);

    Task<EquipmentTypeDto> UpdateAsync(Guid id, UpdateEquipmentTypeRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
