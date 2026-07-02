using Microsoft.EntityFrameworkCore;
using OneCup.Application.Common;
using OneCup.Application.Interfaces;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;

namespace OneCup.Infrastructure.Services;

/// <summary>
/// 编码生成服务实现。事务内行锁取号 + 唯一约束兜底重试。
/// </summary>
public class NumberingService : INumberingService
{
    private const int MaxRetry = 3;
    private readonly OneCupDbContext _db;
    private readonly INumberingClock _clock;

    public NumberingService(OneCupDbContext db, INumberingClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<string> GenerateAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
    {
        for (int attempt = 0; attempt < MaxRetry; attempt++)
        {
            var tx = await _db.Database.BeginTransactionAsync(ct);
            try
            {
                var rule = await _db.NumberingRules
                    .FirstOrDefaultAsync(r => r.TargetType == targetType && r.IsActive, ct)
                    ?? throw new DomainException($"未找到 {targetType} 的启用编码规则");

                if (rule.IncludeCategory && string.IsNullOrEmpty(categoryCode))
                    throw new DomainException("规则要求品类码但未提供");

                // 宽容：规则不要分类码但传了，忽略
                var effectiveCategory = rule.IncludeCategory ? categoryCode : null;

                var now = _clock.GetCurrentTime();
                var periodKey = PeriodKeyCalculator.Calc(rule.ResetPeriod, now);
                var bucketCategory = effectiveCategory ?? "";
                var bucketPeriod = periodKey;

                // 行锁取号
                var bucket = await _db.NumberingCounters
                    .FromSqlRaw(
                        "SELECT * FROM numbering_counters WHERE rule_id={0} AND category_code={1} AND period_key={2} FOR UPDATE",
                        rule.Id, bucketCategory, bucketPeriod)
                    .FirstOrDefaultAsync(ct);

                if (bucket is null)
                {
                    bucket = new NumberingCounter
                    {
                        RuleId = rule.Id,
                        CategoryCode = bucketCategory,
                        PeriodKey = bucketPeriod,
                        CurrentSeq = 0
                    };
                    _db.NumberingCounters.Add(bucket);
                    await _db.SaveChangesAsync(ct);  // 唯一约束冲突会在此抛出
                }

                bucket.CurrentSeq += 1;
                var newSeq = bucket.CurrentSeq;
                await _db.SaveChangesAsync(ct);

                var code = CodeFormatter.Format(
                    rule.Prefix, rule.IncludeCategory, rule.DateSegment,
                    rule.SeqLength, rule.Separator, newSeq, effectiveCategory, now);

                // 写日志（同事务）
                _db.NumberingLogs.Add(new NumberingLog
                {
                    GeneratedCode = code,
                    RuleId = rule.Id,
                    TargetType = rule.TargetType,
                    CategoryCode = effectiveCategory,
                    PeriodKey = string.IsNullOrEmpty(bucketPeriod) ? null : bucketPeriod,
                    SeqValue = newSeq,
                });
                await _db.SaveChangesAsync(ct);

                await tx.CommitAsync(ct);
                return code;
            }
            catch (Exception ex) when (IsUniqueConstraintViolation(ex))
            {
                await tx.RollbackAsync(ct);
                // 桶被别人建了，重试时 SELECT 会找到它
                continue;
            }
        }
        throw new DomainException("编号生成失败：并发冲突，请重试");
    }

    public async Task<string?> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
    {
        var rule = await _db.NumberingRules
            .FirstOrDefaultAsync(r => r.TargetType == targetType && r.IsActive, ct);
        if (rule is null) return null;

        var effectiveCategory = rule.IncludeCategory ? categoryCode : null;

        var now = _clock.GetCurrentTime();
        var periodKey = PeriodKeyCalculator.Calc(rule.ResetPeriod, now);
        var bucketCategory = effectiveCategory ?? "";
        var bucketPeriod = periodKey;

        // 只读查询，不加锁
        var currentSeq = await _db.NumberingCounters
            .Where(c => c.RuleId == rule.Id && c.CategoryCode == bucketCategory && c.PeriodKey == bucketPeriod)
            .Select(c => (int?)c.CurrentSeq)
            .FirstOrDefaultAsync(ct) ?? 0;

        return CodeFormatter.Format(
            rule.Prefix, rule.IncludeCategory, rule.DateSegment,
            rule.SeqLength, rule.Separator, currentSeq + 1, effectiveCategory, now);
    }

    /// <summary>
    /// 识别桶唯一约束冲突（PostgreSQL 唯一约束违反错误码 23505）。
    /// </summary>
    private static bool IsUniqueConstraintViolation(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
        {
            if (e.Message.Contains("23505") || e.Message.Contains("ux_numbering_counters_bucket", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }
}
