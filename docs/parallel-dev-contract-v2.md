# 并行开发协作约定 v2（物料管理 × 工序管理 × 编号分类码优化）

> **新会话开工前必读。** 本文件约定三个并行 worktree 同时开发时的资源分配、
> 共享文件修改规则、各任务文件边界、以及合并回 main 的标准操作。
>
> 最后更新：2026-07-04 ｜ 适用 worktree：`feat/material-mgmt`、`feat/process-mgmt`、`feat/category-code-optimize`
>
> 本约定沿用 v1（`docs/parallel-dev-contract.md`，针对 unit/color/customer，已合并完成）
> 的方法论与合并 playbook（方案 B），仅更新为本轮三个任务的资源分配。
>
> **如本文件与 v1 冲突，以本文件为准**（v1 的 Guid 分配表已过时，不可照搬）。

---

## 0. TL;DR（开工前先看这里）

- 你被分配在 **其中一个** worktree 工作，**只动你那个模块的文件**（第 4 节有逐任务文件清单）。
- 三个任务**完全可并行**。真实冲突点收窄到：①前端 3 个追加型文件（routes/router/locale）②后端 `OneCupDbContextModelSnapshot.cs`（EF 全量快照，必冲突，走第 6 节方案 B 处理）。
- 三个任务**都不动 Redux store**（项目无 per-module slice，业务状态都在组件本地 useState）。三个任务**都不改 `api/` 聚合 index**（前端 api 目录无 index 聚合，加文件天然隔离）。
- 有一批"集中式共享文件"多分支都要改——**严格遵守第 3 节的修改规则**，尤其是种子 Guid 分配（第 3.1 节），**不许抢占对方已分配的 Guid 段**。
- 合并策略走 **方案 B**：开发期各分支互不干扰各自做完，合并时统一处理 EF 冲突（第 6 节）。
- 任何不确定的事，**先看第 8 节 FAQ，再开工**。

---

## 1. worktree 与分支映射

| 任务 | worktree 路径 | 分支 | 新会话在此目录开工 |
| --- | --- | --- | --- |
| ① 物料管理 | `.worktrees/material-mgmt/` | `feat/material-mgmt` | ✅ |
| ② 工序管理 | `.worktrees/process-mgmt/` | `feat/process-mgmt` | ✅ |
| ③ 编号分类码优化 | `.worktrees/category-code-optimize/` | `feat/category-code-optimize` | ✅ |
| 主仓库 | `.`（项目根） | `main` | 合并时才用，**不要在 main 直接开发** |

> 三个 worktree 均基于 `main@45180c5`（当前 HEAD）创建。`main` 是只读基线，只用于最终合并。

**新会话开工第一步**：在新 worktree 目录里确认分支正确
```bash
git branch --show-current   # 应为 feat/material-mgmt / feat/process-mgmt / feat/category-code-optimize
git rev-parse --show-toplevel   # 确认你在哪个 worktree（见 FAQ Q5）
```

---

## 2. 冲突面总览（为什么需要本约定）

### 2.1 零冲突文件（各写各的，git 不会打架）

| 层 | 各任务新增/修改的文件（per-file，互不重叠） |
| --- | --- |
| 后端实体 | `OneCup.Domain/Entities/{Material,Process}.cs`（任务3 若改分类码实体是独占的 `NumberingCategory.cs`） |
| 后端 DTO | `OneCup.Application/Dtos/System/{Material,Process}Dtos.cs` |
| 后端服务/接口 | `OneCup.Application/{Services,Interfaces}/` 各自文件 |
| 后端 Spec/Validator | `OneCup.Application/{Specifications,Validators}/` 各自文件 |
| 后端 EF 配置 | `OneCup.Infrastructure/Persistence/Configurations/{Material,Process}Configuration.cs`（自动被 `ApplyConfigurationsFromAssembly` 扫描，**无需**手动 ApplyConfiguration） |
| 后端控制器 | `OneCup.Api/Controllers/{Materials,Processes}Controller.cs` |
| 前端 API | `frontend/src/api/{material,process}.ts`（任务3 改独占的 `numberingDictionary.ts`） |
| 前端页面 | `frontend/src/pages/business/{material,process}/` 整个目录（任务3 改 `pages/system/numbering/dict/`） |
| 测试 | `backend/tests/OneCup.UnitTests/{Material,Process,NumberingDictionary}/` |

### 2.2 高/中冲突文件（多分支都要改，需协调）

| 文件 | 冲突性质 | 风险 |
| --- | --- | --- |
| `SeedData.cs` | 仅**任务2**新增权限 Guid 段（已预分配，见 3.1） | 🟢 低（任务1/3 不碰） |
| `OneCupDbContext.cs` | 任务1/2 加 DbSet + Seed()；任务3 若改 schema 可能不碰 | 🟢 低（末尾追加，用注释块分隔） |
| `NumberTargetTypes.cs` | 仅**任务2**加 `Process` 常量一行 | 🟢 低 |
| `Program.cs` | 任务1/2 末尾加 `AddScoped`；**仅任务2**加 4 条 `AddPolicy` | 🟢 低（末尾追加） |
| `frontend/src/routes.ts` | 任务1/2 在 `business.children` 追加菜单项 | 🟡 中（末尾追加，多半自动合并） |
| `frontend/src/router.tsx` | 任务1/2 追加 lazy import + element | 🟡 中 |
| `frontend/src/locale/index.ts` | 任务1/2 在 en-US+zh-CN 各加文案 | 🟡 中 |
| `Migrations/<ts>_Add{Module}Module.cs` | 时间戳撞名 | 🟡 中（时间戳不同则不冲突） |
| **`OneCupDbContextModelSnapshot.cs`** | EF 自动重写的全量快照，每次加迁移必冲突 | 🔴 最高（走第 6.4 节专门处理） |

> **任务3 是否撞 ModelSnapshot 取决于它是否改 `NumberingCategory` schema**：
> - 若任务3 是纯前端/接口层优化（不改实体字段）→ **任务3 不产生迁移，与任务1/2 零物理冲突**，是最独立的一支。
> - 若任务3 改分类码实体（加字段/树形结构）→ 产生迁移，与任务1/2 撞 ModelSnapshot，走第 6.4 节。
> 任务3 会话需在开工时尽早确定这一点，若改 schema 请在第 3.2 节登记迁移名。

### 2.3 本轮不存在的冲突点（利好）

- **Redux store 根聚合** `frontend/src/store/index.ts`：项目无 per-module slice（全局只有 userInfo + settings），三个任务都不建 slice，**零冲突**。
- **前端 api 目录**：无 `index.ts` 聚合文件，加模块 API 文件天然隔离，**零冲突**。

---

## 3. 共享文件修改规则（开工即适用）

### 3.1 种子 Guid 分配（最关键，先定好不抢）

`SeedData.cs` 用确定性 Guid，**第 4 段是按区间分配的**。当前 main 基线已用：
- 权限：`301`–`32a`（`PermSystemAuditRead=32a` 是当前最大值）
- 目标类型：`201`–`206`

本轮三个任务的分配（**互不重叠**）：

| 资源 | 任务①物料 | 任务②工序 | 任务③分类码优化 | 已占用（不可碰） |
| --- | --- | --- | --- | --- |
| 权限 Guid（`...32X`） | **复用** `305-308` | **新增** `32b/32c/32d/32e` | **不新增** | `301-32a` 已用 |
| 目标类型 Guid（`...02XX`） | **复用** `0202` | **新增** `0207` | **不新增** | `0201-0206` 已用 |

**具体落点：**

```csharp
// === 任务①物料管理：复用已有，无需新增任何 Guid ===
//   PermMaterialRead=305, Create=306, Update=307, Delete=308  已存在
//   TargetTypeMaterial=0202, NumberTargetTypes.Material="material"  已存在
//   Program.cs 的 material:read/create/update/delete 四条 AddPolicy 已注册

// === 任务②工序管理：唯一需要新增 Guid 的模块 ===
public static readonly Guid PermProcessRead    = Guid.Parse("00000000-0000-0000-0000-00000000032b");
public static readonly Guid PermProcessCreate  = Guid.Parse("00000000-0000-0000-0000-00000000032c");
public static readonly Guid PermProcessUpdate  = Guid.Parse("00000000-0000-0000-0000-00000000032d");
public static readonly Guid PermProcessDelete  = Guid.Parse("00000000-0000-0000-0000-00000000032e");
public static readonly Guid TargetTypeProcess  = Guid.Parse("00000000-0000-0000-0000-000000000207");

// === 任务③分类码优化：不新增 Guid（复用 system:numbering:* 权限） ===
```

> **为什么任务2 从 `32b` 起、目标类型从 `207` 起？** 紧接当前 main 最大值，连续递增，无空洞。
> `32f` 及之后、`208` 及之后为本轮缓冲，现阶段三个分支都不许用。

> **任务①物料注意**：物料是三个任务里种子基础最完备的——权限 `material:*`（305-308）、
> 编号目标类型 `material`（0202）、`NumberTargetTypes.Material` 常量、`Program.cs` 的 AddPolicy
> **全部已在 main 基线**。物料管理**不新增任何 Guid、不改 SeedData.cs、不改 AddPolicy 块**，
> 是改动面最小的模块。

> **任务②工序注意**：工序需要：①`SeedData.cs` 加 5 个常量（4 权限 + 1 目标类型）；
> ②`NumberTargetTypes.cs` 加 `public const string Process = "process";` 一行；
> ③`Program.cs` AddPolicy 块加 4 条 `process:*` 策略；④`OneCupDbContext.Seed()` 加 4 条权限种子
> + 1 条目标类型种子 + admin 角色关联。详见第 4.2 节。

### 3.2 EF 迁移文件命名（防撞名）

各分支加迁移时，EF 的 `migrations add` 会自动生成秒级时间戳前缀。为防撞名，约定迁移名：

| 任务 | 迁移名（让 EF 自动加时间戳前缀） |
| --- | --- |
| ① 物料 | `AddMaterialModule` |
| ② 工序 | `AddProcessModule` |
| ③ 分类码 | `OptimizeNumberingCategory`（**仅当改 schema 时才加**；纯前端优化则无迁移） |

```bash
# 在各 worktree 的 backend 目录下执行（注意 --project/--startup-project 相对路径）
cd backend
dotnet ef migrations add AddMaterialModule --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```

EF 时间戳精确到秒，各分支不太可能同一秒执行；**若真的撞了**，手动改一个文件名的时间戳
后缀（改大），确保各份迁移在合并后能按时间线排列。

### 3.3 `OneCupDbContext.cs` 改法

任务1/2 需要：
1. 加 `public DbSet<Material> Materials => Set<Material>();`（工序同理）。
   - **注意**：`OnModelCreating` 用 `ApplyConfigurationsFromAssembly` 自动扫描，**不需要**手动 `ApplyConfiguration(new XxxConfiguration())`。
2. 在 `Seed()` 方法里追加种子（仅任务2 需要种权限 + 目标类型）。

**约定**：各自在文件**末尾**追加自己的代码块，用注释标注模块名（参考现有 `// ===== Unit 模块 =====` 风格）：
```csharp
// ===== Material 模块 =====
public DbSet<Material> Materials => Set<Material>();
```
合并时冲突会落在这些相邻区块，人工选择保留两边即可。

### 3.4 前端三个集中文件改法

- `routes.ts`：在 `routes[0].children`（即 `menu.business` 的 children，当前只有 customer 一项）数组**末尾**追加你的菜单项。**菜单归属决策：物料、工序都放 `business` 域**（与 customer 同组），不新增顶级域，不改 `layout.tsx` 图标。
- `router.tsx`：追加 lazy import（顶部）和路由 element（children 数组）。
- `locale/index.ts`：**en-US 和 zh-CN 两个对象都要加** `menu.business.{material,process}` 文案。

约定各自在数组/对象末尾追加，合并时大概率 git 自动 merge，小概率手动保留两边。

> **任务③分类码注意**：任务3 不新增菜单项（分类码是 numbering 页内的 Tab，已存在），
> **不改 routes.ts / router.tsx**。它若需要前端文案，只在 `pages/system/numbering/dict/locale/`
> 模块级 locale 改（per-file，零冲突），不动全局 `locale/index.ts`。

### 3.5 `Program.cs` 改法

- 任务1/2：末尾追加 `builder.Services.AddScoped<IMaterialService, MaterialService>();`（工序同理），纯追加。
- **任务2 额外**：在 AddPolicy 块（第 148-194 行附近）追加 4 条 `process:*` 策略，紧接现有 `product:*` 之后：
```csharp
options.AddPolicy("process:read", p => p.RequireClaim("perm_codes", "process:read"));
options.AddPolicy("process:create", p => p.RequireClaim("perm_codes", "process:create"));
options.AddPolicy("process:update", p => p.RequireClaim("perm_codes", "process:update"));
options.AddPolicy("process:delete", p => p.RequireClaim("perm_codes", "process:delete"));
```

---

## 4. 各任务文件边界与业务要点

> 每个会话只需读本节中自己任务的小节即可明确"我该动哪些文件、怎么动"。

### 4.1 任务①：物料管理

**现状**：main 上已有物料的权限种子 + 编号目标类型种子，但**零实体/零页面/零 Controller**。需从零建模块（种子已备好）。

**会新增的文件（全部 per-file，零冲突）：**

后端：
- `backend/src/OneCup.Domain/Entities/Material.cs`
- `backend/src/OneCup.Infrastructure/Persistence/Configurations/MaterialConfiguration.cs`
- `backend/src/OneCup.Application/Dtos/System/MaterialDtos.cs`
- `backend/src/OneCup.Application/Interfaces/IMaterialService.cs`
- `backend/src/OneCup.Application/Services/MaterialService.cs`
- `backend/src/OneCup.Application/Specifications/MaterialSpecs.cs`
- `backend/src/OneCup.Api/Controllers/MaterialsController.cs`
- `backend/src/OneCup.Infrastructure/Migrations/<ts>_AddMaterialModule.cs` (+ `.Designer.cs`)
- `backend/tests/OneCup.UnitTests/Material/...`

前端：
- `frontend/src/api/material.ts`
- `frontend/src/pages/business/material/index.tsx`（**从 `docs/specs/templates/query-table-page.template.tsx` 复制改名**，按 `【替换点】` 注释改字段/列/接口，**不从零手写布局**）
- `frontend/src/pages/business/material/form.tsx`
- `frontend/src/pages/business/material/style/index.module.less`（从 `.less.template` 复制）
- `frontend/src/pages/business/material/locale/{index,en-US,zh-CN}.ts`

**会修改的共享文件：**
- `OneCupDbContext.cs`：加 `DbSet<Material>`（见 3.3）
- `Program.cs`：加 `AddScoped<IMaterialService, MaterialService>()`（见 3.5）
- `OneCupDbContextModelSnapshot.cs`：EF 自动重写（见 6.4）
- `frontend/src/routes.ts` / `router.tsx` / `locale/index.ts`：加物料菜单 + 文案（见 3.4）
- **不改** `SeedData.cs` / `Program.cs` AddPolicy 块 / `NumberTargetTypes.cs`（物料种子已在 main）

**业务要点：**
- **物料是带编号的业务对象，必须遵守 Convention c02**（`docs/conventions/c02-numbered-object-create.md`）：
  新建表单调 `previewCode("material", categoryCode?)` 预览 → 只读回填编号字段 → 无规则则禁用表单 + Alert 提示。
- 后端 `MaterialService.CreateAsync` **必须在事务内调编号引擎** `_numbering.GenerateAsync(NumberTargetTypes.Material, categoryCode, ct)`（参考 `ColorService.CreateAsync` 实现）。
- 列表页严格遵守 AGENTS.md「列表查询页标准」：单 Card 包整页、Form+Grid 三列、查询/重置按钮在表单外侧兄弟 div。
- 删除操作按 Convention c01（`docs/conventions/c01-delete-confirm.md`）选 Popconfirm 或 Modal。
- **物料与工序在本轮不关联**（已确认独立），不要为 BOM 预留外键字段。

### 4.2 任务②：工序管理

**现状**：完全空白（无权限、无目标类型、无实体、无页面），从零起步。

**会新增的文件（结构与物料同构，全部 per-file，零冲突）：**

后端：把任务1 的 Material 换成 Process 即可（`Process.cs` / `ProcessConfiguration.cs` / `ProcessDtos.cs` / `IProcessService.cs` / `ProcessService.cs` / `ProcessSpecs.cs` / `ProcessesController.cs` / `<ts>_AddProcessModule.cs` / 测试 `Process/`）。

前端：`api/process.ts` + `pages/business/process/` 整个目录。

**会修改的共享文件（任务2 改动面最大，因为要新增编号资源）：**
- `SeedData.cs`：加 5 个常量（见 3.1）
- `NumberTargetTypes.cs`：加 `public const string Process = "process";`
- `OneCupDbContext.cs`：加 `DbSet<Process>` + `Seed()` 里加 4 条权限种子 + 1 条目标类型种子 + admin 角色关联（参考现有 `PermMaterialRead` 等的种子写法）
- `Program.cs`：加 `AddScoped<IProcessService, ProcessService>()` + 4 条 `process:*` AddPolicy（见 3.5）
- `OneCupDbContextModelSnapshot.cs`：EF 自动重写（见 6.4）
- `frontend/src/routes.ts` / `router.tsx` / `locale/index.ts`：加工序菜单 + 文案（见 3.4）

**业务要点：**
- **工序需要自动编号**（已确认）。遵守 Convention c02，新建表单调 `previewCode("process", categoryCode?)`。
- 后端 `ProcessService.CreateAsync` 在事务内调 `_numbering.GenerateAsync(NumberTargetTypes.Process, categoryCode, ct)`。
- **工序编号目标类型 `process` 是全新的**——需要：①`SeedData.TargetTypeProcess=0207`；②`NumberTargetTypes.Process="process"`；③`DbContext.Seed()` 种一条 `numbering_target_types` 记录（code=process, name=工序, isActive=true）。注意：编号规则本身是运行时配置（用户在编号管理页为 process 配规则），**不要在代码里硬编码编号规则种子**（见 FAQ Q4）。
- **工序与物料在本轮不关联**（已确认独立），实体不建 MaterialCode 外键字段。
- 列表页、删除、表单标准同任务1。

### 4.3 任务③：编号分类码优化

**本质需求**：打通"业务对象创建表单 ↔ 编号分类码字典"的消费链路。
典型场景：颜色编号规则下有多个分类码（深色/中色/浅色），新建颜色时用户需要**选择**用哪个分类码，而非系统默认。

**现状**：分类码功能（`NumberingCategory` 实体 + CRUD + 引擎强校验 + 前端管理页）**已完整实现**。本次是"优化"，不是从零建。

**会修改的文件（集中在编号字典模块自身 + 消费端表单，per-file 居多）：**

后端：
- `backend/src/OneCup.Application/Services/NumberingDictionaryService.cs`（可能扩展查询能力）
- `backend/src/OneCup.Api/Controllers/NumberingDictionaryController.cs`（可能加端点）
- `backend/src/OneCup.Application/Dtos/System/NumberingDictionaryDtos.cs`（可能调 DTO）
- `backend/src/OneCup.Domain/Entities/NumberingCategory.cs`（**仅当加字段才改**）
- 若改实体字段 → 加迁移 `<ts>_OptimizeNumberingCategory.cs`

前端：
- `frontend/src/api/numberingDictionary.ts`（可能加查询方法）
- `frontend/src/pages/system/numbering/dict/index.tsx`（管理页优化）
- **消费端表单**：如 `frontend/src/pages/master-data/color/` 的表单（若让颜色支持分类选择）、`pages/business/customer/` 等

**会修改的共享文件：**
- **若不改 schema**：几乎不碰共享文件。`routes.ts`/`router.tsx`/`store`/`SeedData`/`Program.cs` 都不改。与任务1/2 **零物理冲突**。
- **若改 schema（加分类字段）**：仅 `OneCupDbContextModelSnapshot.cs` 会与任务1/2 撞（走 6.4）。

**业务要点：**
- **复用 `system:numbering:read/create/update` 权限**，不新增权限点。
- **不新增菜单项**（分类码是 numbering 页内已有 Tab）。
- 若给分类码加字段（如排序 sortOrder、描述、颜色标记等），需走 EF 迁移，并在第 3.2 节登记迁移名 `OptimizeNumberingCategory`。
- 消费端表单（如颜色表单）拉分类列表：调用 `numberingDictionary` 的"按 targetType 拉启用分类"接口（后端已有 `CategoryActiveByTypeSpec`，可能需暴露为 API 端点）。
- 开工时**尽早确定是否改 schema**，若不改则本任务是三个里最独立的，可与任务1/2 完全并行无障碍。

---

## 5. 命名与菜单速查

```
分支 / worktree / 迁移名：
  物料    feat/material-mgmt          .worktrees/material-mgmt/          AddMaterialModule
  工序    feat/process-mgmt           .worktrees/process-mgmt/           AddProcessModule
  分类码  feat/category-code-optimize .worktrees/category-code-optimize/ OptimizeNumberingCategory（仅改 schema 时）

菜单归属（routes.ts）：
  物料 → menu.business.children 末尾追加  key='business/material'
  工序 → menu.business.children 末尾追加  key='business/process'
  分类码 → 不新增菜单项

EF 迁移命令（在各 worktree 的 backend 目录下）：
  dotnet ef migrations add <Name> --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```

---

## 6. 合并策略：方案 B（开发期并行，收尾统一处理）

**核心思路**：开发期各 worktree 互不通信、互不 rebase，各自独立做完。合并时，
**按顺序逐个合入 main，后合者解决与已合分支的冲突**。

### 6.1 合并顺序

**建议顺序：分类码优化（任务③）→ 物料管理（任务①）→ 工序管理（任务②）**。

理由：
- **任务3 改动最局部**（只碰编号字典模块自身 + 可能的消费端表单），与任务1/2 文件交集最小，先合风险最低。
- **任务1（物料）**种子最完备（零新增 Guid），改动面小，第二合。
- **任务2（工序）**是唯一新增编号资源 + 权限 Guid 的模块，放最后合，这样它合并时面对的是"分类码+物料已合入的 main"，一次性把 EF 快照对齐到三方终态，减少中间态冲突次数。
- 但这**不是强制的**——谁先开发完谁先合，顺序可调整，只要遵循"逐个合、后合者解冲突"。

### 6.2 第一个分支合并（以任务3 分类码为例）

```bash
cd "C:/Users/mi/Desktop/work_space/one-cup"   # 回主仓库
git checkout main
git merge --no-ff feat/category-code-optimize
# 第一个合入的，无冲突源，直接合入
```

### 6.3 后续分支合并（物料、工序依次合）

```bash
cd "C:/Users/mi/Desktop/work_space/one-cup/.worktrees/material-mgmt"
git fetch ../..  main:main-local   # 拿到合了分类码之后的 main
git merge main-local                # 此时会冲突，重点在以下几个文件
```

**预期冲突文件与解决方式（每合一个分支都会出现一次）：**

| 文件 | 解决方式 |
| --- | --- |
| `SeedData.cs` | 仅任务2 新增 Guid 段（按 3.1 预分配 `32b-32e`/`207`），不与其他分支重叠，保留即可 |
| `NumberTargetTypes.cs` | 仅任务2 加 1 行，保留即可 |
| `OneCupDbContext.cs` | DbSet / Seed() 各分支有独立注释块，全部保留 |
| `Program.cs` | AddScoped / AddPolicy 末尾追加，保留各分支 |
| `OneCupDbContextModelSnapshot.cs` | 🔴 **必冲突**。见 6.4 专门处理 |
| `{timestamp}_Add{Module}Module.cs` 各迁移文件 | 时间戳不同则不冲突；撞名则改一个时间戳 |
| `routes.ts` / `router.tsx` / `locale/index.ts` | 数组/对象末尾追加，保留各分支（任务3 不碰这些） |

### 6.4 ModelSnapshot 冲突处理（最关键）

EF 的 `ModelSnapshot` 是自动生成的全量快照，**多个分支的版本不能简单手动拼接**。
正确做法（在主仓库 main 上，解决完其他所有冲突后）：

```bash
cd "C:/Users/mi/Desktop/work_space/one-cup"
# 先"接受当前分支版本"占位
git checkout --ours backend/src/OneCup.Infrastructure/Migrations/OneCupDbContextModelSnapshot.cs

# 重新生成快照（基于当前合并后的全部模型）
dotnet ef migrations add MergeMaterialProcessCategory \
  --project backend/src/OneCup.Infrastructure --startup-project backend/src/OneCup.Api
# 这会基于"合体后的 DbContext"生成一个新的空迁移 + 正确的全量快照

# 检查新生成的迁移文件，如果 Up/Down 是空的（说明模型已一致），删掉这个临时迁移，
# 只保留它刷新出的 ModelSnapshot.cs：
rm backend/src/OneCup.Infrastructure/Migrations/*_MergeMaterialProcessCategory.cs
rm backend/src/OneCup.Infrastructure/Migrations/*_MergeMaterialProcessCategory.Designer.cs
# ModelSnapshot.cs 已被上一步刷新，保留它
```

> **原理**：`migrations add` 会用当前 DbContext 模型重新生成 `ModelSnapshot.cs`，
> 这个快照才是"main 的最终真相"。临时迁移只为触发快照刷新，模型若已一致其 Up/Down 为空，
> 删掉即可。**务必跑 `dotnet ef database update` 验证能正确应用。**

### 6.5 合并后验证

```bash
cd "C:/Users/mi/Desktop/work_space/one-cup"
# 后端：构建 + 测试 + 迁移应用到本地库验证
dotnet build backend/OneCup.sln
dotnet test backend/OneCup.sln
dotnet ef database update --project backend/src/OneCup.Infrastructure --startup-project backend/src/OneCup.Api

# 前端：构建 + 测试
cd frontend && npm run build && npm test
```

全绿后，删掉 worktree：
```bash
cd "C:/Users/mi/Desktop/work_space/one-cup"
git worktree remove .worktrees/material-mgmt
git worktree remove .worktrees/process-mgmt
git worktree remove .worktrees/category-code-optimize
git branch -d feat/material-mgmt feat/process-mgmt feat/category-code-optimize
```

---

## 7. 资源分配总表（一图速查）

```
种子 Guid 第 4 段分配（本轮）：
  301–32a  已用（main 基线，勿动）
  32b–32e  任务②工序（PermProcessRead/Create/Update/Delete）
  32f+     缓冲，本轮三分支都不许用

  0201–0206 已用（目标类型，main 基线，勿动）
  0207      任务②工序（TargetTypeProcess）
  0208+     缓冲

  任务①物料：复用 305-308（权限）+ 0202（目标类型），零新增
  任务③分类码：不新增任何 Guid

菜单归属：物料、工序均 → menu.business.children

迁移命名：
  物料 AddMaterialModule   工序 AddProcessModule   分类码 OptimizeNumberingCategory（仅改 schema 时）

合并顺序建议：
  分类码（先，最局部）→ 物料（中，零新增 Guid）→ 工序（后，唯一新增编号资源）
```

---

## 8. FAQ

**Q1：我开工时要不要先 rebase main？**
不要。三个 worktree 都基于同一基线 `main@45180c5`，开发期 main 也不会动（约定不在 main 直接开发）。
合并阶段（第 6 节）才处理同步。

**Q2：我发现需要新增一个本约定没提到的共享文件改动（比如新枚举、新常量）。**
停下来，先在 issue 或对话里提出，**不要单方面新增可能被对方也需要的共享改动**。

**Q3：EF 迁移加完发现模型和快照对不上 / `dotnet ef migrations add` 报错。**
确保你只在自己的 worktree 里改了自己的实体 + Configuration，且 `DbContext` 里已加 `DbSet`。
EF Configuration 会被 `ApplyConfigurationsFromAssembly` 自动扫描，**无需**手动 ApplyConfiguration。
EF 会基于"DbContext 当前认识的全部模型"生成快照。

**Q4：工序编号规则（前缀、流水位、日期段等）要不要在代码里种子化？**
**不要。** 全系统的编号规则都是运行时配置（用户在编号管理页为各目标类型配置规则）。
你只需要：①种 `numbering_target_types` 的 process 记录（让引擎认识 process 这个类型）；
②业务代码里调 `GenerateAsync` 时传 `NumberTargetTypes.Process`。规则配置由用户在前端完成。

**Q5：我在哪个 worktree？**
```bash
git rev-parse --show-toplevel
# 输出 .../one-cup/.worktrees/material-mgmt          → 你在物料管理
# 输出 .../one-cup/.worktrees/process-mgmt           → 你在工序管理
# 输出 .../one-cup/.worktrees/category-code-optimize → 你在分类码优化
# 输出 .../one-cup                                    → 你在主仓库（别在这开发）
```

**Q6：任务3 到底改不改 schema？**
开工时先评估需求。如果你的优化（如"新建颜色时选分类码"）通过**暴露查询接口 + 前端下拉**
就能实现，则不改 schema，本任务与任务1/2 完全独立并行。只有当你确实需要给
`NumberingCategory` 加新字段（排序、描述、颜色标记、父子层级等）时才改 schema，
此时请按 3.2 登记 `OptimizeNumberingCategory` 迁移，合并时走 6.4。

**Q7：新建物料/工序的列表页要遵守什么标准？**
严格遵守 AGENTS.md「列表查询页标准」+ `docs/frontend-standards.md`，从
`docs/specs/templates/query-table-page.template.tsx` 复制改名，**不从零手写布局**。
带编号的对象创建必须走 Convention c02；删除操作按 Convention c01。
