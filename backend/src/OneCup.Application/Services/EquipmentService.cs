using FluentValidation;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 设备实例服务。
/// 编号走编号引擎（事务内取号，c02）。软删除。
/// 列表用扁平投影（不含参数/模板，只带类型名）。
/// </summary>
public class EquipmentService : IEquipmentService
{
    private readonly IRepository<Equipment> _equipments;
    private readonly IRepository<EquipmentType> _types;
    private readonly IUnitOfWork _uow;
    private readonly INumberingService _numbering;
    private readonly IValidator<CreateEquipmentRequest> _createValidator;
    private readonly IValidator<UpdateEquipmentRequest> _updateValidator;

    public EquipmentService(
        IRepository<Equipment> equipments,
        IRepository<EquipmentType> types,
        IUnitOfWork uow,
        INumberingService numbering,
        IValidator<CreateEquipmentRequest> createValidator,
        IValidator<UpdateEquipmentRequest> updateValidator)
    {
        _equipments = equipments;
        _types = types;
        _uow = uow;
        _numbering = numbering;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PagedResult<EquipmentListItemDto>> GetListAsync(
        string? keyword, string? code, Guid? typeId, bool? isActive, EquipmentStatus? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        var total = await _equipments.CountAsync(
            new EquipmentFilterSpec(keyword, code, typeId, isActive, status), ct);
        var items = await _equipments.ListAsync(
            new EquipmentPagedSpec(keyword, code, typeId, isActive, status, page, pageSize), ct);

        // 批量查类型名
        var typeIds = items.Select(e => e.EquipmentTypeId).Distinct().ToList();
        var typeNames = await GetTypeNames(typeIds, ct);

        return new PagedResult<EquipmentListItemDto>
        {
            Items = items.Select(e => new EquipmentListItemDto
            {
                Id = e.Id,
                Code = e.Code,
                Name = e.Name,
                EquipmentTypeId = e.EquipmentTypeId,
                EquipmentTypeName = typeNames.GetValueOrDefault(e.EquipmentTypeId, ""),
                Specification = e.Specification,
                Supplier = e.Supplier,
                Location = e.Location,
                Status = e.Status,
                IsActive = e.IsActive,
                CreatedAt = e.CreatedAt,
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<EquipmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _equipments.FirstOrDefaultAsync(new EquipmentByIdSpec(id), ct);
        if (e is null) return null;

        var typeNames = await GetTypeNames(new[] { e.EquipmentTypeId }, ct);

        return new EquipmentDto
        {
            Id = e.Id,
            Code = e.Code,
            Name = e.Name,
            EquipmentTypeId = e.EquipmentTypeId,
            EquipmentTypeName = typeNames.GetValueOrDefault(e.EquipmentTypeId, ""),
            Specification = e.Specification,
            Supplier = e.Supplier,
            Location = e.Location,
            Status = e.Status,
            PurchaseDate = e.PurchaseDate,
            WarrantyExpiry = e.WarrantyExpiry,
            Remark = e.Remark,
            IsActive = e.IsActive,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
        };
    }

    public async Task<EquipmentDto> CreateAsync(CreateEquipmentRequest request, CancellationToken ct = default)
    {
        await _createValidator.EnsureValidAsync(request, ct);

        // 类型存在性校验
        if (!await _types.AnyAsync(new EquipmentTypeByIdSpec(request.EquipmentTypeId), ct))
        {
            throw new DomainException("设备类型不存在");
        }

        // 名称唯一性（绕过软删除）
        if (await _equipments.AnyIgnoringFiltersAsync(new EquipmentByNameSpec(request.Name), ct))
        {
            throw new DomainException($"设备名称「{request.Name}」已存在");
        }

        Guid createdId = Guid.Empty;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            var code = await _numbering.GenerateAsync(NumberTargetTypes.Equipment, request.CategoryCode, ct);
            var equipment = new Equipment
            {
                Code = code,
                Name = request.Name,
                EquipmentTypeId = request.EquipmentTypeId,
                Specification = request.Specification,
                Supplier = request.Supplier,
                Location = request.Location,
                Status = request.Status,
                PurchaseDate = request.PurchaseDate,
                WarrantyExpiry = request.WarrantyExpiry,
                Remark = request.Remark,
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
            };
            await _equipments.AddAsync(equipment, ct);
            await _uow.SaveChangesAsync(ct);
            createdId = equipment.Id;
        }, ct);

        return await GetByIdAsync(createdId, ct) ?? throw new DomainException("设备创建失败");
    }

    public async Task<EquipmentDto> UpdateAsync(Guid id, UpdateEquipmentRequest request, CancellationToken ct = default)
    {
        await _updateValidator.EnsureValidAsync(request, ct);

        var equipment = await _equipments.FirstOrDefaultAsync(new EquipmentByIdSpec(id), ct)
            ?? throw new DomainException("设备不存在");

        // 类型存在性
        if (!await _types.AnyAsync(new EquipmentTypeByIdSpec(request.EquipmentTypeId), ct))
        {
            throw new DomainException("设备类型不存在");
        }

        // 改名查重
        if (await _equipments.AnyIgnoringFiltersAsync(new EquipmentByNameSpec(request.Name, id), ct))
        {
            throw new DomainException($"设备名称「{request.Name}」已存在");
        }

        equipment.Name = request.Name;
        equipment.EquipmentTypeId = request.EquipmentTypeId;
        equipment.Specification = request.Specification;
        equipment.Supplier = request.Supplier;
        equipment.Location = request.Location;
        equipment.Status = request.Status;
        equipment.PurchaseDate = request.PurchaseDate;
        equipment.WarrantyExpiry = request.WarrantyExpiry;
        equipment.Remark = request.Remark;
        equipment.IsActive = request.IsActive;
        equipment.SortOrder = request.SortOrder;

        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct) ?? throw new DomainException("设备更新失败");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // GetByIdAsync 走软删除过滤器，这里用 GetByIdAsync 绕过（参考 Customer 软删除模式）
        var equipment = await _equipments.GetByIdAsync(id, ct)
            ?? throw new DomainException("设备不存在");

        equipment.IsDeleted = true;
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<Guid, string>> GetTypeNames(IEnumerable<Guid> typeIds, CancellationToken ct)
    {
        if (!typeIds.Any()) return new();
        var all = await _types.ListAsync(ct);
        return all.Where(t => typeIds.Contains(t.Id)).ToDictionary(t => t.Id, t => t.Name);
    }
}
