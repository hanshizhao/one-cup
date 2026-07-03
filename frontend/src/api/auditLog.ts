import request from './request';
import { PagedResult } from './user';

// ── 操作日志 ──
export interface OperationLogListItem {
  id: string;
  userId?: string;
  username: string;
  module: string;
  action: string;
  targetType?: string;
  targetId?: string;
  targetName?: string;
  result: 'Success' | 'Failed';
  statusCode: number;
  durationMs: number;
  createdAt: string;
}

export interface OperationLogDetail extends OperationLogListItem {
  httpMethod: string;
  requestPath: string;
  ipAddress?: string;
  userAgent?: string;
  requestPayload?: string;
  errorMessage?: string;
  stackTrace?: string;
  traceId?: string;
}

export interface OperationLogQuery {
  page?: number;
  pageSize?: number;
  startTime?: string;
  endTime?: string;
  userId?: string;
  username?: string;
  module?: string;
  action?: string;
  result?: 'Success' | 'Failed';
  keyword?: string;
}

export function getOperationLogs(params: OperationLogQuery) {
  return request.get<unknown, PagedResult<OperationLogListItem>>('/api/audit/operation-logs', { params });
}

export function getOperationLog(id: string) {
  return request.get<unknown, OperationLogDetail>(`/api/audit/operation-logs/${id}`);
}

// ── 登录日志 ──
export interface LoginLogItem {
  id: string;
  userId?: string;
  username: string;
  eventType: 'Login' | 'Logout' | 'Refresh' | 'Locked';
  result: 'Success' | 'Failed';
  ipAddress?: string;
  userAgent?: string;
  failureReason?: string;
  message?: string;
  createdAt: string;
}

export interface LoginLogQuery {
  page?: number;
  pageSize?: number;
  startTime?: string;
  endTime?: string;
  userId?: string;
  username?: string;
  eventType?: 'Login' | 'Logout' | 'Refresh' | 'Locked';
  result?: 'Success' | 'Failed';
  failureReason?: string;
}

export function getLoginLogs(params: LoginLogQuery) {
  return request.get<unknown, PagedResult<LoginLogItem>>('/api/audit/login-logs', { params });
}

export function getLoginLog(id: string) {
  return request.get<unknown, LoginLogItem>(`/api/audit/login-logs/${id}`);
}
