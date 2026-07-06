# 工艺库（织造）CRUD 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现工艺库的织造工艺 CRUD（含溯源查询），作为独立主数据资源库的第一块可交付物。

**Architecture:** 三层落地——Domain 实体（库实体 + 3 个子表）→ Application（DTO/Validator/Specs/Service/溯源工具）→ Api（Controller + DI + 权限策略）。遵循项目既有的 Process / EquipmentType 范式。本计划**不含**快照/回写机制（依赖未实现的面料开发主轴）和流程/染色工艺（字段待业务确认），那些留待后续计划。

**Tech Stack:** .NET 10 / EF Core / PostgreSQL / FluentValidation / xUnit（EF InMemory）

## 范围说明（重要）

本计划对应方向文档 `docs/superpowers/specs/2026-07-06-craft-library-design.md` 的**部分实现**：

- ✅ **包含**：工艺库织造工艺的 CRUD + fork 溯源查询（DAG 遍历）+ 编号引擎接入 + 权限/种子
- ❌ **不包含**（留待后续计划）：
  - 快照实体 + 引用即快照机制 + 字段级 diff + 归档回写（依赖面料开发主轴，未实现）
  - 流程工艺、染色工艺（字段待业务确认）
  - 前端页面（后端 API 稳定后再做）
  - 溯源图谱可视化组件（前端图表库选型待定）

本计划交付后，工艺库的织造工艺可在后端独立运行：新建/编辑/查询/停用/删除/溯源，但不具备被面料开发单"引用即快照"的能力（那是快照计划的事）。

## Global Constraints

源自方向文档与项目既有约定，每个 Task 隐式遵守：

- **实体基类**：所有实体继承 `BaseEntity`（含 `Id`/`CreatedAt`/`UpdatedAt`），库实体实现 `ISoftDeletable`（边界2：有引用禁删，但本计划范围内无"引用者"，软删除为未来快照计划预留禁删判定基础）
- **编号 targetType**：`craft-weaving`（kebab-case，与 `equipment-type` 一致）
- **编号引擎调用**：`_numbering.GenerateAsync(NumberTargetTypes.WeavingCraft, request.CategoryCode, ct)`，事务内取号
- **Status 枚举**：`CraftStatus { Active = 1, Inactive = 2 }`（不用 0，与 `EquipmentStatus` 一致避免默认值陷阱）
- **子表 diff 范式**：织机参数/纱支/坯布规格随主体整表替换（PUT 时按 Id diff：null=新增、有值=更新、缺失=删除），同 `EquipmentTypeParameter`
- **删除规约 c01**：库实体软删除；本计划范围内无引用者，删除走 Popconfirm 即可（前端阶段）
- **命名空间**：Domain=`OneCup.Domain.Entities`、Application 服务=`OneCup.Application.Services`、DTO=`OneCup.Application.Dtos.System`、Validator=`OneCup.Application.Validators.System`、Specs=`OneCup.Application.Specifications`、Controller=`OneCup.Api.Controllers`
- **EF 配置 snake_case + snake_case 列名 + `ux_` 前缀唯一索引**（同 Equipment 配置）
- **权限码格式**：`craft-weaving:read|create|update|delete`
- **SeedData Guid 分配**：权限 `...0301+` 段，当前 EquipmentType 用到 `...0332`，故织造工艺从 `...0333` 起；TargetType `...0201+` 段，当前到 `...0208`，故 `craft-weaving` 用 `...0209`

## 文件结构

| 文件 | 责任 | Task |
|---|---|---|
| `backend/src/OneCup.Domain/Enums/CraftStatus.cs` | 工艺状态枚举 | 1 |
| `backend/src/OneCup.Domain/Entities/WeavingCraft.cs` | 织造工艺库实体（主体） | 1 |
| `backend/src/OneCup.Domain/Entities/WeavingCraftMachineParam.cs` | 织机参数子表（设备模板参数快照副本） | 1 |
| `backend/src/OneCup.Domain/Entities/WeavingCraftYarn.cs` | 纱支原料子表 | 1 |
| `backend/src/OneCup.Domain/Entities/WeavingCraftSpec.cs` | 坯布规格子表（键值对结构） | 1 |
| `backend/src/OneCup.Infrastructure/Persistence/Configurations/WeavingCraftConfiguration.cs` | 主体 EF 配置 | 2 |
| `backend/src/OneCup.Infrastructure/Persistence/Configurations/WeavingCraftMachineParamConfiguration.cs` | 织机参数 EF 配置 | 2 |
| `backend/src/OneCup.Infrastructure/Persistence/Configurations/WeavingCraftYarnConfiguration.cs` | 纱支 EF 配置 | 2 |
| `backend/src/OneCup.Infrastructure/Persistence/Configurations/WeavingCraftSpecConfiguration.cs` | 坯布规格 EF 配置 | 2 |
| `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`（改） | 注册 4 个 DbSet | 2 |
| `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs`（改） | 新增权限 Guid + TargetType Guid | 3 |
| `backend/src/OneCup.Application/Common/NumberTargetTypes.cs`（改） | 加 `WeavingCraft` 常量 | 3 |
| Migration（生成） | `AddCraftLibraryWeavingModule` | 3 |
| `backend/src/OneCup.Application/Dtos/System/WeavingCraftDtos.cs` | 请求/响应 DTO | 4 |
| `backend/src/OneCup.Application/Validators/System/CreateWeavingCraftRequestValidator.cs` | 新建校验 | 4 |
| `backend/src/OneCup.Application/Validators/System/UpdateWeavingCraftRequestValidator.cs` | 编辑校验 | 4 |
| `backend/src/OneCup.Application/Specifications/WeavingCraftSpecs.cs` | 查询规范 | 5 |
| `backend/src/OneCup.Application/Services/CraftLineageQuery.cs` | fork 溯源 DAG 遍历工具 | 6 |
| `backend/src/OneCup.Application/Interfaces/IWeavingCraftService.cs` | 服务接口 | 7 |
| `backend/src/OneCup.Application/Services/WeavingCraftService.cs` | 服务实现（CRUD + 溯源 + 子表 diff + 禁删预检） | 7 |
| `backend/src/OneCup.Api/Controllers/WeavingCraftsController.cs` | HTTP 端点 | 8 |
| `backend/src/OneCup.Api/Program.cs`（改） | DI 注册 + 权限策略 | 8 |
| `backend/tests/OneCup.UnitTests/Craft/CraftTestHelper.cs` | 测试共享辅助 | 随各 Task |
| `backend/tests/OneCup.UnitTests/Craft/CraftLineageQueryTests.cs` | 溯源工具测试 | 6 |
| `backend/tests/OneCup.UnitTests/Craft/WeavingCraftServiceTests.cs` | 服务测试 | 7 |

---

## Task 1: Domain 实体与枚举

**Files:**
- Create: `backend/src/OneCup.Domain/Enums/CraftStatus.cs`
- Create: `backend/src/OneCup.Domain/Entities/WeavingCraft.cs`
- Create: `backend/src/OneCup.Domain/Entities/WeavingCraftMachineParam.cs`
- Create: `backend/src/OneCup.Domain/Entities/WeavingCraftYarn.cs`
- Create: `backend/src/OneCup.Domain/Entities/WeavingCraftSpec.cs`

**Interfaces:**
- Consumes: `BaseEntity`（`OneCup.Domain.Entities`）、`ISoftDeletable`（`OneCup.Domain.Entities`，含 `bool IsDeleted`）
- Produces: `WeavingCraft` / `WeavingCraftMachineParam` / `WeavingCraftYarn` / `WeavingCraftSpec` / `CraftStatus`，供 Task 2+ 使用

- [ ] **Step 1: 创建状态枚举**

创建 `backend/src/OneCup.Domain/Enums/CraftStatus.cs`：

```csharp
namespace OneCup.Domain.Enums;

/// <summary>
/// 工艺库中工艺的启用状态。Active 可被新开发单引用；Inactive 不出现在引用选择器。
/// 不用 0 值，避免默认值陷阱（与 EquipmentStatus 一致）。
/// </summary>
public enum CraftStatus
{
    Active = 1,
    Inactive = 2
}
```

- [ ] **Step 2: 创建织造工艺库主体实体**

创建 `backend/src/OneCup.Domain/Entities/WeavingCraft.cs`：

```csharp
using OneCup.Domain.Enums;

namespace OneCup.Domain.Entities;

/// <summary>
/// 织造工艺库实体（独立主数据资源）。对应方向文档 craft-library-design §3.2。
/// 编号由编号引擎生成（targetType=craft-weaving）。软删除。
/// SourceId 实现 fork 溯源：原创为 null，归档回写诞生的工艺指向被 fork 的原工艺。
/// </summary>
public class WeavingCraft : BaseEntity, ISoftDeletable
{
    /// <summary>工艺编号（编号引擎生成，如 ZZ-0001）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>工艺名称（库内唯一）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>fork 溯源：指向被 fork 的原工艺 Id；原创为 null</summary>
    public Guid? SourceId { get; set; }

    public CraftStatus Status { get; set; } = CraftStatus.Active;

    public int SortOrder { get; set; } = 0;

    public string? Remark { get; set; }

    // ===== 其他信息（工艺卡第⑤部分，扁平字段）=====
    public string? NeedleArrangement { get; set; }   // 排针
    public string? ColumnInfo { get; set; }          // 列
    public string? CylinderNeedle { get; set; }      // 针筒针
    public string? YarnArrangement { get; set; }     // 排纱
    public string? YarnLength { get; set; }          // 织胚纱长

    // ===== 子表导航 =====
    public List<WeavingCraftMachineParam> MachineParams { get; set; } = new();
    public List<WeavingCraftYarn> Yarns { get; set; } = new();
    public List<WeavingCraftSpec> Specs { get; set; } = new();

    public bool IsDeleted { get; set; } = false;
}
```

- [ ] **Step 3: 创建织机参数子表实体**

创建 `backend/src/OneCup.Domain/Entities/WeavingCraftMachineParam.cs`：

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 织造工艺卡的织机参数（第②部分）。
/// 值是引用设备模板时复制的快照副本（方向文档决策4）；设备类型/模板来源保留为外键/标识以便溯源。
/// 随 WeavingCraft 整表替换（PUT 时按 Id diff）。
/// </summary>
public class WeavingCraftMachineParam : BaseEntity
{
    public Guid WeavingCraftId { get; set; }

    /// <summary>设备类型外键（追溯是哪种设备）</summary>
    public Guid EquipmentTypeId { get; set; }

    /// <summary>设备运行模板来源（追溯是哪种运行方案），可空（允许手工录入参数）</summary>
    public Guid? EquipmentTemplateId { get; set; }

    /// <summary>参数名（如"车速""温度"）</summary>
    public string ParameterName { get; set; } = string.Empty;

    /// <summary>参数值（统一字符串承载，与 EquipmentTemplateValue 一致）</summary>
    public string? Value { get; set; }

    /// <summary>单位（关联计量单位，Number 类型用），可空</summary>
    public Guid? UnitId { get; set; }

    public int SortOrder { get; set; } = 0;
}
```

- [ ] **Step 4: 创建纱支原料子表实体**

创建 `backend/src/OneCup.Domain/Entities/WeavingCraftYarn.cs`：

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 织造工艺卡的纱支原料表（第③部分）。每种纱支一行。
/// ColorId 是外键引用（边界1：只冻结值不冻结引用对象）。
/// 随 WeavingCraft 整表替换。
/// </summary>
public class WeavingCraftYarn : BaseEntity
{
    public Guid WeavingCraftId { get; set; }

    /// <summary>排位（纱支在织造中的位置序号）</summary>
    public int Position { get; set; }

    /// <summary>纱线名称/规格</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>颜色外键（指向颜色库）</summary>
    public Guid? ColorId { get; set; }

    /// <summary>纱牌（供应商品牌）</summary>
    public string? Brand { get; set; }

    /// <summary>纱批（批次号）</summary>
    public string? BatchNo { get; set; }

    /// <summary>纱比（占比，百分比数值）</summary>
    public decimal? Ratio { get; set; }

    public int SortOrder { get; set; } = 0;
}
```

- [ ] **Step 5: 创建坯布规格子表实体（键值对结构）**

创建 `backend/src/OneCup.Domain/Entities/WeavingCraftSpec.cs`：

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 织造工艺卡的坯布标准规格表（第④部分）。
/// 采用键值对结构（名称+值+单位），因为坯布规格项开放（"下机克重/下机门幅等等等等"），
/// 不固定列。与设备参数值的灵活承载思路一致。
/// 随 WeavingCraft 整表替换。
/// </summary>
public class WeavingCraftSpec : BaseEntity
{
    public Guid WeavingCraftId { get; set; }

    /// <summary>规格项名称（如"下机克重""下机门幅"）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>规格值（统一字符串承载）</summary>
    public string? Value { get; set; }

    /// <summary>单位外键（关联计量单位）</summary>
    public Guid? UnitId { get; set; }

    public int SortOrder { get; set; } = 0;
}
```

- [ ] **Step 6: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCESS（无错误，可能有 warning 但不阻塞）

- [ ] **Step 7: Commit**

```bash
git add backend/src/OneCup.Domain/Enums/CraftStatus.cs backend/src/OneCup.Domain/Entities/WeavingCraft*.cs
git commit -m "feat(craft): 新增织造工艺库 Domain 实体与枚举"
```

---

## Task 2: EF Core 配置与 DbContext 注册

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/WeavingCraftConfiguration.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/WeavingCraftMachineParamConfiguration.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/WeavingCraftYarnConfiguration.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/WeavingCraftSpecConfiguration.cs`
- Modify: `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`

**Interfaces:**
- Consumes: Task 1 的 4 个实体；`OneCupDbContext` 既有结构（DbSet 区域、snake_case 约定）
- Produces: 4 个 DbSet + EF 映射，供 Task 3 Migration 生成与 Task 7 Service 查询使用

参考既有配置范式：`EquipmentTypeConfiguration.cs`（snake_case 列名、`ux_` 唯一索引、`HasOne<>().WithMany()` 导航）。

- [ ] **Step 1: 创建主体 EF 配置**

创建 `backend/src/OneCup.Infrastructure/Persistence/Configurations/WeavingCraftConfiguration.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class WeavingCraftConfiguration : IEntityTypeConfiguration<WeavingCraft>
{
    public void Configure(EntityTypeBuilder<WeavingCraft> b)
    {
        b.ToTable("weaving_crafts");
        b.HasKey(x => x.Id);
        b.Property(x => x.Code).HasMaxLength(64).IsRequired();
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Status).HasConversion<int>();
        b.Property(x => x.SortOrder);
        b.Property(x => x.Remark).HasMaxLength(1000);
        b.Property(x => x.NeedleArrangement).HasMaxLength(200);
        b.Property(x => x.ColumnInfo).HasMaxLength(200);
        b.Property(x => x.CylinderNeedle).HasMaxLength(200);
        b.Property(x => x.YarnArrangement).HasMaxLength(500);
        b.Property(x => x.YarnLength).HasMaxLength(100);
        b.Property(x => x.IsDeleted);
        b.Property(x => x.CreatedAt);
        b.Property(x => x.UpdatedAt);

        // Code 库内唯一（忽略软删除，与设备类型 Name 唯一性预检一致）
        b.HasIndex(x => x.Code).IsUnique().HasDatabaseName("ux_weaving_crafts_code");
        // Name 库内唯一
        b.HasIndex(x => x.Name).IsUnique().HasDatabaseName("ux_weaving_crafts_name");

        b.HasMany(x => x.MachineParams)
            .WithOne()
            .HasForeignKey(x => x.WeavingCraftId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Yarns)
            .WithOne()
            .HasForeignKey(x => x.WeavingCraftId)
            .OnDelete(DeleteBehavior.Cascade);
        b.HasMany(x => x.Specs)
            .WithOne()
            .HasForeignKey(x => x.WeavingCraftId)
            .OnDelete(DeleteBehavior.Cascade);

        b.HasQueryFilter(x => !x.IsDeleted);
    }
}
```

- [ ] **Step 2: 创建织机参数 EF 配置**

创建 `backend/src/OneCup.Infrastructure/Persistence/Configurations/WeavingCraftMachineParamConfiguration.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class WeavingCraftMachineParamConfiguration : IEntityTypeConfiguration<WeavingCraftMachineParam>
{
    public void Configure(EntityTypeBuilder<WeavingCraftMachineParam> b)
    {
        b.ToTable("weaving_craft_machine_params");
        b.HasKey(x => x.Id);
        b.Property(x => x.WeavingCraftId);
        b.Property(x => x.EquipmentTypeId);
        b.Property(x => x.EquipmentTemplateId);
        b.Property(x => x.ParameterName).HasMaxLength(100).IsRequired();
        b.Property(x => x.Value).HasMaxLength(500);
        b.Property(x => x.UnitId);
        b.Property(x => x.SortOrder);
        b.Property(x => x.CreatedAt);
        b.Property(x => x.UpdatedAt);

        // 跨聚合外键不配导航（与 EquipmentTemplateValueConfiguration 一致）：
        // EquipmentTypeId / EquipmentTemplateId / UnitId 仅作标识，不 HasOne<>
    }
}
```

- [ ] **Step 3: 创建纱支 EF 配置**

创建 `backend/src/OneCup.Infrastructure/Persistence/Configurations/WeavingCraftYarnConfiguration.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class WeavingCraftYarnConfiguration : IEntityTypeConfiguration<WeavingCraftYarn>
{
    public void Configure(EntityTypeBuilder<WeavingCraftYarn> b)
    {
        b.ToTable("weaving_craft_yarns");
        b.HasKey(x => x.Id);
        b.Property(x => x.WeavingCraftId);
        b.Property(x => x.Position);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.ColorId);
        b.Property(x => x.Brand).HasMaxLength(200);
        b.Property(x => x.BatchNo).HasMaxLength(200);
        b.Property(x => x.Ratio).HasPrecision(5, 2); // 0-100.00
        b.Property(x => x.SortOrder);
        b.Property(x => x.CreatedAt);
        b.Property(x => x.UpdatedAt);
    }
}
```

- [ ] **Step 4: 创建坯布规格 EF 配置**

创建 `backend/src/OneCup.Infrastructure/Persistence/Configurations/WeavingCraftSpecConfiguration.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class WeavingCraftSpecConfiguration : IEntityTypeConfiguration<WeavingCraftSpec>
{
    public void Configure(EntityTypeBuilder<WeavingCraftSpec> b)
    {
        b.ToTable("weaving_craft_specs");
        b.HasKey(x => x.Id);
        b.Property(x => x.WeavingCraftId);
        b.Property(x => x.Name).HasMaxLength(200).IsRequired();
        b.Property(x => x.Value).HasMaxLength(500);
        b.Property(x => x.UnitId);
        b.Property(x => x.SortOrder);
        b.Property(x => x.CreatedAt);
        b.Property(x => x.UpdatedAt);
    }
}
```

- [ ] **Step 5: 在 DbContext 注册 DbSet**

修改 `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`，在 Equipment 模块 DbSet 区域（第 48 行 `public DbSet<Equipment> Equipments => Set<Equipment>();` 之后）新增：

```csharp
    // ===== Craft 模块（工艺库）=====
    public DbSet<WeavingCraft> WeavingCrafts => Set<WeavingCraft>();
    public DbSet<WeavingCraftMachineParam> WeavingCraftMachineParams => Set<WeavingCraftMachineParam>();
    public DbSet<WeavingCraftYarn> WeavingCraftYarns => Set<WeavingCraftYarn>();
    public DbSet<WeavingCraftSpec> WeavingCraftSpecs => Set<WeavingCraftSpec>();
```

DbContext 用 `ApplyConfigurationsFromAssembly`（第 55 行）自动扫描配置类，**无需**逐个 `ApplyConfiguration`。新增的 4 个 Configuration 类会被自动发现，只要它们实现 `IEntityTypeConfiguration<>` 且与 DbContext 同程序集（已经是）。所以本步骤只需加 DbSet。

- [ ] **Step 6: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCESS

- [ ] **Step 7: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Persistence/Configurations/WeavingCraft*.cs backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs
git commit -m "feat(craft): 新增织造工艺库 EF 配置并注册 DbSet"
```

---

## Task 3: SeedData 常量、NumberTargetTypes 与 Migration

**Files:**
- Modify: `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs`
- Modify: `backend/src/OneCup.Application/Common/NumberTargetTypes.cs`
- Modify: `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`（种子 HasData）
- Create: Migration（`dotnet ef migrations add` 生成）

**Interfaces:**
- Consumes: Task 1-2 的实体与配置；`SeedData` 既有 Guid 分配段（权限 `...0301+`、TargetType `...0201+`）
- Produces: `SeedData.PermWeavingCraftRead/Create/Update/Delete`（`...0333`-`...0336`）、`SeedData.TargetTypeWeavingCraft`（`...0209`）、`NumberTargetTypes.WeavingCraft` 常量、可应用的 Migration

**Guid 分配**（遵循 SeedData 既有递增段）：
- 权限：当前 EquipmentType 用到 `...0332`，织造工艺从 `...0333` 起 → Read=`0333`、Create=`0334`、Update=`0335`、Delete=`0336`
- TargetType：当前到 `...0208`（EquipmentType），织造用 `...0209`

- [ ] **Step 1: 在 SeedData.cs 新增权限与 TargetType Guid 常量**

修改 `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs`，在 EquipmentType 权限常量之后（`PermEquipmentTypeDelete = ...0332` 那行之后）追加：

```csharp
    // ===== Craft（工艺库）模块 =====
    public static readonly Guid PermWeavingCraftRead = Guid.Parse("00000000-0000-0000-0000-000000000333");
    public static readonly Guid PermWeavingCraftCreate = Guid.Parse("00000000-0000-0000-0000-000000000334");
    public static readonly Guid PermWeavingCraftUpdate = Guid.Parse("00000000-0000-0000-0000-000000000335");
    public static readonly Guid PermWeavingCraftDelete = Guid.Parse("00000000-0000-0000-0000-000000000336");
```

并在 TargetType 常量区（`TargetTypeEquipmentType = ...0208` 那行之后）追加：

```csharp
    // 织造工艺（工艺库）
    public static readonly Guid TargetTypeWeavingCraft = Guid.Parse("00000000-0000-0000-0000-000000000209");
```

- [ ] **Step 2: 在 NumberTargetTypes.cs 加常量**

修改 `backend/src/OneCup.Application/Common/NumberTargetTypes.cs`，在 `EquipmentType` 之后追加：

```csharp
    public const string WeavingCraft = "craft-weaving";
```

- [ ] **Step 3: 在 DbContext 种子区注册权限种子**

修改 `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs` 的 `OnModelCreating`。

在 Permission HasData 列表末尾（`PermEquipmentTypeDelete` 那行，把行尾的 `}` 改为 `,` 后追加）新增 4 条织造工艺权限：

```csharp
            // EquipmentType 模块
            new Permission { Id = SeedData.PermEquipmentTypeRead,   Code = "equipment-type:read",   Name = "查看设备类型", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermEquipmentTypeCreate, Code = "equipment-type:create", Name = "录入设备类型", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermEquipmentTypeUpdate, Code = "equipment-type:update", Name = "编辑设备类型", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermEquipmentTypeDelete, Code = "equipment-type:delete", Name = "删除设备类型", CreatedAt = SeedTimestamp },
            // Craft（工艺库）模块
            new Permission { Id = SeedData.PermWeavingCraftRead,   Code = "craft-weaving:read",   Name = "查看织造工艺", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermWeavingCraftCreate, Code = "craft-weaving:create", Name = "录入织造工艺", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermWeavingCraftUpdate, Code = "craft-weaving:update", Name = "编辑织造工艺", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermWeavingCraftDelete, Code = "craft-weaving:delete", Name = "删除织造工艺", CreatedAt = SeedTimestamp }
        );
```

- [ ] **Step 4: 给 developer 角色授予织造工艺只读**

在 `developerPerms` 数组（第 187-194 行）追加 `SeedData.PermWeavingCraftRead`：

```csharp
        var developerPerms = new[]
        {
            SeedData.PermFabricRead, SeedData.PermFabricCreate, SeedData.PermFabricUpdate, SeedData.PermFabricDelete,
            SeedData.PermMaterialRead, SeedData.PermEquipmentRead, SeedData.PermCustomerRead,
            SeedData.PermColorRead, SeedData.PermProductRead, SeedData.PermSystemAuditRead,
            SeedData.PermProcessRead,
            SeedData.PermEquipmentTypeRead,
            SeedData.PermWeavingCraftRead
        };
```

- [ ] **Step 5: 注册 NumberingTargetType 种子**

在 `NumberingTargetType` HasData 列表（第 204-213 行）末尾追加织造工艺类型：

```csharp
            new NumberingTargetType { Id = SeedData.TargetTypeWeavingCraft, Code = "craft-weaving", NameZh = "织造工艺", NameEn = "WeavingCraft", SortOrder = 9, IsActive = true, CreatedAt = SeedTimestamp }
```

（把上一行 `TargetTypeEquipmentType` 行尾的 `}` 改为 `,`）

- [ ] **Step 6: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCESS

- [ ] **Step 7: 生成 Migration**

Run（在 Infrastructure 项目目录或解决方案根，沿用项目既有方式）：
```bash
cd backend
dotnet ef migrations add AddCraftLibraryWeavingModule --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```

Expected: 在 `backend/src/OneCup.Infrastructure/Migrations/` 下生成 `<timestamp>_AddCraftLibraryWeavingModule.cs` 与 designer 文件。打开它检查：4 张表（`weaving_crafts` / `weaving_craft_machine_params` / `weaving_craft_yarns` / `weaving_craft_specs`）的 CreateTable、2 个 `ux_` 唯一索引、4 条 permission InsertData、1 条 numbering_target_type InsertData、developer 角色的 role_permissions 新增 `craft-weaving:read`。

- [ ] **Step 8: 应用 Migration 到开发库验证**

Run:
```bash
cd backend
dotnet ef database update --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```
Expected: 无报错。可选：用 `psql` 或 DBeaver 连接确认 4 张表已创建、种子数据已插入。

- [ ] **Step 9: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Persistence/SeedData.cs backend/src/OneCup.Application/Common/NumberTargetTypes.cs backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs backend/src/OneCup.Infrastructure/Migrations/*AddCraftLibraryWeavingModule*
git commit -m "feat(craft): 新增织造工艺库种子数据与迁移 AddCraftLibraryWeavingModule"
```

---

## Task 4: DTO 与 FluentValidation 校验器

**Files:**
- Create: `backend/src/OneCup.Application/Dtos/System/WeavingCraftDtos.cs`
- Create: `backend/src/OneCup.Application/Validators/System/CreateWeavingCraftRequestValidator.cs`
- Create: `backend/src/OneCup.Application/Validators/System/UpdateWeavingCraftRequestValidator.cs`

**Interfaces:**
- Consumes: Task 1 的实体类型；项目既有的 DTO 范式（`PagedResult<T>`、`CategoryCode` 可选字段，c02 标准）
- Produces: 请求/响应 DTO + 子表 DTO，供 Task 7 Service 与 Task 8 Controller 使用

参考既有 DTO 范式：`ProcessDtos.cs`、`EquipmentDtos.cs`（注意 `CategoryCode` 字段，c02 编号对象标准）。

- [ ] **Step 1: 创建 DTO 文件**

创建 `backend/src/OneCup.Application/Dtos/System/WeavingCraftDtos.cs`：

```csharp
using OneCup.Domain.Enums;

namespace OneCup.Application.Dtos.System;

// ===== 子表 DTO =====

public class MachineParamDto
{
    public Guid? Id { get; set; }           // null=新增、有值=更新、缺失=删除（PUT diff）
    public Guid EquipmentTypeId { get; set; }
    public Guid? EquipmentTemplateId { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public string? Value { get; set; }
    public Guid? UnitId { get; set; }
    public int SortOrder { get; set; }
}

public class YarnDto
{
    public Guid? Id { get; set; }
    public int Position { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid? ColorId { get; set; }
    public string? Brand { get; set; }
    public string? BatchNo { get; set; }
    public decimal? Ratio { get; set; }
    public int SortOrder { get; set; }
}

public class SpecItemDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Value { get; set; }
    public Guid? UnitId { get; set; }
    public int SortOrder { get; set; }
}

// ===== 请求 DTO =====

public class CreateWeavingCraftRequest
{
    public string Name { get; set; } = string.Empty;
    /// <summary>品类码（编号规则开启分类码段时必填，c02）</summary>
    public string? CategoryCode { get; init; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
    public string? NeedleArrangement { get; set; }
    public string? ColumnInfo { get; set; }
    public string? CylinderNeedle { get; set; }
    public string? YarnArrangement { get; set; }
    public string? YarnLength { get; set; }
    public List<MachineParamDto> MachineParams { get; set; } = new();
    public List<YarnDto> Yarns { get; set; } = new();
    public List<SpecItemDto> Specs { get; set; } = new();
}

public class UpdateWeavingCraftRequest
{
    public string Name { get; set; } = string.Empty;
    /// <summary>品类码（编辑时若规则要求分类码也需透传）</summary>
    public string? CategoryCode { get; init; }
    public CraftStatus Status { get; set; } = CraftStatus.Active;
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
    public string? NeedleArrangement { get; set; }
    public string? ColumnInfo { get; set; }
    public string? CylinderNeedle { get; set; }
    public string? YarnArrangement { get; set; }
    public string? YarnLength { get; set; }
    public List<MachineParamDto> MachineParams { get; set; } = new();
    public List<YarnDto> Yarns { get; set; } = new();
    public List<SpecItemDto> Specs { get; set; } = new();
}

// ===== 响应 DTO =====

public class WeavingCraftListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }
    /// <summary>来源工艺编号（便于列表直接展示"← ZZ-0001"，避免前端二次查询）</summary>
    public string? SourceCode { get; set; }
    public CraftStatus Status { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class WeavingCraftDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }
    public string? SourceCode { get; set; }
    public CraftStatus Status { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
    public string? NeedleArrangement { get; set; }
    public string? ColumnInfo { get; set; }
    public string? CylinderNeedle { get; set; }
    public string? YarnArrangement { get; set; }
    public string? YarnLength { get; set; }
    public List<MachineParamDto> MachineParams { get; set; } = new();
    public List<YarnDto> Yarns { get; set; } = new();
    public List<SpecItemDto> Specs { get; set; } = new();
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// ===== 溯源 DTO =====

public class CraftLineageNodeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid? SourceId { get; set; }
    public CraftStatus Status { get; set; }
}
```

- [ ] **Step 2: 创建新建校验器**

创建 `backend/src/OneCup.Application/Validators/System/CreateWeavingCraftRequestValidator.cs`：

```csharp
using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

public class CreateWeavingCraftRequestValidator : AbstractValidator<CreateWeavingCraftRequest>
{
    public CreateWeavingCraftRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Remark).MaximumLength(1000);
        RuleFor(x => x.NeedleArrangement).MaximumLength(200);
        RuleFor(x => x.ColumnInfo).MaximumLength(200);
        RuleFor(x => x.CylinderNeedle).MaximumLength(200);
        RuleFor(x => x.YarnArrangement).MaximumLength(500);
        RuleFor(x => x.YarnLength).MaximumLength(100);

        RuleForEach(x => x.MachineParams).SetValidator(new MachineParamDtoValidator());
        RuleForEach(x => x.Yarns).SetValidator(new YarnDtoValidator());
        RuleForEach(x => x.Specs).SetValidator(new SpecItemDtoValidator());
    }
}

public class MachineParamDtoValidator : AbstractValidator<MachineParamDto>
{
    public MachineParamDtoValidator()
    {
        RuleFor(x => x.EquipmentTypeId).NotEmpty();
        RuleFor(x => x.ParameterName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Value).MaximumLength(500);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class YarnDtoValidator : AbstractValidator<YarnDto>
{
    public YarnDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Brand).MaximumLength(200);
        RuleFor(x => x.BatchNo).MaximumLength(200);
        RuleFor(x => x.Ratio).InclusiveBetween(0, 100);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}

public class SpecItemDtoValidator : AbstractValidator<SpecItemDto>
{
    public SpecItemDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Value).MaximumLength(500);
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
```

- [ ] **Step 3: 创建编辑校验器**

创建 `backend/src/OneCup.Application/Validators/System/UpdateWeavingCraftRequestValidator.cs`：

```csharp
using FluentValidation;
using OneCup.Application.Dtos.System;
using OneCup.Domain.Enums;

namespace OneCup.Application.Validators.System;

public class UpdateWeavingCraftRequestValidator : AbstractValidator<UpdateWeavingCraftRequest>
{
    public UpdateWeavingCraftRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Status).IsInEnum().Must(s => s != default).WithMessage("Status 不能为默认值 0");
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleFor(x => x.Remark).MaximumLength(1000);
        RuleFor(x => x.NeedleArrangement).MaximumLength(200);
        RuleFor(x => x.ColumnInfo).MaximumLength(200);
        RuleFor(x => x.CylinderNeedle).MaximumLength(200);
        RuleFor(x => x.YarnArrangement).MaximumLength(500);
        RuleFor(x => x.YarnLength).MaximumLength(100);

        RuleForEach(x => x.MachineParams).SetValidator(new MachineParamDtoValidator());
        RuleForEach(x => x.Yarns).SetValidator(new YarnDtoValidator());
        RuleForEach(x => x.Specs).SetValidator(new SpecItemDtoValidator());
    }
}
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCESS

- [ ] **Step 5: Commit**

```bash
git add backend/src/OneCup.Application/Dtos/System/WeavingCraftDtos.cs backend/src/OneCup.Application/Validators/System/*WeavingCraft*
git commit -m "feat(craft): 新增织造工艺库 DTO 与校验器"
```

---

## Task 5: 查询规范 Specs

**Files:**
- Create: `backend/src/OneCup.Application/Specifications/WeavingCraftSpecs.cs`

**Interfaces:**
- Consumes: Task 1 的 `WeavingCraft`；`ISpecification<T>` / `Specification<T>` 基类（既有）
- Produces: 5 个 Spec 类，供 Task 7 Service 使用

参考既有范式：`EquipmentTypeSpecs.cs`（FilterSpec/PagedSpec/ActiveSpec/ByIdSpec/ByNameSpec 五件套）。先读 `EquipmentTypeSpecs.cs` 与 `ISpecification.cs` 确认基类签名，再写。

- [ ] **Step 1: 读既有 Spec 范式（理解基类方法名）**

Run: `cat backend/src/OneCup.Application/Specifications/Specification.cs`
与: `cat backend/src/OneCup.Application/Specifications/EquipmentTypeSpecs.cs`

要点（已在下方代码体现）：
- 基类方法名是 `ApplyCriteria` / `ApplyInclude` / `ApplyOrderBy` / `ApplyPaging`
- `Criteria` 是**单个**表达式，多条件用 `&&` 组合在**一个 lambda** 里（不是多次 ApplyCriteria，会覆盖）
- `ApplyPaging(page, pageSize)` 内部已做 `(page-1)*pageSize`，直接传 page
- `ApplyInclude` 接收导航属性名字符串（如 `nameof(WeavingCraft.MachineParams)`）
- ByNameSpec 配合 Repository 的 `AnyIgnoringFiltersAsync` 用于唯一性预检（绕过软删除查询过滤器）

- [ ] **Step 2: 创建 Specs 文件**

创建 `backend/src/OneCup.Application/Specifications/WeavingCraftSpecs.cs`（已核对 `Specification<T>` 基类：单 Criteria 内 `&&` 组合多条件；方法名 `ApplyCriteria`/`ApplyInclude`/`ApplyOrderBy`/`ApplyPaging`；`ApplyPaging(page, pageSize)` 内部已做 `(page-1)*pageSize`）：

```csharp
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;

namespace OneCup.Application.Specifications;

/// <summary>织造工艺过滤（仅过滤，不含分页）。用于 CountAsync。</summary>
public class WeavingCraftFilterSpec : Specification<WeavingCraft>
{
    public WeavingCraftFilterSpec(string? keyword, CraftStatus? status)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(c =>
            (kw == null || c.Code.Contains(kw) || c.Name.Contains(kw)) &&
            (status == null || c.Status == status.Value));
    }
}

/// <summary>织造工艺分页查询（含过滤，按 SortOrder 升序）。</summary>
public class WeavingCraftPagedSpec : Specification<WeavingCraft>
{
    public WeavingCraftPagedSpec(string? keyword, CraftStatus? status, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(c =>
            (kw == null || c.Code.Contains(kw) || c.Name.Contains(kw)) &&
            (status == null || c.Status == status.Value));
        ApplyOrderBy(c => c.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>全部启用项（引用选择器用，按 SortOrder 升序）。</summary>
public class WeavingCraftActiveSpec : Specification<WeavingCraft>
{
    public WeavingCraftActiveSpec()
    {
        ApplyCriteria(c => c.Status == CraftStatus.Active);
        ApplyOrderBy(c => c.SortOrder);
    }
}

/// <summary>按 Id 查询（tracked，Include 全部子表）。</summary>
public class WeavingCraftByIdSpec : Specification<WeavingCraft>
{
    public WeavingCraftByIdSpec(Guid id)
    {
        ApplyCriteria(c => c.Id == id);
        ApplyInclude(nameof(WeavingCraft.MachineParams));
        ApplyInclude(nameof(WeavingCraft.Yarns));
        ApplyInclude(nameof(WeavingCraft.Specs));
    }
}

/// <summary>名称唯一性校验（配合 AnyIgnoringFiltersAsync 绕过软删除）。</summary>
public class WeavingCraftByNameSpec : Specification<WeavingCraft>
{
    public WeavingCraftByNameSpec(string name, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(c => c.Name == name && (exclude == null || c.Id != exclude.Value));
    }
}
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCESS（若编译失败，多半是 Spec 基类方法名不匹配，按 Step 1 读到的实际签名修正）

- [ ] **Step 4: Commit**

```bash
git add backend/src/OneCup.Application/Specifications/WeavingCraftSpecs.cs
git commit -m "feat(craft): 新增织造工艺库查询规范"
```

---

## Task 6: fork 溯源查询工具 CraftLineageQuery（TDD）

**Files:**
- Create: `backend/src/OneCup.Application/Services/CraftLineageQuery.cs`
- Create: `backend/tests/OneCup.UnitTests/Craft/CraftTestHelper.cs`
- Create: `backend/tests/OneCup.UnitTests/Craft/CraftLineageQueryTests.cs`

**Interfaces:**
- Consumes: Task 1 的 `WeavingCraft`（`SourceId` 字段）；`IRepository<WeavingCraft>` 的 `Query()` 逃生舱
- Produces: `CraftLineageQuery.GetLineageAsync(Guid craftId, ct)` 返回 `List<CraftLineageNodeDto>`（含祖先 + 自身 + 后代的家族 DAG 节点），供 Task 7 Service 调用、未来溯源图谱可视化消费

**设计**：沿 `SourceId` 链双向 BFS——向上找祖先（沿 `SourceId` 指针走），向下找后代（找所有 `SourceId == 当前节点` 的节点）。返回扁平节点列表，前端自行渲染为树/DAG（节点带 `SourceId`，足以还原边）。

这是 Task 7 之前要就绪的工具，独立可测（纯查询逻辑）。

- [ ] **Step 1: 创建测试 Helper**

创建 `backend/tests/OneCup.UnitTests/Craft/CraftTestHelper.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Craft;

/// <summary>
/// 工艺库模块测试共享辅助。结构与 EquipmentTestHelper 一致。
/// FakeNumberingService 支持 Prefix 配置（ZZ- 给织造工艺）。
/// </summary>
internal static class CraftTestHelper
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
    public PreviewResult? LastPreview { get; set; }
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
    {
        return Task.FromResult(LastPreview ?? new PreviewResult { Code = $"{_prefix}0001", IncludeCategory = false });
    }
}
```

> ⚠ 实施时先核对 `PreviewResult` 的精确字段名（`Code`/`IncludeCategory`），读 `backend/src/OneCup.Application/Dtos/System/NumberingDtos.cs` 里的 `PreviewResult` 定义，按实际属性名调整。

- [ ] **Step 2: 写溯源查询的失败测试**

创建 `backend/tests/OneCup.UnitTests/Craft/CraftLineageQueryTests.cs`：

```csharp
using OneCup.Application.Dtos.System;
using OneCup.Application.Services;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Craft;

public class CraftLineageQueryTests
{
    private static WeavingCraft Seed(OneCupDbContext db, string code, string name, Guid? sourceId)
    {
        var c = new WeavingCraft { Code = code, Name = name, SourceId = sourceId, Status = CraftStatus.Active };
        db.WeavingCrafts.Add(c);
        return c;
    }

    [Fact]
    public async Task GetLineageAsync_returns_ancestors_self_and_descendants()
    {
        // 家族树：
        //   ZZ-0001 (原创)
        //     ├── ZZ-0002 (fork 自 0001)
        //     │     ├── ZZ-0004 (fork 自 0002)
        //     │     └── ZZ-0005 (fork 自 0002)
        //     └── ZZ-0003 (fork 自 0001)
        using var db = CraftTestHelper.CreateDb("lineage");
        var c1 = Seed(db, "ZZ-0001", "原创", null);
        var c2 = Seed(db, "ZZ-0002", "v2", c1.Id);
        var c3 = Seed(db, "ZZ-0003", "v3", c1.Id);
        var c4 = Seed(db, "ZZ-0004", "v4", c2.Id);
        var c5 = Seed(db, "ZZ-0005", "v5", c2.Id);
        await db.SaveChangesAsync();

        var repo = new Repository<WeavingCraft>(db);  // 见下方注：需确认 Repository 构造
        var query = new CraftLineageQuery(repo);

        // 以 c2 为焦点：祖先 c1、自身 c2、后代 c4 c5。c3 是兄弟（非祖先非后代），不含。
        var result = await query.GetLineageAsync(c2.Id, default);

        var ids = result.Select(n => n.Id).ToList();
        Assert.Contains(c1.Id, ids);
        Assert.Contains(c2.Id, ids);
        Assert.Contains(c4.Id, ids);
        Assert.Contains(c5.Id, ids);
        Assert.DoesNotContain(c3.Id, ids);
    }

    [Fact]
    public async Task GetLineageAsync_of_original_returns_only_self_and_descendants()
    {
        // 以原创 c1 为焦点：无祖先，自身 + 全部后代（c2 c3 c4 c5）
        using var db = CraftTestHelper.CreateDb("lineage-root");
        var c1 = Seed(db, "ZZ-0001", "原创", null);
        var c2 = Seed(db, "ZZ-0002", "v2", c1.Id);
        var c3 = Seed(db, "ZZ-0003", "v3", c1.Id);
        var c4 = Seed(db, "ZZ-0004", "v4", c2.Id);
        await db.SaveChangesAsync();

        var repo = new Repository<WeavingCraft>(db);
        var query = new CraftLineageQuery(repo);

        var result = await query.GetLineageAsync(c1.Id, default);

        var ids = result.Select(n => n.Id).ToList();
        Assert.Equal(4, ids.Count);
        Assert.Contains(c1.Id, ids);
        Assert.Contains(c2.Id, ids);
        Assert.Contains(c3.Id, ids);
        Assert.Contains(c4.Id, ids);
    }

    [Fact]
    public async Task GetLineageAsync_node_fields_populated()
    {
        using var db = CraftTestHelper.CreateDb("lineage-fields");
        var c1 = Seed(db, "ZZ-0001", "原创", null);
        await db.SaveChangesAsync();

        var repo = new Repository<WeavingCraft>(db);
        var query = new CraftLineageQuery(repo);

        var result = await query.GetLineageAsync(c1.Id, default);
        var node = result.Single();

        Assert.Equal(c1.Code, node.Code);
        Assert.Equal(c1.Name, node.Name);
        Assert.Equal(c1.Status, node.Status);
        Assert.Null(node.SourceId);
    }
}
```

> ⚠ 测试里直接 `new Repository<WeavingCraft>(db)`。实施时先 `cat backend/src/OneCup.Infrastructure/Persistence/Repository.cs` 的构造函数签名确认（应该是接收 `OneCupDbContext`）。若构造不同，调整测试。**Repository 是 Infrastructure 层类，测试项目已引用 Infrastructure（EquipmentTestHelper 就用了 OneCupDbContext），可直接 new。**

- [ ] **Step 3: 运行测试确认失败**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "CraftLineageQueryTests" -v n`
Expected: 编译失败（`CraftLineageQuery` 类不存在）

- [ ] **Step 4: 实现 CraftLineageQuery**

创建 `backend/src/OneCup.Application/Services/CraftLineageQuery.cs`：

```csharp
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Domain.Entities;

namespace OneCup.Application.Services;

/// <summary>
/// 工艺 fork 溯源查询工具（方向文档决策2、§3.4）。
/// 沿 SourceId 链双向 BFS：向上找祖先、向下找后代，返回扁平节点列表。
/// 三种工艺（织造/流程/染色）可复用此模式（参数化实体类型，但本计划只服务织造）。
/// </summary>
public class CraftLineageQuery
{
    private readonly IRepository<WeavingCraft> _crafts;

    public CraftLineageQuery(IRepository<WeavingCraft> crafts)
    {
        _crafts = crafts;
    }

    public async Task<List<CraftLineageNodeDto>> GetLineageAsync(Guid craftId, CancellationToken ct = default)
    {
        var result = new List<CraftLineageNodeDto>();
        var visited = new HashSet<Guid>();

        // 逃生舱：一次取全部非软删工艺到内存做图遍历（工艺库数据量有限，可接受）。
        // 若未来数据量增长，可改为按需查询祖先链 + 反查后代集。
        var all = _crafts.Query().ToList();
        var byId = all.ToDictionary(c => c.Id);

        if (!byId.ContainsKey(craftId))
        {
            return result; // 不存在则返回空（Service 层会先做存在性校验并 404）
        }

        var queue = new Queue<Guid>();
        queue.Enqueue(craftId);

        // 第一阶段：向下找后代（BFS，沿"被谁 fork"展开）
        // 同时收集起点；祖先链单独走第二阶段
        while (queue.Count > 0)
        {
            var currentId = queue.Dequeue();
            if (!visited.Add(currentId)) continue;
            var node = byId[currentId];
            result.Add(ToDto(node));

            // 找所有 SourceId 指向 currentId 的节点（后代）
            foreach (var child in all.Where(c => c.SourceId == currentId && !visited.Contains(c.Id)))
            {
                queue.Enqueue(child.Id);
            }
        }

        // 第二阶段：向上找祖先（沿 SourceId 链走）
        var ancestorId = byId[craftId].SourceId;
        while (ancestorId.HasValue && byId.ContainsKey(ancestorId.Value) && visited.Add(ancestorId.Value))
        {
            result.Add(ToDto(byId[ancestorId.Value]));
            ancestorId = byId[ancestorId.Value].SourceId;
        }

        return result;
    }

    private static CraftLineageNodeDto ToDto(WeavingCraft c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        Name = c.Name,
        SourceId = c.SourceId,
        Status = c.Status,
    };
}
```

- [ ] **Step 5: 运行测试确认通过**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "CraftLineageQueryTests" -v n`
Expected: 3 个测试全部 PASS

- [ ] **Step 6: Commit**

```bash
git add backend/src/OneCup.Application/Services/CraftLineageQuery.cs backend/tests/OneCup.UnitTests/Craft/
git commit -m "feat(craft): 新增织造工艺 fork 溯源查询工具（含 TDD 测试）"
```

---

## Task 7: WeavingCraftService（CRUD + 溯源 + 子表 diff + 禁删预检）

**Files:**
- Create: `backend/src/OneCup.Application/Interfaces/IWeavingCraftService.cs`
- Create: `backend/src/OneCup.Application/Services/WeavingCraftService.cs`
- Create: `backend/tests/OneCup.UnitTests/Craft/WeavingCraftServiceTests.cs`

**Interfaces:**
- Consumes: Task 1 实体、Task 4 DTO/Validator、Task 5 Specs、Task 6 `CraftLineageQuery`、`INumberingService`、`IRepository<WeavingCraft>`、`IUnitOfWork`、`NumberTargetTypes.WeavingCraft`
- Produces: `IWeavingCraftService`（GetList/GetById/GetActive/Create/Update/Delete/GetLineage），供 Task 8 Controller 使用

**核心逻辑**：
- Create：名称查重（AnyIgnoringFiltersAsync 绕过软删除）→ 事务内取号 + 落库主体 + 同步子表
- Update：校验 → 取主体（Include 子表）→ 名称查重（排除自身）→ 字段赋值 → **子表整表 diff**（null=新增、有值=更新、缺失=删除）
- Delete：本计划范围内无引用者（快照计划才有），软删除即可
- 子表 diff 是最复杂部分，抽出私有 `SyncMachineParams` / `SyncYarns` / `SyncSpecs` 方法（参考 `EquipmentTypeService.SyncParameters` line 197-245）

- [ ] **Step 1: 写 Service 失败测试**

创建 `backend/tests/OneCup.UnitTests/Craft/WeavingCraftServiceTests.cs`：

```csharp
using OneCup.Application.Dtos.System;
using OneCup.Application.Services;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;  // Repository<> 与 UnitOfWork 均在此命名空间

namespace OneCup.UnitTests.Craft;

public class WeavingCraftServiceTests
{
    private static (WeavingCraftService svc, OneCupDbContext db, FakeNumberingService numbering) BuildSut(string namePrefix)
    {
        var db = CraftTestHelper.CreateDb(namePrefix);
        var numbering = new FakeNumberingService("ZZ-");
        var repo = new Repository<WeavingCraft>(db);
        var uow = new UnitOfWork(db);  // 确认实际构造
        var lineage = new CraftLineageQuery(repo);
        var svc = new WeavingCraftService(repo, uow, numbering, lineage);
        return (svc, db, numbering);
    }

    [Fact]
    public async Task CreateAsync_generates_code_and_persists_with_subtables()
    {
        var (svc, db, _) = BuildSut("create");
        var req = new CreateWeavingCraftRequest
        {
            Name = "平纹标准",
            MachineParams = { new MachineParamDto { EquipmentTypeId = Guid.NewGuid(), ParameterName = "车速", Value = "80" } },
            Yarns = { new YarnDto { Position = 1, Name = "纱A", Ratio = 100m } },
            Specs = { new SpecItemDto { Name = "下机克重", Value = "180", UnitId = Guid.NewGuid() } },
        };

        var result = await svc.CreateAsync(req, default);

        Assert.Equal("ZZ-0001", result.Code);
        Assert.Equal("平纹标准", result.Name);
        Assert.Single(result.MachineParams);
        Assert.Single(result.Yarns);
        Assert.Single(result.Specs);
    }

    [Fact]
    public async Task CreateAsync_rejects_duplicate_name()
    {
        var (svc, db, _) = BuildSut("dup");
        await svc.CreateAsync(new CreateWeavingCraftRequest { Name = "平纹标准" }, default);

        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(new CreateWeavingCraftRequest { Name = "平纹标准" }, default));
        Assert.Contains("已存在", ex.Message);
    }

    [Fact]
    public async Task UpdateAsync_diffs_subtables_add_update_remove()
    {
        var (svc, db, _) = BuildSut("diff");
        var created = await svc.CreateAsync(new CreateWeavingCraftRequest
        {
            Name = "原",
            Yarns =
            {
                new YarnDto { Position = 1, Name = "保留" },
                new YarnDto { Position = 2, Name = "删除" },
            }
        }, default);
        var keepId = created.Yarns[0].Id!.Value;
        var removeId = created.Yarns[1].Id!.Value;

        // 保留 keepId（改 Ratio）、删除 removeId（不在列表里）、新增一条
        var updated = await svc.UpdateAsync(created.Id, new UpdateWeavingCraftRequest
        {
            Name = "原",
            Status = CraftStatus.Active,
            Yarns =
            {
                new YarnDto { Id = keepId, Position = 1, Name = "保留", Ratio = 50m },
                new YarnDto { Position = 3, Name = "新增" },
            }
        }, default);

        var yarns = updated.Yarns.OrderBy(y => y.Position).ToList();
        Assert.Equal(2, yarns.Count);
        Assert.Equal("保留", yarns[0].Name);
        Assert.Equal(50m, yarns[0].Ratio);
        Assert.Equal("新增", yarns[1].Name);
        Assert.DoesNotContain(updated.Yarns, y => y.Id == removeId);
    }

    [Fact]
    public async Task GetLineageAsync_returns_family()
    {
        var (svc, db, _) = BuildSut("lineage-svc");
        var c1 = await svc.CreateAsync(new CreateWeavingCraftRequest { Name = "原创" }, default);
        // 手动塞一条 fork 自 c1 的工艺（模拟归档回写诞生的工艺）
        db.WeavingCrafts.Add(new WeavingCraft { Code = "ZZ-0099", Name = "变体", SourceId = c1.Id, Status = CraftStatus.Active });
        await db.SaveChangesAsync();

        var lineage = await svc.GetLineageAsync(c1.Id, default);

        Assert.Equal(2, lineage.Count);
        Assert.Contains(lineage, n => n.Code == "ZZ-0001");
        Assert.Contains(lineage, n => n.Code == "ZZ-0099");
    }

    [Fact]
    public async Task GetActive_returns_only_active()
    {
        var (svc, db, _) = BuildSut("active");
        await svc.CreateAsync(new CreateWeavingCraftRequest { Name = "启用" }, default);
        var c2 = await svc.CreateAsync(new CreateWeavingCraftRequest { Name = "将停用" }, default);
        await svc.UpdateAsync(c2.Id, new UpdateWeavingCraftRequest { Name = "将停用", Status = CraftStatus.Inactive }, default);

        var active = await svc.GetActiveAsync(default);

        Assert.Single(active);
        Assert.Equal("启用", active[0].Name);
    }
}
```

> ⚠ 实施时核对：①`Repository<>` 命名空间（grep `namespace` in `Repository.cs`）②`UnitOfWork` 构造签名 ③`UnitOfWork` 是否需要额外参数。把测试里的 `new Repository<WeavingCraft>(db)` / `new UnitOfWork(db)` 调成与实际一致。

- [ ] **Step 2: 运行测试确认失败**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "WeavingCraftServiceTests" -v n`
Expected: 编译失败（`WeavingCraftService` 不存在）

- [ ] **Step 3: 创建服务接口**

创建 `backend/src/OneCup.Application/Interfaces/IWeavingCraftService.cs`：

```csharp
using OneCup.Application.Dtos.System;
using OneCup.Domain.Enums;

namespace OneCup.Application.Interfaces;

public interface IWeavingCraftService
{
    Task<PagedResult<WeavingCraftListItemDto>> GetListAsync(
        string? keyword, CraftStatus? status, int page, int pageSize, CancellationToken ct = default);

    Task<WeavingCraftDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<List<WeavingCraftListItemDto>> GetActiveAsync(CancellationToken ct = default);

    Task<WeavingCraftDto> CreateAsync(CreateWeavingCraftRequest request, CancellationToken ct = default);

    Task<WeavingCraftDto> UpdateAsync(Guid id, UpdateWeavingCraftRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);

    Task<List<CraftLineageNodeDto>> GetLineageAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 4: 实现服务（含子表 diff）**

创建 `backend/src/OneCup.Application/Services/WeavingCraftService.cs`：

```csharp
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using OneCup.Application.Services;
using FluentValidation;

namespace OneCup.Application.Services;

/// <summary>
/// 织造工艺库服务。CreateAsync 事务内取号+落库；UpdateAsync 子表整表 diff。
/// 名称库内唯一预检用 AnyIgnoringFiltersAsync 绕过软删除。
/// 本计划范围内 DeleteAsync 软删除（快照计划接入后此处加"有引用禁删"预检）。
/// </summary>
public class WeavingCraftService : IWeavingCraftService
{
    private readonly IRepository<WeavingCraft> _crafts;
    private readonly IUnitOfWork _uow;
    private readonly INumberingService _numbering;
    private readonly CraftLineageQuery _lineage;
    private readonly IValidator<CreateWeavingCraftRequest> _createValidator;
    private readonly IValidator<UpdateWeavingCraftRequest> _updateValidator;

    public WeavingCraftService(
        IRepository<WeavingCraft> crafts,
        IUnitOfWork uow,
        INumberingService numbering,
        CraftLineageQuery lineage,
        IValidator<CreateWeavingCraftRequest> createValidator,
        IValidator<UpdateWeavingCraftRequest> updateValidator)
    {
        _crafts = crafts;
        _uow = uow;
        _numbering = numbering;
        _lineage = lineage;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PagedResult<WeavingCraftListItemDto>> GetListAsync(
        string? keyword, CraftStatus? status, int page, int pageSize, CancellationToken ct = default)
    {
        var total = await _crafts.CountAsync(new WeavingCraftFilterSpec(keyword, status), ct);
        var items = await _crafts.ListAsync(new WeavingCraftPagedSpec(keyword, status, page, pageSize), ct);

        // 批量取来源编号（避免 N+1）
        var sourceIds = items.Where(c => c.SourceId.HasValue).Select(c => c.SourceId!.Value).Distinct().ToList();
        var sourceMap = sourceIds.Any()
            ? _crafts.Query().Where(c => sourceIds.Contains(c.Id)).ToDictionary(c => c.Id, c => c.Code)
            : new Dictionary<Guid, string>();

        return new PagedResult<WeavingCraftListItemDto>
        {
            Items = items.Select(c => new WeavingCraftListItemDto
            {
                Id = c.Id,
                Code = c.Code,
                Name = c.Name,
                SourceId = c.SourceId,
                SourceCode = c.SourceId.HasValue && sourceMap.TryGetValue(c.SourceId.Value, out var sc) ? sc : null,
                Status = c.Status,
                SortOrder = c.SortOrder,
                CreatedAt = c.CreatedAt,
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<WeavingCraftDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _crafts.FirstOrDefaultAsync(new WeavingCraftByIdSpec(id), ct);
        if (c is null) return null;

        string? sourceCode = null;
        if (c.SourceId.HasValue)
        {
            sourceCode = _crafts.Query().Where(x => x.Id == c.SourceId.Value).Select(x => x.Code).FirstOrDefault();
        }

        return ToDto(c, sourceCode);
    }

    public async Task<List<WeavingCraftListItemDto>> GetActiveAsync(CancellationToken ct = default)
    {
        var items = await _crafts.ListAsync(new WeavingCraftActiveSpec(), ct);
        return items.Select(c => new WeavingCraftListItemDto
        {
            Id = c.Id,
            Code = c.Code,
            Name = c.Name,
            SourceId = c.SourceId,
            Status = c.Status,
            SortOrder = c.SortOrder,
            CreatedAt = c.CreatedAt,
        }).ToList();
    }

    public async Task<WeavingCraftDto> CreateAsync(CreateWeavingCraftRequest request, CancellationToken ct = default)
    {
        await _createValidator.EnsureValidAsync(request, ct);

        if (await _crafts.AnyIgnoringFiltersAsync(new WeavingCraftByNameSpec(request.Name), ct))
        {
            throw new DomainException($"织造工艺名称「{request.Name}」已存在");
        }

        Guid createdId = Guid.Empty;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            var code = await _numbering.GenerateAsync(NumberTargetTypes.WeavingCraft, request.CategoryCode, ct);
            var craft = new WeavingCraft
            {
                Code = code,
                Name = request.Name,
                SortOrder = request.SortOrder,
                Remark = request.Remark,
                NeedleArrangement = request.NeedleArrangement,
                ColumnInfo = request.ColumnInfo,
                CylinderNeedle = request.CylinderNeedle,
                YarnArrangement = request.YarnArrangement,
                YarnLength = request.YarnLength,
            };
            ApplySubtables(craft, request.MachineParams, request.Yarns, request.Specs, assignNewIds: true);
            await _crafts.AddAsync(craft, ct);
            await _uow.SaveChangesAsync(ct);
            createdId = craft.Id;
        }, ct);

        return await GetByIdAsync(createdId, ct) ?? throw new DomainException("织造工艺创建失败");
    }

    public async Task<WeavingCraftDto> UpdateAsync(Guid id, UpdateWeavingCraftRequest request, CancellationToken ct = default)
    {
        await _updateValidator.EnsureValidAsync(request, ct);

        var craft = await _crafts.FirstOrDefaultAsync(new WeavingCraftByIdSpec(id), ct)
            ?? throw new DomainException("织造工艺不存在");

        if (await _crafts.AnyIgnoringFiltersAsync(new WeavingCraftByNameSpec(request.Name, id), ct))
        {
            throw new DomainException($"织造工艺名称「{request.Name}」已存在");
        }

        craft.Name = request.Name;
        craft.Status = request.Status;
        craft.SortOrder = request.SortOrder;
        craft.Remark = request.Remark;
        craft.NeedleArrangement = request.NeedleArrangement;
        craft.ColumnInfo = request.ColumnInfo;
        craft.CylinderNeedle = request.CylinderNeedle;
        craft.YarnArrangement = request.YarnArrangement;
        craft.YarnLength = request.YarnLength;

        // 子表整表 diff（null=新增、有值=更新、缺失=删除），同 EquipmentTypeService.SyncParameters 范式
        SyncSubtable(craft.MachineParams, request.MachineParams,
            (e, dto) => { e.EquipmentTypeId = dto.EquipmentTypeId; e.EquipmentTemplateId = dto.EquipmentTemplateId;
                          e.ParameterName = dto.ParameterName; e.Value = dto.Value; e.UnitId = dto.UnitId; e.SortOrder = dto.SortOrder; },
            dto => new WeavingCraftMachineParam { WeavingCraftId = id },
            id => Guid.NewGuid(),  // 新增项分配 Id
            craft);
        SyncSubtable(craft.Yarns, request.Yarns,
            (e, dto) => { e.Position = dto.Position; e.Name = dto.Name; e.ColorId = dto.ColorId;
                          e.Brand = dto.Brand; e.BatchNo = dto.BatchNo; e.Ratio = dto.Ratio; e.SortOrder = dto.SortOrder; },
            dto => new WeavingCraftYarn { WeavingCraftId = id },
            id => Guid.NewGuid(),
            craft);
        SyncSubtable(craft.Specs, request.Specs,
            (e, dto) => { e.Name = dto.Name; e.Value = dto.Value; e.UnitId = dto.UnitId; e.SortOrder = dto.SortOrder; },
            dto => new WeavingCraftSpec { WeavingCraftId = id },
            id => Guid.NewGuid(),
            craft);

        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct) ?? throw new DomainException("织造工艺更新失败");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var craft = await _crafts.GetByIdAsync(id, ct)
            ?? throw new DomainException("织造工艺不存在");

        // 注：本计划范围内无快照引用者，软删除即可。
        // 快照计划接入后，在此加预检：若有 WeavingCraftSnapshot.SourceCraftId == id 的引用，抛 DomainException 禁删。
        craft.IsDeleted = true;
        await _uow.SaveChangesAsync(ct);
    }

    public Task<List<CraftLineageNodeDto>> GetLineageAsync(Guid id, CancellationToken ct = default)
        => _lineage.GetLineageAsync(id, ct);

    // ===== 子表 diff 工具 =====

    /// <summary>
    /// 通用子表整表 diff：existing 为当前加载的子表，incoming 为请求 DTO 列表。
    /// dto.Id == null → 新增；dto.Id 有值且在 existing 中 → 更新；existing 中未被 incoming 引用的 → 删除。
    /// </summary>
    private static void SyncSubtable<TEntity, TDto>(
        List<TEntity> existing,
        List<TDto> incoming,
        Action<TEntity, TDto> applyFields,
        Func<TDto, TEntity> createEntity,
        Func<Guid, Guid> _unused,  // 占位，确保签名清晰；新增项的 Id 由 BaseEntity 构造函数默认值生成
        WeavingCraft parent)
        where TEntity : BaseEntity
    {
        var incomingIds = incoming.Where(d => GetDtoId(d).HasValue).Select(d => GetDtoId(d)!.Value).ToHashSet();

        // 删除：existing 中 Id 不在 incomingIds 中的
        var toRemove = existing.Where(e => !incomingIds.Contains(e.Id)).ToList();
        foreach (var e in toRemove) existing.Remove(e);

        // 更新：incoming 中 Id 匹配 existing 的
        var existingMap = existing.ToDictionary(e => e.Id);
        foreach (var dto in incoming)
        {
            var dtoId = GetDtoId(dto);
            if (dtoId.HasValue && existingMap.TryGetValue(dtoId.Value, out var e))
            {
                applyFields(e, dto);
            }
            else
            {
                // 新增
                var newEntity = createEntity(dto);
                applyFields(newEntity, dto);
                existing.Add(newEntity);
            }
        }
    }

    // 反射取 DTO 的 Id（所有子表 DTO 都有 Guid? Id）。避免给每个子表写一份泛型约束。
    private static Guid? GetDtoId<TDto>(TDto dto) =>
        (dto as dynamic)?.Id as Guid?;

    private static void ApplySubtables(
        WeavingCraft craft,
        List<MachineParamDto> machineParams,
        List<YarnDto> yarns,
        List<SpecItemDto> specs,
        bool assignNewIds)
    {
        foreach (var dto in machineParams)
        {
            craft.MachineParams.Add(new WeavingCraftMachineParam
            {
                WeavingCraftId = craft.Id,
                EquipmentTypeId = dto.EquipmentTypeId,
                EquipmentTemplateId = dto.EquipmentTemplateId,
                ParameterName = dto.ParameterName,
                Value = dto.Value,
                UnitId = dto.UnitId,
                SortOrder = dto.SortOrder,
            });
        }
        foreach (var dto in yarns)
        {
            craft.Yarns.Add(new WeavingCraftYarn
            {
                WeavingCraftId = craft.Id,
                Position = dto.Position,
                Name = dto.Name,
                ColorId = dto.ColorId,
                Brand = dto.Brand,
                BatchNo = dto.BatchNo,
                Ratio = dto.Ratio,
                SortOrder = dto.SortOrder,
            });
        }
        foreach (var dto in specs)
        {
            craft.Specs.Add(new WeavingCraftSpec
            {
                WeavingCraftId = craft.Id,
                Name = dto.Name,
                Value = dto.Value,
                UnitId = dto.UnitId,
                SortOrder = dto.SortOrder,
            });
        }
    }

    private static WeavingCraftDto ToDto(WeavingCraft c, string? sourceCode) => new()
    {
        Id = c.Id,
        Code = c.Code,
        Name = c.Name,
        SourceId = c.SourceId,
        SourceCode = sourceCode,
        Status = c.Status,
        SortOrder = c.SortOrder,
        Remark = c.Remark,
        NeedleArrangement = c.NeedleArrangement,
        ColumnInfo = c.ColumnInfo,
        CylinderNeedle = c.CylinderNeedle,
        YarnArrangement = c.YarnArrangement,
        YarnLength = c.YarnLength,
        MachineParams = c.MachineParams.Select(m => new MachineParamDto
        {
            Id = m.Id, EquipmentTypeId = m.EquipmentTypeId, EquipmentTemplateId = m.EquipmentTemplateId,
            ParameterName = m.ParameterName, Value = m.Value, UnitId = m.UnitId, SortOrder = m.SortOrder,
        }).ToList(),
        Yarns = c.Yarns.Select(y => new YarnDto
        {
            Id = y.Id, Position = y.Position, Name = y.Name, ColorId = y.ColorId,
            Brand = y.Brand, BatchNo = y.BatchNo, Ratio = y.Ratio, SortOrder = y.SortOrder,
        }).ToList(),
        Specs = c.Specs.Select(s => new SpecItemDto
        {
            Id = s.Id, Name = s.Name, Value = s.Value, UnitId = s.UnitId, SortOrder = s.SortOrder,
        }).ToList(),
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
    };
}
```

> ⚠ `SyncSubtable` 用反射（`as dynamic`）取 DTO 的 Id，简化三份泛型。实施时若团队偏好强类型，可改成给三个子表 DTO 实现公共 `IHasOptionalId { Guid? Id { get; } }` 接口，再 where TDto : IHasOptionalId——这是等价的改进，实施时可选择。`_unused` 参数是冗余的，实施时删除。

- [ ] **Step 5: 运行测试确认通过**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "WeavingCraftServiceTests" -v n`
Expected: 5 个测试全部 PASS

- [ ] **Step 6: Commit**

```bash
git add backend/src/OneCup.Application/Interfaces/IWeavingCraftService.cs backend/src/OneCup.Application/Services/WeavingCraftService.cs backend/tests/OneCup.UnitTests/Craft/WeavingCraftServiceTests.cs
git commit -m "feat(craft): 新增织造工艺库 Service（CRUD + 溯源 + 子表 diff）"
```

---

## Task 8: Controller + DI 注册 + 权限策略

**Files:**
- Create: `backend/src/OneCup.Api/Controllers/WeavingCraftsController.cs`
- Modify: `backend/src/OneCup.Api/Program.cs`（DI 注册 + AddPolicy）

**Interfaces:**
- Consumes: Task 7 的 `IWeavingCraftService`；既有 `[Audit]` filter、`[Authorize(Policy=...)]` 范式
- Produces: HTTP 端点（GET 列表/详情/启用项/溯源、POST、PUT、DELETE）+ 可用的 DI/权限配置。本计划完成标志。

- [ ] **Step 1: 创建 Controller**

创建 `backend/src/OneCup.Api/Controllers/WeavingCraftsController.cs`：

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Api.Filters;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Domain.Enums;

namespace OneCup.Api.Controllers;

/// <summary>
/// 织造工艺库端点。类级需 craft-weaving:read；写操作需对应权限。
/// </summary>
[ApiController]
[Route("api/weaving-crafts")]
[Authorize(Policy = "craft-weaving:read")]
public class WeavingCraftsController : ControllerBase
{
    private readonly IWeavingCraftService _service;

    public WeavingCraftsController(IWeavingCraftService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? keyword,
        [FromQuery] CraftStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _service.GetListAsync(keyword, status, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken ct)
    {
        var result = await _service.GetActiveAsync(ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/lineage")]
    public async Task<IActionResult> GetLineage(Guid id, CancellationToken ct)
    {
        var result = await _service.GetLineageAsync(id, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var craft = await _service.GetByIdAsync(id, ct);
        return craft is null ? NotFound() : Ok(craft);
    }

    [Audit(Module = "WeavingCraft", Action = "Create", TargetType = "WeavingCraft")]
    [Authorize(Policy = "craft-weaving:create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateWeavingCraftRequest request, CancellationToken ct)
    {
        var craft = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = craft.Id }, craft);
    }

    [Audit(Module = "WeavingCraft", Action = "Update", TargetType = "WeavingCraft")]
    [Authorize(Policy = "craft-weaving:update")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateWeavingCraftRequest request, CancellationToken ct)
    {
        var craft = await _service.UpdateAsync(id, request, ct);
        return Ok(craft);
    }

    [Audit(Module = "WeavingCraft", Action = "Delete", TargetType = "WeavingCraft")]
    [Authorize(Policy = "craft-weaving:delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
```

- [ ] **Step 2: 在 Program.cs 注册 DI**

修改 `backend/src/OneCup.Api/Program.cs`。在 `AddScoped` 区域（Process/Material 等服务注册之后，搜索 `IProcessService, ProcessService` 那行附近）追加：

```csharp
// ===== Craft（工艺库）模块 =====
builder.Services.AddScoped<IWeavingCraftService, WeavingCraftService>();
builder.Services.AddScoped<CraftLineageQuery>();
```

> `CraftLineageQuery` 也要注册（`WeavingCraftService` 构造函数依赖它）。

- [ ] **Step 3: 在 Program.cs 注册权限策略**

在 `AddPolicy` 区域（搜索 `options.AddPolicy("equipment-type:delete"` 那行之后，或任意 `craft` 相关策略位置）追加 4 条：

```csharp
    options.AddPolicy("craft-weaving:read", p => p.RequireClaim("perm_codes", "craft-weaving:read"));
    options.AddPolicy("craft-weaving:create", p => p.RequireClaim("perm_codes", "craft-weaving:create"));
    options.AddPolicy("craft-weaving:update", p => p.RequireClaim("perm_codes", "craft-weaving:update"));
    options.AddPolicy("craft-weaving:delete", p => p.RequireClaim("perm_codes", "craft-weaving:delete"));
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCESS

- [ ] **Step 5: 全量测试**

Run: `dotnet test backend/OneCup.sln -v n`
Expected: 全部测试 PASS（含既有测试无回归 + 新增 Craft 测试）

- [ ] **Step 6: 冒烟测试（手动，验证端到端）**

启动 API（`dotnet run --project backend/src/OneCup.Api`），用 admin 账号登录拿 token，调：

```bash
# 1. 新建织造工艺
curl -X POST http://localhost:5000/api/weaving-crafts \
  -H "Authorization: Bearer <admin-token>" -H "Content-Type: application/json" \
  -d '{"name":"平纹标准","machineParams":[],"yarns":[],"specs":[]}'

# 2. 查列表
curl http://localhost:5000/api/weaving-crafts -H "Authorization: Bearer <admin-token>"

# 3. 查溯源（应返回至少 1 个节点）
curl http://localhost:5000/api/weaving-crafts/<新建返回的id>/lineage -H "Authorization: Bearer <admin-token>"
```

Expected: 返回 200 + 正确数据，编号为 `ZZ-0001`（若已配置编号规则）或编号引擎返回的预览码。

- [ ] **Step 7: Commit**

```bash
git add backend/src/OneCup.Api/Controllers/WeavingCraftsController.cs backend/src/OneCup.Api/Program.cs
git commit -m "feat(craft): 新增织造工艺库 Controller + DI 注册 + 权限策略"
```

---

## 完成标志

本计划完成后，工艺库的织造工艺具备：
- ✅ 后端 CRUD API（列表/详情/启用项/新建/编辑/删除）
- ✅ fork 溯源查询（DAG 家族）
- ✅ 编号引擎接入（`craft-weaving` targetType）
- ✅ 权限控制（`craft-weaving:read/create/update/delete`，admin 通配、developer 只读）
- ✅ 子表整表 diff（织机参数/纱支/坯布规格随主体 PUT 同步增改删）
- ✅ 单元测试覆盖（溯源工具 3 个 + Service 5 个）

**不具备**（留待后续计划）：
- ❌ 快照实体与"引用即快照"机制（依赖面料开发主轴）
- ❌ 字段级 diff 驱动 `IsModified`（属于快照计划）
- ❌ 归档回写（属于快照计划）
- ❌ 流程工艺、染色工艺（字段待业务确认）
- ❌ 前端页面（后端 API 稳定后另开计划）
- ❌ 溯源图谱可视化（前端图表库选型待定）

---

## 自审备注（实施者必读）

本计划基于 2026-07-06 的项目状态编写。实施前**必须**确认以下项目细节（计划中已标注 ⚠ 的位置）：

1. **PreviewResult 字段名**（Task 6 Step 1）：读 `NumberingDtos.cs` 确认 `Code`/`IncludeCategory`
2. **Repository / UnitOfWork 构造签名与命名空间**（Task 6/7）：读 `Repository.cs` / `UnitOfWork.cs`
3. **`[Audit]` filter 的 Module/Action/TargetType 约定**（Task 8）：读 `AuditLogActionFilter.cs` 确认参数含义
4. **Spec 基类方法名**（Task 5 已修正，但实施时仍建议跑一次编译确认）
5. **迁移命令的工作目录**（Task 3）：确认 `dotnet ef` 在 `backend/` 还是项目根执行（看既有迁移怎么生成的）

若上述任何一项与计划代码不符，**以项目实际代码为准调整**，计划代码是骨架不是圣旨。
