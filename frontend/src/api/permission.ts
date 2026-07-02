import request from './request';

export interface PermissionItem {
  id: string;
  code: string;
  name: string;
  description?: string;
}

export function getPermissionList() {
  return request.get<unknown, PermissionItem[]>('/api/permissions');
}
