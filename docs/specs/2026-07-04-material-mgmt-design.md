# 物料管理(Material)模块设计

> 日期：2026-07-04 ｜ 分支：`feat/material-mgmt` ｜ worktree：`.worktrees/material-mgmt/`
> 并行约定：`docs/parallel-dev-contract-v2.md`（任务①，种子零新增）
> 设计参考：`Color`（带编号对象）+ `Customer`（有 delete + 列表标准）两个既有模块

---

## 0. 背景与范围

### 0.1 业务定位
物料(Material)在编号引擎里命名为"**原料**"。语义上不是"面料"本身（面料有独立的 fabric 类型），而是
**坯布面料生产过程中的投入品**：助剂、染料、原材料（染化料 / 化工料）。

- 典型 name："活性红 3B"、"渗透剂 JFC"、"纯碱"
- 典型 spec："粉末 100%"、"液体 50%"、"纯度 98%"
- 典型 category："助剂"、"染料"（分散染料 / 活性染料）、"原材料"

### 0.2 现状
main 基线（`45180c5`）已为物料**备齐全部种子**：
- 权限：`material:read/create/update/delete`（SeedData PermMaterial 305-308）
- 编号目标类型：`material` / 原料 / Material（SeedData TargetTypeMaterial 0202）
- `NumberTargetTypes.Material = "material"` 常量
- `Program.cs` 的 4 条 `material:*` AddPolicy
- `DbContext.Seed()` 的 4 条权限种子 + 1 条目标类型种子

→ **物料是三个并行任务里唯一零新增 Guid 的模块**，不改 `SeedData.cs` / `NumberTargetTypes.cs` / AddPolicy 块。
需从零建：实体 / 配置 / DTO / Service / Controller / 前端页面 + 共享文件追加菜单。

### 0.3 成功标准
1. 物料完整 CRUD（含状态启停 + 物理删除），编号由引擎在事务内生成（c02 流程）。
2. 列表页严格遵守「列表查询页标准」（单 Card + Form/Grid 三列 + 按钮外侧 + 工具栏 space-between）。
3. 删除按 c01（单条物理删除 → Popconfirm）。
4. 后端单测覆盖 Service + Specs；前后端 build 全绿。
5. 与任务②（工序）零物理冲突（仅 ModelSnapshot 合并期处理）。

---

## 1. 数据模型

### 1.1 实体 `Material : BaseEntity`
（BaseEntity 提供 Id / CreatedAt / UpdatedAt）

| 字段 | 类型 | 列名(snake) | 约束 | 说明 |
|---|---|---|---|---|
| `Code` | string | `code` | MaxLength(32), 必填, **唯一索引** | 系统生成，创建后不可改（c02） |
| `Name` | string | `name` | MaxLength(100), 必填 | 如"活性红 3B" |
| `Spec` | string | `spec` | MaxLength(100), 必填 | 规格型号，如"粉末 100%" |
| `Category` | string | `category` | MaxLength(32), 必填 | 助剂/染料/原材料（自由文本，可细分） |
| `UnitId` | Guid? | `unit_id` | 可空, FK→`measurement_units.id` | 计量单位；可空以兼容"暂未定单位" |
| `Remark` | string? | `remark` | MaxLength(256) | 备注 |
| `SortOrder` | int | `sort_order` | 必填, 默认 0 | 排序号 |
| `IsActive` | bool | `is_active` | 必填, 默认 true | 启停状态 |

### 1.2 外键策略（关键决策：仅 FK，无导航属性）
- 实体仅 `UnitId: Guid?`，**不建** `public virtual MeasurementUnit? Unit` 导航属性。
- 理由：
  1. **与现有模块一致**——Color/Customer/Unit 全是单表自包含，无跨业务表导航属性先例。
  2. **单位表极小（≈20 条）**，前端列表 map 零成本；表单下拉本就要拉一次 `/all`，复用同一份缓存。
  3. **后端更简单**——Spec 无 AddInclude、DTO 无 unitName、Repository 无 Include 链。
  4. **并行合并更安全**——ModelSnapshot 仅多一行 Property + FK，冲突解起来直观。
- EF 配置外键：`builder.HasOne<MeasurementUnit>().WithMany().HasForeignKey(m => m.UnitId)`
  （EF Core 支持"无导航属性的 FK"）。
- **级联删除 = Restrict**：删单位时若物料引用，数据库报错；本轮不在删单位侧做前置校验（留 TODO）。

### 1.3 删除语义
物料有 `material:delete` 权限（对比：Color 无 delete 只启停；Customer 有 delete）。
→ 物料**走物理删除** + **独立启停状态接口**（双轨，同 customer）。
→ 单条物理删除按 **c01 → Popconfirm**，确认文案强调"不可恢复"。

---

## 2. 后端分层（复刻 Color/Customer 模式）

### 2.1 DTO `Dtos/System/MaterialDtos.cs`
```csharp
public record CreateMaterialRequest {
    public string Name { get; init; } = string.Empty;
    public string Spec { get; init; } = string.Empty;
    public string Category { get; init; } = string.Empty;
    public Guid? UnitId { get; init; }
    public string? Remark { get; init; }
    public int SortOrder { get; init; }
}

public record UpdateMaterialRequest {   // 全可空，部分更新；无 Code（不可改）、无 IsActive（走状态接口）
    public string? Name { get; init; }
    public string? Spec { get; init; }
    public string? Category { get; init; }
    public Guid? UnitId { get; init; }
    public string? Remark { get; init; }
    public int? SortOrder { get; init; }
}

public record UpdateMaterialStatusRequest { public bool IsActive { get; init; } }

public class MaterialDto {
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
- CreateDto 无 Code（系统生成）、无 IsActive（默认 true）。
- UpdateDto 无 Code（不可改）、无 IsActive（走状态接口）。
- Update 走**整表覆盖式 PUT**（前端每次提交全量，`entity.X = request.X` 直接赋值），与 Customer 的 PUT 全量一致。

### 2.2 Specs `Specifications/MaterialSpecs.cs`（Ardalis 风格，5 个）
- `MaterialFilterSpec(keyword, category, isActive)` —— 仅过滤，**用于 CountAsync**（总数不跳号）
- `MaterialPagedSpec(keyword, category, isActive, page, pageSize)` —— 过滤 + 排序 + 分页
- `MaterialActiveSpec()` —— 启用项，下拉用
- `MaterialByIdSpec(id)`
- `MaterialByCodeSpec(code, excludingId?)` —— 编号唯一性校验（预留对齐 Color）

**keyword 搜索范围**：`code.Contains(kw) || name.Contains(kw) || spec.Contains(kw)`（规格也搜，染料名常记在 spec）。
**category**：精确匹配（自由文本，枚举化在后续优化）。**isActive**：精确匹配。
**排序**：`ApplyOrderBy(c => c.SortOrder)`（同 Color）。
**关键模式**：FilterSpec 与 PagedSpec 分离，CountAsync 用前者避免对分页子集 Skip/Take。

### 2.3 `Interfaces/IMaterialService.cs` + `Services/MaterialService.cs`

| 方法 | 权限 | 说明 |
|---|---|---|
| `GetMaterialsAsync(page,pageSize,keyword,category,isActive)` | material:read | PagedResult<MaterialDto>，总数用 FilterSpec |
| `GetAllActiveMaterialsAsync()` | material:read | 下拉用 List<MaterialDto> |
| `GetMaterialAsync(id)` | material:read | 单个，ByIdSpec |
| `CreateMaterialAsync(CreateMaterialRequest)` | material:create | **事务内取号**，见下 |
| `UpdateMaterialAsync(id, UpdateMaterialRequest)` | material:update | 整表覆盖式赋值 |
| `UpdateMaterialStatusAsync(id, isActive)` | material:update | 单独状态接口 |
| `DeleteMaterialAsync(id)` | material:delete | **物理删除**（物料独有，Color 没有） |

**构造函数注入**：`IRepository<Material>`、`IUnitOfWork`、`INumberingService`（同 ColorService）。

**CreateAsync 核心逻辑（照搬 ColorService.CreateColorAsync）：**
```csharp
public async Task<MaterialDto> CreateMaterialAsync(CreateMaterialRequest request, CancellationToken ct = default)
{
    Guid createdId = Guid.Empty;
    await _uow.ExecuteInTransactionAsync(async () =>
    {
        // 事务内经编号引擎取号（行锁），计数器增量与物料记录一起提交（不跳号）
        var code = await _numbering.GenerateAsync(NumberTargetTypes.Material, null, ct);
        var entity = new Material {
            Code = code, Name = request.Name, Spec = request.Spec, Category = request.Category,
            UnitId = request.UnitId, Remark = request.Remark, SortOrder = request.SortOrder, IsActive = true,
        };
        await _materials.AddAsync(entity, ct);
        await _uow.SaveChangesAsync(ct);
        createdId = entity.Id;
    }, ct);
    return await GetMaterialAsync(createdId, ct) ?? throw new DomainException("物料创建失败");
}
```
- **categoryCode 传 null**：物料本轮不接分类码选择（任务③范围），先用默认规则。前端 `previewCode("material")` 也不传 categoryCode，行为一致。

**DeleteAsync（新增，Color 没有）：**
```csharp
public async Task DeleteMaterialAsync(Guid id, CancellationToken ct = default)
{
    var entity = await _materials.FirstOrDefaultAsync(new MaterialByIdSpec(id), ct)
        ?? throw new DomainException("物料不存在");
    await _materials.DeleteAsync(entity, ct);
    await _uow.SaveChangesAsync(ct);
}
```

### 2.4 Controller `Controllers/MaterialController.cs`
- **文件名单数**（对齐 `ColorController.cs`），**路由复数** `api/materials`。
- `[ApiController]` + `[Route("api/materials")]`
- GET / GET all / GET {id} → `material:read`
- POST → `material:create`，返回 `CreatedAtAction(nameof(GetMaterial), new { id }, dto)`
- PUT {id} → `material:update`，返回 `NoContent()`
- PUT {id}/status → `material:update`，返回 `NoContent()`
- **DELETE {id} → `material:delete`**，返回 `NoContent()`（物料独有）
- 分页查询参数：`[FromQuery] int page=1, [FromQuery] int pageSize=10, [FromQuery] string? keyword=null, [FromQuery] string? category=null, [FromQuery] bool? isActive=null`

### 2.5 EF 配置 `Persistence/Configurations/MaterialConfiguration.cs`
- `ToTable("materials")`，snake_case 列名
- 每个 Property 显式 `HasMaxLength` / `IsRequired()`
- `HasIndex(m => m.Code).HasDatabaseName("ux_materials_code").IsUnique()`
- FK：`HasOne<MeasurementUnit>().WithMany().HasForeignKey(m => m.UnitId)`，**OnDelete(DeleteBehavior.Restrict)**
- 被 `ApplyConfigurationsFromAssembly` 自动扫描，**无需**手动 ApplyConfiguration

### 2.6 迁移
```bash
cd backend
dotnet ef migrations add AddMaterialModule --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```
生成 `materials` 建表 + 唯一索引 + FK 约束。
**依赖前提**：MeasurementUnit 迁移 `20260704040351_AddUnitModule` 已在基线，`measurement_units` 表存在。
**ModelSnapshot.cs** 由 EF 自动重写，不手改；合并期走合同第 6.4 节。

---

## 3. 前端层（复刻 customer，染化料字段）

### 3.1 目录结构
```
frontend/src/pages/business/material/
├── index.tsx        # 列表页（从 query-table-page 模板 + customer 化）
├── form.tsx         # 新建/编辑 Modal（c02 编号预览）
├── detail.tsx       # 详情 Drawer
├── style/index.module.less   # 从 less 模板复制（三段固定）
└── locale/{index,en-US,zh-CN}.ts
frontend/src/api/material.ts   # API 层
```

### 3.2 API 层 `api/material.ts`
照搬 customer.ts 的双泛型 + 类型分层：
- `MaterialListItem` / `MaterialDetail extends ListItem` / `MaterialFormData` / `MaterialQuery` / `MaterialPagedResult`
- `getMaterials` / `getMaterial` / `createMaterial` / `updateMaterial` / `deleteMaterial` / `updateMaterialStatus`
- 路径 `/api/materials`，`request.<verb><unknown, T>` 双泛型

### 3.3 列表页 `index.tsx`（严格遵守列表查询页标准）
**查询区（3 列，Form + Grid，按钮外侧兄弟 div）：**
- `keyword`（Input，占位"编号/名称/规格"）—— 后端搜 code+name+spec
- `category`（Input allowClear，**自由文本精确匹配**；不做下拉枚举——本轮 YAGNI）
- `isActive`（Select，启用/停用）

**工具栏（flex space-between）：**
- 左：`<PermissionWrapper resource="material" actions={['create']}>` + 新建按钮
- 右：`<Space />`（预留批量位，本轮空）

**表格列：** code / name / spec / category / unitId（`unitMap[id] ?? '-'`）/ sortOrder / isActive（Badge）/ 操作

**单位 map 加载**：进页面 `useEffect` 拉一次 `getAllMeasurementUnits()`，缓存 `unitMap: Record<string,string>`。

**操作列（按 c01 + 权限）：**
- 查看（无权限包装）→ openDetail 开 Drawer
- 编辑（`<PermissionWrapper actions={['update']}>`）→ openEdit
- 删除（`<PermissionWrapper actions={['delete']}>`）→ **Popconfirm**（单条物理删除，c01，文案"不可恢复"）

**分页/查询状态机**：照搬 customer（useEffect 依赖 current/pageSize/formParams；handleSearch 重置 current=1）。

### 3.4 表单 `form.tsx`（Modal，严格遵守 c02）
照搬 customer/form.tsx，改字段 + `previewCode('material')`：
- **c02 流程**（与 customer 同构）：
  - 状态 `previewedCode` / `codeLoading` / `noRule`
  - `useEffect` visible 时：编辑回填；新建调 `previewCode('material')`，`res.code` 有值→回填只读框，null/catch→`setNoRule(true)`
  - 阻塞：`okButtonProps={{disabled:noRule}}` + 顶部 `<Alert type="warning">`（"未配置编号规则…"）+ `<Form disabled={noRule}>`
  - 编号字段：`<Input readOnly value={previewedCode ?? undefined}>`
- **表单字段（layout="vertical"）：**
  - 编号（只读，c02）
  - 名称 `[required]`
  - 规格型号 `[required]`
  - 类别 `[required]`（Input，不做下拉）
  - 计量单位（Select，Options 来自 `getAllMeasurementUnits()`，value=id label=name，allowClear 可空）
  - 排序号（InputNumber，默认 0）
  - 备注（TextArea）
  - 启用状态（Switch，triggerPropName="checked"，新建默认 true）
- **提交**：validate → create/update → onSuccess + onClose；错误映射同 customer
- **编辑模式不调 previewCode**（展示 editing.code）

### 3.5 详情 `detail.tsx`（Drawer + Descriptions）
照搬 customer/detail.tsx。单位列用 `unitMap[data.unitId] ?? '-'` 渲染。

### 3.6 样式 `style/index.module.less`
从 `.less.template` 复制，三段固定不动。

### 3.7 模块级 locale `locale/{en-US,zh-CN}.ts`
照搬 customer locale 结构，key 前缀 `material.*`，染化料语义文案。

---

## 4. 共享文件改动 + 迁移 + 验证 + 风险

### 4.1 共享文件改动清单（物料零新增 Guid）
| 文件 | 改动 | 依据 |
|---|---|---|
| `OneCupDbContext.cs` | 末尾追加 `// ===== Material 模块 =====` + `public DbSet<Material> Materials => Set<Material>();` | 合同 3.3 |
| `Program.cs` | 末尾追加 `builder.Services.AddScoped<IMaterialService, MaterialService>();` | 合同 3.5 |
| `OneCupDbContextModelSnapshot.cs` | EF 自动重写，不手改 | 合同 6.4 |
| `frontend/src/routes.ts` | `menu.business.children` 末尾追加 `{name:'menu.business.material', key:'business/material', requiredPermissions:[{resource:'material',actions:['read']}]}` | 合同 3.4 |
| `frontend/src/router.tsx` | 顶部 lazy import `MaterialPage` + children 路由项 | 合同 3.4 |
| `frontend/src/locale/index.ts` | en-US + zh-CN 各加一条 `menu.business.material` | 合同 3.4 |

**明确不改**：`SeedData.cs` / AddPolicy 块 / `NumberTargetTypes.cs`（种子已在 main，合同 4.1 红线）。

### 4.2 前端依赖检查：`api/measurementUnit.ts`
物料表单下拉 + 列表 map 依赖 `getAllMeasurementUnits()`。实现第一步先探明：
- 若已存在 → 直接 import 复用
- 若不存在 → 新建（per-file，零冲突）

### 4.3 验证（本 worktree 自验）
```bash
dotnet build backend/OneCup.sln
dotnet test backend/OneCup.sln
dotnet ef database update --project backend/src/OneCup.Infrastructure --startup-project backend/src/OneCup.Api
cd frontend && npm run build
```
全绿即自验通过。合并期验证走合同 6.5。

### 4.4 单测覆盖 `backend/tests/OneCup.UnitTests/Material/`
照搬 Color 测试结构：
- `MaterialServiceTests`：Create（事务取号、字段赋值）、Update、Status、Delete、Get（分页 total 不跳号）、GetAllActive
- `MaterialSpecsTests`：FilterSpec/PagedSpec/ActiveSpec/ByIdSpec 条件覆盖

### 4.5 风险与缓解
| 风险 | 等级 | 缓解 |
|---|---|---|
| ModelSnapshot 合并冲突（物料 vs 工序都改 schema） | 🔴 | 不在本轮处理，走合同 6.4 |
| `api/measurementUnit.ts` 未暴露需新建 | 🟢 | per-file 零冲突；实现第一步探明 |
| category 自由文本导致查询分散（"染料" vs "染料 "） | 🟡 | 本轮 keyword 用 Contains（无 trim 归一），接受；后续枚举化时治 |
| FK Restrict 导致删单位报错，无前置校验 | 🟢 | 本轮无删单位需求；留 TODO，后续补"解绑物料"校验 |
| 未为 material 配编号规则 → c02 阻塞 | 🟢 | 预期行为（c02 设计如此）；用户先去编号管理为"原料"配规则 |

### 4.6 不做的事（YAGNI 收口）
- ❌ 物料-工序关联 / BOM（合同明确本轮独立）
- ❌ CAS 号、安全库存、供应商（富字段已排除）
- ❌ category 枚举化/下拉（自由文本，后续优化）
- ❌ 批量删除（本轮单条 Popconfirm，批量留 `<Space/>` 预留位）
- ❌ 导出下载（工具栏右侧 `<Space/>` 本轮留空）
- ❌ detail 用独立页（Drawer 即可）

---

## 5. 架构定位

本模块是 **Color（带编号对象）+ Customer（有 delete + 列表标准）两个既有模式的合成**：
- 取 Color 的"事务内编号生成 + 启停状态接口 + Filter/Paged Spec 分离"骨架。
- 取 Customer 的"物理删除 + 单 Card 列表标准 + c02 表单 + Drawer 详情 + Popconfirm 单删"前端形态。
- 注入染化料语义字段（spec / category / unit 关联）。
- **无新架构决策**，完全落在新约定（c01/c02/列表标准/并行合同）既有的轨道内。
