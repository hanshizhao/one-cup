using FluentValidation;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 设备类型服务。
/// 参数定义随类型整表替换（PUT 时按 Id diff）。
/// 类型删除前校验：无设备引用 + 无模板。
/// 类型编号走编号引擎（事务内取号）。
/// </summary>
public class EquipmentTypeService : IEquipmentTypeService
{
    private readonly IRepository<EquipmentType> _types;
    private readonly IRepository<Equipment> _equipments;
    private readonly IRepository<Process> _processes;
    private readonly IUnitOfWork _uow;
    private readonly INumberingService _numbering;
    private readonly IValidator<CreateEquipmentTypeRequest> _createValidator;
    private readonly IValidator<UpdateEquipmentTypeRequest> _updateValidator;

    public EquipmentTypeService(
        IRepository<EquipmentType> types,
        IRepository<Equipment> equipments,
        IRepository<Process> processes,
        IUnitOfWork uow,
        INumberingService numbering,
        IValidator<CreateEquipmentTypeRequest> createValidator,
        IValidator<UpdateEquipmentTypeRequest> updateValidator)
    {
        _types = types;
        _equipments = equipments;
        _processes = processes;
        _uow = uow;
        _numbering = numbering;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PagedResult<EquipmentTypeListItemDto>> GetListAsync(
        string? keyword, string? code, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var total = await _types.CountAsync(new EquipmentTypeFilterSpec(keyword, code, isActive), ct);
        var items = await _types.ListAsync(
            new EquipmentTypePagedSpec(keyword, code, isActive, page, pageSize), ct);

        return new PagedResult<EquipmentTypeListItemDto>
        {
            Items = items.Select(t => new EquipmentTypeListItemDto
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                ParameterCount = t.Parameters.Count,
                TemplateCount = t.Templates.Count,
                SortOrder = t.SortOrder,
                IsActive = t.IsActive,
                CreatedAt = t.CreatedAt,
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<EquipmentTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(id), ct);
        if (t is null) return null;

        // 单位符号批量查（避免 N+1，这里类型参数量不大，逐个查可接受；如需优化可改 IQueryable 投影）
        var processNames = await GetProcessNames(t.Templates.Select(tl => tl.ProcessId).Distinct(), ct);
        return new EquipmentTypeDto
        {
            Id = t.Id,
            Code = t.Code,
            Name = t.Name,
            Remark = t.Remark,
            SortOrder = t.SortOrder,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            ParameterCount = t.Parameters.Count,
            TemplateCount = t.Templates.Count,
            Parameters = t.Parameters.OrderBy(p => p.SortOrder).Select(p => new EquipmentTypeParameterDto
            {
                Id = p.Id,
                Name = p.Name,
                ValueType = p.ValueType,
                UnitId = p.UnitId,
                MinValue = p.MinValue,
                MaxValue = p.MaxValue,
                Precision = p.Precision,
                Options = EquipmentParameterValueValidator.ParseOptions(p.Options),
                Required = p.Required,
                SortOrder = p.SortOrder,
                Remark = p.Remark,
            }).ToList(),
            Templates = t.Templates.OrderBy(tl => tl.SortOrder).Select(tl => new EquipmentTemplateSummaryDto
            {
                Id = tl.Id,
                Name = tl.Name,
                ProcessId = tl.ProcessId,
                ProcessName = processNames.GetValueOrDefault(tl.ProcessId, ""),
                Status = "valid",  // 摘要不带逐值校验，详情才校验
                SortOrder = tl.SortOrder,
            }).ToList(),
        };
    }

    private async Task<Dictionary<Guid, string>> GetProcessNames(IEnumerable<Guid> processIds, CancellationToken ct)
    {
        var ids = processIds.ToList();
        if (ids.Count == 0) return new();
        var processes = await _processes.ListAsync(ct);
        return processes.Where(p => ids.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name);
    }

    public async Task<List<EquipmentTypeListItemDto>> GetActiveAsync(CancellationToken ct = default)
    {
        var items = await _types.ListAsync(new EquipmentTypeActiveSpec(), ct);
        return items.Select(t => new EquipmentTypeListItemDto
        {
            Id = t.Id,
            Code = t.Code,
            Name = t.Name,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
        }).ToList();
    }

    public async Task<EquipmentTypeDto> CreateAsync(CreateEquipmentTypeRequest request, CancellationToken ct = default)
    {
        await _createValidator.EnsureValidAsync(request, ct);

        if (await _types.AnyIgnoringFiltersAsync(new EquipmentTypeByNameSpec(request.Name), ct))
        {
            throw new DomainException($"设备类型名称「{request.Name}」已存在");
        }

        Guid createdId = Guid.Empty;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            var code = await _numbering.GenerateAsync(NumberTargetTypes.EquipmentType, request.CategoryCode, ct);
            var type = new EquipmentType
            {
                Code = code,
                Name = request.Name,
                Remark = request.Remark,
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
                Parameters = request.Parameters.Select(p => new EquipmentTypeParameter
                {
                    Name = p.Name,
                    ValueType = p.ValueType,
                    UnitId = p.UnitId,
                    MinValue = p.MinValue,
                    MaxValue = p.MaxValue,
                    Precision = p.Precision,
                    Options = EquipmentParameterValueValidator.SerializeOptions(p.Options),
                    Required = p.Required,
                    SortOrder = p.SortOrder,
                    Remark = p.Remark,
                }).ToList(),
            };
            await _types.AddAsync(type, ct);
            await _uow.SaveChangesAsync(ct);
            createdId = type.Id;
        }, ct);

        return await GetByIdAsync(createdId, ct) ?? throw new DomainException("设备类型创建失败");
    }

    public async Task<EquipmentTypeDto> UpdateAsync(Guid id, UpdateEquipmentTypeRequest request, CancellationToken ct = default)
    {
        await _updateValidator.EnsureValidAsync(request, ct);

        // GetByIdSpec 走 Include，加载 Parameters 子集合
        var type = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(id), ct)
            ?? throw new DomainException("设备类型不存在");

        if (await _types.AnyIgnoringFiltersAsync(new EquipmentTypeByNameSpec(request.Name, id), ct))
        {
            throw new DomainException($"设备类型名称「{request.Name}」已存在");
        }

        // 基础字段
        type.Name = request.Name;
        type.Remark = request.Remark;
        type.IsActive = request.IsActive;
        type.SortOrder = request.SortOrder;

        // 参数定义整表替换 diff
        SyncParameters(type, request.Parameters);

        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct) ?? throw new DomainException("设备类型更新失败");
    }

    /// <summary>
    /// 参数定义整表替换 diff：
    /// - request 中 Id=null → 新增
    /// - request 中 Id 有值且 type 中存在 → 更新
    /// - type 中存在但 request 未出现 → 删除
    /// </summary>
    private static void SyncParameters(EquipmentType type, List<ParameterDefinitionDto> requestParams)
    {
        var requestIds = requestParams.Where(p => p.Id.HasValue).Select(p => p.Id!.Value).ToHashSet();

        // 删除：type 中有但 request 未包含的
        var toRemove = type.Parameters.Where(p => !requestIds.Contains(p.Id)).ToList();
        foreach (var p in toRemove)
        {
            type.Parameters.Remove(p);
        }

        // 更新或新增
        foreach (var dto in requestParams)
        {
            if (dto.Id.HasValue)
            {
                var existing = type.Parameters.FirstOrDefault(p => p.Id == dto.Id.Value);
                if (existing is not null)
                {
                    existing.Name = dto.Name;
                    existing.ValueType = dto.ValueType;
                    existing.UnitId = dto.UnitId;
                    existing.MinValue = dto.MinValue;
                    existing.MaxValue = dto.MaxValue;
                    existing.Precision = dto.Precision;
                    existing.Options = EquipmentParameterValueValidator.SerializeOptions(dto.Options);
                    existing.Required = dto.Required;
                    existing.SortOrder = dto.SortOrder;
                    existing.Remark = dto.Remark;
                }
            }
            else
            {
                type.Parameters.Add(new EquipmentTypeParameter
                {
                    // Id 留 Guid.Empty，让 EF Core 值生成器分配。
                    // BaseEntity 的属性初始化器会预填 Guid.NewGuid()，
                    // 在父实体为 Unchanged 时（Update 场景）会把新子实体误判为 Modified 而非 Added，
                    // 导致 InMemory 存储删除查找失败（DbUpdateConcurrencyException）。
                    Id = Guid.Empty,
                    Name = dto.Name,
                    ValueType = dto.ValueType,
                    UnitId = dto.UnitId,
                    MinValue = dto.MinValue,
                    MaxValue = dto.MaxValue,
                    Precision = dto.Precision,
                    Options = EquipmentParameterValueValidator.SerializeOptions(dto.Options),
                    Required = dto.Required,
                    SortOrder = dto.SortOrder,
                    Remark = dto.Remark,
                });
            }
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var type = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(id), ct)
            ?? throw new DomainException("设备类型不存在");

        // 校验：无设备引用
        if (await _equipments.AnyAsync(new EquipmentByTypeSpec(id), ct))
        {
            throw new DomainException("该设备类型下还有设备，无法删除");
        }

        // 校验：无模板
        if (type.Templates.Count > 0)
        {
            throw new DomainException("该设备类型下还有运行模板，无法删除");
        }

        _types.Remove(type);
        await _uow.SaveChangesAsync(ct);
    }
}
