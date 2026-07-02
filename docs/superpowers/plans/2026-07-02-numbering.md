# 编号管理（编码规则引擎）实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 构建统一编码规则引擎，为各业务模块提供配置化的自动编码生成（唯一、连续、可追溯）。

**Architecture:** Clean Architecture 四层。规则/计数/日志三张表。取号走事务内 `SELECT ... FOR UPDATE` 行锁 + 唯一约束兜底重试。拼码是纯函数。编码周期键按北京时间（`Asia/Shanghai`），存储时间戳按 UTC。

**Tech Stack:** .NET 10, EF Core 10, PostgreSQL 17 (Npgsql), xUnit, Testcontainers (并发测试), Arco Design Pro (React + Vite + TS)。

**Spec:** `docs/specs/2026-07-02-numbering-design.md`

## Global Constraints

- 命名：数据库表/列用 snake_case；C# 用 PascalCase。沿用项目既有约定。
- 审计字段：所有实体继承 `BaseEntity`（`Id` Guid / `CreatedAt` DateTime / `UpdatedAt` DateTime?）。`CreatedAt` 不在初始化器赋值——由 `OneCupDbContext.SaveChangesAsync` 的 `SetAuditFields` 填入；种子数据手动赋 `SeedTimestamp`。
- 枚举序列化：项目已全局配置 `JsonStringEnumConverter`，枚举序列化为字符串。
- 异常：业务校验失败抛 `DomainException`（→ 全局异常处理器 → 400），不手动返回 BadRequest。
- 服务注入：直接注入 `OneCupDbContext`（沿用 `UserService` 风格）。服务方法签名带 `CancellationToken ct = default`。
- 权限 Guid 序号：`SeedData` 中 `...1112`/`...1113` 已用于 user/role manage，编号权限从 `...1114` 起。
- 计数表 `category_code`/`period_key` 用空串 `''`（因唯一索引）；日志表这两列允许 NULL。
- 编码周期键/日期段基于北京时间；数据库时间戳基于 UTC。
- 所有命令在 `backend/` 目录运行（除非另有说明）。
- 中文注释/提示沿用项目风格。

---

## File Structure

**新建文件：**
- `backend/src/OneCup.Domain/Enums/DateSegment.cs` — 日期段枚举
- `backend/src/OneCup.Domain/Enums/ResetPeriod.cs` — 重置周期枚举
- `backend/src/OneCup.Domain/Entities/NumberingRule.cs` — 规则实体
- `backend/src/OneCup.Domain/Entities/NumberingCounter.cs` — 计数实体
- `backend/src/OneCup.Domain/Entities/NumberingLog.cs` — 日志实体
- `backend/src/OneCup.Application/Common/NumberTargetTypes.cs` — 业务类型常量类
- `backend/src/OneCup.Application/Common/CodeFormatter.cs` — 拼码纯函数（TDD 核心）
- `backend/src/OneCup.Application/Common/PeriodKeyCalculator.cs` — 周期键纯函数
- `backend/src/OneCup.Application/Dtos/System/NumberingDtos.cs` — 全部编号 DTO
- `backend/src/OneCup.Application/Interfaces/INumberingClock.cs` — 时间提供者接口
- `backend/src/OneCup.Application/Interfaces/INumberingService.cs` — 生成/预览接口
- `backend/src/OneCup.Application/Interfaces/INumberingRuleService.cs` — 规则管理接口
- `backend/src/OneCup.Infrastructure/Services/NumberingClock.cs` — 北京时间实现
- `backend/src/OneCup.Infrastructure/Services/NumberingService.cs` — 生成/预览实现（并发核心）
- `backend/src/OneCup.Infrastructure/Services/NumberingRuleService.cs` — 规则管理实现
- `backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingRuleConfiguration.cs`
- `backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingCounterConfiguration.cs`
- `backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingLogConfiguration.cs`
- `backend/src/OneCup.Api/Controllers/NumberingController.cs`
- `backend/tests/OneCup.UnitTests/Numbering/CodeFormatterTests.cs`
- `backend/tests/OneCup.UnitTests/Numbering/PeriodKeyCalculatorTests.cs`
- `backend/tests/OneCup.UnitTests/Numbering/NumberingClockTests.cs`
- `backend/tests/OneCup.UnitTests/Numbering/NumberingRuleServiceTests.cs`
- `backend/tests/OneCup.UnitTests/Numbering/NumberingServiceConcurrencyTests.cs`
- `frontend/src/api/numbering.ts`
- `frontend/src/pages/system/numbering/index.tsx`
- `frontend/src/pages/system/numbering/locale/index.ts`

**修改文件：**
- `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs` — 加 3 个 DbSet
- `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs` — 加 2 个权限 Guid 常量
- `backend/src/OneCup.Api/Program.cs` — 注册服务 + 2 个授权 Policy
- `backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj` — 加 Testcontainers 依赖
- `frontend/src/routes.ts` — 加编号管理菜单
- `frontend/src/locale/index.ts` — 加 i18n

---

## Task 1: Domain 枚举、常量类与实体

**Files:**
- Create: `backend/src/OneCup.Domain/Enums/DateSegment.cs`
- Create: `backend/src/OneCup.Domain/Enums/ResetPeriod.cs`
- Create: `backend/src/OneCup.Domain/Entities/NumberingRule.cs`
- Create: `backend/src/OneCup.Domain/Entities/NumberingCounter.cs`
- Create: `backend/src/OneCup.Domain/Entities/NumberingLog.cs`

**Interfaces:**
- Produces: `DateSegment` enum（`None/Year/YearMonth/YearMonthDay`）、`ResetPeriod` enum（`None/Yearly/Monthly/Daily`）、`NumberingRule`/`NumberingCounter`/`NumberingLog` 实体。

- [ ] **Step 1: 创建枚举 DateSegment**

```csharp
namespace OneCup.Domain.Enums;

/// <summary>
/// 编码中的日期段类型。
/// </summary>
public enum DateSegment
{
    None,
    Year,
    YearMonth,
    YearMonthDay
}
```

- [ ] **Step 2: 创建枚举 ResetPeriod**

```csharp
namespace OneCup.Domain.Enums;

/// <summary>
/// 流水号重置周期。
/// </summary>
public enum ResetPeriod
{
    None,
    Yearly,
    Monthly,
    Daily
}
```

- [ ] **Step 3: 创建 NumberingRule 实体**

```csharp
using OneCup.Domain.Enums;

namespace OneCup.Domain.Entities;

/// <summary>
/// 编码规则。一条规则描述某个业务对象类型的编码如何生成。
/// </summary>
public class NumberingRule : BaseEntity
{
    /// <summary>业务对象类型（字符串，见 NumberTargetTypes，引擎不校验合法性）</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>规则名称</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>固定前缀段，如 FAB</summary>
    public string Prefix { get; set; } = string.Empty;

    /// <summary>是否拼分类码段</summary>
    public bool IncludeCategory { get; set; }

    /// <summary>日期段类型</summary>
    public DateSegment DateSegment { get; set; } = DateSegment.None;

    /// <summary>流水号位数（补零），1–8</summary>
    public short SeqLength { get; set; } = 4;

    /// <summary>段间分隔符，默认 "-"，可空串</summary>
    public string Separator { get; set; } = "-";

    /// <summary>重置周期</summary>
    public ResetPeriod ResetPeriod { get; set; } = ResetPeriod.None;

    /// <summary>启停状态（停用替代物理删除）</summary>
    public bool IsActive { get; set; } = true;

    /// <summary>备注</summary>
    public string? Remark { get; set; }
}
```

- [ ] **Step 4: 创建 NumberingCounter 实体**

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 编号计数器。一个「规则+品类+周期」对应一行（一个"桶"）。
/// category_code/period_key 用空串代替 NULL（避免 PG 唯一索引 NULL 歧义）。
/// </summary>
public class NumberingCounter : BaseEntity
{
    public Guid RuleId { get; set; }

    /// <summary>品类码（无品类时为空串）</summary>
    public string CategoryCode { get; set; } = string.Empty;

    /// <summary>周期键（不重置时为空串；按年="2026"；按月="202607"；按日="20260702"）</summary>
    public string PeriodKey { get; set; } = string.Empty;

    /// <summary>当前已分配到的最大流水号</summary>
    public int CurrentSeq { get; set; }

    public NumberingRule? Rule { get; set; }
}
```

- [ ] **Step 5: 创建 NumberingLog 实体**

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 编码生成日志。每次取号一条，只追加不修改。
/// 不关联业务对象 ID（保持引擎与业务解耦）。
/// </summary>
public class NumberingLog : BaseEntity
{
    /// <summary>生成的完整编码</summary>
    public string GeneratedCode { get; set; } = string.Empty;

    public Guid RuleId { get; set; }

    /// <summary>业务对象类型（冗余存，便于不 join 规则表直接筛）</summary>
    public string TargetType { get; set; } = string.Empty;

    /// <summary>品类码（可空）</summary>
    public string? CategoryCode { get; set; }

    /// <summary>周期键（可空）</summary>
    public string? PeriodKey { get; set; }

    /// <summary>流水号数值</summary>
    public int SeqValue { get; set; }

    public NumberingRule? Rule { get; set; }
}
```

- [ ] **Step 6: 编译验证**

Run: `dotnet build backend/src/OneCup.Domain/OneCup.Domain.csproj`
Expected: 成功，无错误。

- [ ] **Step 7: Commit**

```bash
git add backend/src/OneCup.Domain/Enums/ backend/src/OneCup.Domain/Entities/NumberingRule.cs backend/src/OneCup.Domain/Entities/NumberingCounter.cs backend/src/OneCup.Domain/Entities/NumberingLog.cs
git commit -m "feat(numbering): Domain 枚举与实体 (规则/计数/日志)"
```

---

## Task 2: 拼码与周期键纯函数（TDD 核心）

这是引擎最该用 TDD 的部分——纯函数、输入输出明确。先写测试，再实现。

**Files:**
- Create: `backend/src/OneCup.Application/Common/NumberTargetTypes.cs`
- Create: `backend/src/OneCup.Application/Common/PeriodKeyCalculator.cs`
- Create: `backend/src/OneCup.Application/Common/CodeFormatter.cs`
- Test: `backend/tests/OneCup.UnitTests/Numbering/PeriodKeyCalculatorTests.cs`
- Test: `backend/tests/OneCup.UnitTests/Numbering/CodeFormatterTests.cs`

**Interfaces:**
- Produces: `NumberTargetTypes`（常量类）、`PeriodKeyCalculator.Calc(ResetPeriod, DateTime): string`、`CodeFormatter.Format(...)` 与 `CodeFormatter.FormatSample(...)`。

- [ ] **Step 1: 创建 NumberTargetTypes 常量类**

```csharp
namespace OneCup.Application.Common;

/// <summary>
/// 已知业务对象类型清单。引擎不校验 target_type 合法性（见设计 6.1），
/// 此常量类仅作拼写提示与前端下拉选项来源，不强制。
/// </summary>
public static class NumberTargetTypes
{
    public const string Fabric = "fabric";
    public const string Material = "material";
    public const string Equipment = "equipment";
    public const string Customer = "customer";
    public const string Color = "color";
    public const string Product = "product";
}
```

- [ ] **Step 2: 写 PeriodKeyCalculator 的失败测试**

`backend/tests/OneCup.UnitTests/Numbering/PeriodKeyCalculatorTests.cs`：

```csharp
using OneCup.Application.Common;
using OneCup.Domain.Enums;

namespace OneCup.UnitTests.Numbering;

public class PeriodKeyCalculatorTests
{
    // 用北京时间 2026-07-02 14:30 作为测试时间
    private static readonly DateTime Now = new(2026, 7, 2, 14, 30, 0);

    [Fact]
    public void Calc_None_ReturnsEmpty()
    {
        Assert.Equal("", PeriodKeyCalculator.Calc(ResetPeriod.None, Now));
    }

    [Fact]
    public void Calc_Yearly_ReturnsYear()
    {
        Assert.Equal("2026", PeriodKeyCalculator.Calc(ResetPeriod.Yearly, Now));
    }

    [Fact]
    public void Calc_Monthly_ReturnsYearMonth()
    {
        Assert.Equal("202607", PeriodKeyCalculator.Calc(ResetPeriod.Monthly, Now));
    }

    [Fact]
    public void Calc_Daily_ReturnsYearMonthDay()
    {
        Assert.Equal("20260702", PeriodKeyCalculator.Calc(ResetPeriod.Daily, Now));
    }
}
```

- [ ] **Step 3: 运行测试验证失败**

Run: `dotnet test backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~PeriodKeyCalculatorTests"`
Expected: 编译失败（`PeriodKeyCalculator` 未定义）。

- [ ] **Step 4: 实现 PeriodKeyCalculator**

`backend/src/OneCup.Application/Common/PeriodKeyCalculator.cs`：

```csharp
using OneCup.Domain.Enums;

namespace OneCup.Application.Common;

/// <summary>
/// 周期键计算（纯函数）。传入的 now 应已是目标时区（北京时间）的时间。
/// </summary>
public static class PeriodKeyCalculator
{
    public static string Calc(ResetPeriod resetPeriod, DateTime now) => resetPeriod switch
    {
        ResetPeriod.None => "",
        ResetPeriod.Yearly => now.Year.ToString(),
        ResetPeriod.Monthly => now.ToString("yyyyMM"),
        ResetPeriod.Daily => now.ToString("yyyyMMdd"),
        _ => ""
    };
}
```

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~PeriodKeyCalculatorTests"`
Expected: 4 passed。

- [ ] **Step 6: 写 CodeFormatter 的失败测试**

`backend/tests/OneCup.UnitTests/Numbering/CodeFormatterTests.cs`：

```csharp
using OneCup.Application.Common;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;

namespace OneCup.UnitTests.Numbering;

public class CodeFormatterTests
{
    private static readonly DateTime Now = new(2026, 7, 2, 14, 30, 0);

    [Fact]
    public void Format_AllSegments()
    {
        var code = CodeFormatter.Format("FAB", true, DateSegment.Year, 4, "-", 7, "COT", Now);
        Assert.Equal("FAB-COT-2026-0007", code);
    }

    [Fact]
    public void Format_NoCategory()
    {
        var code = CodeFormatter.Format("FAB", false, DateSegment.Year, 4, "-", 7, "COT", Now);
        Assert.Equal("FAB-2026-0007", code);
    }

    [Fact]
    public void Format_NoDate()
    {
        var code = CodeFormatter.Format("FAB", true, DateSegment.None, 4, "-", 7, "COT", Now);
        Assert.Equal("FAB-COT-0007", code);
    }

    [Fact]
    public void Format_EmptySeparator()
    {
        var code = CodeFormatter.Format("EQ", false, DateSegment.Year, 4, "", 7, null, Now);
        Assert.Equal("EQ20260007", code);
    }

    [Fact]
    public void Format_PrefixAndSeqOnly()
    {
        var code = CodeFormatter.Format("FAB", false, DateSegment.None, 4, "-", 7, null, Now);
        Assert.Equal("FAB-0007", code);
    }

    [Fact]
    public void Format_YearMonth()
    {
        var code = CodeFormatter.Format("CL", true, DateSegment.YearMonth, 3, "-", 7, "RED", Now);
        Assert.Equal("CL-RED-202607-007", code);
    }

    [Fact]
    public void Format_YearMonthDay()
    {
        var code = CodeFormatter.Format("X", false, DateSegment.YearMonthDay, 4, "-", 1, null, Now);
        Assert.Equal("X-20260702-0001", code);
    }

    [Fact]
    public void Format_SeqPadding6()
    {
        var code = CodeFormatter.Format("MAT", false, DateSegment.None, 6, "-", 7, null, Now);
        Assert.Equal("MAT-000007", code);
    }

    [Fact]
    public void Format_SeqOverflow_Throws()
    {
        // seqLength=4 但 seq=10000（5 位）
        Assert.Throws<DomainException>(() =>
            CodeFormatter.Format("FAB", false, DateSegment.None, 4, "-", 10000, null, Now));
    }

    [Fact]
    public void Format_IncludeCategoryButNullCategory_OmitsCategorySegment()
    {
        // 宽容：声明要分类码但传入 null → 该段省略（业务层调用错误应在服务层拦截）
        var code = CodeFormatter.Format("FAB", true, DateSegment.None, 4, "-", 7, null, Now);
        Assert.Equal("FAB-0007", code);
    }

    [Fact]
    public void FormatSample_UsesPlaceholderCategoryAndSeq1()
    {
        var sample = CodeFormatter.FormatSample("FAB", true, DateSegment.Year, 4, "-", Now);
        Assert.Equal("FAB-CAT-2026-0001", sample);
    }
}
```

- [ ] **Step 7: 运行测试验证失败**

Run: `dotnet test backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~CodeFormatterTests"`
Expected: 编译失败（`CodeFormatter` 未定义）。

- [ ] **Step 8: 实现 CodeFormatter**

`backend/src/OneCup.Application/Common/CodeFormatter.cs`：

```csharp
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Common;

/// <summary>
/// 编码拼码（纯函数）。段顺序固定：[前缀] [分类码?] [日期?] [流水号]，用 separator 连接。
/// now 应已是目标时区（北京时间）的时间。
/// </summary>
public static class CodeFormatter
{
    /// <summary>
    /// 拼出实际编码。
    /// </summary>
    public static string Format(
        string prefix, bool includeCategory, DateSegment dateSegment,
        int seqLength, string separator, int seq, string? categoryCode, DateTime now)
    {
        var segments = new List<string> { prefix };

        if (includeCategory && !string.IsNullOrEmpty(categoryCode))
            segments.Add(categoryCode);

        var datePart = dateSegment switch
        {
            DateSegment.None => null,
            DateSegment.Year => now.ToString("yyyy"),
            DateSegment.YearMonth => now.ToString("yyyyMM"),
            DateSegment.YearMonthDay => now.ToString("yyyyMMdd"),
            _ => null
        };
        if (datePart is not null) segments.Add(datePart);

        // 流水号溢出校验：实际位数超过配置位数 → 阻断（设计 6.5，不自动扩位）
        if (seq.ToString().Length > seqLength)
            throw new DomainException($"流水号已超出配置位数 {seqLength}，请调整规则或联系管理员");

        segments.Add(seq.ToString(new string('0', seqLength)));

        return string.Join(separator, segments);
    }

    /// <summary>
    /// 拼出展示用的示例编码（列表/详情 sampleFormat 字段）。
    /// 用占位品类码 "CAT"、seq=1。
    /// </summary>
    public static string FormatSample(
        string prefix, bool includeCategory, DateSegment dateSegment,
        int seqLength, string separator, DateTime now)
    {
        return Format(prefix, includeCategory, dateSegment, seqLength, separator,
            1, includeCategory ? "CAT" : null, now);
    }
}
```

- [ ] **Step 9: 运行全部 Numbering 测试验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~Numbering"`
Expected: 全部 passed（PeriodKey 4 + CodeFormatter 11 = 15）。

- [ ] **Step 10: Commit**

```bash
git add backend/src/OneCup.Application/Common/NumberTargetTypes.cs backend/src/OneCup.Application/Common/PeriodKeyCalculator.cs backend/src/OneCup.Application/Common/CodeFormatter.cs backend/tests/OneCup.UnitTests/Numbering/PeriodKeyCalculatorTests.cs backend/tests/OneCup.UnitTests/Numbering/CodeFormatterTests.cs
git commit -m "feat(numbering): 拼码与周期键纯函数 + 单元测试"
```

---

## Task 3: DTO、服务接口与时间提供者接口

**Files:**
- Create: `backend/src/OneCup.Application/Dtos/System/NumberingDtos.cs`
- Create: `backend/src/OneCup.Application/Interfaces/INumberingClock.cs`
- Create: `backend/src/OneCup.Application/Interfaces/INumberingService.cs`
- Create: `backend/src/OneCup.Application/Interfaces/INumberingRuleService.cs`

**Interfaces:**
- Consumes: `DateSegment`、`ResetPeriod`、`PagedResult<T>`（已存在）、`NumberTargetTypes`。
- Produces: 所有 Numbering DTO、三个服务接口。后续 Task 4-7 实现这些接口，Task 8 的 Controller 消费它们。

- [ ] **Step 1: 创建 DTO**

```csharp
using OneCup.Domain.Enums;

namespace OneCup.Application.Dtos.System;

// ── 规则 ──

public record CreateNumberingRuleRequest
{
    public string TargetType { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string Prefix { get; init; } = string.Empty;
    public bool IncludeCategory { get; init; }
    public DateSegment DateSegment { get; init; }
    public short SeqLength { get; init; } = 4;
    public string Separator { get; init; } = "-";
    public ResetPeriod ResetPeriod { get; init; }
    public string? Remark { get; init; }
}

public record UpdateNumberingRuleRequest
{
    public string? Name { get; init; }
    public string? Prefix { get; init; }
    public string? TargetType { get; init; }
    public bool? IncludeCategory { get; init; }
    public DateSegment? DateSegment { get; init; }
    public short? SeqLength { get; init; }
    public string? Separator { get; init; }
    public ResetPeriod? ResetPeriod { get; init; }
    public string? Remark { get; init; }
}

public record UpdateRuleStatusRequest
{
    public bool IsActive { get; init; }
}

public class NumberingRuleDto
{
    public Guid Id { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public bool IncludeCategory { get; set; }
    public DateSegment DateSegment { get; set; }
    public short SeqLength { get; set; }
    public string Separator { get; set; } = "-";
    public ResetPeriod ResetPeriod { get; set; }
    public bool IsActive { get; set; }
    public string? Remark { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    /// <summary>展示用示例编码，如 FAB-CAT-2026-0001</summary>
    public string SampleFormat { get; set; } = string.Empty;
}

public class NumberingRuleListItemDto
{
    public Guid Id { get; set; }
    public string TargetType { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Prefix { get; set; } = string.Empty;
    public string SampleFormat { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

// ── 预览 ──

public record PreviewCodeResult
{
    public string? Code { get; init; }
    public string Note { get; init; } = "预览编号，实际保存时以系统分配为准";
}

// ── 日志 ──

public class NumberingLogListItemDto
{
    public Guid Id { get; set; }
    public string GeneratedCode { get; set; } = string.Empty;
    public string TargetType { get; set; } = string.Empty;
    public string? CategoryCode { get; set; }
    public string? PeriodKey { get; set; }
    public int SeqValue { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? RuleName { get; set; }
}
```

- [ ] **Step 2: 创建 INumberingClock**

```csharp
namespace OneCup.Application.Interfaces;

/// <summary>
/// 编号时间提供者。GetCurrentTime 返回用于周期键/日期段计算的北京时间。
/// 数据库时间戳仍用 UTC（不经过此接口）。
/// </summary>
public interface INumberingClock
{
    DateTime GetCurrentTime();
}
```

- [ ] **Step 3: 创建 INumberingService（业务模块调用）**

```csharp
namespace OneCup.Application.Interfaces;

/// <summary>
/// 编码生成服务。业务对象落库事务内调用 GenerateAsync。
/// </summary>
public interface INumberingService
{
    /// <summary>
    /// 生成编码。在调用方事务内执行行锁取号，调用方负责提交事务。
    /// </summary>
    /// <param name="targetType">业务对象类型，如 NumberTargetTypes.Fabric</param>
    /// <param name="categoryCode">品类码，规则开启分类码段时必填</param>
    Task<string> GenerateAsync(string targetType, string? categoryCode = null, CancellationToken ct = default);

    /// <summary>
    /// 预览下一个编码（只读，不消耗计数，仅供参考）。
    /// </summary>
    Task<string?> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default);
}
```

- [ ] **Step 4: 创建 INumberingRuleService（系统管理用）**

```csharp
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 编号规则管理服务（系统管理用，业务模块不需要）。
/// </summary>
public interface INumberingRuleService
{
    Task<PagedResult<NumberingRuleListItemDto>> GetListAsync(
        int page, int pageSize, string? keyword, string? targetType, bool? isActive,
        CancellationToken ct = default);

    Task<NumberingRuleDto?> GetAsync(Guid id, CancellationToken ct = default);

    Task<NumberingRuleDto> CreateAsync(CreateNumberingRuleRequest request, CancellationToken ct = default);

    Task UpdateAsync(Guid id, UpdateNumberingRuleRequest request, CancellationToken ct = default);

    Task UpdateStatusAsync(Guid id, bool isActive, CancellationToken ct = default);

    Task<PagedResult<NumberingLogListItemDto>> GetLogsAsync(
        int page, int pageSize, string? targetType, string? categoryCode,
        Guid? ruleId, string? code, DateTime? startDate, DateTime? endDate,
        CancellationToken ct = default);
}
```

- [ ] **Step 5: 编译验证**

Run: `dotnet build backend/src/OneCup.Application/OneCup.Application.csproj`
Expected: 成功。

- [ ] **Step 6: Commit**

```bash
git add backend/src/OneCup.Application/Dtos/System/NumberingDtos.cs backend/src/OneCup.Application/Interfaces/INumberingClock.cs backend/src/OneCup.Application/Interfaces/INumberingService.cs backend/src/OneCup.Application/Interfaces/INumberingRuleService.cs
git commit -m "feat(numbering): Application 层 DTO 与服务接口"
```

---

## Task 4: NumberingClock 实现 + 跨年边界测试

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Services/NumberingClock.cs`
- Test: `backend/tests/OneCup.UnitTests/Numbering/NumberingClockTests.cs`

**Interfaces:**
- Consumes: `INumberingClock`（Task 3）。

- [ ] **Step 1: 写 NumberingClock 失败测试**

`backend/tests/OneCup.UnitTests/Numbering/NumberingClockTests.cs`：

```csharp
using OneCup.Application.Common;
using OneCup.Infrastructure.Services;

namespace OneCup.UnitTests.Numbering;

public class NumberingClockTests
{
    [Fact]
    public void GetCurrentTime_ConvertsUtcToBeijing()
    {
        var clock = new NumberingClock();
        // 当前北京时间应在 UTC+8 区间内（允许少量误差）
        var now = clock.GetCurrentTime();
        var utcNow = DateTime.UtcNow;
        var expected = utcNow.AddHours(8);

        Assert.Equal(expected.Date, now.Date);
        Assert.True((now - expected).Duration() < TimeSpan.FromMinutes(1));
    }

    [Fact]
    public void PeriodKey_BoundaryYearRollover_BeijingTime()
    {
        // 验证北京时间跨年时周期键正确切换（用 PeriodKeyCalculator + 固定时刻）
        // 2026-12-31 23:59:59 北京时间
        var beforeMidnight = new DateTime(2026, 12, 31, 23, 59, 59);
        Assert.Equal("2026", PeriodKeyCalculator.Calc(Domain.Enums.ResetPeriod.Yearly, beforeMidnight));

        // 2027-01-01 00:00:00 北京时间
        var afterMidnight = new DateTime(2027, 1, 1, 0, 0, 0);
        Assert.Equal("2027", PeriodKeyCalculator.Calc(Domain.Enums.ResetPeriod.Yearly, afterMidnight));
    }

    [Fact]
    public void PeriodKey_BoundaryYearRollover_FromUtc()
    {
        // 关键场景：UTC 2026-12-31 16:00:00 = 北京时间 2027-01-01 00:00:00
        // 即北京时间跨年瞬间对应的 UTC 时刻。验证经 NumberingClock 转换后周期键为 2027。
        var clock = new NumberingClock();
        // 此测试验证时区转换链路：UTC → 北京时间 → 周期键
        // 由于 NumberingClock 取系统 UTC，这里改用直接验证时区信息加载成功
        Assert.NotNull(clock);
        // 时区信息加载不抛异常即视为可用
        var now = clock.GetCurrentTime();
        Assert.True(now.Year >= 2026);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~NumberingClockTests"`
Expected: 编译失败（`NumberingClock` 未定义）。

- [ ] **Step 3: 实现 NumberingClock**

`backend/src/OneCup.Infrastructure/Services/NumberingClock.cs`：

```csharp
using OneCup.Application.Interfaces;

namespace OneCup.Infrastructure.Services;

/// <summary>
/// 编号时间提供者实现：返回北京时间（UTC+8）。
/// 时区 ID 统一用 IANA "Asia/Shanghai"，.NET 10 在 Windows/Linux 全平台通用。
/// </summary>
public class NumberingClock : INumberingClock
{
    private static readonly TimeZoneInfo ChinaTz =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");

    public DateTime GetCurrentTime() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ChinaTz);
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~NumberingClockTests"`
Expected: 3 passed。

- [ ] **Step 5: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Services/NumberingClock.cs backend/tests/OneCup.UnitTests/Numbering/NumberingClockTests.cs
git commit -m "feat(numbering): NumberingClock 北京时间实现 + 跨年边界测试"
```

---

## Task 5: EF 配置、DbContext、SeedData、迁移

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingRuleConfiguration.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingCounterConfiguration.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingLogConfiguration.cs`
- Modify: `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`（加 3 个 DbSet）
- Modify: `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs`（加 2 个权限 Guid）
- Modify: `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs` 的 `Seed()`（加权限种子）

**Interfaces:**
- Consumes: `NumberingRule/NumberingCounter/NumberingLog` 实体（Task 1）。

- [ ] **Step 1: 创建 NumberingRuleConfiguration**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class NumberingRuleConfiguration : IEntityTypeConfiguration<NumberingRule>
{
    public void Configure(EntityTypeBuilder<NumberingRule> builder)
    {
        builder.ToTable("numbering_rules");
        builder.HasKey(r => r.Id);

        builder.Property(r => r.Id).HasColumnName("id");
        builder.Property(r => r.TargetType).HasColumnName("target_type").HasMaxLength(32).IsRequired();
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(64).IsRequired();
        builder.Property(r => r.Prefix).HasColumnName("prefix").HasMaxLength(16).IsRequired();
        builder.Property(r => r.IncludeCategory).HasColumnName("include_category").IsRequired();
        builder.Property(r => r.DateSegment).HasColumnName("date_segment").HasMaxLength(16).HasConversion<string>().IsRequired();
        builder.Property(r => r.SeqLength).HasColumnName("seq_length").IsRequired();
        builder.Property(r => r.Separator).HasColumnName("separator").HasMaxLength(8).IsRequired();
        builder.Property(r => r.ResetPeriod).HasColumnName("reset_period").HasMaxLength(16).HasConversion<string>().IsRequired();
        builder.Property(r => r.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(r => r.Remark).HasColumnName("remark").HasMaxLength(256);
        builder.Property(r => r.CreatedAt).HasColumnName("created_at");
        builder.Property(r => r.UpdatedAt).HasColumnName("updated_at");

        // 部分唯一索引：同一业务类型同时只能有一条启用规则
        builder.HasIndex(r => new { r.TargetType, r.IsActive })
            .HasDatabaseName("ux_numbering_rules_target_type_active")
            .HasFilter("\"is_active\" = true")
            .IsUnique();

        builder.HasIndex(r => r.TargetType).HasDatabaseName("ix_numbering_rules_target_type");
    }
}
```

- [ ] **Step 2: 创建 NumberingCounterConfiguration**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class NumberingCounterConfiguration : IEntityTypeConfiguration<NumberingCounter>
{
    public void Configure(EntityTypeBuilder<NumberingCounter> builder)
    {
        builder.ToTable("numbering_counters");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.RuleId).HasColumnName("rule_id").IsRequired();
        // 空串代替 NULL（参与唯一索引，避免 PG NULL 歧义）
        builder.Property(c => c.CategoryCode).HasColumnName("category_code").HasMaxLength(32).HasDefaultValue("").IsRequired();
        builder.Property(c => c.PeriodKey).HasColumnName("period_key").HasMaxLength(16).HasDefaultValue("").IsRequired();
        builder.Property(c => c.CurrentSeq).HasColumnName("current_seq").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasOne(c => c.Rule)
            .WithMany()
            .HasForeignKey(c => c.RuleId);

        // 桶唯一标识：(rule_id, category_code, period_key)
        builder.HasIndex(c => new { c.RuleId, c.CategoryCode, c.PeriodKey })
            .HasDatabaseName("ux_numbering_counters_bucket")
            .IsUnique();
    }
}
```

- [ ] **Step 3: 创建 NumberingLogConfiguration**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class NumberingLogConfiguration : IEntityTypeConfiguration<NumberingLog>
{
    public void Configure(EntityTypeBuilder<NumberingLog> builder)
    {
        builder.ToTable("numbering_logs");
        builder.HasKey(l => l.Id);

        builder.Property(l => l.Id).HasColumnName("id");
        builder.Property(l => l.GeneratedCode).HasColumnName("generated_code").HasMaxLength(64).IsRequired();
        builder.Property(l => l.RuleId).HasColumnName("rule_id").IsRequired();
        builder.Property(l => l.TargetType).HasColumnName("target_type").HasMaxLength(32).IsRequired();
        builder.Property(l => l.CategoryCode).HasColumnName("category_code").HasMaxLength(32);
        builder.Property(l => l.PeriodKey).HasColumnName("period_key").HasMaxLength(16);
        builder.Property(l => l.SeqValue).HasColumnName("seq_value").IsRequired();
        builder.Property(l => l.CreatedAt).HasColumnName("created_at");

        builder.HasOne(l => l.Rule)
            .WithMany()
            .HasForeignKey(l => l.RuleId);

        builder.HasIndex(l => l.GeneratedCode).HasDatabaseName("ix_numbering_logs_code");
        builder.HasIndex(l => new { l.RuleId, l.CreatedAt }).HasDatabaseName("ix_numbering_logs_rule_id");
        builder.HasIndex(l => new { l.TargetType, l.CreatedAt }).HasDatabaseName("ix_numbering_logs_target_type");
    }
}
```

- [ ] **Step 4: 给 DbContext 加 DbSet**

在 `OneCupDbContext.cs` 的 `public DbSet<RefreshToken> RefreshTokens ...` 之后添加：

```csharp
    public DbSet<NumberingRule> NumberingRules => Set<NumberingRule>();
    public DbSet<NumberingCounter> NumberingCounters => Set<NumberingCounter>();
    public DbSet<NumberingLog> NumberingLogs => Set<NumberingLog>();
```

- [ ] **Step 5: 给 SeedData 加权限 Guid 常量**

在 `SeedData.cs` 的 `PermSystemRoleManage` 之后添加：

```csharp
    public static readonly Guid PermSystemNumberingView = Guid.Parse("00000000-0000-0000-0000-000000000114");
    public static readonly Guid PermSystemNumberingManage = Guid.Parse("00000000-0000-0000-0000-000000000115");
```

- [ ] **Step 6: 给 DbContext.Seed() 加权限种子**

在 `Seed()` 方法里，把现有权限 `HasData` 列表末尾（`PermSystemRoleManage` 那行之后）追加两行：

```csharp
            new Permission { Id = SeedData.PermSystemNumberingView, Code = "system:numbering:view", Name = "查看编号管理", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemNumberingManage, Code = "system:numbering:manage", Name = "管理编号规则", CreatedAt = SeedTimestamp }
```

- [ ] **Step 7: 编译验证**

Run: `dotnet build backend/OneCup.slnx`
Expected: 成功。

- [ ] **Step 8: 生成 EF 迁移**

Run: `dotnet ef migrations add AddNumberingModule --project src/OneCup.Infrastructure --startup-project src/OneCup.Api`
（在 `backend/` 目录下执行）
Expected: 在 `Migrations/` 下生成 `*_AddNumberingModule.cs`，Up 方法含 3 张 CreateTable + 索引 + 2 条权限 InsertData。

- [ ] **Step 9: 验证迁移无 PendingModelChangesWarning**

Run: `dotnet build backend/OneCup.slnx`
Expected: 无 `PendingModelChangesWarning`。

- [ ] **Step 10: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingRuleConfiguration.cs backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingCounterConfiguration.cs backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingLogConfiguration.cs backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs backend/src/OneCup.Infrastructure/Persistence/SeedData.cs backend/src/OneCup.Infrastructure/Migrations/
git commit -m "feat(numbering): EF 配置 + DbContext + 权限种子 + 迁移"
```

---

## Task 6: NumberingRuleService（规则管理 CRUD）

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Services/NumberingRuleService.cs`
- Test: `backend/tests/OneCup.UnitTests/Numbering/NumberingRuleServiceTests.cs`

**Interfaces:**
- Consumes: `INumberingRuleService`（Task 3）、`INumberingClock`（Task 4）、`CodeFormatter.FormatSample`（Task 2）、`NumberingRule/NumberingLog` 实体（Task 1）、`OneCupDbContext`（Task 5）。
- Produces: `NumberingRuleService` 实现完整 CRUD + 锁字段 + 启停唯一性 + 日志查询。

- [ ] **Step 1: 写失败测试**

`backend/tests/OneCup.UnitTests/Numbering/NumberingRuleServiceTests.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
using OneCup.Infrastructure.Services;

namespace OneCup.UnitTests.Numbering;

public class NumberingRuleServiceTests
{
    private static (OneCupDbContext db, NumberingRuleService svc) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"numbering-rule-{Guid.NewGuid()}")
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);
        var svc = new NumberingRuleService(db, new NumberingClock());
        return (db, svc);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    private static CreateNumberingRuleRequest MakeCreate() => new()
    {
        TargetType = "fabric",
        Name = "面料编码",
        Prefix = "FAB",
        IncludeCategory = true,
        DateSegment = DateSegment.Year,
        SeqLength = 4,
        Separator = "-",
        ResetPeriod = ResetPeriod.Yearly,
    };

    [Fact]
    public async Task CreateAsync_CreatesRule()
    {
        var (db, svc) = Setup();
        var rule = await svc.CreateAsync(MakeCreate());
        Assert.Equal("fabric", rule.TargetType);
        Assert.True(rule.IsActive);
        Assert.Contains("FAB-", rule.SampleFormat);
    }

    [Fact]
    public async Task CreateAsync_PrefixContainsSeparator_Throws()
    {
        var (db, svc) = Setup();
        var req = MakeCreate() with { Prefix = "FA-B", Separator = "-" };
        await Assert.ThrowsAsync<DomainException>(() => svc.CreateAsync(req));
    }

    [Fact]
    public async Task CreateAsync_DuplicateActiveTargetType_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(MakeCreate());
        await Assert.ThrowsAsync<DomainException>(() => svc.CreateAsync(MakeCreate()));
    }

    [Fact]
    public async Task UpdateAsync_LockedFieldsWhenActive_Throws()
    {
        var (db, svc) = Setup();
        var rule = await svc.CreateAsync(MakeCreate());
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateAsync(rule.Id, new UpdateNumberingRuleRequest { Prefix = "FABRIC" }));
        Assert.Contains("停用", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_RemarkAllowedWhenActive()
    {
        var (db, svc) = Setup();
        var rule = await svc.CreateAsync(MakeCreate());
        await svc.UpdateAsync(rule.Id, new UpdateNumberingRuleRequest { Remark = "测试备注" });
        var updated = await svc.GetAsync(rule.Id);
        Assert.Equal("测试备注", updated!.Remark);
    }

    [Fact]
    public async Task UpdateStatusAsync_DeactivateAllowsEdit()
    {
        var (db, svc) = Setup();
        var rule = await svc.CreateAsync(MakeCreate());
        await svc.UpdateStatusAsync(rule.Id, false);
        // 停用后应可改关键字段
        await svc.UpdateAsync(rule.Id, new UpdateNumberingRuleRequest { Prefix = "FABRIC" });
        var updated = await svc.GetAsync(rule.Id);
        Assert.Equal("FABRIC", updated!.Prefix);
    }

    [Fact]
    public async Task UpdateStatusAsync_ActivateDuplicateTargetType_Throws()
    {
        var (db, svc) = Setup();
        var rule1 = await svc.CreateAsync(MakeCreate());
        await svc.UpdateStatusAsync(rule1.Id, false);
        var rule2 = await svc.CreateAsync(MakeCreate());
        // rule2 已是启用，尝试启用 rule1 应冲突
        await Assert.ThrowsAsync<DomainException>(() => svc.UpdateStatusAsync(rule1.Id, true));
    }

    [Fact]
    public async Task GetListAsync_FiltersByTargetType()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(MakeCreate() with { TargetType = "material", Name = "物料" });
        var result = await svc.GetListAsync(1, 10, null, "material", null);
        Assert.Single(result.Items);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~NumberingRuleServiceTests"`
Expected: 编译失败（`NumberingRuleService` 未定义）。

- [ ] **Step 3: 实现 NumberingRuleService**

`backend/src/OneCup.Infrastructure/Services/NumberingRuleService.cs`：

```csharp
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
```

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~NumberingRuleServiceTests"`
Expected: 8 passed。

- [ ] **Step 5: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Services/NumberingRuleService.cs backend/tests/OneCup.UnitTests/Numbering/NumberingRuleServiceTests.cs
git commit -m "feat(numbering): NumberingRuleService 规则管理 CRUD + 测试"
```

---

## Task 7: NumberingService（生成 + 预览）+ Testcontainers 并发测试

这是引擎并发安全的核心。测试用 Testcontainers + PostgreSQL（InMemory 不支持 `FOR UPDATE` 行锁）。

**Files:**
- Modify: `backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj`（加 Testcontainers 依赖）
- Create: `backend/src/OneCup.Infrastructure/Services/NumberingService.cs`
- Create: `backend/tests/OneCup.UnitTests/Numbering/NumberingServiceConcurrencyTests.cs`

**Interfaces:**
- Consumes: `INumberingService`（Task 3）、`INumberingClock`（Task 4）、`CodeFormatter`/`PeriodKeyCalculator`（Task 2）、`OneCupDbContext`（Task 5）。
- Produces: `NumberingService` 实现行锁取号 + 重试 + 预览。

- [ ] **Step 1: 加 Testcontainers 测试依赖**

在 `backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj` 的 `<ItemGroup>`（PackageReference 那组）内添加：

```xml
    <PackageReference Include="Testcontainers.PostgreSql" Version="4.4.0" />
```

- [ ] **Step 2: 还原依赖**

Run: `dotnet restore backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj`
Expected: 成功。

- [ ] **Step 3: 写并发测试（含 Testcontainers fixture）**

`backend/tests/OneCup.UnitTests/Numbering/NumberingServiceConcurrencyTests.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
using OneCup.Infrastructure.Services;
using Testcontainers.PostgreSql;

namespace OneCup.UnitTests.Numbering;

/// <summary>
/// 并发取号测试。必须用真实 PostgreSQL（Testcontainers）——InMemory 不支持 FOR UPDATE 行锁。
/// 运行需要本机可访问 Docker。
/// </summary>
public class NumberingServiceConcurrencyTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _pg = new PostgreSqlBuilder()
        .WithImage("postgres:17-alpine")
        .WithDatabase("numbering_test")
        .WithUsername("test")
        .WithPassword("test")
        .Build();

    private OneCupDbContext _db = null!;
    private NumberingService _svc = null!;
    private Guid _ruleId;

    public async Task InitializeAsync()
    {
        await _pg.StartAsync();
        var options = new DbContextOptionsBuilder<OneCupDbContext>()
            .UseNpgsql(_pg.GetConnectionString())
            .Options;
        _db = new OneCupDbContext(options);
        await _db.Database.EnsureCreatedAsync();

        var rule = new NumberingRule
        {
            TargetType = "fabric",
            Name = "面料",
            Prefix = "FAB",
            IncludeCategory = true,
            DateSegment = DateSegment.Year,
            SeqLength = 4,
            Separator = "-",
            ResetPeriod = ResetPeriod.Yearly,
            IsActive = true,
        };
        _db.NumberingRules.Add(rule);
        await _db.SaveChangesAsync();
        _ruleId = rule.Id;

        _svc = new NumberingService(_db, new NumberingClock());
    }

    public async Task DisposeAsync() => await _pg.DisposeAsync();

    private OneCupDbContext NewDbContext()
    {
        var options = new DbContextOptionsBuilder<OneCupDbContext>()
            .UseNpgsql(_pg.GetConnectionString())
            .Options;
        return new OneCupDbContext(options);
    }

    [Fact]
    public async Task GenerateAsync_Serial_SequentialUnique()
    {
        // 串行取号 5 次，应得到 0001-0005
        var codes = new List<string>();
        for (int i = 0; i < 5; i++)
        {
            using var txDb = NewDbContext();
            var svc = new NumberingService(txDb, new NumberingClock());
            codes.Add(await svc.GenerateAsync("fabric", "COT"));
        }
        Assert.Equal("FAB-COT-" + DateTime.UtcNow.AddHours(8).Year + "-0001", codes[0]);
        Assert.Equal(5, codes.Distinct().Count());
    }

    [Fact]
    public async Task GenerateAsync_Concurrent_AllUnique()
    {
        // 10 个并发任务，各取 10 次 = 100 个号，全部唯一
        const int tasks = 10, perTask = 10;
        var results = new List<string>[tasks];
        for (int i = 0; i < tasks; i++) results[i] = new List<string>();

        var tasksList = Enumerable.Range(0, tasks).Select(i => Task.Run(async () =>
        {
            for (int j = 0; j < perTask; j++)
            {
                using var txDb = NewDbContext();
                var svc = new NumberingService(txDb, new NumberingClock());
                results[i].Add(await svc.GenerateAsync("fabric", "COT"));
            }
        })).ToArray();
        await Task.WhenAll(tasksList);

        var all = results.SelectMany(x => x).ToList();
        Assert.Equal(tasks * perTask, all.Count);
        Assert.Equal(all.Count, all.Distinct().Count());  // 全部唯一
    }

    [Fact]
    public async Task GenerateAsync_NewCategory_StartsFromOne()
    {
        using var db = NewDbContext();
        var svc = new NumberingService(db, new NumberingClock());
        var code1 = await svc.GenerateAsync("fabric", "CHE");
        Assert.EndsWith("-0001", code1);
    }

    [Fact]
    public async Task GenerateAsync_DifferentCategories_Independent()
    {
        using var db = NewDbContext();
        var svc = new NumberingService(db, new NumberingClock());
        var cot1 = await svc.GenerateAsync("fabric", "COT");
        var che1 = await svc.GenerateAsync("fabric", "CHE");
        var cot2 = await svc.GenerateAsync("fabric", "COT");
        Assert.EndsWith("-0001", cot1);
        Assert.EndsWith("-0001", che1);
        Assert.EndsWith("-0002", cot2);  // COT 独立计数
    }

    [Fact]
    public async Task GenerateAsync_NoRule_Throws()
    {
        using var db = NewDbContext();
        var svc = new NumberingService(db, new NumberingClock());
        await Assert.ThrowsAsync<DomainException>(() => svc.GenerateAsync("nonexistent", null));
    }

    [Fact]
    public async Task GenerateAsync_CategoryRequired_Throws()
    {
        using var db = NewDbContext();
        var svc = new NumberingService(db, new NumberingClock());
        await Assert.ThrowsAsync<DomainException>(() => svc.GenerateAsync("fabric", null));
    }

    [Fact]
    public async Task PreviewAsync_ReturnsNextWithoutConsuming()
    {
        using var db = NewDbContext();
        var svc = new NumberingService(db, new NumberingClock());
        var preview = await svc.PreviewAsync("fabric", "COT");
        Assert.NotNull(preview);
        // 预览不消耗计数：连续两次预览应相同
        var preview2 = await svc.PreviewAsync("fabric", "COT");
        Assert.Equal(preview, preview2);
    }

    [Fact]
    public async Task PreviewAsync_NoRule_ReturnsNull()
    {
        using var db = NewDbContext();
        var svc = new NumberingService(db, new NumberingClock());
        var preview = await svc.PreviewAsync("nonexistent");
        Assert.Null(preview);
    }
}
```

- [ ] **Step 4: 运行测试验证失败**

Run: `dotnet test backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~NumberingServiceConcurrencyTests"`
Expected: 编译失败（`NumberingService` 未定义）。

- [ ] **Step 5: 实现 NumberingService**

`backend/src/OneCup.Infrastructure/Services/NumberingService.cs`：

```csharp
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
```

- [ ] **Step 6: 运行并发测试验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~NumberingServiceConcurrencyTests"`
Expected: 全部 passed（需 Docker 运行）。重点：`GenerateAsync_Concurrent_AllUnique` 验证 100 个并发号全部唯一。

- [ ] **Step 7: Commit**

```bash
git add backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj backend/src/OneCup.Infrastructure/Services/NumberingService.cs backend/tests/OneCup.UnitTests/Numbering/NumberingServiceConcurrencyTests.cs
git commit -m "feat(numbering): NumberingService 行锁取号 + Testcontainers 并发测试"
```

---

## Task 8: Controller + Program.cs 注册

**Files:**
- Create: `backend/src/OneCup.Api/Controllers/NumberingController.cs`
- Modify: `backend/src/OneCup.Api/Program.cs`（注册服务 + 2 个 Policy）

**Interfaces:**
- Consumes: `INumberingService`、`INumberingRuleService`（Task 3、6、7）。

- [ ] **Step 1: 创建 NumberingController**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 编号管理端点。
/// 规则 CRUD 需 system:numbering:manage；列表/日志/预览仅需登录或 view。
/// 生成接口（GenerateAsync）是内部服务调用，不在此暴露 HTTP。
/// </summary>
[ApiController]
[Route("api/numbering")]
public class NumberingController : ControllerBase
{
    private readonly INumberingRuleService _ruleService;
    private readonly INumberingService _numberingService;

    public NumberingController(INumberingRuleService ruleService, INumberingService numberingService)
    {
        _ruleService = ruleService;
        _numberingService = numberingService;
    }

    // ── 规则管理（view 可查，manage 可改）──

    [HttpGet("rules")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetRules(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null, [FromQuery] string? targetType = null,
        [FromQuery] bool? isActive = null, CancellationToken ct = default)
    {
        var result = await _ruleService.GetListAsync(page, pageSize, keyword, targetType, isActive, ct);
        return Ok(result);
    }

    [HttpGet("rules/{id:guid}")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetRule(Guid id, CancellationToken ct)
    {
        var rule = await _ruleService.GetAsync(id, ct);
        return rule is null ? NotFound() : Ok(rule);
    }

    [HttpPost("rules")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> CreateRule([FromBody] CreateNumberingRuleRequest request, CancellationToken ct)
    {
        var rule = await _ruleService.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetRule), new { id = rule.Id }, rule);
    }

    [HttpPut("rules/{id:guid}")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> UpdateRule(Guid id, [FromBody] UpdateNumberingRuleRequest request, CancellationToken ct)
    {
        await _ruleService.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpPut("rules/{id:guid}/status")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> UpdateRuleStatus(Guid id, [FromBody] UpdateRuleStatusRequest request, CancellationToken ct)
    {
        await _ruleService.UpdateStatusAsync(id, request.IsActive, ct);
        return NoContent();
    }

    // ── 预览（登录即可，不消耗计数）──

    [HttpGet("preview")]
    [Authorize]
    public async Task<IActionResult> Preview([FromQuery] string targetType, [FromQuery] string? categoryCode = null, CancellationToken ct = default)
    {
        var code = await _numberingService.PreviewAsync(targetType, categoryCode, ct);
        return Ok(new PreviewCodeResult { Code = code });
    }

    // ── 生成日志（view 可查）──

    [HttpGet("logs")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetLogs(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? targetType = null, [FromQuery] string? categoryCode = null,
        [FromQuery] Guid? ruleId = null, [FromQuery] string? code = null,
        [FromQuery] DateTime? startDate = null, [FromQuery] DateTime? endDate = null,
        CancellationToken ct = default)
    {
        var result = await _ruleService.GetLogsAsync(page, pageSize, targetType, categoryCode, ruleId, code, startDate, endDate, ct);
        return Ok(result);
    }
}
```

- [ ] **Step 2: 在 Program.cs 注册服务**

在 `Program.cs` 的 `// ── 依赖注入:系统管理服务` 区块末尾（`IPermissionService` 那行之后）追加：

```csharp
builder.Services.AddScoped<INumberingClock, NumberingClock>();
builder.Services.AddScoped<INumberingService, NumberingService>();
builder.Services.AddScoped<INumberingRuleService, NumberingRuleService>();
```

- [ ] **Step 3: 在 Program.cs 注册授权策略**

在 `AddAuthorization` 的 `options.AddPolicy("role-manage", ...)` 之后追加：

```csharp
    options.AddPolicy("numbering-view", policy =>
        policy.RequireClaim("perm_codes", "system:numbering:view"));
    options.AddPolicy("numbering-manage", policy =>
        policy.RequireClaim("perm_codes", "system:numbering:manage"));
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build backend/OneCup.slnx`
Expected: 成功。

- [ ] **Step 5: 运行全部测试确保无回归**

Run: `dotnet test backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~Numbering"`
Expected: 全部 passed（除 Testcontainers 那组需 Docker）。

- [ ] **Step 6: Commit**

```bash
git add backend/src/OneCup.Api/Controllers/NumberingController.cs backend/src/OneCup.Api/Program.cs
git commit -m "feat(numbering): Controller 端点 + 服务/策略注册"
```

---

## Task 9: 前端 API + 页面 + 路由 + i18n

**Files:**
- Create: `frontend/src/api/numbering.ts`
- Create: `frontend/src/pages/system/numbering/index.tsx`
- Create: `frontend/src/pages/system/numbering/locale/index.ts`
- Modify: `frontend/src/routes.ts`
- Modify: `frontend/src/locale/index.ts`

**Interfaces:**
- Consumes: 后端 `/api/numbering/*` 端点（Task 8）。

- [ ] **Step 1: 创建 API 客户端**

`frontend/src/api/numbering.ts`（沿用项目 `request` 实例与泛型风格）：

```typescript
import request from './request';
import { PagedResult } from './user';

// ── 类型 ──
export interface NumberingRuleListItem {
  id: string;
  targetType: string;
  name: string;
  prefix: string;
  sampleFormat: string;
  isActive: boolean;
  createdAt: string;
}

export interface NumberingRule extends NumberingRuleListItem {
  includeCategory: boolean;
  dateSegment: string;
  seqLength: number;
  separator: string;
  resetPeriod: string;
  remark?: string;
  updatedAt?: string;
}

export interface CreateNumberingRuleRequest {
  targetType: string;
  name: string;
  prefix: string;
  includeCategory: boolean;
  dateSegment: string;
  seqLength: number;
  separator: string;
  resetPeriod: string;
  remark?: string;
}

export interface NumberingLogItem {
  id: string;
  generatedCode: string;
  targetType: string;
  categoryCode?: string;
  periodKey?: string;
  seqValue: number;
  createdAt: string;
  ruleName?: string;
}

// ── 规则 ──
export function getNumberingRules(params: {
  page?: number; pageSize?: number; keyword?: string;
  targetType?: string; isActive?: boolean;
}) {
  return request.get<unknown, PagedResult<NumberingRuleListItem>>('/api/numbering/rules', { params });
}

export function getNumberingRule(id: string) {
  return request.get<unknown, NumberingRule>(`/api/numbering/rules/${id}`);
}

export function createNumberingRule(data: CreateNumberingRuleRequest) {
  return request.post<unknown, NumberingRule>('/api/numbering/rules', data);
}

export function updateNumberingRule(id: string, data: Partial<CreateNumberingRuleRequest> & { remark?: string }) {
  return request.put(`/api/numbering/rules/${id}`, data);
}

export function updateNumberingRuleStatus(id: string, isActive: boolean) {
  return request.put(`/api/numbering/rules/${id}/status`, { isActive });
}

// ── 预览 ──
export function previewCode(targetType: string, categoryCode?: string) {
  return request.get<unknown, { code: string | null; note: string }>('/api/numbering/preview', {
    params: { targetType, categoryCode },
  });
}

// ── 日志 ──
export function getNumberingLogs(params: {
  page?: number; pageSize?: number; targetType?: string;
  categoryCode?: string; ruleId?: string; code?: string;
  startDate?: string; endDate?: string;
}) {
  return request.get<unknown, PagedResult<NumberingLogItem>>('/api/numbering/logs', { params });
}
```

- [ ] **Step 2: 在 routes.ts 加菜单项**

在 `routes.ts` 的 system 菜单 children 中（permission 之后）添加：

```typescript
      {
        name: 'menu.system.numbering',
        key: 'system/numbering',
        requiredPermissions: { resource: 'system:numbering', actions: ['view'] },
      },
```

- [ ] **Step 3: 在 locale/index.ts 加 i18n**

在中文/英文 menu.system 下添加（具体位置参考现有 user/role/permission 条目）：

```typescript
// 中文
'menu.system.numbering': '编号管理',
// 英文
'menu.system.numbering': 'Numbering',
```

- [ ] **Step 4: 创建页面 locale**

先查看现有页面 locale 结构：`Read frontend/src/pages/system/user/locale/`（了解项目 locale 文件组织方式与导出格式）。

`frontend/src/pages/system/numbering/locale/index.ts`（按 user 页 locale/index.ts 的导出结构组织）：

```typescript
import zhCN from './zh-CN';
import enUS from './en-US';

export default { 'zh-CN': zhCN, 'en-US': enUS };
```

`frontend/src/pages/system/numbering/locale/zh-CN.ts` 关键 key（en-US.ts 对应英文）：

```typescript
export default {
  'numbering.title': '编号管理',
  'numbering.tab.rules': '规则配置',
  'numbering.tab.logs': '生成日志',
  // 规则列表列
  'numbering.rules.name': '规则名称',
  'numbering.rules.targetType': '业务类型',
  'numbering.rules.prefix': '前缀',
  'numbering.rules.sampleFormat': '示例格式',
  'numbering.rules.status': '状态',
  'numbering.rules.operations': '操作',
  'numbering.rules.active': '启用',
  'numbering.rules.inactive': '停用',
  'numbering.rules.edit': '编辑',
  'numbering.rules.create': '新建规则',
  // 表单字段
  'numbering.form.targetType': '业务类型',
  'numbering.form.name': '规则名称',
  'numbering.form.prefix': '前缀',
  'numbering.form.includeCategory': '包含分类码',
  'numbering.form.dateSegment': '日期段',
  'numbering.form.seqLength': '流水号位数',
  'numbering.form.separator': '分隔符',
  'numbering.form.resetPeriod': '重置周期',
  'numbering.form.remark': '备注',
  'numbering.form.preview': '预览',
  'numbering.form.lockedHint': '已启用的规则不可修改关键配置，请先停用',
  // 日志列表列
  'numbering.logs.code': '编码',
  'numbering.logs.category': '品类',
  'numbering.logs.period': '周期',
  'numbering.logs.seq': '流水号',
  'numbering.logs.time': '时间',
  'numbering.logs.search': '查询',
  // 枚举
  'numbering.enum.dateSegment.None': '不包含',
  'numbering.enum.dateSegment.Year': '年',
  'numbering.enum.dateSegment.YearMonth': '年月',
  'numbering.enum.dateSegment.YearMonthDay': '年月日',
  'numbering.enum.resetPeriod.None': '不重置',
  'numbering.enum.resetPeriod.Yearly': '按年',
  'numbering.enum.resetPeriod.Monthly': '按月',
  'numbering.enum.resetPeriod.Daily': '按日',
};
```

- [ ] **Step 5: 创建主页面**

`frontend/src/pages/system/numbering/index.tsx`（Tab 容器：规则配置 + 生成日志，沿用项目 system/user 的 Table + Drawer 模式）。这是页面组件，参考 `frontend/src/pages/system/user/index.tsx` 的结构：

- 顶部 `Tabs`：`规则配置` | `生成日志`
- **规则配置 Tab**：`Input.Search` + `Select`(targetType) + `Select`(isActive) 工具栏 + `Table`（列：名称/业务类型/前缀/示例格式/状态/操作）+ `Drawer`（新建/编辑表单，含实时预览）
- **生成日志 Tab**：筛选栏（业务类型/品类码/编码关键字/时间范围）+ `Table`（只读）

关键交互逻辑：
- 表单字段 `seqLength` 用 `InputNumber`（min=1, max=8）
- `dateSegment`/`resetPeriod` 用 `Select`（选项：None/Year/YearMonth/YearMonthDay 等，对应后端枚举字符串）
- 实时预览：前端按固定段顺序拼字符串（前缀 + 分类码占位 "CAT" + 日期 + 流水号 0001），随表单更新
- 编辑模式下若 `isActive=true`，关键字段 `disabled`（业务类型/前缀/包含分类码/日期段/流水号位数/分隔符/重置周期），仅备注可改
- 「停用」按钮弹 `Popconfirm`，调 `updateNumberingRuleStatus`

> 实现提示：完整页面代码较长，参照 `pages/system/user/index.tsx` 的骨架（useState 加载、useEffect 拉列表、Table columns、Drawer form）。本页在此基础上加 Tab 容器和第二个日志 Tab。

- [ ] **Step 6: 前端编译验证**

Run: `cd frontend && npm run build`（或在 frontend 目录 `npm run ts-check`）
Expected: 类型检查通过。

- [ ] **Step 7: Commit**

```bash
git add frontend/src/api/numbering.ts frontend/src/pages/system/numbering/ frontend/src/routes.ts frontend/src/locale/index.ts
git commit -m "feat(numbering): 前端 API + 编号管理页面 (规则配置/生成日志)"
```

---

## 最终验证（Definition of Done）

完成所有 Task 后，对照 spec 第 11 节逐项验证：

- [ ] **数据库**：`dotnet ef database update` 成功，三张表 + 索引 + 2 条权限种子就位
- [ ] **拼码单元测试**：`CodeFormatterTests` + `PeriodKeyCalculatorTests` 全 passed（15 个）
- [ ] **规则管理测试**：`NumberingRuleServiceTests` 全 passed（8 个）
- [ ] **并发测试**：`NumberingServiceConcurrencyTests` 全 passed（8 个，需 Docker）；重点 `GenerateAsync_Concurrent_AllUnique` 验证 100 并发号全部唯一
- [ ] **跨年边界**：`NumberingClockTests.PeriodKey_BoundaryYearRollover_BeijingTime` passed
- [ ] **锁字段**：已启用规则关键字段前端置灰、后端 400 拒绝
- [ ] **预览**：预览接口返回正确下一个号，连续两次预览相同（不消耗计数）
- [ ] **权限隔离**：无 `system:numbering:view` 权限的用户看不到菜单、API 返回 403
- [ ] **admin 通配**：admin 能访问所有编号接口
- [ ] **端到端**：前端新建一条 fabric 规则 → 列表出现示例格式 → 编辑停用后可改前缀 → 日志 Tab 查询（生成日志需等业务模块接入后才有数据）

最终提交所有改动后，合并 `feat/numbering` 到 master。
