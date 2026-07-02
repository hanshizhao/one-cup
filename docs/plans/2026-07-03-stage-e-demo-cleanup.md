# 阶段 E:前端 demo 清理 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 删除 Arco Pro 模板残留(demo 页 + demo 逻辑 + 冗余 localStorage 标志 + demo 依赖),修复 NavBar 角色切换覆盖真实权限的高危隐患,让前端只剩 login + system 业务页 + 必要框架。

**Architecture:** 纯删除型清理,不改业务逻辑。按"先删叶子(demo 页/依赖)→ 再删枝干(NavBar demo 逻辑/mock)→ 最后清理痕迹(i18n/死代码/品牌名)"的顺序,每步保持可编译可用。

**Tech Stack:** React 17 / Arco Design / react-router 5 / Vite 2 / TS(本阶段不升级技术栈,只清理)。

## Global Constraints

- **本阶段不升级任何技术栈**(React17/RR5/Vite2 保持),阶段 D 才升级。只做删除清理。
- 清理后必须保持 `tsc --noEmit` 与 `vite build` 通过(前端无测试框架,以类型检查+构建作为质量门)。
- 保留:`pages/system/`、`pages/login/`、`pages/exception/403/`(layout 兜底硬依赖)、`PermissionWrapper` 组件(留作工具)。
- **必须根除**:NavBar 角色切换 effect(用 generatePermission 覆盖后端真实权限)。
- 清理 `userStatus` / `userRole` localStorage 标志(无消费者)。
- 保留 `onecup_access_token` / `onecup_refresh_token`(真实业务 token)。
- 工作目录:`frontend/`。每步结束跑 `npm run build`(或 `tsc --noEmit`)+ 启动验证不报错。
- 国际化:demo 菜单的 i18n 条目(`menu.welcome`/`menu.user*`/`menu.dashboard*` 等)删除,保留 `menu.system*`/`navbar.logout`/`message.*`。

---

## Task 1: 删除 demo 页目录 + Chart 组件

**Files:**
- Delete: `frontend/src/pages/{dashboard,visualization,list,form,profile,result,user,welcome}/`(整个目录)
- Delete: `frontend/src/pages/exception/404/`、`frontend/src/pages/exception/500/`
- Delete: `frontend/src/components/Chart/`(8 个 bizcharts 图表组件,零业务引用)
- 保留:`frontend/src/pages/system/`、`frontend/src/pages/login/`、`frontend/src/pages/exception/403/`

**Interfaces:**
- Consumes: 无(layout 的 `import.meta.glob` 会自动收窄,不报错)。
- Produces: 删除后 `getFlattenRoutes` glob 自动只匹配 system + exception/403。

- [ ] **Step 1: 删除 demo 页目录**

```bash
cd frontend/src/pages
rm -rf dashboard visualization list form profile result user welcome
rm -rf exception/404 exception/500
# 确认保留
ls system login exception
```

- [ ] **Step 2: 删除 Chart 组件**

```bash
cd frontend/src/components
rm -rf Chart
```

- [ ] **Step 3: 类型检查 + 构建**

Run: `cd frontend && npx tsc --noEmit`
Expected: 可能报错(若 NavBar/layout 还 import 了已删目录)——这些断裂在 Task 2/3 修复。若仅有"找不到模块 './pages/...'"类报错属预期。记录报错清单。

> 注:`import.meta.glob` 是运行时动态扫描,删除目录不导致 tsc 报错。但 NavBar 的 demo 下拉项指向的 key 渲染空字符串(无编译错误)。Task 2 修 NavBar。

- [ ] **Step 4: 提交**

```bash
git add -A frontend/src/pages frontend/src/components
git commit -m "chore(fe): 删除 Arco Pro demo 页 + Chart 图表组件"
```

---

## Task 2: 清理 NavBar — 根除角色切换 + 删 demo 下拉项 + 品牌名

**Files:**
- Modify: `frontend/src/components/NavBar/index.tsx`
- Modify: `frontend/src/store/index.ts`(精简 userInfo 冗余字段)

> 这是最关键的 Task:NavBar 的角色切换 effect(generatePermission 覆盖真实权限)是高危隐患,必须彻底根除。

- [ ] **Step 1: 读 NavBar 当前内容,定位所有 demo 逻辑**

读 `frontend/src/components/NavBar/index.tsx`,确认这些要删的行:
- import `generatePermission` from '@/routes'(行 40)
- `useStorage('userStatus')`(行 47,变量 `_` 未用)
- `useStorage('userRole', 'admin')`(行 48)
- 监听 `role` 的 useEffect(行 70-80,覆盖真实权限!)
- `handleChangeRole`(行 94-97)
- droplist 里的 `role` SubMenu(行 101-118)
- droplist 里的 `more` SubMenu(行 123-140,指向 demo 页 key)
- droplist 里的 `setting` 菜单项(行 119-122,指向已删 user/setting)
- `<div>Arco Pro</div>`(行 155)→ 改 OneCup
- `localStorage.setItem('userStatus','logout')`(行 57)

- [ ] **Step 2: 精简 NavBar**

重写 NavBar,只保留:logo(OneCup)+ 用户名(displayName)+ 登出。删除上述全部 demo 逻辑。avatar 渲染改为:有则显示,无则用 displayName 首字母兜底(避免 undefined 空指针)。

- [ ] **Step 3: 精简 store userInfo**

`frontend/src/store/index.ts` 的 userInfo 接口,移除 demo 冗余字段(avatar/job/organization/location/email),只留 `name?: string` + `permissions: Record<string,string[]>`。同步移除 NavBar 对 `userInfo.avatar` 的依赖(用 displayName 首字母)。

- [ ] **Step 4: 类型检查 + 构建**

Run: `cd frontend && npx tsc --noEmit`
Expected: NavBar 相关报错消除。可能仍有 routes.ts generatePermission 未删的 unused 警告(Task 3 处理)。

- [ ] **Step 5: 提交**

```bash
git add frontend/src/components/NavBar frontend/src/store
git commit -m "fix(fe): 根除 NavBar 角色切换覆盖真实权限 + 删 demo 下拉项 + 精简 userInfo"
```

---

## Task 3: 清理 routes.ts 死代码 + localStorage userStatus/userRole

**Files:**
- Modify: `frontend/src/routes.ts`(删 generatePermission + getName 死代码)
- Modify: `frontend/src/api/request.ts`(删 redirectToLogin 的 userStatus 设置)
- Modify: `frontend/src/pages/login/form.tsx`(删 userStatus 设置)

- [ ] **Step 1: routes.ts 删 generatePermission + getName**

读 `frontend/src/routes.ts`:
- 删 `generatePermission(role)` 函数(行 59-70,NavBar Task 2 已不再 import)
- 删 `getName()` 函数(行 48-57,死代码,无外部引用)
- 保留 routes 数组 + useRoute hook

- [ ] **Step 2: 清理 userStatus localStorage(3 处)**

- `frontend/src/api/request.ts`:删 `redirectToLogin()` 里的 `localStorage.setItem('userStatus', 'logout')`
- `frontend/src/pages/login/form.tsx`:删登录成功后的 `localStorage.setItem('userStatus', 'login')`
- (NavBar 的 userStatus 已在 Task 2 删除)

> userRole 仅 NavBar 用,Task 2 已删。

- [ ] **Step 3: 类型检查**

Run: `cd frontend && npx tsc --noEmit`
Expected: 无 generatePermission 相关报错。

- [ ] **Step 4: 提交**

```bash
git add frontend/src/routes.ts frontend/src/api/request.ts frontend/src/pages/login
git commit -m "chore(fe): 删 routes.ts generatePermission/getName 死代码 + 清理 userStatus localStorage"
```

---

## Task 4: 清理 MessageBox + Settings + mock(及依赖)

**Files:**
- Delete: `frontend/src/components/MessageBox/`(依赖 mock 数据)
- Delete: `frontend/src/components/Settings/`(主题面板,demo 遗留)
- Delete: `frontend/src/mock/`(mockjs 数据源)
- Modify: `frontend/src/main.tsx`(删 `import './mock'`)
- Modify: `frontend/src/layout.tsx`(删 NavBar 里 MessageBox/Settings 的使用,及 getIconFromKey 死分支)
- Modify: `frontend/src/components/NavBar/index.tsx`(若 Task 2 未删 MessageBox/Settings 引用,补删)
- Modify: `frontend/package.json`(删 bizcharts/@antv/data-set/@turf/turf/mockjs/react-color/copy-to-clipboard)

- [ ] **Step 1: 确认 MessageBox/Settings 在 layout/NavBar 的引用点**

读 `frontend/src/layout.tsx` 看 NavBar 怎么渲染(是否传 MessageBox/Settings 子组件)。grep MessageBox/Settings 引用。确认删除后无残留 import。

- [ ] **Step 2: 删除组件 + mock 目录 + main.tsx import**

```bash
cd frontend/src
rm -rf components/MessageBox components/Settings mock
```
`main.tsx`:删 `import './mock';` 行。

- [ ] **Step 3: 修 layout.tsx**

删除 NavBar 渲染中对 MessageBox/Settings 的引用(若有)。清理 `getIconFromKey` 的死分支(dashboard/list/form/profile/visualization/result/user 的 case),只留 system + exception。

- [ ] **Step 4: 删 package.json demo 依赖**

`frontend/package.json` dependencies 删除:
- `bizcharts`、`@antv/data-set`、`@turf/turf`、`mockjs`(demo 数据/图表/地图)
- `react-color`、`copy-to-clipboard`(Settings 专用)

Run: `cd frontend && npm install`(更新 lockfile)

- [ ] **Step 5: 类型检查 + 构建**

Run: `cd frontend && npx tsc --noEmit && npm run build`
Expected: 构建成功。无 MessageBox/Settings/bizcharts 相关报错。

- [ ] **Step 6: 提交**

```bash
git add -A frontend
git commit -m "chore(fe): 删除 MessageBox/Settings/mock + demo 依赖 (bizcharts/turf/mockjs/react-color)"
```

---

## Task 5: 清理 i18n + 冒烟验证

**Files:**
- Modify: `frontend/src/locale/index.ts`(删 demo 菜单 i18n)
- Modify: `frontend/package.json`(name 改 OneCup,可选)

- [ ] **Step 1: 清理 locale demo 条目**

读 `frontend/src/locale/index.ts`,删除(en-US + zh-CN 双语):
- `menu.welcome`、`menu.user`、`menu.user.switchRoles`、`menu.user.role.admin`、`menu.user.role.user`
- 任何引用已删 demo 页的 navbar/settings/message 条目(若 Settings 删除后其 i18n 失效)
- 保留:`menu.system*`、`navbar.logout`、`message.*`、`menu.exception.403` 等

- [ ] **Step 2: (可选) package.json name**

`frontend/package.json`:`"name": "arco-design-pro"` → `"name": "one-cup-frontend"`。

- [ ] **Step 3: 全量构建验证**

Run: `cd frontend && npm run build`
Expected: 构建成功,无报错。

- [ ] **Step 4: tsc 零错误**

Run: `cd frontend && npx tsc --noEmit`
Expected: 零错误。

- [ ] **Step 5: 冒烟(启动)**

Run: `cd frontend && npm run dev`
验证清单:
1. 访问 `/login` → 登录页正常渲染
2. 登录后 → 跳 `/system/user` → 用户管理页正常
3. 左侧菜单只有「系统管理」一级 + 子菜单(user/role/permission/numbering)
4. NavBar 只有 logo(OneCup)+ 用户名 + 登出,无角色切换/通知铃铛/主题齿轮
5. developer 登录看不到 system 菜单(权限隔离)
6. 直接访问不存在路由 → 403 兜底页

- [ ] **Step 6: 提交**

```bash
git add frontend/src/locale frontend/package.json
git commit -m "chore(fe): 清理 demo i18n + 阶段E 冒烟验证"
```

---

## Self-Review(写作后自检)

**1. Spec 覆盖(spec 第 6 节 E demo 清理):**
- 6.1 删模板页 → Task 1 ✓
- 6.2 删 demo 逻辑(NavBar 角色切换/generatePermission/userStatus/userRole)→ Task 2+3 ✓
- 6.3 清理 i18n → Task 5 ✓
- 6.4 清理依赖 → Task 4 ✓

**2. 连锁风险:**
- exception/403 必须保留(layout 兜底)→ Task 1 明确保留 ✓
- 删 user/ → NavBar setting 菜单项失效 → Task 2 同步删 ✓
- 删 mock/ → MessageBox 数据源断 → Task 4 一并删 MessageBox ✓
- 删 Settings → react-color/copy-to-clipboard 零引用 → Task 4 删依赖 ✓
- PermissionWrapper 零引用但保留 → 决策已定(留作工具)✓

**3. 顺序合理性:** 先删叶子(页)→ 修 NavBar(核心隐患)→ 清死代码 → 删 mock/依赖 → 收尾 i18n。每步可独立编译验证。✓

**4. 阶段 D 前置:** 本阶段清理后,前端只剩干净的业务骨架,阶段 D(技术栈升级)不必搬运即将删除的代码。✓
