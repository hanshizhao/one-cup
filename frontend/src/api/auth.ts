import request from './request';

export interface TokenResponse {
  accessToken: string;
  refreshToken: string;
  expiresIn: number;
}

export interface CurrentUser {
  id: string;
  username: string;
  displayName: string;
  roles: string[];
  permissions: string[];
}

export function login(username: string, password: string) {
  return request.post<unknown, TokenResponse>('/api/auth/login', {
    username,
    password,
  });
}

export function refreshToken(refreshToken: string) {
  return request.post<unknown, TokenResponse>('/api/auth/refresh', {
    refreshToken,
  });
}

export function logout() {
  return request.post('/api/auth/logout');
}

export function getCurrentUser() {
  return request.get<unknown, CurrentUser>('/api/auth/me');
}
