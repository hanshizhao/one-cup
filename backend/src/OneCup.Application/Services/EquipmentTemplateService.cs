using FluentValidation;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 运行模板服务。
/// 创建/更新时强校验所有参数值（按 ValueType 分支）。
/// 读取时对每个值按当前参数定义实时校验，返回 status（valid/invalid/orphan）。
/// (TypeId, ProcessId, Name) 唯一。
/// </summary>
public class EquipmentTemplateService : IEquipmentTemplateService
{
    private readonly IRepository<EquipmentTemplate> _templates;
    private readonly IRepository<EquipmentType> _types;
    private readonly IRepository<Process> _processes;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<CreateEquipmentTemplateRequest> _createValidator;
    private readonly IValidator<UpdateEquipmentTemplateRequest> _updateValidator;

    public EquipmentTemplateService(
        IRepository<EquipmentTemplate> templates,
        IRepository<EquipmentType> types,
        IRepository<Process> processes,
        IUnitOfWork uow,
        IValidator<CreateEquipmentTemplateRequest> createValidator,
        IValidator<UpdateEquipmentTemplateRequest> updateValidator)
    {
        _templates = templates;
        _types = types;
        _processes = processes;
        _uow = uow;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<List<EquipmentTemplateListItemDto>> GetListAsync(Guid typeId, Guid? processId, CancellationToken ct = default)
    {
        var type = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(typeId), ct)
            ?? throw new DomainException("设备类型不存在");

        var templates = type.Templates.AsEnumerable();
        if (processId.HasValue)
            templates = templates.Where(t => t.ProcessId == processId.Value);

        var processNames = await GetProcessNames(templates.Select(t => t.ProcessId).Distinct(), ct);

        return templates.OrderBy(t => t.SortOrder).Select(t =>
        {
            var values = t.Values;
            var paramsById = type.Parameters.ToDictionary(p => p.Id);
            var worst = WorstStatus(values, paramsById);
            return new EquipmentTemplateListItemDto
            {
                Id = t.Id,
                Name = t.Name,
                ProcessId = t.ProcessId,
                ProcessName = processNames.GetValueOrDefault(t.ProcessId, ""),
                Status = worst.Status,
                StatusMessage = worst.Message,
                SortOrder = t.SortOrder,
                CreatedAt = t.CreatedAt,
            };
        }).ToList();
    }

    public async Task<EquipmentTemplateDto?> GetByIdAsync(Guid typeId, Guid id, CancellationToken ct = default)
    {
        var type = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(typeId), ct);
        if (type is null) return null;

        var template = type.Templates.FirstOrDefault(t => t.Id == id);
        if (template is null) return null;

        var paramsById = type.Parameters.ToDictionary(p => p.Id);
        var processNames = await GetProcessNames(new[] { template.ProcessId }, ct);

        return new EquipmentTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            ProcessId = template.ProcessId,
            ProcessName = processNames.GetValueOrDefault(template.ProcessId, ""),
            Remark = template.Remark,
            SortOrder = template.SortOrder,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt,
            Status = "valid",
            Values = template.Values.Select(v =>
            {
                paramsById.TryGetValue(v.ParameterId, out var param);
                var (status, msg) = EquipmentParameterValueValidator.EvaluateStatus(param, v.Value);
                return new EquipmentTemplateValueDto
                {
                    ParameterId = v.ParameterId,
                    ParameterName = param?.Name ?? "(已删除)",
                    ValueType = param?.ValueType ?? Domain.Enums.ParameterValueType.Text,
                    Value = v.Value,
                    Status = status,
                    StatusMessage = msg,
                };
            }).ToList(),
        };
    }

    public async Task<EquipmentTemplateDto> CreateAsync(Guid typeId, CreateEquipmentTemplateRequest request, CancellationToken ct = default)
    {
        await _createValidator.EnsureValidAsync(request, ct);

        var type = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(typeId), ct)
            ?? throw new DomainException("设备类型不存在");

        // 唯一性：(TypeId, ProcessId, Name)
        if (type.Templates.Any(t => t.ProcessId == request.ProcessId && t.Name == request.Name))
        {
            throw new DomainException($"工序下模板名「{request.Name}」已存在");
        }

        var paramsById = type.Parameters.ToDictionary(p => p.Id);

        // 强校验每个值
        foreach (var v in request.Values)
        {
            if (!paramsById.TryGetValue(v.ParameterId, out var param))
            {
                throw new DomainException($"参数 {v.ParameterId} 不属于此设备类型");
            }
            EquipmentParameterValueValidator.ValidateValue(param, v.Value);
        }

        var template = new EquipmentTemplate
        {
            // Id 留 Guid.Empty，让 EF Core 值生成器分配（同 EquipmentTypeService.SyncParameters）。
            // 父实体 type 在此为 Unchanged，BaseEntity 预填的 Guid 会让新模板被误判为 Modified。
            Id = Guid.Empty,
            EquipmentTypeId = typeId,
            ProcessId = request.ProcessId,
            Name = request.Name,
            Remark = request.Remark,
            SortOrder = request.SortOrder,
            Values = request.Values.Select(v => new EquipmentTemplateValue
            {
                Id = Guid.Empty,
                ParameterId = v.ParameterId,
                Value = v.Value,
            }).ToList(),
        };

        type.Templates.Add(template);
        await _uow.SaveChangesAsync(ct);

        return await GetByIdAsync(typeId, template.Id, ct) ?? throw new DomainException("模板创建失败");
    }

    public async Task<EquipmentTemplateDto> UpdateAsync(Guid typeId, Guid id, UpdateEquipmentTemplateRequest request, CancellationToken ct = default)
    {
        await _updateValidator.EnsureValidAsync(request, ct);

        var type = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(typeId), ct)
            ?? throw new DomainException("设备类型不存在");

        var template = type.Templates.FirstOrDefault(t => t.Id == id)
            ?? throw new DomainException("模板不存在");

        // 唯一性（排除自身）
        if (type.Templates.Any(t => t.Id != id && t.ProcessId == request.ProcessId && t.Name == request.Name))
        {
            throw new DomainException($"工序下模板名「{request.Name}」已存在");
        }

        var paramsById = type.Parameters.ToDictionary(p => p.Id);

        // 强校验每个值
        foreach (var v in request.Values)
        {
            if (!paramsById.TryGetValue(v.ParameterId, out var param))
            {
                throw new DomainException($"参数 {v.ParameterId} 不属于此设备类型");
            }
            EquipmentParameterValueValidator.ValidateValue(param, v.Value);
        }

        // 更新基础字段
        template.Name = request.Name;
        template.ProcessId = request.ProcessId;
        template.Remark = request.Remark;
        template.SortOrder = request.SortOrder;

        // 值整表替换
        template.Values.Clear();
        foreach (var v in request.Values)
        {
            // Id = Guid.Empty，避免 Update 场景下新值实体被误判为 Modified（同 EquipmentTypeService.SyncParameters）。
            template.Values.Add(new EquipmentTemplateValue
            {
                Id = Guid.Empty,
                ParameterId = v.ParameterId,
                Value = v.Value,
            });
        }

        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(typeId, id, ct) ?? throw new DomainException("模板更新失败");
    }

    public async Task DeleteAsync(Guid typeId, Guid id, CancellationToken ct = default)
    {
        var type = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(typeId), ct)
            ?? throw new DomainException("设备类型不存在");

        var template = type.Templates.FirstOrDefault(t => t.Id == id)
            ?? throw new DomainException("模板不存在");

        type.Templates.Remove(template);
        await _uow.SaveChangesAsync(ct);
    }

    // ── 辅助方法 ──

    private async Task<Dictionary<Guid, string>> GetProcessNames(IEnumerable<Guid> processIds, CancellationToken ct)
    {
        if (!processIds.Any()) return new();
        var processes = await _processes.ListAsync(ct);
        return processes.Where(p => processIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name);
    }

    private static (string Status, string? Message) WorstStatus(List<EquipmentTemplateValue> values, Dictionary<Guid, EquipmentTypeParameter> paramsById)
    {
        string? worst = null;
        string? msg = null;
        var priority = new Dictionary<string, int> { ["orphan"] = 2, ["invalid"] = 1, ["valid"] = 0 };
        foreach (var v in values)
        {
            paramsById.TryGetValue(v.ParameterId, out var param);
            var (s, m) = EquipmentParameterValueValidator.EvaluateStatus(param, v.Value);
            if (worst is null || priority[s] > priority[worst])
            {
                worst = s;
                msg = m;
            }
        }
        return (worst ?? "valid", msg);
    }
}
