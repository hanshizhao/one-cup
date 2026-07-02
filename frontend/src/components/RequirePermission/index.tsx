import React from 'react';
import { useAppSelector } from '@/store';
import auth from '@/utils/authentication';
import Forbidden from '@/pages/exception/403';

interface RequirePermissionProps {
  resource: string;
  actions?: string[];
  children: React.ReactNode;
}

/**
 * 路由 element 权限包装：无权限渲染 Forbidden，有权限渲染 children。
 * 作为菜单过滤(useRoute)之外的独立防线，拦截通过 URL 直接访问业务页的情况。
 */
export default function RequirePermission({
  resource,
  actions = ['manage'],
  children,
}: RequirePermissionProps) {
  const { userInfo } = useAppSelector((state) => state.userInfo);
  const permissions = userInfo?.permissions ?? {};
  // admin 通配
  if (permissions['*']?.includes('*')) {
    return <>{children}</>;
  }
  const allowed = auth({ requiredPermissions: [{ resource, actions }] }, permissions);
  return allowed ? <>{children}</> : <Forbidden />;
}
