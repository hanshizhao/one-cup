using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 物料管理服务(CRUD + 启停 + 物理删除)。
/// code 创建后不可改;CreateAsync 在事务内经编号引擎取号。
/// </summary>
public interface IMaterialService
{
    Task<PagedResult<MaterialDto>> GetMaterialsAsync(
        int page, int pageSize, string? keyword, string? category, bool? isActive,
        CancellationToken ct = default);

    Task<List<MaterialDto>> GetAllActiveMaterialsAsync(CancellationToken ct = default);

    Task<MaterialDto?> GetMaterialAsync(Guid id, CancellationToken ct = default);

    Task<MaterialDto> CreateMaterialAsync(CreateMaterialRequest request, CancellationToken ct = default);

    Task UpdateMaterialAsync(Guid id, UpdateMaterialRequest request, CancellationToken ct = default);

    Task UpdateMaterialStatusAsync(Guid id, bool isActive, CancellationToken ct = default);

    Task DeleteMaterialAsync(Guid id, CancellationToken ct = default);
}
