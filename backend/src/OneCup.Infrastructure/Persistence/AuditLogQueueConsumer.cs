using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using OneCup.Application.Options;

namespace OneCup.Infrastructure.Persistence;

/// <summary>
/// 审计日志消费者：单实例 BackgroundService。
/// 循环从 Channel 读一批（最多 BatchSize 条或等 1 秒），分桶批量写入两张表。
/// 写入失败丢弃该批并记日志，绝不阻塞队列或拖垮业务 DB。
/// 应用关闭时（ApplicationStopping）尽力消费完当前批。
/// 用 IDbContextFactory 创建短生命周期 DbContext，避免单例持有 Scoped DbContext。
/// </summary>
public sealed class AuditLogQueueConsumer : BackgroundService
{
    private readonly AuditLogChannel _channel;
    private readonly IDbContextFactory<OneCupDbContext> _dbFactory;
    private readonly AuditLogOptions _options;
    private readonly ILogger<AuditLogQueueConsumer> _logger;

    public AuditLogQueueConsumer(
        AuditLogChannel channel,
        IDbContextFactory<OneCupDbContext> dbFactory,
        IOptions<AuditLogOptions> options,
        ILogger<AuditLogQueueConsumer> logger)
    {
        _channel = channel;
        _dbFactory = dbFactory;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // 阻塞等待第一条（直到有数据或取消令牌），再非阻塞地尽量读满剩余。
        while (!stoppingToken.IsCancellationRequested)
        {
            var batch = await ReadBatchAsync(_options.BatchSize, stoppingToken);
            if (batch.Count == 0) continue;

            await WriteBatchAsync(batch, stoppingToken);
        }
    }

    /// <summary>读一批条目：先等第一条（阻塞至有数据或取消），再非阻塞地尽量读满。</summary>
    private async Task<List<AuditLogEntry>> ReadBatchAsync(int maxCount, CancellationToken ct)
    {
        var batch = new List<AuditLogEntry>(maxCount);

        // 等第一条（可能阻塞到取消）
        try
        {
            var first = await _channel.Reader.ReadAsync(ct);
            batch.Add(first);
        }
        catch (OperationCanceledException)
        {
            return batch;
        }

        // 非阻塞地尽量读满剩余
        while (batch.Count < maxCount && _channel.Reader.TryRead(out var item))
        {
            batch.Add(item);
        }

        return batch;
    }

    /// <summary>批量写库：分桶后各自 AddRange + 一次 SaveChanges。失败丢批记日志。</summary>
    private async Task WriteBatchAsync(List<AuditLogEntry> batch, CancellationToken ct)
    {
        var ops = batch.Where(e => e.Operation is not null).Select(e => e.Operation!).ToList();
        var logins = batch.Where(e => e.Login is not null).Select(e => e.Login!).ToList();

        try
        {
            await using var db = await _dbFactory.CreateDbContextAsync(ct);
            if (ops.Count > 0) await db.OperationLogs.AddRangeAsync(ops, ct);
            if (logins.Count > 0) await db.LoginLogs.AddRangeAsync(logins, ct);
            await db.SaveChangesAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // 写入失败：丢弃该批，避免无限重试堵死队列；日志系统不应拖垮业务
            _logger.LogError(ex, "审计日志批量写入失败，丢弃 {Count} 条 (ops={Ops}, logins={Logins})",
                batch.Count, ops.Count, logins.Count);
        }
    }
}
