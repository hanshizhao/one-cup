using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 颜色主数据管理服务（CRUD + 启停）。
/// code 创建后不可改；hex 格式校验；只启停不物理删除。
/// </summary>
public interface IColorService
{
    Task<PagedResult<ColorDto>> GetColorsAsync(
        int page, int pageSize, string? keyword, string? colorFamily, bool? isActive,
        CancellationToken ct = default);

    Task<List<ColorDto>> GetAllActiveColorsAsync(CancellationToken ct = default);

    Task<ColorDto?> GetColorAsync(Guid id, CancellationToken ct = default);

    Task<ColorDto> CreateColorAsync(CreateColorRequest request, CancellationToken ct = default);

    Task UpdateColorAsync(Guid id, UpdateColorRequest request, CancellationToken ct = default);

    Task UpdateColorStatusAsync(Guid id, bool isActive, CancellationToken ct = default);
}
