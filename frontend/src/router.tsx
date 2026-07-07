import React, { Suspense, lazy } from 'react';
import { createBrowserRouter, redirect, Navigate } from 'react-router-dom';
import { Spin } from '@arco-design/web-react';
import { store, setUserInfo } from '@/store';
import { getCurrentUser } from '@/api/auth';
import { getAccessToken } from '@/utils/token';
import PageLayout from '@/layout';
import Login from '@/pages/login';
import Forbidden from '@/pages/exception/403';
import RequirePermission from '@/components/RequirePermission';

// 业务页按需懒加载(代码分割)
const UserPage = lazy(() => import('@/pages/system/user'));
const RolePage = lazy(() => import('@/pages/system/role'));
const PermissionPage = lazy(() => import('@/pages/system/permission'));
const CustomerPage = lazy(() => import('@/pages/business/customer'));
const MaterialPage = lazy(() => import('@/pages/business/material'));
const ProcessPage = lazy(() => import('@/pages/business/process'));
const EquipmentPage = lazy(() => import('@/pages/business/equipment'));
// 设备模块表单页：设备类型 / 模板走独立页；设备走 Drawer（无独立路由）
const EquipmentTypeFormPage = lazy(() => import('@/pages/business/equipment/type/TypeFormPage'));
const EquipmentTemplateFormPage = lazy(() => import('@/pages/business/equipment/type/template/TemplateFormPage'));
const NumberingPage = lazy(() => import('@/pages/system/numbering'));
const OperationLogPage = lazy(() => import('@/pages/system/operation-log'));
const LoginLogPage = lazy(() => import('@/pages/system/login-log'));
const ColorPage = lazy(() => import('@/pages/master-data/color'));
const UnitPage = lazy(() => import('@/pages/system/unit'));

const PageFallback = () => (
  <div style={{ display: 'flex', justifyContent: 'center', padding: '40px 0' }}>
    <Spin />
  </div>
);

const withSuspense = (node: React.ReactNode) => (
  <Suspense fallback={<PageFallback />}>{node}</Suspense>
);

/**
 * 把后端的 permCodes (["fabric:read", ...] 或 ["*"])
 * 转为前端期望的 Record<resource, actions[]> 格式。
 * admin 角色或 permCodes 含 "*" 返回 {"*": ["*"]}(由 authentication.ts 通配处理)。
 */
export function transformPermissions(
  permCodes: string[],
  roles: string[]
): Record<string, string[]> {
  // admin 通配
  if (roles.includes('admin') || permCodes.includes('*')) {
    return { '*': ['*'] };
  }
  const result: Record<string, string[]> = {};
  permCodes.forEach((code) => {
    // code 格式: "资源:动作" 或 "模块:资源:动作"
    const parts = code.split(':');
    if (parts.length >= 2) {
      // 最后一段是 action，前面拼起来是 resource
      const action = parts[parts.length - 1];
      const resource = parts.slice(0, -1).join(':');
      if (!result[resource]) {
        result[resource] = [];
      }
      result[resource].push(action);
    }
  });
  return result;
}

/**
 * 根路由 loader：在渲染前校验登录态 + 预取 userInfo → dispatch 到 RTK。
 * 取代旧 main.tsx 中的 useEffect + window.location 硬跳转(消除竞态/闪烁)。
 * 无 token 或获取用户信息失败时 throw redirect('/login')，由 Data Router 处理跳转。
 */
async function authLoader(): Promise<null> {
  if (!getAccessToken()) {
    throw redirect('/login');
  }
  try {
    const user = await getCurrentUser();
    const permissions = transformPermissions(user.permissions, user.roles);
    store.dispatch(
      setUserInfo({
        userInfo: { name: user.displayName, permissions },
        userLoading: false,
      })
    );
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
        path: 'business/customer',
        element: withSuspense(
          <RequirePermission resource="customer" actions={['read']}>
            <CustomerPage />
          </RequirePermission>
        ),
      },
      {
        path: 'business/material',
        element: withSuspense(
          <RequirePermission resource="material" actions={['read']}>
            <MaterialPage />
          </RequirePermission>
        ),
      },
      {
        path: 'business/process',
        element: withSuspense(
          <RequirePermission resource="process" actions={['read']}>
            <ProcessPage />
          </RequirePermission>
        ),
      },
      {
        path: 'business/equipment',
        element: withSuspense(
          <RequirePermission resource="equipment" actions={['read']}>
            <EquipmentPage />
          </RequirePermission>
        ),
      },
      // ── 设备模块表单页（设备类型 / 模板走独立页；设备走 Drawer）──
      {
        path: 'business/equipment/type/create',
        element: withSuspense(
          <RequirePermission resource="equipment-type" actions={['create']}>
            <EquipmentTypeFormPage />
          </RequirePermission>
        ),
      },
      {
        path: 'business/equipment/type/edit/:id',
        element: withSuspense(
          <RequirePermission resource="equipment-type" actions={['update']}>
            <EquipmentTypeFormPage />
          </RequirePermission>
        ),
      },
      {
        path: 'business/equipment/template/create',
        element: withSuspense(
          <RequirePermission resource="equipment-type" actions={['create']}>
            <EquipmentTemplateFormPage />
          </RequirePermission>
        ),
      },
      {
        path: 'business/equipment/template/edit/:id',
        element: withSuspense(
          <RequirePermission resource="equipment-type" actions={['update']}>
            <EquipmentTemplateFormPage />
          </RequirePermission>
        ),
      },
      {
        path: 'system/user',
        element: withSuspense(
          <RequirePermission resource="system:user" actions={['read']}>
            <UserPage />
          </RequirePermission>
        ),
      },
      {
        path: 'system/role',
        element: withSuspense(
          <RequirePermission resource="system:role" actions={['read']}>
            <RolePage />
          </RequirePermission>
        ),
      },
      {
        path: 'system/permission',
        element: withSuspense(
          <RequirePermission resource="system:role" actions={['read']}>
            <PermissionPage />
          </RequirePermission>
        ),
      },
      {
        path: 'master-data/numbering',
        element: withSuspense(
          <RequirePermission resource="system:numbering" actions={['read']}>
            <NumberingPage />
          </RequirePermission>
        ),
      },
      {
        path: 'system/operation-log',
        element: withSuspense(
          <RequirePermission resource="system:audit" actions={['read']}>
            <OperationLogPage />
          </RequirePermission>
        ),
      },
      {
        path: 'system/login-log',
        element: withSuspense(
          <RequirePermission resource="system:audit" actions={['read']}>
            <LoginLogPage />
          </RequirePermission>
        ),
      },
      {
        path: 'master-data/color',
        element: withSuspense(
          <RequirePermission resource="color" actions={['read']}>
            <ColorPage />
          </RequirePermission>
        ),
      },
      {
        path: 'master-data/unit',
        element: withSuspense(
          <RequirePermission resource="system:unit" actions={['read']}>
            <UnitPage />
          </RequirePermission>
        ),
      },
    ],
  },
  { path: '/login', element: <Login /> },
  { path: '*', element: <Forbidden /> },
]);
