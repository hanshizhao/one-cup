# OneCup 颜色管理模块设计文档

> 印染厂面料开发管理系统 — 业务主数据：颜色字典
> 创建日期: 2026-07-04
> 状态: 待实现
> 分支: `feat/color-mgmt`（worktree `.worktrees/color-mgmt`）
> 关联: [并行开发协作约定](../../parallel-dev-contract.md)、[编号字典设计](../../specs/2026-07-03-numbering-dictionary-design.md)（同构参考）

---

## 1. 背景与目标

### 1.1 定位

颜色管理是一个**业务主数据字典**模块。颜色是面料/产品/物料等业务模块会引用的
**共享基础数据**（通过稳定的 `code` 标识符关联），类似编号字典里的业务类型字典。

权限种子名"颜色对色"是历史遗留标签；本模块实际范围是**颜色主数据 CRUD**
（不含对色单业务流程，那属于未来独立设计）。

### 1.2 目标

> 提供颜色主数据的可配置字典：编码/名称/颜色值/颜色系/启停，供当前与未来业务模块引用。

- 管理员可在前端纯配置新增颜色（如 `RED001 / 大红 / #FF0000 / 红`），全程不改代码。
- 颜色码（code）创建后不可改，作为稳定引用标识符。
- 停用的颜色保留记录（不物理删除），便于存量数据追溯。
- 颜色值（hex）作为本模块相对编号字典的核心增量价值：前端色块可视化预览。

### 1.3 不在范围内

- 对色单/对色记录管理（未来独立模块）
- Pantone / Lab / RGB 数值等额外色彩空间（将来可加列）
- 独立的"颜色系字典表"（本轮用自由文本字段，见 2.3 决策①）
- 颜色的批量导入/导出
- 物理删除

### 1.4 成功标准

- 管理员可在"基础设置 > 颜色"页面纯配置新增颜色，含色块预览。
- code 重复时后端返回友好错误（映射 400）；hex 格式非法时同样拦截。
- 复用既有权限 `color:read`(109) / `color:write`(110)，**不新增任何 Guid**。

---

## 2. 数据模型

### 2.1 表：`colors`

遵循现有 `BaseEntity`（`id` / `created_at` / `updated_at`）+ 主数据惯例
（code 不可改、只启停不物理删、不建物理外键），与编号字典同构。

| 列 | 类型 | 约束 | 说明 |
|----|------|------|------|
| `id` | uuid | PK | 主键 |
| `code` | varchar(32) | NOT NULL, **UNIQUE** | 编码如 `RED001`。创建后不可改，供面料/产品等模块稳定引用 |
| `name_zh` | varchar(64) | NOT NULL | 中文名，如"大红" |
| `name_en` | varchar(64) | NOT NULL | 英文名，如"Red" |
| `hex` | char(7) | NOT NULL | 颜色值如 `#FF0000`，前端色块预览 + 校验格式 |
| `color_family` | varchar(32) | NOT NULL | 颜色系如"红"。自由文本（前端下拉写死常用项 + allowCreate） |
| `remark` | varchar(256) | NULL | 备注 |
| `sort_order` | int | NOT NULL DEFAULT 0 | 排序号 |
| `is_active` | boolean | NOT NULL DEFAULT true | 启停状态 |
| `created_at` | timestamptz | NOT NULL | 审计 |
| `updated_at` | timestamptz | NOT NULL | 审计 |

唯一索引：`ux_colors_code` ON `(code)`。

### 2.2 实体

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

### 2.3 关键设计决策

**① `color_family` 用自由文本字段，不建独立色族字典表。**

印染颜色系相对固定（红/橙/黄/绿/蓝/紫/中性/黑白灰），前端下拉写死常用项 +
`allowCreate` 允许输入新值即可。做成独立表会多一张表 + CRUD UI，对 MVP 过重（YAGNI）。
将来若需"可配置色族"，可平滑升级为独立表——届时 `color_family` 字段值原样迁移。

**② `hex` 必填。**

色块预览是颜色管理相对编号字典的核心增量价值，hex 留空则退化为纯文字字典。
存 `#RRGGBB`（char(7)），RGB 可由前端从 hex 推导无需单存。暂不收 Pantone/Lab
（YAGNI，将来可加列）。

**③ `code` 全局唯一、创建后不可改。**

与编号字典的 `NumberingTargetType.Code` 完全一致的语义——它是其他模块引用颜色的
稳定标识符。唯一性校验**不含 IsActive 过滤**（停用也占 code）。

**④ 不建物理外键、只启停不物理删除。**

与现有编号三表 + 字典表保持一致。停用后记录保留，存量引用（存的 code 字符串）
仍可追溯，不依赖颜色记录存在。

---

## 3. 后端 API 与服务

### 3.1 架构层级

复用编号字典的完整范式：
- `OneCup.Domain/Entities/Color.cs` — 实体
- `OneCup.Infrastructure/Persistence/Configurations/ColorConfiguration.cs` — EF 配置
- `OneCup.Application/Dtos/System/ColorDtos.cs` — DTO
- `OneCup.Application/Specifications/ColorSpecs.cs` — 查询规格
- `OneCup.Application/Interfaces/IColorService.cs` — 服务接口
- `OneCup.Application/Services/ColorService.cs` — 服务实现（`IRepository<T>` + Specification + `IUnitOfWork`）
- `OneCup.Api/Controllers/ColorController.cs` — 控制器

### 3.2 权限映射（关键）

种子里 `color:read`/`color:write` 已存在（`PermColorRead=...109`/`PermColorWrite=...110`），
但 `Program.cs` **从未声明对应策略**（现有策略全是 `system:*`）。颜色是第一个业务模块，
本模块要**新增两条策略**（对 `Program.cs` 的追加，契约 §3.5 低风险纯追加）：

| 策略名 | claim 要求 | 对应种子权限 |
|--------|-----------|--------------|
| `color-view` | `perm_codes` 含 `color:read` | `PermColorRead=...109`（已存在，复用） |
| `color-manage` | `perm_codes` 含 `color:write` | `PermColorWrite=...110`（已存在，复用） |

**不新增任何 Guid**（契约 §3.1：颜色基础权限已存在，无需新增）。

### 3.3 API 端点

新增独立控制器 `ColorController`，路由 `api/colors`。

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/api/colors` | `color-view` | 分页（keyword / colorFamily / isActive 筛选） |
| `GET` | `/api/colors/all` | `color-view` | 全量启用项（前端下拉用，按 SortOrder 升序） |
| `GET` | `/api/colors/{id}` | `color-view` | 详情 |
| `POST` | `/api/colors` | `color-manage` | 新增（code 锁定字段全可填） |
| `PUT` | `/api/colors/{id}` | `color-manage` | 编辑（code 不暴露，其余可改） |
| `PUT` | `/api/colors/{id}/status` | `color-manage` | 启停切换 |

### 3.4 DTO

```csharp
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

### 3.5 服务行为

**沿用编号字典修复过的语义（避免重复踩坑）：**

- 列表用 `ColorFilterSpec`（无分页）统计 total + `ColorPagedSpec`（有分页）取当前页，
  确保 total 统计不受分页污染（编号字典曾因此出 bug）。
- `code` 唯一性校验用 `ColorByCodeSpec`（**不含 IsActive 过滤**——停用也占 code）。
- 新增/编辑时 `hex` 格式校验（应用层正则 `^#[0-9A-Fa-f]{6}$`，非法 → `DomainException` 映射 400）。
- `code` 不可改：`UpdateColorRequest` 不暴露 `Code` 字段。
- 启停与编辑解耦：`/status` 独立接口。

**新增的规格类**（`ColorSpecs.cs`，与编号字典同构 + 多 colorFamily 维度）：

- `ColorFilterSpec(keyword, colorFamily, isActive)` — 仅过滤，无分页，用于 CountAsync
- `ColorPagedSpec(keyword, colorFamily, isActive, page, pageSize)` — 含过滤 + 排序 + 分页
- `ColorActiveSpec()` — 全部启用项（下拉用）
- `ColorByIdSpec(id)` — 详情
- `ColorByCodeSpec(code, excludingId?)` — code 唯一性校验

筛选 predicate（keyword 命中 code / name_zh / name_en 三列）：
```csharp
ApplyCriteria(c =>
    (kw == null || c.Code.Contains(kw) || c.NameZh.Contains(kw) || c.NameEn.Contains(kw)) &&
    (string.IsNullOrEmpty(colorFamily) || c.ColorFamily == colorFamily) &&
    (isActive == null || c.IsActive == isActive.Value));
```

---

## 4. 前端设计

### 4.1 导航结构

新增顶级菜单 **「基础设置」**（`menu.masterData`），颜色作为其第一个子项。

```
系统管理 (menu.system)
  ├ 用户管理 / 角色管理 / 权限列表 / 编号管理 / 操作日志 / 登录日志
基础设置 (menu.masterData)   ← 新增顶级
  └ 颜色 (menu.masterData.color)  ← 本模块
```

命名用 `masterData` 而非 `settings`（避免与 Arco Pro 已有的 `settings.*` 主题切换
locale key 撞车）。`layout.tsx` 的 `useRoute` 递归渲染菜单数组，加第二个顶级项即
自动生效，**无需改 layout**。

> 这个分类契合主数据语义：颜色是跨业务模块的共享基础数据。`feat/unit-mgmt`
> （计量单位）也是主数据，将来天然归入「基础设置」。

### 4.2 页面布局

按 AGENTS.md 导航规范：颜色是单一功能模块，**侧边栏一个菜单项**，页面内不拆 Tab。
整页就是一个查询表格（无主从联动，因为颜色是单表主数据）。

严格套用 `docs/specs/templates/query-table-page.template.tsx`
（单个 Card + Form/Grid 三列查询 + 表格外侧兄弟按钮 + 工具栏 space-between）：

```
┌──────────────────────────────────────────────────┐
│ 颜色                                              │
├──────────────────────────────────────────────────┤
│ [关键字]  [颜色系▾]  [状态▾]    [查询] | [重置]   │  ← Form+Grid三列，按钮外侧
├──────────────────────────────────────────────────┤
│ [新建颜色]                          共 N 条       │  ← 工具栏 space-between
├──────────────────────────────────────────────────┤
│ 色块  编码   中文名  英文名  颜色系  排序 状态 操作 │
│ ▮    RED001 大红   Red     红      1   启用 编辑  │
│ ▮    BLU001 海蓝   Blue    蓝      2   停用 启用  │
└──────────────────────────────────────────────────┘
```

### 4.3 查询区字段

- **关键字**（Input allowClear）— 命中 code/中文名/英文名
- **颜色系**（Select allowClear + 写死常用项 + allowCreate）— 红/橙/黄/绿/蓝/紫/中性/黑白灰
- **状态**（Select：启用/停用）

查询仅按钮触发（`getFieldsValue`），字段 onChange 不自动查询（AGENTS.md 反模式禁止）。

### 4.4 表格列

| 列 | dataIndex | 说明 |
|----|-----------|------|
| 色块 | hex | 渲染圆点/方块 div（`background: hex`） |
| 编码 | code | |
| 中文名 | nameZh | |
| 英文名 | nameEn | |
| 颜色系 | colorFamily | |
| 排序 | sortOrder | |
| 状态 | isActive | Tag（启用=green） |
| 操作 | — | 编辑 / 启停 Popconfirm |

### 4.5 新建/编辑表单（Drawer）

与编号字典一致的交互范式。字段：
- code（创建时可填，编辑时锁定显示 + 锁定提示 Alert）
- 中文名 / 英文名（必填）
- hex（Input + 实时色块预览；可选用 Arco `ColorPicker` 若可用，否则 Input + 正则校验）
- 颜色系（Select 写死常用项 + allowCreate）
- 排序（InputNumber min=0）
- 备注（TextArea，可选）

### 4.6 国际化

按编号字典页的 locale 三件套：`locale/{index,zh-CN,en-US}.ts`，key 前缀 `color.*`。

### 4.7 权限码转换（已有机制，无需改）

`transformPermissions` 把 `color:read` 解析为 `{color:['read']}`，
`RequirePermission` 按 `resource="color" actions={['read']}` 校验，
前端菜单 `requiredPermissions` 同理。

---

## 5. 迁移、种子与并行开发合规

### 5.1 EF 迁移

本 worktree 内执行，命名 `AddColorModule`（契约 §3.2），让 EF 自动加时间戳前缀：

```bash
dotnet ef migrations add AddColorModule --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```

### 5.2 种子数据 — 严格遵守契约，不新增任何 Guid

- `SeedData.cs`：**完全不新增常量**。`PermColorRead=109`/`PermColorWrite=110`/
  `TargetTypeColor=205` 已在 main 基线，直接复用。
- 颜色字典**不种子业务数据**（与编号字典"分类由管理员按需添加"一致——每个工厂颜色
  体系不同，种子无意义）。迁移只建表，不 HasData。
- 因此 **`OneCupDbContext.Seed()` 无需改动**，只需加 `DbSet<Color>` +
  `ApplyConfiguration`（契约 §3.3 末尾追加，标注 `// ===== Color 模块 =====`）。

> 因不种子数据，合并期 `OneCupDbContext.cs` 的冲突面比单位模块更小——
> 只有 DbSet + ApplyConfiguration 两处追加。

### 5.3 共享文件改动清单（合规自检）

| 文件 | 改动 | 契约归类 |
|------|------|----------|
| `SeedData.cs` | ❌ 不改 | — |
| `OneCupDbContext.cs` | 末尾追加 DbSet + ApplyConfiguration | §3.3 高冲突，可控 |
| `*_AddColorModule.cs` 迁移 | 新增 | §3.2 中 |
| `OneCupDbContextModelSnapshot.cs` | EF 自动重写 | §2.2 最高（合并期按 §4.4 处理） |
| `Program.cs` | 末尾追加 2 条策略 + 1 条 AddScoped | §3.5 低 |
| `frontend/src/routes.ts` | 数组末尾追加 masterData 项 | §3.4 中 |
| `frontend/src/router.tsx` | 追加 import + element | §3.4 中 |
| `frontend/src/locale/index.ts` | en-US/zh-CN 追加 | §3.4 中 |

### 5.4 并行合规确认

- ✅ 只动 color 模块文件 + 末尾追加共享文件
- ✅ **零新增 Guid**（复用 109/110/205）
- ✅ 迁移名 `AddColorModule`
- ✅ 不碰 121-123（单位）、不碰 118-120/124-130/207-210（缓冲）
- ✅ 模型快照冲突开发期不管（方案 B），合并期按契约 §4.4 处理

---

## 6. 测试策略

遵循现有测试模式（单元测试 + Testcontainers 集成测试），分三层。

### 6.1 单元测试（`OneCup.UnitTests/Color/`）

**`ColorServiceTests.cs`：**
- ✅ 新增：合法输入 → 成功
- ❌ code 重复 → `DomainException`（含停用项占 code 的情况）
- ❌ hex 格式非法（如 `GGG`/`#XYZ`/`FF0000` 缺 #）→ `DomainException`
- ✅ 编辑：code 锁定不暴露，name/hex/color_family/remark/sortOrder 可改
- ✅ 启停切换
- ✅ 列表筛选：keyword（code/中/英名）、colorFamily、isActive 多条件组合

**`ColorSpecsTests.cs`：**
- FilterSpec（无分页）vs PagedSpec（有分页）的计数分离正确性
  （沿用编号字典修复过的语义 bug 模式，确保 total 统计不受分页污染）
- `ColorByCodeSpec` 含/不含 excludingId 的唯一性校验行为

### 6.2 集成测试

颜色是纯主数据 CRUD，无并发取号、无引擎交互，风险远低于编号字典的强校验。
`NumberingDictionary` 模块也没写 Testcontainers 集成测试（只在并发测试里附带验证
引擎校验）。因此颜色**不新增独立集成测试**——单元测试覆盖 Repository spec 行为
已足够，与项目现有测试边界一致。

### 6.3 不测试

前端（项目现有测试体系无前端单测覆盖业务页，保持一致）。

---

## 7. 涉及文件清单

### 后端新增
- `OneCup.Domain/Entities/Color.cs`
- `OneCup.Infrastructure/Persistence/Configurations/ColorConfiguration.cs`
- `OneCup.Infrastructure/Migrations/<timestamp>_AddColorModule.cs`
- `OneCup.Application/Dtos/System/ColorDtos.cs`
- `OneCup.Application/Specifications/ColorSpecs.cs`
- `OneCup.Application/Interfaces/IColorService.cs`
- `OneCup.Application/Services/ColorService.cs`
- `OneCup.Api/Controllers/ColorController.cs`

### 后端修改（共享文件，末尾追加）
- `OneCup.Infrastructure/Persistence/OneCupDbContext.cs`（新增 DbSet + ApplyConfiguration）
- `OneCup.Api/Program.cs`（新增 2 条策略 + 1 条 AddScoped）

### 前端新增
- `frontend/src/pages/master-data/color/index.tsx`（查询表格页）
- `frontend/src/pages/master-data/color/locale/{index,zh-CN,en-US}.ts`
- `frontend/src/pages/master-data/color/style/index.module.less`
- `frontend/src/api/color.ts`

### 前端修改（共享文件，末尾追加）
- `frontend/src/routes.ts`（新增 masterData 顶级菜单项 + color 子项）
- `frontend/src/router.tsx`（lazy import + 路由 element）
- `frontend/src/locale/index.ts`（en-US/zh-CN 各加 masterData 文案）

### 测试新增
- `backend/tests/OneCup.UnitTests/Color/ColorServiceTests.cs`
- `backend/tests/OneCup.UnitTests/Color/ColorSpecsTests.cs`

---

## 8. 实现顺序建议

1. 后端实体 + EF 配置 + DbContext 追加 + 迁移（先让表建起来）
2. DTO + Specs + Service 接口 + Service 实现
3. Controller + Program.cs 策略/DI
4. 后端单元测试（先红后绿）
5. 前端 API client + 查询表格页 + 抽屉表单 + locale
6. 前端路由/菜单/locale 追加
7. 全栈联调验证（构建 + 测试 + 迁移应用）
