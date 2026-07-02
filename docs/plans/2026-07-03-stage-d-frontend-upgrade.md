# 阶段 D:前端技术栈升级 实现计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan step-by-step (inline execution — frontend upgrades are tightly-coupled serial refactors, not independent testable tasks; inline handles tsc/build breakages immediately). Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 一次性升级 OneCup 前端到当前主流技术栈:Vite 5、React 18、Redux Toolkit、TypeScript 5 strict、react-router 6 Data Router。

**Architecture:** 单分支串行,按依赖顺序 5 步推进(Vite5→React18→RTK→TS5strict→RR6 Data Router),每步 `tsc --noEmit` + `vite build` 验证。RR6 用 Data Router(createBrowserRouter + authLoader + RequirePermission),authLoader 取代 main.tsx 的 useEffect+window.location 硬跳转。

**Tech Stack:** React 18 / @reduxjs/toolkit / TypeScript 5 strict / Vite 5 / react-router-dom 6 Data Router / Arco Design 2.66(不变)/ axios 1.x。

## Global Constraints

- 工作目录 `frontend/`,单分支 `feat/stage-d-frontend-upgrade`(已建,已含设计文档)。
- 本阶段**不升级 Arco Design**(实际 2.66.15,已兼容 React18)。
- 每步质量门:`npx tsc --noEmit`(本步引入的错误为零)+ `npm run build`(成功)。
- `pages/system/numbering/index.tsx` 的 `Switch` 是 **Arco 组件**(非 router),勿误改;该文件有预存类型错误,**TS strict 步骤顺带修复**。
- 删除动态 glob(`import.meta.glob` + lazyload 机制),业务页(4 个)改 router.tsx 静态 import。
- 保留 `@loadable/component`(若 router 静态 import 后不再用,可在 RR6 步骤评估移除)。
- 别名 `@/` 在 tsconfig paths 与 vite alias 两处,保持同步(通配,通常无需改)。
- 质量门以"非 numbering 的 tsc 错误为零 + build 成功"为准(numbering 预存错误在 TS strict 步骤统一修)。

---

## Step 1: Vite 5 + plugin-react 4 + 清理 webpack 遗留

**Files:**
- Modify: `frontend/package.json`
- Modify: `frontend/tsconfig.json`(target es5→esnext)

> 首要验证 `@arco-plugins/vite-react` 与 Vite5 兼容性(最大未知风险)。不兼容则换官方 svgr + less 主题注入。

- [ ] **Step 1.1: 升级 Vite 相关依赖**

修改 `frontend/package.json`:
- `vite`: `^2.6.14` → `^5.4.0`
- `@vitejs/plugin-react`: `^1.1.0` → `^4.3.0`
- devDependencies 删除 webpack 遗留:`@arco-design/webpack-plugin`、`@svgr/webpack`、`less-loader`、`eslint-plugin-babel`

Run: `cd frontend && npm install --ignore-scripts`(忽略 husky prepare 报错)

- [ ] **Step 1.2: tsconfig target 升级**

`frontend/tsconfig.json`:`"target": "es5"` → `"target": "esnext"`

- [ ] **Step 1.3: 验证构建(关键 — 暴露 Arco vite 插件兼容性)**

Run: `cd frontend && npm run build`
Expected: 成功。
- **若失败**且报错与 `@arco-plugins/vite-react` 或 `@arco-plugins/vite-plugin-svgr` 相关:这两个插件可能不兼容 Vite5。
  - 先尝试升这两个插件到最新:`npm install @arco-plugins/vite-react@latest @arco-plugins/vite-plugin-svgr@latest --ignore-scripts`,再 build。
  - 仍失败:改用 Vite 原生方案——`vite.config.ts` 删除 `vitePluginForArco`,改用 less modifyVars 直接注入主题(`css.preprocessorOptions.less.modifyVars`)+ `vite-plugin-svgr` 官方插件替代 svgr。
  - 这是本步必须解决的阻塞,解决前不进 Step 2。
- **若成功**:继续。

Run: `cd frontend && npx tsc --noEmit`(确认无新增类型错误,numbering 预存的忽略)

- [ ] **Step 1.4: 提交**

```bash
git add -A frontend
git commit -m "feat(fe): Vite 2→5 + plugin-react 1→4 + 清理 webpack 遗留 devDeps"
```

---

## Step 2: React 18 + axios 1.x

**Files:**
- Modify: `frontend/package.json`
- Modify: `frontend/src/main.tsx`(createRoot)

- [ ] **Step 2.1: 升级 React + axios 依赖**

修改 `frontend/package.json`:
- `react`: `^17.0.2` → `^18.3.0`
- `react-dom`: `^17.0.2` → `^18.3.0`
- `@types/react`(devDeps): `^17.0.0` → `^18.3.0`
- `@types/react-dom`(devDeps): `^17.0.0` → `^18.3.0`
- `axios`: `^0.24.0` → `^1.7.0`

Run: `cd frontend && npm install --ignore-scripts`

- [ ] **Step 2.2: main.tsx 改 createRoot**

修改 `frontend/src/main.tsx`:
- L3: `import ReactDOM from 'react-dom';` → `import { createRoot } from 'react-dom/client';`
- L143: `ReactDOM.render(<Index />, document.getElementById('root'));` → 
```tsx
createRoot(document.getElementById('root')!).render(<Index />);
```

- [ ] **Step 2.3: 验证**

Run: `cd frontend && npx tsc --noEmit && npm run build`
Expected: 成功(React18 对 17 向后兼容;Arco 2.66 支持 18)。

> axios 1.x 拦截器 API 兼容,token 刷新排队(request.ts)逻辑不变。若 build 报 axios 相关类型错误,按提示修。

- [ ] **Step 2.4: 提交**

```bash
git add -A frontend
git commit -m "feat(fe): React 17→18 (createRoot) + axios 0.24→1.x"
```

---

## Step 3: Redux Toolkit(替换手写 redux)

**Files:**
- Modify: `frontend/package.json`
- Rewrite: `frontend/src/store/index.ts`
- Modify: `frontend/src/main.tsx`(configureStore + typed dispatch)
- Modify: `frontend/src/layout.tsx`(useAppSelector)
- Modify: `frontend/src/components/NavBar/index.tsx`(useAppSelector)
- Modify: `frontend/src/components/PermissionWrapper/index.tsx`(useAppSelector)

**Interfaces:**
- Produces: `store/index.ts` 导出 `store`、`useAppSelector`、`useAppDispatch`、`AppDispatch`、`RootState`;actions `setUserInfo(payload)`、`setSettings(payload)`。

- [ ] **Step 3.1: 升级依赖**

修改 `frontend/package.json`:
- dependencies 删除 `redux`、`react-redux`
- dependencies 加 `@reduxjs/toolkit`: `^2.2.0`、`react-redux`: `^9.1.0`

Run: `cd frontend && npm install --ignore-scripts`

- [ ] **Step 3.2: 重写 store/index.ts**

```typescript
// frontend/src/store/index.ts
import { configureStore, createSlice } from '@reduxjs/toolkit';
import type { TypedUseSelectorHook } from 'react-redux';
import { useDispatch, useSelector } from 'react-redux';
import defaultSettings from '../settings.json';

export interface UserInfoState {
  name?: string;
  permissions: Record<string, string[]>;
}
export interface UserInfoSliceState {
  userInfo: UserInfoState;
  userLoading: boolean;
}

const initialUserInfo: UserInfoSliceState = {
  userInfo: { permissions: {} },
  userLoading: false,
};

const userInfoSlice = createSlice({
  name: 'userInfo',
  initialState: initialUserInfo,
  reducers: {
    setUserInfo(state, action: { payload: Partial<UserInfoSliceState> }) {
      Object.assign(state, action.payload);
    },
  },
});

const settingsSlice = createSlice({
  name: 'settings',
  initialState: defaultSettings as typeof defaultSettings,
  reducers: {
    setSettings(_state, action: { payload: typeof defaultSettings }) {
      return action.payload;
    },
  },
});

export const { setUserInfo } = userInfoSlice.actions;
export const { setSettings } = settingsSlice.actions;

export const store = configureStore({
  reducer: {
    userInfo: userInfoSlice.reducer,
    settings: settingsSlice.reducer,
  },
});

export type RootState = ReturnType<typeof store.getState>;
export type AppDispatch = typeof store.dispatch;
export const useAppSelector: TypedUseSelectorHook<RootState> = useSelector;
export const useAppDispatch: () => AppDispatch = useDispatch;

// 兼容旧 GlobalState 引用(layout/NavBar/PermissionWrapper 用 state.userInfo 等)
export interface GlobalState {
  userInfo: UserInfoSliceState['userInfo'];
  userLoading: boolean;
  settings: typeof defaultSettings;
}
```

> 注:`useAppSelector` 读的是 `{ userInfo: {...}, settings }` 的 RootState 结构(与旧 GlobalState 字段一致,组件 `state.userInfo` / `state.settings` 不变)。但注意旧 store 是 `state.userInfo`(对象含 name/permissions)且 `state.userLoading` 在顶层——新 RootState 的 `userLoading` 在 `state.userInfo.userLoading`。需同步调整消费点(Step 3.4)。

- [ ] **Step 3.3: 改 main.tsx(configureStore + typed dispatch)**

修改 `frontend/src/main.tsx`:
- L4-5: `import { createStore } from 'redux'; import { Provider } from 'react-redux';` → `import { Provider } from 'react-redux';`
- L11: `import rootReducer from './store';` → `import { store, setUserInfo } from './store';`
- L19: 删除 `const store = createStore(rootReducer);`
- L67-70 `store.dispatch({ type: 'update-userInfo', payload: { userLoading: true } })` → `store.dispatch(setUserInfo({ userLoading: true }));`
- L76-85 `store.dispatch({ type: 'update-userInfo', payload: { userInfo: {...}, userLoading: false } })` → `store.dispatch(setUserInfo({ userInfo: { name: user.displayName, permissions }, userLoading: false }));`
- L88-91 catch 的 dispatch → `store.dispatch(setUserInfo({ userLoading: false }));`

- [ ] **Step 3.4: 改消费点(useAppSelector + userLoading 路径)**

`layout.tsx`:`import { useSelector } from 'react-redux'` → `import { useAppSelector } from '@/store'`;`useSelector((state: GlobalState) => state)` → `useAppSelector((state) => state)`;若有 `state.userLoading` → `state.userInfo.userLoading`(注意新结构)。

`NavBar/index.tsx`:同样改 useAppSelector;`const { userInfo, userLoading } = useSelector(...)` → `const { userInfo, userLoading } = useAppSelector((state) => state.userInfo)`。

`PermissionWrapper/index.tsx`:`useSelector((state: GlobalState) => state.userInfo)` → `useAppSelector((state) => state.userInfo.userInfo)`(RootState 是 `{ userInfo: { userInfo, userLoading }, settings }`)。

> 关键:新 RootState 顶层是 `{ userInfo: { userInfo, userLoading }, settings }`(slice 嵌套)。读用户信息用 `state.userInfo.userInfo`,读 loading 用 `state.userInfo.userLoading`,读 settings 用 `state.settings`。逐文件确认引用路径。

- [ ] **Step 3.5: 验证**

Run: `cd frontend && npx tsc --noEmit && npm run build`
Expected: 成功。若有类型错误,多为 userLoading/userInfo 路径不一致——按 RootState 结构调整。

- [ ] **Step 3.6: 提交**

```bash
git add -A frontend
git commit -m "feat(fe): 手写 redux → Redux Toolkit (createSlice+configureStore+typed hooks)"
```

---

## Step 4: TypeScript 5 + strict + 修复 numbering 类型错误

**Files:**
- Modify: `frontend/package.json`
- Modify: `frontend/tsconfig.json`
- Modify: `frontend/src/layout.tsx`(补类型注解)
- Modify: `frontend/src/routes.ts`(补类型注解)
- Modify: `frontend/src/utils/useStorage.ts`(补类型注解)
- Modify: `frontend/src/pages/system/numbering/index.tsx`(修预存类型错误)
- 其他隐式 any 文件按 tsc 报错逐个修

- [ ] **Step 4.1: 升级 TS + eslint 相关**

修改 `frontend/package.json`:
- `typescript`(devDeps): `^4.5.2` → `^5.5.0`
- `@typescript-eslint/eslint-plugin`: `^5.4.0` → `^7.0.0`
- `@typescript-eslint/parser`: `^5.4.0` → `^7.0.0`

Run: `cd frontend && npm install --ignore-scripts`

- [ ] **Step 4.2: 开启 strict**

修改 `frontend/tsconfig.json`:`"strict": false` → `"strict": true`

- [ ] **Step 4.3: 跑 tsc 收集全部错误**

Run: `cd frontend && npx tsc --noEmit 2>&1 | tee /tmp/d-tsc.txt`
Expected: 大量错误(strict 暴露的隐式 any + numbering 预存错误)。

- [ ] **Step 4.4: 逐文件修复类型错误**

按 tsc 报错清单逐个修(都是补类型注解,非逻辑改动):
- `layout.tsx`:`getFlattenRoutes(routes: IRoute[])`、`renderRoutes(locale: Locale)`、`onClickMenuItem(key: string)`、`useState` 泛型等
- `routes.ts`:`useRoute(userPermission: Record<string, string[]>)`
- `utils/useStorage.ts`:`getDefaultStorage(key: string)`、`setStorageValue(key: string, value: string)`
- `main.tsx`:`getArcoLocale()` 返回类型、`transformPermissions` 已有类型
- 其他文件按报错修

**修复 numbering 预存错误**(`pages/system/numbering/index.tsx`):
- boolean 赋值给 value 类型(Form 的 value 期望 string|number):把 boolean 状态转为字符串(如 `'true'/'false'`)或用受控 Select 替代 Switch,按报错具体行修
- Dayjs 类型不匹配(dateRange):统一类型为 `Dayjs[]` 或 `string[]`,按 Form 的 dateRange 字段类型对齐
- Partial 缺字段(CreateNumberingRuleRequest):补全必填字段或用 `as CreateNumberingRuleRequest` 兜底(若确实是初始化空表单,用 `as` 合理)

每修一批跑一次 `npx tsc --noEmit | grep "error TS" | wc -l` 看错误数下降,直到为零。

- [ ] **Step 4.5: 验证(strict 全通过)**

Run: `cd frontend && npx tsc --noEmit`
Expected: **零错误**(含 numbering,strict 全项目通过)。

Run: `cd frontend && npm run build`
Expected: 成功。

- [ ] **Step 4.6: 提交**

```bash
git add -A frontend
git commit -m "feat(fe): TypeScript 5 + strict:true + 补全隐式any + 修复numbering类型错误"
```

---

## Step 5: react-router 6 Data Router

**Files:**
- Modify: `frontend/package.json`
- Create: `frontend/src/router.tsx`(createBrowserRouter + authLoader + 路由对象)
- Create: `frontend/src/components/RequirePermission/index.tsx`
- Modify: `frontend/src/main.tsx`(RouterProvider,删 fetchUserInfo useEffect + window.location 硬跳转)
- Modify: `frontend/src/layout.tsx`(纯布局 + Outlet,删 Switch/Route/glob)
- Modify: `frontend/src/routes.ts`(保留菜单过滤,删路由渲染)
- Delete: `frontend/src/utils/lazyload.tsx`(若不再用;业务页改静态 import)

**Interfaces:**
- Consumes: RTK `store` + `setUserInfo`(Step 3)、`transformPermissions`(从 main.tsx 提取)、`checkLogin`、`getCurrentUser`
- Produces: `router`(createBrowserRouter 实例)、`<RequirePermission>` 组件

- [ ] **Step 5.1: 升级 RR 依赖**

修改 `frontend/package.json`:
- `react-router`: `^5.2.0` → `^6.26.0`
- `react-router-dom`: `^5.2.0` → `^6.26.0`

Run: `cd frontend && npm install --ignore-scripts`

- [ ] **Step 5.2: 创建 RequirePermission 组件**

```tsx
// frontend/src/components/RequirePermission/index.tsx
import React from 'react';
import { useAppSelector } from '@/store';
import auth from '@/utils/authentication';
import Forbidden from '@/pages/exception/403';

interface RequirePermissionProps {
  resource: string;
  actions?: string[];
  children: React.ReactNode;
}

/** 路由 element 权限包装:无权限渲染 Forbidden,有权限渲染 children。 */
export default function RequirePermission({ resource, actions = ['manage'], children }: RequirePermissionProps) {
  const { userInfo } = useAppSelector((state) => state.userInfo);
  const permissions = userInfo?.permissions ?? {};
  // admin 通配
  if (permissions['*']?.includes('*')) return <>{children}</>;
  const allowed = auth({ requiredPermissions: [{ resource, actions }] }, permissions);
  return allowed ? <>{children}</> : <Forbidden />;
}
```

- [ ] **Step 5.3: 创建 router.tsx(createBrowserRouter + authLoader)**

```tsx
// frontend/src/router.tsx
import { createBrowserRouter, redirect, Navigate } from 'react-router-dom';
import { store, setUserInfo } from '@/store';
import { getCurrentUser } from '@/api/auth';
import { getAccessToken } from '@/utils/token';
import PageLayout from '@/layout';
import Login from '@/pages/login';
import Forbidden from '@/pages/exception/403';
import UserPage from '@/pages/system/user';
import RolePage from '@/pages/system/role';
import PermissionPage from '@/pages/system/permission';
import NumberingPage from '@/pages/system/numbering';
import RequirePermission from '@/components/RequirePermission';

/** 把后端 permCodes 转为前端 Record<resource, actions[]>。admin 通配。 */
function transformPermissions(
  permCodes: string[],
  roles: string[],
): Record<string, string[]> {
  if (roles.includes('admin') || permCodes.includes('*')) {
    return { '*': ['*'] };
  }
  const result: Record<string, string[]> = {};
  permCodes.forEach((code) => {
    const parts = code.split(':');
    if (parts.length >= 2) {
      const action = parts[parts.length - 1];
      const resource = parts.slice(0, -1).join(':');
      if (!result[resource]) result[resource] = [];
      result[resource].push(action);
    }
  });
  return result;
}

/** 根路由 loader:校验登录态 + 预取 userInfo → dispatch 到 RTK。 */
async function authLoader() {
  if (!getAccessToken()) {
    throw redirect('/login');
  }
  try {
    const user = await getCurrentUser();
    const permissions = transformPermissions(user.permissions, user.roles);
    store.dispatch(setUserInfo({
      userInfo: { name: user.displayName, permissions },
      userLoading: false,
    }));
  } catch {
    store.dispatch(setUserInfo({ userLoading: false }));
    throw redirect('/login');
  }
  return null;
}

export const router = createBrowserRouter([
  {
    path: '/',
    element: <PageLayout />,
    errorElement: <Forbidden />,
    loader: authLoader,
    children: [
      { index: true, element: <Navigate to="/system/user" replace /> },
      {
        path: 'system/user',
        element: <RequirePermission resource="system:user" actions={['manage']}><UserPage /></RequirePermission>,
      },
      {
        path: 'system/role',
        element: <RequirePermission resource="system:role" actions={['manage']}><RolePage /></RequirePermission>,
      },
      { path: 'system/permission', element: <PermissionPage /> },
      {
        path: 'system/numbering',
        element: <RequirePermission resource="system:numbering" actions={['view']}><NumberingPage /></RequirePermission>,
      },
    ],
  },
  { path: '/login', element: <Login /> },
  { path: '*', element: <Forbidden /> },
]);
```

- [ ] **Step 5.4: 重写 main.tsx(RouterProvider,删 useEffect 硬跳转)**

`frontend/src/main.tsx` 大幅精简:
- 删除 `BrowserRouter, Switch, Route` import、`getCurrentUser` import、`rootReducer/store` import(改用 RTK store)、`fetchUserInfo` 函数、登录态 useEffect、`transformPermissions`(搬到 router.tsx)
- 保留 ConfigProvider + Provider + GlobalContext.Provider 包裹
- 用 `RouterProvider router={router}` 替代 `<BrowserRouter><Switch>...`

```tsx
// frontend/src/main.tsx(精简后骨架)
import './style/global.less';
import React, { useEffect } from 'react';
import { createRoot } from 'react-dom/client';
import { Provider } from 'react-redux';
import { RouterProvider } from 'react-router-dom';
import { ConfigProvider } from '@arco-design/web-react';
import zhCN from '@arco-design/web-react/es/locale/zh-CN';
import enUS from '@arco-design/web-react/es/locale/en-US';
import { store } from './store';
import { router } from './router';
import { GlobalContext } from './context';
import changeTheme from './utils/changeTheme';
import useStorage from './utils/useStorage';

function Index() {
  const [lang, setLang] = useStorage('arco-lang', 'en-US');
  const [theme, setTheme] = useStorage('arco-theme', 'light');

  useEffect(() => { changeTheme(theme); }, [theme]);

  const arcoLocale = lang === 'zh-CN' ? zhCN : (lang === 'en-US' ? enUS : zhCN);
  const contextValue = { lang, setLang, theme, setTheme };

  return (
    <ConfigProvider locale={arcoLocale} componentConfig={{ Card: { bordered: false }, List: { bordered: false }, Table: { border: false } }}>
      <Provider store={store}>
        <GlobalContext.Provider value={contextValue}>
          <RouterProvider router={router} />
        </GlobalContext.Provider>
      </Provider>
    </ConfigProvider>
  );
}

createRoot(document.getElementById('root')!).render(<Index />);
```

- [ ] **Step 5.5: 重构 layout.tsx(纯布局 + Outlet,删 Switch/Route/glob)**

`frontend/src/layout.tsx` 改动:
- 删除 `import { Switch, Route, Redirect, useHistory } from 'react-router-dom'` → `import { Outlet, useNavigate } from 'react-router-dom'`
- 删除 `lazyload` import、`getFlattenRoutes` 函数、`import.meta.glob`(动态 glob 删除,业务页改 router.tsx 静态 import)
- `const history = useHistory()` → `const navigate = useNavigate()`;`history.push(path)` → `navigate(path)`
- 删除 `<Switch><Route ...>...</Switch>` 整块(路由渲染交给 Data Router)
- 主体改为:菜单(用 useRoute 过滤)+ NavBar + `<Outlet/>`(子路由出口)+ Footer
- `route.component`/`component.preload()` 逻辑删除(静态 import 无需 preload)

> layout 渲染菜单仍用 `routes.ts` 的 routes 数组 + useRoute hook 过滤可见菜单。菜单项点击 `navigate(path)`。

- [ ] **Step 5.6: routes.ts 保留菜单过滤**

`frontend/src/routes.ts`:保留 `routes` 数组(菜单数据)+ `IRoute` 类型 + `useRoute` hook(菜单可见性过滤)。删除已无用的路由渲染相关代码(若有)。菜单点击的 `key` 仍用 `system/user` 等路径。

- [ ] **Step 5.7: 删除 lazyload(若不再引用)**

确认 layout 不再 import lazyload 后:
```bash
rm frontend/src/utils/lazyload.tsx
```
package.json 删除 `@loadable/component` 依赖。

- [ ] **Step 5.8: 修 login/form.tsx 的 Link**

`frontend/src/pages/login/form.tsx`:确认 `<Link>` 用法,RR6 的 Link API 兼容,确认 import 来源是 `react-router-dom`。

- [ ] **Step 5.9: 验证**

Run: `cd frontend && npx tsc --noEmit`
Expected: 零错误。

Run: `cd frontend && npm run build`
Expected: 成功。

Run: `cd frontend && npm run dev`
手动验证(冒烟):
1. 访问 `/` → 未登录自动跳 `/login`
2. 登录成功 → 跳 `/system/user`,侧边栏菜单正常
3. developer 登录 → 看不到 system 菜单(权限隔离);直接输 `/system/user` → 渲染 Forbidden(RequirePermission 拦截)
4. NavBar 登出 → 跳 `/login`
5. 访问不存在路径 → Forbidden

- [ ] **Step 5.10: 提交**

```bash
git add -A frontend
git commit -m "feat(fe): react-router 5→6 Data Router (createBrowserRouter+authLoader+RequirePermission)

- authLoader 取代 main.tsx useEffect+window.location 硬跳转(消除竞态/闪烁)
- <RequirePermission> 包装业务路由 element(权限拦截直访URL)
- 菜单过滤保留 useRoute(两道独立防线)
- 删除动态 glob + lazyload,业务页静态 import
- layout 改纯布局(<Outlet/>),删 Switch/Route/useHistory"
```

---

## Self-Review(写作后自检)

**1. Spec 覆盖(spec 各节):**
- 3.1 Vite5 → Step 1 ✓
- 3.2 React18 → Step 2 ✓
- 3.3 RTK → Step 3 ✓
- 3.4 TS5strict + numbering 修复 → Step 4 ✓
- 3.5 RR6 Data Router → Step 5 ✓
- 3.6 axios1.x → Step 2 附带 ✓;webpack 清理 → Step 1 附带 ✓

**2. 顺序一致性:** Vite5→React18→RTK→TSstrict→RR6,与 spec 一致;RR6 依赖 RTK(authLoader dispatch)在 RTK 之后 ✓。

**3. RTK RootState 结构陷阱:** Step 3.2/3.4 已明确标注——新结构是 `{ userInfo: { userInfo, userLoading }, settings }`(slice 嵌套),消费点路径需调整。这是最容易出错的地方,执行时重点核对。

**4. Data Router loader 返回 null:** authLoader 把数据 dispatch 到 RTK 后 return null,组件用 useAppSelector 读(非 useLoaderData)——与 spec 决策"loader预取→RTK"一致 ✓。

**5. 占位符扫描:** 无 TBD/TODO。Step 1.3 的 Arco 插件不兼容处理给了具体降级方案(官方 svgr + less modifyVars)。Step 4.4 的 numbering 修复给了方向(boolean→string、Dayjs 对齐、Partial 用 as)——这些是按具体报错修,无法预写完整代码,但方向明确。
