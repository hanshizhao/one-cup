import request from './request';

// ── 类型 ──
export interface MaterialListItem {
  id: string;
  code: string;
  name: string;
  spec: string;
  category: string;
  unitId: string | null;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
}

export interface MaterialDetail extends MaterialListItem {
  remark?: string;
  updatedAt?: string;
}

export interface MaterialPagedResult {
  items: MaterialListItem[];
  total: number;
  page: number;
  pageSize: number;
}

export interface MaterialQuery {
  keyword?: string;
  category?: string;
  isActive?: boolean;
  page: number;
  pageSize: number;
}

export interface MaterialFormData {
  name: string;
  spec: string;
  category: string;
  unitId: string | null;
  remark?: string;
  sortOrder: number;
  categoryCode?: string;
}

export interface UpdateMaterialStatusRequest {
  isActive: boolean;
}

// ── API ──
// 注:request.ts 响应拦截器返回 response.data,故用双泛型 <unknown, T>
export function getMaterials(params: MaterialQuery) {
  return request.get<unknown, MaterialPagedResult>('/api/materials', { params });
}

export function getMaterial(id: string) {
  return request.get<unknown, MaterialDetail>(`/api/materials/${id}`);
}

export function createMaterial(data: MaterialFormData) {
  return request.post<unknown, MaterialDetail>('/api/materials', data);
}

export function updateMaterial(id: string, data: MaterialFormData) {
  return request.put<unknown, MaterialDetail>(`/api/materials/${id}`, data);
}

export function deleteMaterial(id: string) {
  return request.delete(`/api/materials/${id}`);
}

export function updateMaterialStatus(id: string, isActive: boolean) {
  return request.put(`/api/materials/${id}/status`, { isActive } satisfies UpdateMaterialStatusRequest);
}
