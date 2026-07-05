import request from './request';

// ── 类型 ──
export interface ProcessListItem {
  id: string;
  code: string;
  name: string;
  category?: string;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
}

export interface ProcessDetail extends ProcessListItem {
  remark?: string;
  updatedAt?: string;
}

export interface ProcessPagedResult {
  items: ProcessListItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ProcessQuery {
  keyword?: string;
  category?: string;
  isActive?: boolean;
  page: number;
  pageSize: number;
}

export interface ProcessFormData {
  name: string;
  category?: string;
  sortOrder: number;
  remark?: string;
  isActive: boolean;
}

// ── API ──
// 注：request.ts 响应拦截器返回 response.data，故使用双泛型 <unknown, T>
// 使 Promise 解析为 T（与 customer.ts / numbering.ts 一致）。
export function getProcesses(params: ProcessQuery) {
  return request.get<unknown, ProcessPagedResult>('/api/processes', { params });
}

export function getProcess(id: string) {
  return request.get<unknown, ProcessDetail>(`/api/processes/${id}`);
}

export function createProcess(data: ProcessFormData) {
  return request.post<unknown, ProcessDetail>('/api/processes', data);
}

export function updateProcess(id: string, data: ProcessFormData) {
  return request.put<unknown, ProcessDetail>(`/api/processes/${id}`, data);
}

export function deleteProcess(id: string) {
  return request.delete(`/api/processes/${id}`);
}
