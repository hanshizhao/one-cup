# 工序管理（Process Management）设计

> 状态：已批准（用户审阅通过）
> 日期：2026-07-04
> 分支：`feat/process-mgmt`（worktree `.worktrees/process-mgmt`）
> 并行约定：`docs/parallel-dev-contract-v2.md` 任务②

---

## 1. 背景与范围

工序（Process）= 面料开发里的生产步骤（如染色、织造、定型）。本轮从零建工序管理模块：带
**自动编号**、**软删除**的业务对象，结构与客户（Customer）同构。

**本轮不做**：
- 工序与物料的关联（合同 §4.2 明确：本轮独立，不加 MaterialCode 外键）。
- BOM / 工序路线 / 工序工时核算。
- 编号规则种子化（合同 FAQ Q4：规则是运行时配置，只种 target_type 让引擎认识 `process`）。
- 扩展 Specification 基类（ThenBy 多字段排序）——属跨模块共享改动，违反并行边界，本轮不做。

### 参考实现（同构模块）
- **Customer**（业务模块 + 编号对象 + 软删除）：实体/配置/服务/控制器/前端全栈模板。
- **Color**（编号对象的 `previewCode` 创建流程 + SortOrder 单字段排序）。

---

## 2. 已确认的关键决策（头脑风暴结论）

| 决策点 | 选择 | 理由 |
| --- | --- | --- |
| 实体字段集 | 轻量：Code + Name + Category + SortOrder + Remark + IsActive + IsDeleted | 够用不臃肿 |
| **Name 唯一性** | **分类内唯一**（同 Category 下唯一，跨 Category 可同名） | 工序实际：不同分类可能都有「检验」「包装」等通用名 |
| **列表默认排序** | **SortOrder 升序（单字段）** | 贴合工序=生产顺序的语义；受限于 Specification 基类只支持单字段 OrderBy |
| developer 角色 | 默认授 `process:read`（只读） | 与 material/customer/color 看齐 |
| admin 角色 | 走通配 `*`，**不**加 role_permissions 行 | 与现有 seed 一致 |

### 关于「SortOrder 单字段排序」的约束说明

调查发现 `Specification<T>` 基类（`backend/src/OneCup.Application/Specifications/Specification.cs`）
与 `Repository<T>.ApplySpecification`（`backend/src/OneCup.Infrastructure/Persistence/Repository.cs`）
**只支持单字段排序，无 ThenBy**：

- 基类只有一个 `OrderBy` + 一个 `OrderByDescending` 插槽，无 `ApplyThenBy`。
- Repository 翻译时 `if (spec.OrderBy is not null) query.OrderBy(...)`，无 ThenBy 链。

因此「SortOrder 升序 + CreatedAt 降序」的二级排序**无法用现有 spec 表达**。两条出路：

- **方案A（采用）**：`ProcessPagedSpec` 仅 `ApplyOrderBy(SortOrder)` 单字段。遵守 YAGNI + 并行
  边界，与 Color 现状一致。SortOrder 相同时的次序不稳定，可接受（用户用不同 SortOrder 区分工序顺序）。
- 方案B（否决）：扩展 Specification 基类加 ThenBy——属跨模块共享文件改动，违反合同 §FAQ Q2
  （不能单方面改可能被对方也需要的共享改动），且 Color/Customer 也会受益但本轮无需求。

### 关于 `NumberTargetTypes.cs` 的弃用注释冲突

该文件注释写「业务代码不应硬编码引用，改用字典查询」，但 `CustomerService` / `ColorService` 实际
都在用 `NumberTargetTypes.Customer` / `.Color` 常量调 `GenerateAsync`。**本轮沿用 Customer/Color
的既成事实**：加 `public const string Process = "process";` 并在 `ProcessService` 中引用。
（合同 §3.1 / §4.2 也明确要求加此常量。）注释与代码的不一致是既有遗留，不在本轮修复范围。

---

## 3. 实体设计

```csharp
// backend/src/OneCup.Domain/Entities/Process.cs
public class Process : BaseEntity, ISoftDeletable
{
    public string Code { get; set; } = string.Empty;       // 编号引擎生成，唯一
    public string Name { get; set; } = string.Empty;       // 工序名称，分类内唯一，≤50
    public string? Category { get; set; }                  // 工序分类（前处理/染色/后整理…），≤50，可空
    public int SortOrder { get; set; } = 0;                // 排序号，默认 0
    public string? Remark { get; set; }                    // 备注，≤500，可空
    public bool IsActive { get; set; } = true;
    public bool IsDeleted { get; set; } = false;
}
```

表 `processes`，列 snake_case。索引：
- `HasIndex(Code).IsUnique()` — 编号全局唯一。
- `HasIndex(Name, Category).IsUnique()` — 分类内唯一。**注意**：PostgreSQL 中 NULL 值在唯一
  索引里互不冲突（多个 Category=NULL 的同名记录不会被索引拦截），故「分类内唯一」的语义需在
  **应用层 spec（`ProcessByNameSpec`）补判空逻辑**兜底，DB 索引只覆盖 Category 非空的场景。
- `HasQueryFilter(p => !p.IsDeleted)` — 全局软删除过滤。

---

## 4. 查询规范（Specifications）

```csharp
// ProcessFilterSpec —— 无分页，给 CountAsync 用（total 不受分页污染）
//   Where: keyword(Name/Code 模糊) && category(精确) && isActive
ProcessFilterSpec(string? keyword, string? category, bool? isActive)

// ProcessPagedSpec —— 过滤 + ApplyOrderBy(SortOrder) + 分页
ProcessPagedSpec(string? keyword, string? category, bool? isActive, int page, int pageSize)

// ProcessByIdSpec —— Id 精确
ProcessByIdSpec(Guid id)

// ProcessByNameSpec —— (Name, Category) 组合查重，配 AnyIgnoringFiltersAsync 绕软删
//   关键：Category 为 null 时，spec 层显式判空（c.Category == null），
//         不依赖 DB 唯一索引对 NULL 的处理。
ProcessByNameSpec(string name, string? category, Guid? excludingId = null)
```

`ProcessByNameSpec` 的 predicate（分类内唯一 + 空分类兜底）：
```csharp
ApplyCriteria(p =>
    p.Name == name &&
    (category == null ? p.Category == null : p.Category == category) &&
    (exclude == null || p.Id != exclude.Value));
```

---

## 5. 服务层（Application）

`IProcessService` / `ProcessService` 与 `ICustomerService` / `CustomerService` 同构：

- 注入 `IRepository<Process>` + `IUnitOfWork` + `INumberingService` + `IValidator<CreateProcessRequest>` + `IValidator<UpdateProcessRequest>`。
- `CreateAsync`：校验 → `AnyIgnoringFiltersAsync(new ProcessByNameSpec(name, category))` 查重
  → `ExecuteInTransactionAsync { GenerateAsync(NumberTargetTypes.Process, null, ct) + AddAsync + SaveChangesAsync }`
  → 返回 `GetByIdAsync`。
- `UpdateAsync`：校验 → 取实体（NotFound 抛 DomainException）→ 改名/改分类时
  `AnyIgnoringFiltersAsync(new ProcessByNameSpec(name, category, id))` 查重 → 赋值 → SaveChanges（无编号操作，不需事务）。
- `DeleteAsync`：`GetByIdAsync`（绕软删过滤器，幂等重删返回 204）→ 置 `IsDeleted=true` → SaveChanges。
- `GetListAsync` / `GetByIdAsync`：映射到 DTO。

DTO（`ProcessDtos.cs`）：`ProcessListItemDto`（表格行）、`ProcessDto : ProcessListItemDto`（详情，加 Remark/UpdatedAt）、`CreateProcessRequest`（无 Code）、`UpdateProcessRequest`（同字段独立类）。

Validators（`Validators/System/Create|UpdateProcessRequestValidator.cs`）：`Name NotEmpty().MaximumLength(50)`；`Category/Remark` 用 `.MaximumLength().When(!IsNullOrEmpty)`。

---

## 6. API（Controller）

```csharp
[ApiController, Route("api/processes"), Authorize(Policy = "process:read")]
public class ProcessesController : ControllerBase
{
    GET     /                  // 分页列表（keyword/category/isActive/page/pageSize）
    GET     /{id}              // 详情
    POST    /                  // [Audit] + Authorize("process:create") → CreatedAtAction
    PUT     /{id}              // [Audit] + Authorize("process:update")
    DELETE  /{id}              // [Audit] + Authorize("process:delete") → NoContent
}
```

---

## 7. 种子数据与权限（共享文件改动，仅本任务）

按合同 §3.1 / §4.2，本任务是**唯一**新增编号资源的模块：

- **`SeedData.cs`** 末尾加 5 个常量（紧接 `32a` / `0206`，连续递增）：
  - `PermProcessRead=...032b` / `Create=...032c` / `Update=...032d` / `Delete=...032e`
  - `TargetTypeProcess=...0207`
- **`NumberTargetTypes.cs`** 末尾加 `public const string Process = "process";`。
- **`OneCupDbContext.cs`**：
  - 加 `// ===== Process 模块 =====` + `public DbSet<Process> Processes => Set<Process>();`
  - `Seed()` 权限 `HasData` 加 4 条 `process:read/create/update/delete`（Id 用上述常量）。
  - `Seed()` `NumberingTargetType.HasData` 加 1 条 `process/工序/Process/SortOrder=7/IsActive=true`（Id=TargetTypeProcess）。
  - `Seed()` `developerPerms` 数组**追加** `SeedData.PermProcessRead`（developer 只读，与 material/customer/color 看齐）。
  - admin 角色走通配 `*`，**不**加 role_permissions 行（与现有一致）。
- **`Program.cs`**：
  - AddScoped 区加 `builder.Services.AddScoped<IProcessService, ProcessService>();`
  - AddPolicy 块紧接 `product:delete` 之后加 4 条 `process:read/create/update/delete`。
- **EF 迁移** `AddProcessModule`：`dotnet ef migrations add AddProcessModule --project src/OneCup.Infrastructure --startup-project src/OneCup.Api`（在 `backend/` 下）。

---

## 8. 前端

### API（`frontend/src/api/process.ts`）
类型 `ProcessListItem` / `ProcessDetail` / `ProcessPagedResult` / `ProcessQuery` / `ProcessFormData`；
函数 `getProcesses` / `getProcess` / `createProcess` / `updateProcess` / `deleteProcess`。双泛型 `<unknown, T>`。

### 页面（`frontend/src/pages/business/process/`）— **从模板复制骨架**
遵守 AGENTS.md「列表查询页标准」+ Convention **c02**（创建流程）+ **c01**（删除确认）：

- **`index.tsx`**：从 `docs/specs/templates/query-table-page.template.tsx` 复制骨架。
  单 Card 包整页；SearchForm（Form + Row gutter=24 + Col span=8）：keyword(工序名称) / category(工序分类,Input) / isActive(Select)。
  查询/重置按钮在表单**外侧**兄弟 div。工具栏 flex space-between（左：新建按钮 `process:create`；右：空 Space）。
  列：Code / Name / Category / SortOrder / IsActive(Badge) / CreatedAt / 操作。
  操作列：查看 / 编辑（`process:update`）/ 删除（`process:delete` + **Popconfirm**，c01 单条软删）。
- **`form.tsx`**：Modal 表单。打开新建时 `previewCode('process')` → `previewedCode` 只读回填 →
  `null` 则 `noRule=true`（Form disabled + Alert 警告 + okButtonProps.disabled，c02）。字段：编号(只读预览)/名称(必填)/分类/排序/备注/启用状态(Switch)。
- **`detail.tsx`**：Drawer + Descriptions 只读详情。
- **`style/index.module.less`**：从 `.less.template` 原样复制三段样式。
- **`locale/{index,en-US,zh-CN}.ts`**：`process.*` 前缀，含 `process.form.noRule.block`（指向「编号管理」配置入口）。

### 前端共享文件（§3.4，末尾追加）
- **`routes.ts`**：`menu.business.children` 末尾加 `{ name:'menu.business.process', key:'business/process', requiredPermissions:[{resource:'process',actions:['read']}] }`。
- **`router.tsx`**：顶部加 `const ProcessPage = lazy(()=>import('@/pages/business/process'));`；children 紧接 customer 加 `path:'business/process'` + `RequirePermission resource="process"`。
- **`locale/index.ts`**：en-US + zh-CN 各加 `menu.business.process`（'Process' / '工序管理'）。**不改** `menu.business` 本身标签。

---

## 9. 测试

- **`ProcessServiceTests.cs`**（InMemory + `Ignore(InMemoryEventId.TransactionIgnoredWarning)` + 自增 `FakeNumberingService`）：
  - 取号生成 Code、Name 分类内唯一（创建/改名）、改名排除自身、Code 不可变、NotFound 抛错、软删除幂等、
    按 keyword/category/isActive 过滤、total 不受分页污染、按 SortOrder 排序返回。
  - **命名空间别名**：`using ProcessEntity = OneCup.Domain.Entities.Process;` 避免与 `System.Diagnostics.Process` 混淆（类比 Customer 测试对 `OneCup.UnitTests.Customer` 命名空间的处理）。
- **`ProcessValidatorTests.cs`**：空 Name 无效、Name 超长无效、Category/Remark 超长无效、合法请求通过。

---

## 10. 验证清单

1. `dotnet build backend/OneCup.sln` 绿。
2. 生成的迁移 `Up()` 建 `processes` 表 + 4 权限种子 + 1 目标类型种子。
3. `dotnet test backend/OneCup.sln` 全绿。
4. `cd frontend && npm run build` 绿。

---

## 11. 不做的事（合同红线）

- 不动 SeedData 已占用的 `301-32a` / `0201-0206` Guid 段。
- 不硬编码编号规则种子（FAQ Q4）。
- 不加 MaterialCode 外键（§4.2）。
- 不改 Redux store / api 目录聚合（无聚合文件，§2.3）。
- 不手写 `ApplyConfiguration`（`ApplyConfigurationsFromAssembly` 自动扫描，§3.3）。
- **不改 `Specification<T>` 基类**（跨模块共享，违反并行边界）。
- ModelSnapshot 冲突留合并阶段方案 B 处理（§6.4）。
