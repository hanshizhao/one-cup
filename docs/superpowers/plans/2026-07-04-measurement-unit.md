# 计量单位管理实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现计量单位字典（单表 CRUD）+ 同类换算接口，含前端管理页。

**Architecture:** 与编号字典模块架构同构的单表字典。一张 `measurement_units` 表承载单位定义与换算系数（factor 列），换算走"基准单位中转"公式 `qty × factor(from) / factor(to)`。基准唯一性、停用保护等约束在应用层保证。

**Tech Stack:** 后端 .NET 10 / EF Core / PostgreSQL / FluentValidation / xUnit（InMemory，不用 mock）；前端 React / Arco Design / Vite / Vitest。

**Spec:** `docs/superpowers/specs/2026-07-04-measurement-unit-design.md`

## Global Constraints

- **并行开发**：worktree `.worktrees/unit-mgmt`，分支 `feat/unit-mgmt`。严格遵守 `docs/parallel-dev-contract.md` §3。
- **种子 Guid**：`PermUnitRead=00000000-0000-0000-0000-000000000121`、`PermUnitWrite=...122`。**118–120 缓冲段绝不碰**。不使用 211（属编号体系，本模块不联动编号）。
- **迁移命名**：`AddUnitModule`（EF 自动加时间戳前缀）。
- **共享文件改法**：各自在文件末尾追加，用 `// ===== Unit 模块 =====` 注释标注（contract §3.3）；前端数组末尾追加（§3.4）。
- **测试模式**：InMemory + 真实 Repository/UnitOfWork，**不用 mock**（参照 `NumberingDictionaryServiceTests`）。validator 测试用 snake_case 命名。
- **code/category 创建后不可改**：UpdateRequest DTO 不含这两个字段。
- **基准约束**：每 category 有且仅一个 `is_base=true`（应用层校验，无 DB partial index）。

---

## File Structure

### 后端新增（9）
| 文件 | 职责 |
|------|------|
| `OneCup.Domain/Entities/MeasurementUnit.cs` | 实体：单位定义 + factor + is_base |
| `OneCup.Infrastructure/Persistence/Configurations/MeasurementUnitConfiguration.cs` | EF 映射：表名/列名/索引 |
| `OneCup.Application/Dtos/System/MeasurementUnitDtos.cs` | DTO：Create/Update/Convert request + UnitDto |
| `OneCup.Application/Validators/System/CreateUnitRequestValidator.cs` | 格式校验（无 IO） |
| `OneCup.Application/Specifications/MeasurementUnitSpecs.cs` | 查询规格：过滤/分页/唯一性/基准查找 |
| `OneCup.Application/Interfaces/IMeasurementUnitService.cs` | 服务接口 |
| `OneCup.Application/Services/MeasurementUnitService.cs` | 服务实现：CRUD + 换算 + 基准约束 |
| `OneCup.Api/Controllers/MeasurementUnitsController.cs` | 8 端点控制器 |
| `OneCup.UnitTests/MeasurementUnit/*.cs` | Service 测试 + Validator 测试 |

### 后端共享文件修改（4）
- `OneCup.Infrastructure/Persistence/SeedData.cs` — 末尾追加 2 个权限 Guid
- `OneCup.Infrastructure/Persistence/OneCupDbContext.cs` — 追加 DbSet + Seed() 末尾追加种子
- `OneCup.Api/Program.cs` — 追加 DI + 授权策略
- `OneCup.Infrastructure/Migrations/<ts>_AddUnitModule.cs` — EF 自动生成

### 前端新增（7）
| 文件 | 职责 |
|------|------|
| `frontend/src/api/measurementUnit.ts` | API 封装 |
| `frontend/src/pages/system/unit/index.tsx` | 页面主体 |
| `frontend/src/pages/system/unit/locale/{index,zh-CN,en-US}.ts` | 国际化 |
| `frontend/src/pages/system/unit/style/index.module.less` | 样式（复制 numbering） |
| `frontend/src/pages/system/unit/__tests__/index.test.tsx` | 布局断言测试 |

### 前端共享文件修改（3）
- `frontend/src/routes.ts` / `frontend/src/router.tsx` / `frontend/src/locale/index.ts`

---

## Task 1: 实体 MeasurementUnit

**Files:**
- Create: `backend/src/OneCup.Domain/Entities/MeasurementUnit.cs`

**Interfaces:**
- Produces: `MeasurementUnit` 实体类型（属性：Code/NameZh/NameEn/Symbol/Category/IsBase/Factor/precision/SortOrder/IsActive），继承 `BaseEntity`（Id/CreatedAt/UpdatedAt）

- [ ] **Step 1: 创建实体文件**

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 计量单位字典。code 创建后不可改。
/// 同类单位按 factor 相对基准单位换算（基准 factor=1）。
/// </summary>
public class MeasurementUnit : BaseEntity
{
    /// <summary>英文标识符，如 kg。创建后不可改</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>中文名，如"千克"</summary>
    public string NameZh { get; set; } = string.Empty;

    /// <summary>英文名，如"Kilogram"</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>符号，如 kg / m / tex</summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>单位类别，如 LENGTH/WEIGHT/YARN</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>是否为该类别的基准单位（每类有且仅有一个）</summary>
    public bool IsBase { get; set; }

    /// <summary>相对该类别基准单位的换算系数（基准=1）</summary>
    public decimal Factor { get; set; } = 1m;

    /// <summary>展示小数位数（0-6）</summary>
    public int Precision { get; set; } = 2;

    /// <summary>排序号（下拉显示顺序）</summary>
    public int SortOrder { get; set; }

    /// <summary>启停状态（停用后不参与换算、下拉不显示，不物理删除）</summary>
    public bool IsActive { get; set; } = true;
}
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build backend/src/OneCup.Domain/OneCup.Domain.csproj`
Expected: BUILD SUCCEEDED（无错误）

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Domain/Entities/MeasurementUnit.cs
git commit -m "feat(unit): MeasurementUnit 实体"
```

---

## Task 2: EF 配置 + DbContext 注册 + 种子

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/MeasurementUnitConfiguration.cs`
- Modify: `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`（追加 DbSet + Seed）
- Modify: `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs`（追加权限 Guid）

**Interfaces:**
- Consumes: `MeasurementUnit` 实体（Task 1）
- Produces: `measurement_units` 表 schema、`DbSet<MeasurementUnit>`、`SeedData.PermUnitRead/PermUnitWrite`

- [ ] **Step 1: 创建 EF 配置**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class MeasurementUnitConfiguration : IEntityTypeConfiguration<MeasurementUnit>
{
    public void Configure(EntityTypeBuilder<MeasurementUnit> builder)
    {
        builder.ToTable("measurement_units");
        builder.HasKey(u => u.Id);

        builder.Property(u => u.Id).HasColumnName("id");
        builder.Property(u => u.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(u => u.NameZh).HasColumnName("name_zh").HasMaxLength(64).IsRequired();
        builder.Property(u => u.NameEn).HasColumnName("name_en").HasMaxLength(64).IsRequired();
        builder.Property(u => u.Symbol).HasColumnName("symbol").HasMaxLength(16).IsRequired();
        builder.Property(u => u.Category).HasColumnName("category").HasMaxLength(32).IsRequired();
        builder.Property(u => u.IsBase).HasColumnName("is_base").IsRequired();
        builder.Property(u => u.Factor).HasColumnName("factor").HasPrecision(18, 8).IsRequired();
        builder.Property(u => u.Precision).HasColumnName("precision").IsRequired();
        builder.Property(u => u.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(u => u.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(u => u.CreatedAt).HasColumnName("created_at");
        builder.Property(u => u.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(u => u.Code)
            .HasDatabaseName("ux_measurement_units_code")
            .IsUnique();

        builder.HasIndex(u => u.Category)
            .HasDatabaseName("ix_measurement_units_category");
    }
}
```

- [ ] **Step 2: SeedData.cs 追加权限 Guid**

在 `SeedData.cs` 末尾（`TargetTypeProduct` 那行之后、类的右括号之前）追加：

```csharp
    // ===== Unit 模块（计量单位） =====
    public static readonly Guid PermUnitRead = Guid.Parse("00000000-0000-0000-0000-000000000121");
    public static readonly Guid PermUnitWrite = Guid.Parse("00000000-0000-0000-0000-000000000122");
```

- [ ] **Step 3: OneCupDbContext.cs 追加 DbSet**

在 `public DbSet<LoginLog> LoginLogs => Set<LoginLog>();` 之后追加：

```csharp
    // ===== Unit 模块 =====
    public DbSet<MeasurementUnit> MeasurementUnits => Set<MeasurementUnit>();
```

- [ ] **Step 4: OneCupDbContext.cs Seed() 追加种子**

在 `Seed()` 方法**末尾**（最后的 `numbering_target_types` HasData 块之后、方法右括号之前）追加：

```csharp
        // ===== Unit 模块：计量单位 =====
        modelBuilder.Entity<Permission>().HasData(
            new Permission { Id = SeedData.PermUnitRead, Code = "system:unit:view", Name = "查看计量单位", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermUnitWrite, Code = "system:unit:manage", Name = "管理计量单位", CreatedAt = SeedTimestamp }
        );

        // 19 个默认单位（6 类，每类一个基准 factor=1）
        modelBuilder.Entity<MeasurementUnit>().HasData(
            // LENGTH 长度
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010001"), Code = "meter", NameZh = "米", NameEn = "Meter", Symbol = "m", Category = "LENGTH", IsBase = true, Factor = 1m, Precision = 2, SortOrder = 1, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010002"), Code = "decimeter", NameZh = "分米", NameEn = "Decimeter", Symbol = "dm", Category = "LENGTH", IsBase = false, Factor = 0.1m, Precision = 2, SortOrder = 2, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010003"), Code = "centimeter", NameZh = "厘米", NameEn = "Centimeter", Symbol = "cm", Category = "LENGTH", IsBase = false, Factor = 0.01m, Precision = 2, SortOrder = 3, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010004"), Code = "yard", NameZh = "码", NameEn = "Yard", Symbol = "yd", Category = "LENGTH", IsBase = false, Factor = 0.9144m, Precision = 2, SortOrder = 4, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010005"), Code = "foot", NameZh = "英尺", NameEn = "Foot", Symbol = "ft", Category = "LENGTH", IsBase = false, Factor = 0.3048m, Precision = 2, SortOrder = 5, IsActive = true, CreatedAt = SeedTimestamp },
            // WEIGHT 重量
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010010"), Code = "kilogram", NameZh = "千克", NameEn = "Kilogram", Symbol = "kg", Category = "WEIGHT", IsBase = true, Factor = 1m, Precision = 2, SortOrder = 1, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010011"), Code = "gram", NameZh = "克", NameEn = "Gram", Symbol = "g", Category = "WEIGHT", IsBase = false, Factor = 0.001m, Precision = 2, SortOrder = 2, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010012"), Code = "ton", NameZh = "吨", NameEn = "Ton", Symbol = "t", Category = "WEIGHT", IsBase = false, Factor = 1000m, Precision = 2, SortOrder = 3, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010013"), Code = "pound", NameZh = "磅", NameEn = "Pound", Symbol = "lb", Category = "WEIGHT", IsBase = false, Factor = 0.453592m, Precision = 2, SortOrder = 4, IsActive = true, CreatedAt = SeedTimestamp },
            // AREA 面积
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010020"), Code = "square_meter", NameZh = "平方米", NameEn = "Square Meter", Symbol = "㎡", Category = "AREA", IsBase = true, Factor = 1m, Precision = 2, SortOrder = 1, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010021"), Code = "square_yard", NameZh = "平方码", NameEn = "Square Yard", Symbol = "yd²", Category = "AREA", IsBase = false, Factor = 0.836127m, Precision = 2, SortOrder = 2, IsActive = true, CreatedAt = SeedTimestamp },
            // COUNT 数量
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010030"), Code = "piece", NameZh = "件", NameEn = "Piece", Symbol = "件", Category = "COUNT", IsBase = true, Factor = 1m, Precision = 0, SortOrder = 1, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010031"), Code = "roll", NameZh = "卷", NameEn = "Roll", Symbol = "卷", Category = "COUNT", IsBase = false, Factor = 1m, Precision = 0, SortOrder = 2, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010032"), Code = "bolt", NameZh = "匹", NameEn = "Bolt", Symbol = "匹", Category = "COUNT", IsBase = false, Factor = 1m, Precision = 0, SortOrder = 3, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010033"), Code = "set", NameZh = "套", NameEn = "Set", Symbol = "套", Category = "COUNT", IsBase = false, Factor = 1m, Precision = 0, SortOrder = 4, IsActive = true, CreatedAt = SeedTimestamp },
            // VOLUME 体积
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010040"), Code = "liter", NameZh = "升", NameEn = "Liter", Symbol = "L", Category = "VOLUME", IsBase = true, Factor = 1m, Precision = 2, SortOrder = 1, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010041"), Code = "milliliter", NameZh = "毫升", NameEn = "Milliliter", Symbol = "mL", Category = "VOLUME", IsBase = false, Factor = 0.001m, Precision = 2, SortOrder = 2, IsActive = true, CreatedAt = SeedTimestamp },
            // YARN 纱线（定长制）
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010050"), Code = "tex", NameZh = "特", NameEn = "Tex", Symbol = "tex", Category = "YARN", IsBase = true, Factor = 1m, Precision = 2, SortOrder = 1, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010051"), Code = "dtex", NameZh = "分特", NameEn = "Decitex", Symbol = "dtex", Category = "YARN", IsBase = false, Factor = 10m, Precision = 2, SortOrder = 2, IsActive = true, CreatedAt = SeedTimestamp },
            new MeasurementUnit { Id = Guid.Parse("00000000-0000-0000-0000-000000010052"), Code = "denier", NameZh = "旦尼尔", NameEn = "Denier", Symbol = "D", Category = "YARN", IsBase = false, Factor = 9m, Precision = 2, SortOrder = 3, IsActive = true, CreatedAt = SeedTimestamp }
        );
```

> **种子 Guid 第 4 段说明**：用 `100XX` 段（10001–10052），避开权限段 101–123 与目标类型段 201–206，与编号体系种子无冲突。

- [ ] **Step 5: 验证编译**

Run: `dotnet build backend/src/OneCup.Infrastructure/OneCup.Infrastructure.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 6: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Persistence/Configurations/MeasurementUnitConfiguration.cs \
        backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs \
        backend/src/OneCup.Infrastructure/Persistence/SeedData.cs
git commit -m "feat(unit): EF 配置 + DbSet + 19 个种子单位 + 2 个权限 Guid

Guid 第4段 121/122（权限），10001-10052（单位种子，避开既有段）
factor 用 numeric(18,8)，code 唯一索引 + category 普通索引"
```

---

## Task 3: DTO

**Files:**
- Create: `backend/src/OneCup.Application/Dtos/System/MeasurementUnitDtos.cs`

**Interfaces:**
- Produces: `CreateUnitRequest`/`UpdateUnitRequest`/`UpdateDictStatusRequest`/`ConvertUnitRequest`/`ConvertUnitResult`/`UnitDto`

- [ ] **Step 1: 创建 DTO 文件**

```csharp
namespace OneCup.Application.Dtos.System;

public record CreateUnitRequest
{
    public string Code { get; init; } = string.Empty;
    public string NameZh { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string Symbol { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public bool IsBase { get; init; }
    public decimal Factor { get; init; } = 1m;
    public int Precision { get; init; } = 2;
    public int SortOrder { get; init; }
}

// code/category 不可改 → DTO 不含这两个字段
public record UpdateUnitRequest
{
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public string? Symbol { get; init; }
    public bool? IsBase { get; init; }
    public decimal? Factor { get; init; }
    public int? Precision { get; init; }
    public int? SortOrder { get; init; }
}

public record UpdateUnitStatusRequest
{
    public bool IsActive { get; init; }
}

public record ConvertUnitRequest
{
    public string FromCode { get; init; } = string.Empty;
    public string ToCode { get; init; } = string.Empty;
    public decimal Quantity { get; init; }
}

public record ConvertUnitResult
{
    public decimal Quantity { get; init; }
    public string FromCode { get; init; } = string.Empty;
    public string ToCode { get; init; } = string.Empty;
    public int Precision { get; init; }
}

public class UnitDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameZh { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Symbol { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public bool IsBase { get; set; }
    public decimal Factor { get; set; }
    public int Precision { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build backend/src/OneCup.Application/OneCup.Application.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Application/Dtos/System/MeasurementUnitDtos.cs
git commit -m "feat(unit): DTO 定义（Create/Update/Convert + UnitDto）"
```

---

## Task 4: Validator + 测试

**Files:**
- Create: `backend/src/OneCup.Application/Validators/System/CreateUnitRequestValidator.cs`
- Test: `backend/tests/OneCup.UnitTests/MeasurementUnit/CreateUnitRequestValidatorTests.cs`

**Interfaces:**
- Consumes: `CreateUnitRequest`（Task 3）
- Produces: `CreateUnitRequestValidator`（Task 6 Service 注入用）

- [ ] **Step 1: 写失败测试**

创建 `backend/tests/OneCup.UnitTests/MeasurementUnit/CreateUnitRequestValidatorTests.cs`：

```csharp
using OneCup.Application.Dtos.System;
using OneCup.Application.Validators.System;

namespace OneCup.UnitTests.MeasurementUnit;

public class CreateUnitRequestValidatorTests
{
    private readonly CreateUnitRequestValidator _validator = new();

    private static CreateUnitRequest Valid() => new()
    {
        Code = "kg", NameZh = "千克", NameEn = "Kilogram",
        Symbol = "kg", Category = "WEIGHT", IsBase = false,
        Factor = 0.001m, Precision = 2, SortOrder = 1,
    };

    [Fact]
    public void Valid_request_passes()
    {
        Assert.True(_validator.Validate(Valid()).IsValid);
    }

    [Fact]
    public void Empty_code_fails()
    {
        var req = Valid(); req.Code = "";
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Uppercase_code_fails()
    {
        var req = Valid(); req.Code = "KG";
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Empty_category_fails()
    {
        var req = Valid(); req.Category = "";
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Lowercase_category_fails()
    {
        var req = Valid(); req.Category = "weight";
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Zero_factor_fails()
    {
        var req = Valid(); req.Factor = 0m;
        Assert.False(_validator.Validate(req).IsValid);
    }

    [Fact]
    public void Precision_out_of_range_fails()
    {
        var req = Valid(); req.Precision = 7;
        Assert.False(_validator.Validate(req).IsValid);
    }
}
```

- [ ] **Step 2: 运行测试验证失败**

Run: `dotnet test backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~CreateUnitRequestValidatorTests"`
Expected: 编译失败（`CreateUnitRequestValidator` 类型不存在）

- [ ] **Step 3: 写最小实现**

创建 `backend/src/OneCup.Application/Validators/System/CreateUnitRequestValidator.cs`：

```csharp
using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>创建计量单位请求校验（仅格式，业务规则在 Service 层）。</summary>
public class CreateUnitRequestValidator : AbstractValidator<CreateUnitRequest>
{
    public CreateUnitRequestValidator()
    {
        RuleFor(x => x.Code)
            .NotEmpty().Length(1, 32)
            .Matches("^[a-z][a-z0-9_]*$").WithMessage("Code 必须为小写英文标识符（字母开头，仅含小写字母/数字/下划线）");
        RuleFor(x => x.NameZh).NotEmpty().Length(1, 64);
        RuleFor(x => x.NameEn).NotEmpty().Length(1, 64);
        RuleFor(x => x.Symbol).NotEmpty().Length(1, 16);
        RuleFor(x => x.Category)
            .NotEmpty().Length(1, 32)
            .Matches("^[A-Z][A-Z0-9_]*$").WithMessage("Category 必须为大写枚举式（字母开头，仅含大写字母/数字/下划线）");
        RuleFor(x => x.Factor).GreaterThan(0m);
        RuleFor(x => x.Precision).InclusiveBetween(0, 6);
        RuleFor(x => x.SortOrder).GreaterThanOrEqual(0);
    }
}
```

- [ ] **Step 4: 运行测试验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~CreateUnitRequestValidatorTests"`
Expected: 7 个测试全部 PASS

- [ ] **Step 5: Commit**

```bash
git add backend/src/OneCup.Application/Validators/System/CreateUnitRequestValidator.cs \
        backend/tests/OneCup.UnitTests/MeasurementUnit/CreateUnitRequestValidatorTests.cs
git commit -m "feat(unit): CreateUnitRequestValidator + 7 个测试"
```

---

## Task 5: Specifications

**Files:**
- Create: `backend/src/OneCup.Application/Specifications/MeasurementUnitSpecs.cs`

**Interfaces:**
- Consumes: `MeasurementUnit`（Task 1）、`Specification<T>` 基类（已存在）
- Produces: `UnitFilterSpec`/`UnitPagedSpec`/`UnitActiveSpec`/`UnitByIdSpec`/`UnitByCodeSpec`/`UnitBaseByCategorySpec`（Task 6 Service 用）

- [ ] **Step 1: 创建 Specs 文件**

```csharp
using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

// ── 过滤/分页 ──

/// <summary>单位过滤规格（仅 keyword/category/isActive，不含分页）。用于 CountAsync 统计总数。</summary>
public class UnitFilterSpec : Specification<MeasurementUnit>
{
    public UnitFilterSpec(string? keyword, string? category, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(u =>
            (kw == null || u.Code.Contains(kw) || u.NameZh.Contains(kw) || u.NameEn.Contains(kw)) &&
            (string.IsNullOrEmpty(category) || u.Category == category) &&
            (isActive == null || u.IsActive == isActive.Value));
    }
}

/// <summary>单位分页查询（含过滤，按 SortOrder、Code 升序）。</summary>
public class UnitPagedSpec : Specification<MeasurementUnit>
{
    public UnitPagedSpec(string? keyword, string? category, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(u =>
            (kw == null || u.Code.Contains(kw) || u.NameZh.Contains(kw) || u.NameEn.Contains(kw)) &&
            (string.IsNullOrEmpty(category) || u.Category == category) &&
            (isActive == null || u.IsActive == isActive.Value));
        ApplyOrderBy(u => u.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>全部启用单位（前端下拉用，按 SortOrder 升序）。</summary>
public class UnitActiveSpec : Specification<MeasurementUnit>
{
    public UnitActiveSpec()
    {
        ApplyCriteria(u => u.IsActive);
        ApplyOrderBy(u => u.SortOrder);
    }
}

// ── 查找/唯一性 ──

public class UnitByIdSpec : Specification<MeasurementUnit>
{
    public UnitByIdSpec(Guid id) => ApplyCriteria(u => u.Id == id);
}

/// <summary>按 code 查找单位（不含 IsActive 过滤——用于 code 唯一性校验与 ConvertAsync 区分"不存在/已停用"）。</summary>
public class UnitByCodeSpec : Specification<MeasurementUnit>
{
    public UnitByCodeSpec(string code, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(u => u.Code == code && (exclude == null || u.Id != exclude.Value));
    }
}

/// <summary>查找某 category 当前的基准单位（可选排除自身 Id）。用于基准唯一性校验。</summary>
public class UnitBaseByCategorySpec : Specification<MeasurementUnit>
{
    public UnitBaseByCategorySpec(string category, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(u => u.Category == category && u.IsBase && (exclude == null || u.Id != exclude.Value));
    }
}
```

- [ ] **Step 2: 验证编译**

Run: `dotnet build backend/src/OneCup.Application/OneCup.Application.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Application/Specifications/MeasurementUnitSpecs.cs
git commit -m "feat(unit): 查询规格（过滤/分页/唯一性/基准查找）"
```

---

## Task 6: Service 接口 + 实现 + 测试

**Files:**
- Create: `backend/src/OneCup.Application/Interfaces/IMeasurementUnitService.cs`
- Create: `backend/src/OneCup.Application/Services/MeasurementUnitService.cs`
- Test: `backend/tests/OneCup.UnitTests/MeasurementUnit/MeasurementUnitServiceTests.cs`

**Interfaces:**
- Consumes: `MeasurementUnit`（T1）、DTO（T3）、`CreateUnitRequestValidator`（T4）、Specs（T5）、`IRepository<T>`/`IUnitOfWork`/`IValidator<T>`/`EnsureValidAsync`（已存在）
- Produces: `IMeasurementUnitService`（Task 7 Controller 注入用）

- [ ] **Step 1: 写接口**

创建 `backend/src/OneCup.Application/Interfaces/IMeasurementUnitService.cs`：

```csharp
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 计量单位管理服务（CRUD + 同类换算）。
/// </summary>
public interface IMeasurementUnitService
{
    Task<PagedResult<UnitDto>> GetListAsync(
        int page, int pageSize, string? keyword, string? category, bool? isActive,
        CancellationToken ct = default);

    Task<List<UnitDto>> GetAllActiveAsync(CancellationToken ct = default);

    Task<List<string>> GetCategoriesAsync(CancellationToken ct = default);

    Task<UnitDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<UnitDto> CreateAsync(CreateUnitRequest request, CancellationToken ct = default);

    Task UpdateAsync(Guid id, UpdateUnitRequest request, CancellationToken ct = default);

    Task UpdateStatusAsync(Guid id, bool isActive, CancellationToken ct = default);

    Task<ConvertUnitResult> ConvertAsync(ConvertUnitRequest request, CancellationToken ct = default);
}
```

- [ ] **Step 2: 写失败测试**

创建 `backend/tests/OneCup.UnitTests/MeasurementUnit/MeasurementUnitServiceTests.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Application.Services;
using OneCup.Application.Validators.System;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.MeasurementUnit;

public class MeasurementUnitServiceTests
{
    private static (OneCupDbContext db, MeasurementUnitService svc) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"unit-test-{Guid.NewGuid()}")
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);
        var svc = new MeasurementUnitService(
            new Repository<Domain.Entities.MeasurementUnit>(db),
            new UnitOfWork(db),
            new CreateUnitRequestValidator());
        return (db, svc);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    private static CreateUnitRequest ValidCreate(string code, string category, bool isBase, decimal factor = 1m) => new()
    {
        Code = code, NameZh = code, NameEn = code, Symbol = code,
        Category = category, IsBase = isBase, Factor = factor,
        Precision = 2, SortOrder = 1,
    };

    // ── Create ──

    [Fact]
    public async Task CreateAsync_CreatesUnit()
    {
        var (db, svc) = Setup();
        var dto = await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        Assert.Equal("meter", dto.Code);
        Assert.True(dto.IsActive);
        Assert.True(dto.IsBase);
    }

    [Fact]
    public async Task CreateAsync_DuplicateCode_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(ValidCreate("meter", "LENGTH", false, 0.1m)));
    }

    [Fact]
    public async Task CreateAsync_BaseFactor_ForcedToOne()
    {
        var (db, svc) = Setup();
        // 即使传 factor=5，IsBase=true 时强制为 1
        var dto = await svc.CreateAsync(ValidCreate("meter", "LENGTH", true, 5m));
        Assert.Equal(1m, dto.Factor);
    }

    [Fact]
    public async Task CreateAsync_SecondBaseInCategory_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateAsync(ValidCreate("yard", "LENGTH", true)));
    }

    [Fact]
    public async Task CreateAsync_NonBase_InDifferentCategory_Ok()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        // 不同 category 各自一个基准，不冲突
        var dto = await svc.CreateAsync(ValidCreate("kg", "WEIGHT", true));
        Assert.True(dto.IsBase);
    }

    // ── Update ──

    [Fact]
    public async Task UpdateAsync_BaseFactorChange_Throws()
    {
        var (db, svc) = Setup();
        var dto = await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateAsync(dto.Id, new UpdateUnitRequest { Factor = 2m }));
    }

    [Fact]
    public async Task UpdateAsync_NonBaseFactorChange_Ok()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        var yard = await svc.CreateAsync(ValidCreate("yard", "LENGTH", false, 0.9m));
        await svc.UpdateAsync(yard.Id, new UpdateUnitRequest { Factor = 0.9144m });
        var updated = await svc.GetByIdAsync(yard.Id);
        Assert.Equal(0.9144m, updated!.Factor);
    }

    [Fact]
    public async Task UpdateAsync_SwitchBase_DemotesOldBase()
    {
        var (db, svc) = Setup();
        var meter = await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        var yard = await svc.CreateAsync(ValidCreate("yard", "LENGTH", false, 0.9144m));
        // 把 yard 设为新基准 → meter 自动降级
        await svc.UpdateAsync(yard.Id, new UpdateUnitRequest { IsBase = true });
        var oldBase = await svc.GetByIdAsync(meter.Id);
        var newBase = await svc.GetByIdAsync(yard.Id);
        Assert.False(oldBase!.IsBase);
        Assert.True(newBase!.IsBase);
        Assert.Equal(1m, newBase.Factor);
    }

    [Fact]
    public async Task UpdateAsync_RemoveLastBase_Throws()
    {
        var (db, svc) = Setup();
        var meter = await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateAsync(meter.Id, new UpdateUnitRequest { IsBase = false }));
    }

    // ── UpdateStatus ──

    [Fact]
    public async Task UpdateStatusAsync_DeactivateBase_Throws()
    {
        var (db, svc) = Setup();
        var meter = await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateStatusAsync(meter.Id, false));
    }

    [Fact]
    public async Task UpdateStatusAsync_DeactivateNonBase_Ok()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        var yard = await svc.CreateAsync(ValidCreate("yard", "LENGTH", false, 0.9144m));
        await svc.UpdateStatusAsync(yard.Id, false);
        var updated = await svc.GetByIdAsync(yard.Id);
        Assert.False(updated!.IsActive);
    }

    // ── Convert ──

    [Fact]
    public async Task ConvertAsync_SameCategory_Ok()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await svc.CreateAsync(ValidCreate("yard", "LENGTH", false, 0.9144m));
        var result = await svc.ConvertAsync(new ConvertUnitRequest
        {
            FromCode = "yard", ToCode = "meter", Quantity = 10m,
        });
        // 10 yard × 0.9144 / 1 = 9.144
        Assert.Equal(9.144m, result.Quantity);
    }

    [Fact]
    public async Task ConvertAsync_DifferentCategory_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await svc.CreateAsync(ValidCreate("kg", "WEIGHT", true));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.ConvertAsync(new ConvertUnitRequest
            {
                FromCode = "meter", ToCode = "kg", Quantity = 1m,
            }));
    }

    [Fact]
    public async Task ConvertAsync_DeactivatedUnit_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        var yard = await svc.CreateAsync(ValidCreate("yard", "LENGTH", false, 0.9144m));
        await svc.UpdateStatusAsync(yard.Id, false);
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.ConvertAsync(new ConvertUnitRequest
            {
                FromCode = "yard", ToCode = "meter", Quantity = 1m,
            }));
    }

    [Fact]
    public async Task ConvertAsync_NonExistent_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.ConvertAsync(new ConvertUnitRequest
            {
                FromCode = "meter", ToCode = "nonexistent", Quantity = 1m,
            }));
    }

    [Fact]
    public async Task ConvertAsync_CountClass_ResultUnchanged()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("piece", "COUNT", true));
        await svc.CreateAsync(ValidCreate("roll", "COUNT", false, 1m));
        var result = await svc.ConvertAsync(new ConvertUnitRequest
        {
            FromCode = "piece", ToCode = "roll", Quantity = 10m,
        });
        // COUNT 类 factor 都为 1 → 结果不变
        Assert.Equal(10m, result.Quantity);
    }

    [Fact]
    public async Task ConvertAsync_YarnClass_Ok()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("tex", "YARN", true));
        await svc.CreateAsync(ValidCreate("denier", "YARN", false, 9m));
        // 10 denier → tex = 10 × 9 / 1 = 90
        var r1 = await svc.ConvertAsync(new ConvertUnitRequest
        {
            FromCode = "denier", ToCode = "tex", Quantity = 10m,
        });
        Assert.Equal(90m, r1.Quantity);
        // 10 tex → denier = 10 × 1 / 9 = 1.11（precision=2）
        var r2 = await svc.ConvertAsync(new ConvertUnitRequest
        {
            FromCode = "tex", ToCode = "denier", Quantity = 10m,
        });
        Assert.Equal(1.11m, r2.Quantity);
    }

    // ── GetCategories ──

    [Fact]
    public async Task GetCategoriesAsync_ReturnsDistinctActive()
    {
        var (db, svc) = Setup();
        await svc.CreateAsync(ValidCreate("meter", "LENGTH", true));
        await svc.CreateAsync(ValidCreate("yard", "LENGTH", false, 0.9144m));
        await svc.CreateAsync(ValidCreate("kg", "WEIGHT", true));
        var cats = await svc.GetCategoriesAsync();
        Assert.Equal(2, cats.Count);
        Assert.Contains("LENGTH", cats);
        Assert.Contains("WEIGHT", cats);
    }
}
```

- [ ] **Step 3: 运行测试验证失败**

Run: `dotnet test backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~MeasurementUnitServiceTests"`
Expected: 编译失败（`MeasurementUnitService` 类型不存在）

- [ ] **Step 4: 写 Service 实现**

创建 `backend/src/OneCup.Application/Services/MeasurementUnitService.cs`：

```csharp
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 计量单位管理服务实现。
/// code 创建后不可改；基准单位每 category 唯一；换算走基准中转。
/// </summary>
public class MeasurementUnitService : IMeasurementUnitService
{
    private readonly IRepository<MeasurementUnit> _units;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<CreateUnitRequest> _createValidator;

    public MeasurementUnitService(
        IRepository<MeasurementUnit> units,
        IUnitOfWork uow,
        IValidator<CreateUnitRequest> createValidator)
    {
        _units = units;
        _uow = uow;
        _createValidator = createValidator;
    }

    public async Task<PagedResult<UnitDto>> GetListAsync(
        int page, int pageSize, string? keyword, string? category, bool? isActive,
        CancellationToken ct = default)
    {
        var total = await _units.CountAsync(new UnitFilterSpec(keyword, category, isActive), ct);
        var units = await _units.ListAsync(
            new UnitPagedSpec(keyword, category, isActive, page, pageSize), ct);

        return new PagedResult<UnitDto>
        {
            Items = units.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<List<UnitDto>> GetAllActiveAsync(CancellationToken ct = default)
    {
        var units = await _units.ListAsync(new UnitActiveSpec(), ct);
        return units.Select(ToDto).ToList();
    }

    public async Task<List<string>> GetCategoriesAsync(CancellationToken ct = default)
    {
        // 取所有启用单位的 category 去重
        var units = await _units.ListAsync(new UnitActiveSpec(), ct);
        return units.Select(u => u.Category).Distinct().OrderBy(c => c).ToList();
    }

    public async Task<UnitDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var u = await _units.FirstOrDefaultAsync(new UnitByIdSpec(id), ct);
        return u is null ? null : ToDto(u);
    }

    public async Task<UnitDto> CreateAsync(CreateUnitRequest request, CancellationToken ct = default)
    {
        await _createValidator.EnsureValidAsync(request, ct);

        // code 唯一性
        if (await _units.AnyAsync(new UnitByCodeSpec(request.Code), ct))
            throw new DomainException($"单位 code '{request.Code}' 已存在");

        // 基准处理
        if (request.IsBase)
        {
            if (await _units.AnyAsync(new UnitBaseByCategorySpec(request.Category), ct))
                throw new DomainException($"类别 '{request.Category}' 已有基准单位");
        }

        var entity = new MeasurementUnit
        {
            Code = request.Code,
            NameZh = request.NameZh,
            NameEn = request.NameEn,
            Symbol = request.Symbol,
            Category = request.Category,
            IsBase = request.IsBase,
            Factor = request.IsBase ? 1m : request.Factor,  // 基准强制 1
            Precision = request.Precision,
            SortOrder = request.SortOrder,
            IsActive = true,
        };
        await _units.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task UpdateAsync(Guid id, UpdateUnitRequest request, CancellationToken ct = default)
    {
        var entity = await _units.FirstOrDefaultAsync(new UnitByIdSpec(id), ct)
            ?? throw new DomainException("单位不存在");

        // factor：基准不可改
        if (request.Factor is not null)
        {
            if (entity.IsBase)
                throw new DomainException("基准单位的换算系数固定为 1，不可修改（请先取消其基准身份）");
            entity.Factor = request.Factor.Value;
        }

        // isBase 切换
        if (request.IsBase is not null && request.IsBase.Value != entity.IsBase)
        {
            if (request.IsBase.Value)
            {
                // false→true：指定为新基准。先降级旧基准（若有），再升当前。
                // 注意：先取旧基准对象，然后降级它，再升当前——两步在同一 SaveChanges 内。
                var existingBase = await _units.FirstOrDefaultAsync(
                    new UnitBaseByCategorySpec(entity.Category, excludingId: id), ct);
                if (existingBase is not null)
                    existingBase.IsBase = false;

                entity.IsBase = true;
                entity.Factor = 1m;
            }
            else
            {
                // true→false：取消基准，检查是否还有其他基准
                var hasOtherBase = await _units.AnyAsync(
                    new UnitBaseByCategorySpec(entity.Category, excludingId: id), ct);
                if (!hasOtherBase)
                    throw new DomainException("每个类别必须保留一个基准单位");

                entity.IsBase = false;
            }
        }

        if (request.NameZh is not null) entity.NameZh = request.NameZh;
        if (request.NameEn is not null) entity.NameEn = request.NameEn;
        if (request.Symbol is not null) entity.Symbol = request.Symbol;
        if (request.Precision is not null) entity.Precision = request.Precision.Value;
        if (request.SortOrder is not null) entity.SortOrder = request.SortOrder.Value;

        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdateStatusAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var entity = await _units.FirstOrDefaultAsync(new UnitByIdSpec(id), ct)
            ?? throw new DomainException("单位不存在");

        if (!isActive && entity.IsBase)
            throw new DomainException("不能停用基准单位，请先将其他单位设为基准");

        entity.IsActive = isActive;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task<ConvertUnitResult> ConvertAsync(ConvertUnitRequest request, CancellationToken ct = default)
    {
        // UnitByCodeSpec 不过滤 IsActive，以便区分"不存在"与"已停用"
        var from = await _units.FirstOrDefaultAsync(new UnitByCodeSpec(request.FromCode), ct)
            ?? throw new DomainException($"单位 '{request.FromCode}' 不存在");
        if (!from.IsActive)
            throw new DomainException($"单位 '{request.FromCode}' 已停用");

        var to = await _units.FirstOrDefaultAsync(new UnitByCodeSpec(request.ToCode), ct)
            ?? throw new DomainException($"单位 '{request.ToCode}' 不存在");
        if (!to.IsActive)
            throw new DomainException($"单位 '{request.ToCode}' 已停用");

        if (from.Category != to.Category)
            throw new DomainException(
                $"单位 '{from.Code}'({from.Category}) 与 '{to.Code}'({to.Category}) 类别不同，无法换算");

        var result = request.Quantity * from.Factor / to.Factor;
        result = Math.Round(result, to.Precision);

        return new ConvertUnitResult
        {
            Quantity = result,
            FromCode = from.Code,
            ToCode = to.Code,
            Precision = to.Precision,
        };
    }

    private static UnitDto ToDto(MeasurementUnit u) => new()
    {
        Id = u.Id,
        Code = u.Code,
        NameZh = u.NameZh,
        NameEn = u.NameEn,
        Symbol = u.Symbol,
        Category = u.Category,
        IsBase = u.IsBase,
        Factor = u.Factor,
        Precision = u.Precision,
        SortOrder = u.SortOrder,
        IsActive = u.IsActive,
        CreatedAt = u.CreatedAt,
        UpdatedAt = u.UpdatedAt,
    };
}
```

- [ ] **Step 5: 运行测试验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~MeasurementUnit"`
Expected: Service 测试（17）+ Validator 测试（7）= 24 个全部 PASS

- [ ] **Step 6: Commit**

```bash
git add backend/src/OneCup.Application/Interfaces/IMeasurementUnitService.cs \
        backend/src/OneCup.Application/Services/MeasurementUnitService.cs \
        backend/tests/OneCup.UnitTests/MeasurementUnit/MeasurementUnitServiceTests.cs
git commit -m "feat(unit): MeasurementUnitService（CRUD+换算+基准约束）+ 17 个测试"
```

---

## Task 7: Controller + DI 注册 + 授权策略

**Files:**
- Create: `backend/src/OneCup.Api/Controllers/MeasurementUnitsController.cs`
- Modify: `backend/src/OneCup.Api/Program.cs`（追加 DI + 授权策略）

**Interfaces:**
- Consumes: `IMeasurementUnitService`（Task 6）

- [ ] **Step 1: 创建 Controller**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 计量单位管理端点。复用 unit-view / unit-manage 权限。
/// </summary>
[ApiController]
[Route("api/measurement-units")]
public class MeasurementUnitsController : ControllerBase
{
    private readonly IMeasurementUnitService _svc;

    public MeasurementUnitsController(IMeasurementUnitService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    [Authorize(Policy = "unit-view")]
    public async Task<IActionResult> GetList(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null, [FromQuery] string? category = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _svc.GetListAsync(page, pageSize, keyword, category, isActive, ct);
        return Ok(result);
    }

    [HttpGet("all")]
    [Authorize(Policy = "unit-view")]
    public async Task<IActionResult> GetAllActive(CancellationToken ct)
    {
        var list = await _svc.GetAllActiveAsync(ct);
        return Ok(list);
    }

    [HttpGet("categories")]
    [Authorize(Policy = "unit-view")]
    public async Task<IActionResult> GetCategories(CancellationToken ct)
    {
        var list = await _svc.GetCategoriesAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "unit-view")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetByIdAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = "unit-manage")]
    public async Task<IActionResult> Create([FromBody] CreateUnitRequest request, CancellationToken ct)
    {
        var dto = await _svc.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "unit-manage")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateUnitRequest request, CancellationToken ct)
    {
        await _svc.UpdateAsync(id, request, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "unit-manage")]
    public async Task<IActionResult> UpdateStatus(Guid id, [FromBody] UpdateUnitStatusRequest request, CancellationToken ct)
    {
        await _svc.UpdateStatusAsync(id, request.IsActive, ct);
        return NoContent();
    }

    [HttpPost("convert")]
    [Authorize(Policy = "unit-view")]
    public async Task<IActionResult> Convert([FromBody] ConvertUnitRequest request, CancellationToken ct)
    {
        var result = await _svc.ConvertAsync(request, ct);
        return Ok(result);
    }
}
```

- [ ] **Step 2: Program.cs 追加 DI**

在 `builder.Services.AddScoped<INumberingDictionaryService, NumberingDictionaryService>();` 之后追加：

```csharp
// ===== Unit 模块 =====
builder.Services.AddScoped<IMeasurementUnitService, MeasurementUnitService>();
```

- [ ] **Step 3: Program.cs 追加授权策略**

在 `options.AddPolicy("audit-view", ...)` 之后追加：

```csharp
    // ===== Unit 模块 =====
    options.AddPolicy("unit-view", policy =>
        policy.RequireClaim("perm_codes", "system:unit:view"));
    options.AddPolicy("unit-manage", policy =>
        policy.RequireClaim("perm_codes", "system:unit:manage"));
```

- [ ] **Step 4: 验证编译**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 5: Commit**

```bash
git add backend/src/OneCup.Api/Controllers/MeasurementUnitsController.cs \
        backend/src/OneCup.Api/Program.cs
git commit -m "feat(unit): MeasurementUnitsController（8端点）+ DI + 授权策略"
```

---

## Task 8: EF 迁移

**Files:**
- Auto-generated: `backend/src/OneCup.Infrastructure/Migrations/<timestamp>_AddUnitModule.cs` + `.Designer.cs`
- Auto-updated: `backend/src/OneCup.Infrastructure/Migrations/OneCupDbContextModelSnapshot.cs`

- [ ] **Step 1: 生成迁移**

Run:
```bash
dotnet ef migrations add AddUnitModule --project backend/src/OneCup.Infrastructure --startup-project backend/src/OneCup.Api
```
Expected: 迁移文件生成，包含 `measurement_units` 表建表 + 唯一索引 + category 索引 + 2 个权限种子 + 19 个单位种子

- [ ] **Step 2: 检查生成的迁移**

Run: `ls backend/src/OneCup.Infrastructure/Migrations/*AddUnitModule*`
Expected: 看到 `<timestamp>_AddUnitModule.cs` 和 `.Designer.cs`

打开迁移文件，确认 `Up()` 包含：
- `CreateTable("measurement_units", ...)` 含所有列
- `CreateIndex("ux_measurement_units_code", ...)` IsUnique
- `CreateIndex("ix_measurement_units_category", ...)`
- `InsertData("permissions", ...)` 2 行
- `InsertData("measurement_units", ...)` 19 行

- [ ] **Step 3: 验证编译 + 全部测试通过**

Run: `dotnet build backend/OneCup.sln && dotnet test backend/OneCup.sln`
Expected: BUILD SUCCEEDED + 所有测试 PASS

- [ ] **Step 4: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Migrations/
git commit -m "feat(unit): AddUnitModule 迁移（建表+索引+19种子）"
```

---

## Task 9: 前端 API 模块

**Files:**
- Create: `frontend/src/api/measurementUnit.ts`

**Interfaces:**
- Produces: `MeasurementUnit`/`CreateUnitRequest`/`UpdateUnitRequest`/`ConvertResult` 类型 + 8 个 API 函数（Task 11 页面用）

- [ ] **Step 1: 创建 API 文件**

```typescript
import request from './request';
import { PagedResult } from './user';

// ── 类型 ──
export interface MeasurementUnit {
  id: string;
  code: string;
  nameZh: string;
  nameEn: string;
  symbol: string;
  category: string;
  isBase: boolean;
  factor: number;
  precision: number;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateUnitRequest {
  code: string;
  nameZh: string;
  nameEn: string;
  symbol: string;
  category: string;
  isBase: boolean;
  factor: number;
  precision: number;
  sortOrder: number;
}

export interface UpdateUnitRequest {
  nameZh?: string;
  nameEn?: string;
  symbol?: string;
  isBase?: boolean;
  factor?: number;
  precision?: number;
  sortOrder?: number;
}

export interface ConvertResult {
  quantity: number;
  fromCode: string;
  toCode: string;
  precision: number;
}

// ── API ──
export function getUnits(params: {
  page?: number;
  pageSize?: number;
  keyword?: string;
  category?: string;
  isActive?: boolean;
}) {
  return request.get<unknown, PagedResult<MeasurementUnit>>(
    '/api/measurement-units',
    { params },
  );
}

export function getAllActiveUnits() {
  return request.get<unknown, MeasurementUnit[]>(
    '/api/measurement-units/all',
  );
}

export function getUnitCategories() {
  return request.get<unknown, string[]>(
    '/api/measurement-units/categories',
  );
}

export function getUnit(id: string) {
  return request.get<unknown, MeasurementUnit>(
    `/api/measurement-units/${id}`,
  );
}

export function createUnit(data: CreateUnitRequest) {
  return request.post<unknown, MeasurementUnit>(
    '/api/measurement-units',
    data,
  );
}

export function updateUnit(id: string, data: UpdateUnitRequest) {
  return request.put(`/api/measurement-units/${id}`, data);
}

export function updateUnitStatus(id: string, isActive: boolean) {
  return request.put(`/api/measurement-units/${id}/status`, { isActive });
}

export function convertUnit(data: {
  fromCode: string;
  toCode: string;
  quantity: number;
}) {
  return request.post<unknown, ConvertUnitResult>(
    '/api/measurement-units/convert',
    data,
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/api/measurementUnit.ts
git commit -m "feat(unit): 前端 API 模块（8 个接口）"
```

---

## Task 10: 前端 locale + style

**Files:**
- Create: `frontend/src/pages/system/unit/locale/index.ts`
- Create: `frontend/src/pages/system/unit/locale/zh-CN.ts`
- Create: `frontend/src/pages/system/unit/locale/en-US.ts`
- Create: `frontend/src/pages/system/unit/style/index.module.less`

- [ ] **Step 1: 创建 locale 三件套**

`locale/zh-CN.ts`:

```typescript
export default {
  'unit.title': '计量单位管理',
  // 查询区
  'unit.search.keyword': '关键词',
  'unit.search.keywordPlaceholder': '搜索 code / 中文名 / 英文名',
  'unit.search.category': '类别',
  'unit.search.status': '状态',
  'unit.search.allStatus': '全部状态',
  'unit.search.submit': '查询',
  'unit.search.reset': '重置',
  // 列
  'unit.col.code': '编码',
  'unit.col.symbol': '符号',
  'unit.col.nameZh': '中文名',
  'unit.col.nameEn': '英文名',
  'unit.col.category': '类别',
  'unit.col.isBase': '基准',
  'unit.col.factor': '换算系数',
  'unit.col.precision': '精度',
  'unit.col.status': '状态',
  'unit.col.operations': '操作',
  'unit.tag.base': '基准',
  'unit.tag.active': '启用',
  'unit.tag.inactive': '停用',
  // 工具栏
  'unit.toolbar.create': '新建单位',
  'unit.toolbar.convert': '换算',
  // 操作
  'unit.action.edit': '编辑',
  'unit.action.enable': '启用',
  'unit.action.disable': '停用',
  'unit.action.enableConfirm': '确定要启用该单位吗？',
  'unit.action.disableConfirm': '确定要停用该单位吗？',
  // 表单
  'unit.form.create': '新建单位',
  'unit.form.edit': '编辑单位',
  'unit.form.code': '编码（英文标识符）',
  'unit.form.codePlaceholder': '如 kg',
  'unit.form.category': '类别',
  'unit.form.categoryPlaceholder': '如 WEIGHT',
  'unit.form.symbol': '符号',
  'unit.form.nameZh': '中文名',
  'unit.form.nameEn': '英文名',
  'unit.form.isBase': '是否基准单位',
  'unit.form.factor': '换算系数',
  'unit.form.precision': '小数位数',
  'unit.form.sortOrder': '排序号',
  'unit.form.lockedHint': '编码 / 类别创建后不可修改',
  'unit.form.required': '该项为必填',
  'unit.form.createSuccess': '创建成功',
  'unit.form.updateSuccess': '更新成功',
  'unit.form.statusSuccess': '状态更新成功',
  // 换算 Drawer
  'unit.convert.title': '单位换算',
  'unit.convert.from': '源单位',
  'unit.convert.to': '目标单位',
  'unit.convert.quantity': '数量',
  'unit.convert.result': '换算结果',
  'unit.convert.resultEmpty': '请选择源单位和目标单位',
} as const;
```

`locale/en-US.ts`:

```typescript
export default {
  'unit.title': 'Measurement Units',
  'unit.search.keyword': 'Keyword',
  'unit.search.keywordPlaceholder': 'Search code / name',
  'unit.search.category': 'Category',
  'unit.search.status': 'Status',
  'unit.search.allStatus': 'All',
  'unit.search.submit': 'Search',
  'unit.search.reset': 'Reset',
  'unit.col.code': 'Code',
  'unit.col.symbol': 'Symbol',
  'unit.col.nameZh': 'Name (ZH)',
  'unit.col.nameEn': 'Name (EN)',
  'unit.col.category': 'Category',
  'unit.col.isBase': 'Base',
  'unit.col.factor': 'Factor',
  'unit.col.precision': 'Precision',
  'unit.col.status': 'Status',
  'unit.col.operations': 'Actions',
  'unit.tag.base': 'Base',
  'unit.tag.active': 'Active',
  'unit.tag.inactive': 'Inactive',
  'unit.toolbar.create': 'New Unit',
  'unit.toolbar.convert': 'Convert',
  'unit.action.edit': 'Edit',
  'unit.action.enable': 'Enable',
  'unit.action.disable': 'Disable',
  'unit.action.enableConfirm': 'Enable this unit?',
  'unit.action.disableConfirm': 'Disable this unit?',
  'unit.form.create': 'New Unit',
  'unit.form.edit': 'Edit Unit',
  'unit.form.code': 'Code',
  'unit.form.codePlaceholder': 'e.g. kg',
  'unit.form.category': 'Category',
  'unit.form.categoryPlaceholder': 'e.g. WEIGHT',
  'unit.form.symbol': 'Symbol',
  'unit.form.nameZh': 'Name (ZH)',
  'unit.form.nameEn': 'Name (EN)',
  'unit.form.isBase': 'Is Base Unit',
  'unit.form.factor': 'Factor',
  'unit.form.precision': 'Precision',
  'unit.form.sortOrder': 'Sort Order',
  'unit.form.lockedHint': 'Code and category cannot be modified after creation',
  'unit.form.required': 'Required',
  'unit.form.createSuccess': 'Created',
  'unit.form.updateSuccess': 'Updated',
  'unit.form.statusSuccess': 'Status updated',
  'unit.convert.title': 'Unit Conversion',
  'unit.convert.from': 'From',
  'unit.convert.to': 'To',
  'unit.convert.quantity': 'Quantity',
  'unit.convert.result': 'Result',
  'unit.convert.resultEmpty': 'Select from and to units',
} as const;
```

`locale/index.ts`:

```typescript
import zhCN from './zh-CN';
import enUS from './en-US';

export default { 'zh-CN': zhCN, 'en-US': enUS };
```

- [ ] **Step 2: 创建 style（复制 numbering 的三 class）**

`style/index.module.less`:

```less
.search-form-wrapper {
  display: flex;
  border-bottom: 1px solid var(--color-border-1);
  margin-bottom: 20px;

  .right-button {
    display: flex;
    flex-direction: column;
    justify-content: space-between;
    padding-left: 20px;
    margin-bottom: 20px;
    border-left: 1px solid var(--color-border-2);
    box-sizing: border-box;
  }
}

.search-form {
  padding-right: 20px;
}

.button-group {
  display: flex;
  justify-content: space-between;
  margin-bottom: 20px;
}
```

- [ ] **Step 3: Commit**

```bash
git add frontend/src/pages/system/unit/locale/ frontend/src/pages/system/unit/style/
git commit -m "feat(unit): 前端 locale 三件套 + style"
```

---

## Task 11: 前端页面主体

**Files:**
- Create: `frontend/src/pages/system/unit/index.tsx`

**Interfaces:**
- Consumes: API（Task 9）、locale（Task 10）、style（Task 10）

- [ ] **Step 1: 创建页面**

```tsx
import { useEffect, useState, useCallback, useMemo } from 'react';
import {
  Table, Button, Drawer, Form, Input, InputNumber, Select, Switch,
  Tag, Popconfirm, Message, Space, Alert, Typography, Card, Grid,
} from '@arco-design/web-react';
import { IconPlus, IconSearch, IconRefresh, IconSwap } from '@arco-design/web-react/icon';
import useLocale from '@/utils/useLocale';
import {
  getUnits, createUnit, updateUnit, updateUnitStatus,
  getAllActiveUnits, getUnitCategories, convertUnit,
  MeasurementUnit, CreateUnitRequest, ConvertResult,
} from '@/api/measurementUnit';
import locale from './locale';
import styles from './style/index.module.less';

const { Title } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;

const DEFAULT_FORM: CreateUnitRequest = {
  code: '', nameZh: '', nameEn: '', symbol: '', category: '',
  isBase: false, factor: 1, precision: 2, sortOrder: 0,
};

export default function UnitManagementPage() {
  const t = useLocale(locale);
  const [searchForm] = Form.useForm();
  const [editForm] = Form.useForm();
  const [convertForm] = Form.useForm();

  // ── 列表 ──
  const [data, setData] = useState<MeasurementUnit[]>([]);
  const [loading, setLoading] = useState(false);
  const [pagination, setPagination] = useState({ current: 1, pageSize: 10, total: 0 });
  const [filters, setFilters] = useState<{ keyword?: string; category?: string; isActive?: boolean }>({});
  const [categoryOptions, setCategoryOptions] = useState<string[]>([]);

  // ── 编辑抽屉 ──
  const [editVisible, setEditVisible] = useState(false);
  const [editMode, setEditMode] = useState<'create' | 'edit'>('create');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editIsBase, setEditIsBase] = useState(false);

  // ── 换算抽屉 ──
  const [convertVisible, setConvertVisible] = useState(false);
  const [allUnits, setAllUnits] = useState<MeasurementUnit[]>([]);
  const [convertResult, setConvertResult] = useState<ConvertResult | null>(null);
  const [convertLoading, setConvertLoading] = useState(false);

  // ── 拉取列表 ──
  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const res = await getUnits({
        page: pagination.current,
        pageSize: pagination.pageSize,
        ...filters,
      });
      setData(res.items);
      setPagination((p) => ({ ...p, total: res.total }));
    } catch {
      // request 拦截器已处理错误提示
    } finally {
      setLoading(false);
    }
  }, [pagination.current, pagination.pageSize, filters]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // 拉取类别选项
  useEffect(() => {
    getUnitCategories().then(setCategoryOptions).catch(() => {});
  }, []);

  // ── 查询 ──
  function handleSearch() {
    const v = searchForm.getFieldsValue();
    setFilters({
      keyword: v.keyword || undefined,
      category: v.category || undefined,
      isActive: v.isActive === undefined ? undefined : v.isActive === 'true',
    });
    setPagination((p) => ({ ...p, current: 1 }));
  }

  function handleReset() {
    searchForm.resetFields();
    setFilters({});
    setPagination((p) => ({ ...p, current: 1 }));
  }

  // ── 编辑 ──
  function openCreate() {
    setEditMode('create');
    setEditingId(null);
    setEditIsBase(false);
    editForm.resetFields();
    editForm.setFieldsValue(DEFAULT_FORM);
    setEditVisible(true);
  }

  function openEdit(record: MeasurementUnit) {
    setEditMode('edit');
    setEditingId(record.id);
    setEditIsBase(record.isBase);
    editForm.resetFields();
    editForm.setFieldsValue({
      nameZh: record.nameZh, nameEn: record.nameEn, symbol: record.symbol,
      isBase: record.isBase, factor: record.factor,
      precision: record.precision, sortOrder: record.sortOrder,
    });
    setEditVisible(true);
  }

  async function handleEditOk() {
    try {
      const values = await editForm.validate();
      if (editMode === 'create') {
        await createUnit(values as CreateUnitRequest);
        Message.success(t['unit.form.createSuccess']);
      } else {
        // isBase=true 时不传 factor（后端强制 1）
        const payload = { ...values };
        if (values.isBase) delete payload.factor;
        await updateUnit(editingId!, payload);
        Message.success(t['unit.form.updateSuccess']);
      }
      setEditVisible(false);
      fetchData();
    } catch {
      // 校验失败或 API 错误
    }
  }

  async function handleToggleStatus(record: MeasurementUnit) {
    try {
      await updateUnitStatus(record.id, !record.isActive);
      Message.success(t['unit.form.statusSuccess']);
      fetchData();
    } catch {
      // ignore
    }
  }

  // ── 换算 ──
  async function openConvert() {
    setConvertVisible(true);
    setConvertResult(null);
    convertForm.resetFields();
    convertForm.setFieldsValue({ quantity: 1 });
    try {
      const list = await getAllActiveUnits();
      setAllUnits(list);
    } catch {
      // ignore
    }
  }

  async function doConvert() {
    const v = convertForm.getFieldsValue();
    if (!v.fromCode || !v.toCode) {
      setConvertResult(null);
      return;
    }
    setConvertLoading(true);
    try {
      const result = await convertUnit({
        fromCode: v.fromCode, toCode: v.toCode, quantity: v.quantity || 1,
      });
      setConvertResult(result);
    } catch {
      setConvertResult(null);
    } finally {
      setConvertLoading(false);
    }
  }

  // 换算 Drawer：源单位选中后，目标单位只显示同 category
  const fromCode = Form.useWatch('fromCode', convertForm);
  const fromUnit = allUnits.find((u) => u.code === fromCode);
  const toOptions = useMemo(() => {
    if (!fromUnit) return allUnits;
    return allUnits.filter((u) => u.category === fromUnit.category);
  }, [fromUnit, allUnits]);

  // ── 列定义 ──
  const columns = [
    { title: t['unit.col.code'], dataIndex: 'code', width: 110 },
    { title: t['unit.col.symbol'], dataIndex: 'symbol', width: 70 },
    { title: t['unit.col.nameZh'], dataIndex: 'nameZh', width: 90 },
    { title: t['unit.col.nameEn'], dataIndex: 'nameEn', width: 110 },
    { title: t['unit.col.category'], dataIndex: 'category', width: 90, render: (v: string) => <Tag>{v}</Tag> },
    {
      title: t['unit.col.isBase'], dataIndex: 'isBase', width: 70,
      render: (v: boolean) => v ? <Tag color="green">{t['unit.tag.base']}</Tag> : null,
    },
    { title: t['unit.col.factor'], dataIndex: 'factor', width: 100 },
    { title: t['unit.col.precision'], dataIndex: 'precision', width: 60 },
    {
      title: t['unit.col.status'], dataIndex: 'isActive', width: 80,
      render: (v: boolean) => v
        ? <Tag color="green">{t['unit.tag.active']}</Tag>
        : <Tag>{t['unit.tag.inactive']}</Tag>,
    },
    {
      title: t['unit.col.operations'], dataIndex: 'operations', width: 160,
      render: (_: unknown, record: MeasurementUnit) => (
        <Space>
          <Button type="text" size="small" onClick={() => openEdit(record)}>
            {t['unit.action.edit']}
          </Button>
          <Popconfirm
            title={record.isActive ? t['unit.action.disableConfirm'] : t['unit.action.enableConfirm']}
            onOk={() => handleToggleStatus(record)}
          >
            <Button type="text" size="small" status={record.isActive ? 'warning' : 'success'}>
              {record.isActive ? t['unit.action.disable'] : t['unit.action.enable']}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <Card>
      <Title heading={6}>{t['unit.title']}</Title>

      {/* 查询区三件套 */}
      <div className={styles['search-form-wrapper']}>
        <Form
          form={searchForm}
          className={styles['search-form']}
          labelAlign="left"
          labelCol={{ span: 5 }}
          wrapperCol={{ span: 19 }}
        >
          <Row gutter={24}>
            <Col span={8}>
              <FormItem label={t['unit.search.keyword']} field="keyword">
                <Input allowClear placeholder={t['unit.search.keywordPlaceholder']} />
              </FormItem>
            </Col>
            <Col span={8}>
              <FormItem label={t['unit.search.category']} field="category">
                <Select allowClear allowCreate placeholder={t['unit.search.category']}>
                  {categoryOptions.map((c) => (
                    <Select.Option key={c} value={c}>{c}</Select.Option>
                  ))}
                </Select>
              </FormItem>
            </Col>
            <Col span={8}>
              <FormItem label={t['unit.search.status']} field="isActive">
                <Select allowClear placeholder={t['unit.search.allStatus']}>
                  <Select.Option value="true">{t['unit.tag.active']}</Select.Option>
                  <Select.Option value="false">{t['unit.tag.inactive']}</Select.Option>
                </Select>
              </FormItem>
            </Col>
          </Row>
        </Form>
        <div className={styles['right-button']}>
          <Button type="primary" icon={<IconSearch />} onClick={handleSearch}>
            {t['unit.search.submit']}
          </Button>
          <Button icon={<IconRefresh />} onClick={handleReset}>
            {t['unit.search.reset']}
          </Button>
        </div>
      </div>

      {/* 工具栏 */}
      <div className={styles['button-group']}>
        <Space>
          <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
            {t['unit.toolbar.create']}
          </Button>
        </Space>
        <Space>
          <Button icon={<IconSwap />} onClick={openConvert}>
            {t['unit.toolbar.convert']}
          </Button>
        </Space>
      </div>

      <Table
        rowKey="id"
        columns={columns}
        data={data}
        loading={loading}
        pagination={{
          ...pagination,
          showTotal: true,
          sizeCanChange: true,
          onChange: (current, pageSize) =>
            setPagination((p) => ({ ...p, current, pageSize })),
        }}
      />

      {/* 新建/编辑抽屉 */}
      <Drawer
        title={editMode === 'create' ? t['unit.form.create'] : t['unit.form.edit']}
        visible={editVisible}
        onOk={handleEditOk}
        onCancel={() => setEditVisible(false)}
        width={480}
        unmountOnExit
      >
        {editMode === 'edit' && (
          <Alert type="info" content={t['unit.form.lockedHint']} style={{ marginBottom: 16 }} />
        )}
        <Form form={editForm} layout="vertical">
          {editMode === 'create' && (
            <>
              <FormItem
                label={t['unit.form.code']}
                field="code"
                rules={[{ required: true, message: t['unit.form.required'] }]}
              >
                <Input placeholder={t['unit.form.codePlaceholder']} />
              </FormItem>
              <FormItem
                label={t['unit.form.category']}
                field="category"
                rules={[{ required: true, message: t['unit.form.required'] }]}
              >
                <Select allowCreate placeholder={t['unit.form.categoryPlaceholder']}>
                  {categoryOptions.map((c) => (
                    <Select.Option key={c} value={c}>{c}</Select.Option>
                  ))}
                </Select>
              </FormItem>
            </>
          )}
          {editMode === 'edit' && (
            <>
              <FormItem label={t['unit.form.code']}>
                <Input disabled value={data.find((x) => x.id === editingId)?.code} />
              </FormItem>
              <FormItem label={t['unit.form.category']}>
                <Input disabled value={data.find((x) => x.id === editingId)?.category} />
              </FormItem>
            </>
          )}
          <FormItem
            label={t['unit.form.nameZh']}
            field="nameZh"
            rules={[{ required: true, message: t['unit.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem
            label={t['unit.form.nameEn']}
            field="nameEn"
            rules={[{ required: true, message: t['unit.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem
            label={t['unit.form.symbol']}
            field="symbol"
            rules={[{ required: true, message: t['unit.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem label={t['unit.form.isBase']} field="isBase" triggerPropName="checked">
            <Switch onChange={(v: boolean) => setEditIsBase(v)} />
          </FormItem>
          <FormItem label={t['unit.form.factor']} field="factor">
            <InputNumber min={0} step={0.00000001} disabled={editIsBase} style={{ width: '100%' }} />
          </FormItem>
          <FormItem label={t['unit.form.precision']} field="precision">
            <InputNumber min={0} max={6} style={{ width: '100%' }} />
          </FormItem>
          <FormItem label={t['unit.form.sortOrder']} field="sortOrder">
            <InputNumber min={0} style={{ width: '100%' }} />
          </FormItem>
        </Form>
      </Drawer>

      {/* 换算抽屉 */}
      <Drawer
        title={t['unit.convert.title']}
        visible={convertVisible}
        onOk={doConvert}
        onCancel={() => setConvertVisible(false)}
        okText={t['unit.search.submit']}
        width={440}
        unmountOnExit
      >
        <Form form={convertForm} layout="vertical">
          <FormItem label={t['unit.convert.from']} field="fromCode">
            <Select allowClear placeholder={t['unit.convert.from']} onChange={doConvert}>
              {Object.entries(
                allUnits.reduce<Record<string, MeasurementUnit[]>>((acc, u) => {
                  (acc[u.category] ??= []).push(u);
                  return acc;
                }, {}),
              ).map(([cat, units]) => (
                <Select.OptGroup key={cat} label={cat}>
                  {units.map((u) => (
                    <Select.Option key={u.code} value={u.code}>
                      {u.nameZh} ({u.symbol})
                    </Select.Option>
                  ))}
                </Select.OptGroup>
              ))}
            </Select>
          </FormItem>
          <FormItem label={t['unit.convert.to']} field="toCode">
            <Select allowClear placeholder={t['unit.convert.to']} onChange={doConvert}>
              {toOptions.map((u) => (
                <Select.Option key={u.code} value={u.code}>
                  {u.nameZh} ({u.symbol})
                </Select.Option>
              ))}
            </Select>
          </FormItem>
          <FormItem label={t['unit.convert.quantity']} field="quantity">
            <InputNumber min={0} style={{ width: '100%' }} onChange={doConvert} />
          </FormItem>
        </Form>
        <div style={{ marginTop: 16 }}>
          <Typography.Text type="secondary">{t['unit.convert.result']}</Typography.Text>
          <div
            style={{
              marginTop: 6, padding: '12px 16px',
              background: 'var(--color-fill-2)', borderRadius: 4,
              fontSize: 18, fontFamily: 'monospace',
            }}
          >
            {convertLoading
              ? '...'
              : convertResult
                ? `${convertForm.getFieldValue('quantity') ?? 1} ${
                    allUnits.find((u) => u.code === convertResult.fromCode)?.symbol ?? ''
                  } = ${convertResult.quantity} ${
                    allUnits.find((u) => u.code === convertResult.toCode)?.symbol ?? ''
                  }`
                : t['unit.convert.resultEmpty']}
          </div>
        </div>
      </Drawer>
    </Card>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/pages/system/unit/index.tsx
git commit -m "feat(unit): 计量单位管理页面（查询表格+编辑+换算Drawer）"
```

---

## Task 12: 前端路由 + 菜单 + 国际化注册

**Files:**
- Modify: `frontend/src/routes.ts`
- Modify: `frontend/src/router.tsx`
- Modify: `frontend/src/locale/index.ts`

- [ ] **Step 1: routes.ts 追加菜单项**

在 `menu.system` 的 `children` 数组**末尾**（`loginLog` 之后、数组结束 `]` 之前）追加：

```typescript
      {
        name: 'menu.system.unit',
        key: 'system/unit',
        requiredPermissions: [
          { resource: 'system:unit', actions: ['view'] },
        ],
      },
```

- [ ] **Step 2: router.tsx 追加 import + element**

在 `const LoginLogPage = lazy(...)` 之后追加：

```typescript
const UnitPage = lazy(() => import('@/pages/system/unit'));
```

在 children 数组中 `system/login-log` 路由之后追加：

```typescript
      {
        path: 'system/unit',
        element: withSuspense(
          <RequirePermission resource="system:unit" actions={['view']}>
            <UnitPage />
          </RequirePermission>
        ),
      },
```

- [ ] **Step 3: locale/index.ts 追加菜单文案**

在 en-US 对象中 `'menu.system.loginLog': 'Login Log',` 之后追加：

```typescript
    'menu.system.unit': 'Units',
```

在 zh-CN 对象中 `'menu.system.loginLog': '登录日志',` 之后追加：

```typescript
    'menu.system.unit': '计量单位',
```

- [ ] **Step 4: 验证前端编译**

Run: `cd frontend && npm run build`
Expected: BUILD 成功（无 TS 错误）

- [ ] **Step 5: Commit**

```bash
git add frontend/src/routes.ts frontend/src/router.tsx frontend/src/locale/index.ts
git commit -m "feat(unit): 前端路由+菜单+国际化注册"
```

---

## Task 13: 前端测试

**Files:**
- Create: `frontend/src/pages/system/unit/__tests__/index.test.tsx`

- [ ] **Step 1: 创建测试文件**

```typescript
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen } from '@testing-library/react';
import { GlobalContext } from '@/context';
import UnitPage from '../index';

// mock API 模块，避免真实网络请求
vi.mock('@/api/measurementUnit', () => ({
  getUnits: vi.fn().mockResolvedValue({ items: [], total: 0 }),
  getAllActiveUnits: vi.fn().mockResolvedValue([]),
  getUnitCategories: vi.fn().mockResolvedValue([]),
  getUnit: vi.fn().mockResolvedValue({}),
  createUnit: vi.fn().mockResolvedValue({}),
  updateUnit: vi.fn().mockResolvedValue({}),
  updateUnitStatus: vi.fn().mockResolvedValue({}),
  convertUnit: vi.fn().mockResolvedValue({}),
}));

// useLocale 从 GlobalContext 读取 lang；提供 lang='zh-CN' 让文案正常解析
const renderWithLocale = (ui: React.ReactElement) =>
  render(
    <GlobalContext.Provider value={{ lang: 'zh-CN' }}>
      {ui}
    </GlobalContext.Provider>,
  );

describe('UnitPage — 标准布局结构', () => {
  beforeEach(() => vi.clearAllMocks());

  it('渲染在单个 Card 内', async () => {
    renderWithLocale(<UnitPage />);
    await screen.findByText('计量单位管理');
    expect(document.querySelector('.arco-card')).toBeInTheDocument();
  });

  it('查询区用 Form（非裸 Space）', async () => {
    const { container } = renderWithLocale(<UnitPage />);
    await screen.findByText('计量单位管理');
    expect(container.querySelector('.arco-form')).toBeInTheDocument();
  });

  it('有查询和重置两个按钮，且文案正确', async () => {
    renderWithLocale(<UnitPage />);
    expect(await screen.findByRole('button', { name: '查询' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: '重置' })).toBeInTheDocument();
  });

  it('新建按钮在工具栏左侧', async () => {
    renderWithLocale(<UnitPage />);
    await screen.findByText('计量单位管理');
    expect(screen.getByRole('button', { name: '新建单位' })).toBeInTheDocument();
  });

  it('换算按钮在工具栏右侧', async () => {
    renderWithLocale(<UnitPage />);
    await screen.findByText('计量单位管理');
    expect(screen.getByRole('button', { name: '换算' })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: 运行测试**

Run: `cd frontend && npm test`
Expected: 5 个测试 PASS（含编号等既有模块测试仍绿）

- [ ] **Step 3: Commit**

```bash
git add frontend/src/pages/system/unit/__tests__/index.test.tsx
git commit -m "test(unit): 前端页面布局结构断言（5 个测试）"
```

---

## Task 14: 全量验证

- [ ] **Step 1: 后端构建 + 测试**

Run:
```bash
dotnet build backend/OneCup.sln
dotnet test backend/OneCup.sln
```
Expected: BUILD SUCCEEDED + 所有测试 PASS（含新增 24 个后端测试 + 既有测试）

- [ ] **Step 2: 前端构建 + 测试 + lint**

Run:
```bash
cd frontend
npm run build
npm test
npm run eslint
```
Expected: 全部通过

- [ ] **Step 3: 迁移本地库验证（可选，需本地 PG）**

Run:
```bash
dotnet ef database update --project backend/src/OneCup.Infrastructure --startup-project backend/src/OneCup.Api
```
Expected: 迁移成功应用，`measurement_units` 表创建 + 19 条种子 + 2 条权限种子入库

- [ ] **Step 4: 最终 commit（如有 lint 修复）**

```bash
git add -A
git commit -m "chore(unit): 全量验证通过（build/test/lint 绿）" --allow-empty
```

---

## 完成标志

- [x] 后端 9 新增文件 + 4 共享文件修改 + 1 迁移
- [x] 前端 7 新增文件 + 3 共享文件修改
- [x] 24 个后端测试 + 5 个前端测试全绿
- [x] `dotnet build` / `dotnet test` / `npm run build` / `npm test` 全绿
- [x] 种子 Guid 121/122（遵守 parallel-dev-contract §3.1）
- [x] 迁移命名 AddUnitModule（遵守 §3.2）
