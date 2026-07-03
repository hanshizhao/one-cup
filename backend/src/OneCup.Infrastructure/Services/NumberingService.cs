using Microsoft.EntityFrameworkCore;
using OneCup.Application.Common;
using OneCup.Application.Interfaces;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;

namespace OneCup.Infrastructure.Services;

/// <summary>
/// 编码生成服务实现。调用方事务内行锁取号 + 唯一约束兜底重试。
/// GenerateAsync 不自管事务——必须在调用方已开启的事务内执行（fail-fast 守卫强制检查）。
/// B+ 方案：业务对象保存失败时调用方回滚事务，计数器增量随之回滚，保证不跳号。
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
        // fail-fast：必须在调用方事务内调用（FOR UPDATE 行锁依赖事务；B+ 不跳号回滚保证依赖事务）
        if (_db.Database.CurrentTransaction is null)
            throw new DomainException("GenerateAsync 必须在调用方的事务内调用");

        for (int attempt = 0; attempt < MaxRetry; attempt++)
        {
            try
            {
                var rule = await _db.NumberingRules
                    .FirstOrDefaultAsync(r => r.TargetType == targetType && r.IsActive, ct)
                    ?? throw new DomainException($"未找到 {targetType} 的启用编码规则");

                // ── 字典强校验（Task 6）──
                var typeExists = await _db.NumberingTargetTypes
                    .AnyAsync(t => t.Code == targetType && t.IsActive, ct);
                if (!typeExists)
                    throw new DomainException($"业务类型 {targetType} 不存在或已停用");

                if (rule.IncludeCategory && string.IsNullOrEmpty(categoryCode))
                    throw new DomainException("规则要求品类码但未提供");

                if (rule.IncludeCategory && !string.IsNullOrEmpty(categoryCode))
                {
                    var catExists = await _db.NumberingCategories
                        .AnyAsync(c => c.TargetTypeCode == targetType
                                    && c.Code == categoryCode && c.IsActive, ct);
                    if (!catExists)
                        throw new DomainException($"分类码 {categoryCode} 不存在或已停用");
                }

                // 宽容：规则不要分类码但传了，忽略
                var effectiveCategory = rule.IncludeCategory ? categoryCode : null;

                var now = _clock.GetCurrentTime();
                var periodKey = PeriodKeyCalculator.Calc(rule.ResetPeriod, now);
                var bucketCategory = effectiveCategory ?? "";
                var bucketPeriod = periodKey;

                // 行锁取号（在调用方事务内加锁）
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

                return code;  // 由调用方提交事务
            }
            catch (Exception ex) when (IsUniqueConstraintViolation(ex))
            {
                // 桶竞态：分离失败的 Added 实体，使变更跟踪器干净后重试（下次 SELECT 会找到对方已提交的桶）。
                // 注意：EF Core 在环境事务内的 SaveChanges 自动创建 savepoint，唯一约束冲突时回滚到 savepoint，
                // 使事务保持可用（PG 默认会在 23505 后进入 aborted 态，savepoint 保护重试的 SELECT 不受影响）。
                // 这是 EF Core 6.0+ 的默认行为，依赖此机制使重试安全。
                foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
                    entry.State = EntityState.Detached;
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

        // ── 字典强校验（Task 6）──
        var typeExists = await _db.NumberingTargetTypes
            .AnyAsync(t => t.Code == targetType && t.IsActive, ct);
        if (!typeExists)
            throw new DomainException($"业务类型 {targetType} 不存在或已停用");

        if (rule.IncludeCategory && !string.IsNullOrEmpty(categoryCode))
        {
            var catExists = await _db.NumberingCategories
                .AnyAsync(c => c.TargetTypeCode == targetType
                            && c.Code == categoryCode && c.IsActive, ct);
            if (!catExists)
                throw new DomainException($"分类码 {categoryCode} 不存在或已停用");
        }

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
