import request from './request';
import { PagedResult } from './user';

// ── 类型 ──
export interface TargetType {
  id: string;
  code: string;
  nameZh: string;
  nameEn: string;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface Category {
  id: string;
  targetTypeCode: string;
  code: string;
  nameZh: string;
  nameEn: string;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateTargetTypeRequest {
  code: string;
  nameZh: string;
  nameEn: string;
  sortOrder: number;
}

export interface UpdateTargetTypeRequest {
  nameZh?: string;
  nameEn?: string;
  sortOrder?: number;
}

export interface CreateCategoryRequest {
  targetTypeCode: string;
  code: string;
  nameZh: string;
  nameEn: string;
  sortOrder: number;
}

export interface UpdateCategoryRequest {
  nameZh?: string;
  nameEn?: string;
  sortOrder?: number;
}

// ── 业务类型 ──
export function getTargetTypes(params: {
  page?: number; pageSize?: number; keyword?: string; isActive?: boolean;
}) {
  return request.get<unknown, PagedResult<TargetType>>('/api/numbering/dict/target-types', { params });
}

export function getAllActiveTargetTypes() {
  return request.get<unknown, TargetType[]>('/api/numbering/dict/target-types/all');
}

export function getTargetType(id: string) {
  return request.get<unknown, TargetType>(`/api/numbering/dict/target-types/${id}`);
}

export function createTargetType(data: CreateTargetTypeRequest) {
  return request.post<unknown, TargetType>('/api/numbering/dict/target-types', data);
}

export function updateTargetType(id: string, data: UpdateTargetTypeRequest) {
  return request.put(`/api/numbering/dict/target-types/${id}`, data);
}

export function updateTargetTypeStatus(id: string, isActive: boolean) {
  return request.put(`/api/numbering/dict/target-types/${id}/status`, { isActive });
}

// ── 分类 ──
export function getCategories(params: {
  page?: number; pageSize?: number; targetTypeCode?: string;
  keyword?: string; isActive?: boolean;
}) {
  return request.get<unknown, PagedResult<Category>>('/api/numbering/dict/categories', { params });
}

export function getActiveCategories(targetTypeCode: string) {
  return request.get<unknown, Category[]>('/api/numbering/dict/categories/all', {
    params: { targetTypeCode },
  });
}

export function getCategory(id: string) {
  return request.get<unknown, Category>(`/api/numbering/dict/categories/${id}`);
}

export function createCategory(data: CreateCategoryRequest) {
  return request.post<unknown, Category>('/api/numbering/dict/categories', data);
}

export function updateCategory(id: string, data: UpdateCategoryRequest) {
  return request.put(`/api/numbering/dict/categories/${id}`, data);
}

export function updateCategoryStatus(id: string, isActive: boolean) {
  return request.put(`/api/numbering/dict/categories/${id}/status`, { isActive });
}
