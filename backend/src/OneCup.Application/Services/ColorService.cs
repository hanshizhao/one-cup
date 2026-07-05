using System.Text.RegularExpressions;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 颜色主数据管理服务实现。通过 IRepository + Specification 访问数据，IUnitOfWork 提交。
/// CreateAsync 在事务内经编号引擎取号（并发安全、不跳号）；code 创建后不可改；
/// hex 格式校验；只启停不物理删除。
/// </summary>
public class ColorService : IColorService
{
    private static readonly Regex HexRegex = new(
        @"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    private readonly IRepository<Color> _colors;
    private readonly IUnitOfWork _uow;
    private readonly INumberingService _numbering;

    public ColorService(IRepository<Color> colors, IUnitOfWork uow, INumberingService numbering)
    {
        _colors = colors;
        _uow = uow;
        _numbering = numbering;
    }

    public async Task<PagedResult<ColorDto>> GetColorsAsync(
        int page, int pageSize, string? keyword, string? colorFamily, bool? isActive,
        CancellationToken ct = default)
    {
        // 关键:总数用仅含过滤条件的 FilterSpec 统计,绝不能用带分页的 PagedSpec,
        // 否则 Repository.CountAsync 会应用 Skip/Take,只统计当前页子集。
        var total = await _colors.CountAsync(
            new ColorFilterSpec(keyword, colorFamily, isActive), ct);
        var colors = await _colors.ListAsync(
            new ColorPagedSpec(keyword, colorFamily, isActive, page, pageSize), ct);

        return new PagedResult<ColorDto>
        {
            Items = colors.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<List<ColorDto>> GetAllActiveColorsAsync(CancellationToken ct = default)
    {
        var colors = await _colors.ListAsync(new ColorActiveSpec(), ct);
        return colors.Select(ToDto).ToList();
    }

    public async Task<ColorDto?> GetColorAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _colors.FirstOrDefaultAsync(new ColorByIdSpec(id), ct);
        return c is null ? null : ToDto(c);
    }

    public async Task<ColorDto> CreateColorAsync(CreateColorRequest request, CancellationToken ct = default)
    {
        ValidateHex(request.Hex);

        Guid createdId = Guid.Empty;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            // 事务内经编号引擎取号（行锁），计数器增量与颜色记录一起提交（不跳号）
            var code = await _numbering.GenerateAsync(NumberTargetTypes.Color, request.CategoryCode, ct);
            var entity = new Color
            {
                Code = code,
                NameZh = request.NameZh,
                NameEn = request.NameEn,
                Hex = request.Hex,
                ColorFamily = request.ColorFamily,
                Remark = request.Remark,
                SortOrder = request.SortOrder,
                IsActive = true,
            };
            await _colors.AddAsync(entity, ct);
            await _uow.SaveChangesAsync(ct);
            createdId = entity.Id;
        }, ct);

        return await GetColorAsync(createdId, ct) ?? throw new DomainException("颜色创建失败");
    }

    public async Task UpdateColorAsync(Guid id, UpdateColorRequest request, CancellationToken ct = default)
    {
        // code 不可改:更新接口不暴露 Code 字段,无需特殊处理
        var entity = await _colors.FirstOrDefaultAsync(new ColorByIdSpec(id), ct)
            ?? throw new DomainException("颜色不存在");

        if (request.Hex is not null)
        {
            ValidateHex(request.Hex);
            entity.Hex = request.Hex;
        }
        if (request.NameZh is not null) entity.NameZh = request.NameZh;
        if (request.NameEn is not null) entity.NameEn = request.NameEn;
        if (request.ColorFamily is not null) entity.ColorFamily = request.ColorFamily;
        if (request.Remark is not null) entity.Remark = request.Remark;
        if (request.SortOrder is not null) entity.SortOrder = request.SortOrder.Value;

        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdateColorStatusAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var entity = await _colors.FirstOrDefaultAsync(new ColorByIdSpec(id), ct)
            ?? throw new DomainException("颜色不存在");
        entity.IsActive = isActive;
        await _uow.SaveChangesAsync(ct);
    }

    // ── 内部工具 ──

    private static void ValidateHex(string hex)
    {
        if (!HexRegex.IsMatch(hex))
            throw new DomainException($"颜色值 '{hex}' 格式非法，必须为 #RRGGBB（如 #FF0000）");
    }

    private static ColorDto ToDto(Color c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        NameZh = c.NameZh,
        NameEn = c.NameEn,
        Hex = c.Hex,
        ColorFamily = c.ColorFamily,
        Remark = c.Remark,
        SortOrder = c.SortOrder,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
    };
}
