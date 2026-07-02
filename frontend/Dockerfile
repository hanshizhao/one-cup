# ─────────────────────────────────────────────────────────────
# OneCup 前端 Dockerfile
# build context = 仓库根 (compose 里设 context: ..)
# 多阶段构建：node 构建 Vite 产物 → nginx 托管静态文件
# ─────────────────────────────────────────────────────────────

# ── 构建阶段 ──
FROM node:20-alpine AS build
WORKDIR /app

# 先拷依赖清单利用 Docker 缓存
COPY frontend/package.json frontend/package-lock.json* ./
RUN npm ci

# 拷源码并构建
COPY frontend/ .
# 构建时注入 API 基址（生产走 nginx 反代，相对路径 /api）
ARG VITE_API_BASE_URL=/api
ENV VITE_API_BASE_URL=$VITE_API_BASE_URL
RUN npm run build

# ── 运行阶段 ──
FROM nginx:alpine
# Vite 默认产物在 dist/
COPY --from=build /app/dist /usr/share/nginx/html
COPY frontend/nginx.conf /etc/nginx/conf.d/default.conf
EXPOSE 80
