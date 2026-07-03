using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 编号字典管理服务实现。通过 IRepository + Specification 访问数据，IUnitOfWork 提交。
/// 业务类型 code 不可改；分类 code/targetTypeCode 不可改。
/// 新增分类时校验 targetTypeCode 存在且启用，防孤儿分类。
/// </summary>
public class NumberingDictionaryService : INumberingDictionaryService
{
    private readonly IRepository<NumberingTargetType> _types;
    private readonly IRepository<NumberingCategory> _categories;
    private readonly IUnitOfWork _uow;

    public NumberingDictionaryService(
        IRepository<NumberingTargetType> types,
        IRepository<NumberingCategory> categories,
        IUnitOfWork uow)
    {
        _types = types;
        _categories = categories;
        _uow = uow;
    }

    // ── 业务类型 ──

    public async Task<PagedResult<TargetTypeDto>> GetTargetTypesAsync(
        int page, int pageSize, string? keyword, bool? isActive, CancellationToken ct = default)
    {
        // 关键:总数用仅含过滤条件的 FilterSpec 统计,绝不能用带分页的 PagedSpec,
        // 否则 Repository.CountAsync 会应用 Skip/Take,只统计当前页子集。
        var total = await _types.CountAsync(new TargetTypeFilterSpec(keyword, isActive), ct);
        var types = await _types.ListAsync(new TargetTypePagedSpec(keyword, isActive, page, pageSize), ct);

        return new PagedResult<TargetTypeDto>
        {
            Items = types.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<List<TargetTypeDto>> GetAllActiveTargetTypesAsync(CancellationToken ct = default)
    {
        var types = await _types.ListAsync(new TargetTypeActiveSpec(), ct);
        return types.Select(ToDto).ToList();
    }

    public async Task<TargetTypeDto?> GetTargetTypeAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _types.FirstOrDefaultAsync(new TargetTypeByIdSpec(id), ct);
        return t is null ? null : ToDto(t);
    }

    public async Task<TargetTypeDto> CreateTargetTypeAsync(CreateTargetTypeRequest request, CancellationToken ct = default)
    {
        // code 唯一性:用 TargetTypeByCodeSpec(不含 IsActive 过滤),停用也占用 code
        if (await _types.AnyAsync(new TargetTypeByCodeSpec(request.Code), ct))
            throw new DomainException($"业务类型 code '{request.Code}' 已存在");

        var entity = new NumberingTargetType
        {
            Code = request.Code,
            NameZh = request.NameZh,
            NameEn = request.NameEn,
            SortOrder = request.SortOrder,
            IsActive = true,
        };
        await _types.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task UpdateTargetTypeAsync(Guid id, UpdateTargetTypeRequest request, CancellationToken ct = default)
    {
        // code 不可改:更新接口不暴露 Code 字段,无需特殊处理
        var entity = await _types.FirstOrDefaultAsync(new TargetTypeByIdSpec(id), ct)
            ?? throw new DomainException("业务类型不存在");

        if (request.NameZh is not null) entity.NameZh = request.NameZh;
        if (request.NameEn is not null) entity.NameEn = request.NameEn;
        if (request.SortOrder is not null) entity.SortOrder = request.SortOrder.Value;

        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdateTargetTypeStatusAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var entity = await _types.FirstOrDefaultAsync(new TargetTypeByIdSpec(id), ct)
            ?? throw new DomainException("业务类型不存在");
        entity.IsActive = isActive;
        await _uow.SaveChangesAsync(ct);
    }

    // ── 分类 ──

    public async Task<PagedResult<CategoryDto>> GetCategoriesAsync(
        int page, int pageSize, string? targetTypeCode, string? keyword, bool? isActive, CancellationToken ct = default)
    {
        var total = await _categories.CountAsync(new CategoryFilterSpec(targetTypeCode, keyword, isActive), ct);
        var cats = await _categories.ListAsync(new CategoryPagedSpec(targetTypeCode, keyword, isActive, page, pageSize), ct);

        return new PagedResult<CategoryDto>
        {
            Items = cats.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<List<CategoryDto>> GetActiveCategoriesAsync(string targetTypeCode, CancellationToken ct = default)
    {
        var cats = await _categories.ListAsync(new CategoryActiveByTypeSpec(targetTypeCode), ct);
        return cats.Select(ToDto).ToList();
    }

    public async Task<CategoryDto?> GetCategoryAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _categories.FirstOrDefaultAsync(new CategoryByIdSpec(id), ct);
        return c is null ? null : ToDto(c);
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        // 校验 targetTypeCode 存在且启用——用 TargetTypeActiveByCodeSpec(非 TargetTypeByCodeSpec),
        // 防止在已停用的业务类型下创建孤儿分类。
        var typeExists = await _types.AnyAsync(
            new TargetTypeActiveByCodeSpec(request.TargetTypeCode), ct);
        if (!typeExists)
            throw new DomainException($"业务类型 '{request.TargetTypeCode}' 不存在或已停用，无法在其下创建分类");

        // 校验 (targetTypeCode, code) 组合唯一
        if (await _categories.AnyAsync(
            new CategoryByTypeAndCodeSpec(request.TargetTypeCode, request.Code), ct))
            throw new DomainException($"分类 code '{request.Code}' 在业务类型 '{request.TargetTypeCode}' 下已存在");

        var entity = new NumberingCategory
        {
            TargetTypeCode = request.TargetTypeCode,
            Code = request.Code,
            NameZh = request.NameZh,
            NameEn = request.NameEn,
            SortOrder = request.SortOrder,
            IsActive = true,
        };
        await _categories.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        // code/targetTypeCode 不可改:更新接口不暴露这两个字段
        var entity = await _categories.FirstOrDefaultAsync(new CategoryByIdSpec(id), ct)
            ?? throw new DomainException("分类不存在");

        if (request.NameZh is not null) entity.NameZh = request.NameZh;
        if (request.NameEn is not null) entity.NameEn = request.NameEn;
        if (request.SortOrder is not null) entity.SortOrder = request.SortOrder.Value;

        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdateCategoryStatusAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var entity = await _categories.FirstOrDefaultAsync(new CategoryByIdSpec(id), ct)
            ?? throw new DomainException("分类不存在");
        entity.IsActive = isActive;
        await _uow.SaveChangesAsync(ct);
    }

    // ── DTO 映射 ──

    private static TargetTypeDto ToDto(NumberingTargetType t) => new()
    {
        Id = t.Id,
        Code = t.Code,
        NameZh = t.NameZh,
        NameEn = t.NameEn,
        SortOrder = t.SortOrder,
        IsActive = t.IsActive,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
    };

    private static CategoryDto ToDto(NumberingCategory c) => new()
    {
        Id = c.Id,
        TargetTypeCode = c.TargetTypeCode,
        Code = c.Code,
        NameZh = c.NameZh,
        NameEn = c.NameEn,
        SortOrder = c.SortOrder,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
    };
}
