import request from './request';
import { PagedResult } from './user';

// ── 类型 ──
export interface MeasurementUnit {
  id: string;
  code: string;
  nameZh: string;
  nameEn: string;
  symbol: string;
  category: string;
  isBase: boolean;
  factor: number;
  precision: number;
  sortOrder: number;
  isActive: boolean;
  createdAt: string;
  updatedAt?: string;
}

export interface CreateUnitRequest {
  code: string;
  nameZh: string;
  nameEn: string;
  symbol: string;
  category: string;
  isBase: boolean;
  factor: number;
  precision: number;
  sortOrder: number;
}

export interface UpdateUnitRequest {
  nameZh?: string;
  nameEn?: string;
  symbol?: string;
  isBase?: boolean;
  factor?: number;
  precision?: number;
  sortOrder?: number;
}

export interface ConvertResult {
  quantity: number;
  fromCode: string;
  toCode: string;
  precision: number;
}

// ── API ──
export function getUnits(params: {
  page?: number;
  pageSize?: number;
  keyword?: string;
  category?: string;
  isActive?: boolean;
}) {
  return request.get<unknown, PagedResult<MeasurementUnit>>(
    '/api/measurement-units',
    { params },
  );
}

export function getAllActiveUnits() {
  return request.get<unknown, MeasurementUnit[]>(
    '/api/measurement-units/all',
  );
}

export function getUnitCategories() {
  return request.get<unknown, string[]>(
    '/api/measurement-units/categories',
  );
}

export function getUnit(id: string) {
  return request.get<unknown, MeasurementUnit>(
    `/api/measurement-units/${id}`,
  );
}

export function createUnit(data: CreateUnitRequest) {
  return request.post<unknown, MeasurementUnit>(
    '/api/measurement-units',
    data,
  );
}

export function updateUnit(id: string, data: UpdateUnitRequest) {
  return request.put(`/api/measurement-units/${id}`, data);
}

export function updateUnitStatus(id: string, isActive: boolean) {
  return request.put(`/api/measurement-units/${id}/status`, { isActive });
}

export function convertUnit(data: {
  fromCode: string;
  toCode: string;
  quantity: number;
}) {
  return request.post<unknown, ConvertResult>(
    '/api/measurement-units/convert',
    data,
  );
}
