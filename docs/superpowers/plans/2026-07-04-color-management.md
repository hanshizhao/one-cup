# 颜色管理模块 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现颜色主数据字典模块（后端 CRUD + 前端查询表格页），颜色作为共享基础数据供未来业务模块引用。

**Architecture:** 后端 Clean Architecture（Domain 实体 / Application DTO+Spec+Service / Infrastructure EF 配置 / Api 控制器），完全复刻编号字典（NumberingDictionary）模块的范式。前端单查询表格页（Card + 三列查询 + 抽屉表单）。复用既有权限 `color:read`(109)/`color:write`(110)，**不新增任何 Guid**。

**Tech Stack:** .NET 10 / EF Core 10 / PostgreSQL / xUnit 2.9（InMemory）；React 18 + TypeScript + Arco Design Web React + axios。

## Global Constraints

- **分支/worktree**：仅在 `.worktrees/color-mgmt`（分支 `feat/color-mgmt`）工作。开工前确认：`git branch --show-current` 输出 `feat/color-mgmt`。
- **Guid 约束（最关键，违反会导致并行合并冲突）**：**零新增 Guid**。复用 main 基线已有的 `PermColorRead=...109`、`PermColorWrite=...110`、`TargetTypeColor=...205`。绝不新增权限/目标类型 Guid，绝不碰 121-123（单位）/118-120/124-130/207-210（缓冲段）。详见 `docs/parallel-dev-contract.md` §3.1。
- **迁移命名**：`AddColorModule`（契约 §3.2，让 EF 自动加时间戳前缀，防与单位的 `AddUnitModule` 撞名）。
- **种子数据**：不种子任何颜色业务数据（迁移只建表，不 `HasData`）。因此 `SeedData.cs` **完全不改动**。
- **共享文件改法**：全部在文件**末尾追加**（DbContext 加 `// ===== Color 模块 =====` 注释块；Program.cs/routes.ts/router.tsx/locale 纯追加）。详见契约 §3.3/§3.4/§3.5。
- **DRY**：颜色模块的 Service/Spec/Controller/DTO 结构与编号字典**同构**——遇到不确定的结构，对照 `NumberingDictionaryService.cs` / `NumberingDictionarySpecs.cs` / `NumberingDictionaryController.cs` / `NumberingDictionaryDtos.cs`。
- **路径根**：后端 `backend/src/...`，前端 `frontend/src/...`，测试 `backend/tests/...`。所有 `dotnet ef` / `dotnet test` 命令在 worktree 根目录 `C:\Users\mi\Desktop\work_space\one-cup\.worktrees\color-mgmt` 执行。
- **每完成一个 Task 立即 commit**（frequent commits）。

---

## File Structure

### 后端新增（每个文件单一职责）

| 文件 | 职责 |
|------|------|
| `backend/src/OneCup.Domain/Entities/Color.cs` | 颜色实体（继承 BaseEntity） |
| `backend/src/OneCup.Application/Dtos/System/ColorDtos.cs` | 请求/响应 DTO |
| `backend/src/OneCup.Application/Specifications/ColorSpecs.cs` | 5 个查询规格 |
| `backend/src/OneCup.Application/Interfaces/IColorService.cs` | 服务接口 |
| `backend/src/OneCup.Application/Services/ColorService.cs` | 服务实现（Repository+Spec+UoW） |
| `backend/src/OneCup.Infrastructure/Persistence/Configurations/ColorConfiguration.cs` | EF 表/列/索引映射 |
| `backend/src/OneCup.Api/Controllers/ColorController.cs` | HTTP 端点 |
| `backend/tests/OneCup.UnitTests/Color/ColorServiceTests.cs` | 服务层单元测试 |
| `backend/tests/OneCup.UnitTests/Color/ColorSpecsTests.cs` | 规格层单元测试 |

### 后端修改（共享文件，末尾追加）

| 文件 | 改动 |
|------|------|
| `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs` | 加 `DbSet<Color>` + `ApplyConfiguration`（`Seed()` 不改） |
| `backend/src/OneCup.Api/Program.cs` | 加 2 条授权策略 + 1 条 DI 注册 |

### EF 迁移（自动生成）

| 文件 | 触发方式 |
|------|----------|
| `backend/src/OneCup.Infrastructure/Migrations/<timestamp>_AddColorModule.cs` + `.Designer.cs` | `dotnet ef migrations add AddColorModule` |
| `backend/src/OneCup.Infrastructure/Migrations/OneCupDbContextModelSnapshot.cs` | EF 自动重写 |

### 前端新增

| 文件 | 职责 |
|------|------|
| `frontend/src/api/color.ts` | API 客户端（类型 + 请求函数） |
| `frontend/src/pages/master-data/color/index.tsx` | 查询表格页 |
| `frontend/src/pages/master-data/color/locale/{index,zh-CN,en-US}.ts` | 国际化三件套 |
| `frontend/src/pages/master-data/color/style/index.module.less` | 样式 |

### 前端修改（共享文件，末尾追加）

| 文件 | 改动 |
|------|------|
| `frontend/src/routes.ts` | `routes` 数组末尾加 `masterData` 顶级项 + `color` 子项 |
| `frontend/src/router.tsx` | lazy import + `master-data/color` 路由 element |
| `frontend/src/locale/index.ts` | en-US/zh-CN 各加 `menu.masterData` 文案 |

---

## Task 1: 颜色实体 + EF 配置

**Files:**
- Create: `backend/src/OneCup.Domain/Entities/Color.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/ColorConfiguration.cs`

**Interfaces:**
- Produces: `OneCup.Domain.Entities.Color` 类（属性：`Id`(来自BaseEntity) / `Code` / `NameZh` / `NameEn` / `Hex` / `ColorFamily` / `Remark` / `SortOrder` / `IsActive`）。Task 2/3/4/5 依赖此类型。

- [ ] **Step 1: 创建实体**

Create `backend/src/OneCup.Domain/Entities/Color.cs`:

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 颜色主数据字典。code 创建后不可改，作为面料/产品等业务模块的稳定引用标识符。
/// </summary>
public class Color : BaseEntity
{
    /// <summary>编码，如 RED001。创建后不可改</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>中文名，如"大红"</summary>
    public string NameZh { get; set; } = string.Empty;

    /// <summary>英文名，如"Red"</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>颜色值 #RRGGBB</summary>
    public string Hex { get; set; } = string.Empty;

    /// <summary>颜色系（自由文本，如"红"）</summary>
    public string ColorFamily { get; set; } = string.Empty;

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>排序号</summary>
    public int SortOrder { get; set; }

    /// <summary>启停状态（停用后引用方按需处理，不物理删除）</summary>
    public bool IsActive { get; set; } = true;
}
```

- [ ] **Step 2: 创建 EF 配置**

对照 `backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingTargetTypeConfiguration.cs` 的同构结构。

Create `backend/src/OneCup.Infrastructure/Persistence/Configurations/ColorConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class ColorConfiguration : IEntityTypeConfiguration<Color>
{
    public void Configure(EntityTypeBuilder<Color> builder)
    {
        builder.ToTable("colors");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(c => c.NameZh).HasColumnName("name_zh").HasMaxLength(64).IsRequired();
        builder.Property(c => c.NameEn).HasColumnName("name_en").HasMaxLength(64).IsRequired();
        builder.Property(c => c.Hex).HasColumnName("hex").HasMaxLength(7).IsFixedLength().IsRequired();
        builder.Property(c => c.ColorFamily).HasColumnName("color_family").HasMaxLength(32).IsRequired();
        builder.Property(c => c.Remark).HasColumnName("remark").HasMaxLength(256);
        builder.Property(c => c.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => c.Code)
            .HasDatabaseName("ux_colors_code")
            .IsUnique();
    }
}
```

> 注意 `Hex` 用 `HasMaxLength(7).IsFixedLength()`（`#RRGGBB` 固定 7 字符，对应 PostgreSQL `char(7)`）。

- [ ] **Step 3: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: Build succeeded，0 errors。（此时实体与配置独立存在，尚未注册到 DbContext，但能编译通过。）

- [ ] **Step 4: Commit**

```bash
git add backend/src/OneCup.Domain/Entities/Color.cs backend/src/OneCup.Infrastructure/Persistence/Configurations/ColorConfiguration.cs
git commit -m "feat(color): 颜色实体 + EF 配置"
```

---

## Task 2: DbContext 注册 + 迁移生成

**Files:**
- Modify: `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`（仅在 `OnModelCreating` 之前加 `DbSet<Color>` 一行——注意本模块用 `ApplyConfigurationsFromAssembly` 自动扫描，**无需**显式 `ApplyConfiguration`；且不种子，**不改 `Seed()`**）
- Create: `backend/src/OneCup.Infrastructure/Migrations/<timestamp>_AddColorModule.cs`（EF 生成）

**Interfaces:**
- Produces: `OneCupDbContext.Colors` DbSet（`DbSet<Color>`）。Task 3/4 测试与 Task 5 服务依赖此属性。

> **关键澄清**：`OneCupDbContext.OnModelCreating` 已用 `ApplyConfigurationsFromAssembly(typeof(OneCupDbContext).Assembly)` 自动扫描本程序集所有 `IEntityTypeConfiguration`。`ColorConfiguration` 在 `OneCup.Infrastructure` 程序集内，会被自动应用——**不要**手写 `modelBuilder.ApplyConfiguration(new ColorConfiguration())`（契约 §3.3 是泛指，但本项目实际机制是自动扫描；对照现有 DbContext 确认无任何实体显式 ApplyConfiguration）。

- [ ] **Step 1: 加 DbSet**

Read `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`，在现有 DbSet 列表（`public DbSet<LoginLog> LoginLogs => Set<LoginLog>();` 这一行之后、`protected override void OnModelCreating` 之前）追加一行：

```csharp
    // ===== Color 模块（feat/color-mgmt）=====
    public DbSet<Color> Colors => Set<Color>();
```

> `// ===== Color 模块 =====` 注释块是契约 §3.3 要求的模块标注，便于合并期识别冲突区块。

- [ ] **Step 2: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: Build succeeded。

- [ ] **Step 3: 生成迁移**

在 worktree 根目录执行（路径相对根）：

```bash
dotnet ef migrations add AddColorModule --project backend/src/OneCup.Infrastructure --startup-project backend/src/OneCup.Api
```

Expected: 生成两个文件 `backend/src/OneCup.Infrastructure/Migrations/<timestamp>_AddColorModule.cs` 和 `.Designer.cs`，并自动重写 `OneCupDbContextModelSnapshot.cs`。

- [ ] **Step 4: 检查迁移内容**

打开生成的 `<timestamp>_AddColorModule.cs`，确认 `Up()` 包含：
- `migrationBuilder.CreateTable("colors", ...)` 含全部列
- `migrationBuilder.CreateIndex("ux_colors_code", "colors", column: "code", unique: true)`

确认 `Down()` 包含 `migrationBuilder.DropTable("colors")`。

如果 Up/Down 为空或不含建表语句，说明 DbSet 未生效或配置未编译——回到 Step 1 检查。

- [ ] **Step 5: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs backend/src/OneCup.Infrastructure/Migrations/
git commit -m "feat(color): 注册 Colors DbSet + AddColorModule 迁移"
```

---

## Task 3: DTOs + Specifications

**Files:**
- Create: `backend/src/OneCup.Application/Dtos/System/ColorDtos.cs`
- Create: `backend/src/OneCup.Application/Specifications/ColorSpecs.cs`

**Interfaces:**
- Consumes: `Color`（Task 1）、`Specification<T>` 基类（`OneCup.Application.Specifications`）、`PagedResult<T>`（`OneCup.Application.Common`）
- Produces:
  - DTOs: `CreateColorRequest` / `UpdateColorRequest` / `UpdateColorStatusRequest` / `ColorDto`（Task 4/5 依赖）
  - Specs: `ColorFilterSpec(string?,string?,bool?)` / `ColorPagedSpec(string?,string?,bool?,int,int)` / `ColorActiveSpec()` / `ColorByIdSpec(Guid)` / `ColorByCodeSpec(string,Guid?)`（Task 4/5 依赖）

- [ ] **Step 1: 创建 DTOs**

对照 `backend/src/OneCup.Application/Dtos/System/NumberingDictionaryDtos.cs` 的 record/class 风格。

Create `backend/src/OneCup.Application/Dtos/System/ColorDtos.cs`:

```csharp
namespace OneCup.Application.Dtos.System;

public record CreateColorRequest
{
    public string Code { get; init; } = string.Empty;
    public string NameZh { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string Hex { get; init; } = string.Empty;
    public string ColorFamily { get; init; } = string.Empty;
    public string? Remark { get; init; }
    public int SortOrder { get; init; }
}

public record UpdateColorRequest
{
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public string? Hex { get; init; }
    public string? ColorFamily { get; init; }
    public string? Remark { get; init; }
    public int? SortOrder { get; init; }
}

public record UpdateColorStatusRequest
{
    public bool IsActive { get; init; }
}

public class ColorDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameZh { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Hex { get; set; } = string.Empty;
    public string ColorFamily { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

- [ ] **Step 2: 创建 Specs**

对照 `backend/src/OneCup.Application/Specifications/NumberingDictionarySpecs.cs` 的同构结构（FilterSpec 无分页用于 Count / PagedSpec 含分页用于取页 / ActiveSpec 全启用 / ByIdSpec / ByCodeSpec 唯一性校验）。

Create `backend/src/OneCup.Application/Specifications/ColorSpecs.cs`:

```csharp
using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>颜色过滤规格（仅 keyword/colorFamily/isActive，不含分页）。用于 CountAsync 统计总数。</summary>
/// <remarks>多条件组合为单一 predicate 调一次 ApplyCriteria（基类覆盖语义，见 NumberingRuleFilterSpec 说明）。</remarks>
public class ColorFilterSpec : Specification<Color>
{
    public ColorFilterSpec(string? keyword, string? colorFamily, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        var fam = string.IsNullOrWhiteSpace(colorFamily) ? null : colorFamily.Trim();
        ApplyCriteria(c =>
            (kw == null || c.Code.Contains(kw) || c.NameZh.Contains(kw) || c.NameEn.Contains(kw)) &&
            (fam == null || c.ColorFamily == fam) &&
            (isActive == null || c.IsActive == isActive.Value));
    }
}

/// <summary>颜色分页查询（含 keyword/colorFamily/isActive 过滤，按 SortOrder 升序）。</summary>
public class ColorPagedSpec : Specification<Color>
{
    public ColorPagedSpec(string? keyword, string? colorFamily, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        var fam = string.IsNullOrWhiteSpace(colorFamily) ? null : colorFamily.Trim();
        ApplyCriteria(c =>
            (kw == null || c.Code.Contains(kw) || c.NameZh.Contains(kw) || c.NameEn.Contains(kw)) &&
            (fam == null || c.ColorFamily == fam) &&
            (isActive == null || c.IsActive == isActive.Value));
        ApplyOrderBy(c => c.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>颜色全部启用项（前端下拉用，按 SortOrder 升序）。</summary>
public class ColorActiveSpec : Specification<Color>
{
    public ColorActiveSpec()
    {
        ApplyCriteria(c => c.IsActive);
        ApplyOrderBy(c => c.SortOrder);
    }
}

public class ColorByIdSpec : Specification<Color>
{
    public ColorByIdSpec(Guid id) => ApplyCriteria(c => c.Id == id);
}

/// <summary>按 code 查找颜色（可选排除自身 Id）。不含 IsActive 过滤——
/// 用于 code 唯一性校验（停用也占用 code）。</summary>
public class ColorByCodeSpec : Specification<Color>
{
    public ColorByCodeSpec(string code, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(c => c.Code == code && (exclude == null || c.Id != exclude.Value));
    }
}
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: Build succeeded。

- [ ] **Step 4: Commit**

```bash
git add backend/src/OneCup.Application/Dtos/System/ColorDtos.cs backend/src/OneCup.Application/Specifications/ColorSpecs.cs
git commit -m "feat(color): DTOs + 查询规格"
```

---

## Task 4: ColorSpecs 单元测试（先红后绿——规格已实现，此 Task 验证正确性）

**Files:**
- Create: `backend/tests/OneCup.UnitTests/Color/ColorSpecsTests.cs`

**Interfaces:**
- Consumes: `ColorSpecs.*`（Task 3）、`Color`（Task 1）、`Repository<T>` + `OneCupDbContext`（用于 InMemory 实测）

> 这些测试针对 `Specification<T>` 的 Criteria 排序分页语义，通过真实 `Repository`（InMemory）验证。对照现有 `backend/tests/OneCup.UnitTests/Persistence/RepositorySpecificationTests.cs` 的测试范式（如有）或直接用 Repository+DbContext 构造。

- [ ] **Step 1: 写失败测试**

Create `backend/tests/OneCup.UnitTests/Color/ColorSpecsTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Color;

public class ColorSpecsTests
{
    private static OneCupDbContext CreateDb()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"color-specs-{Guid.NewGuid()}")
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

    private static Color Make(string code, string family = "红", bool active = true, int sort = 0) => new()
    {
        Code = code, NameZh = code, NameEn = code, Hex = "#FF0000",
        ColorFamily = family, IsActive = active, SortOrder = sort,
    };

    [Fact]
    public async Task ColorFilterSpec_Keyword_MatchesCodeNameZhNameEn()
    {
        var db = CreateDb();
        var repo = new Repository<Color>(db);
        await repo.AddAsync(Make("RED001", family: "红"));
        await repo.AddAsync(Make("BLU001", family: "蓝"));
        await db.SaveChangesAsync();

        // RED 命中 code
        var red = await repo.ListAsync(new ColorFilterSpec("RED", null, null));
        Assert.Single(red);
        Assert.Equal("RED001", red[0].Code);

        // 蓝色系也命中（family 在 PagedSpec 测，这里测 nameEn）
        var blu = await repo.ListAsync(new ColorFilterSpec("BLU", null, null));
        Assert.Single(blu);
    }

    [Fact]
    public async Task ColorFilterSpec_ColorFamily_ExactMatch()
    {
        var db = CreateDb();
        var repo = new Repository<Color>(db);
        await repo.AddAsync(Make("R1", family: "红"));
        await repo.AddAsync(Make("R2", family: "红"));
        await repo.AddAsync(Make("B1", family: "蓝"));
        await db.SaveChangesAsync();

        var reds = await repo.ListAsync(new ColorFilterSpec(null, "红", null));
        Assert.Equal(2, reds.Count);
    }

    [Fact]
    public async Task ColorFilterSpec_IsActive_Filters()
    {
        var db = CreateDb();
        var repo = new Repository<Color>(db);
        await repo.AddAsync(Make("A1", active: true));
        await repo.AddAsync(Make("A2", active: false));
        await db.SaveChangesAsync();

        var active = await repo.ListAsync(new ColorFilterSpec(null, null, true));
        Assert.Single(active);
        Assert.Equal("A1", active[0].Code);

        var inactive = await repo.ListAsync(new ColorFilterSpec(null, null, false));
        Assert.Single(inactive);
        Assert.Equal("A2", inactive[0].Code);
    }

    [Fact]
    public async Task ColorPagedSpec_AppliesSkipTakeAndOrderBy()
    {
        var db = CreateDb();
        var repo = new Repository<Color>(db);
        await repo.AddAsync(Make("C1", sort: 3));
        await repo.AddAsync(Make("C2", sort: 1));
        await repo.AddAsync(Make("C3", sort: 2));
        await db.SaveChangesAsync();

        // 第 1 页 size 2，按 SortOrder 升序 → C2(sort1), C3(sort2)
        var page1 = await repo.ListAsync(new ColorPagedSpec(null, null, null, 1, 2));
        Assert.Equal(2, page1.Count);
        Assert.Equal("C2", page1[0].Code);
        Assert.Equal("C3", page1[1].Code);
    }

    [Fact]
    public async Task ColorPagedSpec_CountUnaffectedByPaging()
    {
        // 关键：FilterSpec 统计 total 不受分页污染（编号字典曾因此出 bug）
        var db = CreateDb();
        var repo = new Repository<Color>(db);
        for (var i = 0; i < 5; i++)
            await repo.AddAsync(Make($"C{i}", family: "红"));
        await db.SaveChangesAsync();

        var total = await repo.CountAsync(new ColorFilterSpec(null, "红", null));
        Assert.Equal(5, total);   // 全部命中，不受分页影响
    }

    [Fact]
    public async Task ColorByCodeSpec_MatchesIgnoringExcludedId()
    {
        var db = CreateDb();
        var repo = new Repository<Color>(db);
        await repo.AddAsync(Make("RED001"));
        await db.SaveChangesAsync();
        var existing = await repo.FirstOrDefaultAsync(new ColorByCodeSpec("RED001"));

        // 排除自身 → 无匹配（用于编辑时唯一性校验）
        var excl = await repo.AnyAsync(new ColorByCodeSpec("RED001", existing!.Id));
        Assert.False(excl);

        // 不排除 → 有匹配
        var incl = await repo.AnyAsync(new ColorByCodeSpec("RED001"));
        Assert.True(incl);
    }

    [Fact]
    public async Task ColorActiveSpec_ReturnsOnlyActiveOrdered()
    {
        var db = CreateDb();
        var repo = new Repository<Color>(db);
        await repo.AddAsync(Make("A1", active: true, sort: 2));
        await repo.AddAsync(Make("A2", active: false, sort: 1));
        await repo.AddAsync(Make("A3", active: true, sort: 1));
        await db.SaveChangesAsync();

        var list = await repo.ListAsync(new ColorActiveSpec());
        Assert.Equal(2, list.Count);
        Assert.Equal("A3", list[0].Code);   // sort1 启用项在前
        Assert.Equal("A1", list[1].Code);
    }
}
```

- [ ] **Step 2: 运行测试**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~ColorSpecsTests"`
Expected: 7 passed。（规格已在 Task 3 实现，测试应直接通过——若失败说明 Task 3 的 predicate 写错，回去修 Task 3。）

- [ ] **Step 3: Commit**

```bash
git add backend/tests/OneCup.UnitTests/Color/ColorSpecsTests.cs
git commit -m "test(color): 颜色查询规格单元测试"
```

---

## Task 5: ColorService + 单元测试（TDD）

**Files:**
- Create: `backend/src/OneCup.Application/Interfaces/IColorService.cs`
- Create: `backend/src/OneCup.Application/Services/ColorService.cs`
- Create: `backend/tests/OneCup.UnitTests/Color/ColorServiceTests.cs`

**Interfaces:**
- Consumes: DTOs（Task 3）、Specs（Task 3）、`IRepository<Color>`、`IUnitOfWork`、`DomainException`（`OneCup.Domain.Exceptions`）
- Produces: `IColorService` 的 6 个方法（Task 6 Controller 依赖）

**hex 校验正则**：`^#[0-9A-Fa-f]{6}$`，非法 → 抛 `DomainException`（API 层映射 400，见 Global Constraints）。

- [ ] **Step 1: 写接口**

对照 `backend/src/OneCup.Application/Interfaces/INumberingDictionaryService.cs` 风格。

Create `backend/src/OneCup.Application/Interfaces/IColorService.cs`:

```csharp
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 颜色主数据管理服务（CRUD + 启停）。
/// code 创建后不可改；hex 格式校验；只启停不物理删除。
/// </summary>
public interface IColorService
{
    Task<PagedResult<ColorDto>> GetColorsAsync(
        int page, int pageSize, string? keyword, string? colorFamily, bool? isActive,
        CancellationToken ct = default);

    Task<List<ColorDto>> GetAllActiveColorsAsync(CancellationToken ct = default);

    Task<ColorDto?> GetColorAsync(Guid id, CancellationToken ct = default);

    Task<ColorDto> CreateColorAsync(CreateColorRequest request, CancellationToken ct = default);

    Task UpdateColorAsync(Guid id, UpdateColorRequest request, CancellationToken ct = default);

    Task UpdateColorStatusAsync(Guid id, bool isActive, CancellationToken ct = default);
}
```

- [ ] **Step 2: 写失败测试（先于实现）**

对照 `backend/tests/OneCup.UnitTests/NumberingDictionary/NumberingDictionaryServiceTests.cs` 的 Setup 范式（InMemory + Repository + UnitOfWork 构造 Service）。

Create `backend/tests/OneCup.UnitTests/Color/ColorServiceTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Application.Services;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Color;

public class ColorServiceTests
{
    private static (OneCupDbContext db, ColorService svc) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"color-{Guid.NewGuid()}")
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);
        var svc = new ColorService(new Repository<Color>(db), new UnitOfWork(db));
        return (db, svc);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    private static CreateColorRequest ValidCreate(string code = "RED001") => new()
    {
        Code = code, NameZh = "大红", NameEn = "Red",
        Hex = "#FF0000", ColorFamily = "红", SortOrder = 1,
    };

    // ── 新增 ──

    [Fact]
    public async Task CreateColorAsync_CreatesColor()
    {
        var (db, svc) = Setup();
        var dto = await svc.CreateColorAsync(ValidCreate());
        Assert.Equal("RED001", dto.Code);
        Assert.Equal("#FF0000", dto.Hex);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task CreateColorAsync_DuplicateCode_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateColorAsync(ValidCreate("RED001"));
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateColorAsync(ValidCreate("RED001")));
    }

    [Fact]
    public async Task CreateColorAsync_DuplicateCodeEvenWhenInactive_Throws()
    {
        // 停用的 code 仍占用（唯一性校验不含 IsActive 过滤）
        var (db, svc) = Setup();
        var dto = await svc.CreateColorAsync(ValidCreate("RED001"));
        await svc.UpdateColorStatusAsync(dto.Id, false);
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateColorAsync(ValidCreate("RED001")));
    }

    [Theory]
    [MemberData(nameof(InvalidHexCases))]
    public async Task CreateColorAsync_InvalidHex_Throws(string hex)
    {
        var (db, svc) = Setup();
        var req = ValidCreate() with { Hex = hex };
        await Assert.ThrowsAsync<DomainException>(() => svc.CreateColorAsync(req));
    }

    public static IEnumerable<object[]> InvalidHexCases => new[]
    {
        new object[] { "FF0000" },    // 缺 #
        new object[] { "#FF00" },     // 长度不足
        new object[] { "#GGGGGG" },   // 非法字符
        new object[] { "#ff00001" },  // 长度超
    };

    [Theory]
    [InlineData("#FF0000")]
    [InlineData("#ff0000")]
    [InlineData("#AbCdEf")]
    public async Task CreateColorAsync_ValidHex_Accepted(string hex)
    {
        var (db, svc) = Setup();
        var req = ValidCreate() with { Hex = hex };
        var dto = await svc.CreateColorAsync(req);
        Assert.Equal(hex, dto.Hex);
    }

    // ── 编辑 ──

    [Fact]
    public async Task UpdateColorAsync_CodeIgnored_FieldsUpdatable()
    {
        var (db, svc) = Setup();
        var dto = await svc.CreateColorAsync(ValidCreate());
        await svc.UpdateColorAsync(dto.Id, new UpdateColorRequest
        {
            NameZh = "大红改", Hex = "#EE0000", ColorFamily = "深红",
            Remark = "备注", SortOrder = 5,
        });
        var updated = await svc.GetColorAsync(dto.Id);
        Assert.Equal("RED001", updated!.Code);          // code 不变（接口不暴露 Code）
        Assert.Equal("大红改", updated.NameZh);
        Assert.Equal("#EE0000", updated.Hex);
        Assert.Equal("深红", updated.ColorFamily);
        Assert.Equal("备注", updated.Remark);
        Assert.Equal(5, updated.SortOrder);
    }

    [Fact]
    public async Task UpdateColorAsync_InvalidHex_Throws()
    {
        var (db, svc) = Setup();
        var dto = await svc.CreateColorAsync(ValidCreate());
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateColorAsync(dto.Id, new UpdateColorRequest { Hex = "nothex" }));
    }

    [Fact]
    public async Task UpdateColorAsync_NotFound_Throws()
    {
        var (db, svc) = Setup();
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateColorAsync(Guid.NewGuid(), new UpdateColorRequest { NameZh = "x" }));
    }

    // ── 启停 ──

    [Fact]
    public async Task UpdateColorStatusAsync_Toggles()
    {
        var (db, svc) = Setup();
        var dto = await svc.CreateColorAsync(ValidCreate());
        await svc.UpdateColorStatusAsync(dto.Id, false);
        var updated = await svc.GetColorAsync(dto.Id);
        Assert.False(updated!.IsActive);
    }

    // ── 查询 ──

    [Fact]
    public async Task GetColorsAsync_FiltersByKeyword()
    {
        var (db, svc) = Setup();
        await svc.CreateColorAsync(ValidCreate("RED001") with { NameZh = "大红" });
        await svc.CreateColorAsync(ValidCreate("BLU001") with { NameZh = "海蓝" });
        var res = await svc.GetColorsAsync(1, 10, "RED", null, null);
        Assert.Single(res.Items);
    }

    [Fact]
    public async Task GetColorsAsync_FiltersByColorFamily()
    {
        var (db, svc) = Setup();
        await svc.CreateColorAsync(ValidCreate("R1") with { ColorFamily = "红" });
        await svc.CreateColorAsync(ValidCreate("R2") with { ColorFamily = "红" });
        await svc.CreateColorAsync(ValidCreate("B1") with { ColorFamily = "蓝" });
        var res = await svc.GetColorsAsync(1, 10, null, "红", null);
        Assert.Equal(2, res.Total);
    }

    [Fact]
    public async Task GetColorsAsync_TotalUnaffectedByPaging()
    {
        var (db, svc) = Setup();
        for (var i = 0; i < 5; i++)
            await svc.CreateColorAsync(ValidCreate($"C{i}") with { ColorFamily = "红" });
        var res = await svc.GetColorsAsync(1, 2, null, "红", null);
        Assert.Equal(2, res.Items.Count);
        Assert.Equal(5, res.Total);   // total 不受分页污染
    }

    [Fact]
    public async Task GetAllActiveColorsAsync_ReturnsOnlyActiveOrdered()
    {
        var (db, svc) = Setup();
        var a = await svc.CreateColorAsync(ValidCreate("A1") with { SortOrder = 2 });
        var b = await svc.CreateColorAsync(ValidCreate("A2") with { SortOrder = 1 });
        await svc.UpdateColorStatusAsync(a.Id, false);
        var list = await svc.GetAllActiveColorsAsync();
        Assert.Single(list);
        Assert.Equal("A2", list[0].Code);
    }

    [Fact]
    public async Task GetColorAsync_NotFound_ReturnsNull()
    {
        var (db, svc) = Setup();
        var dto = await svc.GetColorAsync(Guid.NewGuid());
        Assert.Null(dto);
    }
}
```

- [ ] **Step 3: 运行测试确认失败**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~ColorServiceTests"`
Expected: 编译失败（`ColorService` 未定义）。这是预期的红。

- [ ] **Step 4: 实现 ColorService**

对照 `backend/src/OneCup.Application/Services/NumberingDictionaryService.cs` 的同构实现（DTO 映射 / 唯一性校验 / 启停 / 分页计数分离）。

Create `backend/src/OneCup.Application/Services/ColorService.cs`:

```csharp
using System.Text.RegularExpressions;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 颜色主数据管理服务实现。通过 IRepository + Specification 访问数据，IUnitOfWork 提交。
/// code 创建后不可改；hex 格式校验；只启停不物理删除。
/// </summary>
public class ColorService : IColorService
{
    private static readonly Regex HexRegex = new(
        @"^#[0-9A-Fa-f]{6}$", RegexOptions.Compiled);

    private readonly IRepository<Color> _colors;
    private readonly IUnitOfWork _uow;

    public ColorService(IRepository<Color> colors, IUnitOfWork uow)
    {
        _colors = colors;
        _uow = uow;
    }

    public async Task<PagedResult<ColorDto>> GetColorsAsync(
        int page, int pageSize, string? keyword, string? colorFamily, bool? isActive,
        CancellationToken ct = default)
    {
        // 关键:总数用仅含过滤条件的 FilterSpec 统计,绝不能用带分页的 PagedSpec,
        // 否则 Repository.CountAsync 会应用 Skip/Take,只统计当前页子集。
        var total = await _colors.CountAsync(
            new ColorFilterSpec(keyword, colorFamily, isActive), ct);
        var colors = await _colors.ListAsync(
            new ColorPagedSpec(keyword, colorFamily, isActive, page, pageSize), ct);

        return new PagedResult<ColorDto>
        {
            Items = colors.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<List<ColorDto>> GetAllActiveColorsAsync(CancellationToken ct = default)
    {
        var colors = await _colors.ListAsync(new ColorActiveSpec(), ct);
        return colors.Select(ToDto).ToList();
    }

    public async Task<ColorDto?> GetColorAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _colors.FirstOrDefaultAsync(new ColorByIdSpec(id), ct);
        return c is null ? null : ToDto(c);
    }

    public async Task<ColorDto> CreateColorAsync(CreateColorRequest request, CancellationToken ct = default)
    {
        ValidateHex(request.Hex);

        // code 唯一性:用 ColorByCodeSpec(不含 IsActive 过滤),停用也占用 code
        if (await _colors.AnyAsync(new ColorByCodeSpec(request.Code), ct))
            throw new DomainException($"颜色 code '{request.Code}' 已存在");

        var entity = new Color
        {
            Code = request.Code,
            NameZh = request.NameZh,
            NameEn = request.NameEn,
            Hex = request.Hex,
            ColorFamily = request.ColorFamily,
            Remark = request.Remark,
            SortOrder = request.SortOrder,
            IsActive = true,
        };
        await _colors.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task UpdateColorAsync(Guid id, UpdateColorRequest request, CancellationToken ct = default)
    {
        // code 不可改:更新接口不暴露 Code 字段,无需特殊处理
        var entity = await _colors.FirstOrDefaultAsync(new ColorByIdSpec(id), ct)
            ?? throw new DomainException("颜色不存在");

        if (request.Hex is not null)
        {
            ValidateHex(request.Hex);
            entity.Hex = request.Hex;
        }
        if (request.NameZh is not null) entity.NameZh = request.NameZh;
        if (request.NameEn is not null) entity.NameEn = request.NameEn;
        if (request.ColorFamily is not null) entity.ColorFamily = request.ColorFamily;
        if (request.Remark is not null) entity.Remark = request.Remark;
        if (request.SortOrder is not null) entity.SortOrder = request.SortOrder.Value;

        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdateColorStatusAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var entity = await _colors.FirstOrDefaultAsync(new ColorByIdSpec(id), ct)
            ?? throw new DomainException("颜色不存在");
        entity.IsActive = isActive;
        await _uow.SaveChangesAsync(ct);
    }

    // ── 内部工具 ──

    private static void ValidateHex(string hex)
    {
        if (!HexRegex.IsMatch(hex))
            throw new DomainException($"颜色值 '{hex}' 格式非法，必须为 #RRGGBB（如 #FF0000）");
    }

    private static ColorDto ToDto(Color c) => new()
    {
        Id = c.Id,
        Code = c.Code,
        NameZh = c.NameZh,
        NameEn = c.NameEn,
        Hex = c.Hex,
        ColorFamily = c.ColorFamily,
        Remark = c.Remark,
        SortOrder = c.SortOrder,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
    };
}
```

- [ ] **Step 5: 运行测试确认通过**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~ColorServiceTests"`
Expected: 全部 passed（含 hex 合法/非法 Theory、唯一性、编辑、启停、查询）。

- [ ] **Step 6: 运行全部 Color 测试**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~OneCup.UnitTests.Color"`
Expected: Task 4 + Task 5 全部 passed。

- [ ] **Step 7: Commit**

```bash
git add backend/src/OneCup.Application/Interfaces/IColorService.cs backend/src/OneCup.Application/Services/ColorService.cs backend/tests/OneCup.UnitTests/Color/ColorServiceTests.cs
git commit -m "feat(color): ColorService + 服务层单元测试（hex 校验/唯一性/CRUD）"
```

---

## Task 6: Controller + 授权策略 + DI 注册

**Files:**
- Create: `backend/src/OneCup.Api/Controllers/ColorController.cs`
- Modify: `backend/src/OneCup.Api/Program.cs`（加 2 条策略 + 1 条 DI）

**Interfaces:**
- Consumes: `IColorService`（Task 5）
- Produces: HTTP 端点 `GET/POST/PUT /api/colors*`（Task 8 前端依赖）

**授权策略**（关键，契约 §3.5 低风险纯追加）：现有策略块全是 `system:*`，颜色是第一个业务模块——新增 `color-view`(→`color:read`)、`color-manage`(→`color:write`) 两条。

- [ ] **Step 1: 写 Controller**

对照 `backend/src/OneCup.Api/Controllers/NumberingDictionaryController.cs` 的端点结构（`[ApiController]` + `[Route]` + `[Authorize(Policy=...)]` + `[FromQuery]` 分页参数）。

Create `backend/src/OneCup.Api/Controllers/ColorController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 颜色主数据管理端点。
/// 权限：color-view(perm color:read) / color-manage(perm color:write)。
/// </summary>
[ApiController]
[Route("api/colors")]
public class ColorController : ControllerBase
{
    private readonly IColorService _svc;

    public ColorController(IColorService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    [Authorize(Policy = "color-view")]
    public async Task<IActionResult> GetColors(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null, [FromQuery] string? colorFamily = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _svc.GetColorsAsync(page, pageSize, keyword, colorFamily, isActive, ct);
        return Ok(result);
    }

    [HttpGet("all")]
    [Authorize(Policy = "color-view")]
    public async Task<IActionResult> GetAllActiveColors(CancellationToken ct)
    {
        var list = await _svc.GetAllActiveColorsAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "color-view")]
    public async Task<IActionResult> GetColor(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetColorAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = "color-manage")]
    public async Task<IActionResult> CreateColor([FromBody] CreateColorRequest request, CancellationToken ct)
    {
        var dto = await _svc.CreateColorAsync(request, ct);
        return CreatedAtAction(nameof(GetColor), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "color-manage")]
    public async Task<IActionResult> UpdateColor(Guid id, [FromBody] UpdateColorRequest request, CancellationToken ct)
    {
        await _svc.UpdateColorAsync(id, request, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "color-manage")]
    public async Task<IActionResult> UpdateColorStatus(Guid id, [FromBody] UpdateColorStatusRequest request, CancellationToken ct)
    {
        await _svc.UpdateColorStatusAsync(id, request.IsActive, ct);
        return NoContent();
    }
}
```

- [ ] **Step 2: 加授权策略**

Read `backend/src/OneCup.Api/Program.cs`，定位 `options.AddPolicy("audit-view", ...)` 那一块（约 154-156 行），在其后、`});` 闭合之前追加两条策略：

```csharp
    options.AddPolicy("color-view", policy =>
        policy.RequireClaim("perm_codes", "color:read"));
    options.AddPolicy("color-manage", policy =>
        policy.RequireClaim("perm_codes", "color:write"));
```

> 完整上下文：插入后该块应类似
> ```csharp
>     options.AddPolicy("audit-view", policy =>
>         policy.RequireClaim("perm_codes", "system:audit:view"));
>     options.AddPolicy("color-view", policy =>
>         policy.RequireClaim("perm_codes", "color:read"));
>     options.AddPolicy("color-manage", policy =>
>         policy.RequireClaim("perm_codes", "color:write"));
> });
> ```

- [ ] **Step 3: 加 DI 注册**

在 Program.cs 现有 `builder.Services.AddScoped<INumberingDictionaryService, NumberingDictionaryService>();`（约 111 行）之后追加：

```csharp
builder.Services.AddScoped<IColorService, ColorService>();
```

- [ ] **Step 4: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: Build succeeded。

- [ ] **Step 5: 后端全量测试回归**

Run: `dotnet test backend/OneCup.sln`
Expected: 全部 passed（颜色新测试 + 既有测试无回归）。

- [ ] **Step 6: Commit**

```bash
git add backend/src/OneCup.Api/Controllers/ColorController.cs backend/src/OneCup.Api/Program.cs
git commit -m "feat(color): ColorController + color-view/manage 授权策略 + DI"
```

---

## Task 7: 迁移应用到本地库验证

**Files:** 无新增（验证 Task 2 的迁移能正确应用）

- [ ] **Step 1: 确认本地 PG 可用**

检查 `docker-compose` 或本地 PostgreSQL 是否运行（项目用 docker-compose + 健康探针，见近期 commit `51c8adf`）。如未运行，按 `infra/` 或 `docker-compose.yml` 启动。

- [ ] **Step 2: 应用迁移**

```bash
dotnet ef database update --project backend/src/OneCup.Infrastructure --startup-project backend/src/OneCup.Api
```

Expected: 无错误，输出 `Done.`，`colors` 表 + `ux_colors_code` 索引创建成功。

- [ ] **Step 3: （可选）验证表结构**

用 psql / 数据库工具确认 `colors` 表存在，列与 Task 1 配置一致（`code varchar(32) not null`、`hex char(7) not null`、唯一索引 `ux_colors_code`）。

> 若这一步失败（如连接串/PG 未起），不要阻塞——记录问题，前端可继续开发（前端用 mock 或后续联调）。但**合并前必须验证通过**（契约 §4.5）。

- [ ] **Step 4: Commit（如有 ModelSnapshot 变动）**

通常 Task 2 已提交迁移文件，此步无新文件。若 `database update` 触发了任何文件变更才提交；否则跳过。

---

## Task 8: 前端 API 客户端

**Files:**
- Create: `frontend/src/api/color.ts`

**Interfaces:**
- Consumes: `request`（`frontend/src/api/request.ts`）、`PagedResult`（`frontend/src/api/user.ts`）
- Produces: TS 类型 `Color` / `CreateColorRequest` / `UpdateColorRequest` + 6 个请求函数（Task 9 页面依赖）

- [ ] **Step 1: 创建 API 客户端**

对照 `frontend/src/api/numberingDictionary.ts` 的类型 + 函数风格（`request.get<unknown, T>` 泛型 + params）。

Create `frontend/src/api/color.ts`:

```typescript
import request from './request';
import { PagedResult } from './user';

// ── 类型 ──
export interface Color {
  id: string;
  code: string;
  nameZh: string;
  nameEn: string;
  hex: string;
  colorFamily: string;
  remark?: string;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateColorRequest {
  code: string;
  nameZh: string;
  nameEn: string;
  hex: string;
  colorFamily: string;
  remark?: string;
  sortOrder: number;
}

export interface UpdateColorRequest {
  nameZh?: string;
  nameEn?: string;
  hex?: string;
  colorFamily?: string;
  remark?: string;
  sortOrder?: number;
}

// ── 请求函数 ──
export function getColors(params: {
  page?: number;
  pageSize?: number;
  keyword?: string;
  colorFamily?: string;
  isActive?: boolean;
}) {
  return request.get<unknown, PagedResult<Color>>('/api/colors', { params });
}

export function getAllActiveColors() {
  return request.get<unknown, Color[]>('/api/colors/all');
}

export function getColor(id: string) {
  return request.get<unknown, Color>(`/api/colors/${id}`);
}

export function createColor(data: CreateColorRequest) {
  return request.post<unknown, Color>('/api/colors', data);
}

export function updateColor(id: string, data: UpdateColorRequest) {
  return request.put(`/api/colors/${id}`, data);
}

export function updateColorStatus(id: string, isActive: boolean) {
  return request.put(`/api/colors/${id}/status`, { isActive });
}
```

- [ ] **Step 2: 类型检查**

Run: `cd frontend && npx tsc --noEmit`
Expected: 无类型错误。

- [ ] **Step 3: Commit**

```bash
git add frontend/src/api/color.ts
git commit -m "feat(color): 前端 API 客户端"
```

---

## Task 9: 前端查询表格页 + locale + 样式

**Files:**
- Create: `frontend/src/pages/master-data/color/index.tsx`
- Create: `frontend/src/pages/master-data/color/locale/index.ts`
- Create: `frontend/src/pages/master-data/color/locale/zh-CN.ts`
- Create: `frontend/src/pages/master-data/color/locale/en-US.ts`
- Create: `frontend/src/pages/master-data/color/style/index.module.less`

**Interfaces:**
- Consumes: `frontend/src/api/color.ts`（Task 8）、`useLocale`（`frontend/src/utils/useLocale`）、查询表格模板 `docs/specs/templates/query-table-page.template.tsx`
- Produces: 默认导出的 React 组件 `ColorPage`（Task 10 路由依赖）

**AGENTS.md 合规**：严格套用查询表格模板——单 `<Card>` 包整页 / `Form`+`Grid` 三列 / 查询重置按钮在表单外侧兄弟 div / 仅按钮触发查询 / 工具栏 flex space-between。

- [ ] **Step 1: 创建样式（复制模板三段标准样式）**

Create `frontend/src/pages/master-data/color/style/index.module.less`（从 `docs/specs/templates/query-table-page.module.less.template` 原样复制，三段为固定标准）:

```less
/* 查询区：表单 + 右侧按钮列，flex 并排 */
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

/* 表格工具栏 */
.button-group {
  display: flex;
  justify-content: space-between;
  margin-bottom: 20px;
}

/* 色块圆点 */
.color-swatch {
  display: inline-block;
  width: 18px;
  height: 18px;
  border-radius: 50%;
  border: 1px solid var(--color-border-2);
  vertical-align: middle;
}
```

- [ ] **Step 2: 创建 locale 三件套**

Create `frontend/src/pages/master-data/color/locale/zh-CN.ts`:

```typescript
export default {
  'color.title': '颜色',
  // 查询区
  'color.search.keyword': '关键字',
  'color.search.colorFamily': '颜色系',
  'color.search.status': '状态',
  'color.search.submit': '查询',
  'color.search.reset': '重置',
  // 表格列
  'color.column.swatch': '色块',
  'color.column.code': '编码',
  'color.column.nameZh': '中文名',
  'color.column.nameEn': '英文名',
  'color.column.colorFamily': '颜色系',
  'color.column.sortOrder': '排序',
  'color.column.status': '状态',
  'color.column.operations': '操作',
  // 状态
  'color.active': '启用',
  'color.inactive': '停用',
  // 操作
  'color.create': '新建颜色',
  'color.edit': '编辑',
  'color.enable': '启用',
  'color.disable': '停用',
  'color.disable.confirm': '确定要停用吗？',
  'color.enable.confirm': '确定要启用吗？',
  'color.create.success': '创建成功',
  'color.update.success': '更新成功',
  'color.status.success': '状态已更新',
  // 表单
  'color.form.create': '新建颜色',
  'color.form.edit': '编辑颜色',
  'color.form.code': '编码',
  'color.form.code.placeholder': '如 RED001',
  'color.form.nameZh': '中文名',
  'color.form.nameEn': '英文名',
  'color.form.hex': '颜色值',
  'color.form.hex.placeholder': '如 #FF0000',
  'color.form.colorFamily': '颜色系',
  'color.form.sortOrder': '排序号',
  'color.form.remark': '备注',
  'color.form.lockedHint': '编码创建后不可修改',
  'color.form.required': '该项为必填',
  'color.form.hex.invalid': '请输入 #RRGGBB 格式（如 #FF0000）',
  // 颜色系常用项
  'color.family.red': '红',
  'color.family.orange': '橙',
  'color.family.yellow': '黄',
  'color.family.green': '绿',
  'color.family.blue': '蓝',
  'color.family.purple': '紫',
  'color.family.neutral': '中性',
  'color.family.gray': '黑白灰',
};
```

Create `frontend/src/pages/master-data/color/locale/en-US.ts`:

```typescript
export default {
  'color.title': 'Colors',
  'color.search.keyword': 'Keyword',
  'color.search.colorFamily': 'Color Family',
  'color.search.status': 'Status',
  'color.search.submit': 'Search',
  'color.search.reset': 'Reset',
  'color.column.swatch': 'Swatch',
  'color.column.code': 'Code',
  'color.column.nameZh': 'Name (ZH)',
  'color.column.nameEn': 'Name (EN)',
  'color.column.colorFamily': 'Family',
  'color.column.sortOrder': 'Sort',
  'color.column.status': 'Status',
  'color.column.operations': 'Operations',
  'color.active': 'Active',
  'color.inactive': 'Inactive',
  'color.create': 'New Color',
  'color.edit': 'Edit',
  'color.enable': 'Enable',
  'color.disable': 'Disable',
  'color.disable.confirm': 'Disable this color?',
  'color.enable.confirm': 'Enable this color?',
  'color.create.success': 'Created',
  'color.update.success': 'Updated',
  'color.status.success': 'Status updated',
  'color.form.create': 'New Color',
  'color.form.edit': 'Edit Color',
  'color.form.code': 'Code',
  'color.form.code.placeholder': 'e.g. RED001',
  'color.form.nameZh': 'Name (ZH)',
  'color.form.nameEn': 'Name (EN)',
  'color.form.hex': 'Hex',
  'color.form.hex.placeholder': 'e.g. #FF0000',
  'color.form.colorFamily': 'Color Family',
  'color.form.sortOrder': 'Sort Order',
  'color.form.remark': 'Remark',
  'color.form.lockedHint': 'Code cannot be changed after creation',
  'color.form.required': 'This field is required',
  'color.form.hex.invalid': 'Enter #RRGGBB format (e.g. #FF0000)',
  'color.family.red': 'Red',
  'color.family.orange': 'Orange',
  'color.family.yellow': 'Yellow',
  'color.family.green': 'Green',
  'color.family.blue': 'Blue',
  'color.family.purple': 'Purple',
  'color.family.neutral': 'Neutral',
  'color.family.gray': 'Gray/Black/White',
};
```

Create `frontend/src/pages/master-data/color/locale/index.ts`:

```typescript
import zhCN from './zh-CN';
import enUS from './en-US';

export default { 'zh-CN': zhCN, 'en-US': enUS };
```

- [ ] **Step 3: 创建查询表格页**

严格套用 `docs/specs/templates/query-table-page.template.tsx` 骨架（Card + SearchForm + 工具栏 + 受控 Table），新增 Drawer 表单（参考 `frontend/src/pages/system/numbering/dict/index.tsx` 的抽屉交互）。

Create `frontend/src/pages/master-data/color/index.tsx`:

```tsx
import { useEffect, useMemo, useState, useCallback } from 'react';
import {
  Button, Card, Drawer, Form, Grid, Input, InputNumber, Select, Space,
  Table, Tag, Popconfirm, Message, Typography,
} from '@arco-design/web-react';
import { IconPlus, IconRefresh, IconSearch } from '@arco-design/web-react/icon';
import useLocale from '@/utils/useLocale';
import {
  getColors, createColor, updateColor, updateColorStatus,
  Color, CreateColorRequest,
} from '@/api/color';
import locale from './locale';
import styles from './style/index.module.less';

const { Title } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;
const { Option } = Select;

// 颜色系常用项（写死 + allowCreate）
const FAMILY_OPTIONS = [
  'red', 'orange', 'yellow', 'green', 'blue', 'purple', 'neutral', 'gray',
];

const HEX_PATTERN = /^#[0-9A-Fa-f]{6}$/;

function SearchForm({ onSearch }: { onSearch: (v: Record<string, any>) => void }) {
  const t = useLocale(locale);
  const [form] = Form.useForm();

  const handleSubmit = () => onSearch(form.getFieldsValue());
  const handleReset = () => { form.resetFields(); onSearch({}); };

  return (
    <div className={styles['search-form-wrapper']}>
      <Form
        form={form}
        className={styles['search-form']}
        labelAlign="left"
        labelCol={{ span: 8 }}
        wrapperCol={{ span: 16 }}
      >
        <Row gutter={24}>
          <Col span={8}>
            <FormItem label={t['color.search.keyword']} field="keyword">
              <Input allowClear placeholder="" />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['color.search.colorFamily']} field="colorFamily">
              <Select allowClear showSearch allowCreate>
                {FAMILY_OPTIONS.map((f) => (
                  <Option key={f} value={t[`color.family.${f}`]}>{t[`color.family.${f}`]}</Option>
                ))}
              </Select>
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['color.search.status']} field="isActive">
              <Select allowClear>
                <Option value={true}>{t['color.active']}</Option>
                <Option value={false}>{t['color.inactive']}</Option>
              </Select>
            </FormItem>
          </Col>
        </Row>
      </Form>
      <div className={styles['right-button']}>
        <Button type="primary" icon={<IconSearch />} onClick={handleSubmit}>
          {t['color.search.submit']}
        </Button>
        <Button icon={<IconRefresh />} onClick={handleReset}>
          {t['color.search.reset']}
        </Button>
      </div>
    </div>
  );
}

export default function ColorPage() {
  const t = useLocale(locale);
  const [data, setData] = useState<Color[]>([]);
  const [loading, setLoading] = useState(false);
  const [formParams, setFormParams] = useState<Record<string, any>>({});
  const [pagination, setPagination] = useState({
    sizeCanChange: true, showTotal: true, pageSize: 10, current: 1,
    pageSizeChangeResetCurrent: true,
  });

  // 抽屉
  const [drawerVisible, setDrawerVisible] = useState(false);
  const [editMode, setEditMode] = useState<'create' | 'edit'>('create');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingCode, setEditingCode] = useState<string>('');
  const [form] = Form.useForm();

  const fetchColors = useCallback(() => {
    const { current, pageSize } = pagination;
    setLoading(true);
    getColors({ page: current, pageSize, ...formParams })
      .then((res) => {
        setData(res.items);
        setPagination((p) => ({ ...p, total: res.total }));
      })
      .finally(() => setLoading(false));
  }, [pagination.current, pagination.pageSize, JSON.stringify(formParams)]);

  useEffect(() => { fetchColors(); }, [fetchColors]);

  function handleSearch(params: Record<string, any>) {
    setPagination((p) => ({ ...p, current: 1 }));
    setFormParams(params);
  }

  function onChangeTable({ current, pageSize }: { current: number; pageSize: number }) {
    setPagination((p) => ({ ...p, current, pageSize }));
  }

  function openCreate() {
    setEditMode('create');
    setEditingId(null);
    setEditingCode('');
    form.resetFields();
    form.setFieldsValue({ sortOrder: 0 });
    setDrawerVisible(true);
  }

  function openEdit(record: Color) {
    setEditMode('edit');
    setEditingId(record.id);
    setEditingCode(record.code);
    form.resetFields();
    form.setFieldsValue({
      nameZh: record.nameZh, nameEn: record.nameEn, hex: record.hex,
      colorFamily: record.colorFamily, sortOrder: record.sortOrder, remark: record.remark,
    });
    setDrawerVisible(true);
  }

  async function handleDrawerOk() {
    try {
      const values = await form.validate();
      if (editMode === 'create') {
        await createColor(values as CreateColorRequest);
        Message.success(t['color.create.success']);
      } else {
        await updateColor(editingId!, {
          nameZh: values.nameZh, nameEn: values.nameEn, hex: values.hex,
          colorFamily: values.colorFamily, sortOrder: values.sortOrder, remark: values.remark,
        });
        Message.success(t['color.update.success']);
      }
      setDrawerVisible(false);
      fetchColors();
    } catch {
      // 校验失败或 API 错误
    }
  }

  async function handleToggleStatus(record: Color) {
    await updateColorStatus(record.id, !record.isActive);
    Message.success(t['color.status.success']);
    fetchColors();
  }

  const columns = useMemo(() => [
    {
      title: t['color.column.swatch'], dataIndex: 'hex', width: 70,
      render: (hex: string) => (
        <span className={styles['color-swatch']} style={{ background: hex }} />
      ),
    },
    { title: t['color.column.code'], dataIndex: 'code', width: 120 },
    { title: t['color.column.nameZh'], dataIndex: 'nameZh', width: 120 },
    { title: t['color.column.nameEn'], dataIndex: 'nameEn', width: 120 },
    { title: t['color.column.colorFamily'], dataIndex: 'colorFamily', width: 100 },
    { title: t['color.column.sortOrder'], dataIndex: 'sortOrder', width: 80 },
    {
      title: t['color.column.status'], dataIndex: 'isActive', width: 90,
      render: (v: boolean) => v
        ? <Tag color="green">{t['color.active']}</Tag>
        : <Tag>{t['color.inactive']}</Tag>,
    },
    {
      title: t['color.column.operations'], dataIndex: 'operations', width: 160,
      render: (_: unknown, record: Color) => (
        <Space>
          <Button type="text" size="small" onClick={() => openEdit(record)}>
            {t['color.edit']}
          </Button>
          <Popconfirm
            title={record.isActive
              ? t['color.disable.confirm'] : t['color.enable.confirm']}
            onOk={() => handleToggleStatus(record)}
          >
            <Button type="text" size="small" status={record.isActive ? 'warning' : 'success'}>
              {record.isActive ? t['color.disable'] : t['color.enable']}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ], [t]);

  return (
    <Card>
      <Title heading={6}>{t['color.title']}</Title>
      <SearchForm onSearch={handleSearch} />
      <div className={styles['button-group']}>
        <Space>
          <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
            {t['color.create']}
          </Button>
        </Space>
        <Space />
      </div>
      <Table
        rowKey="id"
        loading={loading}
        onChange={onChangeTable}
        pagination={pagination}
        columns={columns}
        data={data}
      />

      <Drawer
        title={editMode === 'create' ? t['color.form.create'] : t['color.form.edit']}
        visible={drawerVisible}
        onOk={handleDrawerOk}
        onCancel={() => setDrawerVisible(false)}
        width={440}
        unmountOnExit
      >
        <Form form={form} layout="vertical">
          {editMode === 'create' ? (
            <FormItem
              label={t['color.form.code']}
              field="code"
              rules={[{ required: true, message: t['color.form.required'] }]}
            >
              <Input placeholder={t['color.form.code.placeholder']} />
            </FormItem>
          ) : (
            <FormItem label={t['color.form.code']}>
              <Input disabled value={editingCode} />
            </FormItem>
          )}
          <FormItem
            label={t['color.form.nameZh']}
            field="nameZh"
            rules={[{ required: true, message: t['color.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem
            label={t['color.form.nameEn']}
            field="nameEn"
            rules={[{ required: true, message: t['color.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem
            label={t['color.form.hex']}
            field="hex"
            rules={[
              { required: true, message: t['color.form.required'] },
              {
                validator: (v, cb) =>
                  !v || HEX_PATTERN.test(v) ? cb() : cb(t['color.form.hex.invalid']),
              },
            ]}
          >
            <Input placeholder={t['color.form.hex.placeholder']} />
          </FormItem>
          <FormItem label={t['color.form.colorFamily']} field="colorFamily">
            <Select showSearch allowCreate>
              {FAMILY_OPTIONS.map((f) => (
                <Option key={f} value={t[`color.family.${f}`]}>{t[`color.family.${f}`]}</Option>
              ))}
            </Select>
          </FormItem>
          <FormItem label={t['color.form.sortOrder']} field="sortOrder">
            <InputNumber min={0} style={{ width: '100%' }} />
          </FormItem>
          <FormItem label={t['color.form.remark']} field="remark">
            <Input.TextArea />
          </FormItem>
        </Form>
      </Drawer>
    </Card>
  );
}
```

- [ ] **Step 4: 类型检查**

Run: `cd frontend && npx tsc --noEmit`
Expected: 无类型错误。

- [ ] **Step 5: Commit**

```bash
git add frontend/src/pages/master-data/color/
git commit -m "feat(color): 前端查询表格页（色块/查询/抽屉表单）"
```

---

## Task 10: 路由 + 菜单 + locale 追加（前端共享文件）

**Files:**
- Modify: `frontend/src/routes.ts`（routes 数组末尾加 masterData 顶级项）
- Modify: `frontend/src/router.tsx`（lazy import + 路由 element）
- Modify: `frontend/src/locale/index.ts`（en-US/zh-CN 各加 menu.masterData）

**Interfaces:**
- Consumes: `ColorPage`（Task 9 默认导出）、`RequirePermission`（`frontend/src/components/RequirePermission`）

**导航决策**：新增顶级菜单「基础设置」(`menu.masterData`)，颜色为子项 `menu.masterData.color`。权限码 `color:read` 经 `transformPermissions` 解析为 `{color:['read']}`，菜单/路由用 `resource="color" actions={['read']}` 校验。

- [ ] **Step 1: routes.ts 追加顶级菜单**

Read `frontend/src/routes.ts`，在 `routes` 数组的 `menu.system` 顶级项**之后**（数组末尾、闭合 `]` 之前）追加第二个顶级项：

```typescript
  {
    name: 'menu.masterData',
    key: 'master-data',
    children: [
      {
        name: 'menu.masterData.color',
        key: 'master-data/color',
        requiredPermissions: [
          { resource: 'color', actions: ['read'] },
        ],
      },
    ],
  },
```

> 完整结构：`routes` 数组现有 `[{ name: 'menu.system', ... }]`，追加后变为 `[{ menu.system }, { menu.masterData }]`。`useRoute` 递归渲染，第二个顶级菜单自动出现，无需改 layout。

- [ ] **Step 2: router.tsx 追加 lazy import + 路由**

Read `frontend/src/router.tsx`，在现有 lazy import 块（`const LoginLogPage = lazy(...)` 之后）追加：

```typescript
const ColorPage = lazy(() => import('@/pages/master-data/color'));
```

在根路由 `children` 数组（`system/login-log` 路由之后、闭合 `]` 之前）追加：

```typescript
      {
        path: 'master-data/color',
        element: withSuspense(
          <RequirePermission resource="color" actions={['read']}>
            <ColorPage />
          </RequirePermission>
        ),
      },
```

- [ ] **Step 3: locale/index.ts 追加菜单文案**

Read `frontend/src/locale/index.ts`，在 `'en-US'` 对象的 `'menu.system.loginLog': 'Login Log',` 之后追加：

```typescript
    'menu.masterData': 'Master Data',
    'menu.masterData.color': 'Colors',
```

在 `'zh-CN'` 对象的 `'menu.system.loginLog': '登录日志',` 之后追加：

```typescript
    'menu.masterData': '基础设置',
    'menu.masterData.color': '颜色',
```

- [ ] **Step 4: 类型检查 + 构建**

Run: `cd frontend && npx tsc --noEmit && npm run build`
Expected: 构建成功，无类型错误。

- [ ] **Step 5: Commit**

```bash
git add frontend/src/routes.ts frontend/src/router.tsx frontend/src/locale/index.ts
git commit -m "feat(color): 前端路由 + 基础设置菜单 + locale"
```

---

## Task 11: 全栈联调验证

**Files:** 无（验证整体可运行）

- [ ] **Step 1: 后端构建 + 全量测试**

```bash
dotnet build backend/OneCup.sln
dotnet test backend/OneCup.sln
```
Expected: build succeeded + 全部测试 passed。

- [ ] **Step 2: 前端构建 + 测试**

```bash
cd frontend && npm run build && npm test
```
Expected: 构建成功，既有测试无回归。

- [ ] **Step 3: （可选，需后端运行）手动冒烟**

启动后端，以 admin 登录（admin 拥有通配 `*` 权限），访问「基础设置 > 颜色」：
- 新建一个颜色（如 RED001 / 大红 / #FF0000 / 红），看色块是否显示
- 编辑、启停、查询筛选是否生效

- [ ] **Step 4: 并行合规最终自检**

确认：
- `git log --oneline` 显示本分支的 color 提交，无对单位模块文件的改动
- `SeedData.cs` 未被修改（`git diff main -- backend/src/OneCup.Infrastructure/Persistence/SeedData.cs` 应为空）
- 无新增 Guid 常量
- 迁移文件名含 `AddColorModule`

```bash
git diff main -- backend/src/OneCup.Infrastructure/Persistence/SeedData.cs   # 应无输出
git diff main --stat | grep -i unit                                          # 应无单位模块文件
```

Expected: SeedData 无 diff；无 unit 文件改动。

- [ ] **Step 5: 最终提交（如有遗留改动）**

如自检发现任何遗漏改动，补提交。否则本 Task 无新 commit。

---

## 完成标志

全部 Task 完成后：
- 后端：颜色 CRUD 可用，hex 校验生效，授权策略正确（admin 通配、developer 无 color:write 不能改）
- 前端：「基础设置 > 颜色」页可访问，查询/新建/编辑/启停全链路通
- 测试：颜色单元测试全绿，既有测试无回归
- 并行合规：零新增 Guid、迁移命名正确、共享文件末尾追加、SeedData 未动

后续合并按 `docs/parallel-dev-contract.md` §4（方案 B）：开发期不 rebase，合并时统一处理 ModelSnapshot 冲突（§4.4）。
