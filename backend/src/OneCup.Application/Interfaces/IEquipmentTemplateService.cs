using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

public interface IEquipmentTemplateService
{
    Task<List<EquipmentTemplateListItemDto>> GetListAsync(Guid typeId, Guid? processId, CancellationToken ct = default);

    Task<EquipmentTemplateDto?> GetByIdAsync(Guid typeId, Guid id, CancellationToken ct = default);

    Task<EquipmentTemplateDto> CreateAsync(Guid typeId, CreateEquipmentTemplateRequest request, CancellationToken ct = default);

    Task<EquipmentTemplateDto> UpdateAsync(Guid typeId, Guid id, UpdateEquipmentTemplateRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid typeId, Guid id, CancellationToken ct = default);
}
