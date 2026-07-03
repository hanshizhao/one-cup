using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 编号字典管理服务（业务类型 + 分类的 CRUD）。
/// </summary>
public interface INumberingDictionaryService
{
    // ── 业务类型 ──
    Task<PagedResult<TargetTypeDto>> GetTargetTypesAsync(
        int page, int pageSize, string? keyword, bool? isActive, CancellationToken ct = default);

    Task<List<TargetTypeDto>> GetAllActiveTargetTypesAsync(CancellationToken ct = default);

    Task<TargetTypeDto?> GetTargetTypeAsync(Guid id, CancellationToken ct = default);

    Task<TargetTypeDto> CreateTargetTypeAsync(CreateTargetTypeRequest request, CancellationToken ct = default);

    Task UpdateTargetTypeAsync(Guid id, UpdateTargetTypeRequest request, CancellationToken ct = default);

    Task UpdateTargetTypeStatusAsync(Guid id, bool isActive, CancellationToken ct = default);

    // ── 分类 ──
    Task<PagedResult<CategoryDto>> GetCategoriesAsync(
        int page, int pageSize, string? targetTypeCode, string? keyword, bool? isActive, CancellationToken ct = default);

    Task<List<CategoryDto>> GetActiveCategoriesAsync(string targetTypeCode, CancellationToken ct = default);

    Task<CategoryDto?> GetCategoryAsync(Guid id, CancellationToken ct = default);

    Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct = default);

    Task UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default);

    Task UpdateCategoryStatusAsync(Guid id, bool isActive, CancellationToken ct = default);
}
