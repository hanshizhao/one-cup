# OneCup 编号管理（编码规则引擎）设计文档

> 印染厂面料开发管理系统 — 系统级基础设施工具：为各业务模块提供配置化的自动编码生成
> 创建日期: 2026-07-02
> 状态: 待实现
> 关联: [系统管理界面设计](2026-07-02-system-management-design.md)（已完成，本模块挂在其菜单下）

---

## 1. 目标与范围

### 1.1 目标

构建一个**统一编码规则引擎**，让各业务模块（面料、物料、设备、客户、颜色、产品……）通过调用同一套服务获得**唯一、连续、可配置、可追溯**的编码。引擎本身不持有任何业务数据，只负责"按规则取号、拼码、记录计数与日志"。

### 1.2 本轮交付范围

| 模块 | 功能 |
|------|------|
| **规则引擎核心** | 规则表、计数表、日志表三张表；拼码逻辑；并发安全的取号服务 |
| **规则管理 API** | 完整 CRUD（列表/详情/新建/编辑/启停），禁物理删除（停用替代） |
| **编码生成服务** | `INumberingService.GenerateAsync()` —— 纯后端内部调用，事务内行锁取号 |
| **预览接口** | `GET /api/numbering/preview` —— 供前端展示"下一个号大概是什么"，不消耗计数 |
| **生成日志查询** | 分页查询历史取号记录，支持多维度筛选 |
| **前端页面** | 系统管理菜单下新增"编号管理"页面（规则配置 Tab + 生成日志 Tab） |

### 1.3 不在范围内

- 面料/物料等业务模块的接入（等各业务模块独立设计时再调用 `INumberingService`）
- 品类字典的维护（归各业务模块，引擎只消费字符串）
- 编码回收、跨周期跳号修复（落库时生成方案下不需要）
- 自由段排序、JSON 段配置（固定顺序已满足需求）

### 1.4 成功标准

- 引擎能独立运行：配置一条 `fabric` 规则后，单元测试能验证 `GenerateAsync("fabric","COT")` 在并发下产出唯一连续编码
- 任何已生成的编码，都能通过日志表追溯到来源规则、品类、周期、时间
- 已启用的规则无法修改影响格式的关键字段，防止历史编码割裂
- 跨年时刻（北京时间）周期键正确切换，不串号

---

## 2. 后端 API 设计

RESTful 风格，复用已有 Clean Architecture 分层。路由前缀 `/api/numbering`。

### 2.1 规则管理 API

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| GET | `/api/numbering/rules` | `system:numbering:view` | 规则分页列表，query: `page`, `pageSize`, `keyword`(名称/前缀模糊), `targetType`, `isActive` |
| GET | `/api/numbering/rules/{id}` | `system:numbering:view` | 规则详情 |
| POST | `/api/numbering/rules` | `system:numbering:manage` | 新建规则 |
| PUT | `/api/numbering/rules/{id}` | `system:numbering:manage` | 编辑规则（已启用规则拒绝改关键字段，见 2.6） |
| PUT | `/api/numbering/rules/{id}/status` | `system:numbering:manage` | 启停规则，body: `{ isActive: bool }` |

**无 DELETE 接口** —— 停用即软删除替代。

**分页响应格式**：复用已有 `PagedResult<T>`。

### 2.2 预览 API

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| GET | `/api/numbering/preview` | 登录即可（无权限策略） | query: `targetType`, `categoryCode?`；返回预览编码或 null |

响应：`{ code: "FAB-COT-2026-0016", note: "预览编号，实际保存时以系统分配为准" }`

预览**不消耗计数、不加锁、不写日志**，是纯只读计算。并发下实际保存可能拿到更大的号，仅作参考。

### 2.3 生成日志 API

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| GET | `/api/numbering/logs` | `system:numbering:view` | 日志分页查询，query: `page`, `pageSize`, `targetType`, `categoryCode`, `ruleId`, `code`(编码关键字), `startDate`, `endDate` |

### 2.4 DTO 定义

```
// 规则
CreateNumberingRuleRequest: {
  targetType, name, prefix,
  includeCategory,            // bool
  dateSegment,                // enum: None/Year/YearMonth/YearMonthDay
  seqLength,                  // int 1-8
  separator,                  // string，默认 "-"
  resetPeriod,                // enum: None/Yearly/Monthly/Daily
  remark?                     // string
}
UpdateNumberingRuleRequest:   { 同 Create，所有字段可选 }
UpdateRuleStatusRequest:      { isActive }
NumberingRuleDto:             { id, targetType, name, prefix, includeCategory, dateSegment,
                                seqLength, separator, resetPeriod, isActive, remark,
                                createdAt, updatedAt, sampleFormat }
NumberingRuleListItemDto:     { id, targetType, name, prefix, sampleFormat, isActive, createdAt }

// sampleFormat 说明：后端用占位品类码 "CAT"、seq=1、当前北京时间拼出的真实格式示例。
//   例：prefix=FAB includeCategory=true dateSegment=Year seqLength=4 sep="-" → "FAB-CAT-2026-0001"
//   作用：列表/详情展示，让管理员直观看到这条规则的产出形态（不是模板占位符，是可直接读懂的示例）。

// 预览
PreviewCodeResult:            { code?, note }

// 日志
NumberingLogListItemDto:      { id, generatedCode, targetType, categoryCode?, periodKey?,
                                seqValue, createdAt, ruleName? }
```

### 2.5 对内服务接口（业务模块调用）

业务模块只依赖 `INumberingService`（生成/预览），规则管理走独立的 `INumberingRuleService`（系统管理用）。两个接口职责分离，互不污染。

```csharp
public interface INumberingService
{
    /// <summary>生成编码（业务对象落库事务内调用）。线程安全，支持并发。</summary>
    Task<string> GenerateAsync(string targetType, string? categoryCode = null);

    /// <summary>预览下一个编码（只读，不消耗计数，仅供参考）。</summary>
    Task<string?> PreviewAsync(string targetType, string? categoryCode = null);
}

public interface INumberingRuleService
{
    Task<PagedResult<NumberingRuleListItemDto>> GetListAsync(int page, int pageSize,
        string? keyword, string? targetType, bool? isActive);
    Task<NumberingRuleDto?> GetAsync(Guid id);
    Task<NumberingRuleDto> CreateAsync(CreateNumberingRuleRequest req);
    Task UpdateAsync(Guid id, UpdateNumberingRuleRequest req);
    Task UpdateStatusAsync(Guid id, bool isActive);
    Task<PagedResult<NumberingLogListItemDto>> GetLogsAsync(
        int page, int pageSize, string? targetType, string? categoryCode,
        Guid? ruleId, string? code, DateTime? startDate, DateTime? endDate);
}
```

**生成接口契约**（业务模块必须遵守）：

```csharp
using var tx = await _db.Database.BeginTransactionAsync();
try
{
    fabric.Code = await _numberingService.GenerateAsync(NumberTargetTypes.Fabric, "COT");
    _db.Fabrics.Add(fabric);
    await _db.SaveChangesAsync();
    await tx.CommitAsync();   // 业务对象落库成功 → 计数和日志一起提交
}
catch
{
    // tx 自动 dispose 回滚 → 计数自增和日志 INSERT 全部回滚 → 不跳号、无垃圾日志
    throw;
}
```

`GenerateAsync` 内部**不提交事务**，由调用方控制提交时机。

### 2.6 编辑与启停的约束

**编辑接口（已启用规则锁关键字段）**，服务层校验：
- 若规则 `IsActive=true`：仅 `remark` 可改；修改 `name/prefix/targetType/includeCategory/dateSegment/seqLength/separator/resetPeriod` → 返回 400 "已启用的规则不可修改关键配置，请先停用"
- 若规则 `IsActive=false`：所有字段可改

**启停接口（启用时校验唯一性）**：
- 启用一条规则时，校验该 `target_type` 下是否已有其他启用规则，若有 → 返回 400 "该业务类型已有启用规则，请先停用现有的"
- 这保证数据库唯一索引 `ux_numbering_rules_target_type_active` 不被违反

---

## 3. 数据模型

三张表，沿用项目的 snake_case 命名 + `BaseEntity`（`Id` Guid / `CreatedAt` / `UpdatedAt?`）。

### 3.1 `numbering_rules`（编码规则表）

| 列 | 类型 | 约束 | 说明 |
|----|------|------|------|
| `id` | uuid | PK | 继承 BaseEntity |
| `target_type` | varchar(32) | NOT NULL | 业务对象类型，字符串（见 6.1，不用枚举） |
| `name` | varchar(64) | NOT NULL | 规则名称，如"面料编码规则" |
| `prefix` | varchar(16) | NOT NULL | 固定前缀段，如 `FAB` |
| `include_category` | boolean | NOT NULL DEFAULT false | 是否拼分类码段 |
| `date_segment` | varchar(16) | NOT NULL DEFAULT 'None' | 日期段类型，枚举字符串 `None/Year/YearMonth/YearMonthDay` |
| `seq_length` | smallint | NOT NULL DEFAULT 4 | 流水号位数，范围 1–8 |
| `separator` | varchar(8) | NOT NULL DEFAULT '-' | 段间分隔符，可空串 |
| `reset_period` | varchar(16) | NOT NULL DEFAULT 'None' | 重置周期，枚举字符串 `None/Yearly/Monthly/Daily` |
| `is_active` | boolean | NOT NULL DEFAULT true | 启停状态 |
| `remark` | varchar(256) | NULL | 备注 |
| `created_at` | timestamptz | NOT NULL | 继承 BaseEntity |
| `updated_at` | timestamptz | NULL | 继承 BaseEntity |

**索引**：
- 唯一索引 `ux_numbering_rules_target_type_active`：`(target_type) WHERE is_active = true` —— 保证同一业务类型同时只有一条启用规则
- 普通索引 `ix_numbering_rules_target_type`：`target_type`

### 3.2 `numbering_counters`（计数表 —— 并发取号核心）

| 列 | 类型 | 约束 | 说明 |
|----|------|------|------|
| `id` | uuid | PK | |
| `rule_id` | uuid | FK → numbering_rules.id | 哪条规则 |
| `category_code` | varchar(32) | NOT NULL DEFAULT '' | 品类码（无品类时为空串，**不用 NULL** 避免 PG 唯一约束歧义） |
| `period_key` | varchar(16) | NOT NULL DEFAULT '' | 周期键：不重置=`""`；按年=`"2026"`；按月=`"202607"`；按日=`"20260702"` |
| `current_seq` | integer | NOT NULL DEFAULT 0 | 当前已分配到的最大流水号 |
| `created_at` | timestamptz | NOT NULL | |
| `updated_at` | timestamptz | NULL | |

**索引**：
- 唯一索引 `ux_numbering_counters_bucket`：`(rule_id, category_code, period_key)` —— 这就是"桶"的唯一标识

**关键设计点**：`current_seq` 初始为 0，取号时 `+1`。第一次取号自动 INSERT 新行（新品类/新周期自动建桶）。

**为什么计数表用空串而日志表用 NULL**：计数表的 `(rule_id, category_code, period_key)` 三元组是**唯一索引键**（`ux_numbering_counters_bucket`）。PostgreSQL 唯一索引里多个 NULL 视为互不冲突，会导致"不重置/无品类"场景下出现多行空桶、计数紊乱。因此计数表这两列用空串 `''` 保证唯一性语义正确。日志表无此约束，`category_code`/`period_key` 允许 NULL 以忠实反映"本次取号无此维度"（且日志可空字段不影响查询）。

### 3.3 `numbering_logs`（生成日志表 —— 审计追溯）

| 列 | 类型 | 约束 | 说明 |
|----|------|------|------|
| `id` | uuid | PK | |
| `generated_code` | varchar(64) | NOT NULL | 完整编码，如 `FAB-COT-2026-0001` |
| `rule_id` | uuid | FK → numbering_rules.id | 源规则 |
| `target_type` | varchar(32) | NOT NULL | 业务对象类型（冗余存，便于不 join 规则表直接筛） |
| `category_code` | varchar(32) | NULL | 品类码（可空） |
| `period_key` | varchar(16) | NULL | 周期键 |
| `seq_value` | integer | NOT NULL | 流水号数值 |
| `created_at` | timestamptz | NOT NULL | 取号时间（UTC） |

**索引**：
- `ix_numbering_logs_code`：`generated_code` —— 反查"这个号哪来的"
- `ix_numbering_logs_rule_id`：`(rule_id, created_at)` —— 按规则查历史
- `ix_numbering_logs_target_type`：`(target_type, created_at)` —— 按业务类型查

**关键设计点**：只追加不修改不删除。业务对象落库失败时随事务回滚。**不存业务对象 ID**（保持引擎与业务解耦，见 6.4）。

### 3.4 表关系

```
numbering_rules (1) ────< (N) numbering_counters   每规则按"品类+周期"分多桶
                  └───< (N) numbering_logs         每次取号一条日志
```

---

## 4. 核心生成逻辑

### 4.1 取号主流程（`GenerateAsync`）

全部步骤在 `IDbContextTransaction` 内执行（事务由调用方持有并提交）：

```
1. 查规则：SELECT * FROM numbering_rules
           WHERE target_type={targetType} AND is_active=true
   → 找不到 → 抛 DomainException("未找到 {targetType} 的启用编码规则")

2. 校验品类码：
   if (R.IncludeCategory && categoryCode 为空)
       → 抛 DomainException("规则要求品类码但未提供")
   if (!R.IncludeCategory && categoryCode 非空)
       → 忽略 categoryCode（宽容处理）

3. 计算周期键（基于北京时间，见第 5 节）：
   var now = _clock.GetCurrentTime();
   periodKey = R.ResetPeriod switch {
     None    => "",
     Yearly  => now.Year.ToString(),            // "2026"
     Monthly => now.ToString("yyyyMM"),         // "202607"
     Daily   => now.ToString("yyyyMMdd")        // "20260702"
   };

4. 归一化桶维度（空串代替 NULL）：
   bucketCategory = R.IncludeCategory ? categoryCode : ""
   bucketPeriod   = periodKey

5. 行锁取号（见 4.3 并发控制）：
   锁桶 → newSeq = current_seq + 1 → UPDATE

6. 拼码（见 4.2）

7. 写日志：INSERT numbering_logs（同事务）

8. 返回 code（事务由调用方提交）
```

### 4.2 拼码规则

**固定段顺序**：`[前缀] {sep} [分类码?] {sep} [日期?] {sep} [流水号]`

| 段 | 取值 |
|----|------|
| 前缀 | 规则的 `prefix` |
| 分类码 | `IncludeCategory=true` 时取 `categoryCode`；否则该段不出现 |
| 日期 | 基于 `date_segment` 和北京时间：None→不出现；Year→`"2026"`；YearMonth→`"202607"`；YearMonthDay→`"20260702"` |
| 流水号 | `newSeq` 按 `seq_length` 补零，如 len=4 seq=7 → `"0007"` |
| 分隔符 | 规则的 `separator`，连接出现的段；某段不出现则其两侧分隔符也不出现 |

**拼接示例**：

| 规则配置 | newSeq=7 时产出 |
|---------|----------------|
| prefix=FAB, includeCategory=true, dateSegment=Year, seqLength=4, sep=`-` | `FAB-COT-2026-0007` |
| prefix=MAT, includeCategory=false, dateSegment=None, seqLength=6, sep=`-` | `MAT-000007` |
| prefix=EQ, includeCategory=false, dateSegment=Year, seqLength=4, sep=`""` | `EQ20260007` |
| prefix=CL, includeCategory=true, dateSegment=YearMonth, seqLength=3, sep=`-` | `CL-RED-202607-007` |

**流水号溢出**：若 `newSeq` 位数超过 `seq_length`（如 4 位跑到 10000），**不截断、不自动扩位**，直接抛 `DomainException` 阻断生成（见 6.5）。

### 4.3 并发控制（行锁 + 唯一约束兜底）

**已存在的桶**：用原生 SQL `SELECT ... FOR UPDATE` 锁行：

```csharp
var bucket = await _db.NumberingCounters
    .FromSqlRaw(
        "SELECT * FROM numbering_counters WHERE rule_id={0} AND category_code={1} AND period_key={2} FOR UPDATE",
        rule.Id, bucketCategory, bucketPeriod)
    .FirstOrDefaultAsync();
```

**新建桶的竞态**：并发下可能两个事务同时发现桶不存在，第二个 INSERT 因唯一约束 `ux_numbering_counters_bucket` 冲突失败。由服务层有限重试（见 4.4）兜底。

**为什么不用 PG 的 `ON CONFLICT DO UPDATE`**：那会强绑定 PostgreSQL，违背项目的可替换性原则。行锁方案在 EF Core 里有通用写法，换库成本低。

### 4.4 服务层重试机制 + 事务归属

**事务归属（重要）**：`GenerateAsync` **不开、不提交事务**——它在调用方已开启的事务内执行（见 §2.5 调用方契约）。这是 B+ 方案"业务对象落库失败则计数随事务回滚、不跳号"的核心保证。

**fail-fast 守卫**：方法入口检测 `_db.Database.CurrentTransaction is null` 时抛 `DomainException("GenerateAsync 必须在调用方的事务内调用")`。这防止静默不安全调用——`FOR UPDATE` 行锁依赖活动事务，无事务调用会让锁失效。

针对"桶唯一约束冲突"（新品类/新周期首次建桶时的竞态），包一层有限重试（最多 3 次）。注意：因为是调用方持事务，冲突时**不能回滚整个事务**（否则业务对象也回滚），而是清理 ChangeTracker 里失败的 Added 实体后重试：

```csharp
public async Task<string> GenerateAsync(string targetType, string? categoryCode, CancellationToken ct = default)
{
    // fail-fast：必须在调用方事务内（FOR UPDATE 依赖事务；B+ 回滚保证依赖事务）
    if (_db.Database.CurrentTransaction is null)
        throw new DomainException("GenerateAsync 必须在调用方的事务内调用");

    for (int attempt = 0; attempt < 3; attempt++)
    {
        try
        {
            // ... 步骤 1-7（行锁取号、自增、拼码、写日志）...
            return code;   // 不提交事务，由调用方控制（见 §2.5）
        }
        catch (Exception ex) when (IsUniqueConstraintViolation(ex))
        {
            // 桶被别人建了：detach 失败的 Added 实体，重试时 SELECT 会找到已建桶
            foreach (var entry in _db.ChangeTracker.Entries().Where(e => e.State == EntityState.Added).ToList())
                entry.State = EntityState.Detached;
            continue;
        }
    }
    throw new DomainException("编号生成失败：并发冲突，请重试");
}
```

仅"桶唯一约束冲突"重试；其他异常（规则不存在等）直接抛出。

### 4.5 预览逻辑（`PreviewAsync`）

不消耗计数、不加锁、不写日志，纯只读计算：

```
1. 查启用规则 R；找不到 → 返回 null
2. 计算 PeriodKey（同 GenerateAsync 步骤 3）
3. 普通读计数（不加锁）：currentSeq = SELECT current_seq ... ; 不存在 → 0
4. 预览号 = 拼码(R, currentSeq + 1)
5. 返回预览号
```

**语义**：预览号 = "如果现在保存，大概率是这个号"；并发下实际保存可能拿到更大的号。

---

## 5. 时区处理（北京时间跨年）

### 5.1 决策

- **编码周期键 & 日期段**：基于**北京时间（UTC+8）**计算
- **数据库时间戳（`created_at` 等）**：保持 **UTC**（存储层统一化铁律，绝不改动）
- **时区 ID**：统一用 IANA `"Asia/Shanghai"`，.NET 10 在 Windows/Linux 全平台通用

这是"展示层本地化、存储层统一化"的标准做法——只有"用于拼编码的周期键和日期段"使用北京时间，存储时间戳保持 UTC。

### 5.2 实现：抽取 `INumberingClock`

```csharp
// Application 层
public interface INumberingClock
{
    /// <summary>用于编号周期键和日期段计算的北京时间。</summary>
    DateTime GetCurrentTime();
}

// Infrastructure 层实现
public class NumberingClock : INumberingClock
{
    private static readonly TimeZoneInfo ChinaTz =
        TimeZoneInfo.FindSystemTimeZoneById("Asia/Shanghai");

    public DateTime GetCurrentTime() =>
        TimeZoneInfo.ConvertTimeFromUtc(DateTime.UtcNow, ChinaTz);
}
```

DI 注册：`services.AddScoped<INumberingClock, NumberingClock>();`

### 5.3 为什么用接口

1. **可测试**：单元测试注入假时钟，设定跨年边界时刻验证周期键切换——用真实系统时钟测不了
2. **集中化**：编号系统所有"取当前时间"的入口只有一处，避免散落的 `DateTime.UtcNow` 导致时区不一致
3. **未来可配置**：若需支持多租户不同时区，换实现即可，不动业务代码

---

## 6. 关键设计决策

### 6.1 `target_type` 用字符串 + 常量类，不用枚举

`target_type` 是**业务维度**（fabric/material/equipment...），会随业务模块持续扩展。用硬枚举会导致每加一个业务类型都要改引擎代码并重新部署，引擎被业务扩展"卡脖子"。因此：

- 数据库存 `varchar`，**无 CHECK 约束**，不校验合法性
- Application 层提供 `NumberTargetTypes` 静态常量类做"已知清单"提示，**不强制**：

```csharp
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

- 前端规则配置页"业务类型"下拉默认列这 6 个常量，**允许手输**（为未来扩展留口）

**对比引擎自身的配置字段**（date_segment、reset_period）是**封闭集合**，这些用 C# 枚举（EF Core `HasConversion<string>()` 存字符串），兼顾类型安全和可读性。

**哲学**：该封闭的封闭、该开放的开放——业务维度开放、引擎配置封闭。

### 6.2 业务类型调整时的流程（零引擎改动）

| 场景 | 流程 | 引擎改动 |
|------|------|---------|
| 新增业务类型 | 业务模块定义自己的标识 → 管理页建规则 → 调 `GenerateAsync` | 无（常量类更新可选） |
| 业务类型改名 | 旧规则停用 → 新建同名规则 → 业务模块改调用码 | 无 |
| 业务类型废弃 | 管理页停用规则 → 历史数据永久保留 | 无 |
| 编码格式调整 | 旧规则停用 → 新建一条同 `target_type` 规则 | 无 |

**核心承诺**：任何业务类型调整 = 纯数据操作 + 业务模块自身改调用代码。编号引擎代码、数据库结构一律不动。

### 6.3 品类码归属业务模块，引擎只消费

- 品类字典（COT=棉、CHE=化纤...）是面料模块的**业务主数据**，归业务模块管
- 编号引擎把 `categoryCode` 当**字符串参数**用，不持有/不约束品类字典
- 计数表 `numbering_counters.category_code` 无外键到任何品类字典表
- **新品类第一次来取号时自动建桶**（INSERT 新计数行），引擎对品类增删改零感知
- **软约束**：引擎不校验品类合法性，业务模块必须自己保证（如下拉框选而非手输）

### 6.4 流水号按品类独立计数（语义乙）

每个「规则 + 品类 + 周期」一行计数，各品类独立从 0001 开始：

```
规则1 + COT + 2026 → current_seq=15  （棉，今年第15个）
规则1 + CHE + 2026 → current_seq=8   （化纤，今年第8个）
```

符合业务人员"今年开发了 15 块棉织物、8 块化纤"的心智模型。

### 6.5 落库时生成（B+ 方案）

- 编码在**业务对象保存事务**内生成，业务对象落库失败则计数回滚，**不跳号**
- 同时提供预览接口供前端展示"下一个号大概是什么"，预览不消耗计数
- **预览号仅供参考**：并发下实际保存可能拿到更大的号

### 6.6 日志表不关联业务对象

日志记录编码生成事件，**不存业务对象 ID**。原因：业务对象在 `GenerateAsync` 返回时还没 ID（落库前）；若要回填，要求业务模块保存成功后回调引擎更新日志，增加跨模块耦合。"编码→业务对象"的查询直接去业务表按 `Code` 字段查即可。

### 6.7 规则管理：禁物理删除 + 已启用锁关键字段

- 删除按钮实际是"停用"（`is_active=false`），保留历史计数和日志供审计
- 已启用规则只能改 `remark`；要换格式必须停用旧的、新建一条
- 启用规则时校验该 `target_type` 下唯一性

### 6.8 前缀不可含分隔符

新建/编辑规则时，服务层校验 `prefix` 不包含 `separator` 字符（避免产出有歧义的编码）。品类码不校验（由业务模块自治）。

---

## 7. 后端分层落地

严格遵循 Clean Architecture，沿用已有模式。

### 7.1 Domain 层

| 文件 | 内容 |
|------|------|
| `Enums/DateSegment.cs` | 枚举：`None/Year/YearMonth/YearMonthDay` |
| `Enums/ResetPeriod.cs` | 枚举：`None/Yearly/Monthly/Daily` |
| `Entities/NumberingRule.cs` | 规则实体（继承 `BaseEntity`） |
| `Entities/NumberingCounter.cs` | 计数实体（继承 `BaseEntity`） |
| `Entities/NumberingLog.cs` | 日志实体（继承 `BaseEntity`） |

### 7.2 Application 层

| 文件 | 内容 |
|------|------|
| `Common/NumberTargetTypes.cs` | 业务类型常量类（见 6.1） |
| `Dtos/System/NumberingDtos.cs` | 所有编号相关 DTO |
| `Interfaces/INumberingClock.cs` | 时间提供者接口（北京时间） |
| `Interfaces/INumberingService.cs` | 生成/预览服务接口 |
| `Interfaces/INumberingRuleService.cs` | 规则管理服务接口 |

### 7.3 Infrastructure 层

| 文件 | 内容 |
|------|------|
| `Services/NumberingClock.cs` | `INumberingClock` 实现（`Asia/Shanghai`） |
| `Services/NumberingService.cs` | 实现 `INumberingService`：拼码 + 行锁取号 + 重试 + 日志 |
| `Services/NumberingRuleService.cs` | 实现 `INumberingRuleService`：规则 CRUD + 锁字段校验 + 日志查询 |
| `Persistence/Configurations/NumberingRuleConfiguration.cs` | 规则表配置 + 唯一索引 |
| `Persistence/Configurations/NumberingCounterConfiguration.cs` | 计数表配置 + 桶唯一索引 |
| `Persistence/Configurations/NumberingLogConfiguration.cs` | 日志表配置 + 查询索引 |
| `Persistence/OneCupDbContext.cs` | 新增 3 个 `DbSet<>` |
| `Persistence/SeedData.cs` | 新增 2 条权限 + 挂到管理员角色 |

### 7.4 Api 层

| 文件 | 内容 |
|------|------|
| `Controllers/NumberingController.cs` | 规则 CRUD + 预览 + 日志查询端点 |
| `Program.cs` | 注册服务 + 2 个授权 Policy |

### 7.5 EF 迁移

新增迁移 `AddNumberingModule`（从 `backend/` 执行）：
```
dotnet ef migrations add AddNumberingModule --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```

### 7.6 权限种子数据

在 `SeedData.cs` 新增两条权限（Guid 命名沿用项目规范）：

```
system:numbering:view   → 挂到"系统管理员"角色
system:numbering:manage → 挂到"系统管理员"角色
```

`Program.cs` 注册策略：
```csharp
options.AddPolicy("numbering-view",   p => p.RequireClaim("perm_codes", "system:numbering:view"));
options.AddPolicy("numbering-manage", p => p.RequireClaim("perm_codes", "system:numbering:manage"));
```

预览接口仅需 `[Authorize]`（登录即可）。

---

## 8. 前端设计

### 8.1 路由与菜单

`routes.ts` 在「系统管理」下新增子菜单：

```typescript
{
  name: 'menu.system',
  key: 'system',
  children: [
    { name: 'menu.system.user', key: 'system/user' },
    { name: 'menu.system.role', key: 'system/role' },
    { name: 'menu.system.permission', key: 'system/permission' },
    { name: 'menu.system.numbering', key: 'system/numbering',   // 新增
      requiredPermissions: { resource: 'system:numbering', actions: ['view'] } },
  ],
}
```

同步在 `locale/index.ts` 加 `menu.system.numbering`（中/英）。

### 8.2 页面结构：单页 + Tab

入口 `src/pages/system/numbering/index.tsx`，顶部 `Tabs` 切换两个视图：

#### Tab 1：规则配置

```
┌─────────────────────────────────────────────────────┐
│ [搜索: 名称/前缀] [业务类型▾] [状态▾]    [+ 新建规则] │
├─────────────────────────────────────────────────────┤
│ 名称 │ 业务类型 │ 前缀 │ 示例格式       │ 状态 │ 操作  │
│ 面料编码 fabric  FAB  FAB-C-YYYY-#### 启用 [编辑][停用]│
│ 物料编码 material MAT  MAT-######      启用 [编辑][停用]│
│ ...                                                  │
├─────────────────────────────────────────────────────┤
│                    < 1 2 3 >                        │
└─────────────────────────────────────────────────────┘
```

- 表格多一列「**示例格式**」：用规则配置拼出示意编码（如 `FAB-C-YYYY-####`），让管理员直观看到产出形态
- 「状态」用 Arco `Tag`（绿/灰）；「停用」弹 `Popconfirm` 二次确认
- 「+ 新建规则」打开 `Drawer` 抽屉

#### 新建/编辑抽屉表单

| 字段 | 控件 | 说明 |
|------|------|------|
| 业务类型 | `Select`（6 常量，**允许手输**） | 对应 `NumberTargetTypes` |
| 规则名称 | `Input` | 必填 |
| 前缀 | `Input` | 必填 |
| 包含分类码 | `Switch` | |
| 日期段 | `Select`（不包含/年/年月/年月日） | |
| 流水号位数 | `InputNumber`（1-8） | 默认 4 |
| 分隔符 | `Input`（maxLength 8） | 默认 `-`，可清空 |
| 重置周期 | `Select`（不重置/按年/按月/按日） | |
| 备注 | `TextArea` | 可选 |
| **实时预览** | 只读文本 | 表单顶部显示，随输入实时更新（纯前端拼码） |

**编辑模式**：若规则 `isActive=true`，关键字段（业务类型/前缀/包含分类码/日期段/流水号位数/分隔符/重置周期）**置灰禁用**并提示"停用后可修改"，只有备注可改。

#### Tab 2：生成日志

```
┌─────────────────────────────────────────────────────┐
│ [业务类型▾] [品类码] [编码关键字] [时间范围] [查询]   │
├─────────────────────────────────────────────────────┤
│ 编码              业务类型 品类 周期  流水号 时间      │
│ FAB-COT-2026-0007 fabric  COT  2026 7    2026-07-02 │
│ ...                                                  │
├─────────────────────────────────────────────────────┤
│                    < 1 2 3 >                        │
└─────────────────────────────────────────────────────┘
```

纯查询展示，无增删改。

### 8.3 新增前端文件

```
src/api/numbering.ts                       # 规则/预览/日志 API
src/pages/system/numbering/index.tsx       # 主页（Tab 容器）
src/pages/system/numbering/locale/         # i18n
```

### 8.4 使用的 Arco 组件

Tabs、Table、Drawer、Form、Input、InputNumber、Select、Switch、Tag、Popconfirm、DatePicker（日志时间范围）、Message。

---

## 9. 错误处理

沿用项目 `DomainException` → 全局异常处理器 → 400 的模式。

| 场景 | 抛出 | 前端提示 |
|------|------|---------|
| 取号时未找到该 `targetType` 的启用规则 | `DomainException("未找到 {targetType} 的启用编码规则")` | "该业务类型尚未配置编码规则，请联系管理员" |
| 规则要求分类码但业务层未传 | `DomainException("规则要求品类码但未提供")` | （业务层调用错误，用户无感，记日志） |
| 新建/启用规则时该 `targetType` 已有启用规则 | `DomainException("该业务类型已有启用规则，请先停用现有的")` | 原文 |
| 编辑已启用规则的关键字段 | `DomainException("已启用的规则不可修改关键配置，请先停用")` | 原文 |
| 流水号溢出（超过 `seq_length` 位数） | `DomainException("流水号已超出配置位数 {seqLength}，请调整规则或联系管理员")` | 原文（提示扩位） |
| 桶并发竞态重试 3 次仍失败 | `DomainException("编号生成失败：并发冲突，请重试")` | "系统繁忙，请重试" |
| 前缀包含分隔符 | `DomainException("前缀不可包含分隔符")` | 原文 |
| 预览时未配置规则 | 不抛异常，返回 `null` | 前端显示"未配置规则" |

---

## 10. 测试策略

沿用项目 `OneCup.UnitTests`（xUnit）。

### 10.1 拼码逻辑单元测试（纯函数，无数据库）

| 测试用例 | 期望输出 |
|---------|---------|
| 全段组合 prefix=FAB cat=COT date=Year seq=7 len=4 sep=`-` | `FAB-COT-2026-0007` |
| 无分类码 | `FAB-2026-0007` |
| 无日期 | `FAB-COT-0007` |
| 无分隔符 sep=`""` | `FABCOT20260007` |
| 仅前缀+流水 | `FAB-0007` |
| 日期各格式 YearMonth/YearMonthDay | `202607` / `20260702` |
| 流水号补零 seq=7 len=6 | `000007` |
| 流水号溢出 seq=10000 len=4 | 抛 `DomainException` |

### 10.2 规则管理服务测试

| 场景 | 验证点 |
|------|--------|
| 新建规则成功 | 入库正确 |
| 新建时 `targetType` 已有启用规则 | 抛 `DomainException` |
| 编辑已启用规则关键字段 | 抛 `DomainException` |
| 编辑已停用规则 | 成功 |
| 停用/启用规则 | `isActive` 正确切换 |
| 启用时唯一性冲突 | 抛 `DomainException` |
| 前缀含分隔符 | 抛 `DomainException` |

### 10.3 并发取号测试（核心 —— 决定引擎可不可信）

**用 Testcontainers + PostgreSQL**（不能用 InMemory，它不支持 `FOR UPDATE` 行锁也不模拟真实并发）。

| 场景 | 验证点 |
|------|--------|
| 串行取号 100 次 | 产出 0001-0100，连续无重复 |
| 并发取号（10 任务各取 100 次） | 1000 个号全部唯一、无间隙 |
| 新品类首次取号 | 自动建桶，从 0001 开始 |
| 跨周期取号 | 周期键变化后从 0001 重新计数 |
| 不同品类独立计数 | COT 和 CHE 各自独立从 0001 开始 |
| 桶竞态（两事务同时建桶） | 唯一约束兜底，一个成功一个重试后成功 |

### 10.4 跨年边界测试（北京时间）

用 `INumberingClock` 注入假时钟：

| 输入时间（北京时间） | Yearly 重置下的 periodKey |
|---------------------|--------------------------|
| `2026-12-31 23:59:59 +08:00` | `"2026"` |
| `2027-01-01 00:00:00 +08:00` | `"2027"` |

验证同一 UTC 时刻、不同北京时间的周期键正确切换。

---

## 11. 验证标准（Definition of Done）

1. **数据库**：迁移成功，三张表 + 索引正确创建，权限种子数据就位。
2. **规则管理**：能分页查看/搜索/筛选规则，新建规则（填表单→实时预览→保存→列表刷新），编辑停用规则，停用/启用规则（含唯一性冲突提示）。
3. **生成（后端验证）**：配置一条 `fabric` 规则后，单元测试通过——串行 100 次取号连续无重复，并发 10×100 次取号全部唯一。
4. **跨周期**：跨年（北京时间）时周期键正确切换，新周期从 0001 重新计数。
5. **品类独立计数**：不同品类各自独立从 0001 计数。
6. **日志追溯**：任何已生成的编码都能在日志页查到来源规则、品类、周期、时间。
7. **锁字段**：已启用规则的关键字段在前端置灰、后端拒绝修改。
8. **预览**：预览接口返回正确的下一个号，不消耗计数（调预览前后计数不变）。
9. **权限隔离**：无 `system:numbering:view` 权限的用户看不到编号管理菜单、访问 API 返回 403。
10. **admin 通配**：admin 角色能访问所有编号管理接口（`*` 通配放行）。

---

## 12. 后续演进

| 顺序 | 内容 |
|------|------|
| 1 | 面料模块接入编号引擎（面料模块设计时落地） |
| 2 | 其他业务模块（物料/设备/客户/颜色/产品）逐个接入 |
| 3 | 日志归档策略（按年分区或定期归档，避免无限膨胀） |
| 4 | 规则复制功能（基于现有规则快速新建） |
| 5 | 多级分类码（如面料分大类/小类两级，若业务出现需求） |
