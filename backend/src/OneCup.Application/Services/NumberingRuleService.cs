using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 编号规则管理服务实现。
/// 通过 IRepository + Specification 访问数据,不直接依赖 EF Core;
/// 写入操作通过 IUnitOfWork 提交。
/// GetLogsAsync 涉及 NumberingLogs ⧝ NumberingRules 的左连接投影,
/// 通用 Specification(Where/Include/OrderBy/Paging)无法表达,故走 IRepository.Query()
/// 逃生舱口 + System.Linq LINQ join(不引入 EF Core 耦合)。
/// </summary>
public class NumberingRuleService : INumberingRuleService
{
    private readonly IRepository<NumberingRule> _rules;
    private readonly IRepository<NumberingLog> _logs;
    private readonly IUnitOfWork _uow;
    private readonly INumberingClock _clock;

    public NumberingRuleService(
        IRepository<NumberingRule> rules,
        IRepository<NumberingLog> logs,
        IUnitOfWork uow,
        INumberingClock clock)
    {
        _rules = rules;
        _logs = logs;
        _uow = uow;
        _clock = clock;
    }

    public async Task<PagedResult<NumberingRuleListItemDto>> GetListAsync(
        int page, int pageSize, string? keyword, string? targetType, bool? isActive,
        CancellationToken ct = default)
    {
        // 关键:总数用仅含过滤条件的 FilterSpec 统计,绝不能用带分页的 PagedSpec,
        // 否则 Repository.CountAsync 会应用 Skip/Take,只统计当前页子集。
        var total = await _rules.CountAsync(new NumberingRuleFilterSpec(keyword, targetType, isActive), ct);

        var rules = await _rules.ListAsync(new NumberingRulePagedSpec(keyword, targetType, isActive, page, pageSize), ct);

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
        var r = await _rules.FirstOrDefaultAsync(new NumberingRuleByIdSpec(id), ct);
        if (r is null) return null;
        return ToDto(r);
    }

    public async Task<NumberingRuleDto> CreateAsync(CreateNumberingRuleRequest request, CancellationToken ct = default)
    {
        ValidateRequest(request.Prefix, request.Separator, request.SeqLength);

        // 同 targetType 启用规则唯一性
        if (await _rules.AnyAsync(new NumberingRuleActiveTargetTypeSpec(request.TargetType), ct))
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
        await _rules.AddAsync(rule, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(rule);
    }

    public async Task UpdateAsync(Guid id, UpdateNumberingRuleRequest request, CancellationToken ct = default)
    {
        // 载入已跟踪实体(load-then-modify 写入用 tracked load)。
        var rule = await _rules.FirstOrDefaultAsync(new NumberingRuleByIdSpec(id), ct)
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
                await _rules.AnyAsync(new NumberingRuleActiveTargetTypeSpec(rule.TargetType, id), ct))
                throw new DomainException("该业务类型已有启用规则，请先停用现有的");

            ValidateRequest(rule.Prefix, rule.Separator, rule.SeqLength);
        }

        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdateStatusAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        // 载入已跟踪实体(load-then-modify 写入用 tracked load)。
        var rule = await _rules.FirstOrDefaultAsync(new NumberingRuleByIdSpec(id), ct)
            ?? throw new DomainException("规则不存在");

        if (isActive && !rule.IsActive)
        {
            // 启用时校验该 targetType 唯一性
            if (await _rules.AnyAsync(new NumberingRuleActiveTargetTypeSpec(rule.TargetType, id), ct))
                throw new DomainException("该业务类型已有启用规则，请先停用现有的");
        }
        rule.IsActive = isActive;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<PagedResult<NumberingLogListItemDto>> GetLogsAsync(
        int page, int pageSize, string? targetType, string? categoryCode,
        Guid? ruleId, string? code, DateTime? startDate, DateTime? endDate,
        CancellationToken ct = default)
    {
        // 左连接(NumberingLogs ⧝ NumberingRules)投影:通用 Specification 无法表达,
        // 走 IRepository.Query() 逃生舱口 + System.Linq LINQ join。
        // 仅用 System.Linq 操作符(Where/OrderBy/Skip/Take/Select/ToList/Count),
        // 不使用 EF Core 扩展方法(ToListAsync/CountAsync),以保持 Application 零 EF 依赖。
        // ct 传入底层 Queryable 时无法透传(LINQ 同步迭代),故用 Task.Run 包裹同步物化。
        return await Task.Run(() =>
        {
            var query = from log in _logs.Query()
                        join rule in _rules.Query() on log.RuleId equals rule.Id into rg
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

            var total = query.Count();
            var rows = query
                .OrderByDescending(x => x.log.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

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
        }, ct);
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
