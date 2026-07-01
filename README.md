# OneCup — 印染厂面料开发管理系统

> 前后端分离的全栈项目,用于印染厂新面料开发管理。

## 技术栈

| 层面 | 技术 |
|------|------|
| 后端 | .NET 10 (LTS) + EF Core + PostgreSQL |
| 前端 | Arco Design Pro (Vite + React + TypeScript) |
| 认证 | JWT Token |
| 部署 | Linux + Docker (单体应用) |

## 项目结构

```
one-cup/
├── backend/          # .NET 解决方案 (四层架构)
│   ├── src/
│   │   ├── OneCup.Api/              # 控制器、中间件、启动配置
│   │   ├── OneCup.Application/      # 业务逻辑、DTO、服务接口
│   │   ├── OneCup.Domain/           # 领域模型 (零依赖)
│   │   └── OneCup.Infrastructure/   # EF Core、数据库访问
│   ├── tests/
│   └── docker-compose.dev.yml       # 本地 PostgreSQL
├── frontend/         # Arco Design Pro (Vite + React)
├── docs/
│   └── specs/        # 设计文档
└── .zcode/
    └── skills/       # 项目级 Skill (Arco Design)
```

## 快速开始

### 前置环境

- .NET 10 SDK
- Node.js 18+
- Docker (用于本地 PostgreSQL)

### 后端

```bash
cd backend

# 1. 启动本地 PostgreSQL
docker compose -f docker-compose.dev.yml up -d

# 2. 运行 (开发环境)
dotnet run --project src/OneCup.Api
```

后端默认运行在 `http://localhost:5000`。

### 前端

```bash
cd frontend
npm install
npm run dev
```

前端默认运行在 `http://localhost:5173`。

## 设计文档

- [技术架构设计](docs/specs/2026-07-01-tech-stack-design.md)
