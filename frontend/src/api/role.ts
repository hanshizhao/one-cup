import request from './request';

export interface RoleListItem {
  id: string;
  name: string;
  code: string;
  description?: string;
  createdAt: string;
  userCount: number;
  permissionCount: number;
}

export interface RoleDetail {
  id: string;
  name: string;
  code: string;
  description?: string;
  createdAt: string;
  permissionIds: string[];
}

export function getRoleList() {
  return request.get<unknown, RoleListItem[]>('/api/roles');
}

export function getRoleById(id: string) {
  return request.get<unknown, RoleDetail>(`/api/roles/${id}`);
}

export function createRole(data: { name: string; code: string; description?: string }) {
  return request.post('/api/roles', data);
}

export function updateRole(id: string, data: { name: string; description?: string; permissionIds: string[] }) {
  return request.put(`/api/roles/${id}`, data);
}

export function deleteRole(id: string) {
  return request.delete(`/api/roles/${id}`);
}
