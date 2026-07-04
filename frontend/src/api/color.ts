import request from './request';
import { PagedResult } from './user';

// ── 类型 ──
export interface Color {
  id: string;
  code: string;
  nameZh: string;
  nameEn: string;
  hex: string;
  colorFamily: string;
  remark?: string;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateColorRequest {
  code: string;
  nameZh: string;
  nameEn: string;
  hex: string;
  colorFamily: string;
  remark?: string;
  sortOrder: number;
}

export interface UpdateColorRequest {
  nameZh?: string;
  nameEn?: string;
  hex?: string;
  colorFamily?: string;
  remark?: string;
  sortOrder?: number;
}

// ── 请求函数 ──
export function getColors(params: {
  page?: number;
  pageSize?: number;
  keyword?: string;
  colorFamily?: string;
  isActive?: boolean;
}) {
  return request.get<unknown, PagedResult<Color>>('/api/colors', { params });
}

export function getAllActiveColors() {
  return request.get<unknown, Color[]>('/api/colors/all');
}

export function getColor(id: string) {
  return request.get<unknown, Color>(`/api/colors/${id}`);
}

export function createColor(data: CreateColorRequest) {
  return request.post<unknown, Color>('/api/colors', data);
}

export function updateColor(id: string, data: UpdateColorRequest) {
  return request.put(`/api/colors/${id}`, data);
}

export function updateColorStatus(id: string, isActive: boolean) {
  return request.put(`/api/colors/${id}/status`, { isActive });
}
