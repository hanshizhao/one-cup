import auth, { AuthParams, UserPermission } from '@/utils/authentication';
import { useEffect, useMemo, useState } from 'react';

export type IRoute = AuthParams & {
  name: string;
  key: string;
  // 当前页是否展示面包屑
  breadcrumb?: boolean;
  children?: IRoute[];
  // 当前路由是否渲染菜单项，为 true 的话不会在菜单中显示，但可通过路由地址访问。
  ignore?: boolean;
  // 路由路径
  path?: string;
};

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
          { resource: 'system:numbering', actions: ['read'] },
        ],
      },
      {
        name: 'menu.masterData.unit',
        key: 'master-data/unit',
        requiredPermissions: [
          { resource: 'system:unit', actions: ['read'] },
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
          { resource: 'system:user', actions: ['read'] },
        ],
      },
      {
        name: 'menu.system.role',
        key: 'system/role',
        requiredPermissions: [
          { resource: 'system:role', actions: ['read'] },
        ],
      },
      {
        name: 'menu.system.permission',
        key: 'system/permission',
        requiredPermissions: [
          { resource: 'system:role', actions: ['read'] },
        ],
      },
      {
        name: 'menu.system.operationLog',
        key: 'system/operation-log',
        requiredPermissions: [
          { resource: 'system:audit', actions: ['read'] },
        ],
      },
      {
        name: 'menu.system.loginLog',
        key: 'system/login-log',
        requiredPermissions: [
          { resource: 'system:audit', actions: ['read'] },
        ],
      },
    ],
  },
];

const useRoute = (userPermission: UserPermission): [IRoute[], string] => {
  const filterRoute = (routes: IRoute[], arr: IRoute[] = []): IRoute[] => {
    if (!routes.length) {
      return [];
    }
    for (const route of routes) {
      const { requiredPermissions, oneOfPerm } = route;
      let visible = true;
      if (requiredPermissions) {
        visible = auth({ requiredPermissions, oneOfPerm }, userPermission);
      }

      if (!visible) {
        continue;
      }
      if (route.children && route.children.length) {
        const newRoute = { ...route, children: [] };
        filterRoute(route.children, newRoute.children);
        if (newRoute.children.length) {
          arr.push(newRoute);
        }
      } else {
        arr.push({ ...route });
      }
    }

    return arr;
  };

  const [permissionRoute, setPermissionRoute] = useState(routes);

  useEffect(() => {
    const newRoutes = filterRoute(routes);
    setPermissionRoute(newRoutes);
  }, [JSON.stringify(userPermission)]);

  const defaultRoute = useMemo(() => {
    const first = permissionRoute[0];
    if (first) {
      const firstRoute = first?.children?.[0]?.key || first.key;
      return firstRoute;
    }
    return '';
  }, [permissionRoute]);

  return [permissionRoute, defaultRoute];
};

export default useRoute;
