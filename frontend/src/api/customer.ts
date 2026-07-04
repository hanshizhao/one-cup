import request from './request';

// ── 类型 ──
export interface CustomerListItem {
  id: string;
  code: string;
  name: string;
  shortName?: string;
  contactPerson?: string;
  contactPhone?: string;
  isActive: boolean;
  createdAt: string;
}

export interface CustomerDetail extends CustomerListItem {
  remark?: string;
  updatedAt?: string;
}

export interface CustomerPagedResult {
  items: CustomerListItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface CustomerQuery {
  keyword?: string;
  code?: string;
  isActive?: boolean;
  page: number;
  pageSize: number;
}

export interface CustomerFormData {
  name: string;
  shortName?: string;
  contactPerson?: string;
  contactPhone?: string;
  remark?: string;
  isActive: boolean;
}

// ── API ──
// 注：request.ts 响应拦截器返回 response.data，故使用双泛型 <unknown, T>
// 使 Promise 解析为 T（与 numbering.ts / role.ts / user.ts 一致）。
export function getCustomers(params: CustomerQuery) {
  return request.get<unknown, CustomerPagedResult>('/api/customers', { params });
}

export function getCustomer(id: string) {
  return request.get<unknown, CustomerDetail>(`/api/customers/${id}`);
}

export function createCustomer(data: CustomerFormData) {
  return request.post<unknown, CustomerDetail>('/api/customers', data);
}

export function updateCustomer(id: string, data: CustomerFormData) {
  return request.put<unknown, CustomerDetail>(`/api/customers/${id}`, data);
}

export function deleteCustomer(id: string) {
  return request.delete(`/api/customers/${id}`);
}
