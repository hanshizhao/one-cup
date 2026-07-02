using Microsoft.EntityFrameworkCore;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;

namespace OneCup.Infrastructure.Services;

/// <summary>
/// 编号规则管理服务实现。
/// </summary>
public class NumberingRuleService : INumberingRuleService
{
    private readonly OneCupDbContext _db;
    private readonly INumberingClock _clock;

    public NumberingRuleService(OneCupDbContext db, INumberingClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<PagedResult<NumberingRuleListItemDto>> GetListAsync(
        int page, int pageSize, string? keyword, string? targetType, bool? isActive,
        CancellationToken ct = default)
    {
        var query = _db.NumberingRules.AsQueryable();

        if (!string.IsNullOrWhiteSpace(keyword))
        {
            keyword = keyword.Trim();
            query = query.Where(r => r.Name.Contains(keyword) || r.Prefix.Contains(keyword));
        }
        if (!string.IsNullOrEmpty(targetType))
            query = query.Where(r => r.TargetType == targetType);
        if (isActive is not null)
            query = query.Where(r => r.IsActive == isActive);

        var total = await query.CountAsync(ct);
        var rules = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<NumberingRuleListItemDto>
        {
            Items = rules.Select(r => new NumberingRuleListItemDto
            {
                Id = r.Id,
                TargetType = r.TargetType,
                Name = r.Name,
                Prefix = r.Prefix,
                IsActive = r.IsActive,
                CreatedAt = r.CreatedAt,
                SampleFormat = CodeFormatter.FormatSample(r.Prefix, r.IncludeCategory, r.DateSegment, r.SeqLength, r.Separator, _clock.GetCurrentTime())
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<NumberingRuleDto?> GetAsync(Guid id, CancellationToken ct = default)
    {
        var r = await _db.NumberingRules.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (r is null) return null;
        return ToDto(r);
    }

    public async Task<NumberingRuleDto> CreateAsync(CreateNumberingRuleRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request.Prefix, request.Separator, request.SeqLength);

        // 同 targetType 启用规则唯一性
        if (await _db.NumberingRules.AnyAsync(r => r.TargetType == request.TargetType && r.IsActive, ct))
            throw new DomainException("该业务类型已有启用规则，请先停用现有的");

        var rule = new NumberingRule
        {
            TargetType = request.TargetType,
            Name = request.Name,
            Prefix = request.Prefix,
            IncludeCategory = request.IncludeCategory,
            DateSegment = request.DateSegment,
            SeqLength = request.SeqLength,
            Separator = request.Separator,
            ResetPeriod = request.ResetPeriod,
            Remark = request.Remark,
            IsActive = true,
        };
        _db.NumberingRules.Add(rule);
        await _db.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task UpdateAsync(Guid id, UpdateNumberingRuleRequest request, CancellationToken ct = default)
    {
        var rule = await _db.NumberingRules.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new DomainException("规则不存在");

        if (rule.IsActive && HasKeyFieldChange(request))
            throw new DomainException("已启用的规则不可修改关键配置，请先停用");

        // 应用变更（仅非 null 字段）
        if (request.Name is not null) rule.Name = request.Name;
        if (request.Remark is not null) rule.Remark = request.Remark;
        if (!rule.IsActive)
        {
            if (request.Prefix is not null) rule.Prefix = request.Prefix;
            if (request.TargetType is not null) rule.TargetType = request.TargetType;
            if (request.IncludeCategory is not null) rule.IncludeCategory = request.IncludeCategory.Value;
            if (request.DateSegment is not null) rule.DateSegment = request.DateSegment.Value;
            if (request.SeqLength is not null) rule.SeqLength = request.SeqLength.Value;
            if (request.Separator is not null) rule.Separator = request.Separator;
            if (request.ResetPeriod is not null) rule.ResetPeriod = request.ResetPeriod.Value;

            // 改关键字段时复检唯一性（防止停用规则改成与他人冲突的 targetType 再保存）
            if (request.TargetType is not null &&
                await _db.NumberingRules.AnyAsync(r => r.Id != id && r.TargetType == rule.TargetType && r.IsActive, ct))
                throw new DomainException("该业务类型已有启用规则，请先停用现有的");

            ValidateRequest(rule.Prefix, rule.Separator, rule.SeqLength);
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task UpdateStatusAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var rule = await _db.NumberingRules.FirstOrDefaultAsync(r => r.Id == id, ct)
            ?? throw new DomainException("规则不存在");

        if (isActive && !rule.IsActive)
        {
            // 启用时校验该 targetType 唯一性
            if (await _db.NumberingRules.AnyAsync(r => r.Id != id && r.TargetType == rule.TargetType && r.IsActive, ct))
                throw new DomainException("该业务类型已有启用规则，请先停用现有的");
        }
        rule.IsActive = isActive;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<NumberingLogListItemDto>> GetLogsAsync(
        int page, int pageSize, string? targetType, string? categoryCode,
        Guid? ruleId, string? code, DateTime? startDate, DateTime? endDate,
        CancellationToken ct = default)
    {
        var query = from log in _db.NumberingLogs
                    join rule in _db.NumberingRules on log.RuleId equals rule.Id into rg
                    from rule in rg.DefaultIfEmpty()
                    select new { log, rule };

        if (!string.IsNullOrEmpty(targetType))
            query = query.Where(x => x.log.TargetType == targetType);
        if (!string.IsNullOrEmpty(categoryCode))
            query = query.Where(x => x.log.CategoryCode == categoryCode);
        if (ruleId is not null)
            query = query.Where(x => x.log.RuleId == ruleId);
        if (!string.IsNullOrWhiteSpace(code))
        {
            code = code.Trim();
            query = query.Where(x => x.log.GeneratedCode.Contains(code));
        }
        if (startDate is not null)
            query = query.Where(x => x.log.CreatedAt >= startDate);
        if (endDate is not null)
            query = query.Where(x => x.log.CreatedAt <= endDate);

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(x => x.log.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<NumberingLogListItemDto>
        {
            Items = rows.Select(x => new NumberingLogListItemDto
            {
                Id = x.log.Id,
                GeneratedCode = x.log.GeneratedCode,
                TargetType = x.log.TargetType,
                CategoryCode = x.log.CategoryCode,
                PeriodKey = x.log.PeriodKey,
                SeqValue = x.log.SeqValue,
                CreatedAt = x.log.CreatedAt,
                RuleName = x.rule?.Name,
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    private NumberingRuleDto ToDto(NumberingRule r) => new()
    {
        Id = r.Id,
        TargetType = r.TargetType,
        Name = r.Name,
        Prefix = r.Prefix,
        IncludeCategory = r.IncludeCategory,
        DateSegment = r.DateSegment,
        SeqLength = r.SeqLength,
        Separator = r.Separator,
        ResetPeriod = r.ResetPeriod,
        IsActive = r.IsActive,
        Remark = r.Remark,
        CreatedAt = r.CreatedAt,
        UpdatedAt = r.UpdatedAt,
        SampleFormat = CodeFormatter.FormatSample(r.Prefix, r.IncludeCategory, r.DateSegment, r.SeqLength, r.Separator, _clock.GetCurrentTime())
    };

    private static void ValidateRequest(string prefix, string separator, short seqLength)
    {
        if (seqLength < 1 || seqLength > 8)
            throw new DomainException("流水号位数须在 1-8 之间");
        // 前缀不可包含分隔符（避免产出有歧义的编码）
        if (!string.IsNullOrEmpty(separator) && prefix.Contains(separator))
            throw new DomainException("前缀不可包含分隔符");
    }

    private static bool HasKeyFieldChange(UpdateNumberingRuleRequest r) =>
        r.Prefix is not null || r.TargetType is not null || r.IncludeCategory is not null ||
        r.DateSegment is not null || r.SeqLength is not null || r.Separator is not null ||
        r.ResetPeriod is not null;
}
