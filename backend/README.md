# OneCup 后端

.NET 10 四层架构（Clean Architecture）：Domain → Application → Infrastructure → Api。

## 运行

```bash
cd backend
dotnet run --project src/OneCup.Api
```

默认监听 `http://localhost:5000`。数据库连接串用 user-secrets 配置（见根 README）。

## 测试

测试分两类，依赖不同：

| 类别 | 数量 | 依赖 | 本地怎么跑 |
|------|------|------|-----------|
| 常规单元测试 | ~178 | 无（纯逻辑 / EF Core InMemory） | `dotnet test` 默认全跑 |
| 编号并发测试 | 14 | 真实 PostgreSQL（测 `FOR UPDATE` 行锁） | 需额外配置，见下 |

### 只跑常规单元测试（无需任何外部依赖）

本地没装 Docker、也不想连云库时，跳过并发测试：

```bash
dotnet test --filter "FullyQualifiedName!~ConcurrencyTests"
```

### 跑含并发测试的全量测试

并发测试（`NumberingServiceConcurrencyTests`）必须用真实 PostgreSQL——InMemory 不支持行锁，测不出并发正确性。两种本地运行方式：

#### 方式 A：Testcontainers（需本机 Docker）

什么都不用配。测试会自动起一个临时 PostgreSQL 容器跑完即销毁。
前提：本机 Docker Desktop（或 Docker Engine）正在运行。

```bash
dotnet test   # 直接跑,Docker 开着就行
```

> CI（GitHub Actions 的 ubuntu runner）天然有 Docker,走的就是这条路径,所以 CI 上这 14 个测试全绿。

#### 方式 B：指向云上独立测试库（无需本机 Docker）

适合本机不想开 Docker、但有云服务器 PostgreSQL 的场景。

**第 1 步：在云上 PG 建独立测试库**（只需一次）

连上你的云 PostgreSQL,执行:

```sql
CREATE DATABASE numbering_test;
```

> ⚠️ **必须是独立库,库名固定 `numbering_test`**。
> 测试每个方法都会 DROP+重建该库,连业务库 = 删库事故。
> 代码里有护栏:库名不等于 `numbering_test` 会直接拒绝运行。

**第 2 步：暴露 PG 端口到本机**（若尚未暴露）

编辑 `infra/docker-compose.yml`,取消 `postgres.ports` 的注释后重启,见 [infra/README.md](../infra/README.md)。

**第 3 步：设置环境变量,跑测试**

```bash
# bash/zsh
export NUMBERING_TEST_PG="Host=<云IP>;Port=<端口>;Database=numbering_test;Username=onecup;Password=<密码>"
dotnet test

# PowerShell
$env:NUMBERING_TEST_PG="Host=<云IP>;Port=<端口>;Database=numbering_test;Username=onecup;Password=<密码>"
dotnet test

# Windows CMD
set NUMBERING_TEST_PG=Host=<云IP>;Port=<端口>;Database=numbering_test;Username=onecup;Password=<密码>
dotnet test
```

连接串含特殊字符的密码难以 shell 转义时,可改用文件:

```bash
echo -n "Host=...;Database=numbering_test;..." > ~/.onecup_test_pg
export NUMBERING_TEST_PG_FILE=~/.onecup_test_pg
dotnet test
```

### 安全护栏

`NumberingServiceConcurrencyTests` 在 `InitializeAsync` 里强制校验:环境变量 `NUMBERING_TEST_PG`
指向的库名必须严格等于 `numbering_test`。不匹配立即抛 `InvalidOperationException`,绝不执行 DROP。
即使用户误配成生产库 `onecup`,测试一启动就失败,不会造成数据丢失。
