import request from './request';

export interface UserListItem {
  id: string;
  username: string;
  displayName: string;
  email?: string;
  isActive: boolean;
  createdAt: string;
  roleNames: string[];
}

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface UserDetail {
  id: string;
  username: string;
  displayName: string;
  email?: string;
  isActive: boolean;
  createdAt: string;
  roleIds: string[];
  roleNames: string[];
}

export interface RoleOption {
  id: string;
  name: string;
  code: string;
}

export function getUserList(page: number, pageSize: number, keyword?: string) {
  return request.get<unknown, PagedResult<UserListItem>>('/api/users', {
    params: { page, pageSize, keyword },
  });
}

export function getUserById(id: string) {
  return request.get<unknown, UserDetail>(`/api/users/${id}`);
}

export function createUser(data: {
  username: string;
  displayName: string;
  email?: string;
  password: string;
  roleIds: string[];
}) {
  return request.post('/api/users', data);
}

export function updateUser(id: string, data: {
  displayName: string;
  email?: string;
  isActive: boolean;
  roleIds: string[];
}) {
  return request.put(`/api/users/${id}`, data);
}

export function resetPassword(id: string, newPassword: string) {
  return request.put(`/api/users/${id}/password`, { newPassword });
}

export function updateUserStatus(id: string, isActive: boolean) {
  return request.put(`/api/users/${id}/status`, { isActive });
}
