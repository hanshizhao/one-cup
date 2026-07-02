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
const NumberingPage = lazy(() => import('@/pages/system/numbering'));

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
function transformPermissions(
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
        path: 'system/user',
        element: withSuspense(
          <RequirePermission resource="system:user" actions={['manage']}>
            <UserPage />
          </RequirePermission>
        ),
      },
      {
        path: 'system/role',
        element: withSuspense(
          <RequirePermission resource="system:role" actions={['manage']}>
            <RolePage />
          </RequirePermission>
        ),
      },
      { path: 'system/permission', element: withSuspense(<PermissionPage />) },
      {
        path: 'system/numbering',
        element: withSuspense(
          <RequirePermission resource="system:numbering" actions={['view']}>
            <NumberingPage />
          </RequirePermission>
        ),
      },
    ],
  },
  { path: '/login', element: <Login /> },
  { path: '*', element: <Forbidden /> },
]);
