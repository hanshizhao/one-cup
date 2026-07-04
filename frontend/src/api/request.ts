import axios from 'axios';
import { Message } from '@arco-design/web-react';
import {
  getAccessToken,
  getRefreshToken,
  setTokens,
  removeTokens,
} from '@/utils/token';

const request = axios.create({
  baseURL: import.meta.env.VITE_API_BASE_URL || '',
  timeout: 15000,
});

// 不需要 token 的接口
const WHITE_LIST = ['/api/auth/login', '/api/auth/refresh'];

// 并发 refresh 防抖
let isRefreshing = false;
let pendingQueue: Array<(token: string) => void> = [];

// ── 请求拦截器：自动注入 token ──
request.interceptors.request.use((config) => {
  const url = config.url || '';
  if (!WHITE_LIST.some((p) => url.startsWith(p))) {
    const token = getAccessToken();
    if (token) {
      config.headers.Authorization = `Bearer ${token}`;
    }
  }
  return config;
});

// ── 响应拦截器：401 自动刷新 ──
request.interceptors.response.use(
  (response) => response.data,
  async (error) => {
    const originalRequest = error.config;
    const status = error.response?.status;

    // 401 且非刷新接口 且未重试过 → 尝试刷新
    if (
      status === 401 &&
      originalRequest &&
      !originalRequest._retried &&
      !WHITE_LIST.some((p: string) => originalRequest.url.startsWith(p))
    ) {
      if (isRefreshing) {
        // 已有刷新在进行中，排队等待
        return new Promise((resolve, reject) => {
          pendingQueue.push((token: string) => {
            if (token) {
              originalRequest._retried = true;
              originalRequest.headers.Authorization = `Bearer ${token}`;
              resolve(request(originalRequest));
            } else {
              reject(error);
            }
          });
        });
      }

      const refreshToken = getRefreshToken();
      if (!refreshToken) {
        redirectToLogin();
        return Promise.reject(error);
      }

      isRefreshing = true;
      try {
        const res = await axios.post(
          `${import.meta.env.VITE_API_BASE_URL || ''}/api/auth/refresh`,
          { refreshToken },
        );
        const { accessToken, refreshToken: newRefresh } = res.data;
        setTokens(accessToken, newRefresh);

        // 重放排队请求
        pendingQueue.forEach((cb) => cb(accessToken));
        pendingQueue = [];

        // 重放原请求
        originalRequest._retried = true;
        originalRequest.headers.Authorization = `Bearer ${accessToken}`;
        return request(originalRequest);
      } catch (refreshError) {
        pendingQueue = [];
        redirectToLogin();
        return Promise.reject(refreshError);
      } finally {
        isRefreshing = false;
      }
    }

    // 其他错误：全局提示（按状态码映射普通人能看懂的文案）
    // 优先使用后端返回的业务 message（如校验错误"角色编码只能含小写字母"），
    // 后端无 message 时按状态码兜底，避免暴露 "Request failed with status code 403" 这种程序员文案。
    if (status !== 401) {
      const backendMessage = error.response?.data?.message;
      const friendly = backendMessage || statusToFriendlyMessage(status, error);
      Message.error(friendly);
    }
    return Promise.reject(error);
  },
);

/**
 * 按 HTTP 状态码返回普通人能看懂的错误文案。
 * 后端未提供业务 message 时兜底使用，避免暴露原始 "Request failed with status code 403" 之类的技术文案。
 */
function statusToFriendlyMessage(status: number, error: unknown): string {
  switch (status) {
    case 400:
      return '请求参数有误，请检查输入后重试';
    case 403:
      return '您没有该操作的权限';
    case 404:
      return '请求的资源不存在';
    case 408:
      return '请求超时，请稍后重试';
    case 409:
      return '操作冲突，数据可能已被他人修改，请刷新后重试';
    case 429:
      return '操作过于频繁，请稍后再试';
    default:
      if (status >= 500) return '服务器开小差了，请稍后重试';
      // 网络错误（无 response）或未知状态码
      return (error as { message?: string })?.message || '网络异常，请稍后重试';
  }
}

function redirectToLogin() {
  removeTokens();
  if (window.location.pathname !== '/login') {
    window.location.href = '/login';
  }
}

export default request;
