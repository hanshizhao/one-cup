import request from './request';
import { PagedResult } from './user';

// ── 类型 ──
export interface NumberingRuleListItem {
  id: string;
  targetType: string;
  name: string;
  prefix: string;
  sampleFormat: string;
  isActive: boolean;
  createdAt: string;
}

export interface NumberingRule extends NumberingRuleListItem {
  includeCategory: boolean;
  dateSegment: string;
  seqLength: number;
  separator: string;
  resetPeriod: string;
  remark?: string;
  updatedAt?: string;
}

export interface CreateNumberingRuleRequest {
  targetType: string;
  name: string;
  prefix: string;
  includeCategory: boolean;
  dateSegment: string;
  seqLength: number;
  separator: string;
  resetPeriod: string;
  remark?: string;
}

export interface NumberingLogItem {
  id: string;
  generatedCode: string;
  targetType: string;
  categoryCode?: string;
  periodKey?: string;
  seqValue: number;
  createdAt: string;
  ruleName?: string;
}

// ── 规则 ──
export function getNumberingRules(params: {
  page?: number; pageSize?: number; keyword?: string;
  targetType?: string; isActive?: boolean;
}) {
  return request.get<unknown, PagedResult<NumberingRuleListItem>>('/api/numbering/rules', { params });
}

export function getNumberingRule(id: string) {
  return request.get<unknown, NumberingRule>(`/api/numbering/rules/${id}`);
}

export function createNumberingRule(data: CreateNumberingRuleRequest) {
  return request.post<unknown, NumberingRule>('/api/numbering/rules', data);
}

export function updateNumberingRule(id: string, data: Partial<CreateNumberingRuleRequest> & { remark?: string }) {
  return request.put(`/api/numbering/rules/${id}`, data);
}

export function updateNumberingRuleStatus(id: string, isActive: boolean) {
  return request.put(`/api/numbering/rules/${id}/status`, { isActive });
}

// ── 预览 ──
export function previewCode(targetType: string, categoryCode?: string) {
  return request.get<unknown, { code: string | null; note: string }>('/api/numbering/preview', {
    params: { targetType, categoryCode },
  });
}

// ── 日志 ──
export function getNumberingLogs(params: {
  page?: number; pageSize?: number; targetType?: string;
  categoryCode?: string; ruleId?: string; code?: string;
  startDate?: string; endDate?: string;
}) {
  return request.get<unknown, PagedResult<NumberingLogItem>>('/api/numbering/logs', { params });
}
