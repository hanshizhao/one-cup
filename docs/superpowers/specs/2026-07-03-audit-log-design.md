# OneCup 审计日志系统设计文档

> 印染厂面料开发管理系统 — 操作日志 + 登录日志（安全审计追责 + 问题排查）
> 创建日期: 2026-07-03
> 状态: 待实现
> 关联: [认证全链路设计](../../specs/2026-07-01-auth-fullchain-design.md)（登录日志采集复用其埋点）

---

## 1. 背景与目标

### 1.1 现状痛点

系统已具备成熟的认证授权（JWT + RBAC + 登录锁定 + RefreshToken 轮换），但**审计能力几乎为零**：

- `AuthService` 在登录成功/失败/锁定/登出/刷新时已有 `_logger.LogXxx`，但**只输出到控制台，不落库**，无法查询、统计、追溯。
- `AuthorizationAuditHandler` 在 403 权限拒绝时记 Warning，同样**只到日志管道**，重启即失。
- 全局异常处理（`Program.cs` 内联 `UseExceptionHandler`）已把异常映射为统一响应，但**异常发生不留痕**，事后无法回溯。
- 没有任何审计实体 / 表 / 服务 / 查询接口 / 前端页面。

### 1.2 驱动需求

本系统同时服务两个核心场景：

1. **安全审计 / 追责**——出安全事件或数据被篡改时，能追溯"谁在什么时候对什么资源做了什么"。要求：覆盖所有写操作 + 敏感读，登录成功失败全记，数据可信。
2. **问题排查 / 调试**——出 bug 或用户反馈时，能看到当时的操作轨迹和上下文辅助定位。要求：关联请求、抓异常堆栈、记入参出参。

### 1.3 本轮交付范围

| 模块 | 功能 |
|------|------|
| **操作日志** | 实体 + 表 + 全局捕获（Filter + `[Audit]` 特性 + 启发式）+ 敏感字段脱敏 + 查询 API + 前端查询页 |
| **登录日志** | 实体 + 表 + 会话全生命周期采集（登录/登出/刷新/锁定）+ 查询 API + 前端查询页 |
| **写入管道** | `Channel` + `BackgroundService` 后台批量消费，独立 `IDbContextFactory` 事务隔离 |
| **定时清理** | 后台任务按可配保留天数删除超期日志 |
| **权限控制** | 新增 `system:audit:view` 权限码 + `audit-view` 授权策略 + 种子数据 |

### 1.4 不在范围内

- 日志的聚合 / 分析仪表盘（统计图表、Top10 趋势等，留待后续运营监控需求）
- 日志导出（CSV / Excel，留待合规需求触发）
- 日志的物理防篡改（如哈希链、只追加文件存储，留待合规需求触发）
- 基于 Outbox 的强可靠写入（当前用内存队列，容忍崩溃丢日志）
- 操作日志对 GET 查询的全量记录（GET 默认不记，仅显式标注 `[Audit]` 的敏感读才记）

### 1.5 成功标准

- 所有非 GET 请求（含业务校验失败和 500 异常）都产生一条操作日志，业务语义清晰可读。
- 登录、登出、刷新、锁定事件都产生一条登录日志，含 IP、UA、账号、结果。
- 前端可在两个查询页按时间 / 用户 / 模块 / 动作 / 结果多条件筛选并分页浏览。
- 日志写入异步进行，对业务请求响应延迟无可感知影响。
- 日志库（表）异常不会影响业务请求的正常完成。
- 仅持有 `system:audit:view` 权限的账号可见日志菜单与数据。

---

## 2. 总体架构

### 2.1 分支策略

从 `master` 切出独立分支 `feat/audit-log`。**完全不碰** `feat/numbering-dictionary` 分支（同事正在进行编号字典优化）。日志模块自成体系，PR 干净独立；等字典分支合并进 master 后，日志分支再 rebase。

### 2.2 模块划分

两个独立但共享写入基础设施的子模块：

```
登录日志 (LoginLog)          操作日志 (OperationLog)
  │                              │
  ├─ 实体 LoginLog               ├─ 实体 OperationLog
  ├─ 登录事件采集                ├─ 全局 ActionFilter 捕获
  ├─ 同一套写入管道 ◄────────────┤
  │                              ├─ [Audit] 特性标注
  │                              ├─ 敏感字段脱敏
  │                              │
  └──────────┬───────────────────┘
             ▼
     共享基础设施
     ├─ Channels 写入队列 + BackgroundService 消费者
     ├─ IDbContextFactory 独立 DbContext（事务隔离）
     ├─ IAuditLogWriter 接口（fire-and-forget 写入端口）
     └─ 定时清理 BackgroundService（删超期日志）
```

### 2.3 分层落点（遵循现有 Clean Architecture 约定）

| 层 | 职责 |
|----|------|
| **Domain** | `LoginLog`、`OperationLog` 实体 + `LoginEventType`/`OperationResult` 枚举（继承 `BaseEntity`） |
| **Application** | DTO、Specification、`IAuditLogService`（查询）、`IAuditLogWriter`（写入端口）、`PayloadSanitizer`（脱敏）、`AuditLogOptions`（配置） |
| **Infrastructure** | EF 配置（snake_case）+ `AuditLogWriter`（实现 `IAuditLogWriter`，对接队列）+ `AuditLogChannel`（队列）+ `AuditLogQueueConsumer`（消费 BackgroundService）+ `AuditLogCleanupService`（清理 BackgroundService） |
| **Api** | `OperationLogActionFilter`（全局捕获）+ `[Audit]` 特性 + `CurrentUserService`（复用）+ `OperationLogsController` + `LoginLogsController` + `audit-view` 策略 |
| **前端** | `pages/system/operation-log/` + `pages/system/login-log/` + `api/auditLog.ts` |

---

## 3. 数据模型

### 3.1 表 `operation_logs`（操作日志）

记录所有非 GET 请求 + 标注了 `[Audit]` 的读操作。

| 属性名 | 列名（snake_case） | 类型 | 说明 |
|--------|---------------------|------|------|
| `Id` | `id` | uuid PK | 复用 `BaseEntity` |
| `UserId` | `user_id` | uuid? | 操作人；匿名请求（如登录失败）为 null |
| `Username` | `username` | varchar(64) | 操作人用户名快照（账号可能改名/删除） |
| `Module` | `module` | varchar(32) | 业务模块：`User`/`Role`/`Numbering`/`Auth` 等 |
| `Action` | `action` | varchar(32) | 动作：`Create`/`Update`/`Delete`/`Login` 等 |
| `TargetType` | `target_type` | varchar(64)? | 目标资源类型，如 `User` |
| `TargetId` | `target_id` | varchar(64)? | 目标资源 ID（Guid 转字符串，兼容复合键） |
| `TargetName` | `target_name` | varchar(128)? | 目标资源名称（可读性） |
| `Result` | `result` | varchar(16) | 枚举 `Success`/`Failed`，字符串存储 |
| `HttpMethod` | `http_method` | varchar(8) | `POST`/`PUT`/`DELETE` |
| `RequestPath` | `request_path` | varchar(256) | 请求路由模板，如 `/api/users/{id}` |
| `StatusCode` | `status_code` | int | HTTP 响应码（200/400/500） |
| `IpAddress` | `ip_address` | varchar(64)? | 客户端 IP |
| `UserAgent` | `user_agent` | varchar(256)? | 截断的 UA |
| `RequestPayload` | `request_payload` | jsonb? | 脱敏后的入参（jsonb 便于查询） |
| `ErrorMessage` | `error_message` | text? | 失败时的错误消息（DomainException.Message） |
| `StackTrace` | `stack_trace` | text? | 仅 500 异常记录堆栈（400 不记，避免噪音） |
| `DurationMs` | `duration_ms` | int | 耗时毫秒 |
| `TraceId` | `trace_id` | varchar(64)? | 关联同一请求（W3C TraceContext） |
| `CreatedAt` | `created_at` | timestamptz | 复用 `BaseEntity`，作事件发生时间 |

**索引**（不建唯一约束，日志允许重复）：
- `ix_op_logs_created_at` — 时间倒序查
- `ix_op_logs_user_id` — 按人查
- `ix_op_logs_module_action` — 按模块+动作筛

### 3.2 表 `login_logs`（登录日志）

记录会话生命周期事件。

| 属性名 | 列名 | 类型 | 说明 |
|--------|-------|------|------|
| `Id` | `id` | uuid PK | |
| `UserId` | `user_id` | uuid? | 登录失败（账号不存在）时为 null |
| `Username` | `username` | varchar(64) | 尝试登录的账号（即使不存在也记） |
| `EventType` | `event_type` | varchar(16) | 枚举：`Login`/`Logout`/`Refresh`/`Locked`，字符串存储 |
| `Result` | `result` | varchar(16) | `Success`/`Failed` |
| `IpAddress` | `ip_address` | varchar(64)? | |
| `UserAgent` | `user_agent` | varchar(256)? | |
| `FailureReason` | `failure_reason` | varchar(128)? | 失败原因分类：`InvalidCredentials`/`AccountLocked`/`UserNotFound` 等 |
| `Message` | `message` | varchar(256)? | 人类可读补充信息 |
| `CreatedAt` | `created_at` | timestamptz | 事件时间 |

**索引**：
- `ix_login_logs_created_at` — 时间倒序查
- `ix_login_logs_user_id` — 按人查
- `ix_login_logs_username` — 按账号查登录历史

### 3.3 枚举设计

两个枚举都用字符串存储（遵循项目现有 `DateSegment`/`ResetPeriod` 的 `.HasConversion<string>()` 约定，可读且新增成员不破坏存量数据）：

```
LoginEventType:   Login | Logout | Refresh | Locked
OperationResult:  Success | Failed
```

### 3.4 关键设计决定

1. **`OperationLog` 不使用 `UpdatedAt`**——日志一旦生成不可修改，符合审计语义。`BaseEntity` 的 `UpdatedAt` 字段会留着为空（不为此改基类）。
2. **`Username` 用快照而非外键关联 `users` 表**——账号可能改名或软删除，审计记录必须保留当时的账号名。
3. **`TargetId` 用 `varchar` 而非 `uuid`**——兼容未来复合键资源，且跨模块统一字段类型。
4. **`RequestPayload` 用 jsonb**——PostgreSQL 原生支持，既能存结构化入参，又便于按内容查询。
5. **`StackTrace` 仅 500 时记**——400（DomainException）是业务校验失败，属正常流程，记堆栈是噪音。
6. **登录日志的 `UserId` 可空**——登录失败且账号不存在时，没有合法 UserId。

---

## 4. 捕获与脱敏机制

### 4.1 操作日志捕获：全局 ActionFilter + `[Audit]` 特性

**`[Audit]` 特性**（Api 层，标注在 Controller Action 上）：

```csharp
[Audit(Module = "User", Action = "Create", TargetType = "User")]
[HttpPost]
public Task<IActionResult> Create(...)

[Audit(Module = "Auth", Action = "ResetPassword", TargetType = "User")]
[HttpPut("{id:guid}/password")]
public Task<IActionResult> ResetPassword(...)
```

属性：`Module`（必填）、`Action`（必填）、`TargetType`（可选，默认从路由推断）、`Description`（可选，更详细描述）。

**`OperationLogActionFilter`**（全局 ActionFilter，Api 层）核心流程：

```
请求进来
  ├─ 是 GET 且未标注 [Audit]？ → 跳过（GET 默认不记，除非显式标注，如敏感查询）
  ├─ 读 [Audit] 特性 → 有则用其 Module/Action/TargetType
  ├─ 无 [Audit] 特性 → 走启发式推断：
  │     Module  ← 从路由前缀推断（api/users → User）
  │     Action  ← 从 HTTP 方法推断（POST→Create, PUT→Update, DELETE→Delete）
  │     TargetType ← 同 Module
  ├─ 捕获 TargetId：
  │     优先从路由参数 {id} 取；
  │     POST 创建类从响应 CreatedAtAction 的 id 取（action 执行后读）
  ├─ 请求体序列化 + 脱敏 → RequestPayload
  ├─ action 执行后记录 StatusCode / ErrorMessage / DurationMs
  └─ 组装 OperationLog → 投递到 IAuditLogWriter（入队，不等）
```

**Filter 注册**：`Program.cs` 中 `builder.Services.AddControllers(o => o.Filters.Add<OperationLogActionFilter>())`。

### 4.2 敏感字段脱敏

**脱敏器** `PayloadSanitizer`（Application 层，纯函数可单测）规则：

1. **字段名黑名单**（大小写不敏感匹配）：`password`、`oldPassword`、`newPassword`、`token`、`accessToken`、`refreshToken`、`secret`、`authorization` → 值替换为 `"***"`
2. **递归处理**：嵌套对象和数组里的敏感字段一并打码
3. **请求体大小限制**：超过 8KB 的 payload 只记 `"[truncated: {size} bytes]"`，避免巨型 body 撑爆 jsonb
4. **非 JSON body**（如 multipart 文件上传）：不记录，标记 `"[binary]"`

示例：`{"username":"admin","password":"abc123"}` → `{"username":"admin","password":"***"}`

实现用 `System.Text.Json` 的 `Utf8JsonReader` 流式处理（不依赖 JsonDocument 全量解析，性能好），纯函数 `string Sanitize(string json)` 便于单测。

### 4.3 登录日志采集：复用现有埋点

`AuthService` 已在所有会话事件点有 `_logger.LogXxx`。改动方式——**注入 `IAuditLogWriter`，在现有日志分支旁加一次入队**：

| 事件 | 现有埋点位置 | EventType | Result / FailureReason |
|------|--------------|-----------|------------------------|
| 登录成功 | `AuthService.LoginAsync` 成功分支 | `Login` | `Success` |
| 密码错 | `LoginAsync` 验证失败分支 | `Login` | `Failed` / `InvalidCredentials` |
| 账号不存在 | `LoginAsync` 用户查找失败 | `Login` | `Failed` / `UserNotFound` |
| 账号锁定 | `LoginAsync` lockout 命中 | `Login` | `Failed` / `AccountLocked` |
| 锁定触发 | lockout 阈值达到时 | `Locked` | `Failed` / `LockoutTriggered` |
| 登出 | `LogoutAsync` | `Logout` | `Success` |
| 刷新成功 | `RefreshAsync` 成功 | `Refresh` | `Success` |
| 刷新失败 | `RefreshAsync` 失败（token 失效/吊销） | `Refresh` | `Failed` / `InvalidRefreshToken` |

**关键约束——IP/UA 的传递路径**：登录日志采集在 `AuthService`（Application 层），它本身不依赖 HttpContext（保持可单测、不耦合 AspNetCore）。解决路径分两段：
1. `AuthService` 的登录相关方法（`LoginAsync` / `LogoutAsync` / `RefreshAsync`）签名**增加两个可选的纯字符串参数** `string? ipAddress = null, string? userAgent = null`。这两个参数只是普通 string，不引入任何 HttpContext 类型依赖。
2. Api 层 Controller 在调用 `AuthService` 时，从 `HttpContext.Connection.RemoteIpAddress` 和 `HttpContext.Request.Headers.UserAgent` 取值后传入。

`AuthService` 内部构造 `LoginLog` 时把这些字段带上，连同业务字段一起 `writer.Enqueue(loginLog)`。这样 AuthService 既拿到了 IP/UA（用于落库），又没有依赖 AspNetCore（参数只是 string），单元测试可直接传 null 或固定值。

### 4.4 捕获完整性保障

- **全局 Filter 兜底**：所有非 GET 请求必经 Filter，即使忘贴 `[Audit]` 也不会漏（只是业务语义差些）。
- **异常也记**：Filter 在 `exception` 分支捕获未处理异常，记录 `Result=Failed` + 500 堆栈；`DomainException` 记 `Result=Failed` + ErrorMessage 但不记堆栈。
- **`[Audit]` 不存在的 GET 默认跳过**：避免查询接口产生海量日志。需要审计的敏感读操作（如导出、查看敏感数据）显式贴 `[Audit]`。

---

## 5. 写入管道与定时清理

### 5.1 整体数据流

```
ActionFilter / AuthService
    │ 构造 OperationLog / LoginLog 实体
    ▼
IAuditLogWriter（应用层端口，fire-and-forget）
    │ Enqueue()
    ▼
Channel<AuditLogEntry>（有界队列，内存）
    │
    ▼
AuditLogQueueConsumer（BackgroundService，单消费者）
    │ 批量读取 + 批量写入
    ▼
operation_logs / login_logs 表
```

### 5.2 队列：`Channel<AuditLogEntry>`

**统一入口实体** `AuditLogEntry`（Infrastructure 层内部类型）包装两种日志：

```csharp
record AuditLogEntry
{
    OperationLog? Operation
    LoginLog? Login
}
```

**有界 Channel 配置**（`AuditLogChannel`，Infrastructure 层，单例）：

```csharp
Channel.CreateBounded<AuditLogEntry>(new BoundedChannelOptions(capacity: 10000)
{
    FullMode = BoundedChannelFullMode.DropOldest,  // 队列满时丢最老的（保最新，符合追责优先近期）
    SingleReader = true,   // 单消费者
    SingleWriter = false   // 多生产者（多个请求）
})
```

**容量 10000 的依据**：假设系统 100 并发写操作，每秒产生约 200 条日志，批写入 500 条/秒的消费速度绰绰有余；10000 容量约 50 秒缓冲，足以扛住短时洪峰。

**DropOldest 的权衡**：队列满时丢弃最老的日志。这是有意识的选择——审计追责时近期记录更重要，且只在极端洪峰（队列积压 1 万条）时才触发，正常情况不会满。

### 5.3 消费者：`AuditLogQueueConsumer`

`BackgroundService` 子类，单实例，启动后循环：

```
while (!stoppingToken.IsCancellationRequested)
{
    // 从队列读一批（最多 100 条或等待 1 秒，先到先服务）
    batch = await ReadBatchAsync(maxCount: 100, timeout: 1s)

    // 分桶：operationLogs / loginLogs
    var ops = batch.Where(e => e.Operation != null).Select(e => e.Operation!)
    var logins = batch.Where(e => e.Login != null).Select(e => e.Login!)

    // 批量写入（各自一张表，分开 insert）
    using var db = _dbContextFactory.CreateDbContext()
    db.Set<OperationLog>().AddRange(ops)
    db.Set<LoginLog>().AddRange(logins)
    await db.SaveChangesAsync(stoppingToken)
}
```

**批写入策略**：EF Core 的 `AddRange` + 单次 `SaveChangesAsync`（每批一次事务）。不引入第三方 bulk insert 库（保持零依赖）。单批 100 条一次提交，比逐条 insert 快一个数量级。如果后续日志量极大，可换 Npgsql 原生 `COPY`，但当前规模不需要。

**消费循环容错**：
- **写入失败（DB 异常）**：记 `_logger.LogError`（控制台日志，不丢给前端），**丢弃该批**继续消费下一批。理由：日志系统不应阻塞或拖垮业务 DB；丢一批日志好过无限重试堵死队列。DropOldest 已保证队列不会因重试堆积。
- **消费者崩溃**：`BackgroundService` 由 ASP.NET Core 托管重启，未消费的内存队列内容丢失——这是"后台队列"选型时已接受的代价。
- **应用关闭**：利用 `IHostApplicationLifetime.ApplicationStopping`，消费者收到取消信号后**尽力消费完当前批**再退出（最多多等 5 秒），减少关停时的丢失。

### 5.4 写入端口：`IAuditLogWriter`

应用层定义接口，Infrastructure 实现。**这个抽象的存在是为了隔离**：

```csharp
// Application 层
public interface IAuditLogWriter
{
    // fire-and-forget：入队即返回，永不阻塞调用方
    void Enqueue(OperationLog log)
    void Enqueue(LoginLog log)
}
```

**为什么用 void 而非 Task**：明确告诉调用方"这是 fire-and-forget，不要 await，不要依赖它完成"。Filter 和 AuthService 调 `Enqueue` 后立即返回，请求响应零延迟增加。

实现类 `AuditLogWriter`（Infrastructure）只做一件事：把实体包成 `AuditLogEntry` 写进 Channel。

**接口的第二个价值**：将来若要升级可靠性（如改 Outbox 模式），只需替换 `AuditLogWriter` 实现，Filter 和 AuthService 零改动。

### 5.5 定时清理：`AuditLogCleanupService`

独立 `BackgroundService`，单实例，定时删除超期日志：

```csharp
// 每天凌晨 3:00 执行一次（可配置）
while (!stoppingToken.IsCancellationRequested)
{
    await Task.Delay(nextRunInterval, stoppingToken)  // 计算到下次 3:00 的间隔

    var cutoff = DateTime.UtcNow - TimeSpan.FromDays(retentionDays)
    await db.Set<OperationLog>().Where(x => x.CreatedAt < cutoff).ExecuteDeleteAsync()
    await db.Set<LoginLog>().Where(x => x.CreatedAt < cutoff).ExecuteDeleteAsync()

    _logger.LogInformation("清理超期审计日志，截止 {cutoff}", cutoff)
}
```

用 `ExecuteDeleteAsync`（EF Core 7+ 原生批量删除）一条 SQL 搞定，不把实体载入内存。删除按 `CreatedAt` 索引走，有前面建的 `ix_*_created_at` 支撑。

### 5.6 配置：`AuditLogOptions`

```yaml
AuditLog:
  RetentionDays: 180          # 日志保留天数，默认半年
  CleanupTime: "03:00"        # 清理执行时刻
  QueueCapacity: 10000        # 队列容量
  BatchSize: 100              # 批写入大小
```

### 5.7 事务隔离——日志写入不影响业务

**关键设计**：`AuditLogWriter` / 消费者使用**独立的 DbContext 实例**（通过 `IDbContextFactory<OneCupDbContext>` 创建短生命周期实例）。日志写入与业务请求的 `UnitOfWork` 是完全独立的事务，互不影响。即使日志表锁死或 DB 异常，业务请求照常完成（只是该条日志可能丢失）。

> 注册 `AddDbContextFactory<OneCupDbContext>`，单例消费者通过工厂每次处理一批时创建一个新 DbContext，处理完即 Dispose。这避免了"单例持有 Scoped DbContext"的经典陷阱。

---

## 6. 查询 API 与权限

### 6.1 查询服务：`IAuditLogService`（Application 层）

两个独立查询入口，操作日志和登录日志分开（字段差异大，合并查询反而别扭）：

```csharp
public interface IAuditLogService
{
    Task<PagedResult<OperationLogDto>> SearchOperationsAsync(OperationLogQuery query, CancellationToken ct);
    Task<OperationLogDto?> GetOperationAsync(Guid id, CancellationToken ct);
    Task<PagedResult<LoginLogDto>> SearchLoginsAsync(LoginLogQuery query, CancellationToken ct);
    Task<LoginLogDto?> GetLoginAsync(Guid id, CancellationToken ct);
}
```

**查询 DTO**（`[FromQuery]` 绑定，复用现有分页约定 `page`/`pageSize`）：

```csharp
public record OperationLogQuery
{
    public int Page { get; init; } = 1;
    public int PageSize { get; init; } = 10;
    public DateTime? StartTime { get; init; }      // 时间范围
    public DateTime? EndTime { get; init; }
    public Guid? UserId { get; init; }              // 按操作人筛
    public string? Username { get; init; }          // 模糊匹配用户名
    public string? Module { get; init; }            // 按模块筛
    public string? Action { get; init; }            // 按动作筛
    public OperationResult? Result { get; init; }   // Success/Failed
    public string? Keyword { get; init; }           // 模糊搜 RequestPath/TargetName/ErrorMessage
}
```

`LoginLogQuery` 类似：时间范围、UserId、Username、EventType、Result、FailureReason。

**只读查询**：复用现有 `IRepository<T>` + Specification 模式。日志是只追加数据，查询全用 `AsNoTracking`。同样遵循字典模块"过滤 Spec（Count 用）与分页 Spec（取数用）拆开"的约定，避免 Count 误加分页。

### 6.2 DTO 映射

遵循项目"无 AutoMapper，Service 内私有静态方法映射"约定。**敏感字段在 DTO 层二次防护**——虽然写入时已脱敏，但 `RequestPayload` 在返回前端前再过一次 `PayloadSanitizer.Sanitize`（纵深防御，防数据库里混入未脱敏的历史数据）。

```csharp
public class OperationLogDto
{
    public Guid Id { get; init; }
    public Guid? UserId { get; init; }
    public string Username { get; init; } = "";
    public string Module { get; init; } = "";
    public string Action { get; init; } = "";
    public string? TargetType { get; init; }
    public string? TargetId { get; init; }
    public string? TargetName { get; init; }
    public OperationResult Result { get; init; }
    public string HttpMethod { get; init; } = "";
    public string RequestPath { get; init; } = "";
    public int StatusCode { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? RequestPayload { get; init; }     // 已脱敏 jsonb → 字符串
    public string? ErrorMessage { get; init; }
    public string? StackTrace { get; init; }          // 详情接口才返回（列表不返回，防泄露）
    public int DurationMs { get; init; }
    public string? TraceId { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

**`StackTrace` 列表不返回**：列表接口（`SearchOperationsAsync`）的 DTO 不含 `StackTrace`，只有详情接口（`GetOperationAsync`）才返回。避免列表泄露大量堆栈、也减少传输量。

### 6.3 Controllers：两个 Controller

```csharp
[ApiController]
[Route("api/audit/operation-logs")]
[Authorize(Policy = "audit-view")]
public class OperationLogsController(IAuditLogService svc)

[ApiController]
[Route("api/audit/login-logs")]
[Authorize(Policy = "audit-view")]
public class LoginLogsController(IAuditLogService svc)
```

端点（与现有 Controller 风格一致）：
- `GET api/audit/operation-logs` — 分页查询（`[FromQuery] OperationLogQuery`）
- `GET api/audit/operation-logs/{id:guid}` — 详情
- `GET api/audit/login-logs` — 分页查询
- `GET api/audit/login-logs/{id:guid}` — 详情

返回类型复用 `PagedResult<T>` + `Ok()`/`NotFound()`，与现有 `NumberingDictionaryController` 完全一致。

### 6.4 权限：新增 `system:audit:view` 权限码 + `audit-view` 策略

**1. 种子数据**（`SeedData.cs` + `OneCupDbContext.Seed()`）：新增一条 Permission 记录：

```
Id:   SeedData.PermissionAuditView   // 新确定性 Guid，如 ...000000000016
Code: "system:audit:view"
Name: "审计日志查看"
```

并给 `developer` 角色关联该权限（admin 通配 `*` 自动拥有，无需显式关联）。

**2. 授权策略**（`Program.cs`）：

```csharp
builder.Services.AddAuthorization(options =>
{
    // ... 现有策略 ...
    options.AddPolicy("audit-view", policy =>
        policy.RequireClaim("perm_codes", "system:audit:view"));
});
```

**3. 前端菜单权限**：前端 Redux store 的 `userInfo.permissions` 已携带权限码，菜单项用现有 `RequirePermission` 组件包裹，`system:audit:view` 控制日志菜单的显隐。

### 6.5 前端菜单结构

在 `routes.ts` 的 `menu.system` 下新增两项：

```
menu.system
  ├─ user          (system:user:manage)
  ├─ role          (system:role:manage)
  ├─ permission    (system:permission:view)
  ├─ numbering     (system:numbering:view)
  ├─ operation-log (system:audit:view)   ← 新增
  └─ login-log     (system:audit:view)   ← 新增
```

前端 API 层新增 `api/auditLog.ts`（`getOperationLogs`/`getLoginLogs` 等），复用现有 `request.ts` 的 axios 封装。

---

## 7. 测试策略

遵循项目现有测试约定（xUnit + EF InMemory，无 Moq，手动 new 组装）。

| 测试对象 | 测试要点 |
|----------|----------|
| `PayloadSanitizer` | 纯函数单测：黑名单字段打码、嵌套递归、超大截断、二进制标记、无敏感字段原样返回 |
| `IAuditLogService` | InMemory DbContext 组装：多条件筛选、分页正确性、Count 不误加分页、StackTrace 列表不返回、空结果处理 |
| `AuditLogChannel` + `AuditLogQueueConsumer` | 队列入队出队、批量读取、DropOldest 满队行为、消费者容错（写入异常丢批继续） |
| `OperationLogActionFilter` | `[Audit]` 标注优先、启发式推断正确、GET 跳过、异常分支记录 Failed、TargetId 从路由/响应取 |
| 登录日志采集点 | 各 EventType 的 Result/FailureReason 映射正确 |

---

## 8. 文件清单（按现有模块模板对应）

**Domain**（2 实体 + 2 枚举）：
- `backend/src/OneCup.Domain/Entities/OperationLog.cs`
- `backend/src/OneCup.Domain/Entities/LoginLog.cs`
- `backend/src/OneCup.Domain/Enums/LoginEventType.cs`
- `backend/src/OneCup.Domain/Enums/OperationResult.cs`

**Application**：
- `backend/src/OneCup.Application/Dtos/System/AuditLogDtos.cs`（OperationLogDto/LoginLogDto + Query）
- `backend/src/OneCup.Application/Specifications/AuditLogSpecs.cs`
- `backend/src/OneCup.Application/Interfaces/IAuditLogService.cs`
- `backend/src/OneCup.Application/Interfaces/IAuditLogWriter.cs`
- `backend/src/OneCup.Application/Services/AuditLogService.cs`
- `backend/src/OneCup.Application/Common/PayloadSanitizer.cs`
- `backend/src/OneCup.Application/Options/AuditLogOptions.cs`

**Infrastructure**：
- `backend/src/OneCup.Infrastructure/Persistence/Configurations/OperationLogConfiguration.cs`
- `backend/src/OneCup.Infrastructure/Persistence/Configurations/LoginLogConfiguration.cs`
- `backend/src/OneCup.Infrastructure/Persistence/AuditLogChannel.cs`
- `backend/src/OneCup.Infrastructure/Persistence/AuditLogWriter.cs`（实现 `IAuditLogWriter`）
- `backend/src/OneCup.Infrastructure/Persistence/AuditLogQueueConsumer.cs`（`BackgroundService`）
- `backend/src/OneCup.Infrastructure/Persistence/AuditLogCleanupService.cs`（`BackgroundService`）
- `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs`（加 DbSet + Seed）
- `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs`（加确定性 Guid）
- 迁移文件（`dotnet ef migrations add AddAuditLog`）

**Api**：
- `backend/src/OneCup.Api/Controllers/OperationLogsController.cs`
- `backend/src/OneCup.Api/Controllers/LoginLogsController.cs`
- `backend/src/OneCup.Api/Filters/OperationLogActionFilter.cs`
- `backend/src/OneCup.Api/Filters/AuditAttribute.cs`
- `backend/src/OneCup.Api/Program.cs`（注册 Filter / Service / DbContextFactory / BackgroundServices / 策略）

**前端**：
- `frontend/src/pages/system/operation-log/index.tsx`
- `frontend/src/pages/system/login-log/index.tsx`
- `frontend/src/api/auditLog.ts`
- `frontend/src/routes.ts`（加菜单项）

**测试**：
- `backend/tests/OneCup.UnitTests/AuditLog/PayloadSanitizerTests.cs`
- `backend/tests/OneCup.UnitTests/AuditLog/AuditLogServiceTests.cs`
- `backend/tests/OneCup.UnitTests/AuditLog/AuditLogQueueTests.cs`
- `backend/tests/OneCup.UnitTests/AuditLog/OperationLogActionFilterTests.cs`
- `backend/tests/OneCup.UnitTests/AuditLog/LoginLogCollectionTests.cs`
