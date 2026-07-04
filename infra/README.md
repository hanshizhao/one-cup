# OneCup 基础设施与部署

数据库、后端、前端等服务部署在云服务器上。

## 服务清单（全栈部署）

| 服务 | 宿主机端口 | 容器端口 | 说明 |
|------|-----------|---------|------|
| PostgreSQL 17 | （默认不暴露） | 5432 | 主数据库（仅 compose 内部网络可达） |
| pgAdmin 4 | 15672 | 80 | Web 版数据库管理界面（可选，仅运维用） |
| 后端 .NET API | 5000 | 5000 | REST API（启动时自动迁移数据库） |
| 前端 nginx | 8080 | 80 | 静态文件托管 + API 反向代理 |

> 前端经 nginx 反代访问 API，对外只需暴露前端端口（8080）。API 和数据库可不直接对公网开放。

### 健康检查

后端暴露 `/health` 端点供容器编排与外部监控探活：

| 端点 | 鉴权 | 说明 |
|------|------|------|
| `GET /health` | 公开 | 返回 `200`（Healthy）或 `503`（Unhealthy）。docker-compose 用它做容器健康检查。 |
| `GET /health/details` | 仅开发环境 | 返回每项检查的详细 JSON（名称、状态、耗时、异常）。生产不暴露。 |

- 后端容器启动后，docker-compose 会持续探测 `/health`：EF 迁移完成且数据库连通后才标记为 `healthy`，期间不会把流量转发过来（frontend `depends_on: backend(service_healthy)`）。
- 探活含 PostgreSQL 连接检查（执行 `SELECT 1`），能发现"进程活着但 DB 连不上"的故障。
- 本地开发联调时访问 `http://localhost:5000/health/details` 可看详细诊断信息。

---

## 一、全栈部署（数据库 + 后端 + 前端）

### 部署步骤

```bash
# 1. 将整个项目仓库上传到云服务器（或 git clone）
scp -r . user@your-server:/opt/onecup/
# 或: ssh user@your-server 'git clone <repo> /opt/onecup'

# 2. 登录服务器，进入 infra 目录
ssh user@your-server
cd /opt/onecup/infra

# 3. 创建 .env 文件，填入真实配置
cp .env.example .env
vi .env   # 设置 POSTGRES_PASSWORD / JWT_SECRET / 等

# 4. 一键启动全栈（构建镜像 + 启动容器）
docker compose up -d --build

# 5. 验证
docker compose ps
docker compose logs -f backend    # 看后端启动 + 迁移日志
```

### 访问地址

- **前端**：`http://<服务器IP>:8080`
- **API**：`http://<服务器IP>:8080/api/`（经 nginx 反代，与前端同源）
- **pgAdmin**：`http://<服务器IP>:15672`

### 自动迁移说明

后端容器启动时会自动应用 EF Core 迁移（`Database__MigrateOnStartup=true`）。
- 后端容器 `depends_on: postgres (service_healthy)`，确保数据库 ready 后才启动
- 迁移是幂等的，重复启动不会报错
- 查看迁移日志：`docker compose logs backend | grep 迁移`

### 从开发机直连数据库（可选）

默认数据库端口不对公网暴露。如需从开发机直连，编辑 `docker-compose.yml` 取消 `postgres.ports` 注释，并配置防火墙白名单。

---

## 二、仅数据库部署（开发场景）

若只需在云上部署数据库、应用在本地开发运行：

```bash
# 上传 infra 目录 + 配置 .env 后, 只启动数据库相关服务
docker compose up -d postgres pgadmin
```

然后在本机配置后端连接字符串（见下方"本地开发机连接"）。

---

## 安全配置（必须做）

在云服务器上配置防火墙，限制端口访问来源：

```bash
# 仅允许你的开发机公网 IP 访问 pgAdmin（替换为你的真实 IP）
# Ubuntu/Debian (ufw):
sudo ufw allow from <你的公网IP> to any port 15672
sudo ufw deny 15672

# 前端端口按需开放（对用户开放则 allow any）：
sudo ufw allow 8080

# 或在云厂商控制台的安全组中配置入站规则
```

**生产建议**：在前方再加一层反向代理（nginx/Caddy）处理 HTTPS 证书，仅暴露 443 端口。

---

## 使用 pgAdmin

浏览器访问 `http://<云服务器IP>:15672`，用 `.env` 中的 `PGADMIN_EMAIL` / `PGADMIN_PASSWORD` 登录。

添加服务器连接时：
- 主机名：`postgres`（容器间通过 Docker 网络通信）
- 端口：`5432`（容器内部端口）
- 用户名/密码：`.env` 中的 `POSTGRES_USER` / `POSTGRES_PASSWORD`

---

## 本地开发机连接

在云服务器部署数据库后，本机开发时配置后端连接字符串。

推荐使用 .NET user-secrets（密码不进 git）：

```bash
cd backend/src/OneCup.Api

# 设置连接字符串（需先在 docker-compose.yml 取消 postgres.ports 注释以暴露端口）
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=<云服务器IP>;Port=15207;Database=onecup;Username=onecup;Password=<你的密码>"
```

开发环境下 `appsettings.Development.json` 已配置 `Database:MigrateOnStartup=true`，
本机 `dotnet run` 启动时会自动应用迁移，无需手动执行 `dotnet ef database update`。

user-secrets 存储在用户目录下，与项目代码分离，不会被提交到 git。

---

## 常用命令

```bash
# 查看服务状态
docker compose ps

# 查看日志
docker compose logs -f backend     # 后端（含迁移日志）
docker compose logs -f frontend    # 前端 nginx
docker compose logs -f postgres    # 数据库

# 重新构建并启动（代码更新后）
docker compose up -d --build

# 重启某个服务
docker compose restart backend

# 停止服务
docker compose down

# 停止并删除数据卷（⚠️ 会丢失所有数据！）
docker compose down -v
```
