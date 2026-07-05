using FluentValidation;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 物料管理服务实现。通过 IRepository + Specification 访问数据,IUnitOfWork 提交。
/// CreateAsync 在事务内经编号引擎取号(并发安全、不跳号);code 创建后不可改;
/// 支持启停 + 物理删除(物料有 material:delete 权限)。
/// </summary>
public class MaterialService : IMaterialService
{
    private readonly IRepository<Material> _materials;
    private readonly IUnitOfWork _uow;
    private readonly INumberingService _numbering;
    private readonly IValidator<CreateMaterialRequest> _createValidator;
    private readonly IValidator<UpdateMaterialRequest> _updateValidator;

    public MaterialService(
        IRepository<Material> materials,
        IUnitOfWork uow,
        INumberingService numbering,
        IValidator<CreateMaterialRequest> createValidator,
        IValidator<UpdateMaterialRequest> updateValidator)
    {
        _materials = materials;
        _uow = uow;
        _numbering = numbering;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PagedResult<MaterialDto>> GetMaterialsAsync(
        int page, int pageSize, string? keyword, string? category, bool? isActive,
        CancellationToken ct = default)
    {
        // 关键:总数用仅含过滤条件的 FilterSpec 统计,绝不能用带分页的 PagedSpec,
        // 否则 Repository.CountAsync 会应用 Skip/Take,只统计当前页子集。
        var total = await _materials.CountAsync(
            new MaterialFilterSpec(keyword, category, isActive), ct);
        var materials = await _materials.ListAsync(
            new MaterialPagedSpec(keyword, category, isActive, page, pageSize), ct);

        return new PagedResult<MaterialDto>
        {
            Items = materials.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<List<MaterialDto>> GetAllActiveMaterialsAsync(CancellationToken ct = default)
    {
        var materials = await _materials.ListAsync(new MaterialActiveSpec(), ct);
        return materials.Select(ToDto).ToList();
    }

    public async Task<MaterialDto?> GetMaterialAsync(Guid id, CancellationToken ct = default)
    {
        var m = await _materials.FirstOrDefaultAsync(new MaterialByIdSpec(id), ct);
        return m is null ? null : ToDto(m);
    }

    public async Task<MaterialDto> CreateMaterialAsync(CreateMaterialRequest request, CancellationToken ct = default)
    {
        await _createValidator.EnsureValidAsync(request, ct);
        Guid createdId = Guid.Empty;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            // 事务内经编号引擎取号(行锁),计数器增量与物料记录一起提交(不跳号)
            var code = await _numbering.GenerateAsync(NumberTargetTypes.Material, request.CategoryCode, ct);
            var entity = new Material
            {
                Code = code,
                Name = request.Name,
                Spec = request.Spec,
                Category = request.Category,
                UnitId = request.UnitId,
                Remark = request.Remark,
                SortOrder = request.SortOrder,
                IsActive = true,
            };
            await _materials.AddAsync(entity, ct);
            await _uow.SaveChangesAsync(ct);
            createdId = entity.Id;
        }, ct);

        return await GetMaterialAsync(createdId, ct) ?? throw new DomainException("物料创建失败");
    }

    public async Task UpdateMaterialAsync(Guid id, UpdateMaterialRequest request, CancellationToken ct = default)
    {
        await _updateValidator.EnsureValidAsync(request, ct);
        // 整表覆盖式 PUT(对齐 CustomerService.UpdateAsync),不做 null-skip:
        // 否则用户在前端清空可选字段(如计量单位/备注)时,提交 null 会被当成"不修改",
        // 导致字段无法清空。UpdateDto 字段可空(string?/Guid?/int?)正是为了让 null 合法穿透到赋值。
        // code 不可改:更新接口不暴露 Code 字段。
        var entity = await _materials.FirstOrDefaultAsync(new MaterialByIdSpec(id), ct)
            ?? throw new DomainException("物料不存在");

        entity.Name = request.Name ?? entity.Name;          // 必填字段:null 时保持原值(防御性)
        entity.Spec = request.Spec ?? entity.Spec;
        entity.Category = request.Category ?? entity.Category;
        entity.UnitId = request.UnitId;                      // 可空字段:直接赋值,允许清空
        entity.Remark = request.Remark;                      // 可空字段:直接赋值,允许清空
        entity.SortOrder = request.SortOrder ?? entity.SortOrder;

        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdateMaterialStatusAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var entity = await _materials.FirstOrDefaultAsync(new MaterialByIdSpec(id), ct)
            ?? throw new DomainException("物料不存在");
        entity.IsActive = isActive;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteMaterialAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _materials.FirstOrDefaultAsync(new MaterialByIdSpec(id), ct)
            ?? throw new DomainException("物料不存在");
        _materials.Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    // ── 内部工具 ──

    private static MaterialDto ToDto(Material m) => new()
    {
        Id = m.Id,
        Code = m.Code,
        Name = m.Name,
        Spec = m.Spec,
        Category = m.Category,
        UnitId = m.UnitId,
        Remark = m.Remark,
        SortOrder = m.SortOrder,
        IsActive = m.IsActive,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
    };
}
