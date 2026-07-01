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
│   └── tests/
├── frontend/         # Arco Design Pro (Vite + React)
├── infra/            # 开发环境基础设施 (部署在云服务器)
│   ├── docker-compose.yml           # PostgreSQL
│   └── .env.example                 # 配置模板 (复制为 .env)
├── docs/
│   ├── specs/        # 设计文档
│   └── adr/          # 架构决策记录
└── .zcode/
    └── skills/       # 项目级 Skill
```

## 快速开始

### 前置环境

- .NET 10 SDK
- Node.js 18+
- Docker (部署在云服务器上,非本机)

### 数据库 (云服务器)

数据库部署在云服务器上,详见 [infra/README.md](infra/README.md)。

```bash
# 1. 在云服务器上部署 PostgreSQL
#    (参考 infra/README.md 的部署步骤)

# 2. 本机配置连接字符串 (使用 user-secrets, 密码不进 git)
cd backend/src/OneCup.Api
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=<云服务器IP>;Port=5432;Database=onecup;Username=onecup;Password=<密码>"
```

### 后端

```bash
cd backend
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
