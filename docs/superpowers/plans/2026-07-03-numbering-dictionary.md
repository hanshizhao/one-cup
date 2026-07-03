# 编号模块字典化 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 为编号引擎新增"业务类型字典"+"分类字典"两张可配置表，引擎生成时强校验合法性，前端提供主从表联动管理页，实现未来业务出现时纯配置不改代码。

**Architecture:** 遵循现有 Clean Architecture 四层（Domain → Application → Infrastructure → Api）。两张新表复用 `BaseEntity` / `IRepository<T>` / `Specification<T>` / `PagedResult<T>` 既有模式，与 `NumberingRule` 完全同构。引擎改造仅在 `NumberingService` 两个方法中插入字典校验。前端新增独立字典管理页（主从表）+ 改造现有规则抽屉下拉为动态拉取。

**Tech Stack:** .NET 10, EF Core (PostgreSQL), ASP.NET Core, React + Arco Design Pro, TypeScript, Vite

## Global Constraints

- 实体继承 `OneCup.Domain.Entities.BaseEntity`（`Id`/`CreatedAt`/`UpdatedAt`）
- EF 配置放在 `OneCup.Infrastructure.Persistence.Configurations`，`ToTable` 用 snake_case 表名，`HasColumnName` 用 snake_case 列名
- Service 接口在 `OneCup.Application.Interfaces`，实现在 `OneCup.Application.Services`，通过 `IRepository<T>` + `IUnitOfWork` 访问数据，不直接依赖 EF Core
- Specification 继承 `Specification<T>`，多条件必须组合为单一 predicate 调一次 `ApplyCriteria`（基类是覆盖语义）
- 种子 Guid 用确定性常量 `Guid.Parse("...")`，`HasData` 时间戳用 `SeedTimestamp = new(2026,7,1,0,0,0,DateTimeKind.Utc)`
- 迁移命令：`dotnet ef migrations add <Name> --project src/OneCup.Infrastructure --startup-project src/OneCup.Api`
- 权限复用现有 `numbering-view` / `numbering-manage`（不新增权限点）
- 前端 API 放 `frontend/src/api/`，页面放 `frontend/src/pages/system/numbering/dict/`，locale 放对应子目录
- 前端 i18n key 用 `numbering.dict.*` 前缀
- 业务类型/分类只启停，不物理删除（与 `NumberingRule` 一致）

---

## File Structure

### 后端新增
| 文件 | 职责 |
|------|------|
| `backend/src/OneCup.Domain/Entities/NumberingTargetType.cs` | 业务类型实体（code/nameZh/nameEn/sortOrder/isActive）|
| `backend/src/OneCup.Domain/Entities/NumberingCategory.cs` | 分类实体（targetTypeCode/code/nameZh/nameEn/sortOrder/isActive）|
| `backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingTargetTypeConfiguration.cs` | 业务类型 EF 配置 + code 唯一索引 |
| `backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingCategoryConfiguration.cs` | 分类 EF 配置 + (targetTypeCode,code) 组合唯一索引 |
| `backend/src/OneCup.Application/Dtos/System/NumberingDictionaryDtos.cs` | 两套字典的 Request/Response DTO |
| `backend/src/OneCup.Application/Interfaces/INumberingDictionaryService.cs` | 字典管理服务接口 |
| `backend/src/OneCup.Application/Services/NumberingDictionaryService.cs` | 字典管理服务实现 |
| `backend/src/OneCup.Application/Specifications/NumberingDictionarySpecs.cs` | 两套字典的查询规格 |
| `backend/src/OneCup.Api/Controllers/NumberingDictionaryController.cs` | 字典 API 控制器 |

### 后端修改
| 文件 | 改动 |
|------|------|
| `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs` | 新增 2 个 DbSet + 种子 HasData |
| `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs` | 新增 6 个业务类型种子 Guid 常量 |
| `backend/src/OneCup.Infrastructure/Services/NumberingService.cs` | Generate/Preview 加字典校验 |
| `backend/src/OneCup.Application/Common/NumberTargetTypes.cs` | 注释降级说明 |
| `backend/src/OneCup.Api/Program.cs` | 注册 INumberingDictionaryService DI |
| `backend/src/OneCup.Infrastructure/Migrations/<ts>_AddNumberingDictionary.cs` | 迁移（由 dotnet ef 生成）|

### 前端新增
| 文件 | 职责 |
|------|------|
| `frontend/src/api/numberingDictionary.ts` | 字典 API 客户端 |
| `frontend/src/pages/system/numbering/dict/index.tsx` | 字典管理页（主从表）|
| `frontend/src/pages/system/numbering/dict/locale/{zh-CN,en-US,index}.ts` | 字典页国际化 |

### 前端修改
| 文件 | 改动 |
|------|------|
| `frontend/src/pages/system/numbering/index.tsx` | 下拉动态拉取 + 预览占位符 + 日志显示中文名 |
| `frontend/src/pages/system/numbering/locale/{zh-CN,en-US}.ts` | 补 dict 相 key |
| `frontend/src/routes.ts` | 新增字典菜单项 |
| `frontend/src/router.tsx` | 新增字典路由 |

### 测试新增/修改
| 文件 | 职责 |
|------|------|
| `backend/tests/OneCup.UnitTests/NumberingDictionary/NumberingDictionaryServiceTests.cs` | 字典服务单测 |
| `backend/tests/OneCup.UnitTests/Numbering/NumberingServiceConcurrencyTests.cs` | 扩展：强校验路径集成测试 |

---

## Task 1: 领域实体

**Files:**
- Create: `backend/src/OneCup.Domain/Entities/NumberingTargetType.cs`
- Create: `backend/src/OneCup.Domain/Entities/NumberingCategory.cs`

**Interfaces:**
- Produces: `NumberingTargetType`（实体，含 Code/NameZh/NameEn/SortOrder/IsActive），`NumberingCategory`（实体，含 TargetTypeCode/Code/NameZh/NameEn/SortOrder/IsActive）

- [ ] **Step 1: 创建业务类型实体**

Create `backend/src/OneCup.Domain/Entities/NumberingTargetType.cs`:

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 编号业务类型字典。描述可被编号引擎消费的业务对象类型（如面料/原料）。
/// code 创建后不可改，作为编号规则的 target_type 标识。
/// </summary>
public class NumberingTargetType : BaseEntity
{
    /// <summary>英文标识符，如 fabric。创建后不可改</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>中文名，如"面料"</summary>
    public string NameZh { get; set; } = string.Empty;

    /// <summary>英文名，如"Fabric"</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>排序号（下拉显示顺序）</summary>
    public int SortOrder { get; set; }

    /// <summary>启停状态（停用后引擎校验拒绝、下拉不显示，不物理删除）</summary>
    public bool IsActive { get; set; } = true;
}
```

- [ ] **Step 2: 创建分类实体**

Create `backend/src/OneCup.Domain/Entities/NumberingCategory.cs`:

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 编号分类字典。挂在业务类型下，code 即编号拼码中的分类段。
/// 如面料下：棉(COT)、涤纶(POL)。唯一性：(targetTypeCode, code) 组合唯一。
/// </summary>
public class NumberingCategory : BaseEntity
{
    /// <summary>所属业务类型 code，如 fabric</summary>
    public string TargetTypeCode { get; set; } = string.Empty;

    /// <summary>分类码，如 COT。创建后不可改，即编号里的分类段</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>中文名，如"棉"</summary>
    public string NameZh { get; set; } = string.Empty;

    /// <summary>英文名，如"Cotton"</summary>
    public string NameEn { get; set; } = string.Empty;

    /// <summary>排序号</summary>
    public int SortOrder { get; set; }

    /// <summary>启停状态</summary>
    public bool IsActive { get; set; } = true;
}
```

- [ ] **Step 3: 编译验证**

Run: `cd backend && dotnet build src/OneCup.Domain/OneCup.Domain.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 4: Commit**

```bash
git add backend/src/OneCup.Domain/Entities/NumberingTargetType.cs backend/src/OneCup.Domain/Entities/NumberingCategory.cs
git commit -m "feat(domain): 编号字典实体 (NumberingTargetType + NumberingCategory)"
```

---

## Task 2: EF 配置 + DbContext + 种子数据

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingTargetTypeConfiguration.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingCategoryConfiguration.cs`
- Modify: `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs`
- Modify: `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`

**Interfaces:**
- Consumes: `NumberingTargetType`, `NumberingCategory` from Task 1
- Produces: `NumberingTargetTypeConfiguration`, `NumberingCategoryConfiguration`（被 `ApplyConfigurationsFromAssembly` 自动发现）

- [ ] **Step 1: 创建业务类型 EF 配置**

Create `backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingTargetTypeConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class NumberingTargetTypeConfiguration : IEntityTypeConfiguration<NumberingTargetType>
{
    public void Configure(EntityTypeBuilder<NumberingTargetType> builder)
    {
        builder.ToTable("numbering_target_types");
        builder.HasKey(t => t.Id);

        builder.Property(t => t.Id).HasColumnName("id");
        builder.Property(t => t.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(t => t.NameZh).HasColumnName("name_zh").HasMaxLength(64).IsRequired();
        builder.Property(t => t.NameEn).HasColumnName("name_en").HasMaxLength(64).IsRequired();
        builder.Property(t => t.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(t => t.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(t => t.CreatedAt).HasColumnName("created_at");
        builder.Property(t => t.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(t => t.Code)
            .HasDatabaseName("ux_numbering_target_types_code")
            .IsUnique();
    }
}
```

- [ ] **Step 2: 创建分类 EF 配置**

Create `backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingCategoryConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class NumberingCategoryConfiguration : IEntityTypeConfiguration<NumberingCategory>
{
    public void Configure(EntityTypeBuilder<NumberingCategory> builder)
    {
        builder.ToTable("numbering_categories");
        builder.HasKey(c => c.Id);

        builder.Property(c => c.Id).HasColumnName("id");
        builder.Property(c => c.TargetTypeCode).HasColumnName("target_type_code").HasMaxLength(32).IsRequired();
        builder.Property(c => c.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(c => c.NameZh).HasColumnName("name_zh").HasMaxLength(64).IsRequired();
        builder.Property(c => c.NameEn).HasColumnName("name_en").HasMaxLength(64).IsRequired();
        builder.Property(c => c.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(c => c.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(c => c.CreatedAt).HasColumnName("created_at");
        builder.Property(c => c.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(c => new { c.TargetTypeCode, c.Code })
            .HasDatabaseName("ux_numbering_categories_type_code")
            .IsUnique();

        builder.HasIndex(c => c.TargetTypeCode)
            .HasDatabaseName("ix_numbering_categories_target_type");
    }
}
```

- [ ] **Step 3: 在 SeedData.cs 新增 6 个业务类型种子 Guid**

Modify `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs`，在类末尾（`AdminPasswordHash` 字段之后）新增：

```csharp
    // 业务类型字典种子 Guid：第 4 段从 201 开始递增
    public static readonly Guid TargetTypeFabric = Guid.Parse("00000000-0000-0000-0000-000000000201");
    public static readonly Guid TargetTypeMaterial = Guid.Parse("00000000-0000-0000-0000-000000000202");
    public static readonly Guid TargetTypeEquipment = Guid.Parse("00000000-0000-0000-0000-000000000203");
    public static readonly Guid TargetTypeCustomer = Guid.Parse("00000000-0000-0000-0000-000000000204");
    public static readonly Guid TargetTypeColor = Guid.Parse("00000000-0000-0000-0000-000000000205");
    public static readonly Guid TargetTypeProduct = Guid.Parse("00000000-0000-0000-0000-000000000206");
```

- [ ] **Step 4: 在 DbContext 新增 DbSet + 种子 HasData**

Modify `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`:

在 `NumberingLogs` DbSet 之后（第 22 行后）新增两行：

```csharp
    public DbSet<NumberingTargetType> NumberingTargetTypes => Set<NumberingTargetType>();
    public DbSet<NumberingCategory> NumberingCategories => Set<NumberingCategory>();
```

在 `Seed` 方法末尾（`role_permissions` 的 HasData 之后，方法闭合 `}` 之前）新增：

```csharp
        // ── 编号业务类型字典（6 个默认类型，code 与 NumberTargetTypes 常量一致，保证存量数据无缝兼容）──
        modelBuilder.Entity<NumberingTargetType>().HasData(
            new NumberingTargetType { Id = SeedData.TargetTypeFabric, Code = "fabric", NameZh = "面料", NameEn = "Fabric", SortOrder = 1, IsActive = true, CreatedAt = SeedTimestamp },
            new NumberingTargetType { Id = SeedData.TargetTypeMaterial, Code = "material", NameZh = "原料", NameEn = "Material", SortOrder = 2, IsActive = true, CreatedAt = SeedTimestamp },
            new NumberingTargetType { Id = SeedData.TargetTypeEquipment, Code = "equipment", NameZh = "设备", NameEn = "Equipment", SortOrder = 3, IsActive = true, CreatedAt = SeedTimestamp },
            new NumberingTargetType { Id = SeedData.TargetTypeCustomer, Code = "customer", NameZh = "客户", NameEn = "Customer", SortOrder = 4, IsActive = true, CreatedAt = SeedTimestamp },
            new NumberingTargetType { Id = SeedData.TargetTypeColor, Code = "color", NameZh = "颜色", NameEn = "Color", SortOrder = 5, IsActive = true, CreatedAt = SeedTimestamp },
            new NumberingTargetType { Id = SeedData.TargetTypeProduct, Code = "product", NameZh = "产品", NameEn = "Product", SortOrder = 6, IsActive = true, CreatedAt = SeedTimestamp }
        );
```

- [ ] **Step 5: 编译验证**

Run: `cd backend && dotnet build src/OneCup.Infrastructure/OneCup.Infrastructure.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 6: 生成迁移**

Run:
```bash
cd backend
dotnet ef migrations add AddNumberingDictionary --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```
Expected: 迁移文件生成在 `Migrations/` 下，包含建 `numbering_target_types` / `numbering_categories` 两表 + 唯一索引 + 6 条 `numbering_target_types` 种子 InsertData。

- [ ] **Step 7: 校验迁移内容**

打开生成的迁移文件，确认：
- `CreateTable("numbering_target_types")` 含 code 唯一索引 `ux_numbering_target_types_code`
- `CreateTable("numbering_categories")` 含 `(target_type_code, code)` 组合唯一索引 `ux_numbering_categories_type_code`
- `InsertData("numbering_target_types", ...)` 6 条记录
- `Down` 方法含 DropTable 两表

- [ ] **Step 8: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingTargetTypeConfiguration.cs \
        backend/src/OneCup.Infrastructure/Persistence/Configurations/NumberingCategoryConfiguration.cs \
        backend/src/OneCup.Infrastructure/Persistence/SeedData.cs \
        backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs \
        backend/src/OneCup.Infrastructure/Migrations/
git commit -m "feat(infra): 字典 EF 配置 + DbContext + 种子迁移 (AddNumberingDictionary)"
```

---

## Task 3: DTO + Specification

**Files:**
- Create: `backend/src/OneCup.Application/Dtos/System/NumberingDictionaryDtos.cs`
- Create: `backend/src/OneCup.Application/Specifications/NumberingDictionarySpecs.cs`

**Interfaces:**
- Consumes: `NumberingTargetType`, `NumberingCategory` from Task 1
- Produces: 业务类型/分类的 Request/Response DTO + 查询规格（后续 Service 依赖这些类型名）

- [ ] **Step 1: 创建字典 DTO**

Create `backend/src/OneCup.Application/Dtos/System/NumberingDictionaryDtos.cs`:

```csharp
namespace OneCup.Application.Dtos.System;

// ── 业务类型 ──

public record CreateTargetTypeRequest
{
    public string Code { get; init; } = string.Empty;
    public string NameZh { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

public record UpdateTargetTypeRequest
{
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public int? SortOrder { get; init; }
}

public record UpdateDictStatusRequest
{
    public bool IsActive { get; init; }
}

public class TargetTypeDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameZh { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// ── 分类 ──

public record CreateCategoryRequest
{
    public string TargetTypeCode { get; init; } = string.Empty;
    public string Code { get; init; } = string.Empty;
    public string NameZh { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public int SortOrder { get; init; }
}

public record UpdateCategoryRequest
{
    public string? NameZh { get; init; }
    public string? NameEn { get; init; }
    public int? SortOrder { get; init; }
}

public class CategoryDto
{
    public Guid Id { get; set; }
    public string TargetTypeCode { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string NameZh { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

- [ ] **Step 2: 创建字典查询规格**

Create `backend/src/OneCup.Application/Specifications/NumberingDictionarySpecs.cs`:

```csharp
using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

// ── 业务类型规格 ──

/// <summary>业务类型过滤规格（仅 keyword/isActive，不含分页）。用于 CountAsync 统计总数。</summary>
/// <remarks>多条件必须组合为单一 predicate 调一次 ApplyCriteria（基类覆盖语义，见 NumberingRuleFilterSpec 说明）。</remarks>
public class TargetTypeFilterSpec : Specification<NumberingTargetType>
{
    public TargetTypeFilterSpec(string? keyword, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(t =>
            (kw == null || t.Code.Contains(kw) || t.NameZh.Contains(kw) || t.NameEn.Contains(kw)) &&
            (isActive == null || t.IsActive == isActive.Value));
    }
}

/// <summary>业务类型分页查询（含 keyword/isActive 过滤，按 SortOrder 升序）。</summary>
public class TargetTypePagedSpec : Specification<NumberingTargetType>
{
    public TargetTypePagedSpec(string? keyword, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(t =>
            (kw == null || t.Code.Contains(kw) || t.NameZh.Contains(kw) || t.NameEn.Contains(kw)) &&
            (isActive == null || t.IsActive == isActive.Value));
        ApplyOrderBy(t => t.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>业务类型全部启用项（前端下拉用，按 SortOrder 升序）。</summary>
public class TargetTypeActiveSpec : Specification<NumberingTargetType>
{
    public TargetTypeActiveSpec()
    {
        ApplyCriteria(t => t.IsActive);
        ApplyOrderBy(t => t.SortOrder);
    }
}

public class TargetTypeByIdSpec : Specification<NumberingTargetType>
{
    public TargetTypeByIdSpec(Guid id) => ApplyCriteria(t => t.Id == id);
}

/// <summary>按 code 查找业务类型（可选排除自身 Id）。不含 IsActive 过滤——
/// 用于 code 唯一性校验时正确（停用也占用了 code）。</summary>
public class TargetTypeByCodeSpec : Specification<NumberingTargetType>
{
    public TargetTypeByCodeSpec(string code, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(t => t.Code == code && (exclude == null || t.Id != exclude.Value));
    }
}

/// <summary>按 code 查找存在且启用的业务类型。
/// 用于"校验业务类型存在且启用"场景（引擎校验、分类新增前的合法性校验）。</summary>
public class TargetTypeActiveByCodeSpec : Specification<NumberingTargetType>
{
    public TargetTypeActiveByCodeSpec(string code)
    {
        ApplyCriteria(t => t.Code == code && t.IsActive);
    }
}

// ── 分类规格 ──

/// <summary>分类过滤规格（仅 targetTypeCode/keyword/isActive，不含分页）。用于 CountAsync。</summary>
public class CategoryFilterSpec : Specification<NumberingCategory>
{
    public CategoryFilterSpec(string? targetTypeCode, string? keyword, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(c =>
            (string.IsNullOrEmpty(targetTypeCode) || c.TargetTypeCode == targetTypeCode) &&
            (kw == null || c.Code.Contains(kw) || c.NameZh.Contains(kw) || c.NameEn.Contains(kw)) &&
            (isActive == null || c.IsActive == isActive.Value));
    }
}

/// <summary>分类分页查询（含 targetTypeCode/keyword/isActive，按 SortOrder 升序）。</summary>
public class CategoryPagedSpec : Specification<NumberingCategory>
{
    public CategoryPagedSpec(string? targetTypeCode, string? keyword, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(c =>
            (string.IsNullOrEmpty(targetTypeCode) || c.TargetTypeCode == targetTypeCode) &&
            (kw == null || c.Code.Contains(kw) || c.NameZh.Contains(kw) || c.NameEn.Contains(kw)) &&
            (isActive == null || c.IsActive == isActive.Value));
        ApplyOrderBy(c => c.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>某业务类型下全部启用分类（前端联动下拉用，按 SortOrder 升序）。</summary>
public class CategoryActiveByTypeSpec : Specification<NumberingCategory>
{
    public CategoryActiveByTypeSpec(string targetTypeCode)
    {
        ApplyCriteria(c => c.TargetTypeCode == targetTypeCode && c.IsActive);
        ApplyOrderBy(c => c.SortOrder);
    }
}

public class CategoryByIdSpec : Specification<NumberingCategory>
{
    public CategoryByIdSpec(Guid id) => ApplyCriteria(c => c.Id == id);
}

/// <summary>分类唯一性校验：(targetTypeCode, code) 组合唯一，可选排除自身 Id。</summary>
public class CategoryByTypeAndCodeSpec : Specification<NumberingCategory>
{
    public CategoryByTypeAndCodeSpec(string targetTypeCode, string code, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(c => c.TargetTypeCode == targetTypeCode && c.Code == code
                        && (exclude == null || c.Id != exclude.Value));
    }
}
```

- [ ] **Step 3: 编译验证**

Run: `cd backend && dotnet build src/OneCup.Application/OneCup.Application.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 4: Commit**

```bash
git add backend/src/OneCup.Application/Dtos/System/NumberingDictionaryDtos.cs \
        backend/src/OneCup.Application/Specifications/NumberingDictionarySpecs.cs
git commit -m "feat(app): 字典 DTO + Specification"
```

---

## Task 4: 字典服务接口 + 实现

**Files:**
- Create: `backend/src/OneCup.Application/Interfaces/INumberingDictionaryService.cs`
- Create: `backend/src/OneCup.Application/Services/NumberingDictionaryService.cs`

**Interfaces:**
- Consumes: DTOs + Specs from Task 3, `IRepository<NumberingTargetType>` / `IRepository<NumberingCategory>` / `IUnitOfWork`（已存在的泛型仓储，无需新建）
- Produces: `INumberingDictionaryService`（控制器和引擎校验都依赖此接口的方法名）

- [ ] **Step 1: 创建服务接口**

Create `backend/src/OneCup.Application/Interfaces/INumberingDictionaryService.cs`:

```csharp
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 编号字典管理服务（业务类型 + 分类的 CRUD）。
/// </summary>
public interface INumberingDictionaryService
{
    // ── 业务类型 ──
    Task<PagedResult<TargetTypeDto>> GetTargetTypesAsync(
        int page, int pageSize, string? keyword, bool? isActive, CancellationToken ct = default);

    Task<List<TargetTypeDto>> GetAllActiveTargetTypesAsync(CancellationToken ct = default);

    Task<TargetTypeDto?> GetTargetTypeAsync(Guid id, CancellationToken ct = default);

    Task<TargetTypeDto> CreateTargetTypeAsync(CreateTargetTypeRequest request, CancellationToken ct = default);

    Task UpdateTargetTypeAsync(Guid id, UpdateTargetTypeRequest request, CancellationToken ct = default);

    Task UpdateTargetTypeStatusAsync(Guid id, bool isActive, CancellationToken ct = default);

    // ── 分类 ──
    Task<PagedResult<CategoryDto>> GetCategoriesAsync(
        int page, int pageSize, string? targetTypeCode, string? keyword, bool? isActive, CancellationToken ct = default);

    Task<List<CategoryDto>> GetActiveCategoriesAsync(string targetTypeCode, CancellationToken ct = default);

    Task<CategoryDto?> GetCategoryAsync(Guid id, CancellationToken ct = default);

    Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct = default);

    Task UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default);

    Task UpdateCategoryStatusAsync(Guid id, bool isActive, CancellationToken ct = default);
}
```

- [ ] **Step 2: 写业务类型 code 重复校验失败测试（先写测试）**

Create `backend/tests/OneCup.UnitTests/NumberingDictionary/NumberingDictionaryServiceTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Application.Services;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.NumberingDictionary;

public class NumberingDictionaryServiceTests
{
    private static (OneCupDbContext db, NumberingDictionaryService svc) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"numbering-dict-{Guid.NewGuid()}")
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);
        var svc = new NumberingDictionaryService(
            new Repository<NumberingTargetType>(db),
            new Repository<NumberingCategory>(db),
            new UnitOfWork(db));
        return (db, svc);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    // ── 业务类型 ──

    [Fact]
    public async Task CreateTargetTypeAsync_CreatesType()
    {
        var (db, svc) = Setup();
        var dto = await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "order", NameZh = "订单", NameEn = "Order", SortOrder = 10,
        });
        Assert.Equal("order", dto.Code);
        Assert.True(dto.IsActive);
    }

    [Fact]
    public async Task CreateTargetTypeAsync_DuplicateCode_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "order", NameZh = "订单", NameEn = "Order",
        });
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
            {
                Code = "order", NameZh = "订单2", NameEn = "Order2",
            }));
    }

    [Fact]
    public async Task UpdateTargetTypeAsync_CodeIgnored()
    {
        // code 不可改：更新时即使传了新 code 也应忽略
        var (db, svc) = Setup();
        var dto = await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "order", NameZh = "订单", NameEn = "Order",
        });
        await svc.UpdateTargetTypeAsync(dto.Id, new UpdateTargetTypeRequest
        {
            NameZh = "订单改", NameEn = "Order改",
        });
        var updated = await svc.GetTargetTypeAsync(dto.Id);
        Assert.Equal("order", updated!.Code);        // code 不变
        Assert.Equal("订单改", updated.NameZh);
    }

    [Fact]
    public async Task UpdateTargetTypeStatusAsync_Toggles()
    {
        var (db, svc) = Setup();
        var dto = await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "order", NameZh = "订单", NameEn = "Order",
        });
        await svc.UpdateTargetTypeStatusAsync(dto.Id, false);
        var updated = await svc.GetTargetTypeAsync(dto.Id);
        Assert.False(updated!.IsActive);
    }

    // ── 分类 ──

    [Fact]
    public async Task CreateCategoryAsync_RequiresExistingTargetType()
    {
        var (db, svc) = Setup();
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateCategoryAsync(new CreateCategoryRequest
            {
                TargetTypeCode = "nonexistent", Code = "COT", NameZh = "棉", NameEn = "Cotton",
            }));
    }

    [Fact]
    public async Task CreateCategoryAsync_CreatesCategory()
    {
        var (db, svc) = Setup();
        await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "fabric", NameZh = "面料", NameEn = "Fabric",
        });
        var dto = await svc.CreateCategoryAsync(new CreateCategoryRequest
        {
            TargetTypeCode = "fabric", Code = "COT", NameZh = "棉", NameEn = "Cotton",
        });
        Assert.Equal("COT", dto.Code);
        Assert.Equal("fabric", dto.TargetTypeCode);
    }

    [Fact]
    public async Task CreateCategoryAsync_DuplicateInSameType_Throws()
    {
        var (db, svc) = Setup();
        await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "fabric", NameZh = "面料", NameEn = "Fabric",
        });
        await svc.CreateCategoryAsync(new CreateCategoryRequest
        {
            TargetTypeCode = "fabric", Code = "COT", NameZh = "棉", NameEn = "Cotton",
        });
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.CreateCategoryAsync(new CreateCategoryRequest
            {
                TargetTypeCode = "fabric", Code = "COT", NameZh = "棉2", NameEn = "Cotton2",
            }));
    }

    [Fact]
    public async Task CreateCategoryAsync_SameCodeDifferentType_Allowed()
    {
        // COT 在 fabric 下有了，material 下也可有 COT
        var (db, svc) = Setup();
        await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "fabric", NameZh = "面料", NameEn = "Fabric",
        });
        await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "material", NameZh = "原料", NameEn = "Material",
        });
        await svc.CreateCategoryAsync(new CreateCategoryRequest
        {
            TargetTypeCode = "fabric", Code = "COT", NameZh = "棉", NameEn = "Cotton",
        });
        // 不同 targetTypeCode 下同 code 不冲突
        await svc.CreateCategoryAsync(new CreateCategoryRequest
        {
            TargetTypeCode = "material", Code = "COT", NameZh = "棉纱", NameEn = "Cotton Yarn",
        });
    }

    [Fact]
    public async Task GetActiveCategoriesAsync_ReturnsOnlyActive()
    {
        var (db, svc) = Setup();
        await svc.CreateTargetTypeAsync(new CreateTargetTypeRequest
        {
            Code = "fabric", NameZh = "面料", NameEn = "Fabric",
        });
        var c1 = await svc.CreateCategoryAsync(new CreateCategoryRequest
        {
            TargetTypeCode = "fabric", Code = "COT", NameZh = "棉", NameEn = "Cotton", SortOrder = 1,
        });
        await svc.CreateCategoryAsync(new CreateCategoryRequest
        {
            TargetTypeCode = "fabric", Code = "POL", NameZh = "涤纶", NameEn = "Polyester", SortOrder = 2,
        });
        await svc.UpdateCategoryStatusAsync(c1.Id, false);

        var list = await svc.GetActiveCategoriesAsync("fabric");
        Assert.Single(list);
        Assert.Equal("POL", list[0].Code);
    }
}
```

- [ ] **Step 3: 运行测试确认失败（服务未实现）**

Run: `cd backend && dotnet test tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~NumberingDictionaryServiceTests"`
Expected: FAIL（编译错误，`NumberingDictionaryService` 不存在）

- [ ] **Step 4: 创建服务实现**

Create `backend/src/OneCup.Application/Services/NumberingDictionaryService.cs`:

```csharp
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 编号字典管理服务实现。通过 IRepository + Specification 访问数据，IUnitOfWork 提交。
/// 业务类型 code 不可改；分类 code/targetTypeCode 不可改。
/// 新增分类时校验 targetTypeCode 存在且启用，防孤儿分类。
/// </summary>
public class NumberingDictionaryService : INumberingDictionaryService
{
    private readonly IRepository<NumberingTargetType> _types;
    private readonly IRepository<NumberingCategory> _categories;
    private readonly IUnitOfWork _uow;

    public NumberingDictionaryService(
        IRepository<NumberingTargetType> types,
        IRepository<NumberingCategory> categories,
        IUnitOfWork uow)
    {
        _types = types;
        _categories = categories;
        _uow = uow;
    }

    // ── 业务类型 ──

    public async Task<PagedResult<TargetTypeDto>> GetTargetTypesAsync(
        int page, int pageSize, string? keyword, bool? isActive, CancellationToken ct = default)
    {
        var total = await _types.CountAsync(new TargetTypeFilterSpec(keyword, isActive), ct);
        var types = await _types.ListAsync(new TargetTypePagedSpec(keyword, isActive, page, pageSize), ct);

        return new PagedResult<TargetTypeDto>
        {
            Items = types.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<List<TargetTypeDto>> GetAllActiveTargetTypesAsync(CancellationToken ct = default)
    {
        var types = await _types.ListAsync(new TargetTypeActiveSpec(), ct);
        return types.Select(ToDto).ToList();
    }

    public async Task<TargetTypeDto?> GetTargetTypeAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _types.FirstOrDefaultAsync(new TargetTypeByIdSpec(id), ct);
        return t is null ? null : ToDto(t);
    }

    public async Task<TargetTypeDto> CreateTargetTypeAsync(CreateTargetTypeRequest request, CancellationToken ct = default)
    {
        if (await _types.AnyAsync(new TargetTypeByCodeSpec(request.Code), ct))
            throw new DomainException($"业务类型 code '{request.Code}' 已存在");

        var entity = new NumberingTargetType
        {
            Code = request.Code,
            NameZh = request.NameZh,
            NameEn = request.NameEn,
            SortOrder = request.SortOrder,
            IsActive = true,
        };
        await _types.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task UpdateTargetTypeAsync(Guid id, UpdateTargetTypeRequest request, CancellationToken ct = default)
    {
        // code 不可改：更新接口不暴露 Code 字段，无需特殊处理
        var entity = await _types.FirstOrDefaultAsync(new TargetTypeByIdSpec(id), ct)
            ?? throw new DomainException("业务类型不存在");

        if (request.NameZh is not null) entity.NameZh = request.NameZh;
        if (request.NameEn is not null) entity.NameEn = request.NameEn;
        if (request.SortOrder is not null) entity.SortOrder = request.SortOrder.Value;

        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdateTargetTypeStatusAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var entity = await _types.FirstOrDefaultAsync(new TargetTypeByIdSpec(id), ct)
            ?? throw new DomainException("业务类型不存在");
        entity.IsActive = isActive;
        await _uow.SaveChangesAsync(ct);
    }

    // ── 分类 ──

    public async Task<PagedResult<CategoryDto>> GetCategoriesAsync(
        int page, int pageSize, string? targetTypeCode, string? keyword, bool? isActive, CancellationToken ct = default)
    {
        var total = await _categories.CountAsync(new CategoryFilterSpec(targetTypeCode, keyword, isActive), ct);
        var cats = await _categories.ListAsync(new CategoryPagedSpec(targetTypeCode, keyword, isActive, page, pageSize), ct);

        return new PagedResult<CategoryDto>
        {
            Items = cats.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<List<CategoryDto>> GetActiveCategoriesAsync(string targetTypeCode, CancellationToken ct = default)
    {
        var cats = await _categories.ListAsync(new CategoryActiveByTypeSpec(targetTypeCode), ct);
        return cats.Select(ToDto).ToList();
    }

    public async Task<CategoryDto?> GetCategoryAsync(Guid id, CancellationToken ct = default)
    {
        var c = await _categories.FirstOrDefaultAsync(new CategoryByIdSpec(id), ct);
        return c is null ? null : ToDto(c);
    }

    public async Task<CategoryDto> CreateCategoryAsync(CreateCategoryRequest request, CancellationToken ct = default)
    {
        // 校验 targetTypeCode 存在且启用
        var typeExists = await _types.AnyAsync(
            new TargetTypeActiveByCodeSpec(request.TargetTypeCode), ct);
        if (!typeExists)
            throw new DomainException($"业务类型 '{request.TargetTypeCode}' 不存在或已停用，无法在其下创建分类");

        // 校验 (targetTypeCode, code) 组合唯一
        if (await _categories.AnyAsync(
            new CategoryByTypeAndCodeSpec(request.TargetTypeCode, request.Code), ct))
            throw new DomainException($"分类 code '{request.Code}' 在业务类型 '{request.TargetTypeCode}' 下已存在");

        var entity = new NumberingCategory
        {
            TargetTypeCode = request.TargetTypeCode,
            Code = request.Code,
            NameZh = request.NameZh,
            NameEn = request.NameEn,
            SortOrder = request.SortOrder,
            IsActive = true,
        };
        await _categories.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        return ToDto(entity);
    }

    public async Task UpdateCategoryAsync(Guid id, UpdateCategoryRequest request, CancellationToken ct = default)
    {
        // code/targetTypeCode 不可改：更新接口不暴露这两个字段
        var entity = await _categories.FirstOrDefaultAsync(new CategoryByIdSpec(id), ct)
            ?? throw new DomainException("分类不存在");

        if (request.NameZh is not null) entity.NameZh = request.NameZh;
        if (request.NameEn is not null) entity.NameEn = request.NameEn;
        if (request.SortOrder is not null) entity.SortOrder = request.SortOrder.Value;

        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdateCategoryStatusAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var entity = await _categories.FirstOrDefaultAsync(new CategoryByIdSpec(id), ct)
            ?? throw new DomainException("分类不存在");
        entity.IsActive = isActive;
        await _uow.SaveChangesAsync(ct);
    }

    // ── DTO 映射 ──

    private static TargetTypeDto ToDto(NumberingTargetType t) => new()
    {
        Id = t.Id,
        Code = t.Code,
        NameZh = t.NameZh,
        NameEn = t.NameEn,
        SortOrder = t.SortOrder,
        IsActive = t.IsActive,
        CreatedAt = t.CreatedAt,
        UpdatedAt = t.UpdatedAt,
    };

    private static CategoryDto ToDto(NumberingCategory c) => new()
    {
        Id = c.Id,
        TargetTypeCode = c.TargetTypeCode,
        Code = c.Code,
        NameZh = c.NameZh,
        NameEn = c.NameEn,
        SortOrder = c.SortOrder,
        IsActive = c.IsActive,
        CreatedAt = c.CreatedAt,
        UpdatedAt = c.UpdatedAt,
    };
}
```

- [ ] **Step 5: 运行测试确认通过**

Run: `cd backend && dotnet test tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~NumberingDictionaryServiceTests"`
Expected: 8 个测试全部 PASS

- [ ] **Step 6: Commit**

```bash
git add backend/src/OneCup.Application/Interfaces/INumberingDictionaryService.cs \
        backend/src/OneCup.Application/Services/NumberingDictionaryService.cs \
        backend/tests/OneCup.UnitTests/NumberingDictionary/NumberingDictionaryServiceTests.cs
git commit -m "feat(app): 字典管理服务接口+实现+单测"
```

---

## Task 5: API 控制器 + DI 注册

**Files:**
- Create: `backend/src/OneCup.Api/Controllers/NumberingDictionaryController.cs`
- Modify: `backend/src/OneCup.Api/Program.cs`

**Interfaces:**
- Consumes: `INumberingDictionaryService` from Task 4
- Produces: HTTP 端点 `api/numbering/dict/target-types` 和 `api/numbering/dict/categories`

- [ ] **Step 1: 创建字典控制器**

Create `backend/src/OneCup.Api/Controllers/NumberingDictionaryController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 编号字典管理端点（业务类型 + 分类）。
/// 复用 numbering-view / numbering-manage 权限。
/// </summary>
[ApiController]
[Route("api/numbering/dict")]
public class NumberingDictionaryController : ControllerBase
{
    private readonly INumberingDictionaryService _svc;

    public NumberingDictionaryController(INumberingDictionaryService svc)
    {
        _svc = svc;
    }

    // ── 业务类型 ──

    [HttpGet("target-types")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetTargetTypes(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null, [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _svc.GetTargetTypesAsync(page, pageSize, keyword, isActive, ct);
        return Ok(result);
    }

    [HttpGet("target-types/all")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetAllActiveTargetTypes(CancellationToken ct)
    {
        var list = await _svc.GetAllActiveTargetTypesAsync(ct);
        return Ok(list);
    }

    [HttpGet("target-types/{id:guid}")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetTargetType(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetTargetTypeAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("target-types")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> CreateTargetType([FromBody] CreateTargetTypeRequest request, CancellationToken ct)
    {
        var dto = await _svc.CreateTargetTypeAsync(request, ct);
        return CreatedAtAction(nameof(GetTargetType), new { id = dto.Id }, dto);
    }

    [HttpPut("target-types/{id:guid}")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> UpdateTargetType(Guid id, [FromBody] UpdateTargetTypeRequest request, CancellationToken ct)
    {
        await _svc.UpdateTargetTypeAsync(id, request, ct);
        return NoContent();
    }

    [HttpPut("target-types/{id:guid}/status")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> UpdateTargetTypeStatus(Guid id, [FromBody] UpdateDictStatusRequest request, CancellationToken ct)
    {
        await _svc.UpdateTargetTypeStatusAsync(id, request.IsActive, ct);
        return NoContent();
    }

    // ── 分类 ──

    [HttpGet("categories")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetCategories(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? targetTypeCode = null, [FromQuery] string? keyword = null,
        [FromQuery] bool? isActive = null, CancellationToken ct = default)
    {
        var result = await _svc.GetCategoriesAsync(page, pageSize, targetTypeCode, keyword, isActive, ct);
        return Ok(result);
    }

    [HttpGet("categories/all")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetActiveCategories([FromQuery] string targetTypeCode, CancellationToken ct)
    {
        var list = await _svc.GetActiveCategoriesAsync(targetTypeCode, ct);
        return Ok(list);
    }

    [HttpGet("categories/{id:guid}")]
    [Authorize(Policy = "numbering-view")]
    public async Task<IActionResult> GetCategory(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetCategoryAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost("categories")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> CreateCategory([FromBody] CreateCategoryRequest request, CancellationToken ct)
    {
        var dto = await _svc.CreateCategoryAsync(request, ct);
        return CreatedAtAction(nameof(GetCategory), new { id = dto.Id }, dto);
    }

    [HttpPut("categories/{id:guid}")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> UpdateCategory(Guid id, [FromBody] UpdateCategoryRequest request, CancellationToken ct)
    {
        await _svc.UpdateCategoryAsync(id, request, ct);
        return NoContent();
    }

    [HttpPut("categories/{id:guid}/status")]
    [Authorize(Policy = "numbering-manage")]
    public async Task<IActionResult> UpdateCategoryStatus(Guid id, [FromBody] UpdateDictStatusRequest request, CancellationToken ct)
    {
        await _svc.UpdateCategoryStatusAsync(id, request.IsActive, ct);
        return NoContent();
    }
}
```

- [ ] **Step 2: 在 Program.cs 注册 DI**

Modify `backend/src/OneCup.Api/Program.cs`，在 `INumberingRuleService` 注册行（约第 89 行）之后新增一行：

```csharp
builder.Services.AddScoped<INumberingDictionaryService, NumberingDictionaryService>();
```

- [ ] **Step 3: 编译验证**

Run: `cd backend && dotnet build src/OneCup.Api/OneCup.Api.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 4: Commit**

```bash
git add backend/src/OneCup.Api/Controllers/NumberingDictionaryController.cs backend/src/OneCup.Api/Program.cs
git commit -m "feat(api): 字典管理控制器 + DI 注册"
```

---

## Task 6: 引擎强校验改造

**Files:**
- Modify: `backend/src/OneCup.Infrastructure/Services/NumberingService.cs`
- Modify: `backend/src/OneCup.Application/Common/NumberTargetTypes.cs`

**Interfaces:**
- Consumes: `NumberingTargetTypes` / `NumberingCategories` DbSet（已在 Task 2 注册到 DbContext）
- Produces: `GenerateAsync` / `PreviewAsync` 加了字典强校验（行为变更）

- [ ] **Step 1: 扩展并发集成测试（先写失败测试）**

打开 `backend/tests/OneCup.UnitTests/Numbering/NumberingServiceConcurrencyTests.cs`。在文件末尾（类闭合 `}` 之前）新增以下测试方法。

**注意：** 这些集成测试需要真实 PostgreSQL（Testcontainers）。如果当前环境无法连接 PG，先确认该文件顶部的 PG 连接方式（`NUMBERING_TEST_PG` 环境变量或 Testcontainers）。测试方法的 Setup 需要在数据库里先种入字典数据。参照该文件现有测试如何创建 `NumberingRule` 来创建 `NumberingTargetType` / `NumberingCategory`。

新增测试方法：

```csharp
    // ──────────────────────────────────────────────────────────────
    // 字典强校验（Task 6 新增）
    // ──────────────────────────────────────────────────────────────

    [Fact]
    public async Task GenerateAsync_InvalidTargetType_Throws()
    {
        await using var fixture = await CreateFixtureAsync();
        // 不种入 "ghost" 业务类型
        await using var tx = await fixture.Db.Database.BeginTransactionAsync();
        var ex = await Assert.ThrowsAsync<DomainException>(() =>
            fixture.Svc.GenerateAsync("ghost", null));
        Assert.Contains("ghost", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_DisabledTargetType_Throws()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.Db.NumberingTargetTypes.Add(new NumberingTargetType
        {
            Code = "disabled", NameZh = "停用类型", NameEn = "Disabled", IsActive = false,
        });
        await fixture.Db.SaveChangesAsync();

        await using var tx = await fixture.Db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Svc.GenerateAsync("disabled", null));
    }

    [Fact]
    public async Task GenerateAsync_ValidTypeNoCategory_WithCategoryRule_Throws()
    {
        await using var fixture = await CreateFixtureAsync();
        // 种入 fabric 类型（无分类）
        fixture.Db.NumberingTargetTypes.Add(new NumberingTargetType
        {
            Code = "fabric", NameZh = "面料", NameEn = "Fabric", IsActive = true,
        });
        // 规则要求分类码
        fixture.Db.NumberingRules.Add(new NumberingRule
        {
            TargetType = "fabric", Name = "面料规则", Prefix = "FAB",
            IncludeCategory = true, DateSegment = DateSegment.None,
            SeqLength = 4, Separator = "-", ResetPeriod = ResetPeriod.None, IsActive = true,
        });
        await fixture.Db.SaveChangesAsync();

        await using var tx = await fixture.Db.Database.BeginTransactionAsync();
        // 传了字典里不存在的分类码
        await Assert.ThrowsAsync<DomainException>(() => fixture.Svc.GenerateAsync("fabric", "GHOST"));
    }

    [Fact]
    public async Task GenerateAsync_ValidTypeValidCategory_Succeeds()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.Db.NumberingTargetTypes.Add(new NumberingTargetType
        {
            Code = "fabric", NameZh = "面料", NameEn = "Fabric", IsActive = true,
        });
        fixture.Db.NumberingCategories.Add(new NumberingCategory
        {
            TargetTypeCode = "fabric", Code = "COT", NameZh = "棉", NameEn = "Cotton", IsActive = true,
        });
        fixture.Db.NumberingRules.Add(new NumberingRule
        {
            TargetType = "fabric", Name = "面料规则", Prefix = "FAB",
            IncludeCategory = true, DateSegment = DateSegment.None,
            SeqLength = 4, Separator = "-", ResetPeriod = ResetPeriod.None, IsActive = true,
        });
        await fixture.Db.SaveChangesAsync();

        await using var tx = await fixture.Db.Database.BeginTransactionAsync();
        var code = await fixture.Svc.GenerateAsync("fabric", "COT");
        Assert.Contains("COT", code);
    }

    [Fact]
    public async Task GenerateAsync_DisabledCategory_Throws()
    {
        await using var fixture = await CreateFixtureAsync();
        fixture.Db.NumberingTargetTypes.Add(new NumberingTargetType
        {
            Code = "fabric", NameZh = "面料", NameEn = "Fabric", IsActive = true,
        });
        fixture.Db.NumberingCategories.Add(new NumberingCategory
        {
            TargetTypeCode = "fabric", Code = "COT", NameZh = "棉", NameEn = "Cotton", IsActive = false,
        });
        fixture.Db.NumberingRules.Add(new NumberingRule
        {
            TargetType = "fabric", Name = "面料规则", Prefix = "FAB",
            IncludeCategory = true, DateSegment = DateSegment.None,
            SeqLength = 4, Separator = "-", ResetPeriod = ResetPeriod.None, IsActive = true,
        });
        await fixture.Db.SaveChangesAsync();

        await using var tx = await fixture.Db.Database.BeginTransactionAsync();
        await Assert.ThrowsAsync<DomainException>(() => fixture.Svc.GenerateAsync("fabric", "COT"));
    }

    [Fact]
    public async Task GenerateAsync_NoCategoryRule_IgnoresPassedCategory()
    {
        // 规则不要分类码时，即使传了 categoryCode 也不校验（宽容忽略保持不变）
        await using var fixture = await CreateFixtureAsync();
        fixture.Db.NumberingTargetTypes.Add(new NumberingTargetType
        {
            Code = "fabric", NameZh = "面料", NameEn = "Fabric", IsActive = true,
        });
        // 注意：不种入任何分类，规则 IncludeCategory=false
        fixture.Db.NumberingRules.Add(new NumberingRule
        {
            TargetType = "fabric", Name = "面料规则", Prefix = "FAB",
            IncludeCategory = false, DateSegment = DateSegment.None,
            SeqLength = 4, Separator = "-", ResetPeriod = ResetPeriod.None, IsActive = true,
        });
        await fixture.Db.SaveChangesAsync();

        await using var tx = await fixture.Db.Database.BeginTransactionAsync();
        // 传了字典里没有的分类码，但规则不要分类码 → 应成功
        var code = await fixture.Svc.GenerateAsync("fabric", "ANYTHING");
        Assert.StartsWith("FAB", code);
        Assert.DoesNotContain("ANYTHING", code);
    }
```

**注意：** 上面的 `CreateFixtureAsync()` 和 `fixture.Db` / `fixture.Svc` 是该文件现有的测试基础设施。实现者需参照该文件现有的 fixture 创建模式（可能是 `NumberingTestFixture` 类或类似 helper）。如果现有 fixture helper 不返回 DbContext，需要调整使其暴露 `Db` 属性供测试种入字典数据。

- [ ] **Step 2: 运行测试确认失败**

Run: `cd backend && dotnet test tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~NumberingServiceConcurrencyTests"`
Expected: 新增的 5 个测试 FAIL（引擎尚未校验字典）

- [ ] **Step 3: 改造 NumberingService 加字典校验**

Modify `backend/src/OneCup.Infrastructure/Services/NumberingService.cs`。

在 `GenerateAsync` 方法中，找到"找规则"的代码块之后（`var rule = await _db.NumberingRules...` 之后、`if (rule.IncludeCategory && string.IsNullOrEmpty(categoryCode))` 之前），插入字典校验。

**3a. 在 using 区块顶部新增（如果没有的话）：**
确认文件顶部已 `using OneCup.Domain.Entities;`（NumberingTargetType/NumberingCategory 需要）。

**3b. 在 GenerateAsync 的找规则之后、取号之前，插入：**

```csharp
                // ── 字典强校验（Task 6）──
                var typeExists = await _db.NumberingTargetTypes
                    .AnyAsync(t => t.Code == targetType && t.IsActive, ct);
                if (!typeExists)
                    throw new DomainException($"业务类型 {targetType} 不存在或已停用");
```

在现有的 `if (rule.IncludeCategory && string.IsNullOrEmpty(categoryCode))` 校验**之后**，`var effectiveCategory = ...` **之前**，插入：

```csharp
                if (rule.IncludeCategory && !string.IsNullOrEmpty(categoryCode))
                {
                    var catExists = await _db.NumberingCategories
                        .AnyAsync(c => c.TargetTypeCode == targetType
                                    && c.Code == categoryCode && c.IsActive, ct);
                    if (!catExists)
                        throw new DomainException($"分类码 {categoryCode} 不存在或已停用");
                }
```

**3c. 在 PreviewAsync 中同样加校验：**

找到 PreviewAsync 里 `var rule = await _db.NumberingRules...` 之后、`var effectiveCategory = ...` 之前，插入同样的两段校验代码（targetType 校验 + categoryCode 校验）。

- [ ] **Step 4: 运行测试确认通过**

Run: `cd backend && dotnet test tests/OneCup.UnitTests/OneCup.UnitTests.csproj --filter "FullyQualifiedName~NumberingServiceConcurrencyTests"`
Expected: 全部 PASS（含新增 5 个 + 原有测试不回归）

- [ ] **Step 5: NumberTargetTypes.cs 降级注释**

Modify `backend/src/OneCup.Application/Common/NumberTargetTypes.cs`，在类注释中补充降级说明：

将文件顶部的类注释从：
```csharp
/// <summary>
/// 已知业务对象类型清单。引擎不校验 target_type 合法性（见设计 6.1），
/// 此常量类仅作拼写提示与前端下拉选项来源，不强制。
/// </summary>
```

改为：
```csharp
/// <summary>
/// 已知业务对象类型清单（初始值）。
/// 【已降级】字典化改造后，业务类型由 numbering_target_types 表管理，引擎通过强校验消费字典。
/// 本常量类仅保留供种子迁移引用初始 6 个类型，业务代码不应硬编码引用，改用字典查询。
/// </summary>
```

- [ ] **Step 6: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Services/NumberingService.cs \
        backend/src/OneCup.Application/Common/NumberTargetTypes.cs \
        backend/tests/OneCup.UnitTests/Numbering/NumberingServiceConcurrencyTests.cs
git commit -m "feat(infra): 编号引擎字典强校验 + NumberTargetTypes 降级注释"
```

---

## Task 7: 前端 API 客户端

**Files:**
- Create: `frontend/src/api/numberingDictionary.ts`

**Interfaces:**
- Produces: 类型定义 + API 函数（字典管理页和规则抽屉改造都依赖）

- [ ] **Step 1: 创建字典 API 客户端**

Create `frontend/src/api/numberingDictionary.ts`:

```typescript
import request from './request';
import { PagedResult } from './user';

// ── 类型 ──
export interface TargetType {
  id: string;
  code: string;
  nameZh: string;
  nameEn: string;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface Category {
  id: string;
  targetTypeCode: string;
  code: string;
  nameZh: string;
  nameEn: string;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateTargetTypeRequest {
  code: string;
  nameZh: string;
  nameEn: string;
  sortOrder: number;
}

export interface UpdateTargetTypeRequest {
  nameZh?: string;
  nameEn?: string;
  sortOrder?: number;
}

export interface CreateCategoryRequest {
  targetTypeCode: string;
  code: string;
  nameZh: string;
  nameEn: string;
  sortOrder: number;
}

export interface UpdateCategoryRequest {
  nameZh?: string;
  nameEn?: string;
  sortOrder?: number;
}

// ── 业务类型 ──
export function getTargetTypes(params: {
  page?: number; pageSize?: number; keyword?: string; isActive?: boolean;
}) {
  return request.get<unknown, PagedResult<TargetType>>('/api/numbering/dict/target-types', { params });
}

export function getAllActiveTargetTypes() {
  return request.get<unknown, TargetType[]>('/api/numbering/dict/target-types/all');
}

export function getTargetType(id: string) {
  return request.get<unknown, TargetType>(`/api/numbering/dict/target-types/${id}`);
}

export function createTargetType(data: CreateTargetTypeRequest) {
  return request.post<unknown, TargetType>('/api/numbering/dict/target-types', data);
}

export function updateTargetType(id: string, data: UpdateTargetTypeRequest) {
  return request.put(`/api/numbering/dict/target-types/${id}`, data);
}

export function updateTargetTypeStatus(id: string, isActive: boolean) {
  return request.put(`/api/numbering/dict/target-types/${id}/status`, { isActive });
}

// ── 分类 ──
export function getCategories(params: {
  page?: number; pageSize?: number; targetTypeCode?: string;
  keyword?: string; isActive?: boolean;
}) {
  return request.get<unknown, PagedResult<Category>>('/api/numbering/dict/categories', { params });
}

export function getActiveCategories(targetTypeCode: string) {
  return request.get<unknown, Category[]>('/api/numbering/dict/categories/all', {
    params: { targetTypeCode },
  });
}

export function getCategory(id: string) {
  return request.get<unknown, Category>(`/api/numbering/dict/categories/${id}`);
}

export function createCategory(data: CreateCategoryRequest) {
  return request.post<unknown, Category>('/api/numbering/dict/categories', data);
}

export function updateCategory(id: string, data: UpdateCategoryRequest) {
  return request.put(`/api/numbering/dict/categories/${id}`, data);
}

export function updateCategoryStatus(id: string, isActive: boolean) {
  return request.put(`/api/numbering/dict/categories/${id}/status`, { isActive });
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/api/numberingDictionary.ts
git commit -m "feat(fe): 字典 API 客户端"
```

---

## Task 8: 前端字典管理页（locale + 页面）

**Files:**
- Create: `frontend/src/pages/system/numbering/dict/locale/zh-CN.ts`
- Create: `frontend/src/pages/system/numbering/dict/locale/en-US.ts`
- Create: `frontend/src/pages/system/numbering/dict/locale/index.ts`
- Create: `frontend/src/pages/system/numbering/dict/index.tsx`

**Interfaces:**
- Consumes: API from Task 7

- [ ] **Step 1: 创建 zh-CN locale**

Create `frontend/src/pages/system/numbering/dict/locale/zh-CN.ts`:

```typescript
export default {
  'numbering.dict.title': '业务字典',
  // 业务类型区
  'numbering.dict.type.title': '业务类型',
  'numbering.dict.type.create': '新增类型',
  'numbering.dict.type.code': 'Code',
  'numbering.dict.type.nameZh': '中文名',
  'numbering.dict.type.nameEn': '英文名',
  'numbering.dict.type.sortOrder': '排序',
  'numbering.dict.type.status': '状态',
  'numbering.dict.type.operations': '操作',
  // 分类区
  'numbering.dict.category.title': '分类',
  'numbering.dict.category.create': '新增分类',
  'numbering.dict.category.selectType': '请先点击上方选择一个业务类型',
  'numbering.dict.category.code': 'Code',
  'numbering.dict.category.nameZh': '中文名',
  'numbering.dict.category.nameEn': '英文名',
  'numbering.dict.category.sortOrder': '排序',
  'numbering.dict.category.status': '状态',
  'numbering.dict.category.operations': '操作',
  // 表单
  'numbering.dict.form.code': 'Code（英文标识符）',
  'numbering.dict.form.code.placeholder': '如 fabric',
  'numbering.dict.form.nameZh': '中文名',
  'numbering.dict.form.nameEn': '英文名',
  'numbering.dict.form.sortOrder': '排序号',
  'numbering.dict.form.targetType': '所属业务类型',
  'numbering.dict.form.lockedHint': 'Code 创建后不可修改',
  'numbering.dict.form.required': '该项为必填',
  // 状态
  'numbering.dict.active': '启用',
  'numbering.dict.inactive': '停用',
  'numbering.dict.edit': '编辑',
  'numbering.dict.enable': '启用',
  'numbering.dict.disable': '停用',
  'numbering.dict.disable.confirm': '确定要停用吗？',
  'numbering.dict.enable.confirm': '确定要启用吗？',
  'numbering.dict.create.success': '创建成功',
  'numbering.dict.update.success': '更新成功',
  'numbering.dict.status.success': '状态更新成功',
} as const;
```

- [ ] **Step 2: 创建 en-US locale**

Create `frontend/src/pages/system/numbering/dict/locale/en-US.ts`:

```typescript
export default {
  'numbering.dict.title': 'Business Dictionary',
  'numbering.dict.type.title': 'Target Types',
  'numbering.dict.type.create': 'New Type',
  'numbering.dict.type.code': 'Code',
  'numbering.dict.type.nameZh': 'Name (ZH)',
  'numbering.dict.type.nameEn': 'Name (EN)',
  'numbering.dict.type.sortOrder': 'Sort',
  'numbering.dict.type.status': 'Status',
  'numbering.dict.type.operations': 'Actions',
  'numbering.dict.category.title': 'Categories',
  'numbering.dict.category.create': 'New Category',
  'numbering.dict.category.selectType': 'Select a target type above first',
  'numbering.dict.category.code': 'Code',
  'numbering.dict.category.nameZh': 'Name (ZH)',
  'numbering.dict.category.nameEn': 'Name (EN)',
  'numbering.dict.category.sortOrder': 'Sort',
  'numbering.dict.category.status': 'Status',
  'numbering.dict.category.operations': 'Actions',
  'numbering.dict.form.code': 'Code (identifier)',
  'numbering.dict.form.code.placeholder': 'e.g. fabric',
  'numbering.dict.form.nameZh': 'Name (ZH)',
  'numbering.dict.form.nameEn': 'Name (EN)',
  'numbering.dict.form.sortOrder': 'Sort Order',
  'numbering.dict.form.targetType': 'Target Type',
  'numbering.dict.form.lockedHint': 'Code cannot be changed after creation',
  'numbering.dict.form.required': 'Required',
  'numbering.dict.active': 'Active',
  'numbering.dict.inactive': 'Inactive',
  'numbering.dict.edit': 'Edit',
  'numbering.dict.enable': 'Enable',
  'numbering.dict.disable': 'Disable',
  'numbering.dict.disable.confirm': 'Disable this item?',
  'numbering.dict.enable.confirm': 'Enable this item?',
  'numbering.dict.create.success': 'Created',
  'numbering.dict.update.success': 'Updated',
  'numbering.dict.status.success': 'Status updated',
} as const;
```

- [ ] **Step 3: 创建 locale index**

Create `frontend/src/pages/system/numbering/dict/locale/index.ts`:

```typescript
import zhCN from './zh-CN';
import enUS from './en-US';

export default { 'zh-CN': zhCN, 'en-US': enUS };
```

- [ ] **Step 4: 创建字典管理页面**

Create `frontend/src/pages/system/numbering/dict/index.tsx`:

```tsx
import { useEffect, useState, useCallback } from 'react';
import {
  Table, Button, Drawer, Form, Input, InputNumber, Switch,
  Tag, Popconfirm, Message, Space, Alert, Typography,
} from '@arco-design/web-react';
import { IconPlus } from '@arco-design/web-react/icon';
import useLocale from '@/utils/useLocale';
import {
  getTargetTypes, createTargetType, updateTargetType, updateTargetTypeStatus,
  getCategories, createCategory, updateCategory, updateCategoryStatus,
  TargetType, Category,
  CreateTargetTypeRequest, CreateCategoryRequest,
} from '@/api/numberingDictionary';
import locale from './locale';

const FormItem = Form.Item;

export default function NumberingDictionaryPage() {
  const t = useLocale(locale);

  // ── 业务类型 ──
  const [typeData, setTypeData] = useState<TargetType[]>([]);
  const [typeLoading, setTypeLoading] = useState(false);
  const [selectedTypeId, setSelectedTypeId] = useState<string | null>(null);
  const selectedType = typeData.find((x) => x.id === selectedTypeId);

  // 业务类型抽屉
  const [typeDrawerVisible, setTypeDrawerVisible] = useState(false);
  const [typeEditMode, setTypeEditMode] = useState<'create' | 'edit'>('create');
  const [editingTypeId, setEditingTypeId] = useState<string | null>(null);
  const [typeForm] = Form.useForm();

  // ── 分类 ──
  const [categoryData, setCategoryData] = useState<Category[]>([]);
  const [categoryLoading, setCategoryLoading] = useState(false);

  // 分类抽屉
  const [catDrawerVisible, setCatDrawerVisible] = useState(false);
  const [catEditMode, setCatEditMode] = useState<'create' | 'edit'>('create');
  const [editingCatId, setEditingCatId] = useState<string | null>(null);
  const [catForm] = Form.useForm();

  // ── 拉取业务类型 ──
  const fetchTypes = useCallback(async () => {
    setTypeLoading(true);
    try {
      // 拉全量（不分页），字典项通常不多
      const res = await getTargetTypes({ page: 1, pageSize: 200 });
      setTypeData(res.items);
    } finally {
      setTypeLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchTypes();
  }, [fetchTypes]);

  // ── 拉取选中类型的分类 ──
  const fetchCategories = useCallback(async () => {
    if (!selectedType) return;
    setCategoryLoading(true);
    try {
      const res = await getCategories({
        page: 1, pageSize: 200, targetTypeCode: selectedType.code,
      });
      setCategoryData(res.items);
    } finally {
      setCategoryLoading(false);
    }
  }, [selectedType]);

  useEffect(() => {
    if (selectedType) {
      fetchCategories();
    } else {
      setCategoryData([]);
    }
  }, [selectedType, fetchCategories]);

  // ── 业务类型操作 ──
  function openCreateType() {
    setTypeEditMode('create');
    setEditingTypeId(null);
    typeForm.resetFields();
    typeForm.setFieldsValue({ sortOrder: 0 });
    setTypeDrawerVisible(true);
  }

  function openEditType(record: TargetType) {
    setTypeEditMode('edit');
    setEditingTypeId(record.id);
    typeForm.resetFields();
    typeForm.setFieldsValue({
      nameZh: record.nameZh, nameEn: record.nameEn, sortOrder: record.sortOrder,
    });
    setTypeDrawerVisible(true);
  }

  async function handleTypeOk() {
    try {
      const values = await typeForm.validate();
      if (typeEditMode === 'create') {
        await createTargetType(values as CreateTargetTypeRequest);
        Message.success(t['numbering.dict.create.success']);
      } else {
        await updateTargetType(editingTypeId!, {
          nameZh: values.nameZh, nameEn: values.nameEn, sortOrder: values.sortOrder,
        });
        Message.success(t['numbering.dict.update.success']);
      }
      setTypeDrawerVisible(false);
      fetchTypes();
    } catch {
      // 校验失败或 API 错误
    }
  }

  async function handleToggleTypeStatus(record: TargetType) {
    await updateTargetTypeStatus(record.id, !record.isActive);
    Message.success(t['numbering.dict.status.success']);
    fetchTypes();
  }

  // ── 分类操作 ──
  function openCreateCategory() {
    setCatEditMode('create');
    setEditingCatId(null);
    catForm.resetFields();
    catForm.setFieldsValue({ sortOrder: 0 });
    setCatDrawerVisible(true);
  }

  function openEditCategory(record: Category) {
    setCatEditMode('edit');
    setEditingCatId(record.id);
    catForm.resetFields();
    catForm.setFieldsValue({
      nameZh: record.nameZh, nameEn: record.nameEn, sortOrder: record.sortOrder,
    });
    setCatDrawerVisible(true);
  }

  async function handleCategoryOk() {
    try {
      const values = await catForm.validate();
      if (catEditMode === 'create') {
        await createCategory({
          ...values,
          targetTypeCode: selectedType!.code,
        } as CreateCategoryRequest);
        Message.success(t['numbering.dict.create.success']);
      } else {
        await updateCategory(editingCatId!, {
          nameZh: values.nameZh, nameEn: values.nameEn, sortOrder: values.sortOrder,
        });
        Message.success(t['numbering.dict.update.success']);
      }
      setCatDrawerVisible(false);
      fetchCategories();
    } catch {
      // ignore
    }
  }

  async function handleToggleCatStatus(record: Category) {
    await updateCategoryStatus(record.id, !record.isActive);
    Message.success(t['numbering.dict.status.success']);
    fetchCategories();
  }

  // ── 列定义 ──
  const typeColumns = [
    { title: t['numbering.dict.type.code'], dataIndex: 'code', width: 120 },
    { title: t['numbering.dict.type.nameZh'], dataIndex: 'nameZh', width: 120 },
    { title: t['numbering.dict.type.nameEn'], dataIndex: 'nameEn', width: 120 },
    { title: t['numbering.dict.type.sortOrder'], dataIndex: 'sortOrder', width: 80 },
    {
      title: t['numbering.dict.type.status'],
      dataIndex: 'isActive',
      width: 80,
      render: (v: boolean) => v
        ? <Tag color="green">{t['numbering.dict.active']}</Tag>
        : <Tag>{t['numbering.dict.inactive']}</Tag>,
    },
    {
      title: t['numbering.dict.type.operations'],
      dataIndex: 'operations',
      width: 160,
      render: (_: unknown, record: TargetType) => (
        <Space>
          <Button type="text" size="small" onClick={() => openEditType(record)}>
            {t['numbering.dict.edit']}
          </Button>
          <Popconfirm
            title={record.isActive
              ? t['numbering.dict.disable.confirm']
              : t['numbering.dict.enable.confirm']}
            onOk={() => handleToggleTypeStatus(record)}
          >
            <Button type="text" size="small" status={record.isActive ? 'warning' : 'success'}>
              {record.isActive ? t['numbering.dict.disable'] : t['numbering.dict.enable']}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  const categoryColumns = [
    { title: t['numbering.dict.category.code'], dataIndex: 'code', width: 120 },
    { title: t['numbering.dict.category.nameZh'], dataIndex: 'nameZh', width: 120 },
    { title: t['numbering.dict.category.nameEn'], dataIndex: 'nameEn', width: 120 },
    { title: t['numbering.dict.category.sortOrder'], dataIndex: 'sortOrder', width: 80 },
    {
      title: t['numbering.dict.category.status'],
      dataIndex: 'isActive',
      width: 80,
      render: (v: boolean) => v
        ? <Tag color="green">{t['numbering.dict.active']}</Tag>
        : <Tag>{t['numbering.dict.inactive']}</Tag>,
    },
    {
      title: t['numbering.dict.category.operations'],
      dataIndex: 'operations',
      width: 160,
      render: (_: unknown, record: Category) => (
        <Space>
          <Button type="text" size="small" onClick={() => openEditCategory(record)}>
            {t['numbering.dict.edit']}
          </Button>
          <Popconfirm
            title={record.isActive
              ? t['numbering.dict.disable.confirm']
              : t['numbering.dict.enable.confirm']}
            onOk={() => handleToggleCatStatus(record)}
          >
            <Button type="text" size="small" status={record.isActive ? 'warning' : 'success'}>
              {record.isActive ? t['numbering.dict.disable'] : t['numbering.dict.enable']}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div>
      {/* ── 业务类型区 ── */}
      <div style={{ marginBottom: 8, fontWeight: 600 }}>
        {t['numbering.dict.type.title']}
      </div>
      <Space style={{ marginBottom: 12, justifyContent: 'space-between', width: '100%' }}>
        <span />
        <Button type="primary" icon={<IconPlus />} onClick={openCreateType}>
          {t['numbering.dict.type.create']}
        </Button>
      </Space>
      <Table
        rowKey="id"
        columns={typeColumns}
        data={typeData}
        loading={typeLoading}
        pagination={false}
        size="compact"
        rowSelection={{
          type: 'radio',
          selectedRowKeys: selectedTypeId ? [selectedTypeId] : [],
          onChange: (keys) => setSelectedTypeId(keys[0] as string),
        }}
        onRow={(record: TargetType) => ({
          onClick: () => setSelectedTypeId(record.id),
        })}
      />

      {/* ── 分类区 ── */}
      <div style={{ marginTop: 24, marginBottom: 8, fontWeight: 600 }}>
        {t['numbering.dict.category.title']}
        {selectedType && (
          <span style={{ marginLeft: 8, color: 'var(--color-text-3)', fontWeight: 400 }}>
            ({selectedType.nameZh} / {selectedType.code})
          </span>
        )}
      </div>
      {selectedType ? (
        <>
          <Space style={{ marginBottom: 12, justifyContent: 'space-between', width: '100%' }}>
            <span />
            <Button type="primary" icon={<IconPlus />} onClick={openCreateCategory}>
              {t['numbering.dict.category.create']}
            </Button>
          </Space>
          <Table
            rowKey="id"
            columns={categoryColumns}
            data={categoryData}
            loading={categoryLoading}
            pagination={false}
            size="compact"
          />
        </>
      ) : (
        <Alert type="info" content={t['numbering.dict.category.selectType']} />
      )}

      {/* ── 业务类型抽屉 ── */}
      <Drawer
        title={typeEditMode === 'create'
          ? t['numbering.dict.type.create']
          : t['numbering.dict.edit']}
        visible={typeDrawerVisible}
        onOk={handleTypeOk}
        onCancel={() => setTypeDrawerVisible(false)}
        width={440}
        unmountOnExit
      >
        {typeEditMode === 'create' && (
          <FormItem
            label={t['numbering.dict.form.code']}
            field="code"
            rules={[{ required: true, message: t['numbering.dict.form.required'] }]}
          >
            <Input placeholder={t['numbering.dict.form.code.placeholder']} />
          </FormItem>
        )}
        {typeEditMode === 'edit' && (
          <Alert type="info" content={t['numbering.dict.form.lockedHint']} style={{ marginBottom: 16 }} />
        )}
        <Form form={typeForm} layout="vertical">
          {typeEditMode === 'edit' && (
            <FormItem label={t['numbering.dict.form.code']}>
              <Input disabled value={typeData.find((x) => x.id === editingTypeId)?.code} />
            </FormItem>
          )}
          <FormItem
            label={t['numbering.dict.form.nameZh']}
            field="nameZh"
            rules={[{ required: true, message: t['numbering.dict.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem
            label={t['numbering.dict.form.nameEn']}
            field="nameEn"
            rules={[{ required: true, message: t['numbering.dict.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem label={t['numbering.dict.form.sortOrder']} field="sortOrder">
            <InputNumber min={0} style={{ width: '100%' }} />
          </FormItem>
        </Form>
      </Drawer>

      {/* ── 分类抽屉 ── */}
      <Drawer
        title={catEditMode === 'create'
          ? t['numbering.dict.category.create']
          : t['numbering.dict.edit']}
        visible={catDrawerVisible}
        onOk={handleCategoryOk}
        onCancel={() => setCatDrawerVisible(false)}
        width={440}
        unmountOnExit
      >
        {catEditMode === 'edit' && (
          <Alert type="info" content={t['numbering.dict.form.lockedHint']} style={{ marginBottom: 16 }} />
        )}
        <Form form={catForm} layout="vertical">
          {catEditMode === 'create' && (
            <>
              <FormItem label={t['numbering.dict.form.targetType']}>
                <Input disabled value={`${selectedType?.nameZh} (${selectedType?.code})`} />
              </FormItem>
              <FormItem
                label={t['numbering.dict.form.code']}
                field="code"
                rules={[{ required: true, message: t['numbering.dict.form.required'] }]}
              >
                <Input placeholder="如 COT" />
              </FormItem>
            </>
          )}
          {catEditMode === 'edit' && (
            <FormItem label={t['numbering.dict.form.code']}>
              <Input disabled value={categoryData.find((x) => x.id === editingCatId)?.code} />
            </FormItem>
          )}
          <FormItem
            label={t['numbering.dict.form.nameZh']}
            field="nameZh"
            rules={[{ required: true, message: t['numbering.dict.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem
            label={t['numbering.dict.form.nameEn']}
            field="nameEn"
            rules={[{ required: true, message: t['numbering.dict.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem label={t['numbering.dict.form.sortOrder']} field="sortOrder">
            <InputNumber min={0} style={{ width: '100%' }} />
          </FormItem>
        </Form>
      </Drawer>
    </div>
  );
}
```

- [ ] **Step 5: Commit**

```bash
git add frontend/src/pages/system/numbering/dict/
git commit -m "feat(fe): 字典管理页 (主从表联动) + locale"
```

---

## Task 9: 前端路由 + 菜单接入

**Files:**
- Modify: `frontend/src/routes.ts`
- Modify: `frontend/src/router.tsx`

- [ ] **Step 1: 在 routes.ts 新增字典菜单项**

Modify `frontend/src/routes.ts`，在 `system/numbering` 菜单项（约第 41-45 行）之后，新增字典子菜单：

在 `{ name: 'menu.system.numbering', key: 'system/numbering', ... }` 之后新增：

```typescript
      {
        name: 'menu.system.numbering.dict',
        key: 'system/numbering/dict',
        requiredPermissions: [
          { resource: 'system:numbering', actions: ['view'] },
        ],
      },
```

- [ ] **Step 2: 在 router.tsx 新增懒加载 + 路由**

Modify `frontend/src/router.tsx`:

在 `const NumberingPage = lazy(...)` 行（约第 16 行）之后新增：

```tsx
const NumberingDictPage = lazy(() => import('@/pages/system/numbering/dict'));
```

在 `system/numbering` 路由项（约第 108-114 行）之后新增：

```tsx
      {
        path: 'system/numbering/dict',
        element: withSuspense(
          <RequirePermission resource="system:numbering" actions={['view']}>
            <NumberingDictPage />
          </RequirePermission>
        ),
      },
```

- [ ] **Step 3: 在全局 locale 补充菜单文案**

找到全局菜单 locale 文件（`frontend/src/locale/zh-CN.ts` 和 `en-US.ts`），在 `menu.system.numbering` 对应行之后新增：

zh-CN.ts:
```typescript
  'menu.system.numbering.dict': '业务字典',
```

en-US.ts:
```typescript
  'menu.system.numbering.dict': 'Dictionary',
```

> **注意：** 实现者需确认全局 locale 文件的确切路径和 key 格式，与现有 `menu.system.numbering` 的写法保持一致。

- [ ] **Step 4: 前端编译验证**

Run: `cd frontend && npm run build`
Expected: BUILD 成功，无 TypeScript 错误

- [ ] **Step 5: Commit**

```bash
git add frontend/src/routes.ts frontend/src/router.tsx frontend/src/locale/
git commit -m "feat(fe): 字典管理页路由 + 菜单接入"
```

---

## Task 10: 改造现有编号规则抽屉（下拉动态化）

**Files:**
- Modify: `frontend/src/pages/system/numbering/index.tsx`
- Modify: `frontend/src/pages/system/numbering/locale/zh-CN.ts`
- Modify: `frontend/src/pages/system/numbering/locale/en-US.ts`

- [ ] **Step 1: 在 numbering locale 补充动态下拉相关 key**

Modify `frontend/src/pages/system/numbering/locale/zh-CN.ts`，在业务类型选项块之后新增：

```typescript
  // 动态下拉
  'numbering.form.preview.categoryHint': '（实际分类以业务对象为准）',
  'numbering.logs.categoryName': '分类名称',
```

Modify `frontend/src/pages/system/numbering/locale/en-US.ts`，新增对应英文：

```typescript
  'numbering.form.preview.categoryHint': '(actual category depends on business object)',
  'numbering.logs.categoryName': 'Category Name',
```

- [ ] **Step 2: 改造 index.tsx 业务类型下拉为动态拉取**

Modify `frontend/src/pages/system/numbering/index.tsx`:

**2a. 删除写死的 TARGET_TYPE_OPTIONS 常量**（第 41-48 行的 `const TARGET_TYPE_OPTIONS = [...]`），替换为从 API 动态拉取。

在文件顶部 import 区（约第 33 行后）新增：

```tsx
import { getAllActiveTargetTypes, TargetType } from '@/api/numberingDictionary';
```

在组件内部（`const t = useLocale(locale);` 之后）新增状态 + 拉取：

```tsx
  const [targetTypeOptions, setTargetTypeOptions] = useState<TargetType[]>([]);

  useEffect(() => {
    getAllActiveTargetTypes().then(setTargetTypeOptions).catch(() => {});
  }, []);
```

**2b. 替换所有 `TARGET_TYPE_OPTIONS.map` 为 `targetTypeOptions.map`**

文件中有 3 处用到 `TARGET_TYPE_OPTIONS`（规则筛选下拉、日志筛选下拉、表单内业务类型下拉）。每处的 `<Select.Option>` 渲染改为：

```tsx
{targetTypeOptions.map((tp) => (
  <Select.Option key={tp.code} value={tp.code}>
    {tp.nameZh}
  </Select.Option>
))}
```

**2c. 改造实时预览的分类占位符**

找到 `previewCode` 的 `useMemo`（约第 142-162 行），将 `if (v.includeCategory) segments.push('CAT');` 这行改为：尝试取该业务类型下第一个启用分类的 code，取不到则用 `CAT` 占位。

在组件内部新增（在 targetTypeOptions 状态之后）：

```tsx
  const [categoryOptions, setCategoryOptions] = useState<{ code: string; nameZh: string }[]>([]);

  // 当表单 targetType 变化时拉取该类型的分类
  useEffect(() => {
    if (formValues.targetType) {
      getActiveCategories(formValues.targetType)
        .then((list) => setCategoryOptions(list.map((c) => ({ code: c.code, nameZh: c.nameZh }))))
        .catch(() => setCategoryOptions([]));
    } else {
      setCategoryOptions([]);
    }
  }, [formValues.targetType]);
```

并在顶部 import 补充：

```tsx
import { getActiveCategories } from '@/api/numberingDictionary';
```

将预览的 `if (v.includeCategory) segments.push('CAT');` 改为：

```tsx
    if (v.includeCategory) segments.push(categoryOptions[0]?.code || 'CAT');
```

并在 `useMemo` 依赖数组中加入 `categoryOptions`。

- [ ] **Step 3: 日志 Tab 业务类型/分类列显示中文名（设计 §5.3）**

当前日志列 `targetType` 和 `categoryCode` 直接显示 code（如 `fabric` / `COT`）。改为优先显示中文名。

**3a. 构建一个 code→名称 的映射辅助函数。** 在组件内部（拉取 targetTypeOptions 之后）新增：

```tsx
  // 日志列显示辅助：code → 中文名映射（兜底显示 code 本身，停用项也能显示）
  const targetTypeNameMap = useMemo(() => {
    const m: Record<string, string> = {};
    targetTypeOptions.forEach((t) => { m[t.code] = t.nameZh; });
    return m;
  }, [targetTypeOptions]);
```

**3b. 改造日志列定义的 targetType 渲染。** 找到 `logColumns` 中 `targetType` 列的 render（约第 357-359 行），从：

```tsx
      render: (val: string) => t[`numbering.targetType.${val}`] || val,
```

改为：

```tsx
      render: (val: string) => targetTypeNameMap[val] || val,
```

**3c. 分类列保持显示 code 即可**（分类联动依赖具体业务类型，全局映射代价大；code 本身已具业务含义，如 `COT`）。如后续需要中文名，可在日志行 hover 时通过 tooltip 展示，本轮不做。

- [ ] **Step 4: 前端编译验证**

Run: `cd frontend && npm run build`
Expected: BUILD 成功

- [ ] **Step 5: Commit**

```bash
git add frontend/src/pages/system/numbering/index.tsx \
        frontend/src/pages/system/numbering/locale/zh-CN.ts \
        frontend/src/pages/system/numbering/locale/en-US.ts
git commit -m "feat(fe): 编号规则抽屉下拉动态拉取 + 预览优化 + 日志列中文名"
```

---

## Task 11: 全量验证 + 收尾

**Files:**
- 无新增，全量回归验证

- [ ] **Step 1: 后端全量编译 + 单元测试**

Run:
```bash
cd backend
dotnet build
dotnet test tests/OneCup.UnitTests/OneCup.UnitTests.csproj
```
Expected: BUILD SUCCEEDED + 全部测试 PASS（含新增 NumberingDictionaryServiceTests 8 个 + NumberingServiceConcurrencyTests 扩展）

- [ ] **Step 2: 数据库迁移应用（如有开发库）**

Run:
```bash
cd backend
dotnet ef database update --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```
Expected: 迁移成功应用，`numbering_target_types` 含 6 条种子数据

- [ ] **Step 3: 前端全量构建**

Run: `cd frontend && npm run build`
Expected: BUILD 成功

- [ ] **Step 4: 手动冒烟测试清单**

启动前后端后，验证：
- [ ] 编号管理菜单下出现"业务字典"子菜单
- [ ] 业务字典页显示 6 个种子业务类型（面料/原料/设备/客户/颜色/产品）
- [ ] 点击某业务类型行 → 下方分类区联动（初始为空 + 提示）
- [ ] 新增分类（如 fabric 下 COT/棉）→ 成功显示
- [ ] 编号规则抽屉的业务类型下拉显示中文名而非 code
- [ ] 编号规则抽屉实时预览：开启分类码后显示真实分类 code 而非 CAT
- [ ] 停用一个分类 → 引擎取号时报错

- [ ] **Step 5: 最终 Commit（如有遗留改动）**

```bash
git add -A
git commit -m "chore: 字典模块收尾验证"
```
