using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 计量单位管理服务（CRUD + 同类换算）。
/// </summary>
public interface IMeasurementUnitService
{
    Task<PagedResult<UnitDto>> GetListAsync(
        int page, int pageSize, string? keyword, string? category, bool? isActive,
        CancellationToken ct = default);

    Task<List<UnitDto>> GetAllActiveAsync(CancellationToken ct = default);

    Task<List<string>> GetCategoriesAsync(CancellationToken ct = default);

    Task<UnitDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<UnitDto> CreateAsync(CreateUnitRequest request, CancellationToken ct = default);

    Task UpdateAsync(Guid id, UpdateUnitRequest request, CancellationToken ct = default);

    Task UpdateStatusAsync(Guid id, bool isActive, CancellationToken ct = default);

    Task<ConvertUnitResult> ConvertAsync(ConvertUnitRequest request, CancellationToken ct = default);
}
