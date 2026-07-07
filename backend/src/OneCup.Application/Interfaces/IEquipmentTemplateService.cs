using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

public interface IEquipmentTemplateService
{
    Task<List<EquipmentTemplateListItemDto>> GetListAsync(Guid typeId, Guid? processId, CancellationToken ct = default);

    /// <summary>跨类型分页查询（顶层 Templates 标签页用，可选按类型过滤）。</summary>
    Task<PagedResult<EquipmentTemplateListItemDto>> GetPagedAsync(
        Guid? typeId, string? keyword, Guid? processId, int page, int pageSize, CancellationToken ct = default);

    Task<EquipmentTemplateDto?> GetByIdAsync(Guid typeId, Guid id, CancellationToken ct = default);

    /// <summary>顶层详情查询（按模板 id，不带 typeId）。</summary>
    Task<EquipmentTemplateDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<EquipmentTemplateDto> CreateAsync(Guid typeId, CreateEquipmentTemplateRequest request, CancellationToken ct = default);

    Task<EquipmentTemplateDto> UpdateAsync(Guid typeId, Guid id, UpdateEquipmentTemplateRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid typeId, Guid id, CancellationToken ct = default);
}
