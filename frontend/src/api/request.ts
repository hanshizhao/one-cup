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

    // 其他错误：全局提示
    const message = error.response?.data?.message || error.message || '请求失败';
    if (status !== 401) {
      Message.error(message);
    }
    return Promise.reject(error);
  },
);

function redirectToLogin() {
  removeTokens();
  if (window.location.pathname !== '/login') {
    window.location.href = '/login';
  }
}

export default request;
