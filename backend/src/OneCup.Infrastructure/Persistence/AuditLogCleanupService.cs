using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneCup.Application.Options;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence;

/// <summary>
/// 审计日志定时清理：单实例 BackgroundService。
/// 每天在 CleanupTime（本地时间，默认 03:00）执行一次，删除超过 RetentionDays 的日志。
/// 用 ExecuteDeleteAsync（EF Core 7+ 原生批量删除）一条 SQL，不载入实体。
/// 通过 IServiceScopeFactory 每次创建独立 scope 解析 Scoped DbContext，避免单例持有 Scoped 服务。
/// </summary>
public sealed class AuditLogCleanupService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly AuditLogOptions _options;
    private readonly ILogger<AuditLogCleanupService> _logger;
    private readonly TimeProvider _timeProvider;

    public AuditLogCleanupService(
        IServiceScopeFactory scopeFactory,
        IOptions<AuditLogOptions> options,
        ILogger<AuditLogCleanupService> logger,
        TimeProvider? timeProvider = null)
    {
        _scopeFactory = scopeFactory;
        _options = options.Value;
        _logger = logger;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var delay = NextRunDelay();
            try
            {
                await Task.Delay(delay, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                return;
            }

            if (stoppingToken.IsCancellationRequested) return;

            await CleanupAsync(stoppingToken);
        }
    }

    /// <summary>计算到下次 CleanupTime 的等待时长。</summary>
    private TimeSpan NextRunDelay()
    {
        if (!TimeOnly.TryParse(_options.CleanupTime, out var time))
            time = new TimeOnly(3, 0);

        var now = _timeProvider.GetLocalNow();
        var next = now.Date.Add(time.ToTimeSpan());
        if (next <= now) next = next.AddDays(1);
        return next - now;
    }

    private async Task CleanupAsync(CancellationToken ct)
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-_options.RetentionDays);

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<OneCupDbContext>();
            var opsDeleted = await db.Set<OperationLog>().Where(x => x.CreatedAt < cutoff).ExecuteDeleteAsync(ct);
            var loginsDeleted = await db.Set<LoginLog>().Where(x => x.CreatedAt < cutoff).ExecuteDeleteAsync(ct);

            if (opsDeleted > 0 || loginsDeleted > 0)
            {
                _logger.LogInformation("清理超期审计日志：操作日志 {Ops} 条，登录日志 {Logins} 条，截止 {Cutoff}",
                    opsDeleted, loginsDeleted, cutoff);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "审计日志清理任务失败");
        }
    }
}
