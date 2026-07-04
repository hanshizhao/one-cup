using FluentValidation;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 计量单位管理服务实现。
/// code 创建后不可改；基准单位每 category 唯一；换算走基准中转。
/// </summary>
public class MeasurementUnitService : IMeasurementUnitService
{
    private readonly IRepository<MeasurementUnit> _units;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<CreateUnitRequest> _createValidator;

    public MeasurementUnitService(
        IRepository<MeasurementUnit> units,
        IUnitOfWork uow,
        IValidator<CreateUnitRequest> createValidator)
    {
        _units = units;
        _uow = uow;
        _createValidator = createValidator;
    }

    public async Task<PagedResult<UnitDto>> GetListAsync(
        int page, int pageSize, string? keyword, string? category, bool? isActive,
        CancellationToken ct = default)
    {
        var total = await _units.CountAsync(new UnitFilterSpec(keyword, category, isActive), ct);
        var units = await _units.ListAsync(
            new UnitPagedSpec(keyword, category, isActive, page, pageSize), ct);

        return new PagedResult<UnitDto>
        {
            Items = units.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<List<UnitDto>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var units = await _units.ListAsync(new UnitActiveSpec(), ct);
        return units.Select(ToDto).ToList();
    }

    public async Task<List<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        // 取所有启用单位的 category 去重
        var units = await _units.ListAsync(new UnitActiveSpec(), ct);
        return units.Select(u => u.Category).Distinct().OrderBy(c => c).ToList();
    }

    public async Task<UnitDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var u = await _units.FirstOrDefaultAsync(new UnitByIdSpec(id), ct);
        return u is null ? null : ToDto(u);
    }

    public async Task<UnitDto> CreateAsync(CreateUnitRequest request, CancellationToken ct = default)
    {
        await _createValidator.EnsureValidAsync(request, ct);

        // code 唯一性
        if (await _units.AnyAsync(new UnitByCodeSpec(request.Code), ct))
            throw new DomainException($"单位 code '{request.Code}' 已存在");

        // 基准处理
        if (request.IsBase)
        {
            if (await _units.AnyAsync(new UnitBaseByCategorySpec(request.Category), ct))
                throw new DomainException($"类别 '{request.Category}' 已有基准单位");
        }

        var entity = new MeasurementUnit
        {
            Code = request.Code,
            NameZh = request.NameZh,
            NameEn = request.NameEn,
            Symbol = request.Symbol,
            Category = request.Category,
            IsBase = request.IsBase,
            Factor = request.IsBase ? 1m : request.Factor,  // 基准强制 1
            Precision = request.Precision,
            SortOrder = request.SortOrder,
            IsActive = true,
        };
        await _units.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task UpdateAsync(Guid id, UpdateUnitRequest request, CancellationToken ct = default)
    {
        var entity = await _units.FirstOrDefaultAsync(new UnitByIdSpec(id), ct)
            ?? throw new DomainException("单位不存在");

        // factor：基准不可改
        if (request.Factor is not null)
        {
            if (entity.IsBase)
                throw new DomainException("基准单位的换算系数固定为 1，不可修改（请先取消其基准身份）");
            entity.Factor = request.Factor.Value;
        }

        // isBase 切换
        if (request.IsBase is not null && request.IsBase.Value != entity.IsBase)
        {
            if (request.IsBase.Value)
            {
                // false→true：指定为新基准。先降级旧基准（若有），再升当前。
                // 注意：先取旧基准对象，然后降级它，再升当前——两步在同一 SaveChanges 内。
                var existingBase = await _units.FirstOrDefaultAsync(
                    new UnitBaseByCategorySpec(entity.Category, excludingId: id), ct);
                if (existingBase is not null)
                    existingBase.IsBase = false;

                entity.IsBase = true;
                entity.Factor = 1m;
            }
            else
            {
                // true→false：取消基准，检查是否还有其他基准
                var hasOtherBase = await _units.AnyAsync(
                    new UnitBaseByCategorySpec(entity.Category, excludingId: id), ct);
                if (!hasOtherBase)
                    throw new DomainException("每个类别必须保留一个基准单位");

                entity.IsBase = false;
            }
        }

        if (request.NameZh is not null) entity.NameZh = request.NameZh;
        if (request.NameEn is not null) entity.NameEn = request.NameEn;
        if (request.Symbol is not null) entity.Symbol = request.Symbol;
        if (request.Precision is not null) entity.Precision = request.Precision.Value;
        if (request.SortOrder is not null) entity.SortOrder = request.SortOrder.Value;

        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdateStatusAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var entity = await _units.FirstOrDefaultAsync(new UnitByIdSpec(id), ct)
            ?? throw new DomainException("单位不存在");

        if (!isActive && entity.IsBase)
            throw new DomainException("不能停用基准单位，请先将其他单位设为基准");

        entity.IsActive = isActive;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<ConvertUnitResult> ConvertAsync(ConvertUnitRequest request, CancellationToken ct = default)
    {
        // UnitByCodeSpec 不过滤 IsActive，以便区分"不存在"与"已停用"
        var from = await _units.FirstOrDefaultAsync(new UnitByCodeSpec(request.FromCode), ct)
            ?? throw new DomainException($"单位 '{request.FromCode}' 不存在");
        if (!from.IsActive)
            throw new DomainException($"单位 '{request.FromCode}' 已停用");

        var to = await _units.FirstOrDefaultAsync(new UnitByCodeSpec(request.ToCode), ct)
            ?? throw new DomainException($"单位 '{request.ToCode}' 不存在");
        if (!to.IsActive)
            throw new DomainException($"单位 '{request.ToCode}' 已停用");

        if (from.Category != to.Category)
            throw new DomainException(
                $"单位 '{from.Code}'({from.Category}) 与 '{to.Code}'({to.Category}) 类别不同，无法换算");

        var result = request.Quantity * from.Factor / to.Factor;
        result = Math.Round(result, to.Precision);

        return new ConvertUnitResult
        {
            Quantity = result,
            FromCode = from.Code,
            ToCode = to.Code,
            Precision = to.Precision,
        };
    }

    private static UnitDto ToDto(MeasurementUnit u) => new()
    {
        Id = u.Id,
        Code = u.Code,
        NameZh = u.NameZh,
        NameEn = u.NameEn,
        Symbol = u.Symbol,
        Category = u.Category,
        IsBase = u.IsBase,
        Factor = u.Factor,
        Precision = u.Precision,
        SortOrder = u.SortOrder,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt,
        UpdatedAt = u.UpdatedAt,
    };
}
