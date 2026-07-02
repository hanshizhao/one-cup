# 阶段 D:前端技术栈升级 设计文档

> OneCup 印染厂面料开发管理系统 — 前端技术栈升级
> 创建日期: 2026-07-03
> 状态: 已确认
> 关联:
> - [用户角色权限整改方案](2026-07-02-rbac-overhaul-design.md)(第 7 节 D 技术栈升级)
> - 阶段 A/B/C/E 已完成合并

---

## 1. 背景与目标

### 1.1 背景

前端源自 Arco Design Pro 模板,技术栈整体偏旧。阶段 E 已清理 demo 残留,现在前端是干净的业务骨架(8 个业务页 + 4 个组件),是升级技术栈的最佳时机——升级时不必搬运即将废弃的代码。

### 1.2 现状(阶段 E 后)

| 维度 | 当前(实际安装版本) | 问题 |
|------|------|------|
| React | 17.0.2 | 非当前主流 |
| react-router | 5.3.4 | v5,路由范式旧(Switch/Redirect/useHistory) |
| Vite | 2.9.18 | v2,偏老 |
| TypeScript | 4.9.5 | `strict: false` |
| 状态管理 | redux 4.2.1 + react-redux 7.2.9(手写 reducer) | 无 RTK,裸字符串 action |
| axios | 0.24.0 | 偏旧,有已知问题 |
| Arco Design | 2.66.15(声明 ^2.32.2) | 已原生支持 React18,无需升级 |

业务面极小:8 个业务页(`system/*` 4 个 + `login/*` 3 个 + `exception/403`)、4 个组件(Footer/NavBar/PermissionWrapper/lazyload)。改动集中在 4 个核心文件:`main.tsx`、`layout.tsx`、`routes.ts`、`store/index.ts`。

### 1.3 目标

一次性升级到当前主流技术栈,消除技术债:
1. Vite 2→5(+ plugin-react 1→4)
2. React 17→18(createRoot)
3. 手写 redux → Redux Toolkit
4. TypeScript 4→5 + `strict: true`
5. react-router 5→6(useRoutes 数据驱动)
6. 附带:axios 0.24→1.x、清理 webpack 遗留 devDeps、修复 numbering 页预存类型错误

### 1.4 已确认决策(头脑风暴)

| 维度 | 决策 |
|------|------|
| 组织方式 | **单分支串行**(5 项按依赖顺序,每项 tsc+build 验证,一个 PR 合并) |
| 升级顺序 | Vite5 → React18 → RTK → TS5strict → RR6(+ axios 附带) |
| RR6 路由声明 | **Data Router**(createBrowserRouter + RouterProvider + authLoader + element 权限包装) |
| 懒加载 | **保留 @loadable/component**(与 RR6 兼容,保留 preload) |
| axios | **升到 1.x**(API 兼容,修已知问题) |
| TS strict 隔离 | **顺手修复 numbering 页类型错误**(让全项目 strict 通过,不豁免) |
| StrictMode | 暂不启用(避免 fetchUserInfo 双调) |

---

## 2. 升级顺序与依赖

```
1. Vite 5 + plugin-react 4       ← 打地基,优先验证 Arco vite 插件兼容性(最大未知风险)
2. React 18(createRoot)          ← 依赖 plugin-react 4
3. Redux Toolkit                  ← store 重写,为 TS strict 提供类型铺垫;authLoader 依赖它(dispatch)
4. TypeScript 5 + strict          ← RTK 已有类型,补全剩余隐式 any + 修 numbering 类型错误
5. react-router 6(Data Router)   ← 依赖 RTK(authLoader dispatch userInfo);改动最集中,最后做
+ 附带: axios 1.x(随 React18 步骤)、webpack 遗留 devDeps 清理(随 Vite5 步骤)
```

每步完成跑 `npx tsc --noEmit` + `npm run build` 验证(前端无测试框架,以类型检查 + 构建为质量门)。

> 注意顺序:RR6(Data Router)现在依赖 RTK(authLoader 要 dispatch userInfo),所以必须在 RTK 之后。这维持了原"Vite→React→RTK→TS→RR6"的顺序,无需调整。

---

## 3. 各项升级设计

### 3.1 Vite 5 + plugin-react 4

**改动:**
- `package.json`:`vite` ^5、`@vitejs/plugin-react` ^4
- `vite.config.ts`:配置 API 基本兼容,无需大改
- 清理 webpack 遗留 devDeps:`@arco-design/webpack-plugin`、`@svgr/webpack`、`less-loader`、`eslint-plugin-babel`
- tsconfig `target` es5 → esnext(可选,顺手)

**风险点(本步首要验证):** `@arco-plugins/vite-react`(1.3.3)和 `@arco-plugins/vite-plugin-svgr`(0.7.2)对 Vite5 的兼容性。这两个 Arco 官方插件版本较旧。
- 若兼容:无需额外处理。
- 若不兼容:改用 Vite 原生 less 主题注入(less modifyVars)+ `vite-plugin-svgr` 官方替代。这是本步必须尽早验证的原因——Vite5 升不通,后续都无从谈起。

### 3.2 React 18

**改动:**
- `package.json`:`react`/`react-dom` ^18、`@types/react`/`@types/react-dom` ^18
- `main.tsx`:`import ReactDOM from 'react-dom'` → `import { createRoot } from 'react-dom/client'`;`ReactDOM.render(<Index/>, root)` → `createRoot(root).render(<Index/>)`
- 暂不加 StrictMode(避免 fetchUserInfo effect 双调;后续可评估)
- Arco 2.66 已支持 React18,ConfigProvider/组件无改动

### 3.3 Redux Toolkit

**改动:**
- `package.json`:移除 `redux`、`react-redux`(RTK 内含),加 `@reduxjs/toolkit` + `react-redux` ^9(配合 React18)
- `store/index.ts` 重写:
  - `userInfoSlice`(state: `{ name?, permissions, userLoading? }`,reducers: `setUserInfo`)
  - `settingsSlice`(state: `settings`,reducers: `setSettings`)
  - `configureStore({ reducer: { userInfo, settings } })`
  - 导出 typed hooks:`useAppSelector`/`useAppDispatch`、`RootState`
- `main.tsx`:`createStore(rootReducer)` → `configureStore(...)`;3 处 `dispatch({type:'update-userInfo', payload})` → `dispatch(setUserInfo({...}))`
- 4 个 useSelector 消费点(layout/NavBar/PermissionWrapper)→ typed `useAppSelector`(或保留 useSelector 配 RootState 类型)
- `GlobalState` 接口由 slice RootState 推导,不再手写

**状态极简:** 1 reducer/2 action/0 业务 dispatch/4 消费点,无 thunk/saga,机械重写。

### 3.4 TypeScript 5 + strict

**改动:**
- `package.json`:`typescript` ^5、`@typescript-eslint/*` ^6(配合 TS5)
- `tsconfig.json`:`strict: false` → `true`;`target` es5 → esnext
- 补全隐式 any(RTK 后 store 自动有类型,重点在 layout/routes/useStorage):
  - `layout.tsx`:`getFlattenRoutes(routes)`、`renderRoutes`、`onClickMenuItem(key)` 等
  - `routes.ts`:`useRoute(userPermission)` 参数类型
  - `utils/useStorage.ts`:参数类型
- **修复 numbering 页预存类型错误**(决策 A):`pages/system/numbering/index.tsx` 的 boolean 赋值、Dayjs 类型不匹配、Partial 缺字段等(阶段 E 发现的预存问题),让全项目 strict 通过

### 3.5 react-router 6(Data Router)

采用 RR6.4+ Data Router 范式:`createBrowserRouter` + `RouterProvider` + `loader`。比 useRoutes 更进一步——用 authLoader 顺手解决 main.tsx 的登录态硬跳转竞态问题(阶段 A 标记的隐患)。

**架构:**
```
createBrowserRouter([
  { path: '/', element: <RootLayout/>, errorElement: <RootError/>, loader: authLoader, children: [
      { index: true, element: <Navigate to="system/user" replace/> },
      { path: 'system/user', element: <RequirePermission req="system:user:manage"><UserPage/></RequirePermission> },
      { path: 'system/role', element: <RequirePermission req="system:role:manage"><RolePage/></RequirePermission> },
      { path: 'system/permission', element: <PermissionPage/> },
      { path: 'system/numbering', element: <RequirePermission req="system:numbering:view"><NumberingPage/></RequirePermission> },
  ]},
  { path: '/login', element: <LoginPage/> },
  { path: '*', element: <Forbidden403/> },
])
main.tsx: createRoot(root).render(<RouterProvider router={router}/>)
```

**核心组件:**
- **`authLoader`**(`router.tsx`):路由解析时执行。校验登录态(`checkLogin`):未登录 → `redirect('/login')`;已登录 → `getCurrentUser()` + `transformPermissions()` → `dispatch(setUserInfo(...))` 到 RTK。返回 null(数据进 RTK,组件用 useAppSelector 读)。这取代 main.tsx 的 `useEffect` + `window.location` 硬跳转——渲染时数据已就绪,无闪烁/竞态。
- **`<RequirePermission>`**(`components/RequirePermission`):包装业务路由 element。读 RTK userInfo.permissions,无权限渲染 `<Forbidden403/>`,有权限渲染 children。权限逻辑集中可见。
- **`<RootLayout>`**(原 layout.tsx 重构):含 `<Outlet/>`(子路由出口)+ 侧边栏菜单(用现有 useRoute 过滤可见菜单)+ NavBar。不再含 `<Switch>/<Route>`(Data Router 接管路由渲染)。
- **菜单过滤保留**:`routes.ts` 的静态 `routes` 数组 + `useRoute` hook 仍用于**菜单可见性过滤**(侧边栏渲染),与路由 element 的 `<RequirePermission>` 是两道独立防线(菜单隐藏 + 直访 URL 拦截)。

**改动文件:**
- 新增 `router.tsx`(createBrowserRouter + authLoader + 路由对象)
- 新增 `components/RequirePermission/index.tsx`
- `main.tsx`:`BrowserRouter` + 手写 Switch/Route → `RouterProvider router={router}`;删 fetchUserInfo useEffect + window.location 硬跳转(交给 authLoader)
- `layout.tsx`:删 `<Switch>/<Route>/<Redirect>`;改为纯布局(菜单 + NavBar + `<Outlet/>`);`useHistory` → `useNavigate`;`history.push` → `navigate`;删除 `getFlattenRoutes` + `lazyload` glob(Data Router 用静态 element,不再需要动态 glob 扫描)——组件改为直接 import 或 React.lazy
- `routes.ts`:保留 routes 数组 + useRoute(菜单过滤);删 getName(阶段 E 已删)
- 懒加载:Data Router 下用 React.lazy + Suspense 或保留 @loadable;`getFlattenRoutes` 的动态 glob 删除后,页面组件改为 router.tsx 里静态 import(业务页少,无需懒加载)或按需 lazy

**关于动态 glob 的取舍:** 当前 layout 用 `import.meta.glob` 扫描所有 pages 再 lazyload,这是为了 Arco 模板的动态菜单。Data Router 用静态路由对象,业务页只有 4 个,**直接 import 更简单清晰**,删除 glob + lazyload 机制(减少黑魔法)。若将来页多再上 React.lazy。

**注意(勿误改):** `pages/system/numbering/index.tsx` 和 `user/index.tsx` 里的 `Switch` 是 **Arco 的 Switch 组件**(非 router Switch),迁移时不能动。

### 3.6 附带项

- **axios 0.24→1.x**:API 兼容(实例/拦截器/队列不变),修已知问题。随 React18 步骤一起升。
- **webpack 遗留 devDeps 清理**:随 Vite5 步骤。
- **品牌/别名**:tsconfig paths `@/*` 与 vite alias 保持同步(通配,无需改)。

---

## 4. 验证标准(每步 + 最终)

每步完成:
- `npx tsc --noEmit` 零错误(本步引入的)
- `npm run build` 成功

最终(全部完成后):
1. `tsc --noEmit` 零错误(strict 模式,含 numbering 页)
2. `npm run build` 成功
3. 路由用 RR6 Data Router(createBrowserRouter/RouterProvider/authLoader),无 BrowserRouter/Switch/Route/Redirect/useHistory;权限经 `<RequirePermission>` 包装
4. 状态管理用 RTK(createSlice/configureStore/typed hooks),无手写 reducer/裸字符串 action
5. React18(createRoot),无 ReactDOM.render
6. Vite5 + plugin-react4,构建正常
7. `npm run dev` 启动正常
8. 登录→系统管理页→权限隔离 全流程可用(手动冒烟)

---

## 5. 风险与缓解

| 风险 | 影响 | 缓解 |
|------|------|------|
| @arco-plugins/vite-react 与 Vite5 不兼容 | 阻塞 Vite5 升级(地基) | **Vite5 最先做**,尽早暴露;不兼容则换官方 svgr 插件 + less 主题注入 |
| RR6 Data Router 与现有架构整合 | 中,路由入口/布局/登录态全部重构 | authLoader 取代 main.tsx useEffect(消除竞态);<RequirePermission> 包装 element;菜单过滤保留 useRoute;删动态 glob 改静态 import |
| authLoader 依赖 RTK(dispatch) | 低 | RTK 在 RR6 之前完成;loader 内 dispatch userInfo 到 store |
| TS strict 暴露大量隐式 any | 中,补全工作量大 | RTK 先做(自动类型铺垫);numbering 预存错误顺手修(决策A) |
| React18 effect 双调(fetchUserInfo) | 低 | 暂不加 StrictMode;useRef 防重或后续评估 |
| axios 1.x 拦截器行为微变 | 低 | API 兼容;升级后重点验证 token 刷新排队(request.ts) |

---

## 6. 不在范围内

- 不加 StrictMode(后续评估)
- 不改 Arco Design 版本(2.66 已够用)
- 不引入 RR6 Data Router(createBrowserRouter/loader,对当前项目过度)
- 不换 @loadable 为 React.lazy(保留 preload)
- 不加 server.proxy(保持 CORS 直连)
- 不补前端单测(阶段 F 才做)
