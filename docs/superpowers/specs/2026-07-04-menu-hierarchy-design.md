# 侧边栏菜单层级设计

- 日期：2026-07-04
- 状态：待实现
- 范围：前端 `frontend/src/` 菜单结构（`routes.ts` / `router.tsx` / `layout.tsx` / `locale/index.ts`）
- 背景：技术栈设计文档（`docs/specs/2026-07-01-tech-stack-design.md §4`）给出的四层分组已过时，需重新设计一个"对模块增删免疫"的菜单结构。

---

## 1. 目标与非目标

### 目标
- 建立一个**面向终态**的菜单层级，未来新增/调整模块时无需改动结构本身。
- 修正当前菜单中存在的归属不一致（编号管理、计量单位误置于「系统管理」）。
- 落实 AGENTS.md「侧边栏保持扁平、模块内子视图用 Tabs」的规范。

### 非目标
- 不重新设计各模块**页面内部**的 Tabs 结构（如面料开发内工艺 Tabs 的具体呈现——待 fabric 模块设计时定）。
- 不改动权限模型（`requiredPermissions` / 资源命名）。
- 不实现未来模块（☆ 标记项）的页面，仅在菜单中预留位置。

---

## 2. 设计原则

1. **分组维度 = 模块在流程中的角色**。这是结构稳定性的来源：未来任何新模块，按"它在流程里扮演什么角色"即可定归属，无需重新设计结构。
   - 流程本身（核心作业）
   - 流程引用的资料（主数据）
   - 全局基础配置（基础数据）
   - 系统运行支撑
2. **顶级排序 = 主轴优先 + 使用频率递减**。系统定位是"以面料开发为主轴的流程系统"，故生产流程居首；系统管理退至末位。
3. **侧边栏保持扁平**。模块内部子视图（工艺子单据、编号管理的字典/日志）一律用页面内 Tabs，不进侧边栏。
4. **结构面向终态**。当前「生产流程」域为空（面料开发/产品管理未实现），但排序不为暂态调整；待 fabric 模块上线后自然填充。

---

## 3. 菜单结构树

```
▸ 生产流程                      [图标待定]
    面料开发 ☆                  (内含织造/流程/染色工艺 Tabs，呈现方式 TBD)
    产品管理 ☆                  (归档成品检索)
▸ 业务资料                      [图标待定]
    客户 ✓
    颜色库 ✓
    对色管理 ☆
    设备管理 ☆
    原料物料 ☆
    样品文档 ☆
▸ 基础设置                      [IconStorage]
    编号管理 ✓                  (内含 规则配置/业务字典/生成日志 Tabs)
    计量单位 ✓                  ← 从「系统管理」迁入
▸ 系统管理                      [IconSettings]
    用户管理 ✓
    角色管理 ✓
    权限列表 ✓
    操作日志 ✓
    登录日志 ✓
```

图例：✓ 已实现　☆ 未来模块（本次仅在菜单预留位置，不实现页面）

---

## 4. 相对现状的变更

### 4.1 结构变更

| 变更 | 原状态 | 新状态 | 理由 |
|---|---|---|---|
| 新增顶级「生产流程」 | 不存在 | 第一位 | 系统主轴，预留面料开发/产品管理 |
| 「业务管理」→「业务资料」 | 第一位 | 第二位 | 与"生产流程"对仗，语义更准（这些是资料，非流程） |
| 新增顶级「基础设置」 | 第三位（仅含颜色） | 第三位（编号+计量单位+颜色） | 原本就是基础数据层，收纳迁入项 |
| 「基础设置」颜色保留 | — | 不变 | 颜色属主数据，但已稳定在此，不强迁 |
| 「系统管理」瘦身 | 含 7 项 | 含 5 项 | 仅留纯系统支撑项 |

> 说明：「颜色库」目前实现为颜色字典，属主数据。理想归属应在「业务资料」，但它已稳定挂在「基础设置」且 url 为 `master-data/color`。本次**不强迁**，避免改动 color 模块的现有实现；待 color 模块后续演进（如分离"颜色字典"与"对色管理"）时再统一处理。

### 4.2 模块迁移

| 模块 | 原路径 | 新路径 | 原 locale key | 新 locale key |
|---|---|---|---|---|
| 编号管理 | `system/numbering` | `master-data/numbering` | `menu.system.numbering` | `menu.masterData.numbering` |
| 计量单位 | `system/unit` | `master-data/unit` | `menu.system.unit` | `menu.masterData.unit` |

**保留不动的项**（避免连带改动）：
- 颜色：`master-data/color` / `menu.masterData.color` 不变。
- 客户：`business/customer` / `menu.business.customer` —— 顶级 key `business` 沿用，仅改显示文案为"业务资料"。

### 4.3 顶级 locale key 决策

| 顶级域 | locale key | 显示文案（zh-CN） | 说明 |
|---|---|---|---|
| 生产流程 | `menu.production`（新增） | 生产流程 | 新增 key |
| 业务资料 | `menu.business`（沿用） | 业务资料（原"业务管理"） | **沿用 key，仅改文案**，避免改 customer 子项 |
| 基础设置 | `menu.masterData`（沿用） | 基础设置 | 沿用 |
| 系统管理 | `menu.system`（沿用） | 系统管理 | 沿用 |

> 关键决策：`menu.business` 这个 key **保留不变**，只改它对应的显示文案（业务管理 → 业务资料）。这样 `menu.business.customer` 子项无需改动，最小化迁移面。仅 `business` 顶级文案和新增的 `production` 文案需要在 locale 文件里调整。

---

## 5. 实现影响（改动文件清单）

### 5.1 `frontend/src/routes.ts`
- 新增顶级 `production` 域（第一位，children 暂为 `[]` 或含未来项占位）。
- 将 `numbering`、`unit` 从 `system` 的 children 移到 `master-data` 的 children。
- 顶级顺序：`production` → `business` → `master-data` → `system`。
- `numbering`、`unit` 的 `key` 从 `system/numbering`、`system/unit` 改为 `master-data/numbering`、`master-data/unit`。

> **⚠️ defaultRoute 副作用及决策**：`useRoute`（routes.ts:133-140）取 `permissionRoute[0].children[0].key` 作为默认路由。`production` 若放第一位且 children 为空，登录后默认跳转 `defaultRoute` 会得到空字符串，导致索引路由失效。
>
> **本次采用方案 A**：`production` **暂不放入 `routes` 数组**，待 fabric 模块实现时再加入。即"结构在 spec 里完整定义为目标态，但代码里数组第一项仍是 `business`"，避免空组导致默认路由失效。`menu.production` 这个 locale key 也暂不新增（待 production 域真正加入时再配）。方案 B（放入空组 + 改 fallback）本次不采用。

### 5.2 `frontend/src/router.tsx`
- `numbering`、`unit` 的路由路径从 `/system/numbering`、`/system/unit` 改为 `/master-data/numbering`、`/master-data/unit`。
- 索引路由 `/` 的重定向目标若指向 `/system/user` 可不变；但需配合 5.1 的 defaultRoute 方案检查。
- 新增 `/production/*` 路由占位（仅当采用方案 B 时）。

### 5.3 `frontend/src/layout.tsx`
- `getIconFromKey`（layout.tsx:25-34）需为 `production`、`business` 两个 key 配 Arco 图标：
  - `production` → 建议用 `IconFile`（工艺单/流程文档语义）
  - `business` → 建议用 `IconBook` 或 `IconStorage`（资料/词典语义）
- `import` 补充对应图标。

### 5.4 `frontend/src/locale/index.ts`
- **暂不新增** `menu.production`（采用 5.1 方案 A，production 域本次不入代码；待 fabric 模块实现时再配 key 与文案）。
- 修改 `menu.business` 文案：zh-CN "业务管理" → "业务资料"；en-US "Business" → "Master Data"（或保留 Business，视术语统一）。
- 修改 `menu.system.numbering` → 新增 `menu.masterData.numbering`，删除旧 key（或保留旧 key 做向后兼容，但本项目无外部消费者，直接删）。
- 修改 `menu.system.unit` → 新增 `menu.masterData.unit`，删除旧 key。
- 现有 `menu.masterData.color`、`menu.business.customer` 不变。

### 5.5 不需要改动的
- 权限资源命名（`system:numbering`、`system:unit`、`color`、`customer` 等）—— 权限与菜单路径解耦，本次不改权限。
- 各页面组件实现（`pages/system/numbering/`、`pages/system/unit/` 的目录可保留不动，仅路由路径变；或顺带把目录迁到 `pages/master-data/`，**可选**，非必须）。

---

## 6. 待定项（本次不决策，留给后续模块设计）

1. **工艺 Tabs 呈现**：面料开发内织造/流程/染色工艺是详情页 Tabs 还是列表页露出 —— 待 fabric 模块设计。
2. **颜色库归属**：是否将颜色从「基础设置」迁到「业务资料」—— 待 color 模块演进（分离颜色字典与对色管理）时定。
3. **页面目录迁移**：`pages/system/numbering/`、`pages/system/unit/` 是否物理迁移到 `pages/master-data/` —— 可选，不影响功能。

---

## 7. 验收标准

- [ ] 侧边栏本次呈现**三个**顶级域（采用方案 A，production 域待 fabric 实现时加入），顺序为 业务资料 / 基础设置 / 系统管理；目标态四域结构以本 spec 为准。
- [ ] 编号管理、计量单位出现在「基础设置」下，不再出现在「系统管理」下。
- [ ] 「系统管理」仅含 用户/角色/权限/操作日志/登录日志 五项。
- [ ] 顶级图标正确显示（「业务资料」不再是无图标的空占位；「生产流程」域本次不出现，其图标待 fabric 上线时配）。
- [ ] 登录后默认路由正常（第一项为 business，默认进 customer）。
- [ ] 中英文 locale 文案正确，无残留旧 key（`menu.system.numbering`、`menu.system.unit` 已删除）。
- [ ] `numbering`、`unit` 页面通过新路径 `/master-data/numbering`、`/master-data/unit` 可正常访问，权限校验不变。
