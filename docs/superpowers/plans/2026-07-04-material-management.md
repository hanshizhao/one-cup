# 物料管理模块 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 实现物料(染化料/助剂/原材料)管理 CRUD 模块,物料编号在创建事务内由编号引擎生成;前端列表页遵守列表查询页标准,新建走 c02 编号预览流程,删除走 c01 Popconfirm。

**Architecture:** 后端完全复刻 `ColorService`(事务内取号)+ `Customer`(物理删除)模式 —— Repository + Specification + UnitOfWork,无新架构决策。前端复刻 `customer` 页面(单 Card 列表 + Modal 表单 + Drawer 详情)。物料是三个并行任务里唯一零新增 Guid 的模块(权限 305-308、目标类型 0202 全在 main 基线)。

**Tech Stack:** .NET 10 + EF Core + PostgreSQL(后端);Vite + React + TypeScript + Arco Design Pro(前端)。测试:xUnit + EF Core InMemory + 内置 Assert,无 Moq/FluentAssertions。

## Global Constraints

- **不新增任何 Guid**:权限复用 `PermMaterialRead/Create/Update/Delete`(305-308)、目标类型复用 `TargetTypeMaterial`(0202)、`NumberTargetTypes.Material="material"`,全部已在 main 基线(`SeedData.cs`)。
- **不改 `SeedData.cs` / `NumberTargetTypes.cs` / `Program.cs` AddPolicy 块**(物料种子已在 main,合同 4.1 红线)。
- **EF 迁移命名 `AddMaterialModule`**(合同 3.2,防撞名)。
- **后端列名 snake_case**(`HasColumnName`),表名 `materials`(复数)。
- **遵守 `docs/parallel-dev-contract-v2.md` 任务①文件边界**:只动物料 per-file + 末尾追加共享文件。
- **工作目录**:`.worktrees/material-mgmt/`,分支 `feat/material-mgmt`。
- **前端列表页必须从 `docs/specs/templates/query-table-page.template.tsx` 复制改造**(AGENTS.md 强制),不从零手写。
- **删除走 Convention c01**(单条物理删除 → Popconfirm);**新建走 Convention c02**(previewCode → 只读回填 → 无规则禁用表单)。
- **UnitId 仅 FK 无导航属性**(设计 1.2 节决策);外键级联 = Restrict。
- **后端构建**:`dotnet build backend/OneCup.sln`;**测试**:`dotnet test backend/OneCup.sln`;**前端构建**:`cd frontend && npm run build`。
- **后端校验用 FluentValidation**(对齐 Customer,非 Color):`CreateMaterialRequestValidator`/`UpdateMaterialRequestValidator` 两个 `AbstractValidator` 类,MaterialService 构造函数注入 `IValidator<Create/UpdateMaterialRequest>` 并在 Create/Update 首行调 `EnsureValidAsync`。校验器由 Program.cs:66 `AddValidatorsFromAssembly` 自动注册,无需改 Program.cs。
- **Update 走整表覆盖式 PUT,不做 null-skip**(对齐 CustomerService.UpdateAsync):UpdateDto 可空字段(`UnitId`/`Remark` 等)的 `null` 表示"置空"而非"不修改",否则前端清空可选字段会失效。必填字段(`Name`/`Spec`/`Category`)的 `null` 防御性保持原值(`request.X ?? entity.X`)。

---

## File Structure

**后端新建(per-file 零冲突):**
- `backend/src/OneCup.Domain/Entities/Material.cs` — 实体(8 字段,继承 BaseEntity)
- `backend/src/OneCup.Application/Dtos/System/MaterialDtos.cs` — DTO(Create/Update/UpdateStatus/Dto)
- `backend/src/OneCup.Application/Specifications/MaterialSpecs.cs` — 查询规范(5 个 Spec)
- `backend/src/OneCup.Application/Validators/System/CreateMaterialRequestValidator.cs` — 新建校验
- `backend/src/OneCup.Application/Validators/System/UpdateMaterialRequestValidator.cs` — 编辑校验
- `backend/src/OneCup.Application/Interfaces/IMaterialService.cs` — 服务接口
- `backend/src/OneCup.Application/Services/MaterialService.cs` — 服务实现
- `backend/src/OneCup.Infrastructure/Persistence/Configurations/MaterialConfiguration.cs` — EF 配置
- `backend/src/OneCup.Api/Controllers/MaterialController.cs` — 控制器(文件名单数,路由复数)
- `backend/tests/OneCup.UnitTests/Material/MaterialServiceTests.cs` — 服务测试
- `backend/tests/OneCup.UnitTests/Material/MaterialSpecsTests.cs` — 规范测试

**后端修改(共享文件,末尾追加):**
- `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs` — 加 `DbSet<Material>`
- `backend/src/OneCup.Api/Program.cs` — 加 `AddScoped<IMaterialService, MaterialService>()`

**EF 迁移(EF 生成,合并期走合同 6.4):**
- `backend/src/OneCup.Infrastructure/Migrations/{ts}_AddMaterialModule.cs` + `.Designer.cs`
- `backend/src/OneCup.Infrastructure/Migrations/OneCupDbContextModelSnapshot.cs`(自动刷新)

**前端新建(per-file 零冲突):**
- `frontend/src/api/material.ts` — API 模块
- `frontend/src/pages/business/material/index.tsx` — 列表页
- `frontend/src/pages/business/material/form.tsx` — 新建/编辑 Modal
- `frontend/src/pages/business/material/detail.tsx` — 详情 Drawer
- `frontend/src/pages/business/material/locale/{index,en-US,zh-CN}.ts` — 页面级 i18n
- `frontend/src/pages/business/material/style/index.module.less` — 样式

**前端复用(已存在,不改):**
- `frontend/src/api/measurementUnit.ts` — 已有 `getAllActiveUnits()` 返回 `MeasurementUnit[]`(字段含 `id`/`nameZh`/`nameEn`/`symbol`,**注意无 `name` 字段**,中文单位名是 `nameZh`)。

**前端修改(共享文件,末尾追加):**
- `frontend/src/routes.ts` — `menu.business.children` 追加物料菜单项
- `frontend/src/router.tsx` — lazy import + 路由项
- `frontend/src/locale/index.ts` — `menu.business.material` 文案(en-US + zh-CN)

---

## Task 1: Material 实体 + EF 配置

**Files:**
- Create: `backend/src/OneCup.Domain/Entities/Material.cs`
- Create: `backend/src/OneCup.Infrastructure/Persistence/Configurations/MaterialConfiguration.cs`

**Interfaces:**
- Produces: `Material` 实体类(`: BaseEntity`),字段 `Code/Name/Spec/Category/UnitId/Remark/SortOrder/IsActive`。供 Task 4(Specs)、Task 6(Service)、Task 8(DbContext)、Task 10/11(测试)使用;EF 配置在本 Task 内。
- Consumes: `BaseEntity`(OneCup.Domain,提供 Id/CreatedAt/UpdatedAt)、`MeasurementUnit`(已有实体,FK 目标)。

- [ ] **Step 1: 创建 Material 实体**

Create `backend/src/OneCup.Domain/Entities/Material.cs`:

```csharp
namespace OneCup.Domain.Entities;

/// <summary>
/// 物料(染化料/助剂/原材料)。坯布面料生产过程中的投入品。
/// code 创建后不可改,作为业务模块的稳定引用标识符。
/// </summary>
public class Material : BaseEntity
{
    /// <summary>编码,如 DYE-0001。创建后不可改</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>名称,如"活性红 3B"</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>规格型号,如"粉末 100%"</summary>
    public string Spec { get; set; } = string.Empty;

    /// <summary>原料类别(自由文本),如"助剂/染料/原材料"</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>计量单位 Id,关联 measurement_units;可空以兼容"暂未定单位"</summary>
    public Guid? UnitId { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>排序号</summary>
    public int SortOrder { get; set; }

    /// <summary>启停状态(停用后引用方按需处理,可物理删除)</summary>
    public bool IsActive { get; set; } = true;
}
```

- [ ] **Step 2: 创建 EF 配置**

Create `backend/src/OneCup.Infrastructure/Persistence/Configurations/MaterialConfiguration.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using OneCup.Domain.Entities;

namespace OneCup.Infrastructure.Persistence.Configurations;

public class MaterialConfiguration : IEntityTypeConfiguration<Material>
{
    public void Configure(EntityTypeBuilder<Material> builder)
    {
        builder.ToTable("materials");
        builder.HasKey(m => m.Id);

        builder.Property(m => m.Id).HasColumnName("id");
        builder.Property(m => m.Code).HasColumnName("code").HasMaxLength(32).IsRequired();
        builder.Property(m => m.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        builder.Property(m => m.Spec).HasColumnName("spec").HasMaxLength(100).IsRequired();
        builder.Property(m => m.Category).HasColumnName("category").HasMaxLength(32).IsRequired();
        builder.Property(m => m.UnitId).HasColumnName("unit_id");
        builder.Property(m => m.Remark).HasColumnName("remark").HasMaxLength(256);
        builder.Property(m => m.SortOrder).HasColumnName("sort_order").IsRequired();
        builder.Property(m => m.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(m => m.CreatedAt).HasColumnName("created_at");
        builder.Property(m => m.UpdatedAt).HasColumnName("updated_at");

        builder.HasIndex(m => m.Code)
            .HasDatabaseName("ux_materials_code")
            .IsUnique();

        // 仅 FK 无导航属性(设计 1.2 节);级联 Restrict,防止删单位连带删物料
        builder.HasOne<MeasurementUnit>()
            .WithMany()
            .HasForeignKey(m => m.UnitId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCEEDED(此时实体/配置未被 DbContext 发现,但能编译通过;DbSet 在 Task 8 加)。

- [ ] **Step 4: Commit**

```bash
git add backend/src/OneCup.Domain/Entities/Material.cs backend/src/OneCup.Infrastructure/Persistence/Configurations/MaterialConfiguration.cs
git commit -m "feat(material): 实体 + EF 配置"
```

---

## Task 2: DTO

**Files:**
- Create: `backend/src/OneCup.Application/Dtos/System/MaterialDtos.cs`

**Interfaces:**
- Produces: `CreateMaterialRequest` / `UpdateMaterialRequest` / `UpdateMaterialStatusRequest` / `MaterialDto`。供 Task 3(校验器)、Task 6(Service)、Task 7(Controller)使用。
- Consumes: 无。

- [ ] **Step 1: 创建 DTO 文件**

Create `backend/src/OneCup.Application/Dtos/System/MaterialDtos.cs`:

```csharp
namespace OneCup.Application.Dtos.System;

/// <summary>新建物料请求。Code 不在此处——由系统在事务内经编号引擎生成;IsActive 默认 true。</summary>
public record CreateMaterialRequest
{
    public string Name { get; init; } = string.Empty;
    public string Spec { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public Guid? UnitId { get; init; }
    public string? Remark { get; init; }
    public int SortOrder { get; init; }
}

/// <summary>更新物料请求。全可空,部分更新;Code 不可改(不在此处),IsActive 走状态接口。</summary>
public record UpdateMaterialRequest
{
    public string? Name { get; init; }
    public string? Spec { get; init; }
    public string? Category { get; init; }
    public Guid? UnitId { get; init; }
    public string? Remark { get; init; }
    public int? SortOrder { get; init; }
}

public record UpdateMaterialStatusRequest
{
    public bool IsActive { get; init; }
}

public class MaterialDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Spec { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public Guid? UnitId { get; set; }
    public string? Remark { get; set; }
    public int SortOrder { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Application/Dtos/System/MaterialDtos.cs
git commit -m "feat(material): DTO"
```

---

## Task 3: FluentValidation 校验器

**Files:**
- Create: `backend/src/OneCup.Application/Validators/System/CreateMaterialRequestValidator.cs`
- Create: `backend/src/OneCup.Application/Validators/System/UpdateMaterialRequestValidator.cs`

**Interfaces:**
- Produces: `CreateMaterialRequestValidator` / `UpdateMaterialRequestValidator`(FluentValidation `AbstractValidator`)。供 Task 6(MaterialService)注入调用。`AddValidatorsFromAssembly` 已在 Program.cs:66 注册,自动被发现。
- Consumes: `CreateMaterialRequest`/`UpdateMaterialRequest`(Task 2)、`FluentValidation`。

- [ ] **Step 1: 创建 Create 校验器**

Create `backend/src/OneCup.Application/Validators/System/CreateMaterialRequestValidator.cs`:

```csharp
using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>新建物料请求校验。Name/Spec/Category 必填且限长(对齐 Customer 校验风格)。</summary>
public class CreateMaterialRequestValidator : AbstractValidator<CreateMaterialRequest>
{
    public CreateMaterialRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Spec).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(32);
        RuleFor(x => x.Remark).MaximumLength(256).When(x => !string.IsNullOrEmpty(x.Remark));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0);
    }
}
```

- [ ] **Step 2: 创建 Update 校验器**

Create `backend/src/OneCup.Application/Validators/System/UpdateMaterialRequestValidator.cs`:

```csharp
using FluentValidation;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Validators.System;

/// <summary>编辑物料请求校验(字段约束同 Create,必填字段在 null 时跳过以支持部分更新语义)。</summary>
public class UpdateMaterialRequestValidator : AbstractValidator<UpdateMaterialRequest>
{
    public UpdateMaterialRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(100).When(x => x.Name is not null);
        RuleFor(x => x.Spec).NotEmpty().MaximumLength(100).When(x => x.Spec is not null);
        RuleFor(x => x.Category).NotEmpty().MaximumLength(32).When(x => x.Category is not null);
        RuleFor(x => x.Remark).MaximumLength(256).When(x => !string.IsNullOrEmpty(x.Remark));
        RuleFor(x => x.SortOrder).GreaterThanOrEqualTo(0).When(x => x.SortOrder is not null);
    }
}
```

- [ ] **Step 3: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 4: Commit**

```bash
git add backend/src/OneCup.Application/Validators/System/CreateMaterialRequestValidator.cs backend/src/OneCup.Application/Validators/System/UpdateMaterialRequestValidator.cs
git commit -m "feat(material): FluentValidation 校验器"
```

---

## Task 4: Specifications

**Files:**
- Create: `backend/src/OneCup.Application/Specifications/MaterialSpecs.cs`

**Interfaces:**
- Produces: `MaterialFilterSpec` / `MaterialPagedSpec` / `MaterialActiveSpec` / `MaterialByIdSpec` / `MaterialByCodeSpec`。供 Task 6(Service)、Task 10/11(测试)使用。
- Consumes: `Specification<T>`(Ardalis 基类,OneCup.Application.Specifications namespace)、`Material` 实体(Task 1)。

- [ ] **Step 1: 创建 Specs 文件**

Create `backend/src/OneCup.Application/Specifications/MaterialSpecs.cs`:

```csharp
using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>物料过滤规格(仅 keyword/category/isActive,不含分页)。用于 CountAsync 统计总数。</summary>
/// <remarks>多条件组合为单一 predicate 调一次 ApplyCriteria(基类覆盖语义)。</remarks>
public class MaterialFilterSpec : Specification<Material>
{
    public MaterialFilterSpec(string? keyword, string? category, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        var cat = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        ApplyCriteria(m =>
            (kw == null || m.Code.Contains(kw) || m.Name.Contains(kw) || m.Spec.Contains(kw)) &&
            (cat == null || m.Category == cat) &&
            (isActive == null || m.IsActive == isActive.Value));
    }
}

/// <summary>物料分页查询(含 keyword/category/isActive 过滤,按 SortOrder 升序)。</summary>
public class MaterialPagedSpec : Specification<Material>
{
    public MaterialPagedSpec(string? keyword, string? category, bool? isActive, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        var cat = string.IsNullOrWhiteSpace(category) ? null : category.Trim();
        ApplyCriteria(m =>
            (kw == null || m.Code.Contains(kw) || m.Name.Contains(kw) || m.Spec.Contains(kw)) &&
            (cat == null || m.Category == cat) &&
            (isActive == null || m.IsActive == isActive.Value));
        ApplyOrderBy(m => m.SortOrder);
        ApplyPaging(page, pageSize);
    }
}

/// <summary>物料全部启用项(前端下拉用,按 SortOrder 升序)。</summary>
public class MaterialActiveSpec : Specification<Material>
{
    public MaterialActiveSpec()
    {
        ApplyCriteria(m => m.IsActive);
        ApplyOrderBy(m => m.SortOrder);
    }
}

public class MaterialByIdSpec : Specification<Material>
{
    public MaterialByIdSpec(Guid id) => ApplyCriteria(m => m.Id == id);
}

/// <summary>按 code 查找物料(可选排除自身 Id)。不含 IsActive 过滤——用于 code 唯一性校验。</summary>
public class MaterialByCodeSpec : Specification<Material>
{
    public MaterialByCodeSpec(string code, Guid? excludingId = null)
    {
        var exclude = excludingId;
        ApplyCriteria(m => m.Code == code && (exclude == null || m.Id != exclude.Value));
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Application/Specifications/MaterialSpecs.cs
git commit -m "feat(material): Specifications"
```

---

## Task 5: IMaterialService 接口

**Files:**
- Create: `backend/src/OneCup.Application/Interfaces/IMaterialService.cs`

**Interfaces:**
- Produces: `IMaterialService`(7 方法签名)。供 Task 6(实现)、Task 7(Controller)、Task 8(DI 注册)使用。
- Consumes: `PagedResult<T>`(OneCup.Application.Common)、`MaterialDto`/`Create*Request`(Task 2)。

- [ ] **Step 1: 创建接口文件**

Create `backend/src/OneCup.Application/Interfaces/IMaterialService.cs`:

```csharp
using OneCup.Application.Common;
using OneCup.Application.Dtos.System;

namespace OneCup.Application.Interfaces;

/// <summary>
/// 物料管理服务(CRUD + 启停 + 物理删除)。
/// code 创建后不可改;CreateAsync 在事务内经编号引擎取号。
/// </summary>
public interface IMaterialService
{
    Task<PagedResult<MaterialDto>> GetMaterialsAsync(
        int page, int pageSize, string? keyword, string? category, bool? isActive,
        CancellationToken ct = default);

    Task<List<MaterialDto>> GetAllActiveMaterialsAsync(CancellationToken ct = default);

    Task<MaterialDto?> GetMaterialAsync(Guid id, CancellationToken ct = default);

    Task<MaterialDto> CreateMaterialAsync(CreateMaterialRequest request, CancellationToken ct = default);

    Task UpdateMaterialAsync(Guid id, UpdateMaterialRequest request, CancellationToken ct = default);

    Task UpdateMaterialStatusAsync(Guid id, bool isActive, CancellationToken ct = default);

    Task DeleteMaterialAsync(Guid id, CancellationToken ct = default);
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Application/Interfaces/IMaterialService.cs
git commit -m "feat(material): IMaterialService 接口"
```

---

## Task 6: MaterialService 实现

**Files:**
- Create: `backend/src/OneCup.Application/Services/MaterialService.cs`

**Interfaces:**
- Produces: `MaterialService` 类(实现 `IMaterialService`)。供 Task 7(Controller)、Task 8(DI)、Task 10(测试)使用。
- Consumes:
  - `IRepository<Material>`(`Task<T?> FirstOrDefaultAsync(ISpecification<T>, ct)` / `Task AddAsync(T, ct)` / `void Remove(T)` / `Task<IReadOnlyList<T>> ListAsync(ISpecification<T>, ct)` / `Task<int> CountAsync(ISpecification<T>, ct)`)
  - `IUnitOfWork`(`Task<int> SaveChangesAsync(ct)` / `Task ExecuteInTransactionAsync(Func<Task>, ct)`)
  - `INumberingService`(`Task<string> GenerateAsync(string targetType, string? categoryCode, ct)` —— 注意返回 `string` 非 nullable)
  - `IValidator<CreateMaterialRequest>` / `IValidator<UpdateMaterialRequest>`(FluentValidation,Task 3 产出;`EnsureValidAsync` 扩展在 `OneCup.Application.Common.ValidationExtensions`)
  - `NumberTargetTypes.Material`(常量 `"material"`,已在 main)
  - `Material` 实体(Task 1)、`MaterialDtos`(Task 2)、`MaterialSpecs`(Task 4)
  - `DomainException`(OneCup.Domain.Exceptions)

- [ ] **Step 1: 创建 Service 实现**

Create `backend/src/OneCup.Application/Services/MaterialService.cs`:

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
/// 物料管理服务实现。通过 IRepository + Specification 访问数据,IUnitOfWork 提交。
/// CreateAsync 在事务内经编号引擎取号(并发安全、不跳号);code 创建后不可改;
/// 支持启停 + 物理删除(物料有 material:delete 权限)。
/// </summary>
public class MaterialService : IMaterialService
{
    private readonly IRepository<Material> _materials;
    private readonly IUnitOfWork _uow;
    private readonly INumberingService _numbering;
    private readonly IValidator<CreateMaterialRequest> _createValidator;
    private readonly IValidator<UpdateMaterialRequest> _updateValidator;

    public MaterialService(
        IRepository<Material> materials,
        IUnitOfWork uow,
        INumberingService numbering,
        IValidator<CreateMaterialRequest> createValidator,
        IValidator<UpdateMaterialRequest> updateValidator)
    {
        _materials = materials;
        _uow = uow;
        _numbering = numbering;
        _createValidator = createValidator;
        _updateValidator = updateValidator;
    }

    public async Task<PagedResult<MaterialDto>> GetMaterialsAsync(
        int page, int pageSize, string? keyword, string? category, bool? isActive,
        CancellationToken ct = default)
    {
        // 关键:总数用仅含过滤条件的 FilterSpec 统计,绝不能用带分页的 PagedSpec,
        // 否则 Repository.CountAsync 会应用 Skip/Take,只统计当前页子集。
        var total = await _materials.CountAsync(
            new MaterialFilterSpec(keyword, category, isActive), ct);
        var materials = await _materials.ListAsync(
            new MaterialPagedSpec(keyword, category, isActive, page, pageSize), ct);

        return new PagedResult<MaterialDto>
        {
            Items = materials.Select(ToDto).ToList(),
            Total = total,
            Page = page,
            PageSize = pageSize,
        };
    }

    public async Task<List<MaterialDto>> GetAllActiveMaterialsAsync(CancellationToken ct = default)
    {
        var materials = await _materials.ListAsync(new MaterialActiveSpec(), ct);
        return materials.Select(ToDto).ToList();
    }

    public async Task<MaterialDto?> GetMaterialAsync(Guid id, CancellationToken ct = default)
    {
        var m = await _materials.FirstOrDefaultAsync(new MaterialByIdSpec(id), ct);
        return m is null ? null : ToDto(m);
    }

    public async Task<MaterialDto> CreateMaterialAsync(CreateMaterialRequest request, CancellationToken ct = default)
    {
        await _createValidator.EnsureValidAsync(request, ct);
        Guid createdId = Guid.Empty;
        await _uow.ExecuteInTransactionAsync(async () =>
        {
            // 事务内经编号引擎取号(行锁),计数器增量与物料记录一起提交(不跳号)
            var code = await _numbering.GenerateAsync(NumberTargetTypes.Material, null, ct);
            var entity = new Material
            {
                Code = code,
                Name = request.Name,
                Spec = request.Spec,
                Category = request.Category,
                UnitId = request.UnitId,
                Remark = request.Remark,
                SortOrder = request.SortOrder,
                IsActive = true,
            };
            await _materials.AddAsync(entity, ct);
            await _uow.SaveChangesAsync(ct);
            createdId = entity.Id;
        }, ct);

        return await GetMaterialAsync(createdId, ct) ?? throw new DomainException("物料创建失败");
    }

    public async Task UpdateMaterialAsync(Guid id, UpdateMaterialRequest request, CancellationToken ct = default)
    {
        await _updateValidator.EnsureValidAsync(request, ct);
        // 整表覆盖式 PUT(对齐 CustomerService.UpdateAsync),不做 null-skip:
        // 否则用户在前端清空可选字段(如计量单位/备注)时,提交 null 会被当成"不修改",
        // 导致字段无法清空。UpdateDto 字段可空(string?/Guid?/int?)正是为了让 null 合法穿透到赋值。
        // code 不可改:更新接口不暴露 Code 字段。
        var entity = await _materials.FirstOrDefaultAsync(new MaterialByIdSpec(id), ct)
            ?? throw new DomainException("物料不存在");

        entity.Name = request.Name ?? entity.Name;          // 必填字段:null 时保持原值(防御性)
        entity.Spec = request.Spec ?? entity.Spec;
        entity.Category = request.Category ?? entity.Category;
        entity.UnitId = request.UnitId;                      // 可空字段:直接赋值,允许清空
        entity.Remark = request.Remark;                      // 可空字段:直接赋值,允许清空
        entity.SortOrder = request.SortOrder ?? entity.SortOrder;

        await _uow.SaveChangesAsync(ct);
    }

    public async Task UpdateMaterialStatusAsync(Guid id, bool isActive, CancellationToken ct = default)
    {
        var entity = await _materials.FirstOrDefaultAsync(new MaterialByIdSpec(id), ct)
            ?? throw new DomainException("物料不存在");
        entity.IsActive = isActive;
        await _uow.SaveChangesAsync(ct);
    }

    public async Task DeleteMaterialAsync(Guid id, CancellationToken ct = default)
    {
        var entity = await _materials.FirstOrDefaultAsync(new MaterialByIdSpec(id), ct)
            ?? throw new DomainException("物料不存在");
        _materials.Remove(entity);
        await _uow.SaveChangesAsync(ct);
    }

    // ── 内部工具 ──

    private static MaterialDto ToDto(Material m) => new()
    {
        Id = m.Id,
        Code = m.Code,
        Name = m.Name,
        Spec = m.Spec,
        Category = m.Category,
        UnitId = m.UnitId,
        Remark = m.Remark,
        SortOrder = m.SortOrder,
        IsActive = m.IsActive,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
    };
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Application/Services/MaterialService.cs
git commit -m "feat(material): MaterialService 实现(事务内取号 + 物理删除)"
```

---

## Task 7: MaterialController

**Files:**
- Create: `backend/src/OneCup.Api/Controllers/MaterialController.cs`

**Interfaces:**
- Produces: `MaterialController`(HTTP 端点,路由 `api/materials`)。供 Task 12(前端 API)调用。
- Consumes: `IMaterialService`(Task 5/6)、各 DTO(Task 2)。权限策略 `material:read/create/update/delete`(已在 main)。

- [ ] **Step 1: 创建 Controller**

Create `backend/src/OneCup.Api/Controllers/MaterialController.cs`:

```csharp
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OneCup.Application.Dtos.System;
using OneCup.Application.Interfaces;

namespace OneCup.Api.Controllers;

/// <summary>
/// 物料管理端点。
/// 权限:material:read / material:create / material:update / material:delete(策略名 = 权限码)。
/// </summary>
[ApiController]
[Route("api/materials")]
public class MaterialController : ControllerBase
{
    private readonly IMaterialService _svc;

    public MaterialController(IMaterialService svc)
    {
        _svc = svc;
    }

    [HttpGet]
    [Authorize(Policy = "material:read")]
    public async Task<IActionResult> GetMaterials(
        [FromQuery] int page = 1, [FromQuery] int pageSize = 10,
        [FromQuery] string? keyword = null, [FromQuery] string? category = null,
        [FromQuery] bool? isActive = null,
        CancellationToken ct = default)
    {
        var result = await _svc.GetMaterialsAsync(page, pageSize, keyword, category, isActive, ct);
        return Ok(result);
    }

    [HttpGet("all")]
    [Authorize(Policy = "material:read")]
    public async Task<IActionResult> GetAllActiveMaterials(CancellationToken ct)
    {
        var list = await _svc.GetAllActiveMaterialsAsync(ct);
        return Ok(list);
    }

    [HttpGet("{id:guid}")]
    [Authorize(Policy = "material:read")]
    public async Task<IActionResult> GetMaterial(Guid id, CancellationToken ct)
    {
        var dto = await _svc.GetMaterialAsync(id, ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Policy = "material:create")]
    public async Task<IActionResult> CreateMaterial([FromBody] CreateMaterialRequest request, CancellationToken ct)
    {
        var dto = await _svc.CreateMaterialAsync(request, ct);
        return CreatedAtAction(nameof(GetMaterial), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Policy = "material:update")]
    public async Task<IActionResult> UpdateMaterial(Guid id, [FromBody] UpdateMaterialRequest request, CancellationToken ct)
    {
        await _svc.UpdateMaterialAsync(id, request, ct);
        return NoContent();
    }

    [HttpPut("{id:guid}/status")]
    [Authorize(Policy = "material:update")]
    public async Task<IActionResult> UpdateMaterialStatus(Guid id, [FromBody] UpdateMaterialStatusRequest request, CancellationToken ct)
    {
        await _svc.UpdateMaterialStatusAsync(id, request.IsActive, ct);
        return NoContent();
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Policy = "material:delete")]
    public async Task<IActionResult> DeleteMaterial(Guid id, CancellationToken ct)
    {
        await _svc.DeleteMaterialAsync(id, ct);
        return NoContent();
    }
}
```

- [ ] **Step 2: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 3: Commit**

```bash
git add backend/src/OneCup.Api/Controllers/MaterialController.cs
git commit -m "feat(material): MaterialController(api/materials)"
```

---

## Task 8: DbContext 加 DbSet + Program.cs 加 DI

**Files:**
- Modify: `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`
- Modify: `backend/src/OneCup.Api/Program.cs`

**Interfaces:**
- Produces: `OneCupDbContext.Materials` DbSet(供迁移识别实体)、DI 注册 `IMaterialService`。
- Consumes: `Material` 实体(Task 1)、`MaterialService`/`IMaterialService`(Task 4/5)。

- [ ] **Step 1: DbContext 加 DbSet**

在 `OneCupDbContext.cs` 文件**末尾**的 DbSet 声明区追加(参照现有 `// ===== Unit 模块 =====` 风格,在最后一个 DbSet 后追加)。

用 Edit 工具,找到:
```csharp
    // ===== Unit 模块 =====
    public DbSet<MeasurementUnit> MeasurementUnits => Set<MeasurementUnit>();
```
替换为:
```csharp
    // ===== Unit 模块 =====
    public DbSet<MeasurementUnit> MeasurementUnits => Set<MeasurementUnit>();

    // ===== Material 模块 =====
    public DbSet<Material> Materials => Set<Material>();
```

> 注:`OnModelCreating` 用 `ApplyConfigurationsFromAssembly` 自动扫描,无需手动 ApplyConfiguration。

- [ ] **Step 2: Program.cs 加 AddScoped**

在 `Program.cs` 的 AddScoped 区末尾(参照现有 `// ===== Unit 模块 =====` 块之后)追加。

用 Edit 工具,找到:
```csharp
    // ===== Unit 模块 =====
    builder.Services.AddScoped<IMeasurementUnitService, MeasurementUnitService>();
```
替换为:
```csharp
    // ===== Unit 模块 =====
    builder.Services.AddScoped<IMeasurementUnitService, MeasurementUnitService>();

    // ===== Material 模块 =====
    builder.Services.AddScoped<IMaterialService, MaterialService>();
```

> 注:**不碰 AddPolicy 块**——material:read/create/update/delete 四条已在 main(155-158 行)。

- [ ] **Step 3: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 4: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs backend/src/OneCup.Api/Program.cs
git commit -m "feat(material): DbContext DbSet + DI 注册"
```

---

## Task 9: EF 迁移 AddMaterialModule

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Migrations/{ts}_AddMaterialModule.cs` (+ `.Designer.cs`)
- Modify: `backend/src/OneCup.Infrastructure/Migrations/OneCupDbContextModelSnapshot.cs`(EF 自动)

**Interfaces:** 无(EF 生成文件,合并期走合同 6.4)。

- [ ] **Step 1: 生成迁移**

Run:
```bash
cd backend
dotnet ef migrations add AddMaterialModule --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```
Expected: 生成 `{ts}_AddMaterialModule.cs` + `.Designer.cs`,ModelSnapshot 自动刷新。打开生成的 `Up()` 确认含 `CreateTable("materials", ...)` + `CreateIndex("ux_materials_code")` + `AddForeignKey("fk_materials_unit_id")`。

- [ ] **Step 2: 应用本地库验证**

Run:
```bash
cd backend
dotnet ef database update --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```
Expected: `Done.`(迁移成功应用,materials 表创建)。

- [ ] **Step 3: 编译验证**

Run: `dotnet build backend/OneCup.sln`
Expected: BUILD SUCCEEDED

- [ ] **Step 4: Commit**

```bash
git add backend/src/OneCup.Infrastructure/Migrations/
git commit -m "feat(material): EF 迁移 AddMaterialModule"
```

---

## Task 10: MaterialService 单元测试

**Files:**
- Create: `backend/tests/OneCup.UnitTests/Material/MaterialServiceTests.cs`

**Interfaces:**
- Consumes: `MaterialService`(Task 6)、`Material` 实体、各 DTO(Task 2)、`INumberingService`/`IUnitOfWork`/`IRepository<T>`、`Repository<T>`/`UnitOfWork`(真实实现)、`OneCupDbContext`、`NumberTargetTypes.Material`、`DomainException`、`Create/UpdateMaterialRequestValidator`(Task 3)。
- 测试基础设施:xUnit `[Fact]` + 内置 `Assert`、EF Core InMemory、手写 `FakeNumberingService`(照搬 Color 测试,改前缀为 `MAT-`)。

- [ ] **Step 1: 创建服务测试文件**

Create `backend/tests/OneCup.UnitTests/Material/MaterialServiceTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Dtos.System;
using OneCup.Application.Validators.System;
using OneCup.Domain.Entities;
using OneCup.Domain.Exceptions;
using OneCup.Application.Interfaces;
using OneCup.Application.Services;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Material;

public class MaterialServiceTests
{
    private static (OneCupDbContext db, MaterialService svc, FakeNumberingService numbering) Setup()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"material-{Guid.NewGuid()}")
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .UseInternalServiceProvider(BuildServiceProvider())
            .Options);
        var numbering = new FakeNumberingService();
        var svc = new MaterialService(
            new Repository<Domain.Entities.Material>(db),
            new UnitOfWork(db),
            numbering,
            new CreateMaterialRequestValidator(),
            new UpdateMaterialRequestValidator());
        return (db, svc, numbering);
    }

    private static IServiceProvider BuildServiceProvider()
    {
        var services = new ServiceCollection();
        services.AddEntityFrameworkInMemoryDatabase();
        return services.BuildServiceProvider();
    }

    private static CreateMaterialRequest ValidCreate() => new()
    {
        Name = "活性红 3B", Spec = "粉末 100%", Category = "染料", SortOrder = 1,
    };

    // ── 新增 ──

    [Fact]
    public async Task CreateMaterialAsync_CreatesMaterial_WithGeneratedCode()
    {
        var (_, svc, numbering) = Setup();
        numbering.NextCode = "MAT-0001";

        var dto = await svc.CreateMaterialAsync(ValidCreate());
        Assert.Equal("MAT-0001", dto.Code);   // code 由编号引擎生成
        Assert.Equal("活性红 3B", dto.Name);
        Assert.Equal("粉末 100%", dto.Spec);
        Assert.Equal("染料", dto.Category);
        Assert.True(dto.IsActive);
        Assert.Null(dto.UnitId);   // 默认无单位
    }

    [Fact]
    public async Task CreateMaterialAsync_WithUnitId_StoresUnitId()
    {
        var (_, svc, _) = Setup();
        var unitId = Guid.NewGuid();
        var dto = await svc.CreateMaterialAsync(ValidCreate() with { UnitId = unitId });
        Assert.Equal(unitId, dto.UnitId);
    }

    [Fact]
    public async Task CreateMaterialAsync_EmptyName_ThrowsValidation()
    {
        // FluentValidation 拦截:Name 必填,空串/空白抛 ValidationException
        var (_, svc, _) = Setup();
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            svc.CreateMaterialAsync(ValidCreate() with { Name = "" }));
    }

    [Fact]
    public async Task CreateMaterialAsync_OverlongSpec_ThrowsValidation()
    {
        var (_, svc, _) = Setup();
        var tooLong = new string('x', 101);   // Spec 上限 100
        await Assert.ThrowsAsync<FluentValidation.ValidationException>(() =>
            svc.CreateMaterialAsync(ValidCreate() with { Spec = tooLong }));
    }

    // ── 编辑 ──

    [Fact]
    public async Task UpdateMaterialAsync_CodeImmutable_FieldsUpdatable()
    {
        var (_, svc, numbering) = Setup();
        numbering.NextCode = "MAT-0001";
        var dto = await svc.CreateMaterialAsync(ValidCreate());

        var unitId = Guid.NewGuid();
        await svc.UpdateMaterialAsync(dto.Id, new UpdateMaterialRequest
        {
            Name = "活性红改", Spec = "液体 50%", Category = "助剂",
            UnitId = unitId, Remark = "备注", SortOrder = 5,
        });

        var updated = await svc.GetMaterialAsync(dto.Id);
        Assert.Equal("MAT-0001", updated!.Code);   // code 不可改
        Assert.Equal("活性红改", updated.Name);
        Assert.Equal("液体 50%", updated.Spec);
        Assert.Equal("助剂", updated.Category);
        Assert.Equal(unitId, updated.UnitId);
        Assert.Equal("备注", updated.Remark);
        Assert.Equal(5, updated.SortOrder);
    }

    [Fact]
    public async Task UpdateMaterialAsync_NotFound_Throws()
    {
        var (_, svc, _) = Setup();
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.UpdateMaterialAsync(Guid.NewGuid(), new UpdateMaterialRequest { Name = "x" }));
    }

    [Fact]
    public async Task UpdateMaterialAsync_NullUnitId_ClearsUnit()
    {
        // 关键:整表覆盖式 PUT(对齐 Customer),不做 null-skip。
        // 否则用户清空单位下拉提交 null 会被当"不修改",单位无法清空。
        var (_, svc, numbering) = Setup();
        numbering.NextCode = "MAT-0001";
        var unitId = Guid.NewGuid();
        var dto = await svc.CreateMaterialAsync(ValidCreate() with { UnitId = unitId });
        Assert.Equal(unitId, dto.UnitId);

        await svc.UpdateMaterialAsync(dto.Id, new UpdateMaterialRequest { UnitId = null });
        var updated = await svc.GetMaterialAsync(dto.Id);
        Assert.Null(updated!.UnitId);   // 单位被清空,而非保留原值
    }

    // ── 启停 ──

    [Fact]
    public async Task UpdateMaterialStatusAsync_Toggles()
    {
        var (_, svc, _) = Setup();
        var dto = await svc.CreateMaterialAsync(ValidCreate());
        await svc.UpdateMaterialStatusAsync(dto.Id, false);
        var updated = await svc.GetMaterialAsync(dto.Id);
        Assert.False(updated!.IsActive);
    }

    // ── 删除 ──

    [Fact]
    public async Task DeleteMaterialAsync_RemovesEntity()
    {
        var (_, svc, _) = Setup();
        var dto = await svc.CreateMaterialAsync(ValidCreate());
        await svc.DeleteMaterialAsync(dto.Id);
        var after = await svc.GetMaterialAsync(dto.Id);
        Assert.Null(after);   // 物理删除
    }

    [Fact]
    public async Task DeleteMaterialAsync_NotFound_Throws()
    {
        var (_, svc, _) = Setup();
        await Assert.ThrowsAsync<DomainException>(() =>
            svc.DeleteMaterialAsync(Guid.NewGuid()));
    }

    // ── 查询 ──

    [Fact]
    public async Task GetMaterialsAsync_FiltersByKeyword()
    {
        var (_, svc, _) = Setup();
        await svc.CreateMaterialAsync(ValidCreate() with { Name = "活性红" });
        await svc.CreateMaterialAsync(ValidCreate() with { Name = "渗透剂" });
        var res = await svc.GetMaterialsAsync(1, 10, "活性", null, null);
        Assert.Single(res.Items);
    }

    [Fact]
    public async Task GetMaterialsAsync_FiltersByKeyword_MatchesSpec()
    {
        // keyword 也搜 spec 字段
        var (_, svc, _) = Setup();
        await svc.CreateMaterialAsync(ValidCreate() with { Name = "某染料", Spec = "粉末 100%" });
        await svc.CreateMaterialAsync(ValidCreate() with { Name = "其他", Spec = "液体 50%" });
        var res = await svc.GetMaterialsAsync(1, 10, "粉末", null, null);
        Assert.Single(res.Items);
    }

    [Fact]
    public async Task GetMaterialsAsync_FiltersByCategory()
    {
        var (_, svc, _) = Setup();
        await svc.CreateMaterialAsync(ValidCreate() with { Category = "染料" });
        await svc.CreateMaterialAsync(ValidCreate() with { Category = "染料" });
        await svc.CreateMaterialAsync(ValidCreate() with { Category = "助剂" });
        var res = await svc.GetMaterialsAsync(1, 10, null, "染料", null);
        Assert.Equal(2, res.Total);
    }

    [Fact]
    public async Task GetMaterialsAsync_TotalUnaffectedByPaging()
    {
        var (_, svc, _) = Setup();
        for (var i = 0; i < 5; i++)
            await svc.CreateMaterialAsync(ValidCreate() with { Category = "染料" });
        var res = await svc.GetMaterialsAsync(1, 2, null, "染料", null);
        Assert.Equal(2, res.Items.Count);
        Assert.Equal(5, res.Total);   // total 不受分页污染
    }

    [Fact]
    public async Task GetAllActiveMaterialsAsync_ReturnsOnlyActiveOrdered()
    {
        var (_, svc, _) = Setup();
        var a = await svc.CreateMaterialAsync(ValidCreate() with { SortOrder = 2 });
        var b = await svc.CreateMaterialAsync(ValidCreate() with { SortOrder = 1 });
        await svc.UpdateMaterialStatusAsync(a.Id, false);
        var list = await svc.GetAllActiveMaterialsAsync();
        Assert.Single(list);
        Assert.Equal(b.Id, list[0].Id);
    }

    [Fact]
    public async Task GetMaterialAsync_NotFound_ReturnsNull()
    {
        var (_, svc, _) = Setup();
        var dto = await svc.GetMaterialAsync(Guid.NewGuid());
        Assert.Null(dto);
    }
}

/// <summary>
/// 编号引擎测试替身:每次 GenerateAsync 返回自增 code(MAT-0001, MAT-0002 ...)。
/// NextCode 可显式覆盖首次返回值。照搬 Color 测试的 FakeNumberingService,改前缀为 MAT-。
/// </summary>
internal sealed class FakeNumberingService : INumberingService
{
    private int _seq;
    public string? NextCode { get; set; }

    public Task<string> GenerateAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
    {
        if (NextCode is not null)
        {
            var code = NextCode;
            NextCode = null;
            return Task.FromResult(code);
        }
        _seq++;
        return Task.FromResult($"MAT-{_seq:D4}");
    }

    public Task<string?> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
        => Task.FromResult<string?>(NextCode ?? $"MAT-{(_seq + 1):D4}");
}
```

- [ ] **Step 2: 运行测试验证通过**

Run: `dotnet test backend/OneCup.sln --filter "FullyQualifiedName~OneCup.UnitTests.Material"`
Expected: 全部 PASS(16 个测试用例)

- [ ] **Step 3: Commit**

```bash
git add backend/tests/OneCup.UnitTests/Material/MaterialServiceTests.cs
git commit -m "test(material): MaterialService 单元测试"
```

---

## Task 11: MaterialSpecs 单元测试

**Files:**
- Create: `backend/tests/OneCup.UnitTests/Material/MaterialSpecsTests.cs`

**Interfaces:**
- Consumes: `MaterialSpecs`(Task 4)、`Material` 实体、`Repository<T>`、`OneCupDbContext`。

- [ ] **Step 1: 创建 Specs 测试文件**

Create `backend/tests/OneCup.UnitTests/Material/MaterialSpecsTests.cs`:

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using OneCup.Application.Specifications;
using OneCup.Domain.Entities;
using OneCup.Infrastructure.Persistence;

namespace OneCup.UnitTests.Material;

public class MaterialSpecsTests
{
    private static OneCupDbContext CreateDb()
    {
        var db = new OneCupDbContext(new DbContextOptionsBuilder<OneCupDbContext>()
            .UseInMemoryDatabase($"material-specs-{Guid.NewGuid()}")
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

    private static Domain.Entities.Material Make(string code, string category = "染料", bool active = true, int sort = 0) => new()
    {
        Code = code, Name = code, Spec = code, Category = category,
        IsActive = active, SortOrder = sort,
    };

    [Fact]
    public async Task MaterialFilterSpec_Keyword_MatchesCodeNameSpec()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Material>(db);
        await repo.AddAsync(Make("MAT001"));
        await repo.AddAsync(Make("AUX001"));
        await db.SaveChangesAsync();

        var mat = await repo.ListAsync(new MaterialFilterSpec("MAT", null, null));
        Assert.Single(mat);
        Assert.Equal("MAT001", mat[0].Code);

        var aux = await repo.ListAsync(new MaterialFilterSpec("AUX", null, null));
        Assert.Single(aux);
    }

    [Fact]
    public async Task MaterialFilterSpec_Category_ExactMatch()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Material>(db);
        await repo.AddAsync(Make("M1", category: "染料"));
        await repo.AddAsync(Make("M2", category: "染料"));
        await repo.AddAsync(Make("A1", category: "助剂"));
        await db.SaveChangesAsync();

        var dyes = await repo.ListAsync(new MaterialFilterSpec(null, "染料", null));
        Assert.Equal(2, dyes.Count);
    }

    [Fact]
    public async Task MaterialFilterSpec_IsActive_Filters()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Material>(db);
        await repo.AddAsync(Make("A1", active: true));
        await repo.AddAsync(Make("A2", active: false));
        await db.SaveChangesAsync();

        var active = await repo.ListAsync(new MaterialFilterSpec(null, null, true));
        Assert.Single(active);
        Assert.Equal("A1", active[0].Code);

        var inactive = await repo.ListAsync(new MaterialFilterSpec(null, null, false));
        Assert.Single(inactive);
        Assert.Equal("A2", inactive[0].Code);
    }

    [Fact]
    public async Task MaterialPagedSpec_AppliesSkipTakeAndOrderBy()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Material>(db);
        await repo.AddAsync(Make("M1", sort: 3));
        await repo.AddAsync(Make("M2", sort: 1));
        await repo.AddAsync(Make("M3", sort: 2));
        await db.SaveChangesAsync();

        var page1 = await repo.ListAsync(new MaterialPagedSpec(null, null, null, 1, 2));
        Assert.Equal(2, page1.Count);
        Assert.Equal("M2", page1[0].Code);   // sort1
        Assert.Equal("M3", page1[1].Code);   // sort2
    }

    [Fact]
    public async Task MaterialFilterSpec_CountUnaffectedByPaging()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Material>(db);
        for (var i = 0; i < 5; i++)
            await repo.AddAsync(Make($"M{i}", category: "染料"));
        await db.SaveChangesAsync();

        var total = await repo.CountAsync(new MaterialFilterSpec(null, "染料", null));
        Assert.Equal(5, total);
    }

    [Fact]
    public async Task MaterialByCodeSpec_MatchesIgnoringExcludedId()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Material>(db);
        await repo.AddAsync(Make("MAT001"));
        await db.SaveChangesAsync();
        var existing = await repo.FirstOrDefaultAsync(new MaterialByCodeSpec("MAT001"));

        var excl = await repo.AnyAsync(new MaterialByCodeSpec("MAT001", existing!.Id));
        Assert.False(excl);

        var incl = await repo.AnyAsync(new MaterialByCodeSpec("MAT001"));
        Assert.True(incl);
    }

    [Fact]
    public async Task MaterialActiveSpec_ReturnsOnlyActiveOrdered()
    {
        var db = CreateDb();
        var repo = new Repository<Domain.Entities.Material>(db);
        await repo.AddAsync(Make("A1", active: true, sort: 2));
        await repo.AddAsync(Make("A2", active: false, sort: 1));
        await repo.AddAsync(Make("A3", active: true, sort: 1));
        await db.SaveChangesAsync();

        var list = await repo.ListAsync(new MaterialActiveSpec());
        Assert.Equal(2, list.Count);
        Assert.Equal("A3", list[0].Code);   // sort1 启用项在前
        Assert.Equal("A1", list[1].Code);
    }
}
```

- [ ] **Step 2: 运行测试验证通过**

Run: `dotnet test backend/OneCup.sln --filter "FullyQualifiedName~OneCup.UnitTests.Material"`
Expected: 全部 PASS(MaterialService 16 + MaterialSpecs 7 = 23 个测试用例)

- [ ] **Step 3: 后端全量构建 + 测试**

Run: `dotnet build backend/OneCup.sln && dotnet test backend/OneCup.sln`
Expected: BUILD SUCCEEDED + 全部测试 PASS(含新增 20 + 既有测试无回归)

- [ ] **Step 4: Commit**

```bash
git add backend/tests/OneCup.UnitTests/Material/MaterialSpecsTests.cs
git commit -m "test(material): MaterialSpecs 单元测试"
```

---

## Task 12: 前端 API 层

**Files:**
- Create: `frontend/src/api/material.ts`

**Interfaces:**
- Produces: `MaterialListItem` / `MaterialDetail` / `MaterialFormData` / `MaterialQuery` / `MaterialPagedResult` 类型 + `getMaterials`/`getMaterial`/`createMaterial`/`updateMaterial`/`deleteMaterial`/`updateMaterialStatus` 方法。供 Task 13-15(前端页面)使用。
- Consumes: `request`(`./request`,双泛型模式)。

- [ ] **Step 1: 创建 API 文件**

Create `frontend/src/api/material.ts`:

```ts
import request from './request';

// ── 类型 ──
export interface MaterialListItem {
  id: string;
  code: string;
  name: string;
  spec: string;
  category: string;
  unitId: string | null;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
}

export interface MaterialDetail extends MaterialListItem {
  remark?: string;
  updatedAt?: string;
}

export interface MaterialPagedResult {
  items: MaterialListItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface MaterialQuery {
  keyword?: string;
  category?: string;
  isActive?: boolean;
  page: number;
  pageSize: number;
}

export interface MaterialFormData {
  name: string;
  spec: string;
  category: string;
  unitId: string | null;
  remark?: string;
  sortOrder: number;
}

export interface UpdateMaterialStatusRequest {
  isActive: boolean;
}

// ── API ──
// 注:request.ts 响应拦截器返回 response.data,故用双泛型 <unknown, T>
export function getMaterials(params: MaterialQuery) {
  return request.get<unknown, MaterialPagedResult>('/api/materials', { params });
}

export function getMaterial(id: string) {
  return request.get<unknown, MaterialDetail>(`/api/materials/${id}`);
}

export function createMaterial(data: MaterialFormData) {
  return request.post<unknown, MaterialDetail>('/api/materials', data);
}

export function updateMaterial(id: string, data: MaterialFormData) {
  return request.put<unknown, MaterialDetail>(`/api/materials/${id}`, data);
}

export function deleteMaterial(id: string) {
  return request.delete(`/api/materials/${id}`);
}

export function updateMaterialStatus(id: string, isActive: boolean) {
  return request.put(`/api/materials/${id}/status`, { isActive } satisfies UpdateMaterialStatusRequest);
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/api/material.ts
git commit -m "feat(material): 前端 API 层"
```

---

## Task 13: 前端样式 + locale + 列表页

**Files:**
- Create: `frontend/src/pages/business/material/style/index.module.less`
- Create: `frontend/src/pages/business/material/locale/index.ts`
- Create: `frontend/src/pages/business/material/locale/en-US.ts`
- Create: `frontend/src/pages/business/material/locale/zh-CN.ts`
- Create: `frontend/src/pages/business/material/index.tsx`

**Interfaces:**
- Produces: `MaterialPage` 默认导出(列表页,遵守列表查询页标准)。
- Consumes: `@/api/material`(Task 12)、`@/api/measurementUnit.getAllActiveUnits`(已有,返回 `MeasurementUnit[]`,字段 `id`/`nameZh`)、`@/components/PermissionWrapper`、`@/utils/useLocale`、`./form`、`./detail`、`./locale`、`./style`。

- [ ] **Step 1: 创建样式(从模板复制,三段固定)**

Create `frontend/src/pages/business/material/style/index.module.less`:

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

- [ ] **Step 2: 创建 locale 聚合**

Create `frontend/src/pages/business/material/locale/index.ts`:

```ts
import zhCN from './zh-CN';
import enUS from './en-US';

export default { 'zh-CN': zhCN, 'en-US': enUS };
```

- [ ] **Step 3: 创建 zh-CN 文案**

Create `frontend/src/pages/business/material/locale/zh-CN.ts`:

```ts
export default {
  'material.title': '原料管理',
  'material.search.keyword': '编号/名称/规格',
  'material.search.category': '类别',
  'material.search.status': '启用状态',
  'material.column.code': '原料编号',
  'material.column.name': '名称',
  'material.column.spec': '规格型号',
  'material.column.category': '类别',
  'material.column.unit': '单位',
  'material.column.sortOrder': '排序',
  'material.column.status': '状态',
  'material.column.createdAt': '创建时间',
  'material.column.operations': '操作',
  'material.active': '启用',
  'material.inactive': '停用',
  'material.button.create': '新建原料',
  'material.button.search': '查询',
  'material.button.reset': '重置',
  'material.button.view': '查看',
  'material.button.edit': '编辑',
  'material.button.delete': '删除',
  'material.form.title.create': '新建原料',
  'material.form.title.edit': '编辑原料',
  'material.form.code': '原料编号',
  'material.form.code.placeholder': '创建时自动生成',
  'material.form.code.previewing': '编号预览中…',
  'material.form.noRule.block': '检测到尚未为原料配置编号规则,无法新建原料。请先到「编号管理」为原料配置一条启用的编号规则,再回来新建。',
  'material.form.name': '名称',
  'material.form.spec': '规格型号',
  'material.form.category': '类别',
  'material.form.unit': '计量单位',
  'material.form.sortOrder': '排序号',
  'material.form.remark': '备注',
  'material.form.isActive': '启用状态',
  'material.detail.title': '原料详情',
  'material.message.deleteOk': '确定删除该原料吗?删除后不可恢复。',
  'material.message.deleteSuccess': '删除成功',
  'material.message.createSuccess': '创建成功',
  'material.message.updateSuccess': '更新成功',
  'material.message.loading': '加载中…',
  'material.message.loadFailed': '加载失败',
  'material.error.noNumberingRule': '请先在编号管理为原料配置启用规则',
};
```

- [ ] **Step 4: 创建 en-US 文案**

Create `frontend/src/pages/business/material/locale/en-US.ts`:

```ts
export default {
  'material.title': 'Material',
  'material.search.keyword': 'Code/Name/Spec',
  'material.search.category': 'Category',
  'material.search.status': 'Status',
  'material.column.code': 'Code',
  'material.column.name': 'Name',
  'material.column.spec': 'Spec',
  'material.column.category': 'Category',
  'material.column.unit': 'Unit',
  'material.column.sortOrder': 'Sort',
  'material.column.status': 'Status',
  'material.column.createdAt': 'Created At',
  'material.column.operations': 'Operations',
  'material.active': 'Active',
  'material.inactive': 'Inactive',
  'material.button.create': 'New Material',
  'material.button.search': 'Search',
  'material.button.reset': 'Reset',
  'material.button.view': 'View',
  'material.button.edit': 'Edit',
  'material.button.delete': 'Delete',
  'material.form.title.create': 'New Material',
  'material.form.title.edit': 'Edit Material',
  'material.form.code': 'Material Code',
  'material.form.code.placeholder': 'Auto-generated on create',
  'material.form.code.previewing': 'Previewing code…',
  'material.form.noRule.block': 'No active numbering rule configured for material. Please configure one in "Numbering" first, then return here to create a material.',
  'material.form.name': 'Name',
  'material.form.spec': 'Spec',
  'material.form.category': 'Category',
  'material.form.unit': 'Unit',
  'material.form.sortOrder': 'Sort Order',
  'material.form.remark': 'Remark',
  'material.form.isActive': 'Active',
  'material.detail.title': 'Material Detail',
  'material.message.deleteOk': 'Delete this material? This cannot be undone.',
  'material.message.deleteSuccess': 'Deleted',
  'material.message.createSuccess': 'Created',
  'material.message.updateSuccess': 'Updated',
  'material.message.loading': 'Loading…',
  'material.message.loadFailed': 'Load failed',
  'material.error.noNumberingRule': 'Please configure an active numbering rule for material first',
};
```

- [ ] **Step 5: 创建列表页**

Create `frontend/src/pages/business/material/index.tsx`:

```tsx
import { useEffect, useMemo, useState } from 'react';
import {
  Badge,
  Button,
  Card,
  Form,
  Grid,
  Input,
  Message,
  Popconfirm,
  Select,
  Space,
  Table,
  Typography,
} from '@arco-design/web-react';
import { IconPlus, IconRefresh, IconSearch } from '@arco-design/web-react/icon';
import {
  MaterialDetail,
  MaterialListItem,
  deleteMaterial,
  getMaterial,
  getMaterials,
} from '@/api/material';
import { getAllActiveUnits, MeasurementUnit } from '@/api/measurementUnit';
import useLocale from '@/utils/useLocale';
import PermissionWrapper from '@/components/PermissionWrapper';
import locale from './locale';
import styles from './style/index.module.less';
import MaterialFormModal from './form';
import MaterialDetailDrawer from './detail';

const { Title } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;
const Option = Select.Option;

function SearchForm({ onSearch }: { onSearch: (v: Record<string, any>) => void }) {
  const [form] = Form.useForm();
  const t = useLocale(locale);

  const handleSubmit = () => onSearch(form.getFieldsValue());
  const handleReset = () => {
    form.resetFields();
    onSearch({});
  };

  return (
    <div className={styles['search-form-wrapper']}>
      <Form
        form={form}
        className={styles['search-form']}
        labelAlign="left"
        labelCol={{ span: 7 }}
        wrapperCol={{ span: 17 }}
      >
        <Row gutter={24}>
          <Col span={8}>
            <FormItem label={t['material.search.keyword']} field="keyword">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['material.search.category']} field="category">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['material.search.status']} field="isActive">
              <Select allowClear>
                <Option value={true}>{t['material.active']}</Option>
                <Option value={false}>{t['material.inactive']}</Option>
              </Select>
            </FormItem>
          </Col>
        </Row>
      </Form>
      <div className={styles['right-button']}>
        <Button type="primary" icon={<IconSearch />} onClick={handleSubmit}>
          {t['material.button.search']}
        </Button>
        <Button icon={<IconRefresh />} onClick={handleReset}>
          {t['material.button.reset']}
        </Button>
      </div>
    </div>
  );
}

export default function MaterialPage() {
  const t = useLocale(locale);
  const [data, setData] = useState<MaterialListItem[]>([]);
  const [loading, setLoading] = useState(true);
  const [formParams, setFormParams] = useState<Record<string, any>>({});
  const [pagination, setPagination] = useState({
    sizeCanChange: true,
    showTotal: true,
    pageSize: 10,
    current: 1,
    total: 0,
    pageSizeChangeResetCurrent: true,
  });

  const [formVisible, setFormVisible] = useState(false);
  const [editing, setEditing] = useState<MaterialDetail | null>(null);
  const [detailVisible, setDetailVisible] = useState(false);
  const [detailData, setDetailData] = useState<MaterialDetail | null>(null);

  // 单位 map:进页面拉一次,列表列 + 详情 + 表单共用
  const [units, setUnits] = useState<MeasurementUnit[]>([]);
  const unitMap = useMemo(() => {
    const m: Record<string, string> = {};
    units.forEach((u) => (m[u.id] = u.nameZh)); // 注意:单位名字段是 nameZh
    return m;
  }, [units]);

  useEffect(() => {
    getAllActiveUnits()
      .then(setUnits)
      .catch(() => {
        // 单位拉取失败不阻塞主流程,单位列展示 '-'
      });
  }, []);

  function fetchData() {
    setLoading(true);
    getMaterials({
      page: pagination.current,
      pageSize: pagination.pageSize,
      ...formParams,
    })
      .then((res) => {
        setData(res.items || []);
        setPagination((p) => ({ ...p, total: res.total || 0 }));
      })
      .finally(() => setLoading(false));
  }

  function openCreate() {
    setEditing(null);
    setFormVisible(true);
  }
  function openEdit(record: MaterialListItem) {
    const closeLoading = Message.loading({ content: t['material.message.loading'] });
    getMaterial(record.id)
      .then((detail) => {
        setEditing(detail);
        setFormVisible(true);
      })
      .catch(() => Message.error(t['material.message.loadFailed']))
      .finally(() => closeLoading());
  }
  function openDetail(record: MaterialListItem) {
    const closeLoading = Message.loading({ content: t['material.message.loading'] });
    getMaterial(record.id)
      .then((detail) => {
        setDetailData(detail);
        setDetailVisible(true);
      })
      .catch(() => Message.error(t['material.message.loadFailed']))
      .finally(() => closeLoading());
  }
  async function handleDelete(record: MaterialListItem) {
    try {
      await deleteMaterial(record.id);
      Message.success(t['material.message.deleteSuccess']);
      fetchData();
    } catch {
      // ignore
    }
  }

  const columns = useMemo(
    () => [
      { title: t['material.column.code'], dataIndex: 'code' },
      { title: t['material.column.name'], dataIndex: 'name' },
      { title: t['material.column.spec'], dataIndex: 'spec' },
      { title: t['material.column.category'], dataIndex: 'category' },
      {
        title: t['material.column.unit'],
        dataIndex: 'unitId',
        render: (id: string | null) => (id ? unitMap[id] ?? '-' : '-'),
      },
      { title: t['material.column.sortOrder'], dataIndex: 'sortOrder' },
      {
        title: t['material.column.status'],
        dataIndex: 'isActive',
        render: (v: boolean) => (
          <Badge
            status={v ? 'success' : 'default'}
            text={v ? t['material.active'] : t['material.inactive']}
          />
        ),
      },
      { title: t['material.column.createdAt'], dataIndex: 'createdAt' },
      {
        title: t['material.column.operations'],
        dataIndex: 'operations',
        render: (_: any, record: MaterialListItem) => (
          <Space>
            <Button type="text" size="small" onClick={() => openDetail(record)}>
              {t['material.button.view']}
            </Button>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'material', actions: ['update'] }]}
            >
              <Button type="text" size="small" onClick={() => openEdit(record)}>
                {t['material.button.edit']}
              </Button>
            </PermissionWrapper>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'material', actions: ['delete'] }]}
            >
              <Popconfirm
                title={t['material.message.deleteOk']}
                onOk={() => handleDelete(record)}
              >
                <Button type="text" size="small" status="danger">
                  {t['material.button.delete']}
                </Button>
              </Popconfirm>
            </PermissionWrapper>
          </Space>
        ),
      },
    ],
    [t, unitMap],
  );

  function handleSearch(params: Record<string, any>) {
    setPagination((p) => ({ ...p, current: 1 }));
    setFormParams(params);
  }

  function onChangeTable({ current, pageSize }: any) {
    setPagination((p) => ({ ...p, current, pageSize }));
  }

  useEffect(() => {
    fetchData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pagination.current, pagination.pageSize, JSON.stringify(formParams)]);

  return (
    <Card>
      <Title heading={6}>{t['material.title']}</Title>
      <SearchForm onSearch={handleSearch} />
      <div className={styles['button-group']}>
        <Space>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'material', actions: ['create'] }]}
          >
            <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
              {t['material.button.create']}
            </Button>
          </PermissionWrapper>
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
      <MaterialFormModal
        visible={formVisible}
        editing={editing}
        units={units}
        onClose={() => setFormVisible(false)}
        onSuccess={fetchData}
      />
      <MaterialDetailDrawer
        visible={detailVisible}
        data={detailData}
        unitMap={unitMap}
        onClose={() => setDetailVisible(false)}
      />
    </Card>
  );
}
```

- [ ] **Step 6: Commit(此时尚未可构建,form/detail 在下两个 Task 创建)**

```bash
git add frontend/src/pages/business/material/style/ frontend/src/pages/business/material/locale/ frontend/src/pages/business/material/index.tsx
git commit -m "feat(material): 列表页 + 样式 + locale"
```

---

## Task 14: 前端表单 Modal(c02 编号预览)

**Files:**
- Create: `frontend/src/pages/business/material/form.tsx`

**Interfaces:**
- Produces: `MaterialFormModal` 默认导出(props: `visible` / `editing: MaterialDetail | null` / `units: MeasurementUnit[]` / `onClose` / `onSuccess`)。
- Consumes: `@/api/material`(Task 12)、`@/api/numbering.previewCode`(已有)、`@/api/measurementUnit.MeasurementUnit`(类型)、`@/utils/useLocale`、`./locale`。严格遵守 c02。

- [ ] **Step 1: 创建表单组件**

Create `frontend/src/pages/business/material/form.tsx`:

```tsx
import { useEffect, useState } from 'react';
import {
  Alert,
  Form,
  Input,
  InputNumber,
  Message,
  Modal,
  Select,
  Switch,
} from '@arco-design/web-react';
import {
  MaterialDetail,
  MaterialFormData,
  createMaterial,
  updateMaterial,
} from '@/api/material';
import { MeasurementUnit } from '@/api/measurementUnit';
import { previewCode } from '@/api/numbering';
import useLocale from '@/utils/useLocale';
import locale from './locale';

const FormItem = Form.Item;
const TextArea = Input.TextArea;
const Option = Select.Option;

export default function MaterialFormModal({
  visible,
  editing,
  units,
  onClose,
  onSuccess,
}: {
  visible: boolean;
  editing: MaterialDetail | null; // null = 新建模式
  units: MeasurementUnit[];
  onClose: () => void;
  onSuccess: () => void;
}) {
  const t = useLocale(locale);
  const [form] = Form.useForm();
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  // 新建模式:预览下一个物料编号(不消耗计数)。null = 无启用规则 / 预览中。
  const [previewedCode, setPreviewedCode] = useState<string | null>(null);
  const [codeLoading, setCodeLoading] = useState(false);
  // 无编号规则:阻塞新建(用户填了也提交不了)
  const [noRule, setNoRule] = useState(false);

  useEffect(() => {
    if (visible) {
      setErrorMsg('');
      setNoRule(false);
      if (editing) {
        // 编辑模式:展示实际编号
        setPreviewedCode(editing.code);
        form.setFieldsValue({
          name: editing.name,
          spec: editing.spec,
          category: editing.category,
          unitId: editing.unitId,
          sortOrder: editing.sortOrder,
          remark: editing.remark,
          isActive: editing.isActive,
        });
      } else {
        // 新建模式:预览下一个编号(只读,不消耗计数)
        setPreviewedCode(null);
        setCodeLoading(true);
        previewCode('material')
          .then((res) => {
            // null 表示无启用规则 → 阻塞新建
            if (res.code) {
              setPreviewedCode(res.code);
              setNoRule(false);
            } else {
              setNoRule(true);
            }
          })
          .catch(() => setNoRule(true))
          .finally(() => setCodeLoading(false));
        form.resetFields();
        form.setFieldValue('isActive', true);
      }
    }
  }, [visible, editing, form]);

  const handleOk = async () => {
    try {
      const values = (await form.validate()) as MaterialFormData;
      setConfirmLoading(true);
      setErrorMsg('');
      if (editing) {
        await updateMaterial(editing.id, values);
        Message.success(t['material.message.updateSuccess']);
      } else {
        await createMaterial(values);
        Message.success(t['material.message.createSuccess']);
      }
      onSuccess();
      onClose();
    } catch (err: any) {
      // 后端 400:无编号规则等,展示在顶部 Alert
      const msg = err?.response?.data?.message || err?.message || '';
      if (msg.includes('编号') || msg.includes('rule') || msg.includes('numbering')) {
        setErrorMsg(t['material.error.noNumberingRule']);
      } else {
        setErrorMsg(msg);
      }
    } finally {
      setConfirmLoading(false);
    }
  };

  return (
    <Modal
      title={editing ? t['material.form.title.edit'] : t['material.form.title.create']}
      visible={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={confirmLoading}
      okButtonProps={{ disabled: noRule }}
      unmountOnExit
    >
      {noRule && (
        <Alert type="warning" content={t['material.form.noRule.block']} style={{ marginBottom: 16 }} />
      )}
      {errorMsg && <Alert type="error" content={errorMsg} style={{ marginBottom: 16 }} />}
      <Form form={form} layout="vertical" disabled={noRule}>
        <FormItem label={t['material.form.code']}>
          <Input
            value={previewedCode ?? undefined}
            readOnly
            placeholder={codeLoading ? t['material.form.code.previewing'] : t['material.form.code.placeholder']}
          />
        </FormItem>
        <FormItem label={t['material.form.name']} field="name" rules={[{ required: true }]}>
          <Input maxLength={100} />
        </FormItem>
        <FormItem label={t['material.form.spec']} field="spec" rules={[{ required: true }]}>
          <Input maxLength={100} />
        </FormItem>
        <FormItem label={t['material.form.category']} field="category" rules={[{ required: true }]}>
          <Input maxLength={32} />
        </FormItem>
        <FormItem label={t['material.form.unit']} field="unitId">
          <Select allowClear placeholder={t['material.form.unit']}>
            {units.map((u) => (
              <Option key={u.id} value={u.id}>
                {u.nameZh}
                {u.symbol ? ` (${u.symbol})` : ''}
              </Option>
            ))}
          </Select>
        </FormItem>
        <FormItem label={t['material.form.sortOrder']} field="sortOrder" initialValue={0}>
          <InputNumber min={0} style={{ width: '100%' }} />
        </FormItem>
        <FormItem label={t['material.form.remark']} field="remark">
          <TextArea maxLength={256} />
        </FormItem>
        <FormItem label={t['material.form.isActive']} field="isActive" triggerPropName="checked">
          <Switch />
        </FormItem>
      </Form>
    </Modal>
  );
}
```

- [ ] **Step 2: Commit**

```bash
git add frontend/src/pages/business/material/form.tsx
git commit -m "feat(material): 表单 Modal(c02 编号预览)"
```

---

## Task 15: 前端详情 Drawer

**Files:**
- Create: `frontend/src/pages/business/material/detail.tsx`

**Interfaces:**
- Produces: `MaterialDetailDrawer` 默认导出(props: `visible` / `data: MaterialDetail | null` / `unitMap: Record<string,string>` / `onClose`)。
- Consumes: `@/api/material.MaterialDetail`、`@/utils/useLocale`、`./locale`。

- [ ] **Step 1: 创建详情组件**

Create `frontend/src/pages/business/material/detail.tsx`:

```tsx
import { Descriptions, Drawer } from '@arco-design/web-react';
import { MaterialDetail } from '@/api/material';
import useLocale from '@/utils/useLocale';
import locale from './locale';

export default function MaterialDetailDrawer({
  visible,
  data,
  unitMap,
  onClose,
}: {
  visible: boolean;
  data: MaterialDetail | null;
  unitMap: Record<string, string>;
  onClose: () => void;
}) {
  const t = useLocale(locale);
  return (
    <Drawer
      title={t['material.detail.title']}
      visible={visible}
      onCancel={onClose}
      footer={null}
      width={480}
    >
      {data && (
        <Descriptions
          column={1}
          data={[
            { label: t['material.column.code'], value: data.code },
            { label: t['material.column.name'], value: data.name },
            { label: t['material.column.spec'], value: data.spec },
            { label: t['material.column.category'], value: data.category },
            {
              label: t['material.column.unit'],
              value: data.unitId ? unitMap[data.unitId] ?? '-' : '-',
            },
            { label: t['material.column.sortOrder'], value: data.sortOrder },
            { label: t['material.form.remark'], value: data.remark || '-' },
            {
              label: t['material.column.status'],
              value: data.isActive ? t['material.active'] : t['material.inactive'],
            },
            { label: t['material.column.createdAt'], value: data.createdAt },
          ]}
        />
      )}
    </Drawer>
  );
}
```

- [ ] **Step 2: 前端构建验证(此时 material 目录自包含,但 routes 尚未接入,构建应通过)**

Run: `cd frontend && npm run build`
Expected: BUILD SUCCEEDED(无 TS 类型错误)

- [ ] **Step 3: Commit**

```bash
git add frontend/src/pages/business/material/detail.tsx
git commit -m "feat(material): 详情 Drawer"
```

---

## Task 16: 前端路由 + 菜单 + 全局文案接入

**Files:**
- Modify: `frontend/src/routes.ts`
- Modify: `frontend/src/router.tsx`
- Modify: `frontend/src/locale/index.ts`

**Interfaces:** 无(末尾追加,合同 3.4)。

- [ ] **Step 1: routes.ts 追加菜单项**

在 `frontend/src/routes.ts` 的 `menu.business` children 数组**末尾**追加(参照现有 customer 项)。用 Edit 工具,找到:
```typescript
      {
        name: 'menu.business.customer',
        key: 'business/customer',
        requiredPermissions: [
          { resource: 'customer', actions: ['read'] },
        ],
      },
```
替换为:
```typescript
      {
        name: 'menu.business.customer',
        key: 'business/customer',
        requiredPermissions: [
          { resource: 'customer', actions: ['read'] },
        ],
      },
      {
        name: 'menu.business.material',
        key: 'business/material',
        requiredPermissions: [
          { resource: 'material', actions: ['read'] },
        ],
      },
```

- [ ] **Step 2: router.tsx 追加 lazy import**

在 `frontend/src/router.tsx` 的 lazy import 区追加(参照现有 CustomerPage)。用 Edit 工具,找到:
```typescript
const CustomerPage = lazy(() => import('@/pages/business/customer'));
```
替换为:
```typescript
const CustomerPage = lazy(() => import('@/pages/business/customer'));
const MaterialPage = lazy(() => import('@/pages/business/material'));
```

- [ ] **Step 3: router.tsx 追加路由项**

在 children 路由区(customer 路由项之后)追加。用 Edit 工具,找到 customer 路由项的闭合:
```typescript
      {
        path: 'business/customer',
        element: withSuspense(
          <RequirePermission resource="customer" actions={['read']}>
            <CustomerPage />
          </RequirePermission>
        ),
      },
```
替换为:
```typescript
      {
        path: 'business/customer',
        element: withSuspense(
          <RequirePermission resource="customer" actions={['read']}>
            <CustomerPage />
          </RequirePermission>
        ),
      },
      {
        path: 'business/material',
        element: withSuspense(
          <RequirePermission resource="material" actions={['read']}>
            <MaterialPage />
          </RequirePermission>
        ),
      },
```

- [ ] **Step 4: locale/index.ts 追加菜单文案(en-US + zh-CN)**

在 `frontend/src/locale/index.ts` 的 en-US 对象追加一行,用 Edit 工具,找到:
```typescript
    'menu.business.customer': 'Customer',
```
替换为:
```typescript
    'menu.business.customer': 'Customer',
    'menu.business.material': 'Material',
```

在 zh-CN 对象追加一行,找到:
```typescript
    'menu.business.customer': '客户管理',
```
替换为:
```typescript
    'menu.business.customer': '客户管理',
    'menu.business.material': '原料管理',
```

- [ ] **Step 5: 前端构建验证**

Run: `cd frontend && npm run build`
Expected: BUILD SUCCEEDED

- [ ] **Step 6: Commit**

```bash
git add frontend/src/routes.ts frontend/src/router.tsx frontend/src/locale/index.ts
git commit -m "feat(material): 路由 + 菜单 + 全局文案接入"
```

---

## Task 17: 全量验证

**Files:** 无(纯验证)。

- [ ] **Step 1: 后端全量构建 + 测试**

Run:
```bash
dotnet build backend/OneCup.sln
dotnet test backend/OneCup.sln
```
Expected: BUILD SUCCEEDED + 全部测试 PASS(新增 23 + 既有无回归)

- [ ] **Step 2: 迁移应用本地库验证**

Run:
```bash
cd backend
dotnet ef database update --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```
Expected: `Done.`(materials 表创建成功,FK/索引齐全)

- [ ] **Step 3: 前端构建**

Run: `cd frontend && npm run build`
Expected: BUILD SUCCEEDED

- [ ] **Step 4: 检查 git 状态干净**

Run: `git status`
Expected: `nothing to commit, working tree clean`

- [ ] **Step 5: 完成报告**

确认所有 Task 已 commit,记录 commit 数和测试数。本 worktree 自验完成;合并期验证由合同第 6 节统一跑。

---

## Self-Review 结果

> 本节是真正逐条核查后的记录(初版 plan 写完后回头比对 spec 与标杆实现,发现两处真实缺陷已 inline 修复,见下)。

**1. Spec coverage(对照 `2026-07-04-material-mgmt-design.md`):**
- §1 数据模型(8 字段 + 仅 FK 无导航 + Restrict) → Task 1 ✅
- §2.1 DTO → Task 2 ✅
- §2.2 Specs(5 个) → Task 4 ✅
- §2.3 IMaterialService/MaterialService(7 方法 + 事务取号 + 物理删除) → Task 5/6 ✅
- §2.4 Controller(7 端点 + 单数文件名 + 复数路由) → Task 7 ✅
- §2.5 EF 配置(已含 Task 1) → Task 1 ✅
- §2.6 迁移 AddMaterialModule → Task 9 ✅
- §4.1 共享文件(DbContext/Program) → Task 8 ✅
- §3 前端(API/列表/表单 c02/详情/样式/locale) → Task 12-16 ✅
- §4.4 单测(Service + Specs) → Task 10/11 ✅
- §4.3 验证 → Task 17 ✅
- 无遗漏。

**2. 缺陷修复(本次 self-review 真正发现并 inline 修正,非走过场):**

- **🔴 缺陷 A — UpdateMaterialAsync 的 null-skip bug(已修):** 初版 Service 用 `if (request.X is not null) entity.X = request.X;`,但 spec §2.3 明确要求"整表覆盖式 PUT,对齐 CustomerService.UpdateAsync"。null-skip 会让前端清空可选字段(如计量单位 `UnitId`)提交 null 后被当"不修改",字段无法清空。**修复:** Task 6 改为可空字段直接赋值(`entity.UnitId = request.UnitId`)、必填字段防御性 `?? entity.X`;Task 10 新增 `UpdateMaterialAsync_NullUnitId_ClearsUnit` 用例覆盖该路径。

- **🔴 缺陷 B — 缺失 FluentValidation 校验(已修):** 标杆不一致——Customer 有 `Create/UpdateCustomerRequestValidator` + Service 注入 `IValidator` 调 `EnsureValidAsync`,Color 无校验器(仅内联 `ValidateHex`)。初版 plan 照搬 Color 导致后端裸接 DTO,`Name` 空串/`Spec` 超长可直接落库。**经用户确认选"对齐 Customer"。修复:** 新增 Task 3(两个 Validator 文件)、Task 6 Service 注入两个 validator 并在 Create/Update 首行调 `EnsureValidAsync`、Task 10 `Setup()` 传 validator 实例 + 新增 2 个校验失败用例(`EmptyName`/`OverlongSpec`)。Program.cs:66 `AddValidatorsFromAssembly` 已注册,无需改。

**3. Placeholder scan:** 全部代码块完整,无 TBD/TODO/"类似 Task N"。每步含确切文件路径、完整代码、确切命令与预期输出。✅

**4. Type consistency(核查修复后):**
- `Material` 实体字段在 Task 1/2/4/6/10/11 一致。
- `MaterialService` 构造函数 `(IRepository<Material>, IUnitOfWork, INumberingService, IValidator<CreateMaterialRequest>, IValidator<UpdateMaterialRequest>)` 在 Task 6(定义)和 Task 10(测试 new)一致(缺陷 B 修复后已同步)。
- `Remove(T)` 方法名(Task 6 用 `_materials.Remove(entity)`)与 `IRepository<T>` 签名一致(非 DeleteAsync)。
- `GenerateAsync` 返回 `string`(非 nullable),Task 6 接收为 `var code` 一致。
- 前端 `MaterialDetail`/`MaterialFormData` 在 Task 12(定义)/13(index)/14(form)/15(detail)一致。
- `unitMap` prop 从 Task 13(index)传给 Task 15(detail),类型 `Record<string,string>` 一致。
- `units: MeasurementUnit[]` prop 从 Task 13 传给 Task 14,字段用 `u.nameZh`/`u.symbol`/`u.id` 与 `measurementUnit.ts` 一致(已确认无 `name` 字段)。
- **交叉 Task 引用编号**:重编号后已逐一核对修正(Task 1/2/3/4/5/6/7/12/13/14 的 Produces/Consumes 行全部对齐新编号)。✅
- 测试计数:Task 10 = 16 用例,Task 11 = 7 用例,合计 23(Task 10/11 Step 2、Task 17 Step 1 预期值已同步)。✅
