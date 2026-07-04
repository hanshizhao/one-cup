# 并行开发协作约定（单位管理 × 颜色管理 × 客户管理）

> **新会话开工前必读。** 本文件约定三个并行 worktree 同时开发时的资源分配、
> 共享文件修改规则、以及合并回 main 的标准操作。
>
> 最后更新：2026-07-04 ｜ 适用 worktree：`feat/unit-mgmt`、`feat/color-mgmt`、`feat/customer-mgmt`
>
> **变更说明**：客户管理为后期新增分支。它**完全复用 main 基线已有的种子**
>（权限 107/108、目标类型 204），不占用任何新 Guid 段，因此对单位/颜色分支
> 的开发**零影响**——单位/颜色分支手里的旧版本约定对它们仍然有效，合并时
> 自动同步到本最新版。

---

## 0. TL;DR（开工前先看这里）

- 你被分配在 **其中一个** worktree 工作，**只动你那个模块的文件**。
- 有一批"集中式共享文件"多分支都要改——**严格遵守本文件第 3 节的修改规则**，
  尤其是种子 Guid 分配（第 3.1 节），**不许抢占对方已分配的 Guid 段**。
- 合并策略走 **方案 B**：开发期各分支互不干扰各自做完，合并时统一处理 EF 冲突（第 4 节）。
- 任何不确定的事，**先看本文件第 5 节 FAQ，再开工**。

---

## 1. worktree 与分支

| 模块 | worktree 路径 | 分支 | 新会话在此目录开工 |
| --- | --- | --- | --- |
| 单位管理 | `.worktrees/unit-mgmt/` | `feat/unit-mgmt` | ✅ |
| 颜色管理 | `.worktrees/color-mgmt/` | `feat/color-mgmt` | ✅ |
| 客户管理 | `.worktrees/customer-mgmt/` | `feat/customer-mgmt` | ✅ |
| 主仓库 | `.`（项目根） | `main` | 合并时才用，**不要在 main 直接开发** |

> 单位/颜色 worktree 基于 `main@aa7e5fd` 创建（早期基线）；
> 客户管理基于更新后的 main 创建。`main` 是只读基线，只用于最终合并。

**新会话开工第一步**：在新 worktree 目录里确认分支正确
```bash
git branch --show-current   # 应为 feat/unit-mgmt / feat/color-mgmt / feat/customer-mgmt
```

---

## 2. 冲突面分析（为什么需要本约定）

### 2.1 零冲突文件（各写各的，git 不会打架）

| 层 | 你新增的文件（per-file） |
| --- | --- |
| 后端实体 | `OneCup.Domain/Entities/{Unit,Color}.cs` |
| 后端 DTO | `OneCup.Application/Dtos/System/{Unit,Color}Dtos.cs` |
| 后端服务/接口 | `OneCup.Application/Services/` + `Interfaces/` 各自文件 |
| 后端 EF 配置 | `OneCup.Infrastructure/Persistence/Configurations/{Unit,Color}Configuration.cs` |
| 后端控制器 | `OneCup.Api/Controllers/{Unit,Color}Controller.cs` |
| 前端 API | `frontend/src/api/{unit,color}.ts` |
| 前端页面 | `frontend/src/pages/system/{unit,color}/` 整个目录 |

### 2.2 高冲突文件（两边都要改，需协调）

| 文件 | 冲突性质 | 风险 |
| --- | --- | --- |
| `SeedData.cs` | 两边都加 Guid 常量 | 🔴 高（Guid 段撞车） |
| `OneCupDbContext.cs` | 两边都加 DbSet + Seed() | 🔴 高 |
| `OneCupDbContextModelSnapshot.cs` | EF 自动重写，每次加迁移必冲突 | 🔴 最高 |
| 新增的 `*_Add{Module}.cs` 迁移文件 | 时间戳撞名 | 🟡 中 |
| `frontend/src/routes.ts` | 同 routes 数组追加 | 🟡 中（git 多半能自动合并） |
| `frontend/src/router.tsx` | 追加 import + element | 🟡 中 |
| `frontend/src/locale/index.ts` | en-US + zh-CN 两处追加 | 🟡 中 |
| `Program.cs` | 追加 AddScoped | 🟢 低（纯追加） |
| `NumberTargetTypes.cs` | 见 3.1 注 | 🟢 低 |

---

## 3. 共享文件修改规则（开工即适用）

### 3.1 种子 Guid 分配（最关键，先定好不抢）

`SeedData.cs` 用确定性 Guid，**第 4 段是按区间分配的**。各分支已划定互不重叠的区间：

| 资源类别 | 单位管理（unit） | 颜色管理（color） | 客户管理（customer） | 已占用（不可碰） |
| --- | --- | --- | --- | --- |
| 权限 Guid（`...000000012X`） | **121–123** | 复用 `109/110` | 复用 `107/108` | 101–117 已用 |
| 目标类型 Guid（`...000000022X`） | **211** | 复用 `205` | 复用 `204` | 201–206 已用 |

**具体分配：**

```csharp
// === 单位管理独占（唯一需要新增 Guid 的模块）===
public static readonly Guid PermUnitRead    = Guid.Parse("00000000-0000-0000-0000-000000000121");
public static readonly Guid PermUnitWrite   = Guid.Parse("00000000-0000-0000-0000-000000000122");
public static readonly Guid TargetTypeUnit  = Guid.Parse("00000000-0000-0000-0000-000000000211");

// === 颜色管理：复用已有，无需新增 Guid ===
//   PermColorRead=...109, PermColorWrite=...110, TargetTypeColor=...205  已存在

// === 客户管理：复用已有，无需新增 Guid ===
//   PermCustomerRead=...107, PermCustomerWrite=...108, TargetTypeCustomer=...204  已存在
```

> **为什么单位从 121 起、留 118–120 空？** 给 main 上可能的其他小改动留缓冲，
> 避免开发中途有人往 118–120 塞东西导致撞车。**118–120 现阶段各分支都不许用。**

> **颜色管理注意**：`NumberTargetTypes.cs` 已含 `Color = "color"` 常量，且 color 的
> 权限种子和目标类型种子**已存在**。颜色管理**不需要新增权限 Guid、不需要新增目标类型**，
> 直接复用。如果你的设计需要"颜色写权限"且现有 `PermColorWrite=110` 不够用，
> 用 **131–133** 段（同样留缓冲），**不要用 121–123（单位已占）**。

> **客户管理注意**：客户是三个模块里种子基础最完备的——权限 `customer:read/write`
>（107/108）、编号目标类型 `customer`（204）、`NumberTargetTypes.Customer` 常量
> **全部已在 main 基线**。客户管理**不新增任何 Guid**，是唯一完全不碰 `SeedData.cs`
> 的模块。若客户编号规则需要，**不要种子化**（全系统编号规则均为运行时配置，
> 见 FAQ Q4）；客户实体本身需要新增 per-file 代码 + 一份 EF 迁移。

### 3.2 EF 迁移文件命名（防撞名）

各分支加迁移时，**时间戳必须不同**。EF 的 `migrations add` 会自动生成时间戳，
但为确保不撞，约定：

- **单位管理**迁移名：`AddUnitModule`（让 EF 自动加时间戳前缀）
- **颜色管理**迁移名：`AddColorModule`
- **客户管理**迁移名：`AddCustomerModule`

```bash
# 单位管理 worktree 内
dotnet ef migrations add AddUnitModule --project src/OneCup.Infrastructure --startup-project src/OneCup.Api

# 颜色管理 worktree 内
dotnet ef migrations add AddColorModule --project src/OneCup.Infrastructure --startup-project src/OneCup.Api

# 客户管理 worktree 内
dotnet ef migrations add AddCustomerModule --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```

EF 时间戳精确到秒，各分支不太可能同一秒执行，但**若真的撞了**，手动改一个文件名的时间戳
后缀（改大），确保各份迁移在合并后能按时间线排列。

### 3.3 `OneCupDbContext.cs` 改法

两边都要：
1. 在 `DbContext` 里加 `public DbSet<Unit> Units => Set<Unit>();`（颜色同理）。
2. 在 `OnModelCreating` 末尾追加 `modelBuilder.ApplyConfiguration(new UnitConfiguration());`。
3. 在 `Seed()` 方法里追加 `HasData(...)` 种子。

**约定**：各自在文件**末尾**追加自己的代码块，用注释标注模块名：
```csharp
// ===== Unit 模块 =====
public DbSet<Unit> Units => Set<Unit>();
// ... Seed() 里：
.HasData(new { Id = SeedData.PermUnitRead, ... })
```
合并时冲突会落在这些相邻区块，人工选择保留两边即可。

### 3.4 前端三个集中文件改法

- `routes.ts`：在 `routes[0].children`（即 `menu.system` 的 children）数组**末尾**追加你的菜单项。
- `router.tsx`：追加 lazy import 和路由 element。
- `locale/index.ts`：**en-US 和 zh-CN 两个对象都要加** `menu.system.{unit,color}` 文案。

约定各自在数组/对象末尾追加，合并时大概率 git 自动 merge，小概率手动保留两边。

### 3.5 `Program.cs` 改法

末尾追加 `builder.Services.AddScoped<IUnitService, UnitService>();`，纯追加，无冲突风险。

---

## 4. 合并策略：方案 B（开发期并行，收尾统一处理）

**核心思路**：开发期各 worktree 互不通信、互不 rebase，各自独立做完。合并时，
**按顺序逐个合入 main，后合者解决与已合分支的冲突**。

### 4.1 合并顺序

**建议顺序：客户管理 → 颜色管理 → 单位管理**（后合者承担冲突解决）。

理由：
- 客户、颜色都**完全复用 main 基线种子**（不新增 Guid），改动面小、冲突简单，先合；
- 单位是**唯一新增 Guid 的模块**，放最后合，这样它合并时面对的是"客户+颜色已合入的 main"，
  一次性把 EF 快照对齐到三方终态，减少中间态冲突次数。
- 但这**不是强制的**——谁先开发完谁先合，顺序可调整，只要遵循"逐个合、后合者解冲突"。

### 4.2 第一个分支合并（以 customer 为例）

```bash
cd "C:/Users/mi/Desktop/work_space/one-cup"   # 回主仓库
git checkout main
git merge --no-ff feat/customer-mgmt
# customer 是第一个合入的，无冲突源，直接合入
git push origin main   # 如果有远端
```

### 4.3 后续分支合并（color、unit 依次合）

```bash
cd "C:/Users/mi/Desktop/work_space/one-cup/.worktrees/color-mgmt"
git fetch ../..  main:main-local   # 拿到合了 customer 之后的 main
git merge main-local                # 此时会冲突，重点在以下几个文件
```

**预期冲突文件与解决方式（每合一个分支都会出现一次）：**

| 文件 | 解决方式 |
| --- | --- |
| `SeedData.cs` | 各分支 Guid 段不重叠（按 3.1 分配），保留各分支的常量块即可 |
| `OneCupDbContext.cs` | DbSet / ApplyConfiguration / Seed() 各分支有独立区块，全部保留 |
| `OneCupDbContextModelSnapshot.cs` | 🔴 **必冲突**。见 4.4 专门处理 |
| `{timestamp}_Add{Module}Module.cs` 各迁移文件 | 时间戳不同则不冲突；撞名则改一个时间戳 |
| `routes.ts` / `router.tsx` / `locale/index.ts` | 数组/对象追加，保留各分支 |

### 4.4 ModelSnapshot 冲突处理（最关键）

EF 的 `ModelSnapshot` 是自动生成的全量快照，**两个分支的版本不能简单手动拼接**。
正确做法：

```bash
# 解决完其他所有冲突后，ModelSnapshot 冲突先"接受当前分支版本"占位
git checkout --ours src/OneCup.Infrastructure/Migrations/OneCupDbContextModelSnapshot.cs

# 然后重新生成快照（基于当前合并后的模型）
dotnet ef migrations add MergeUnitAndColor --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
# 这会基于"unit + color 合体后的 DbContext"生成一个新的空迁移 + 正确的全量快照

# 检查新生成的迁移文件，如果 Up/Down 是空的（说明模型已一致），删掉这个临时迁移文件，
# 只保留它刷新出的 ModelSnapshot.cs：
rm src/OneCup.Infrastructure/Migrations/*_MergeUnitAndColor.cs
rm src/OneCup.Infrastructure/Migrations/*_MergeUnitAndColor.Designer.cs
# ModelSnapshot.cs 已被上一步刷新，保留它
```

> **原理**：`migrations add` 会用当前 DbContext 模型重新生成 `ModelSnapshot.cs`，
> 这个快照才是"main 的最终真相"。临时迁移只为触发快照刷新，模型若已一致其 Up/Down 为空，
> 删掉即可。**务必跑 `dotnet ef database update` 验证能正确应用。**

### 4.5 合并后验证

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
git worktree remove .worktrees/unit-mgmt
git worktree remove .worktrees/color-mgmt
git branch -d feat/unit-mgmt feat/color-mgmt   # 已合并的分支可安全删除
```

---

## 5. FAQ

**Q1：我开工时要不要先 rebase main？**
不要。两个 worktree 都基于同一基线 `87bf3a9`，开发期 main 也不会动（约定不在 main 直接开发）。
合并阶段（第 4 节）才处理同步。

**Q2：我发现需要新增一个本约定没提到的共享文件改动（比如新枚举、新常量）。**
停下来，先在 issue 或对话里提出，**不要单方面新增可能被对方也需要的共享改动**。

**Q3：EF 迁移加完发现模型和快照对不上 / `dotnet ef migrations add` 报错。**
确保你只在自己的 worktree 里改了自己的实体 + Configuration，且 `DbContext` 里
`ApplyConfiguration` 已加。EF 会基于"DbContext 当前认识的全部模型"生成快照。

**Q4：颜色管理说"复用已有种子"，但我需要"颜色分类"这种新字典？**
颜色相关的"目标类型"和"读写权限"已存在，可直接用。若你需要全新的字典概念
（如"颜色分类"），按编号管理的 `numbering_target_types` 字典模式新增独立表，
**不要复用既有 Guid**。

**Q5：我在哪个 worktree？**
```bash
git rev-parse --show-toplevel
# 输出 .../one-cup/.worktrees/unit-mgmt   → 你在单位管理
# 输出 .../one-cup/.worktrees/color-mgmt  → 你在颜色管理
# 输出 .../one-cup                         → 你在主仓库（别在这开发）
```

---

## 6. 资源分配总表（一图速查）

```
种子 Guid 第 4 段分配：
  101–117  已用（main 基线，勿动）
  118–120  缓冲，两边都不许用
  121–123  单位管理（PermUnitRead=121 / PermUnitWrite=122）
  124–130  缓冲
  131–133  颜色管理额外需求备用（color 基础权限 109/110 已存在）
  107/108  客户管理复用（PermCustomerRead/Write，main 已存在）
  201–206  已用（目标类型，main 基线，勿动）
  207–210  缓冲
  211      单位管理（TargetTypeUnit）
  204      客户管理复用（TargetTypeCustomer，main 已存在）
  212+     颜色/客户管理额外目标类型备用

EF 迁移命名：
  单位：AddUnitModule    颜色：AddColorModule    客户：AddCustomerModule

合并顺序建议：
  客户（先）→ 颜色（中）→ 单位（后，唯一新增 Guid 的模块）
```
