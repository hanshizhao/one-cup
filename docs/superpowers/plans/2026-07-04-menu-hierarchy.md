# 侧边栏菜单层级重构 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 将前端侧边栏从「业务管理/系统管理/基础设置」三分组重构为面向终态的「业务资料/基础设置/系统管理」三域（生产流程域待 fabric 模块上线时加入），并把编号管理、计量单位从系统管理迁入基础设置。

**Architecture:** 菜单数据源是 `routes.ts` 的声明式树，渲染在 `layout.tsx`，实际路由表在 `router.tsx`，文案在 `locale/index.ts`。本次重构同步改这四个文件：迁移 `numbering`/`unit` 的 key 与路径、改 `business` 顶级文案、为 `business` 域补图标。权限资源命名（`system:numbering` 等）与后端 API 路径（`/api/numbering/*`）不变。

**Tech Stack:** React 18, Arco Design (`@arco-design/web-react`), React Router v6, TypeScript。

## Global Constraints

- **不改动权限模型**：`requiredPermissions` 里的 `resource`/`actions`（如 `system:numbering`、`system:unit`、`color`、`customer`）保持不变。菜单路径迁移 ≠ 权限资源迁移。
- **不动后端**：`/api/numbering/*`、`/api/unit/*` 等 API 路径不变。
- **不实现未来模块**：`production` 域、fabric/product 页面本次都不做，仅 locale 文案与现有项迁移。
- **方案 A**：`production` 顶级域本次**不放入** `routes` 数组（避免空组导致 `useRoute` 的 `defaultRoute` 失效），待 fabric 模块实现时再加入。
- **页面目录物理位置不动**：`pages/system/numbering/`、`pages/system/unit/` 目录保留原位（仅路由路径变，import 路径不变），物理迁移列为可选后续项。
- **不破坏现有功能**：每个 task 结束后菜单可正常渲染、路由可正常访问。

---

## File Structure

| 文件 | 本次职责 | 改动类型 |
|---|---|---|
| `frontend/src/routes.ts` | 菜单树：迁移 numbering/unit 到 master-data，调整顺序 | 修改 |
| `frontend/src/router.tsx` | 路由表：改 numbering/unit 的 path | 修改 |
| `frontend/src/layout.tsx` | `getIconFromKey` 为 `business` 配图标 | 修改 |
| `frontend/src/locale/index.ts` | 文案迁移：business 文案、numbering/unit 的 key | 修改 |

---

## Task 1: 迁移 locale 文案（业务资料 + numbering/unit key 迁移）

**Files:**
- Modify: `frontend/src/locale/index.ts`

**Interfaces:**
- Produces: 新的 locale key `menu.masterData.numbering`、`menu.masterData.unit`；`menu.business` 文案改为"业务资料"；删除旧 key `menu.system.numbering`、`menu.system.numbering.dict`、`menu.system.unit`。

**Rationale:** 先改文案层，这样后续改 routes.ts 时引用的 key 已经存在，避免中间态报错。这是整个重构的数据基础。

- [ ] **Step 1: 修改 en-US 区块**

将 `frontend/src/locale/index.ts` 的 `'en-US'` 对象内，替换以下三行：

原内容（第 3、9-10、15 行）：
```ts
    'menu.business': 'Business',
```
```ts
    'menu.system.numbering': 'Numbering',
    'menu.system.numbering.dict': 'Dictionary',
```
```ts
    'menu.system.unit': 'Units',
```

改为：
```ts
    'menu.business': 'Master Data',
```
```ts
    'menu.masterData.numbering': 'Numbering',
```
```ts
    'menu.masterData.unit': 'Units',
```

（注意：删除了 `menu.system.numbering.dict` 这一行——经核查该 key 无消费方，dict 页面用的是本地 `numbering.dict.*` key 而非 `menu.*` key。）

- [ ] **Step 2: 修改 zh-CN 区块**

将 `'zh-CN'` 对象内，替换对应行。

原内容（第 23、29-30、35 行）：
```ts
    'menu.business': '业务管理',
```
```ts
    'menu.system.numbering': '编号管理',
    'menu.system.numbering.dict': '业务字典',
```
```ts
    'menu.system.unit': '计量单位',
```

改为：
```ts
    'menu.business': '业务资料',
```
```ts
    'menu.masterData.numbering': '编号管理',
```
```ts
    'menu.masterData.unit': '计量单位',
```

- [ ] **Step 3: 验证 locale 文件无残留旧 key**

Run: `grep -n "menu.system.numbering\|menu.system.unit" frontend/src/locale/index.ts`
Expected: 无输出（两个旧 key 均已删除）。

Run: `grep -n "menu.masterData.numbering\|menu.masterData.unit\|menu.business" frontend/src/locale/index.ts`
Expected: 能看到新 key `menu.masterData.numbering`、`menu.masterData.unit`，以及改过文案的 `menu.business`（en-US 为 'Master Data'，zh-CN 为 '业务资料'）。

- [ ] **Step 4: 类型检查**

Run: `cd frontend && npx tsc --noEmit`
Expected: 无错误（locale 文件改动不影响类型）。

- [ ] **Step 5: Commit**

```bash
git add frontend/src/locale/index.ts
git commit -m "refactor(menu): 迁移 locale key 业务资料/编号/单位

- menu.business 文案 业务管理→业务资料 (en: Master Data)
- menu.system.numbering → menu.masterData.numbering
- menu.system.unit → menu.masterData.unit
- 删除未使用的 menu.system.numbering.dict"
```

---

## Task 2: 迁移 routes.ts（菜单树重组）

**Files:**
- Modify: `frontend/src/routes.ts`

**Interfaces:**
- Consumes: Task 1 产出的新 locale key（`menu.masterData.numbering`、`menu.masterData.unit`）。
- Produces: 菜单树顶级顺序为 `business` → `master-data` → `system`；`numbering`/`unit` 移入 `master-data` 的 children，key 改为 `master-data/numbering`、`master-data/unit`；`system` 仅剩 user/role/permission/operationLog/loginLog 五项。

**Rationale:** 这是菜单结构的核心改动。注意 `production` 域按方案 A **不加入**数组——避免 `useRoute` 的 `defaultRoute`（routes.ts:133-140 取 `permissionRoute[0].children[0].key`）因空组失效。

- [ ] **Step 1: 替换整个 routes 数组**

将 `frontend/src/routes.ts` 第 16-95 行的 `export const routes: IRoute[] = [...]` 整体替换为：

```ts
export const routes: IRoute[] = [
  {
    name: 'menu.business',
    key: 'business',
    children: [
      {
        name: 'menu.business.customer',
        key: 'business/customer',
        requiredPermissions: [
          { resource: 'customer', actions: ['read'] },
        ],
      },
    ],
  },
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
      {
        name: 'menu.masterData.numbering',
        key: 'master-data/numbering',
        requiredPermissions: [
          { resource: 'system:numbering', actions: ['view'] },
        ],
      },
      {
        name: 'menu.masterData.unit',
        key: 'master-data/unit',
        requiredPermissions: [
          { resource: 'system:unit', actions: ['view'] },
        ],
      },
    ],
  },
  {
    name: 'menu.system',
    key: 'system',
    children: [
      {
        name: 'menu.system.user',
        key: 'system/user',
        requiredPermissions: [
          { resource: 'system:user', actions: ['manage'] },
        ],
      },
      {
        name: 'menu.system.role',
        key: 'system/role',
        requiredPermissions: [
          { resource: 'system:role', actions: ['manage'] },
        ],
      },
      {
        name: 'menu.system.permission',
        key: 'system/permission',
      },
      {
        name: 'menu.system.operationLog',
        key: 'system/operation-log',
        requiredPermissions: [
          { resource: 'system:audit', actions: ['view'] },
        ],
      },
      {
        name: 'menu.system.loginLog',
        key: 'system/login-log',
        requiredPermissions: [
          { resource: 'system:audit', actions: ['view'] },
        ],
      },
    ],
  },
];
```

关键变化说明（供 implementer 核对）：
- `business` 仍在第一位（其 key 不变，仅文案在 Task 1 改了）。
- `master-data` 升到第二位，children 含 color + numbering + unit（numbering/unit 从 system 迁来，key 改为 `master-data/*`）。
- `system` 降到第三位，children 仅剩 user/role/permission/operationLog/loginLog 五项。
- `production` 域**不出现**（方案 A）。
- `requiredPermissions` 的 resource（`system:numbering`、`system:unit`）**保持不变**——这是权限资源，与菜单路径解耦。

- [ ] **Step 2: 类型检查**

Run: `cd frontend && npx tsc --noEmit`
Expected: 无错误。

- [ ] **Step 3: Commit**

```bash
git add frontend/src/routes.ts
git commit -m "refactor(menu): routes 菜单树重组为三域

- 顶级顺序 业务资料→基础设置→系统管理
- numbering/unit 从 system 迁入 master-data (key 改 master-data/*)
- system 仅剩 user/role/permission/operationLog/loginLog
- production 域按方案 A 暂不入数组(待 fabric 上线)"
```

---

## Task 3: 迁移 router.tsx 路由路径

**Files:**
- Modify: `frontend/src/router.tsx`

**Interfaces:**
- Consumes: Task 2 的菜单 key（`master-data/numbering`、`master-data/unit`）——router.tsx 的 path 必须与 routes.ts 的 key 一致（layout.tsx:96 用 `/${key}` 合成路径）。
- Produces: `/master-data/numbering`、`/master-data/unit` 两个新路由；删除 `/system/numbering`、`/system/unit` 旧路由。

**Rationale:** routes.ts 的菜单 key 与 router.tsx 的 path 必须严格对应，否则点击菜单项跳转会 404。import 路径（`@/pages/system/numbering`）不变——页面目录物理位置不动。

- [ ] **Step 1: 迁移 numbering 路由**

在 `frontend/src/router.tsx` 中，找到 numbering 路由块（第 121-128 行）：

```tsx
      {
        path: 'system/numbering',
        element: withSuspense(
          <RequirePermission resource="system:numbering" actions={['view']}>
            <NumberingPage />
          </RequirePermission>
        ),
      },
```

改为（仅 path 变，`resource` 不变）：

```tsx
      {
        path: 'master-data/numbering',
        element: withSuspense(
          <RequirePermission resource="system:numbering" actions={['view']}>
            <NumberingPage />
          </RequirePermission>
        ),
      },
```

- [ ] **Step 2: 迁移 unit 路由**

找到 unit 路由块（第 153-160 行）：

```tsx
      {
        path: 'system/unit',
        element: withSuspense(
          <RequirePermission resource="system:unit" actions={['view']}>
            <UnitPage />
          </RequirePermission>
        ),
      },
```

改为：

```tsx
      {
        path: 'master-data/unit',
        element: withSuspense(
          <RequirePermission resource="system:unit" actions={['view']}>
            <UnitPage />
          </RequirePermission>
        ),
      },
```

- [ ] **Step 3: 确认索引路由 fallback 仍有效**

索引路由（第 95 行）`{ index: true, element: <Navigate to="/system/user" replace /> }` 指向 `/system/user`——该路径本次未动，fallback 仍有效。**无需改动**。

（注：`useRoute` 的 `defaultRoute` 现取 `business/customer`，也是有效的。两套 fallback 都指向存在路由，安全。）

- [ ] **Step 4: 类型检查**

Run: `cd frontend && npx tsc --noEmit`
Expected: 无错误。

- [ ] **Step 5: Commit**

```bash
git add frontend/src/router.tsx
git commit -m "refactor(menu): 迁移 numbering/unit 路由路径到 master-data

- /system/numbering → /master-data/numbering
- /system/unit → /master-data/unit
- 权限 resource 不变(system:numbering/system:unit)"
```

---

## Task 4: 为 business 域补菜单图标

**Files:**
- Modify: `frontend/src/layout.tsx`

**Interfaces:**
- Consumes: 无新依赖，使用已有的 `@arco-design/web-react/icon`。
- Produces: `getIconFromKey('business')` 返回 `IconBook`，不再返回空占位 div。

**Rationale:** 当前 `getIconFromKey`（layout.tsx:25-34）只处理 `system`/`master-data`，`business` 走 default 分支返回空占位，导致「业务资料」顶级菜单无图标。补上图标。

- [ ] **Step 1: 添加 IconBook import**

在 `frontend/src/layout.tsx` 第 5-10 行的 import 块中，将：

```tsx
import {
  IconSettings,
  IconStorage,
  IconMenuFold,
  IconMenuUnfold,
} from '@arco-design/web-react/icon';
```

改为：

```tsx
import {
  IconSettings,
  IconStorage,
  IconBook,
  IconMenuFold,
  IconMenuUnfold,
} from '@arco-design/web-react/icon';
```

- [ ] **Step 2: 在 getIconFromKey 中添加 business 分支**

将 `getIconFromKey` 函数（layout.tsx:25-34）：

```tsx
function getIconFromKey(key: string) {
  switch (key) {
    case 'system':
      return <IconSettings className={styles.icon} />;
    case 'master-data':
      return <IconStorage className={styles.icon} />;
    default:
      return <div className={styles['icon-empty']} />;
  }
}
```

改为：

```tsx
function getIconFromKey(key: string) {
  switch (key) {
    case 'system':
      return <IconSettings className={styles.icon} />;
    case 'master-data':
      return <IconStorage className={styles.icon} />;
    case 'business':
      return <IconBook className={styles.icon} />;
    default:
      return <div className={styles['icon-empty']} />;
  }
}
```

- [ ] **Step 3: 类型检查**

Run: `cd frontend && npx tsc --noEmit`
Expected: 无错误（`IconBook` 是 `@arco-design/web-react/icon` 的合法导出）。

- [ ] **Step 4: Commit**

```bash
git add frontend/src/layout.tsx
git commit -m "feat(menu): 业务资料顶级菜单补 IconBook 图标"
```

---

## Task 5: 整体验证

**Files:**
- 无文件改动，纯验证 task。

**Interfaces:**
- Consumes: Task 1-4 的全部产出。

**Rationale:** 菜单重构涉及四个文件的联动，必须在浏览器实际验证：菜单结构、跳转、默认路由、图标、中英文文案。类型检查通过 ≠ 运行时正确。

- [ ] **Step 1: 启动前端 dev server**

Run: `cd frontend && npm run dev`
Expected: Vite 启动无报错。

- [ ] **Step 2: 逐项核对验收标准**

打开浏览器登录后，逐项核对（对照 spec 第 7 节）：

1. **菜单三域顺序**：业务资料 → 基础设置 → 系统管理（production 域本次不出现）。
2. **基础设置含三项**：颜色管理、编号管理、计量单位。
3. **系统管理含五项**：用户管理、角色管理、权限列表、操作日志、登录日志（不再有编号管理、计量单位）。
4. **业务资料图标**：顶级菜单显示 IconBook 图标（不再是空占位）。
5. **默认路由**：登录后正常进入页面（不会白屏/空跳转）。
6. **编号管理跳转**：点击菜单 → URL 变为 `/master-data/numbering` → 页面正常加载。
7. **计量单位跳转**：点击菜单 → URL 变为 `/master-data/unit` → 页面正常加载。
8. **旧路径不再可达（预期）**：直接访问 `/system/numbering` 不再渲染编号管理页（旧路径已删除；具体降级行为——空白或 Forbidden——非本次验收项，spec 未要求旧路径重定向）。
9. **中英文切换**：切到英文，"业务资料"显示为 "Master Data"；"编号管理"显示为 "Numbering"。

- [ ] **Step 3: 如有问题，回到对应 Task 修复**

若某项不通过，定位到具体 Task 修复后重新验证。全部通过后继续。

- [ ] **Step 4: 最终 Commit（如 Task 5 期间有 hotfix）**

若 Task 5 期间改了代码：
```bash
git add -A
git commit -m "fix(menu): 整体验证修正"
```

若无需改动，跳过此步。

---

## 完成标准

全部 Task 1-5 的 checkbox 勾选完毕，且：
- [ ] `tsc --noEmit` 通过
- [ ] 浏览器验收 9 项全部通过
- [ ] 无残留旧 key（`menu.system.numbering`、`menu.system.unit`、`menu.system.numbering.dict`）
- [ ] 提交历史清晰（每个 Task 一个 commit）
