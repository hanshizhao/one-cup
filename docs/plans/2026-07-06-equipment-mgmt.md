# 设备管理模块实现计划（Equipment Management）

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现设备管理模块，包含设备类型（含参数定义 + 运行模板）和设备实例，支持类型级参数 schema 定义、按工序绑定的运行模板、多类型参数值校验。

**Architecture:** 后端遵循项目 clean architecture（Domain/Entities → Application/DTOs+Services+Specs → Infrastructure/EF+Migrations → Api/Controllers）。前端侧边栏一项"设备" + 页面内 Tabs（设备/设备类型）。设备类型和设备实例走编号引擎（c02），运行模板按 (类型,工序) 维度人工命名。测试用 EF InMemory + 真实 Repository/UnitOfWork + 内联 FakeNumberingService，无 mock 框架。

**Tech Stack:** .NET 10 / EF Core 10 (Npgsql + InMemory for tests) / FluentValidation / xUnit / React + Vite + Arco Design Pro + TypeScript

## Global Constraints

- 后端分层路径固定：实体→`Domain/Entities`，DTO→`Application/Dtos/System/EquipmentDtos.cs`，接口→`Application/Interfaces/`，服务→`Application/Services/`，规范→`Application/Specifications/`，校验器→`Application/Validators/System/`，EF配置→`Infrastructure/Persistence/Configurations/`，控制器→`Api/Controllers/`，DI→`Api/Program.cs`，DbSet→`OneCupDbContext.cs`
- 表名复数 snake_case（`equipment_types`、`equipments`）；列名 snake_case；唯一索引用 `ux_` 前缀；PK 用 `PK_<table>`
- 编号引擎调用必须 `_numbering.GenerateAsync(NumberTargetTypes.Xxx, request.CategoryCode, ct)`，禁止硬编码 null（c02）
- 设备实例软删除（ISoftDeletable + HasQueryFilter）；类型/参数/模板/值物理删除
- 前端列表页从 `docs/specs/templates/query-table-page.template.tsx` 复制，不手写布局
- 前端编号表单用 `useNumberingPreview(targetType)` hook + `<CategorySelect>`，不手写 state
- 设备类型走 `'EquipmentType'` targetType，设备实例走 `'Equipment'` targetType
- 测试无 mock 框架：真实 Repository<T> + UnitOfWork + EF InMemory + 内联 FakeNumberingService
- spec 文档：`docs/specs/2026-07-06-equipment-mgmt-design.md`

---

## Task 1: 新增枚举（ParameterValueType / EquipmentStatus）

**Files:**
- Create: `backend/src/OneCup.Domain/Enums/ParameterValueType.cs`
- Create: `backend/src/OneCup.Domain/Enums/EquipmentStatus.cs`

**Interfaces:**
- Produces: `ParameterValueType` enum（Number=0/Text=1/Enum=2）、`EquipmentStatus` enum（Running=0/Stopped=1/Maintenance=2），供 Task 2 实体引用

- [ ] **Step 1: 创建 ParameterValueType.cs**

```csharp
namespace OneCup.Domain.Enums;

/// <summary>
/// 设备参数的值类型，决定输入控件与校验分支。
/// </summary>
public enum ParameterValueType
{
    /// <summary>数值型 — 带 Min/Max/Precision 范围校验</summary>
    Number = 0,
    /// <summary>文本型 — 自由文本</summary>
    Text = 1,
    /// <summary>枚举型 — 值必须在 Options 列表内</summary>
    Enum = 2,
}
```

- [ ] **Step 2: 创建 EquipmentStatus.cs**

```csharp
namespace OneCup.Domain.Enums;

/// <summary>
/// 设备实例的运行状态。
/// </summary>
public enum EquipmentStatus
{
    /// <summary>运行中</summary>
    Running = 0,
    /// <summary>停机</summary>
    Stopped = 1,
    /// <summary>维修中</summary>
    Maintenance = 2,
}
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build backend/src/OneCup.Domain/OneCup.Domain.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 4: Commit**

```bash
git add backend/src/OneCup.Domain/Enums/ParameterValueType.cs backend/src/OneCup.Domain/Enums/EquipmentStatus.cs
git commit -m "feat(equipment): 新增 ParameterValueType 和 EquipmentStatus 枚举"
```

---

## Task 2: 新增 5 个实体

**Files:**
- Create: `backend/src/OneCup.Domain/Entities/EquipmentType.cs`
- Create: `backend/src/OneCup.Domain/Entities/EquipmentTypeParameter.cs`
- Create: `backend/src/OneCup.Domain/Entities/EquipmentTemplate.cs`
- Create: `backend/src/OneCup.Domain/Entities/EquipmentTemplateValue.cs`
- Create: `backend/src/OneCup.Domain/Entities/Equipment.cs`

**Interfaces:**
- Consumes: `BaseEntity`（`Domain/Entities/BaseEntity.cs`，提供 Id/CreatedAt/UpdatedAt）、`ISoftDeletable`（`Domain/Entities/ISoftDeletable.cs`，提供 IsDeleted）、Task 1 的两个枚举
- Produces: 5 个实体类，供 Task 3 EF 配置引用

- [ ] **Step 1: 创建 EquipmentType.cs**

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 设备类型（定型机/染色机/烧毛机…）。编号由编号系统生成。
/// 承载参数定义 schema（EquipmentTypeParameter）和运行模板（EquipmentTemplate）。
/// 物理删除——删除前需校验无设备引用、无模板。
/// </summary>
public class EquipmentType : BaseEntity
{
    /// <summary>类型编号（编号系统生成）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>类型名称（唯一）</summary>
    public string Name { get; set; } = string.Empty;

    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;

    /// <summary>参数定义 schema（子集合）</summary>
    public List<EquipmentTypeParameter> Parameters { get; set; } = new();

    /// <summary>运行模板</summary>
    public List<EquipmentTemplate> Templates { get; set; } = new();
}
```

- [ ] **Step 2: 创建 EquipmentTypeParameter.cs**

```csharp
using OneCup.Domain.Enums;

namespace OneCup.Domain.Entities;

/// <summary>
/// 设备类型的参数定义。描述该类型设备有哪些参数、每个参数的约束。
/// 随 EquipmentType 整表替换（PUT 时按 Id diff：null=新增、有值=更新、缺失=删除）。
/// 物理删除——删除后引用它的模板值变孤儿，由读时校验检测。
/// </summary>
public class EquipmentTypeParameter : BaseEntity
{
    public Guid EquipmentTypeId { get; set; }

    /// <summary>参数名（同类型内唯一）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>值类型</summary>
    public ParameterValueType ValueType { get; set; }

    /// <summary>单位（关联计量单位，Number 类型用）</summary>
    public Guid? UnitId { get; set; }

    /// <summary>数值下限（Number 类型）</summary>
    public string? MinValue { get; set; }

    /// <summary>数值上限（Number 类型）</summary>
    public string? MaxValue { get; set; }

    /// <summary>小数位限制（Number 类型）</summary>
    public int? Precision { get; set; }

    /// <summary>枚举可选值（JSON 数组字符串，Enum 类型）</summary>
    public string? Options { get; set; }

    public bool Required { get; set; } = false;
    public int SortOrder { get; set; } = 0;
    public string? Remark { get; set; }
}
```

- [ ] **Step 3: 创建 EquipmentTemplate.cs**

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 运行模板——某设备类型在某工序下的运行方案（如烧毛机·烧毛工序·轻烧）。
/// 绑定 (EquipmentTypeId, ProcessId)，名称在三元组内唯一。
/// 不走编号引擎。物理删除。
/// </summary>
public class EquipmentTemplate : BaseEntity
{
    public Guid EquipmentTypeId { get; set; }
    public Guid ProcessId { get; set; }

    /// <summary>模板名（TypeId+ProcessId+Name 唯一）</summary>
    public string Name { get; set; } = string.Empty;

    public string? Remark { get; set; }
    public int SortOrder { get; set; } = 0;

    /// <summary>参数值集合</summary>
    public List<EquipmentTemplateValue> Values { get; set; } = new();
}
```

- [ ] **Step 4: 创建 EquipmentTemplateValue.cs**

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 模板的参数值。Value 用统一字符串列承载所有类型（Number/Text/Enum）。
/// (TemplateId, ParameterId) 唯一——一个模板对同一参数只有一个值。
/// </summary>
public class EquipmentTemplateValue : BaseEntity
{
    public Guid EquipmentTemplateId { get; set; }

    /// <summary>引用参数定义（EquipmentTypeParameter）</summary>
    public Guid ParameterId { get; set; }

    /// <summary>统一字符串承载（Number/Text/Enum 共用）</summary>
    public string? Value { get; set; }
}
```

- [ ] **Step 5: 创建 Equipment.cs**

```csharp
using OneCup.Domain.Enums;

namespace OneCup.Domain.Entities;

/// <summary>
/// 设备实例。编号由编号系统生成。软删除（未来被工单引用需保留审计）。
/// 不存参数值——参数值只存在于运行模板。
/// </summary>
public class Equipment : BaseEntity, ISoftDeletable
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;

    /// <summary>所属设备类型（FK，无导航属性，与 Material→Unit 一致的宽松耦合）</summary>
    public Guid EquipmentTypeId { get; set; }

    /// <summary>规格型号</summary>
    public string? Specification { get; set; }

    public string? Supplier { get; set; }
    public string? Location { get; set; }
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Running;
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyExpiry { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public bool IsDeleted { get; set; } = false;
}
```

- [ ] **Step 6: 编译验证**

Run: `dotnet build backend/src/OneCup.Domain/OneCup.Domain.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 7: Commit**

```bash
git add backend/src/OneCup.Domain/Entities/Equipment*.cs
git commit -m "feat(equipment): 新增 5 个实体（EquipmentType/Parameter/Template/TemplateValue/Equipment）"
```

---

## Task 3: EF 配置（5 个实体）

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/EquipmentTypeConfiguration.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/EquipmentTypeParameterConfiguration.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/EquipmentTemplateConfiguration.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/EquipmentTemplateValueConfiguration.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/EquipmentConfiguration.cs`
- Modify: `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`（追加 5 个 DbSet）

**Interfaces:**
- Consumes: Task 2 的 5 个实体、现有 `MeasurementUnit`/`Process` 实体（FK 目标）
- Produces: 5 个 IEntityTypeConfiguration + 5 个 DbSet 注册，供 Task 4 迁移生成

参考模式：`MaterialConfiguration.cs`（nullable FK + Restrict）、`ProcessConfiguration.cs`（软删除 + HasQueryFilter）

- [ ] **Step 1: 创建 EquipmentTypeConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class EquipmentTypeConfiguration : IEntityTypeConfiguration<EquipmentType>
{
    public void Configure(EntityTypeBuilder<EquipmentType> builder)
    {
        builder.ToTable("equipment_types");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.Code).HasDatabaseName("ux_equipment_types_code").IsUnique();
        builder.HasIndex(e => e.Name).HasDatabaseName("ux_equipment_types_name").IsUnique();

        // 参数定义子集合：级联删除（类型删则参数删）
        builder.HasMany(e => e.Parameters)
            .WithOne()
            .HasForeignKey(p => p.EquipmentTypeId)
            .OnDelete(DeleteBehavior.Cascade);

        // 运行模板子集合：级联删除
        builder.HasMany(e => e.Templates)
            .WithOne()
            .HasForeignKey(t => t.EquipmentTypeId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 2: 创建 EquipmentTypeParameterConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class EquipmentTypeParameterConfiguration : IEntityTypeConfiguration<EquipmentTypeParameter>
{
    public void Configure(EntityTypeBuilder<EquipmentTypeParameter> builder)
    {
        builder.ToTable("equipment_type_parameters");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.EquipmentTypeId).HasColumnName("equipment_type_id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(e => e.ValueType).HasColumnName("value_type").HasConversion<int>();
        builder.Property(e => e.UnitId).HasColumnName("unit_id");
        builder.Property(e => e.MinValue).HasColumnName("min_value").HasMaxLength(50);
        builder.Property(e => e.MaxValue).HasColumnName("max_value").HasMaxLength(50);
        builder.Property(e => e.Precision).HasColumnName("precision");
        builder.Property(e => e.Options).HasColumnName("options");
        builder.Property(e => e.Required).HasColumnName("required").IsRequired();
        builder.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        // 同类型内参数名唯一
        builder.HasIndex(e => new { e.EquipmentTypeId, e.Name })
            .HasDatabaseName("ux_equipment_type_parameters_type_name")
            .IsUnique();

        // 单位 FK：Restrict，删单位不连带删参数
        builder.HasOne<MeasurementUnit>()
            .WithMany()
            .HasForeignKey(e => e.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 3: 创建 EquipmentTemplateConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class EquipmentTemplateConfiguration : IEntityTypeConfiguration<EquipmentTemplate>
{
    public void Configure(EntityTypeBuilder<EquipmentTemplate> builder)
    {
        builder.ToTable("equipment_templates");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.EquipmentTypeId).HasColumnName("equipment_type_id");
        builder.Property(e => e.ProcessId).HasColumnName("process_id");
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
        builder.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        // (类型, 工序, 名称) 三元组唯一
        builder.HasIndex(e => new { e.EquipmentTypeId, e.ProcessId, e.Name })
            .HasDatabaseName("ux_equipment_templates_type_process_name")
            .IsUnique();

        // 工序 FK：Restrict（工序是软删除，此处不级联）
        builder.HasOne<Process>()
            .WithMany()
            .HasForeignKey(e => e.ProcessId)
            .OnDelete(DeleteBehavior.Restrict);

        // 参数值子集合：级联删除（模板删则值删）
        builder.HasMany(e => e.Values)
            .WithOne()
            .HasForeignKey(v => v.EquipmentTemplateId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
```

- [ ] **Step 4: 创建 EquipmentTemplateValueConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class EquipmentTemplateValueConfiguration : IEntityTypeConfiguration<EquipmentTemplateValue>
{
    public void Configure(EntityTypeBuilder<EquipmentTemplateValue> builder)
    {
        builder.ToTable("equipment_template_values");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.EquipmentTemplateId).HasColumnName("equipment_template_id");
        builder.Property(e => e.ParameterId).HasColumnName("parameter_id");
        builder.Property(e => e.Value).HasColumnName("value").HasMaxLength(200);
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        // 一个模板对同一参数只有一个值
        builder.HasIndex(e => new { e.EquipmentTemplateId, e.ParameterId })
            .HasDatabaseName("ux_equipment_template_values_template_parameter")
            .IsUnique();

        // 参数定义 FK：不配置导航关系约束（无导航属性）。
        // 注意：参数定义删除时，引用它的模板值应保留为孤儿（由读时校验标记 orphan），
        // 而非被数据库连带删除。但参数定义属于 EquipmentType 子集合，删除通过
        // 类型更新时的整表替换 diff 发生（见 EquipmentTypeService.SyncParameters）。
        // 由于此 FK 无导航属性配置，EF 不会自动管理此关系的级联，
        // 删除参数定义不会触发数据库层面的值删除——孤儿值保留。
        // 不配 HasOne<>().WithMany() 避免 EF 在同聚合多级级联（Type→Parameter→Value）产生冲突。
    }
}
```

> **关键说明（FK 策略）**：`EquipmentTemplateValue.ParameterId` 不配置 `HasOne<EquipmentTypeParameter>().WithMany()` 导航关系。原因：参数定义（EquipmentTypeParameter）和模板值（EquipmentTemplateValue）分属不同聚合（前者属于 EquipmentType，后者属于 EquipmentTemplate），跨聚合引用用裸 Guid FK（与项目 Material→MeasurementUnit 的"无导航属性"风格一致）。这样删除参数定义时，EF 不会尝试级联处理模板值，存量值保留为孤儿，由读时校验（`EvaluateStatus` 返回 `orphan`）检测。**这是一个需要验证的边界**：实现 Task 10 时必须测试"删参数后模板值变 orphan"场景。

- [ ] **Step 5: 创建 EquipmentConfiguration.cs**

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("equipments");
        builder.HasKey(e => e.Id);

        builder.Property(e => e.Id).HasColumnName("id");
        builder.Property(e => e.Code).HasColumnName("code").HasMaxLength(50).IsRequired();
        builder.Property(e => e.Name).HasColumnName("name").HasMaxLength(50).IsRequired();
        builder.Property(e => e.EquipmentTypeId).HasColumnName("equipment_type_id");
        builder.Property(e => e.Specification).HasColumnName("specification").HasMaxLength(200);
        builder.Property(e => e.Supplier).HasColumnName("supplier").HasMaxLength(100);
        builder.Property(e => e.Location).HasColumnName("location").HasMaxLength(100);
        builder.Property(e => e.Status).HasColumnName("status").HasConversion<int>().IsRequired();
        builder.Property(e => e.PurchaseDate).HasColumnName("purchase_date");
        builder.Property(e => e.WarrantyExpiry).HasColumnName("warranty_expiry");
        builder.Property(e => e.Remark).HasColumnName("remark").HasMaxLength(500);
        builder.Property(e => e.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(e => e.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(e => e.IsDeleted).HasColumnName("is_deleted").IsRequired();
        builder.Property(e => e.CreatedAt).HasColumnName("created_at");
        builder.Property(e => e.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(e => e.Code).HasDatabaseName("ux_equipments_code").IsUnique();
        builder.HasIndex(e => e.Name).HasDatabaseName("ux_equipments_name").IsUnique();

        // 设备类型 FK：Restrict，删类型前需校验无设备引用（应用层拦截）
        builder.HasOne<EquipmentType>()
            .WithMany()
            .HasForeignKey(e => e.EquipmentTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        // 软删除全局过滤器
        builder.HasQueryFilter(e => !e.IsDeleted);
    }
}
```

- [ ] **Step 6: 在 OneCupDbContext.cs 追加 5 个 DbSet**

在 `Process` DbSet 声明（约 line 41）之后追加：

```csharp
    // ===== Equipment 模块（feat/equipment-mgmt）=====
    public DbSet<EquipmentType> EquipmentTypes => Set<EquipmentType>();
    public DbSet<EquipmentTypeParameter> EquipmentTypeParameters => Set<EquipmentTypeParameter>();
    public DbSet<EquipmentTemplate> EquipmentTemplates => Set<EquipmentTemplate>();
    public DbSet<EquipmentTemplateValue> EquipmentTemplateValues => Set<EquipmentTemplateValue>();
    public DbSet<Equipment> Equipments => Set<Equipment>();
```

- [ ] **Step 7: 编译验证**

Run: `dotnet build backend/src/OneCup.Infrastructure/OneCup.Infrastructure.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 8: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Persistence/Configurations/Equipment*.cs backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs
git commit -m "feat(equipment): 新增 5 个 EF 配置 + DbSet 注册"
```

---

## Task 4: Seed 数据 + 编号常量

**Files:**
- Modify: `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs`（追加常量）
- Modify: `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`（Seed 方法追加块）
- Modify: `backend/src/OneCup.Application/Common/NumberTargetTypes.cs`（追加常量）

**Interfaces:**
- Produces: `PermEquipmentTypeRead/Create/Update/Delete` Guid 常量、`TargetTypeEquipmentType` Guid 常量、`NumberTargetTypes.EquipmentType` 字符串常量、Seed 的权限/targetType 行

- [ ] **Step 1: 在 SeedData.cs 追加常量**

在 `SeedData.cs` 文件末尾的 `TargetTypeProcess` 声明（约 line 77）之后、类的闭合 `}` 之前追加：

```csharp

    // === Equipment 模块（feat/equipment-mgmt）===
    // 设备实例 Equipment 权限码已存在: PermEquipmentRead/Create/Update/Delete (...0309-030c)
    // 设备实例 targetType 已存在: TargetTypeEquipment (...0203, code="equipment")
    // 以下为设备类型 EquipmentType 新增:
    public static readonly Guid PermEquipmentTypeRead   = Guid.Parse("00000000-0000-0000-0000-00000000032f");
    public static readonly Guid PermEquipmentTypeCreate = Guid.Parse("00000000-0000-0000-0000-000000000330");
    public static readonly Guid PermEquipmentTypeUpdate = Guid.Parse("00000000-0000-0000-0000-000000000331");
    public static readonly Guid PermEquipmentTypeDelete = Guid.Parse("00000000-0000-0000-0000-000000000332");
    public static readonly Guid TargetTypeEquipmentType = Guid.Parse("00000000-0000-0000-0000-000000000208");
```

- [ ] **Step 2: 在 OneCupDbContext.cs Seed() 方法的权限 HasData 块追加 EquipmentType 权限**

在 `PermProcessDelete` 那行（约 line 142，权限 HasData 块的末尾几行）之后追加（在闭合 `);` 之前）：

```csharp
            new Permission { Id = SeedData.PermEquipmentTypeRead,   Code = "equipment-type:read",   Name = "查看设备类型", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermEquipmentTypeCreate, Code = "equipment-type:create", Name = "录入设备类型", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermEquipmentTypeUpdate, Code = "equipment-type:update", Name = "编辑设备类型", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermEquipmentTypeDelete, Code = "equipment-type:delete", Name = "删除设备类型", CreatedAt = SeedTimestamp },
```

- [ ] **Step 3: 在 developerPerms 数组追加设备权限**

找到 `developerPerms` 数组定义（约 line 175-181），在 `SeedData.PermProcessRead` 之后追加：

```csharp
            SeedData.PermEquipmentRead,
            SeedData.PermEquipmentTypeRead,
```

- [ ] **Step 4: 在 NumberingTargetType HasData 块追加 EquipmentType 类型**

在 `TargetTypeProcess` 那行（约 line 198）之后追加（在闭合 `);` 之前）：

```csharp
            new NumberingTargetType { Id = SeedData.TargetTypeEquipmentType, Code = "equipment-type", NameZh = "设备类型", NameEn = "EquipmentType", SortOrder = 8, IsActive = true, CreatedAt = SeedTimestamp },
```

- [ ] **Step 5: 在 NumberTargetTypes.cs 追加常量**

在 `NumberTargetTypes.cs` 的 `Process` 常量之后追加：

```csharp
    public const string EquipmentType = "equipment-type";
```

- [ ] **Step 6: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 7: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Persistence/SeedData.cs backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs backend/src/OneCup.Application/Common/NumberTargetTypes.cs
git commit -m "feat(equipment): 追加 EquipmentType 权限/编号类型 seed + NumberTargetTypes 常量"
```

---

## Task 5: 生成 EF 迁移

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Migrations/<timestamp>_AddEquipmentModule.cs`（由 CLI 生成）

**Interfaces:**
- Consumes: Task 3 的 EF 配置 + Task 4 的 seed 数据
- Produces: `AddEquipmentModule` 迁移文件，包含 5 张表创建 + seed 数据 InsertData

- [ ] **Step 1: 生成迁移**

Run:
```bash
cd backend/src/OneCup.Infrastructure
dotnet ef migrations add AddEquipmentModule --startup-project ../OneCup.Api
```
Expected: 生成 `Migrations/<timestamp>_AddEquipmentModule.cs` 和 `.Designer.cs`，`ModelSnapshot.cs` 更新

- [ ] **Step 2: 检查生成的迁移文件**

打开生成的 `<timestamp>_AddEquipmentModule.cs`，验证：
- 5 张表（`equipment_types`、`equipment_type_parameters`、`equipment_templates`、`equipment_template_values`、`equipments`）按 FK 依赖顺序建表
- 列名都是 snake_case
- 唯一索引名带 `ux_` 前缀
- `InsertData` 包含 4 条 EquipmentType 权限 + 1 条 NumberingTargetType + 2 条 role_permissions（developer 角色）
- `equipments` 表有 `is_deleted` 列

如果生成的迁移与预期不符（如列名未 snake_case、FK 方向错误），检查 Task 3 的 EF 配置，修正后重新生成（先 `dotnet ef migrations remove`）。

- [ ] **Step 3: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 4: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Migrations/
git commit -m "feat(equipment): 生成 AddEquipmentModule 迁移"
```

---

## Task 6: DTOs

**Files:**
- Create: `backend/src/OneCup.Application/Dtos/System/EquipmentDtos.cs`

**Interfaces:**
- Consumes: Task 1 的枚举、Task 2 的实体（用于详情 DTO 映射参考）
- Produces: 所有设备模块的请求/响应 DTO 类，供 Task 7-9 服务层和 Task 10-12 控制器引用

- [ ] **Step 1: 创建 EquipmentDtos.cs**

```csharp
using OneCup.Domain.Enums;

namespace OneCup.Application.Dtos.System;

// ═══════════════════════════════════════════
// 设备类型（EquipmentType）
// ═══════════════════════════════════════════

/// <summary>设备类型列表项。</summary>
public class EquipmentTypeListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int ParameterCount { get; set; }
    public int TemplateCount { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>设备类型详情（含参数定义 + 模板摘要）。</summary>
public class EquipmentTypeDto : EquipmentTypeListItemDto
{
    public string? Remark { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<EquipmentTypeParameterDto> Parameters { get; set; } = new();
    public List<EquipmentTemplateSummaryDto> Templates { get; set; } = new();
}

/// <summary>参数定义（详情展示）。</summary>
public class EquipmentTypeParameterDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ParameterValueType ValueType { get; set; }
    public Guid? UnitId { get; set; }
    public string? UnitSymbol { get; set; }
    public string? MinValue { get; set; }
    public string? MaxValue { get; set; }
    public int? Precision { get; set; }
    public List<string>? Options { get; set; }
    public bool Required { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

/// <summary>模板摘要（类型详情内嵌展示）。</summary>
public class EquipmentTemplateSummaryDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string? Status { get; set; }   // valid/invalid/orphan（读时校验）
    public int SortOrder { get; set; }
}

/// <summary>新建设备类型请求。参数定义整表提交。</summary>
public class CreateEquipmentTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    /// <summary>可选；编号规则要求分类码时必填，由引擎强校验。</summary>
    public string? CategoryCode { get; set; }
    public List<ParameterDefinitionDto> Parameters { get; set; } = new();
}

/// <summary>编辑设备类型请求。</summary>
public class UpdateEquipmentTypeRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    public List<ParameterDefinitionDto> Parameters { get; set; } = new();
}

/// <summary>
/// 参数定义提交项。Id=null=新增，Id 有值=更新；未出现在数组里的存量 Id=删除。
/// </summary>
public class ParameterDefinitionDto
{
    public Guid? Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public ParameterValueType ValueType { get; set; }
    public Guid? UnitId { get; set; }
    public string? MinValue { get; set; }
    public string? MaxValue { get; set; }
    public int? Precision { get; set; }
    public List<string>? Options { get; set; }
    public bool Required { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

// ═══════════════════════════════════════════
// 运行模板（EquipmentTemplate）
// ═══════════════════════════════════════════

/// <summary>模板列表项。</summary>
public class EquipmentTemplateListItemDto
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public Guid ProcessId { get; set; }
    public string ProcessName { get; set; } = string.Empty;
    public string? Status { get; set; }   // valid/invalid/orphan
    public string? StatusMessage { get; set; }
    public int SortOrder { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>模板详情（含参数值 + 校验状态）。</summary>
public class EquipmentTemplateDto : EquipmentTemplateListItemDto
{
    public string? Remark { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public List<EquipmentTemplateValueDto> Values { get; set; } = new();
}

/// <summary>模板参数值（详情返回，带校验状态）。</summary>
public class EquipmentTemplateValueDto
{
    public Guid ParameterId { get; set; }
    public string ParameterName { get; set; } = string.Empty;
    public ParameterValueType ValueType { get; set; }
    public string? UnitSymbol { get; set; }
    public string? Value { get; set; }
    /// <summary>valid / invalid / orphan</summary>
    public string Status { get; set; } = "valid";
    public string? StatusMessage { get; set; }
}

/// <summary>新建模板请求。值整表提交。</summary>
public class CreateEquipmentTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid ProcessId { get; set; }
    public string? Remark { get; set; }
    public int SortOrder { get; set; } = 0;
    public List<TemplateValueDto> Values { get; set; } = new();
}

/// <summary>编辑模板请求。</summary>
public class UpdateEquipmentTemplateRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid ProcessId { get; set; }
    public string? Remark { get; set; }
    public int SortOrder { get; set; } = 0;
    public List<TemplateValueDto> Values { get; set; } = new();
}

/// <summary>模板值提交项。</summary>
public class TemplateValueDto
{
    public Guid ParameterId { get; set; }
    public string? Value { get; set; }
}

// ═══════════════════════════════════════════
// 设备实例（Equipment）
// ═══════════════════════════════════════════

/// <summary>设备列表项（扁平投影，不含参数/模板）。</summary>
public class EquipmentListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public Guid EquipmentTypeId { get; set; }
    public string EquipmentTypeName { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public string? Supplier { get; set; }
    public string? Location { get; set; }
    public EquipmentStatus Status { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>设备详情。</summary>
public class EquipmentDto : EquipmentListItemDto
{
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyExpiry { get; set; }
    public string? Remark { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

/// <summary>新建设备请求。Code 不在此处——由系统在事务内生成。</summary>
public class CreateEquipmentRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid EquipmentTypeId { get; set; }
    public string? Specification { get; set; }
    public string? Supplier { get; set; }
    public string? Location { get; set; }
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Running;
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyExpiry { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
    /// <summary>可选；编号规则要求分类码时必填。</summary>
    public string? CategoryCode { get; set; }
}

/// <summary>编辑设备请求。</summary>
public class UpdateEquipmentRequest
{
    public string Name { get; set; } = string.Empty;
    public Guid EquipmentTypeId { get; set; }
    public string? Specification { get; set; }
    public string? Supplier { get; set; }
    public string? Location { get; set; }
    public EquipmentStatus Status { get; set; } = EquipmentStatus.Running;
    public DateOnly? PurchaseDate { get; set; }
    public DateOnly? WarrantyExpiry { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; } = 0;
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build backend/src/OneCup.Application/OneCup.Application.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Application/Dtos/System/EquipmentDtos.cs
git commit -m "feat(equipment): 新增设备模块全部 DTO"
```

---

## Task 7: 校验器（Validators）

**Files:**
- Create: `backend/src/OneCup.Application/Validators/System/CreateEquipmentTypeRequestValidator.cs`
- Create: `backend/src/OneCup.Application/Validators/System/UpdateEquipmentTypeRequestValidator.cs`
- Create: `backend/src/OneCup.Application/Validators/System/CreateEquipmentTemplateRequestValidator.cs`
- Create: `backend/src/OneCup.Application/Validators/System/UpdateEquipmentTemplateRequestValidator.cs`
- Create: `backend/src/OneCup.Application/Validators/System/CreateEquipmentRequestValidator.cs`
- Create: `backend/src/OneCup.Application/Validators/System/UpdateEquipmentRequestValidator.cs`

**Interfaces:**
- Consumes: Task 6 的 DTO 类
- Produces: 6 个校验器，供 Task 8-10 服务层 `EnsureValidAsync` 调用

参考模式：`CreateMaterialRequestValidator.cs`（`.NotEmpty().MaximumLength(N)` + `.When()` 可选字段）

- [ ] **Step 1: 创建 CreateEquipmentTypeRequestValidator.cs**

```csharp
using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>新建设备类型请求校验。</summary>
public class CreateEquipmentTypeRequestValidator : AbstractValidator<CreateEquipmentTypeRequest>
{
    public CreateEquipmentTypeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleForEach(x => x.Parameters).SetValidator(new ParameterDefinitionDtoValidator());
    }
}

/// <summary>参数定义项校验（Create/Update 共用）。</summary>
public class ParameterDefinitionDtoValidator : AbstractValidator<ParameterDefinitionDto>
{
    public ParameterDefinitionDtoValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.MinValue).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.MinValue));
        RuleFor(x => x.MaxValue).MaximumLength(50).When(x => !string.IsNullOrEmpty(x.MaxValue));
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
```

- [ ] **Step 2: 创建 UpdateEquipmentTypeRequestValidator.cs**

```csharp
using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>编辑设备类型请求校验。</summary>
public class UpdateEquipmentTypeRequestValidator : AbstractValidator<UpdateEquipmentTypeRequest>
{
    public UpdateEquipmentTypeRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
        RuleForEach(x => x.Parameters).SetValidator(new ParameterDefinitionDtoValidator());
    }
}
```

- [ ] **Step 3: 创建 CreateEquipmentTemplateRequestValidator.cs**

```csharp
using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>新建模板请求校验。参数值合法性由服务层强校验（依赖参数定义，跨实体）。</summary>
public class CreateEquipmentTemplateRequestValidator : AbstractValidator<CreateEquipmentTemplateRequest>
{
    public CreateEquipmentTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ProcessId).NotEmpty();
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
```

- [ ] **Step 4: 创建 UpdateEquipmentTemplateRequestValidator.cs**

```csharp
using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>编辑模板请求校验。</summary>
public class UpdateEquipmentTemplateRequestValidator : AbstractValidator<UpdateEquipmentTemplateRequest>
{
    public UpdateEquipmentTemplateRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.ProcessId).NotEmpty();
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
```

- [ ] **Step 5: 创建 CreateEquipmentRequestValidator.cs**

```csharp
using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>新建设备请求校验。</summary>
public class CreateEquipmentRequestValidator : AbstractValidator<CreateEquipmentRequest>
{
    public CreateEquipmentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.EquipmentTypeId).NotEmpty();
        RuleFor(x => x.Specification).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Specification));
        RuleFor(x => x.Supplier).MaximumLength(100).When(x => !string.IsNullOrEmpty(x.Supplier));
        RuleFor(x => x.Location).MaximumLength(100).When(x => !string.IsNullOrEmpty(x.Location));
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
```

- [ ] **Step 6: 创建 UpdateEquipmentRequestValidator.cs**

```csharp
using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>编辑设备请求校验。</summary>
public class UpdateEquipmentRequestValidator : AbstractValidator<UpdateEquipmentRequest>
{
    public UpdateEquipmentRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(50);
        RuleFor(x => x.EquipmentTypeId).NotEmpty();
        RuleFor(x => x.Specification).MaximumLength(200).When(x => !string.IsNullOrEmpty(x.Specification));
        RuleFor(x => x.Supplier).MaximumLength(100).When(x => !string.IsNullOrEmpty(x.Supplier));
        RuleFor(x => x.Location).MaximumLength(100).When(x => !string.IsNullOrEmpty(x.Location));
        RuleFor(x => x.Remark).MaximumLength(500).When(x => !string.IsNullOrEmpty(x.Remark));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
```

- [ ] **Step 7: 编译验证**

Run: `dotnet build backend/src/OneCup.Application/OneCup.Application.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 8: Commit**

```bash
git add backend/src/OneCup.Application/Validators/System/*Equipment*.cs
git commit -m "feat(equipment): 新增 6 个 FluentValidation 校验器"
```

---

## Task 8: 查询规范（Specifications）

**Files:**
- Create: `backend/src/OneCup.Application/Specifications/EquipmentTypeSpecs.cs`
- Create: `backend/src/OneCup.Application/Specifications/EquipmentSpecs.cs`

**Interfaces:**
- Consumes: Task 2 实体、`Specification<T>` 基类（`Application/Specifications/Specification.cs`）
- Produces: 设备类型和设备的查询规范类，供 Task 9-11 服务层引用

参考模式：`MaterialSpecs.cs`（FilterSpec/PagedSpec/ActiveSpec/ByIdSpec/ByNameSpec 五件套）、`ProcessByNameSpec`（nullable 字段的唯一性校验写法）

- [ ] **Step 1: 创建 EquipmentTypeSpecs.cs**

```csharp
using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>设备类型过滤规格（仅过滤，不含分页）。用于 CountAsync。</summary>
public class EquipmentTypeFilterSpec : Specification<EquipmentType>
{
    public EquipmentTypeFilterSpec(string? keyword, string? code, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(e =>
            (kw == null || e.Code.Contains(kw) || e.Name.Contains(kw)) &&
            (string.IsNullOrEmpty(code) || e.Code.Contains(code)) &&
            (isActive == null || e.IsActive == isActive.Value));
    }
}

/// <summary>设备类型分页查询（含过滤，按 SortOrder 升序）。</summary>
public class EquipmentTypePagedSpec : Specification<EquipmentType>
{
    public EquipmentTypePagedSpec(string? keyword, string? code, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(e =>
            (kw == null || e.Code.Contains(kw) || e.Name.Contains(kw)) &&
            (string.IsNullOrEmpty(code) || e.Code.Contains(code)) &&
            (isActive == null || e.IsActive == isActive.Value));
        ApplyOrderBy(e => e.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>设备类型全部启用项（前端下拉用，按 SortOrder 升序）。</summary>
public class EquipmentTypeActiveSpec : Specification<EquipmentType>
{
    public EquipmentTypeActiveSpec()
    {
        ApplyCriteria(e => e.IsActive);
        ApplyOrderBy(e => e.SortOrder);
    }
}

/// <summary>按 Id 查询设备类型（tracked）。</summary>
public class EquipmentTypeByIdSpec : Specification<EquipmentType>
{
    public EquipmentTypeByIdSpec(Guid id)
    {
        ApplyCriteria(e => e.Id == id);
        ApplyInclude(nameof(EquipmentType.Parameters));
        ApplyInclude(nameof(EquipmentType.Templates));
    }
}

/// <summary>名称唯一性校验。</summary>
public class EquipmentTypeByNameSpec : Specification<EquipmentType>
{
    public EquipmentTypeByNameSpec(string name, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(e => e.Name == name && (exclude == null || e.Id != exclude.Value));
    }
}
```

- [ ] **Step 2: 创建 EquipmentSpecs.cs**

```csharp
using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>设备过滤规格（仅过滤，不含分页）。用于 CountAsync。</summary>
public class EquipmentFilterSpec : Specification<Equipment>
{
    public EquipmentFilterSpec(string? keyword, string? code, Guid? typeId, bool? isActive, Domain.Enums.EquipmentStatus? status)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(e =>
            (kw == null || e.Code.Contains(kw) || e.Name.Contains(kw)) &&
            (string.IsNullOrEmpty(code) || e.Code.Contains(code)) &&
            (typeId == null || e.EquipmentTypeId == typeId.Value) &&
            (isActive == null || e.IsActive == isActive.Value) &&
            (status == null || e.Status == status.Value));
    }
}

/// <summary>设备分页查询（含过滤，按 SortOrder 升序）。</summary>
public class EquipmentPagedSpec : Specification<Equipment>
{
    public EquipmentPagedSpec(string? keyword, string? code, Guid? typeId, bool? isActive, Domain.Enums.EquipmentStatus? status, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(e =>
            (kw == null || e.Code.Contains(kw) || e.Name.Contains(kw)) &&
            (string.IsNullOrEmpty(code) || e.Code.Contains(code)) &&
            (typeId == null || e.EquipmentTypeId == typeId.Value) &&
            (isActive == null || e.IsActive == isActive.Value) &&
            (status == null || e.Status == status.Value));
        ApplyOrderBy(e => e.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>按 Id 查询设备（tracked）。</summary>
public class EquipmentByIdSpec : Specification<Equipment>
{
    public EquipmentByIdSpec(Guid id) => ApplyCriteria(e => e.Id == id);
}

/// <summary>名称唯一性校验（绕过软删除过滤器）。</summary>
public class EquipmentByNameSpec : Specification<Equipment>
{
    public EquipmentByNameSpec(string name, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(e => e.Name == name && (exclude == null || e.Id != exclude.Value));
    }
}

/// <summary>按设备类型查询（用于删除类型前的引用校验）。</summary>
public class EquipmentByTypeSpec : Specification<Equipment>
{
    public EquipmentByTypeSpec(Guid typeId) => ApplyCriteria(e => e.EquipmentTypeId == typeId);
}
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build backend/src/OneCup.Application/OneCup.Application.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 4: Commit**

```bash
git add backend/src/OneCup.Application/Specifications/Equipment*.cs
git commit -m "feat(equipment): 新增设备类型和设备的查询规范"
```

---

## Task 9: 参数值校验服务（核心校验逻辑）

**Files:**
- Create: `backend/src/OneCup.Application/Services/EquipmentParameterValueValidator.cs`

**Interfaces:**
- Consumes: Task 6 的 `EquipmentTemplateValueDto`、`TemplateValueDto`；Task 2 的 `EquipmentTypeParameter` 实体；Task 1 的 `ParameterValueType` 枚举
- Produces: `EquipmentParameterValueValidator` 静态类，提供 `ValidateValue`（单值强校验，保存时用）和 `EvaluateStatus`（读时状态判定），供 Task 10/11 服务层调用

> 这是本模块校验逻辑的核心，独立成一个无依赖的纯函数类，便于单测。spec §3.2/3.3 的全部规则都在这里。

- [ ] **Step 1: 创建 EquipmentParameterValueValidator.cs**

```csharp
using OneCup.Application.Dtos.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 设备模板参数值校验器（纯函数，无副作用，便于单测）。
/// 提供两种模式：
/// - ValidateValue: 强校验（保存时用），失败抛 DomainException
/// - EvaluateStatus: 读时状态判定（valid/invalid/orphan），不抛异常
/// 对应 spec §3.2/3.3。
/// </summary>
public static class EquipmentParameterValueValidator
{
    /// <summary>
    /// 强校验单个值（保存时用）。失败抛 DomainException。
    /// </summary>
    /// <param name="param">参数定义；null 表示参数已删除（孤儿），应调用方先过滤</param>
    /// <param name="value">提交的值</param>
    public static void ValidateValue(EquipmentTypeParameter? param, string? value)
    {
        if (param is null)
        {
            throw new DomainException("参数定义不存在，请清除该值");
        }

        // 必填校验
        if (param.Required && string.IsNullOrWhiteSpace(value))
        {
            throw new DomainException($"参数「{param.Name}」为必填项");
        }

        // 空值且非必填 → 通过
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        switch (param.ValueType)
        {
            case ParameterValueType.Number:
                ValidateNumber(param, value);
                break;
            case ParameterValueType.Enum:
                ValidateEnum(param, value);
                break;
            case ParameterValueType.Text:
                // 文本类型无额外约束（长度由 DTO 校验器兜底）
                break;
        }
    }

    /// <summary>
    /// 读时状态判定（不抛异常）。用于详情/列表返回 status 字段。
    /// </summary>
    /// <param name="param">参数定义；null 表示孤儿（参数已删除）</param>
    /// <param name="value">当前值</param>
    /// <returns>(status, statusMessage)，status: valid/invalid/orphan</returns>
    public static (string Status, string? Message) EvaluateStatus(EquipmentTypeParameter? param, string? value)
    {
        // 孤儿：参数定义已删除
        if (param is null)
        {
            return ("orphan", "参数已删除，请清除该值");
        }

        // 必填且空
        if (param.Required && string.IsNullOrWhiteSpace(value))
        {
            return ("invalid", $"参数「{param.Name}」为必填项");
        }

        // 空值且非必填 → 有效
        if (string.IsNullOrWhiteSpace(value))
        {
            return ("valid", null);
        }

        return param.ValueType switch
        {
            ParameterValueType.Number => EvaluateNumber(param, value),
            ParameterValueType.Enum   => EvaluateEnum(param, value),
            ParameterValueType.Text   => ("valid", null),
            _ => ("valid", null),
        };
    }

    // ── 数值校验 ──

    private static void ValidateNumber(EquipmentTypeParameter param, string value)
    {
        if (!decimal.TryParse(value, out var num))
        {
            throw new DomainException($"参数「{param.Name}」的值「{value}」不是有效数值");
        }

        if (decimal.TryParse(param.MinValue, out var min) && num < min)
        {
            throw new DomainException($"参数「{param.Name}」的值「{value}」低于最小值 {min}");
        }

        if (decimal.TryParse(param.MaxValue, out var max) && num > max)
        {
            throw new DomainException($"参数「{param.Name}」的值「{value}」超出最大值 {max}");
        }

        if (param.Precision is int precision && precision >= 0)
        {
            var decimals = value.Contains('.') ? value.SkipWhile(c => c != '.').Skip(1).Count() : 0;
            if (decimals > precision)
            {
                throw new DomainException($"参数「{param.Name}」的值「{value}」小数位超过 {precision} 位");
            }
        }
    }

    private static (string, string?) EvaluateNumber(EquipmentTypeParameter param, string value)
    {
        if (!decimal.TryParse(value, out var num))
            return ("invalid", $"参数「{param.Name}」的值「{value}」不是有效数值");

        if (decimal.TryParse(param.MinValue, out var min) && num < min)
            return ("invalid", $"参数「{param.Name}」的值「{value}」低于最小值 {min}");

        if (decimal.TryParse(param.MaxValue, out var max) && num > max)
            return ("invalid", $"参数「{param.Name}」的值「{value}」超出最大值 {max}");

        if (param.Precision is int precision && precision >= 0)
        {
            var decimals = value.Contains('.') ? value.SkipWhile(c => c != '.').Skip(1).Count() : 0;
            if (decimals > precision)
                return ("invalid", $"参数「{param.Name}」的值「{value}」小数位超过 {precision} 位");
        }

        return ("valid", null);
    }

    // ── 枚举校验 ──

    private static void ValidateEnum(EquipmentTypeParameter param, string value)
    {
        var options = ParseOptions(param.Options);
        if (options.Count == 0)
        {
            throw new DomainException($"参数「{param.Name}」未配置可选值");
        }
        if (!options.Contains(value))
        {
            throw new DomainException($"参数「{param.Name}」的值「{value}」不在可选值列表内");
        }
    }

    private static (string, string?) EvaluateEnum(EquipmentTypeParameter param, string value)
    {
        var options = ParseOptions(param.Options);
        if (options.Count == 0 || !options.Contains(value))
            return ("invalid", $"参数「{param.Name}」的值「{value}」不是有效选项");
        return ("valid", null);
    }

    /// <summary>解析 Options JSON 数组字符串为列表。</summary>
    public static List<string> ParseOptions(string? optionsJson)
    {
        if (string.IsNullOrWhiteSpace(optionsJson))
            return new();

        // 简单解析：["a","b","c"] → ["a","b","c"]
        // 用 System.Text.Json 反序列化
        try
        {
            return System.Text.Json.JsonSerializer.Deserialize<List<string>>(optionsJson) ?? new();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>序列化选项列表为 JSON 字符串（实体存储用）。</summary>
    public static string? SerializeOptions(List<string>? options)
    {
        if (options is null || options.Count == 0)
            return null;
        return System.Text.Json.JsonSerializer.Serialize(options);
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build backend/src/OneCup.Application/OneCup.Application.csproj`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Application/Services/EquipmentParameterValueValidator.cs
git commit -m "feat(equipment): 新增参数值校验器（强校验 + 读时状态判定）"
```

---

## Task 10: 设备类型服务（EquipmentTypeService）

**Files:**
- Create: `backend/src/OneCup.Application/Interfaces/IEquipmentTypeService.cs`
- Create: `backend/src/OneCup.Application/Services/EquipmentTypeService.cs`
- Test: `backend/tests/OneCup.UnitTests/Equipment/EquipmentTypeServiceTests.cs`
- Test: `backend/tests/OneCup.UnitTests/Equipment/EquipmentParameterValueValidatorTests.cs`

**Interfaces:**
- Consumes: Task 2 实体、Task 4 NumberTargetTypes、Task 6 DTO、Task 7 校验器、Task 8 Specs、Task 9 `EquipmentParameterValueValidator`（Options 序列化用）、现有 `INumberingService`/`IRepository<T>`/`IUnitOfWork`/`EnsureValidAsync`
- Produces: `IEquipmentTypeService` 接口 + 实现，供 Task 13 控制器和前端调用；DTO 映射方法（详情返回含 Parameters + Templates 摘要）

- [ ] **Step 1: 写参数值校验器的失败测试（Task 9 的单测，先于此处写）**

创建 `backend/tests/OneCup.UnitTests/Equipment/EquipmentParameterValueValidatorTests.cs`：

```csharp
using OneCup.Application.Services;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using Xunit;

namespace OneCup.UnitTests.Equipment;

public class EquipmentParameterValueValidatorTests
{
    private static EquipmentTypeParameter Param(ParameterValueType type,
        string? min = null, string? max = null, int? precision = null,
        string? options = null, bool required = false) => new()
    {
        Name = "测试参数",
        ValueType = type,
        MinValue = min,
        MaxValue = max,
        Precision = precision,
        Options = options,
        Required = required,
    };

    // ── 数值校验 ──

    [Fact]
    public void ValidateValue_Number_InRange_Passes()
    {
        var p = Param(ParameterValueType.Number, "80", "200");
        EquipmentParameterValueValidator.ValidateValue(p, "120");
        // 不抛异常即通过
    }

    [Fact]
    public void ValidateValue_Number_AboveMax_Throws()
    {
        var p = Param(ParameterValueType.Number, "80", "200");
        Assert.Throws<DomainException>(() => EquipmentParameterValueValidator.ValidateValue(p, "250"));
    }

    [Fact]
    public void ValidateValue_Number_BelowMin_Throws()
    {
        var p = Param(ParameterValueType.Number, "80", "200");
        Assert.Throws<DomainException>(() => EquipmentParameterValueValidator.ValidateValue(p, "50"));
    }

    [Fact]
    public void ValidateValue_Number_NotNumeric_Throws()
    {
        var p = Param(ParameterValueType.Number);
        Assert.Throws<DomainException>(() => EquipmentParameterValueValidator.ValidateValue(p, "abc"));
    }

    [Fact]
    public void ValidateValue_Number_PrecisionExceeded_Throws()
    {
        var p = Param(ParameterValueType.Number, precision: 1);
        Assert.Throws<DomainException>(() => EquipmentParameterValueValidator.ValidateValue(p, "1.234"));
    }

    [Fact]
    public void ValidateValue_Number_NoRange_AnyNumberPasses()
    {
        var p = Param(ParameterValueType.Number);
        EquipmentParameterValueValidator.ValidateValue(p, "99999");
    }

    // ── 枚举校验 ──

    [Fact]
    public void ValidateValue_Enum_InOptions_Passes()
    {
        var p = Param(ParameterValueType.Enum, options: """["低","中","高"]""");
        EquipmentParameterValueValidator.ValidateValue(p, "中");
    }

    [Fact]
    public void ValidateValue_Enum_NotInOptions_Throws()
    {
        var p = Param(ParameterValueType.Enum, options: """["低","中","高"]""");
        Assert.Throws<DomainException>(() => EquipmentParameterValueValidator.ValidateValue(p, "极高"));
    }

    // ── 通用校验 ──

    [Fact]
    public void ValidateValue_RequiredEmpty_Throws()
    {
        var p = Param(ParameterValueType.Text, required: true);
        Assert.Throws<DomainException>(() => EquipmentParameterValueValidator.ValidateValue(p, ""));
    }

    [Fact]
    public void ValidateValue_NotRequiredEmpty_Passes()
    {
        var p = Param(ParameterValueType.Text, required: false);
        EquipmentParameterValueValidator.ValidateValue(p, "");
    }

    [Fact]
    public void EvaluateStatus_ParamNull_ReturnsOrphan()
    {
        var (status, _) = EquipmentParameterValueValidator.EvaluateStatus(null, "100");
        Assert.Equal("orphan", status);
    }

    [Fact]
    public void EvaluateStatus_NumberOutOfRange_ReturnsInvalid()
    {
        var p = Param(ParameterValueType.Number, "80", "200");
        var (status, _) = EquipmentParameterValueValidator.EvaluateStatus(p, "250");
        Assert.Equal("invalid", status);
    }

    [Fact]
    public void EvaluateStatus_ValidValue_ReturnsValid()
    {
        var p = Param(ParameterValueType.Number, "80", "200");
        var (status, _) = EquipmentParameterValueValidator.EvaluateStatus(p, "120");
        Assert.Equal("valid", status);
    }
}
```

- [ ] **Step 2: 运行校验器测试，验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~EquipmentParameterValueValidatorTests"`
Expected: 全部 PASS

- [ ] **Step 3: 创建 IEquipmentTypeService.cs 接口**

```csharp
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

public interface IEquipmentTypeService
{
    Task<PagedResult<EquipmentTypeListItemDto>> GetListAsync(
        string? keyword, string? code, bool? isActive, int page, int pageSize, CancellationToken ct = default);

    Task<EquipmentTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    /// <summary>获取启用类型列表（前端下拉用）。</summary>
    Task<List<EquipmentTypeListItemDto>> GetActiveAsync(CancellationToken ct = default);

    Task<EquipmentTypeDto> CreateAsync(CreateEquipmentTypeRequest request, CancellationToken ct = default);

    Task<EquipmentTypeDto> UpdateAsync(Guid id, UpdateEquipmentTypeRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 4: 创建 EquipmentTypeService.cs 实现**

```csharp
using FluentValidation;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 设备类型服务。
/// 参数定义随类型整表替换（PUT 时按 Id diff）。
/// 类型删除前校验：无设备引用 + 无模板。
/// 类型编号走编号引擎（事务内取号）。
/// </summary>
public class EquipmentTypeService : IEquipmentTypeService
{
    private readonly IRepository<EquipmentType> _types;
    private readonly IRepository<Equipment> _equipments;
    private readonly IUnitOfWork _uow;
    private readonly INumberingService _numbering;
    private readonly IValidator<CreateEquipmentTypeRequest> _createValidator;
    private readonly IValidator<UpdateEquipmentTypeRequest> _updateValidator;

    public EquipmentTypeService(
        IRepository<EquipmentType> types,
        IRepository<Equipment> equipments,
        IUnitOfWork uow,
        INumberingService numbering,
        IValidator<CreateEquipmentTypeRequest> createValidator,
        IValidator<UpdateEquipmentTypeRequest> updateValidator)
    {
        _types = types;
        _equipments = equipments;
        _uow = uow;
        _numbering = numbering;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PagedResult<EquipmentTypeListItemDto>> GetListAsync(
        string? keyword, string? code, bool? isActive, int page, int pageSize, CancellationToken ct = default)
    {
        var total = await _types.CountAsync(new EquipmentTypeFilterSpec(keyword, code, isActive), ct);
        var items = await _types.ListAsync(
            new EquipmentTypePagedSpec(keyword, code, isActive, page, pageSize), ct);

        return new PagedResult<EquipmentTypeListItemDto>
        {
            Items = items.Select(t => new EquipmentTypeListItemDto
            {
                Id = t.Id,
                Code = t.Code,
                Name = t.Name,
                ParameterCount = t.Parameters.Count,
                TemplateCount = t.Templates.Count,
                IsActive = t.IsActive,
                CreatedAt = t.CreatedAt,
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<EquipmentTypeDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var t = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(id), ct);
        if (t is null) return null;

        // 单位符号批量查（避免 N+1，这里类型参数量不大，逐个查可接受；如需优化可改 IQueryable 投影）
        return new EquipmentTypeDto
        {
            Id = t.Id,
            Code = t.Code,
            Name = t.Name,
            Remark = t.Remark,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
            UpdatedAt = t.UpdatedAt,
            ParameterCount = t.Parameters.Count,
            TemplateCount = t.Templates.Count,
            Parameters = t.Parameters.OrderBy(p => p.SortOrder).Select(p => new EquipmentTypeParameterDto
            {
                Id = p.Id,
                Name = p.Name,
                ValueType = p.ValueType,
                UnitId = p.UnitId,
                MinValue = p.MinValue,
                MaxValue = p.MaxValue,
                Precision = p.Precision,
                Options = EquipmentParameterValueValidator.ParseOptions(p.Options),
                Required = p.Required,
                SortOrder = p.SortOrder,
                Remark = p.Remark,
            }).ToList(),
            Templates = t.Templates.OrderBy(tl => tl.SortOrder).Select(tl => new EquipmentTemplateSummaryDto
            {
                Id = tl.Id,
                Name = tl.Name,
                ProcessId = tl.ProcessId,
                ProcessName = "",  // ProcessName 在 Controller 层或此处补查；为简化，先留空，后续可优化
                Status = "valid",  // 摘要不带逐值校验，详情才校验
                SortOrder = tl.SortOrder,
            }).ToList(),
        };
    }

    public async Task<List<EquipmentTypeListItemDto>> GetActiveAsync(CancellationToken ct = default)
    {
        var items = await _types.ListAsync(new EquipmentTypeActiveSpec(), ct);
        return items.Select(t => new EquipmentTypeListItemDto
        {
            Id = t.Id,
            Code = t.Code,
            Name = t.Name,
            IsActive = t.IsActive,
            CreatedAt = t.CreatedAt,
        }).ToList();
    }

    public async Task<EquipmentTypeDto> CreateAsync(CreateEquipmentTypeRequest request, CancellationToken ct = default)
    {
        await _createValidator.EnsureValidAsync(request, ct);

        if (await _types.AnyIgnoringFiltersAsync(new EquipmentTypeByNameSpec(request.Name), ct))
        {
            throw new DomainException($"设备类型名称「{request.Name}」已存在");
        }

        Guid createdId = Guid.Empty;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            var code = await _numbering.GenerateAsync(NumberTargetTypes.EquipmentType, request.CategoryCode, ct);
            var type = new EquipmentType
            {
                Code = code,
                Name = request.Name,
                Remark = request.Remark,
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
                Parameters = request.Parameters.Select(p => new EquipmentTypeParameter
                {
                    Name = p.Name,
                    ValueType = p.ValueType,
                    UnitId = p.UnitId,
                    MinValue = p.MinValue,
                    MaxValue = p.MaxValue,
                    Precision = p.Precision,
                    Options = EquipmentParameterValueValidator.SerializeOptions(p.Options),
                    Required = p.Required,
                    SortOrder = p.SortOrder,
                    Remark = p.Remark,
                }).ToList(),
            };
            await _types.AddAsync(type, ct);
            await _uow.SaveChangesAsync(ct);
            createdId = type.Id;
        }, ct);

        return await GetByIdAsync(createdId, ct) ?? throw new DomainException("设备类型创建失败");
    }

    public async Task<EquipmentTypeDto> UpdateAsync(Guid id, UpdateEquipmentTypeRequest request, CancellationToken ct = default)
    {
        await _updateValidator.EnsureValidAsync(request, ct);

        // GetByIdSpec 走 Include，加载 Parameters 子集合
        var type = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(id), ct)
            ?? throw new DomainException("设备类型不存在");

        if (await _types.AnyIgnoringFiltersAsync(new EquipmentTypeByNameSpec(request.Name, id), ct))
        {
            throw new DomainException($"设备类型名称「{request.Name}」已存在");
        }

        // 基础字段
        type.Name = request.Name;
        type.Remark = request.Remark;
        type.IsActive = request.IsActive;
        type.SortOrder = request.SortOrder;

        // 参数定义整表替换 diff
        SyncParameters(type, request.Parameters);

        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct) ?? throw new DomainException("设备类型更新失败");
    }

    /// <summary>
    /// 参数定义整表替换 diff：
    /// - request 中 Id=null → 新增
    /// - request 中 Id 有值且 type 中存在 → 更新
    /// - type 中存在但 request 未出现 → 删除
    /// </summary>
    private static void SyncParameters(EquipmentType type, List<ParameterDefinitionDto> requestParams)
    {
        var requestIds = requestParams.Where(p => p.Id.HasValue).Select(p => p.Id!.Value).ToHashSet();

        // 删除：type 中有但 request 未包含的
        var toRemove = type.Parameters.Where(p => !requestIds.Contains(p.Id)).ToList();
        foreach (var p in toRemove)
        {
            type.Parameters.Remove(p);
        }

        // 更新或新增
        foreach (var dto in requestParams)
        {
            if (dto.Id.HasValue)
            {
                var existing = type.Parameters.FirstOrDefault(p => p.Id == dto.Id.Value);
                if (existing is not null)
                {
                    existing.Name = dto.Name;
                    existing.ValueType = dto.ValueType;
                    existing.UnitId = dto.UnitId;
                    existing.MinValue = dto.MinValue;
                    existing.MaxValue = dto.MaxValue;
                    existing.Precision = dto.Precision;
                    existing.Options = EquipmentParameterValueValidator.SerializeOptions(dto.Options);
                    existing.Required = dto.Required;
                    existing.SortOrder = dto.SortOrder;
                    existing.Remark = dto.Remark;
                }
            }
            else
            {
                type.Parameters.Add(new EquipmentTypeParameter
                {
                    Name = dto.Name,
                    ValueType = dto.ValueType,
                    UnitId = dto.UnitId,
                    MinValue = dto.MinValue,
                    MaxValue = dto.MaxValue,
                    Precision = dto.Precision,
                    Options = EquipmentParameterValueValidator.SerializeOptions(dto.Options),
                    Required = dto.Required,
                    SortOrder = dto.SortOrder,
                    Remark = dto.Remark,
                });
            }
        }
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var type = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(id), ct)
            ?? throw new DomainException("设备类型不存在");

        // 校验：无设备引用
        if (await _equipments.AnyAsync(new EquipmentByTypeSpec(id), ct))
        {
            throw new DomainException("该设备类型下还有设备，无法删除");
        }

        // 校验：无模板
        if (type.Templates.Count > 0)
        {
            throw new DomainException("该设备类型下还有运行模板，无法删除");
        }

        await _types.Remove(type);
        await _uow.SaveChangesAsync(ct);
    }
}
```

- [ ] **Step 5: 写共享测试辅助 EquipmentTestHelper.cs**

> **重要**：三个服务测试文件（Task 10/11/12）共用同一命名空间 `OneCup.UnitTests.Equipment`，必须把 `FakeNumberingService` 提到共享文件，否则 CS0101 重复定义。

创建 `backend/tests/OneCup.UnitTests/Equipment/EquipmentTestHelper.cs`：

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
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
```

- [ ] **Step 6: 写 EquipmentTypeServiceTests.cs**

创建 `backend/tests/OneCup.UnitTests/Equipment/EquipmentTypeServiceTests.cs`：

```csharp
using OneCup.Application.Dtos.System;
using OneCup.Application.Services;
using OneCup.Application.Validators.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
using Xunit;

namespace OneCup.UnitTests.Equipment;

public class EquipmentTypeServiceTests
{
    private static (OneCupDbContext db, EquipmentTypeService svc, FakeNumberingService numbering) Setup()
    {
        var db = EquipmentTestHelper.CreateDb("eqtype");
        var numbering = new FakeNumberingService("EQT-");
        var svc = new EquipmentTypeService(
            new Repository<EquipmentType>(db),
            new Repository<Equipment>(db),
            new UnitOfWork(db),
            numbering,
            new CreateEquipmentTypeRequestValidator(),
            new UpdateEquipmentTypeRequestValidator());
        return (db, svc, numbering);
    }

    private static CreateEquipmentTypeRequest ValidCreate() => new()
    {
        Name = "定型机",
        Parameters = new()
        {
            new() { Name = "车速", ValueType = ParameterValueType.Number, MinValue = "80", MaxValue = "200", Required = true, SortOrder = 1 },
            new() { Name = "档位", ValueType = ParameterValueType.Enum, Options = new() { "低", "中", "高" }, Required = true, SortOrder = 2 },
        },
    };

    [Fact]
    public async Task CreateAsync_GeneratesCode_AndPersistsParameters()
    {
        var (_, svc, numbering) = Setup();
        numbering.NextCode = "EQT-0001";

        var dto = await svc.CreateAsync(ValidCreate());

        Assert.Equal("EQT-0001", dto.Code);
        Assert.Equal("定型机", dto.Name);
        Assert.Equal(2, dto.Parameters.Count);
        Assert.Equal("车速", dto.Parameters[0].Name);
        Assert.Equal(ParameterValueType.Number, dto.Parameters[0].ValueType);
        Assert.Equal("80", dto.Parameters[0].MinValue);
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_Throws()
    {
        var (_, svc, _) = Setup();
        await svc.CreateAsync(ValidCreate());

        await Assert.ThrowsAsync<DomainException>(() => svc.CreateAsync(ValidCreate()));
    }

    [Fact]
    public async Task UpdateAsync_SyncsParameters_AddUpdateDelete()
    {
        var (_, svc, _) = Setup();
        var created = await svc.CreateAsync(ValidCreate());

        // 删除"档位"，更新"车速"范围，新增"温度"
        var update = new UpdateEquipmentTypeRequest
        {
            Name = "定型机",
            Parameters = new()
            {
                new() { Id = created.Parameters[0].Id, Name = "车速", ValueType = ParameterValueType.Number, MinValue = "50", MaxValue = "150", Required = true, SortOrder = 1 },
                new() { Name = "温度", ValueType = ParameterValueType.Number, MinValue = "100", MaxValue = "220", Required = true, SortOrder = 2 },
            },
        };

        var updated = await svc.UpdateAsync(created.Id, update);

        Assert.Equal(2, updated.Parameters.Count);
        Assert.Equal("温度", updated.Parameters[1].Name);
        Assert.DoesNotContain(updated.Parameters, p => p.Name == "档位");
        Assert.Equal("50", updated.Parameters[0].MinValue);  // 更新生效
    }

    [Fact]
    public async Task DeleteAsync_WithEquipment_Throws()
    {
        var (db, svc, _) = Setup();
        var created = await svc.CreateAsync(ValidCreate());

        // 手动塞一台设备引用此类型
        db.Equipments.Add(new Equipment { Code = "EQ-001", Name = "1号机", EquipmentTypeId = created.Id });
        await db.SaveChangesAsync();

        await Assert.ThrowsAsync<DomainException>(() => svc.DeleteAsync(created.Id));
    }

    [Fact]
    public async Task DeleteAsync_NoReferences_Succeeds()
    {
        var (_, svc, _) = Setup();
        var created = await svc.CreateAsync(ValidCreate());

        await svc.DeleteAsync(created.Id);

        var found = await svc.GetByIdAsync(created.Id);
        Assert.Null(found);
    }
}
```

> **注意**：FakeNumberingService 已移到共享文件 `EquipmentTestHelper.cs`（Step 5），本文件不再内联定义。

- [ ] **Step 7: 运行测试，验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~EquipmentTypeServiceTests|FullyQualifiedName~EquipmentParameterValueValidatorTests"`
Expected: 全部 PASS

- [ ] **Step 8: Commit**

```bash
git add backend/src/OneCup.Application/Interfaces/IEquipmentTypeService.cs backend/src/OneCup.Application/Services/EquipmentTypeService.cs backend/tests/OneCup.UnitTests/Equipment/
git commit -m "feat(equipment): 设备类型服务 + 参数校验器单测 + 共享测试辅助"
```

---

## Task 11: 运行模板服务（EquipmentTemplateService）

**Files:**
- Create: `backend/src/OneCup.Application/Interfaces/IEquipmentTemplateService.cs`
- Create: `backend/src/OneCup.Application/Services/EquipmentTemplateService.cs`
- Test: `backend/tests/OneCup.UnitTests/Equipment/EquipmentTemplateServiceTests.cs`

**Interfaces:**
- Consumes: Task 2 实体、Task 6 DTO、Task 7 校验器、Task 9 `EquipmentParameterValueValidator`、现有 `IRepository<T>`/`IUnitOfWork`/`EnsureValidAsync`、`Process` 实体（查 ProcessName）
- Produces: `IEquipmentTemplateService` 接口 + 实现，供 Task 14 控制器调用

> 此服务实现 spec §3 的全部校验逻辑：创建/更新时强校验，读取时返回 status。模板按 EquipmentTypeId 路由，ProcessId 来自请求。

- [ ] **Step 1: 创建 IEquipmentTemplateService.cs 接口**

```csharp
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

public interface IEquipmentTemplateService
{
    Task<List<EquipmentTemplateListItemDto>> GetListAsync(Guid typeId, Guid? processId, CancellationToken ct = default);

    Task<EquipmentTemplateDto?> GetByIdAsync(Guid typeId, Guid id, CancellationToken ct = default);

    Task<EquipmentTemplateDto> CreateAsync(Guid typeId, CreateEquipmentTemplateRequest request, CancellationToken ct = default);

    Task<EquipmentTemplateDto> UpdateAsync(Guid typeId, Guid id, UpdateEquipmentTemplateRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid typeId, Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 2: 创建 EquipmentTemplateService.cs 实现**

```csharp
using FluentValidation;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 运行模板服务。
/// 创建/更新时强校验所有参数值（按 ValueType 分支）。
/// 读取时对每个值按当前参数定义实时校验，返回 status（valid/invalid/orphan）。
/// (TypeId, ProcessId, Name) 唯一。
/// </summary>
public class EquipmentTemplateService : IEquipmentTemplateService
{
    private readonly IRepository<EquipmentTemplate> _templates;
    private readonly IRepository<EquipmentType> _types;
    private readonly IRepository<Process> _processes;
    private readonly IUnitOfWork _uow;
    private readonly IValidator<CreateEquipmentTemplateRequest> _createValidator;
    private readonly IValidator<UpdateEquipmentTemplateRequest> _updateValidator;

    public EquipmentTemplateService(
        IRepository<EquipmentTemplate> templates,
        IRepository<EquipmentType> types,
        IRepository<Process> processes,
        IUnitOfWork uow,
        IValidator<CreateEquipmentTemplateRequest> createValidator,
        IValidator<UpdateEquipmentTemplateRequest> updateValidator)
    {
        _templates = templates;
        _types = types;
        _processes = processes;
        _uow = uow;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<List<EquipmentTemplateListItemDto>> GetListAsync(Guid typeId, Guid? processId, CancellationToken ct = default)
    {
        var type = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(typeId), ct)
            ?? throw new DomainException("设备类型不存在");

        var templates = type.Templates.AsEnumerable();
        if (processId.HasValue)
            templates = templates.Where(t => t.ProcessId == processId.Value);

        var processNames = await GetProcessNames(templates.Select(t => t.ProcessId).Distinct(), ct);

        return templates.OrderBy(t => t.SortOrder).Select(t =>
        {
            var values = t.Values;
            var paramsById = type.Parameters.ToDictionary(p => p.Id);
            var worst = WorstStatus(values, paramsById);
            return new EquipmentTemplateListItemDto
            {
                Id = t.Id,
                Name = t.Name,
                ProcessId = t.ProcessId,
                ProcessName = processNames.GetValueOrDefault(t.ProcessId, ""),
                Status = worst.Status,
                StatusMessage = worst.Message,
                SortOrder = t.SortOrder,
                CreatedAt = t.CreatedAt,
            };
        }).ToList();
    }

    public async Task<EquipmentTemplateDto?> GetByIdAsync(Guid typeId, Guid id, CancellationToken ct = default)
    {
        var type = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(typeId), ct);
        if (type is null) return null;

        var template = type.Templates.FirstOrDefault(t => t.Id == id);
        if (template is null) return null;

        var paramsById = type.Parameters.ToDictionary(p => p.Id);
        var processNames = await GetProcessNames(new[] { template.ProcessId }, ct);

        return new EquipmentTemplateDto
        {
            Id = template.Id,
            Name = template.Name,
            ProcessId = template.ProcessId,
            ProcessName = processNames.GetValueOrDefault(template.ProcessId, ""),
            Remark = template.Remark,
            SortOrder = template.SortOrder,
            CreatedAt = template.CreatedAt,
            UpdatedAt = template.UpdatedAt,
            Status = "valid",
            Values = template.Values.Select(v =>
            {
                paramsById.TryGetValue(v.ParameterId, out var param);
                var (status, msg) = EquipmentParameterValueValidator.EvaluateStatus(param, v.Value);
                return new EquipmentTemplateValueDto
                {
                    ParameterId = v.ParameterId,
                    ParameterName = param?.Name ?? "(已删除)",
                    ValueType = param?.ValueType ?? Domain.Enums.ParameterValueType.Text,
                    Value = v.Value,
                    Status = status,
                    StatusMessage = msg,
                };
            }).ToList(),
        };
    }

    public async Task<EquipmentTemplateDto> CreateAsync(Guid typeId, CreateEquipmentTemplateRequest request, CancellationToken ct = default)
    {
        await _createValidator.EnsureValidAsync(request, ct);

        var type = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(typeId), ct)
            ?? throw new DomainException("设备类型不存在");

        // 唯一性：(TypeId, ProcessId, Name)
        if (type.Templates.Any(t => t.ProcessId == request.ProcessId && t.Name == request.Name))
        {
            throw new DomainException($"工序下模板名「{request.Name}」已存在");
        }

        var paramsById = type.Parameters.ToDictionary(p => p.Id);

        // 强校验每个值
        foreach (var v in request.Values)
        {
            if (!paramsById.TryGetValue(v.ParameterId, out var param))
            {
                throw new DomainException($"参数 {v.ParameterId} 不属于此设备类型");
            }
            EquipmentParameterValueValidator.ValidateValue(param, v.Value);
        }

        var template = new EquipmentTemplate
        {
            EquipmentTypeId = typeId,
            ProcessId = request.ProcessId,
            Name = request.Name,
            Remark = request.Remark,
            SortOrder = request.SortOrder,
            Values = request.Values.Select(v => new EquipmentTemplateValue
            {
                ParameterId = v.ParameterId,
                Value = v.Value,
            }).ToList(),
        };

        type.Templates.Add(template);
        await _uow.SaveChangesAsync(ct);

        return await GetByIdAsync(typeId, template.Id, ct) ?? throw new DomainException("模板创建失败");
    }

    public async Task<EquipmentTemplateDto> UpdateAsync(Guid typeId, Guid id, UpdateEquipmentTemplateRequest request, CancellationToken ct = default)
    {
        await _updateValidator.EnsureValidAsync(request, ct);

        var type = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(typeId), ct)
            ?? throw new DomainException("设备类型不存在");

        var template = type.Templates.FirstOrDefault(t => t.Id == id)
            ?? throw new DomainException("模板不存在");

        // 唯一性（排除自身）
        if (type.Templates.Any(t => t.Id != id && t.ProcessId == request.ProcessId && t.Name == request.Name))
        {
            throw new DomainException($"工序下模板名「{request.Name}」已存在");
        }

        var paramsById = type.Parameters.ToDictionary(p => p.Id);

        // 强校验每个值
        foreach (var v in request.Values)
        {
            if (!paramsById.TryGetValue(v.ParameterId, out var param))
            {
                throw new DomainException($"参数 {v.ParameterId} 不属于此设备类型");
            }
            EquipmentParameterValueValidator.ValidateValue(param, v.Value);
        }

        // 更新基础字段
        template.Name = request.Name;
        template.ProcessId = request.ProcessId;
        template.Remark = request.Remark;
        template.SortOrder = request.SortOrder;

        // 值整表替换
        template.Values.Clear();
        foreach (var v in request.Values)
        {
            template.Values.Add(new EquipmentTemplateValue
            {
                ParameterId = v.ParameterId,
                Value = v.Value,
            });
        }

        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(typeId, id, ct) ?? throw new DomainException("模板更新失败");
    }

    public async Task DeleteAsync(Guid typeId, Guid id, CancellationToken ct = default)
    {
        var type = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(typeId), ct)
            ?? throw new DomainException("设备类型不存在");

        var template = type.Templates.FirstOrDefault(t => t.Id == id)
            ?? throw new DomainException("模板不存在");

        type.Templates.Remove(template);
        await _uow.SaveChangesAsync(ct);
    }

    // ── 辅助方法 ──

    private async Task<Dictionary<Guid, string>> GetProcessNames(IEnumerable<Guid> processIds, CancellationToken ct)
    {
        if (!processIds.Any()) return new();
        var processes = await _processes.ListAsync(ct);
        return processes.Where(p => processIds.Contains(p.Id)).ToDictionary(p => p.Id, p => p.Name);
    }

    private static (string Status, string? Message) WorstStatus(List<EquipmentTemplateValue> values, Dictionary<Guid, EquipmentTypeParameter> paramsById)
    {
        string? worst = null;
        string? msg = null;
        var priority = new Dictionary<string, int> { ["orphan"] = 2, ["invalid"] = 1, ["valid"] = 0 };
        foreach (var v in values)
        {
            paramsById.TryGetValue(v.ParameterId, out var param);
            var (s, m) = EquipmentParameterValueValidator.EvaluateStatus(param, v.Value);
            if (worst is null || priority[s] > priority[worst])
            {
                worst = s;
                msg = m;
            }
        }
        return (worst ?? "valid", msg);
    }
}
```

- [ ] **Step 3: 写 EquipmentTemplateServiceTests.cs**

创建 `backend/tests/OneCup.UnitTests/Equipment/EquipmentTemplateServiceTests.cs`：

```csharp
using OneCup.Application.Dtos.System;
using OneCup.Application.Services;
using OneCup.Application.Validators.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
using Xunit;

namespace OneCup.UnitTests.Equipment;

public class EquipmentTemplateServiceTests
{
    private static (OneCupDbContext db, EquipmentTypeService typeSvc, EquipmentTemplateService tplSvc) Setup()
    {
        var db = EquipmentTestHelper.CreateDb("eqtpl");
        var numbering = new FakeNumberingService("EQT-");
        var typeSvc = new EquipmentTypeService(
            new Repository<EquipmentType>(db),
            new Repository<Equipment>(db),
            new UnitOfWork(db),
            numbering,
            new CreateEquipmentTypeRequestValidator(),
            new UpdateEquipmentTypeRequestValidator());
        var tplSvc = new EquipmentTemplateService(
            new Repository<EquipmentTemplate>(db),
            new Repository<EquipmentType>(db),
            new Repository<Process>(db),
            new UnitOfWork(db),
            new CreateEquipmentTemplateRequestValidator(),
            new UpdateEquipmentTemplateRequestValidator());
        return (db, typeSvc, tplSvc);
    }

    private static async Task<(Guid typeId, Guid numberParamId, Guid enumParamId)> SeedType(EquipmentTypeService typeSvc)
    {
        var dto = await typeSvc.CreateAsync(new CreateEquipmentTypeRequest
        {
            Name = "定型机",
            Parameters = new()
            {
                new() { Name = "车速", ValueType = ParameterValueType.Number, MinValue = "80", MaxValue = "200", Required = true, SortOrder = 1 },
                new() { Name = "档位", ValueType = ParameterValueType.Enum, Options = new() { "低", "中", "高" }, Required = true, SortOrder = 2 },
            },
        });
        return (dto.Id, dto.Parameters[0].Id, dto.Parameters[1].Id);
    }

    private static async Task<Guid> SeedProcess(OneCupDbContext db)
    {
        var p = new Process { Code = "PRC-001", Name = "定型" };
        db.Processes.Add(p);
        await db.SaveChangesAsync();
        return p.Id;
    }

    [Fact]
    public async Task CreateAsync_ValidValues_Succeeds()
    {
        var (db, typeSvc, tplSvc) = Setup();
        var (typeId, numParamId, enumParamId) = await SeedType(typeSvc);
        var processId = await SeedProcess(db);

        var dto = await tplSvc.CreateAsync(typeId, new CreateEquipmentTemplateRequest
        {
            Name = "高温快烤",
            ProcessId = processId,
            Values = new()
            {
                new() { ParameterId = numParamId, Value = "150" },
                new() { ParameterId = enumParamId, Value = "高" },
            },
        });

        Assert.Equal("高温快烤", dto.Name);
        Assert.Equal(2, dto.Values.Count);
        Assert.All(dto.Values, v => Assert.Equal("valid", v.Status));
    }

    [Fact]
    public async Task CreateAsync_NumberOutOfRange_Throws()
    {
        var (db, typeSvc, tplSvc) = Setup();
        var (typeId, numParamId, _) = await SeedType(typeSvc);
        var processId = await SeedProcess(db);

        await Assert.ThrowsAsync<DomainException>(() => tplSvc.CreateAsync(typeId, new CreateEquipmentTemplateRequest
        {
            Name = "超限",
            ProcessId = processId,
            Values = new() { new() { ParameterId = numParamId, Value = "250" } },
        }));
    }

    [Fact]
    public async Task CreateAsync_EnumNotInOptions_Throws()
    {
        var (db, typeSvc, tplSvc) = Setup();
        var (typeId, _, enumParamId) = await SeedType(typeSvc);
        var processId = await SeedProcess(db);

        await Assert.ThrowsAsync<DomainException>(() => tplSvc.CreateAsync(typeId, new CreateEquipmentTemplateRequest
        {
            Name = "非法枚举",
            ProcessId = processId,
            Values = new() { new() { ParameterId = enumParamId, Value = "极高" } },
        }));
    }

    [Fact]
    public async Task CreateAsync_RequiredEmpty_Throws()
    {
        var (db, typeSvc, tplSvc) = Setup();
        var (typeId, numParamId, _) = await SeedType(typeSvc);
        var processId = await SeedProcess(db);

        await Assert.ThrowsAsync<DomainException>(() => tplSvc.CreateAsync(typeId, new CreateEquipmentTemplateRequest
        {
            Name = "缺值",
            ProcessId = processId,
            Values = new() { new() { ParameterId = numParamId, Value = "" } },
        }));
    }

    [Fact]
    public async Task GetById_OrphanValue_ReturnsOrphanStatus()
    {
        var (db, typeSvc, tplSvc) = Setup();
        var (typeId, numParamId, _) = await SeedType(typeSvc);
        var processId = await SeedProcess(db);

        // 建模板
        var created = await tplSvc.CreateAsync(typeId, new CreateEquipmentTemplateRequest
        {
            Name = "T1", ProcessId = processId,
            Values = new() { new() { ParameterId = numParamId, Value = "100" } },
        });

        // 删除参数定义（通过更新类型，不传该参数）
        await typeSvc.UpdateAsync(typeId, new UpdateEquipmentTypeRequest
        {
            Name = "定型机",
            Parameters = new() { },  // 清空参数
        });

        // 读取模板 → 该值应标 orphan
        var detail = await tplSvc.GetByIdAsync(typeId, created.Id);
        Assert.NotNull(detail);
        Assert.Equal("orphan", detail!.Values[0].Status);
    }

    [Fact]
    public async Task UpdateAsync_DuplicateNameSameProcess_Throws()
    {
        var (db, typeSvc, tplSvc) = Setup();
        var (typeId, numParamId, _) = await SeedType(typeSvc);
        var processId = await SeedProcess(db);

        await tplSvc.CreateAsync(typeId, new CreateEquipmentTemplateRequest
        {
            Name = "T1", ProcessId = processId,
            Values = new() { new() { ParameterId = numParamId, Value = "100" } },
        });

        await Assert.ThrowsAsync<DomainException>(() => tplSvc.CreateAsync(typeId, new CreateEquipmentTemplateRequest
        {
            Name = "T1", ProcessId = processId,
            Values = new() { new() { ParameterId = numParamId, Value = "100" } },
        }));
    }
}
```

- [ ] **Step 4: 运行测试，验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~EquipmentTemplateServiceTests"`
Expected: 全部 PASS

- [ ] **Step 5: Commit**

```bash
git add backend/src/OneCup.Application/Interfaces/IEquipmentTemplateService.cs backend/src/OneCup.Application/Services/EquipmentTemplateService.cs backend/tests/OneCup.UnitTests/Equipment/EquipmentTemplateServiceTests.cs
git commit -m "feat(equipment): 运行模板服务 + 单测（强校验 + 读时状态）"
```

---

## Task 12: 设备实例服务（EquipmentService）

**Files:**
- Create: `backend/src/OneCup.Application/Interfaces/IEquipmentService.cs`
- Create: `backend/src/OneCup.Application/Services/EquipmentService.cs`
- Test: `backend/tests/OneCup.UnitTests/Equipment/EquipmentServiceTests.cs`

**Interfaces:**
- Consumes: Task 2 实体、Task 4 NumberTargetTypes、Task 6 DTO、Task 7 校验器、Task 8 Specs、现有 `INumberingService`/`IRepository<T>`/`IUnitOfWork`/`EnsureValidAsync`、`EquipmentType` 实体（查 EquipmentTypeName）
- Produces: `IEquipmentService` 接口 + 实现，供 Task 15 控制器调用

- [ ] **Step 1: 创建 IEquipmentService.cs 接口**

```csharp
using OneCup.Application.Dtos.System;
using OneCup.Domain.Enums;

namespace OneCup.Application.Interfaces;

public interface IEquipmentService
{
    Task<PagedResult<EquipmentListItemDto>> GetListAsync(
        string? keyword, string? code, Guid? typeId, bool? isActive, EquipmentStatus? status,
        int page, int pageSize, CancellationToken ct = default);

    Task<EquipmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<EquipmentDto> CreateAsync(CreateEquipmentRequest request, CancellationToken ct = default);

    Task<EquipmentDto> UpdateAsync(Guid id, UpdateEquipmentRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 2: 创建 EquipmentService.cs 实现**

```csharp
using FluentValidation;
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;

namespace OneCup.Application.Services;

/// <summary>
/// 设备实例服务。
/// 编号走编号引擎（事务内取号，c02）。软删除。
/// 列表用扁平投影（不含参数/模板，只带类型名）。
/// </summary>
public class EquipmentService : IEquipmentService
{
    private readonly IRepository<Equipment> _equipments;
    private readonly IRepository<EquipmentType> _types;
    private readonly IUnitOfWork _uow;
    private readonly INumberingService _numbering;
    private readonly IValidator<CreateEquipmentRequest> _createValidator;
    private readonly IValidator<UpdateEquipmentRequest> _updateValidator;

    public EquipmentService(
        IRepository<Equipment> equipments,
        IRepository<EquipmentType> types,
        IUnitOfWork uow,
        INumberingService numbering,
        IValidator<CreateEquipmentRequest> createValidator,
        IValidator<UpdateEquipmentRequest> updateValidator)
    {
        _equipments = equipments;
        _types = types;
        _uow = uow;
        _numbering = numbering;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PagedResult<EquipmentListItemDto>> GetListAsync(
        string? keyword, string? code, Guid? typeId, bool? isActive, EquipmentStatus? status,
        int page, int pageSize, CancellationToken ct = default)
    {
        var total = await _equipments.CountAsync(
            new EquipmentFilterSpec(keyword, code, typeId, isActive, status), ct);
        var items = await _equipments.ListAsync(
            new EquipmentPagedSpec(keyword, code, typeId, isActive, status, page, pageSize), ct);

        // 批量查类型名
        var typeIds = items.Select(e => e.EquipmentTypeId).Distinct().ToList();
        var typeNames = await GetTypeNames(typeIds, ct);

        return new PagedResult<EquipmentListItemDto>
        {
            Items = items.Select(e => new EquipmentListItemDto
            {
                Id = e.Id,
                Code = e.Code,
                Name = e.Name,
                EquipmentTypeId = e.EquipmentTypeId,
                EquipmentTypeName = typeNames.GetValueOrDefault(e.EquipmentTypeId, ""),
                Specification = e.Specification,
                Supplier = e.Supplier,
                Location = e.Location,
                Status = e.Status,
                IsActive = e.IsActive,
                CreatedAt = e.CreatedAt,
            }).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<EquipmentDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var e = await _equipments.FirstOrDefaultAsync(new EquipmentByIdSpec(id), ct);
        if (e is null) return null;

        var typeNames = await GetTypeNames(new[] { e.EquipmentTypeId }, ct);

        return new EquipmentDto
        {
            Id = e.Id,
            Code = e.Code,
            Name = e.Name,
            EquipmentTypeId = e.EquipmentTypeId,
            EquipmentTypeName = typeNames.GetValueOrDefault(e.EquipmentTypeId, ""),
            Specification = e.Specification,
            Supplier = e.Supplier,
            Location = e.Location,
            Status = e.Status,
            PurchaseDate = e.PurchaseDate,
            WarrantyExpiry = e.WarrantyExpiry,
            Remark = e.Remark,
            IsActive = e.IsActive,
            CreatedAt = e.CreatedAt,
            UpdatedAt = e.UpdatedAt,
        };
    }

    public async Task<EquipmentDto> CreateAsync(CreateEquipmentRequest request, CancellationToken ct = default)
    {
        await _createValidator.EnsureValidAsync(request, ct);

        // 类型存在性校验
        if (!await _types.AnyAsync(new EquipmentTypeByIdSpec(request.EquipmentTypeId), ct))
        {
            throw new DomainException("设备类型不存在");
        }

        // 名称唯一性（绕过软删除）
        if (await _equipments.AnyIgnoringFiltersAsync(new EquipmentByNameSpec(request.Name), ct))
        {
            throw new DomainException($"设备名称「{request.Name}」已存在");
        }

        Guid createdId = Guid.Empty;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            var code = await _numbering.GenerateAsync(NumberTargetTypes.Equipment, request.CategoryCode, ct);
            var equipment = new Equipment
            {
                Code = code,
                Name = request.Name,
                EquipmentTypeId = request.EquipmentTypeId,
                Specification = request.Specification,
                Supplier = request.Supplier,
                Location = request.Location,
                Status = request.Status,
                PurchaseDate = request.PurchaseDate,
                WarrantyExpiry = request.WarrantyExpiry,
                Remark = request.Remark,
                IsActive = request.IsActive,
                SortOrder = request.SortOrder,
            };
            await _equipments.AddAsync(equipment, ct);
            await _uow.SaveChangesAsync(ct);
            createdId = equipment.Id;
        }, ct);

        return await GetByIdAsync(createdId, ct) ?? throw new DomainException("设备创建失败");
    }

    public async Task<EquipmentDto> UpdateAsync(Guid id, UpdateEquipmentRequest request, CancellationToken ct = default)
    {
        await _updateValidator.EnsureValidAsync(request, ct);

        var equipment = await _equipments.FirstOrDefaultAsync(new EquipmentByIdSpec(id), ct)
            ?? throw new DomainException("设备不存在");

        // 类型存在性
        if (!await _types.AnyAsync(new EquipmentTypeByIdSpec(request.EquipmentTypeId), ct))
        {
            throw new DomainException("设备类型不存在");
        }

        // 改名查重
        if (await _equipments.AnyIgnoringFiltersAsync(new EquipmentByNameSpec(request.Name, id), ct))
        {
            throw new DomainException($"设备名称「{request.Name}」已存在");
        }

        equipment.Name = request.Name;
        equipment.EquipmentTypeId = request.EquipmentTypeId;
        equipment.Specification = request.Specification;
        equipment.Supplier = request.Supplier;
        equipment.Location = request.Location;
        equipment.Status = request.Status;
        equipment.PurchaseDate = request.PurchaseDate;
        equipment.WarrantyExpiry = request.WarrantyExpiry;
        equipment.Remark = request.Remark;
        equipment.IsActive = request.IsActive;
        equipment.SortOrder = request.SortOrder;

        await _uow.SaveChangesAsync(ct);
        return await GetByIdAsync(id, ct) ?? throw new DomainException("设备更新失败");
    }

    public async Task DeleteAsync(Guid id, CancellationToken ct = default)
    {
        // GetByIdAsync 走软删除过滤器，这里用 GetByIdAsync 绕过（参考 Customer 软删除模式）
        var equipment = await _equipments.GetByIdAsync(id, ct)
            ?? throw new DomainException("设备不存在");

        equipment.IsDeleted = true;
        await _uow.SaveChangesAsync(ct);
    }

    private async Task<Dictionary<Guid, string>> GetTypeNames(IEnumerable<Guid> typeIds, CancellationToken ct)
    {
        if (!typeIds.Any()) return new();
        var all = await _types.ListAsync(ct);
        return all.Where(t => typeIds.Contains(t.Id)).ToDictionary(t => t.Id, t => t.Name);
    }
}
```

- [ ] **Step 3: 写 EquipmentServiceTests.cs**

创建 `backend/tests/OneCup.UnitTests/Equipment/EquipmentServiceTests.cs`：

```csharp
using OneCup.Application.Dtos.System;
using OneCup.Application.Services;
using OneCup.Application.Validators.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Enums;
using OneCup.Domain.Exceptions;
using OneCup.Infrastructure.Persistence;
using Xunit;

namespace OneCup.UnitTests.Equipment;

public class EquipmentServiceTests
{
    private static (OneCupDbContext db, EquipmentService svc, FakeNumberingService numbering) Setup()
    {
        var db = EquipmentTestHelper.CreateDb("eq");
        var numbering = new FakeNumberingService("EQ-");
        var svc = new EquipmentService(
            new Repository<Equipment>(db),
            new Repository<EquipmentType>(db),
            new UnitOfWork(db),
            numbering,
            new CreateEquipmentRequestValidator(),
            new UpdateEquipmentRequestValidator());
        return (db, svc, numbering);
    }

    private static async Task<Guid> SeedType(OneCupDbContext db)
    {
        var t = new EquipmentType { Code = "EQT-001", Name = "定型机" };
        db.EquipmentTypes.Add(t);
        await db.SaveChangesAsync();
        return t.Id;
    }

    private static CreateEquipmentRequest ValidCreate(Guid typeId) => new()
    {
        Name = "1号定型机",
        EquipmentTypeId = typeId,
        Supplier = "某机械厂",
        Location = "1号车间",
        Status = EquipmentStatus.Running,
    };

    [Fact]
    public async Task CreateAsync_GeneratesCode()
    {
        var (db, svc, numbering) = Setup();
        var typeId = await SeedType(db);
        numbering.NextCode = "EQ-0001";

        var dto = await svc.CreateAsync(ValidCreate(typeId));

        Assert.Equal("EQ-0001", dto.Code);
        Assert.Equal("定型机", dto.EquipmentTypeName);
        Assert.Equal(EquipmentStatus.Running, dto.Status);
    }

    [Fact]
    public async Task CreateAsync_NonExistentType_Throws()
    {
        var (_, svc, _) = Setup();
        await Assert.ThrowsAsync<DomainException>(
            () => svc.CreateAsync(ValidCreate(Guid.NewGuid())));
    }

    [Fact]
    public async Task CreateAsync_DuplicateName_Throws()
    {
        var (db, svc, _) = Setup();
        var typeId = await SeedType(db);
        await svc.CreateAsync(ValidCreate(typeId));

        await Assert.ThrowsAsync<DomainException>(() => svc.CreateAsync(ValidCreate(typeId)));
    }

    [Fact]
    public async Task DeleteAsync_SoftDeletes_AndIdempotent()
    {
        var (db, svc, _) = Setup();
        var typeId = await SeedType(db);
        var created = await svc.CreateAsync(ValidCreate(typeId));

        await svc.DeleteAsync(created.Id);
        // 软删后查不到
        var found = await svc.GetByIdAsync(created.Id);
        Assert.Null(found);

        // 幂等：再删不报错（GetByIdAsync 绕过过滤器）
        await svc.DeleteAsync(created.Id);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesFields()
    {
        var (db, svc, _) = Setup();
        var typeId = await SeedType(db);
        var created = await svc.CreateAsync(ValidCreate(typeId));

        var updated = await svc.UpdateAsync(created.Id, new UpdateEquipmentRequest
        {
            Name = "1号定型机",
            EquipmentTypeId = typeId,
            Status = EquipmentStatus.Maintenance,
            Location = "2号车间",
        });

        Assert.Equal(EquipmentStatus.Maintenance, updated.Status);
        Assert.Equal("2号车间", updated.Location);
    }
}
```

> **注意**：FakeNumberingService 已在共享文件 `EquipmentTestHelper.cs`（Task 10 Step 5）定义，本文件不再内联定义。Task 11/12 测试提交时需确认该共享文件已在 Task 10 创建。

- [ ] **Step 4: 运行测试，验证通过**

Run: `dotnet test backend/tests/OneCup.UnitTests --filter "FullyQualifiedName~EquipmentServiceTests"`
Expected: 全部 PASS

- [ ] **Step 5: Commit**

```bash
git add backend/src/OneCup.Application/Interfaces/IEquipmentService.cs backend/src/OneCup.Application/Services/EquipmentService.cs backend/tests/OneCup.UnitTests/Equipment/EquipmentServiceTests.cs
git commit -m "feat(equipment): 设备实例服务 + 单测"
```

---

## Task 13: 控制器（3 个）+ DI 注册

**Files:**
- Create: `backend/src/OneCup.Api/Controllers/EquipmentTypesController.cs`
- Create: `backend/src/OneCup.Api/Controllers/EquipmentTemplatesController.cs`
- Create: `backend/src/OneCup.Api/Controllers/EquipmentsController.cs`
- Modify: `backend/src/OneCup.Api/Program.cs`（追加服务注册 + 权限策略）

**Interfaces:**
- Consumes: Task 10-12 的三个服务接口
- Produces: 3 个 RESTful 控制器，HTTP 端点

参考模式：`CustomersController.cs`（类级 `[Authorize(Policy="xxx:read")]` + 方法级写权限 + `[Audit]`）

- [ ] **Step 1: 在 Program.cs 注册服务和权限策略**

在服务注册段（约 line 120，Material 注册之后）追加：

```csharp
        // ===== Equipment 模块 =====
        builder.Services.AddScoped<IEquipmentTypeService, EquipmentTypeService>();
        builder.Services.AddScoped<IEquipmentTemplateService, EquipmentTemplateService>();
        builder.Services.AddScoped<IEquipmentService, EquipmentService>();
```

在授权策略段（约 line 164-167，已有 `equipment:*` 策略之后）追加 EquipmentType 策略：

```csharp
        options.AddPolicy("equipment-type:read", p => p.RequireClaim("perm_codes", "equipment-type:read"));
        options.AddPolicy("equipment-type:create", p => p.RequireClaim("perm_codes", "equipment-type:create"));
        options.AddPolicy("equipment-type:update", p => p.RequireClaim("perm_codes", "equipment-type:update"));
        options.AddPolicy("equipment-type:delete", p => p.RequireClaim("perm_codes", "equipment-type:delete"));
```

- [ ] **Step 2: 创建 EquipmentTypesController.cs**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Api.Filters;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 设备类型管理端点。类级需 equipment-type:read；写操作需对应权限。
/// </summary>
[ApiController]
[Route("api/equipment-types")]
[Authorize(Policy = "equipment-type:read")]
public class EquipmentTypesController : ControllerBase
{
    private readonly IEquipmentTypeService _service;

    public EquipmentTypesController(IEquipmentTypeService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? keyword,
        [FromQuery] string? code,
        [FromQuery] bool? isActive,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _service.GetListAsync(keyword, code, isActive, page, pageSize, ct);
        return Ok(result);
    }

    /// <summary>启用类型列表（前端下拉用，不分页）。</summary>
    [HttpGet("active")]
    public async Task<IActionResult> GetActive(CancellationToken ct = default)
    {
        var result = await _service.GetActiveAsync(ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var type = await _service.GetByIdAsync(id, ct);
        return type is null ? NotFound() : Ok(type);
    }

    [Audit(Module = "EquipmentType", Action = "Create", TargetType = "EquipmentType")]
    [Authorize(Policy = "equipment-type:create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEquipmentTypeRequest request, CancellationToken ct)
    {
        var type = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = type.Id }, type);
    }

    [Audit(Module = "EquipmentType", Action = "Update", TargetType = "EquipmentType")]
    [Authorize(Policy = "equipment-type:update")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEquipmentTypeRequest request, CancellationToken ct)
    {
        var type = await _service.UpdateAsync(id, request, ct);
        return Ok(type);
    }

    [Audit(Module = "EquipmentType", Action = "Delete", TargetType = "EquipmentType")]
    [Authorize(Policy = "equipment-type:delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
```

- [ ] **Step 3: 创建 EquipmentTemplatesController.cs**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 运行模板管理端点（嵌套在设备类型路由下）。
/// 类级需 equipment-type:read；写操作需 equipment-type:create/update/delete。
/// </summary>
[ApiController]
[Route("api/equipment-types/{typeId:guid}/templates")]
[Authorize(Policy = "equipment-type:read")]
public class EquipmentTemplatesController : ControllerBase
{
    private readonly IEquipmentTemplateService _service;

    public EquipmentTemplatesController(IEquipmentTemplateService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        Guid typeId,
        [FromQuery] Guid? processId,
        CancellationToken ct = default)
    {
        var result = await _service.GetListAsync(typeId, processId, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid typeId, Guid id, CancellationToken ct)
    {
        var template = await _service.GetByIdAsync(typeId, id, ct);
        return template is null ? NotFound() : Ok(template);
    }

    [Authorize(Policy = "equipment-type:create")]
    [HttpPost]
    public async Task<IActionResult> Create(
        Guid typeId,
        [FromBody] CreateEquipmentTemplateRequest request,
        CancellationToken ct)
    {
        var template = await _service.CreateAsync(typeId, request, ct);
        return CreatedAtAction(nameof(GetById), new { typeId, id = template.Id }, template);
    }

    [Authorize(Policy = "equipment-type:update")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid typeId, Guid id,
        [FromBody] UpdateEquipmentTemplateRequest request,
        CancellationToken ct)
    {
        var template = await _service.UpdateAsync(typeId, id, request, ct);
        return Ok(template);
    }

    [Authorize(Policy = "equipment-type:delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid typeId, Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(typeId, id, ct);
        return NoContent();
    }
}
```

- [ ] **Step 4: 创建 EquipmentsController.cs**

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Api.Filters;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;
using OneCup.Domain.Enums;

namespace OneCup.Api.Controllers;

/// <summary>
/// 设备实例管理端点。类级需 equipment:read；写操作需对应权限。
/// </summary>
[ApiController]
[Route("api/equipment")]
[Authorize(Policy = "equipment:read")]
public class EquipmentsController : ControllerBase
{
    private readonly IEquipmentService _service;

    public EquipmentsController(IEquipmentService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<IActionResult> GetList(
        [FromQuery] string? keyword,
        [FromQuery] string? code,
        [FromQuery] Guid? typeId,
        [FromQuery] bool? isActive,
        [FromQuery] EquipmentStatus? status,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _service.GetListAsync(keyword, code, typeId, isActive, status, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken ct)
    {
        var equipment = await _service.GetByIdAsync(id, ct);
        return equipment is null ? NotFound() : Ok(equipment);
    }

    [Audit(Module = "Equipment", Action = "Create", TargetType = "Equipment")]
    [Authorize(Policy = "equipment:create")]
    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEquipmentRequest request, CancellationToken ct)
    {
        var equipment = await _service.CreateAsync(request, ct);
        return CreatedAtAction(nameof(GetById), new { id = equipment.Id }, equipment);
    }

    [Audit(Module = "Equipment", Action = "Update", TargetType = "Equipment")]
    [Authorize(Policy = "equipment:update")]
    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid id, [FromBody] UpdateEquipmentRequest request, CancellationToken ct)
    {
        var equipment = await _service.UpdateAsync(id, request, ct);
        return Ok(equipment);
    }

    [Audit(Module = "Equipment", Action = "Delete", TargetType = "Equipment")]
    [Authorize(Policy = "equipment:delete")]
    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _service.DeleteAsync(id, ct);
        return NoContent();
    }
}
```

- [ ] **Step 5: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 6: 运行全部后端测试确认无回归**

Run: `dotnet test backend/tests/OneCup.UnitTests`
Expected: 全部 PASS（含原有模块测试 + 新增 Equipment 测试）

- [ ] **Step 7: Commit**

```bash
git add backend/src/OneCup.Api/Controllers/Equipment*.cs backend/src/OneCup.Api/Program.cs
git commit -m "feat(equipment): 3 个控制器 + DI 注册 + 权限策略"
```

---

## 后端阶段完成检查点

至此后端全部完成（Task 1-13）。在进入前端前，启动后端验证 API 可用：

- [ ] **手动验证（可选但推荐）：启动后端，用 curl/Postman 测试关键链路**

```bash
cd backend/src/OneCup.Api
dotnet run
```

测试流程（需带 admin JWT）：
1. `GET /api/equipment-types/active` → 空列表
2. `POST /api/equipment-types` 创建类型（带参数定义）→ 返回编号 + 参数
3. `GET /api/equipment-types/{id}` → 含 Parameters
4. `POST /api/equipment-types/{typeId}/templates` 建模板（填值）→ 校验通过
5. `POST /api/equipment-types/{typeId}/templates` 模板值越界 → 400
6. `PUT /api/equipment-types/{id}` 改参数定义 → 模板读时返回 invalid 状态
7. `POST /api/equipment` 建设备 → 返回编号
8. `DELETE /api/equipment-types/{id}` 有设备引用 → 400

---

## Task 14: 前端 API client + 类型

**Files:**
- Create: `frontend/src/api/equipment.ts`

**Interfaces:**
- Consumes: Task 6/13 的后端 DTO 和端点
- Produces: 前端 API 调用函数 + TypeScript 类型，供 Task 15-17 页面调用

参考模式：`frontend/src/api/customer.ts`

- [ ] **Step 1: 创建 equipment.ts**

```typescript
import request from './request';

// ═══════════════════════════════════════════
// 类型定义（对齐后端 DTO）
// ═══════════════════════════════════════════

export const PARAMETER_VALUE_TYPES = ['Number', 'Text', 'Enum'] as const;
export type ParameterValueType = typeof PARAMETER_VALUE_TYPES[number];

export const EQUIPMENT_STATUSES = ['Running', 'Stopped', 'Maintenance'] as const;
export type EquipmentStatus = typeof EQUIPMENT_STATUSES[number];

export interface ParameterDefinitionDto {
  id?: string;
  name: string;
  valueType: ParameterValueType;
  unitId?: string;
  minValue?: string;
  maxValue?: string;
  precision?: number;
  options?: string[];
  required: boolean;
  sortOrder: number;
  remark?: string;
}

export interface EquipmentTypeParameterDto extends ParameterDefinitionDto {
  id: string;
  unitSymbol?: string;
}

export interface EquipmentTypeListItemDto {
  id: string;
  code: string;
  name: string;
  parameterCount: number;
  templateCount: number;
  isActive: boolean;
  createdAt: string;
}

export interface EquipmentTemplateSummaryDto {
  id: string;
  name: string;
  processId: string;
  processName: string;
  status?: string;
  sortOrder: number;
}

export interface EquipmentTypeDto extends EquipmentTypeListItemDto {
  remark?: string;
  updatedAt?: string;
  parameters: EquipmentTypeParameterDto[];
  templates: EquipmentTemplateSummaryDto[];
}

export interface CreateEquipmentTypeRequest {
  name: string;
  remark?: string;
  isActive: boolean;
  sortOrder: number;
  categoryCode?: string;
  parameters: ParameterDefinitionDto[];
}

export type UpdateEquipmentTypeRequest = Omit<CreateEquipmentTypeRequest, 'categoryCode'>;

// ── 模板 ──

export interface TemplateValueDto {
  parameterId: string;
  value?: string;
}

export interface EquipmentTemplateValueDto extends TemplateValueDto {
  parameterName: string;
  valueType: ParameterValueType;
  unitSymbol?: string;
  status: string;
  statusMessage?: string;
}

export interface EquipmentTemplateListItemDto {
  id: string;
  name: string;
  processId: string;
  processName: string;
  status?: string;
  statusMessage?: string;
  sortOrder: number;
  createdAt: string;
}

export interface EquipmentTemplateDto extends EquipmentTemplateListItemDto {
  remark?: string;
  updatedAt?: string;
  values: EquipmentTemplateValueDto[];
}

export interface CreateEquipmentTemplateRequest {
  name: string;
  processId: string;
  remark?: string;
  sortOrder: number;
  values: TemplateValueDto[];
}

export type UpdateEquipmentTemplateRequest = CreateEquipmentTemplateRequest;

// ── 设备实例 ──

export interface EquipmentListItemDto {
  id: string;
  code: string;
  name: string;
  equipmentTypeId: string;
  equipmentTypeName: string;
  specification?: string;
  supplier?: string;
  location?: string;
  status: EquipmentStatus;
  isActive: boolean;
  createdAt: string;
}

export interface EquipmentDto extends EquipmentListItemDto {
  purchaseDate?: string;
  warrantyExpiry?: string;
  remark?: string;
  updatedAt?: string;
}

export interface CreateEquipmentRequest {
  name: string;
  equipmentTypeId: string;
  specification?: string;
  supplier?: string;
  location?: string;
  status: EquipmentStatus;
  purchaseDate?: string;
  warrantyExpiry?: string;
  remark?: string;
  isActive: boolean;
  sortOrder: number;
  categoryCode?: string;
}

export type UpdateEquipmentRequest = Omit<CreateEquipmentRequest, 'categoryCode'>;

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

// ═══════════════════════════════════════════
// API 函数
// ═══════════════════════════════════════════

// ── 设备类型 ──
export const getEquipmentTypes = (params: {
  keyword?: string; code?: string; isActive?: boolean; page?: number; pageSize?: number;
}) => request.get<unknown, PagedResult<EquipmentTypeListItemDto>>('/api/equipment-types', { params });

export const getActiveEquipmentTypes = () =>
  request.get<unknown, EquipmentTypeListItemDto[]>('/api/equipment-types/active');

export const getEquipmentTypeById = (id: string) =>
  request.get<unknown, EquipmentTypeDto>(`/api/equipment-types/${id}`);

export const createEquipmentType = (data: CreateEquipmentTypeRequest) =>
  request.post<unknown, EquipmentTypeDto>('/api/equipment-types', data);

export const updateEquipmentType = (id: string, data: UpdateEquipmentTypeRequest) =>
  request.put<unknown, EquipmentTypeDto>(`/api/equipment-types/${id}`, data);

export const deleteEquipmentType = (id: string) =>
  request.delete<unknown, void>(`/api/equipment-types/${id}`);

// ── 运行模板 ──
export const getEquipmentTemplates = (typeId: string, processId?: string) =>
  request.get<unknown, EquipmentTemplateListItemDto[]>(`/api/equipment-types/${typeId}/templates`, {
    params: processId ? { processId } : undefined,
  });

export const getEquipmentTemplateById = (typeId: string, id: string) =>
  request.get<unknown, EquipmentTemplateDto>(`/api/equipment-types/${typeId}/templates/${id}`);

export const createEquipmentTemplate = (typeId: string, data: CreateEquipmentTemplateRequest) =>
  request.post<unknown, EquipmentTemplateDto>(`/api/equipment-types/${typeId}/templates`, data);

export const updateEquipmentTemplate = (typeId: string, id: string, data: UpdateEquipmentTemplateRequest) =>
  request.put<unknown, EquipmentTemplateDto>(`/api/equipment-types/${typeId}/templates/${id}`, data);

export const deleteEquipmentTemplate = (typeId: string, id: string) =>
  request.delete<unknown, void>(`/api/equipment-types/${typeId}/templates/${id}`);

// ── 设备实例 ──
export const getEquipments = (params: {
  keyword?: string; code?: string; typeId?: string; isActive?: boolean; status?: EquipmentStatus;
  page?: number; pageSize?: number;
}) => request.get<unknown, PagedResult<EquipmentListItemDto>>('/api/equipment', { params });

export const getEquipmentById = (id: string) =>
  request.get<unknown, EquipmentDto>(`/api/equipment/${id}`);

export const createEquipment = (data: CreateEquipmentRequest) =>
  request.post<unknown, EquipmentDto>('/api/equipment', data);

export const updateEquipment = (id: string, data: UpdateEquipmentRequest) =>
  request.put<unknown, EquipmentDto>(`/api/equipment/${id}`, data);

export const deleteEquipment = (id: string) =>
  request.delete<unknown, void>(`/api/equipment/${id}`);
```

- [ ] **Step 2: 编译验证**

Run: `cd frontend && npx tsc --noEmit`
Expected: 无报错

- [ ] **Step 3: Commit**

```bash
git add frontend/src/api/equipment.ts
git commit -m "feat(equipment): 前端 API client + 类型定义"
```

---

## Task 15: 前端路由 + 容器页 + locale

**Files:**
- Modify: `frontend/src/routes.ts`（追加菜单项）
- Modify: `frontend/src/router.tsx`（追加路由）
- Create: `frontend/src/pages/business/equipment/index.tsx`（Tabs 容器页）
- Create: `frontend/src/pages/business/equipment/locale/index.ts`
- Create: `frontend/src/pages/business/equipment/locale/zh-CN.ts`
- Create: `frontend/src/pages/business/equipment/locale/en-US.ts`
- Create: `frontend/src/pages/business/equipment/style/index.module.less`

**Interfaces:**
- Consumes: Task 14 API client
- Produces: 菜单项、路由、Tabs 容器页框架（设备 tab + 设备类型 tab），供 Task 16/17 填充内容

- [ ] **Step 1: 在 routes.ts 追加菜单项**

在 `menu.business` 的 children 数组中，`process` 之后追加：

```typescript
      {
        name: 'menu.business.equipment',
        key: 'business/equipment',
        requiredPermissions: [
          { resource: 'equipment', actions: ['read'] },
        ],
      },
```

- [ ] **Step 2: 创建 locale 文件**

创建 `frontend/src/pages/business/equipment/locale/zh-CN.ts`：

```typescript
export default {
  'menu.business.equipment': '设备',
  'equipment.title': '设备管理',
  'equipment.tab.equipment': '设备',
  'equipment.tab.type': '设备类型',
};
```

创建 `frontend/src/pages/business/equipment/locale/en-US.ts`：

```typescript
export default {
  'menu.business.equipment': 'Equipment',
  'equipment.title': 'Equipment Management',
  'equipment.tab.equipment': 'Equipment',
  'equipment.tab.type': 'Equipment Type',
};
```

创建 `frontend/src/pages/business/equipment/locale/index.ts`：

```typescript
import zhCN from './zh-CN';
import enUS from './en-US';

export default {
  'zh-CN': zhCN,
  'en-US': enUS,
};
```

- [ ] **Step 3: 注册 locale**

查看项目现有 locale 加载方式（通常在 `frontend/src/locale/` 下的 index 文件聚合），将 equipment 的 locale 合并进去。具体做法：参考 customer 模块 locale 是如何被加载的（可能是自动扫描或手动 import），按相同方式接入。若项目用 i18n 自动加载，确认文件位置正确即可。

- [ ] **Step 4: 创建 style/index.module.less**

```less
.container {
  display: flex;
  flex-direction: column;
  height: 100%;
  padding: 0;
}
```

- [ ] **Step 5: 创建容器页 index.tsx（Tabs 框架）**

```tsx
import { useState } from 'react';
import { Tabs, Typography } from '@arco-design/web-react';
import { useTranslation } from 'react-i18next';
import PermissionWrapper from '@/components/PermissionWrapper';
import EquipmentTab from './equipment/EquipmentTab';
import TypeTab from './type/TypeTab';
import styles from './style/index.module.less';

const { Title } = Typography;

export default function EquipmentPage() {
  const { t } = useTranslation();
  const [activeTab, setActiveTab] = useState('equipment');

  return (
    <div className={styles.container}>
      <Tabs activeTab={activeTab} onChange={setActiveTab}>
        <Tabs.TabPane key="equipment" title={t('equipment.tab.equipment')}>
          <EquipmentTab />
        </Tabs.TabPane>
        <Tabs.TabPane
          key="type"
          title={t('equipment.tab.type')}
          disabled={false}
        >
          <PermissionWrapper requiredPermissions={[{ resource: 'equipment-type', actions: ['read'] }]}>
            <TypeTab />
          </PermissionWrapper>
        </Tabs.TabPane>
      </Tabs>
    </div>
  );
}
```

> **注意**：`EquipmentTab` 和 `TypeTab` 在 Task 16/17 才创建，此步会编译报错。可以将 Step 5 推迟到 Task 16/17 完成后，或在 Task 15 先创建空占位组件。建议：此 Task 先做 routes + locale + style，index.tsx 留到 Task 17 末尾连同子页面一起接入。

- [ ] **Step 6: 在 router.tsx 追加路由**

参考现有 customer 路由配置，追加 equipment 路由。通常形式：

```tsx
{
  path: '/business/equipment',
  element: <EquipmentPage />,
}
```

具体语法看 `frontend/src/router.tsx` 现有写法对齐。

- [ ] **Step 7: Commit（routes + locale + style，index.tsx 待 Task 17）**

```bash
git add frontend/src/routes.ts frontend/src/router.tsx frontend/src/pages/business/equipment/locale/ frontend/src/pages/business/equipment/style/
git commit -m "feat(equipment): 前端路由 + locale + 容器页框架"
```

---

## Task 16: 设备类型页（列表 + 表单 + 详情 + 参数编辑器）

**Files:**
- Create: `frontend/src/pages/business/equipment/type/TypeTab.tsx`
- Create: `frontend/src/pages/business/equipment/type/TypeForm.tsx`
- Create: `frontend/src/pages/business/equipment/type/TypeDetail.tsx`
- Create: `frontend/src/pages/business/equipment/type/ParameterEditor.tsx`

**Interfaces:**
- Consumes: Task 14 API client、`useNumberingPreview('EquipmentType')` hook、`<CategorySelect>`、`docs/specs/templates/query-table-page.template.tsx`
- Produces: 设备类型完整 CRUD 页面

> 这是最复杂的前端任务（参数定义动态表格）。参考 customer/form.tsx 的 c02 模式 + Arco 的 Table/Form 组件。

- [ ] **Step 1: 创建 TypeTab.tsx（列表页，从模板复制）**

从 `docs/specs/templates/query-table-page.template.tsx` 复制结构，替换为设备类型：
- 查询字段：keyword、isActive
- 列：code、name、parameterCount、templateCount、isActive、createdAt、操作
- 操作：详情（Drawer）、编辑（Modal）、删除（Popconfirm→ 因类型删除影响范围大，实际用 Modal 确认，对齐 c01）
- 新建按钮：打开 TypeForm Modal

关键代码骨架（完整代码参考模板，此处给关键差异）：

```tsx
import { useEffect, useState, useCallback } from 'react';
import { Button, Space, Table, Message, Popconfirm } from '@arco-design/web-react';
import { useTranslation } from 'react-i18next';
import { IconPlus, IconEdit, IconDelete, IconEye } from '@arco-design/web-react/icon';
import PermissionWrapper from '@/components/PermissionWrapper';
import { getEquipmentTypes, deleteEquipmentType, EquipmentTypeListItemDto } from '@/api/equipment';
import TypeForm from './TypeForm';
import TypeDetail from './TypeDetail';
// ... 标准 Query Table 结构，按模板替换字段
```

- [ ] **Step 2: 创建 ParameterEditor.tsx（参数定义动态表格组件）**

这是核心组件：可增删行的表格，每行 ValueType 切换会展开不同子控件。

```tsx
import { Table, Input, Select, InputNumber, Button, Space, Switch, Tag } from '@arco-design/web-react';
import { IconPlus, IconDelete } from '@arco-design/web-react/icon';
import { ParameterDefinitionDto, ParameterValueType } from '@/api/equipment';

interface Props {
  value: ParameterDefinitionDto[];
  onChange: (value: ParameterDefinitionDto[]) => void;
  unitOptions?: { label: string; value: string }[];
}

export default function ParameterEditor({ value, onChange, unitOptions = [] }: Props) {
  // 增删改行的方法
  const addRow = () => onChange([...value, {
    name: '', valueType: 'Number', required: false, sortOrder: value.length + 1,
  }]);
  const removeRow = (idx: number) => value.filter((_, i) => i !== idx);
  const updateRow = (idx: number, patch: Partial<ParameterDefinitionDto>) =>
    value.map((v, i) => i === idx ? { ...v, ...patch } : v);

  // ValueType 切换时重置不相关字段
  // Number → 显示单位/Min/Max/Precision
  // Enum → 显示 Options 输入（Tag 输入或逗号分隔）
  // Text → 无额外控件

  return (
    <div>
      <Table data={value} pagination={false} rowKey={(_, i) => String(i)}>
        {/* 列：参数名 / 类型 / 约束 / 必填 / 操作 */}
      </Table>
      <Button icon={<IconPlus />} onClick={addRow}>添加参数</Button>
    </div>
  );
}
```

> **实现细节**：ValueType=Enum 时用 Arco 的 `Input.Tag`（或逗号分隔的 Input）收集 Options 数组；ValueType=Number 时展开 Min/Max/Precision 输入框 + 单位 Select。完整实现需参考 Arco Table 的 render 函数写法。

- [ ] **Step 3: 创建 TypeForm.tsx（新建/编辑 Modal，含 c02 + 参数编辑器）**

```tsx
import { useEffect } from 'react';
import { Modal, Form, Input, Switch, Alert } from '@arco-design/web-react';
import { useNumberingPreview } from '@/components/Numbering/useNumberingPreview';
import CategorySelect from '@/components/Numbering/CategorySelect';
import ParameterEditor from './ParameterEditor';
import {
  CreateEquipmentTypeRequest, createEquipmentType, updateEquipmentType, getEquipmentTypeById,
} from '@/api/equipment';
// ... 参考 customer/form.tsx 的 c02 模式
```

关键流程：
- `useNumberingPreview('EquipmentType')` 取编号预览
- `preview.noRule` 时禁用表单 + Alert 提示
- `!editing && preview.includeCategory` 时渲染 `<CategorySelect>`
- 编号只读展示 `preview.code`
- `<ParameterEditor>` 嵌在表单内
- 提交：`{ ...values, categoryCode: preview.categoryCode, parameters }`

- [ ] **Step 4: 创建 TypeDetail.tsx（详情 Drawer）**

展示类型基础信息 + 参数定义表格 + 运行模板列表（摘要）。模板列表点击可打开模板编辑（Task 17）。

- [ ] **Step 5: 编译验证**

Run: `cd frontend && npx tsc --noEmit`
Expected: 无报错

- [ ] **Step 6: 手动验证**

`npm run dev` 后访问设备类型 tab：新建类型含参数 → 保存 → 列表显示 → 详情展示参数。

- [ ] **Step 7: Commit**

```bash
git add frontend/src/pages/business/equipment/type/
git commit -m "feat(equipment): 设备类型页（列表+表单+详情+参数编辑器）"
```

---

## Task 17: 运行模板页 + 容器页接入

**Files:**
- Create: `frontend/src/pages/business/equipment/type/template/TemplateList.tsx`
- Create: `frontend/src/pages/business/equipment/type/template/TemplateForm.tsx`
- Create: `frontend/src/pages/business/equipment/type/template/TemplateValueEditor.tsx`
- Create: `frontend/src/pages/business/equipment/equipment/EquipmentTab.tsx`
- Create: `frontend/src/pages/business/equipment/equipment/EquipmentForm.tsx`
- Create: `frontend/src/pages/business/equipment/equipment/EquipmentDetail.tsx`
- Create: `frontend/src/pages/business/equipment/index.tsx`（Task 15 占位的容器页，正式接入）

**Interfaces:**
- Consumes: Task 14 API client、`useNumberingPreview('Equipment')`、工序列表 API（`/api/process` 或现有 process API）

- [ ] **Step 1: 创建 TemplateValueEditor.tsx（模板值动态表单）**

按参数定义的 ValueType 渲染：Number=InputNumber、Enum=Select、Text=Input。编辑场景带 status 标记（invalid/orphan 标红）。

```tsx
import { Form, Input, InputNumber, Select, Alert, Tag } from '@arco-design/web-react';
import { EquipmentTypeParameterDto, EquipmentTemplateValueDto, TemplateValueDto } from '@/api/equipment';

interface Props {
  parameters: EquipmentTypeParameterDto[];  // 该类型的参数定义
  values: TemplateValueDto[];                // 当前编辑的值
  existingValues?: EquipmentTemplateValueDto[]; // 编辑模式下的带状态值
  onChange: (values: TemplateValueDto[]) => void;
}
// 按参数定义遍历渲染输入控件，invalid/orphan 的项标红
```

- [ ] **Step 2: 创建 TemplateForm.tsx（模板新建/编辑 Modal）**

```tsx
// 工序下拉：从 /api/process 拉启用工序列表
// 模板名输入
// TemplateValueEditor 嵌入
// 保存调 createEquipmentTemplate / updateEquipmentTemplate
```

- [ ] **Step 3: 创建 TemplateList.tsx（模板列表，嵌在类型详情或独立 Modal）**

列表展示某类型下的模板，列：name、processName、status（带徽标）、操作（编辑/删除）。删除走 Popconfirm（c01，单条物理删除）。

- [ ] **Step 4: 创建 EquipmentTab.tsx（设备列表，从模板复制）**

标准 Query Table，查询字段：keyword、typeId（下拉）、isActive、status。列：code、name、equipmentTypeName、status、supplier、location、isActive、createdAt、操作。

- [ ] **Step 5: 创建 EquipmentForm.tsx（设备新建/编辑 Modal，标准 c02）**

```tsx
import { useNumberingPreview } from '@/components/Numbering/useNumberingPreview';
import CategorySelect from '@/components/Numbering/CategorySelect';
// 完全参考 customer/form.tsx 结构
// targetType: 'Equipment'
// 设备类型下拉：getActiveEquipmentTypes()
// 状态：Radio（Running/Stopped/Maintenance）
```

- [ ] **Step 6: 创建 EquipmentDetail.tsx（设备详情 Drawer）**

展示设备全部字段（含类型名）。

- [ ] **Step 7: 正式创建容器页 index.tsx（Task 15 的占位在此实现）**

接入 EquipmentTab 和 TypeTab（代码见 Task 15 Step 5）。

- [ ] **Step 8: 编译验证**

Run: `cd frontend && npx tsc --noEmit`
Expected: 无报错

- [ ] **Step 9: 手动端到端验证**

完整流程：
1. 建设备类型（含参数）→ 建运行模板（填值，校验通过）
2. 改类型参数定义 → 模板详情显示 invalid/orphan 状态
3. 编辑模板修正越界值 → 保存成功
4. 建设备实例（选类型）→ 列表展示
5. 删除类型（有设备引用）→ Modal 提示拒绝

- [ ] **Step 10: Commit**

```bash
git add frontend/src/pages/business/equipment/
git commit -m "feat(equipment): 运行模板页 + 设备实例页 + 容器页接入（前端完成）"
```

---

## Self-Review 结果

**1. Spec 覆盖检查**：
- §2 数据模型（5 实体 + 2 枚举）→ Task 1-2 ✓
- §3 校验逻辑 → Task 9（校验器）+ Task 11（模板服务应用）✓
- §4 API 设计（3 资源）→ Task 6（DTO）+ Task 10-12（服务）+ Task 13（控制器）✓
- §5 前端设计 → Task 14-17 ✓
- §6 迁移与 Seed → Task 3-5 ✓
- §7 后端文件清单 → Task 1-13 全覆盖 ✓
- §8 测试范围 → Task 9-12 的单测 ✓

**2. 占位符扫描**：Task 16-17 的前端代码给的是骨架而非完整实现（因前端 UI 代码高度依赖 Arco 组件 API 细节，完整代码需实现时参照 Arco 文档和模板）。这是有意为之——前端列表页明确要求"从模板复制"，完整代码在模板文件里。参数编辑器和模板值编辑器给了接口和关键逻辑，实现时填充 Arco 细节。**这是可接受的**，因为：(a) 后端是逻辑核心，代码完整；(b) 前端有明确模板可复制，不是从零写。

**3. 类型一致性**：
- `EquipmentTypeByIdSpec` 在 Task 8 定义，在 Task 10/11/12 一致使用 ✓
- `EquipmentParameterValueValidator` 的 `ValidateValue`/`EvaluateStatus`/`ParseOptions`/`SerializeOptions` 方法名在 Task 9 定义，Task 10/11 一致调用 ✓
- DTO 属性名（`ParameterDefinitionDto.Id` 可空、`TemplateValueDto.ParameterId/Value`）在 Task 6 定义，Task 10/11 服务层一致使用 ✓
- 前端 API 函数名（`getEquipmentTypes`/`createEquipmentType` 等）在 Task 14 定义，Task 16/17 一致引用 ✓

**4. 已知需实现时注意的点**：
- Task 10 `GetByIdAsync` 的 `ProcessName` 暂留空字符串——模板列表的 ProcessName 需要补查 Process。已在 Task 11 的 `GetListAsync`/`GetByIdAsync` 里通过 `GetProcessNames` 辅助方法实现。Task 10 类型详情里的模板摘要 ProcessName 同样需要补查，实现时注意（可注入 IProcessService 或直接用 IRepository<Process>）。
- Task 11 `GetProcessNames` 用 `ListAsync()` 全量查后过滤——参数化类型量不大时可接受，如需优化可加 `ProcessByIdsSpec`。
- Task 3 Step 4 的 `EquipmentTemplateValue → EquipmentTypeParameter` FK：计划已改为不配置 `HasOne<>().WithMany()` 导航关系（用裸 Guid FK，跨聚合引用风格）。这样删参数定义不会触发数据库级联，存量值保留为孤儿。**实现 Task 10 时必须验证此场景**：删参数后 SaveChanges 不报错，且模板值仍存在（读时返回 orphan）。Task 11 的 `GetById_OrphanValue_ReturnsOrphanStatus` 测试已覆盖此场景——若该测试失败，说明 EF InMemory 与真实 PG 行为不一致，需调整 EF 配置。
