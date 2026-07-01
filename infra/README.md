# 开发环境基础设施

数据库等服务部署在云服务器上,本机通过网络连接。

## 服务清单

| 服务 | 宿主机端口 | 容器端口 | 说明 |
|------|-----------|---------|------|
| PostgreSQL 17 | 15207 | 5432 | 主数据库 |
| pgAdmin 4 | 15672 | 80 | Web 版数据库管理界面 |

> 缓存 (Redis) 等其他服务待实际需要时再添加 (YAGNI)。

## 云服务器部署步骤

```bash
# 1. 将 infra/ 目录上传到云服务器
scp -r infra/ user@your-server:/opt/onecup/

# 2. 登录服务器, 进入目录
ssh user@your-server
cd /opt/onecup/infra

# 3. 创建 .env 文件, 填入真实配置
cp .env.example .env
vi .env   # 修改所有密码为强密码

# 4. 启动服务
docker compose up -d

# 5. 验证
docker compose ps
docker compose logs
```

## 安全配置 (必须做)

在云服务器上配置防火墙,限制端口访问来源:

```bash
# 仅允许你的开发机公网 IP 访问 (替换为你的真实 IP)
# Ubuntu/Debian (ufw):
sudo ufw allow from <你的公网IP> to any port 15207
sudo ufw allow from <你的公网IP> to any port 15672
sudo ufw deny 15207
sudo ufw deny 15672

# 或在云厂商控制台的安全组中配置入站规则
```

**切勿对 0.0.0.0/0 开放这些端口。**

## 使用 pgAdmin

部署完成后,浏览器访问:

```
http://<云服务器IP>:15672
```

使用 `.env` 中配置的 `PGADMIN_EMAIL` 和 `PGADMIN_PASSWORD` 登录。

添加服务器连接时:
- 主机名: `postgres` (容器间通过 Docker 网络通信)
- 端口: `5432` (容器内部端口,不是 15207)
- 用户名/密码: `.env` 中的 `POSTGRES_USER` / `POSTGRES_PASSWORD`

## 本地开发机连接

在云服务器部署完成后,配置后端连接字符串。

推荐使用 .NET user-secrets (密码不进 git):

```bash
cd backend/src/OneCup.Api

# 设置连接字符串 (注意端口是 15207)
dotnet user-secrets set "ConnectionStrings:DefaultConnection" \
  "Host=<云服务器IP>;Port=15207;Database=onecup;Username=onecup;Password=<你的密码>"
```

user-secrets 存储在用户目录下,与项目代码分离,不会被提交到 git。

## 常用命令

```bash
# 查看服务状态
docker compose ps

# 查看日志
docker compose logs -f postgres
docker compose logs -f pgadmin

# 重启某个服务
docker compose restart postgres

# 停止服务
docker compose down

# 停止并删除数据卷 (⚠️ 会丢失所有数据!)
docker compose down -v
```
