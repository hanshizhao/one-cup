import request from './request';

// ═══════════════════════════════════════════
// 类型定义（对齐后端 DTO）
// ═══════════════════════════════════════════

export const PARAMETER_VALUE_TYPES = ['Number', 'Text', 'Enum'] as const;
export type ParameterValueType = typeof PARAMETER_VALUE_TYPES[number];

export const EQUIPMENT_STATUSES = ['Running', 'Stopped', 'Maintenance'] as const;
export type EquipmentStatus = typeof EQUIPMENT_STATUSES[number];

export interface ParameterDefinitionDto {
  id?: string;
  name: string;
  valueType: ParameterValueType;
  unitId?: string;
  minValue?: string;
  maxValue?: string;
  precision?: number;
  options?: string[];
  required: boolean;
  sortOrder: number;
  remark?: string;
}

export interface EquipmentTypeParameterDto extends ParameterDefinitionDto {
  id: string;
  unitSymbol?: string;
}

export interface EquipmentTypeListItemDto {
  id: string;
  code: string;
  name: string;
  parameterCount: number;
  templateCount: number;
  isActive: boolean;
  createdAt: string;
}

export interface EquipmentTemplateSummaryDto {
  id: string;
  name: string;
  processId: string;
  processName: string;
  status?: string;
  sortOrder: number;
}

export interface EquipmentTypeDto extends EquipmentTypeListItemDto {
  remark?: string;
  updatedAt?: string;
  parameters: EquipmentTypeParameterDto[];
  templates: EquipmentTemplateSummaryDto[];
}

export interface CreateEquipmentTypeRequest {
  name: string;
  remark?: string;
  isActive: boolean;
  sortOrder: number;
  categoryCode?: string;
  parameters: ParameterDefinitionDto[];
}

export type UpdateEquipmentTypeRequest = Omit<CreateEquipmentTypeRequest, 'categoryCode'>;

// ── 模板 ──

export interface TemplateValueDto {
  parameterId: string;
  value?: string;
}

export interface EquipmentTemplateValueDto extends TemplateValueDto {
  parameterName: string;
  valueType: ParameterValueType;
  unitSymbol?: string;
  status: string;
  statusMessage?: string;
}

export interface EquipmentTemplateListItemDto {
  id: string;
  name: string;
  processId: string;
  processName: string;
  status?: string;
  statusMessage?: string;
  sortOrder: number;
  createdAt: string;
}

export interface EquipmentTemplateDto extends EquipmentTemplateListItemDto {
  remark?: string;
  updatedAt?: string;
  values: EquipmentTemplateValueDto[];
}

export interface CreateEquipmentTemplateRequest {
  name: string;
  processId: string;
  remark?: string;
  sortOrder: number;
  values: TemplateValueDto[];
}

export type UpdateEquipmentTemplateRequest = CreateEquipmentTemplateRequest;

// ── 设备实例 ──

export interface EquipmentListItemDto {
  id: string;
  code: string;
  name: string;
  equipmentTypeId: string;
  equipmentTypeName: string;
  specification?: string;
  supplier?: string;
  location?: string;
  status: EquipmentStatus;
  isActive: boolean;
  createdAt: string;
}

export interface EquipmentDto extends EquipmentListItemDto {
  purchaseDate?: string;
  warrantyExpiry?: string;
  remark?: string;
  updatedAt?: string;
}

export interface CreateEquipmentRequest {
  name: string;
  equipmentTypeId: string;
  specification?: string;
  supplier?: string;
  location?: string;
  status: EquipmentStatus;
  purchaseDate?: string;
  warrantyExpiry?: string;
  remark?: string;
  isActive: boolean;
  sortOrder: number;
  categoryCode?: string;
}

export type UpdateEquipmentRequest = Omit<CreateEquipmentRequest, 'categoryCode'>;

export interface PagedResult<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

// ═══════════════════════════════════════════
// API 函数
// ═══════════════════════════════════════════

// ── 设备类型 ──
export const getEquipmentTypes = (params: {
  keyword?: string; code?: string; isActive?: boolean; page?: number; pageSize?: number;
}) => request.get<unknown, PagedResult<EquipmentTypeListItemDto>>('/api/equipment-types', { params });

export const getActiveEquipmentTypes = () =>
  request.get<unknown, EquipmentTypeListItemDto[]>('/api/equipment-types/active');

export const getEquipmentTypeById = (id: string) =>
  request.get<unknown, EquipmentTypeDto>(`/api/equipment-types/${id}`);

export const createEquipmentType = (data: CreateEquipmentTypeRequest) =>
  request.post<unknown, EquipmentTypeDto>('/api/equipment-types', data);

export const updateEquipmentType = (id: string, data: UpdateEquipmentTypeRequest) =>
  request.put<unknown, EquipmentTypeDto>(`/api/equipment-types/${id}`, data);

export const deleteEquipmentType = (id: string) =>
  request.delete<unknown, void>(`/api/equipment-types/${id}`);

// ── 运行模板 ──
export const getEquipmentTemplates = (typeId: string, processId?: string) =>
  request.get<unknown, EquipmentTemplateListItemDto[]>(`/api/equipment-types/${typeId}/templates`, {
    params: processId ? { processId } : undefined,
  });

export const getEquipmentTemplateById = (typeId: string, id: string) =>
  request.get<unknown, EquipmentTemplateDto>(`/api/equipment-types/${typeId}/templates/${id}`);

export const createEquipmentTemplate = (typeId: string, data: CreateEquipmentTemplateRequest) =>
  request.post<unknown, EquipmentTemplateDto>(`/api/equipment-types/${typeId}/templates`, data);

export const updateEquipmentTemplate = (typeId: string, id: string, data: UpdateEquipmentTemplateRequest) =>
  request.put<unknown, EquipmentTemplateDto>(`/api/equipment-types/${typeId}/templates/${id}`, data);

export const deleteEquipmentTemplate = (typeId: string, id: string) =>
  request.delete<unknown, void>(`/api/equipment-types/${typeId}/templates/${id}`);

// ── 设备实例 ──
export const getEquipments = (params: {
  keyword?: string; code?: string; typeId?: string; isActive?: boolean; status?: EquipmentStatus;
  page?: number; pageSize?: number;
}) => request.get<unknown, PagedResult<EquipmentListItemDto>>('/api/equipment', { params });

export const getEquipmentById = (id: string) =>
  request.get<unknown, EquipmentDto>(`/api/equipment/${id}`);

export const createEquipment = (data: CreateEquipmentRequest) =>
  request.post<unknown, EquipmentDto>('/api/equipment', data);

export const updateEquipment = (id: string, data: UpdateEquipmentRequest) =>
  request.put<unknown, EquipmentDto>(`/api/equipment/${id}`, data);

export const deleteEquipment = (id: string) =>
  request.delete<unknown, void>(`/api/equipment/${id}`);
