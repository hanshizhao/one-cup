using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Equipment;

/// <summary>
/// 设备模块测试共享辅助。
/// FakeNumberingService 支持 Prefix 配置（EQT- 给类型、EQ- 给设备），
/// 三份测试文件共用，避免同命名空间重复定义。
/// </summary>
internal static class EquipmentTestHelper
{
    public static OneCupDbContext CreateDb(string namePrefix)
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"{namePrefix}-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);
        return db;
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }
}

/// <summary>
/// 共享 fake。Prefix 决定生成编号前缀；NextCode 设了就用一次后清空，否则自增。
/// </summary>
internal sealed class FakeNumberingService : INumberingService
{
    private readonly string _prefix;
    public string? NextCode { get; set; }
    private int _seq;

    public FakeNumberingService(string prefix)
    {
        _prefix = prefix;
    }

    public Task<string> GenerateAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
    {
        if (NextCode is not null)
        {
            var code = NextCode;
            NextCode = null;
            return Task.FromResult(code);
        }
        _seq++;
        return Task.FromResult($"{_prefix}{_seq:D4}");
    }

    public Task<PreviewResult> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
        => Task.FromResult(new PreviewResult { Code = NextCode ?? $"{_prefix}{(_seq + 1):D4}", IncludeCategory = false });
}
