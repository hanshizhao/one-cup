# OneCup 用户/角色/权限 整改设计文档

> 印染厂面料开发管理系统 — 用户角色权限模块全面整改
> 创建日期: 2026-07-02
> 状态: 已确认
> 关联:
> - [认证全链路设计](2026-07-01-auth-fullchain-design.md)
> - [系统管理界面设计](2026-07-02-system-management-design.md)
> - [技术栈选型](2026-07-01-tech-stack-design.md)
> - ADR [0004-clean-architecture-layering](../adr/0004-clean-architecture-layering.md)
> - ADR [0006-jwt-authentication](../adr/0006-jwt-authentication.md)

---

## 1. 背景与目标

### 1.1 背景

用户/角色/权限功能已初步完成（认证全链路 + 系统管理三件套），代码审查发现一批问题，按性质分为：

- **安全高危**：前端 NavBar 角色切换会用伪造权限覆盖后端真实权限；JWT 密钥默认值不安全且无启动校验；登录端点无限流无锁定。
- **设计缺陷**：请求 DTO 无输入校验；Repository/UnitOfWork 形同虚设；Service 错放在 Infrastructure 层；前端无集中路由守卫；系统管理页未接入按钮级权限。
- **代码质量**：admin 通配逻辑三处重复；前端技术栈整体偏旧（React 17/RR5/Vite 2/手写 redux/TS strict 关闭）；大量 Arco Pro demo 残留；测试覆盖不足。

### 1.2 目标

在项目早期（代码量小、崩溃损失可接受）一次性偿还技术债：

1. 建立可上线安全基线（限流/锁定/日志/密钥校验）
2. 后端回归标准 Clean Architecture（Service 进 Application 层、启用 Specification + Repository + UoW）
3. 前端升级到当前主流技术栈（RR6 / RTK / TS5 strict）
4. 彻底清理 demo 残留
5. 补充集成测试与前端单测

### 1.3 已确认决策（来自头脑风暴）

| 维度 | 决策 |
|------|------|
| 整改范围 | **全面整改**（安全 + 缺陷 + 设计 + 架构 + 测试） |
| 推进策略 | **分阶段串行**（6 个阶段，每阶段独立可验证可回滚） |
| 后端分层 | **修正为标准 Clean Architecture**（Service 迁回 Application 层） |
| 查询边界 | **Specification 规范模式**（Service 构造规范，Repository 翻译为 LINQ） |
| 输入校验 | **FluentValidation** |
| 删除用户 | **软删除**（IsDeleted 字段 + 全局查询过滤） |
| token 存储 | **保持 localStorage**（不改为 httpOnly cookie） |
| 前端技术栈 | **全面升级到主流**（React18 / RR6 / Vite5 / TS5 / RTK） |
| demo 清理 | **彻底清理**（删模板页 + demo 逻辑 + 冗余状态） |
| RR 迁移 | **全部迁到 RR6 新 API**（Routes/element/loader） |
| 状态管理 | **迁到 Redux Toolkit** |

### 1.4 不在范围内

- token 改 httpOnly cookie（本轮保持 localStorage）
- 多实例部署的分布式限流（本轮用内存方案，文档标注限制）
- 操作日志 / 审计日志的业务化呈现（本轮只补安全事件结构化日志）
- 前端 E2E 测试（本轮只补单元测试）

---

## 2. 阶段划分

```
阶段1  A 安全基线             ← 风险最高，先上线
阶段2  B 架构修正              ← 后端回归标准 Clean Architecture
阶段3  C 校验 + 数据           ← FluentValidation + 软删除 + product:write
阶段4  E demo 清理             ← 轻量独立，顺手清
阶段5  D 技术栈升级 + bug修复  ← 前端大工程，在干净基础上做
阶段6  F 测试补充              ← 集成测试 + 前端单测
```

> 顺序设计原则：安全最先（阶段1）；架构打底在前端升级之前（阶段2/3 → 阶段5）；清理在升级之前做（阶段4 → 阶段5），避免升级时搬运即将删除的代码；测试单列为阶段6 但可前移穿插。
>
> 每阶段结束跑一遍全流程冒烟（登录→用户管理→角色管理→权限隔离）。

---

## 3. 阶段 A：后端安全基线

### 3.1 JWT SecretKey 启动校验（fail-fast）

**问题**：`appsettings.json` 默认值 `"REPLACE_VIA_USER_SECRETS"` 仅 24 字符，HMAC-SHA256 要求 ≥256 bit（32 字节）。部署忘覆盖会启动崩溃，且占位符进了 git。

**方案**：
- `JwtOptions` 实现校验：`SecretKey` 长度 < 32 字节 或 等于占位符常量 → 启动抛 `InvalidOperationException`。
- 在 `Program.cs` 服务注册后调用校验（`builder.Services.AddOptions<JwtOptions>().Validate(...).ValidateOnStart()`）。
- 占位符常量提取为 `JwtOptions.PlaceholderSecret`，校验时引用，避免散落。

### 3.2 登录/刷新端点限流 + 失败锁定

**问题**：`/api/auth/login` 与 `/api/auth/refresh` 允许匿名 + 无频率限制 → 暴力破解风险。

**方案**：
- **限流**：使用 .NET 内置 `System.Threading.RateLimiting`。
  - 登录端点：固定窗口，按 `IP + 用户名` 维度，例如每分钟 10 次。
  - 全局兜底：按 IP 维度的总速率上限。
  - 在 `Program.cs` `AddRateLimiter` 配置，命名策略，端点用 `[EnableRateLimiting("auth-login")]` 标注。
- **失败锁定**：账号维度失败计数。
  - 连续失败 N 次（默认 5）→ 锁定该账号 X 分钟（默认 15）。
  - 本轮用**内存计数器**（`IMemoryCache`，key = `lockout:{username}`），标注限制：**仅单实例可用，多实例需换 Redis**。
  - 登录成功重置计数；锁定期内直接拒绝（不查库、不校验密码）。
  - 锁定状态在 AuthService 校验，返回明确的 `locked` 错误码（不泄露"用户是否存在"，但锁定本身可观测，属可接受权衡）。

### 3.3 全局异常处理：生产环境不回显内部错误

**问题**：`Program.cs:119-122` 全局异常处理器把 `exception.Message` 直接返回客户端，可能泄露内部细节。

**方案**：
- 区分环境：`Development` 保留详细 message（便于调试）；`Production` 返回通用 `"服务器内部错误"`。
- 结构化错误响应：`{ code, message }`，`code` 用稳定的错误标识（如 `INTERNAL_ERROR` / `VALIDATION_ERROR` / `UNAUTHORIZED`）。
- 异常本身通过日志（见 3.4）记录完整堆栈，不依赖响应体。

### 3.4 安全事件日志

**问题**：全项目无任何 `ILogger` 调用，登录失败/成功、token 吊销、权限拒绝均无记录。

**方案**：在关键路径引入 `ILogger<T>`：
| 事件 | 级别 | 字段 | 位置 |
|------|------|------|------|
| 登录成功 | Information | username, userId, IP | AuthService.LoginAsync |
| 登录失败 | Warning | username(尝试值), IP, 原因 | AuthService.LoginAsync |
| 账号锁定触发 | Warning | username, IP | AuthService |
| Token 吊销 | Information | userId, token(部分掩码) | AuthService.Refresh/Logout |
| 权限拒绝 | Warning | userId, 策略, 端点 | 自定义 IAuthorizationHandler 或中间件 |

- IP 从 `IHttpContextAccessor` 获取（注意代理场景的 X-Forwarded-For，本轮取 `Connection.RemoteIpAddress`，标注后续需配 ForwardedHeaders）。
- 日志不记录密码、完整 token 等敏感字段。

### 3.5 admin 通配逻辑收敛

**问题**：admin 的 `perm_codes = ["*"]` 通配逻辑分散在 `AuthService.GetCurrentUserAsync`、`JwtTokenService.GenerateAccessToken`、`WildcardAuthorizationHandler` 三处，易漂移。

**方案**：
- 在 Application 层新增 `IPermissionCalculator`（实现 `PermissionCalculator`）：
  - `bool IsWildcard(IEnumerable<string> permCodes)` — 判断是否含 `*`。
  - `IReadOnlyList<string> GetEffective(IEnumerable<Role> roles)` — 聚合角色权限，admin 角色直接返回 `["*"]`。
- 三处调用点改为依赖 `IPermissionCalculator`，逻辑单一来源。

---

## 4. 阶段 B：后端架构修正（标准 Clean Architecture）

### 4.1 现状与目标

**现状**（违背 ADR-0004 意图）：
- Service 实现（AuthService/UserService/...）放在 `OneCup.Infrastructure/Services/`，直接依赖 `OneCupDbContext`。
- `IRepository<>` / `IUnitOfWork` 已定义并注册，但**无任何业务代码使用**（死代码）。
- 业务查询 LINQ（Include、分页、过滤）直接写在 Service 里。

**目标**：
- Service 实现迁到 `OneCup.Application/Services/`。
- Service 依赖 `IRepository<T>` + `IUnitOfWork` + `ISpecification<T>`，**不引用 EF Core**。
- Application 层零 EF Core 依赖（仅 Domain + 自身抽象）。
- EF Core 配置、DbContext、Repository 实现、Migration 留在 Infrastructure。

依赖方向严格遵循 ADR-0004：
```
Api → Application → Domain
Api → Infrastructure → Application → Domain
```

### 4.2 Specification 规范模式

新增 `OneCup.Application/Specifications/`：

```csharp
// 规范接口
public interface ISpecification<T>
{
    Expression<Func<T, bool>>? Criteria { get; }
    List<Expression<Func<T, object>>> Includes { get; }
    Expression<Func<T, object>>? OrderBy { get; }
    Expression<Func<T, object>>? OrderByDescending { get; }
    int? Take { get; }
    int? Skip { get; }
}

// 基类（链式构造）
public abstract class Specification<T> : ISpecification<T> { ... }

// 具体规范示例
public class UserPagedSpec : Specification<User>
{
    public UserPagedSpec(string? keyword, int page, int pageSize)
    {
        if (!string.IsNullOrWhiteSpace(keyword))
            Criteria = u => u.Username.Contains(keyword) || u.DisplayName.Contains(keyword);
        Skip = (page - 1) * pageSize;
        Take = pageSize;
        OrderBy = u => u.CreatedAt;
    }
}
```

Repository 扩展：
```csharp
public interface IRepository<T> where T : BaseEntity
{
    Task<T?> GetByIdAsync(Guid id);
    Task<IReadOnlyList<T>> ListAsync(ISpecification<T>? spec = null);
    Task<int> CountAsync(ISpecification<T>? spec = null);
    Task AddAsync(T entity);
    void Update(T entity);
    void Remove(T entity);
}
```

Infrastructure 的 `Repository<T>` 实现 `QueryAsync`，把 `ISpecification` 翻译为 `IQueryable`（应用 Criteria/Include/OrderBy/Skip/Take）。

### 4.3 Service 迁移与改造

| 文件 | 动作 |
|------|------|
| `Infrastructure/Services/AuthService.cs` → `Application/Services/AuthService.cs` | 迁移；DbContext 替换为 `IRepository<User>` + 规范；密码校验、token 签发逻辑不变 |
| `Infrastructure/Services/UserService.cs` → `Application/Services/UserService.cs` | 迁移；查询用 `UserPagedSpec`；admin 保护逻辑保留 |
| `Infrastructure/Services/RoleService.cs` → `Application/Services/RoleService.cs` | 迁移；删除关联用户检测改用规范 |
| `Infrastructure/Services/PermissionService.cs` → `Application/Services/PermissionService.cs` | 迁移 |
| `Infrastructure/Services/JwtTokenService.cs` | **留在 Infrastructure**（依赖 JWT 库，属基础设施关注点）；但调用 `IPermissionCalculator`（在 Application） |
| `Infrastructure/Services/PasswordHasher.cs` | **留在 Infrastructure**（BCrypt 属基础设施） |
| `Api/Services/CurrentUserService.cs` | 留在 Api；改为实现 `ICurrentUserService` 接口（接口放 Application） |

> 注：JwtTokenService / PasswordHasher 因依赖具体加密库，留在 Infrastructure 是合理的——它们是"技术细节"而非"业务逻辑"。Application 层通过 `IJwtTokenService` / `IPasswordHasher` 接口依赖它们。

### 4.4 UnitOfWork 真正启用

- Service 中所有"多步写操作"（如创建用户=加用户+分配角色）用 `IUnitOfWork.SaveChangesAsync()` 提交，保证事务边界。
- 单实体操作也可显式调 UoW，保持一致风格。
- 删除 Service 内直接 `_db.SaveChangesAsync()` 调用。

### 4.5 迁移影响

- 单元测试：现有 `Auth/User/RoleServiceTests` 用 InMemory DbContext 直接构造 Service。迁移后 Service 不再依赖 DbContext，测试改为构造 fake `IRepository`（手写 fake，沿用项目无 Mock 库的风格）或引入轻量 fake 仓储。
- 风险点：规范到 LINQ 的翻译正确性需测试覆盖（阶段6 补）。

---

## 5. 阶段 C：输入校验 + 数据修正

### 5.1 FluentValidation

**问题**：请求 DTO 无任何校验，空用户名/空密码/超长字符串直接抛 500。

**方案**：
- 引入 `FluentValidation.AspNetCore`。
- 每个 DTO 一个 Validator，放 `OneCup.Application/Validators/`：

| Validator | 规则要点 |
|-----------|---------|
| `LoginRequestValidator` | username/password 非空、长度上限 |
| `CreateUserRequestValidator` | username 非空+长度[3,50]、displayName 非空、email 格式(可选)、password 强度(长度≥8+含字母数字)、roleIds 非空 |
| `UpdateUserRequestValidator` | displayName 非空、email 格式、roleIds 非空 |
| `ResetPasswordRequestValidator` | newPassword 强度规则（与 Create 一致） |
| `CreateRoleRequestValidator` | name/code 非空+长度、code 格式（小写字母+数字+横线） |
| `UpdateRoleRequestValidator` | name 非空、permissionIds 可空（允许清空权限） |

- 注册：`AddFluentValidationAutoValidation()` → 自动拦截请求，校验失败返回 400 + 字段级错误信息。
- 密码强度规则统一抽一个常量/方法，避免 Create/Reset 重复。

### 5.2 补 product:write 权限

**问题**：seed 有 material/equipment/customer/color/fabric 的 write，唯独缺 `product:write`。

**方案**：
- SeedData 新增 `product:write` 权限常量。
- `developer` 角色按需纳入（developer 当前是只读为主 + fabric:write，是否给 product:write 取决于业务——本轮先**只补权限定义**，不绑定 developer，保持 developer 现有能力不变）。
- 新增权限 → 新 migration（权限是数据，需 seed 同步）。

### 5.3 软删除用户

**问题**：无删除用户能力；企业系统需保留历史关联可追溯。

**方案**：
- `User` 实体加 `IsDeleted`（bool，默认 false）。
- EF Core 配置：全局查询过滤器 `HasQueryFilter(u => !u.IsDeleted)`，所有常规查询自动排除已删除。
- 新增端点 `DELETE /api/users/{id}`：
  - admin 用户不可删除（保护）。
  - 标记 `IsDeleted = true`，不物理删除。
  - 同步吊销该用户所有 refresh token。
- 列表/详情查询因全局过滤器自动排除已删除用户，无需逐处改。
- 新 migration 加列。

### 5.4 DTO 同步

- `UserDto` / `UserListItemDto` 不暴露 `IsDeleted`（前端无需感知）。
- 删除端点无请求体，返回 204。

---

## 6. 阶段 E：前端 demo 清理

### 6.1 删除模板页

删除以下目录及其内容（纯 Arco Pro demo，无业务价值）：
- `pages/dashboard/`
- `pages/visualization/`
- `pages/list/`
- `pages/form/`
- `pages/profile/`
- `pages/result/`
- `pages/exception/`（403 兜底页保留逻辑但简化为独立轻量组件）
- `pages/user/`（demo 的用户中心，非系统管理）

### 6.2 删除 demo 逻辑

| 位置 | 动作 |
|------|------|
| `components/NavBar/index.tsx:70-80` | 删除角色切换 `useEffect`（覆盖权限的 demo 逻辑） |
| `routes.ts:52` `generatePermission()` | 删除（前端伪造权限的 demo 函数） |
| NavBar 角色切换 UI | 切换角色的下拉/按钮移除 |
| `useStorage('userRole', ...)` | 删除 |
| `useStorage('userStatus', ...)` | 删除（`checkLogin` 只看 token，此标志冗余） |
| `localStorage['userStatus']` / `['userRole']` 读写 | 全部清理 |

### 6.3 清理 i18n

- 删除 demo 页对应的 `locale/index.ts` 条目（menu.dashboard / menu.visualization / ...）。
- 保留系统管理、登录、异常页的 i18n。

### 6.4 清理依赖

- 移除仅 demo 使用的依赖（如 bizcharts、@turf/turf，若确认无业务页使用）。
- 保留 lodash、nprogress 等通用依赖。

> 阶段 E 在阶段 D（升级）之前做：避免升级时搬运即将删除的代码。

---

## 7. 阶段 D：前端技术栈升级 + bug 修复

### 7.1 依赖升级清单

| 依赖 | 当前 | 目标 | 备注 |
|------|------|------|------|
| react / react-dom | 17.0.2 | 18.x | 项目入口改 `createRoot` |
| react-router-dom | 5.2.0 | 6.x | 路由范式全改（见 7.2） |
| vite / @vitejs/plugin-react | 2.6 / - | 5.x / 4.x | 配置语法小幅调整 |
| typescript | 4.5 | 5.x | 开启 `strict: true` |
| @reduxjs/toolkit + react-redux | 无 | 最新 | 替换手写 redux（见 7.3） |
| axios | 0.24 | 1.x | API 兼容，主要修安全/兼容问题 |
| @arco-design/web-react | 2.32 | 最新 2.x | 跟随升级 |

### 7.2 react-router 5 → 6 迁移

**范式变化**：
- `Switch` → `Routes`
- `<Route component={X}>` → `<Route element={<X/>}>`
- `Redirect` → `<Navigate>`
- `useRouteMatch` → `useParams` / 相对路径
- 嵌套路由用 `<Outlet />`

**集中式路由守卫**（替换 `main.tsx` 的 `window.location` 硬跳转）：
```tsx
function PrivateRoute({ children }) {
  const token = getAccessToken();
  return token ? children : <Navigate to="/login" replace />;
}

// 路由配置
<Routes>
  <Route path="/login" element={<Login />} />
  <Route path="/" element={<PrivateRoute><Layout /></PrivateRoute>}>
    <Route index element={<Navigate to="/system/user" />} />
    <Route path="system/user" element={<UserPage />} />
    ...
  </Route>
</Routes>
```

- 权限过滤后的菜单仍用现有 `useRoute()` 逻辑，但适配 RR6 配置形态。
- 深链/刷新场景由 `<PrivateRoute>` 守卫，消除闪烁/竞态。

### 7.3 Redux → Redux Toolkit

- `store/index.ts` 手写 reducer → `createSlice`：
  - `userInfoSlice`：`{ name, avatar, permissions }`
  - `settingsSlice`：主题/语言等
- 组合为 `configureStore`。
- 权限转换 `transformPermissions()` 逻辑保留，在 fetchUserInfo 后 dispatch RTK action。
- 配合 TS5 strict，补全 action/payload 类型（消灭 `any`）。

### 7.4 TS strict + 类型修复

- `tsconfig.json` `strict: true`。
- 修复主要 `any`：
  - `store/index.ts` reducer action 类型
  - `layout.tsx` 多处 any
  - `role/index.tsx:50` treeData 类型
  - PermissionWrapper props 类型
- 目标：`tsc --noEmit` 零错误（升级后）。

### 7.5 系统管理页接入 PermissionWrapper

- 用户页：新增/编辑/重置密码/禁用按钮包 `PermissionWrapper`，要求 `system:user:manage`。
- 角色页：新增/编辑/删除按钮包 `PermissionWrapper`，要求 `system:role:manage`。
- 与菜单级过滤、后端鉴权三层叠加，前端按钮级收口。

### 7.6 bug 修复（顺带在升级中完成）

| bug | 修复 |
|-----|------|
| NavBar 角色切换覆盖权限（3.1/6.2 已删 demo 逻辑） | 随阶段 E 删除，升级阶段验证不再复现 |
| avatar 空指针 `NavBar/index.tsx:213` | `fetchUserInfo` 设置 avatar（用默认头像兜底）；渲染前判空 |
| `getRefreshToken` 死代码 + request.ts 绕过封装 | request.ts 刷新逻辑改用 `api/auth.ts` 的 `refreshToken()`，删重复读取 |

### 7.7 入口改造

- `main.tsx` → `createRoot`（React18）。
- 删除 `window.location` 硬跳转，改由路由守卫处理。

---

## 8. 阶段 F：测试补充

### 8.1 后端集成测试

新增 `OneCup.IntegrationTests` 项目（xunit + `WebApplicationFactory<Program>`）：

| 场景 | 覆盖 |
|------|------|
| JWT 中间件 | 无 token → 401；无效 token → 401；过期 → 401 |
| 策略授权 | 有 `system:user:manage` → 200；无 → 403 |
| admin 通配 | admin token 访问任意策略端点 → 200 |
| 输入校验 | 空 username → 400 + 错误信息；弱密码 → 400 |
| 异常映射 | 未知错误 → 500 + 通用 message（prod） |
| 限流 | 超阈值 → 429 |
| 软删除 | 删除后列表不可见；再次删除已删 → 404 |

### 8.2 前端单元测试

引入 `vitest` + `@testing-library/react` + `@testing-library/jest-dom`：

| 模块 | 覆盖 |
|------|------|
| `transformPermissions` | 普通权限拆分；admin `*` → 全通配；空权限 |
| `authentication()` 判定 | 全局通配短路；resource+action 匹配；oneOfPerm 任一满足；无权限拒绝 |
| token 刷新排队 | 401 触发刷新；并发请求排队；刷新失败跳登录 |
| PermissionWrapper | 有权限渲染 children；无权限渲染 backup |

---

## 9. 风险与权衡

### 9.1 已识别风险

| 风险 | 影响 | 缓解 |
|------|------|------|
| 阶段 B 动所有 Service 位置 + 查询方式 | 高，回归风险大 | 单元测试先行（迁移即改测试）；规范翻译加测试；阶段结束全流程冒烟 |
| 阶段 D 前端路由范式全变 | 高，路由/守卫/菜单耦合 | 升级前先做阶段 E 清理减少搬运；逐页迁移；保留旧菜单过滤逻辑只适配新 API |
| RR6 / RTK / TS5 升级叠加 | 中，类型错误批量暴露 | 分依赖升级，每升一个跑一次 tsc；先 RR6 再 RTK 再 strict |
| 内存限流单实例限制 | 中，多实例失效 | 文档明确标注；接口设计预留 Redis 替换点（`ILockoutStore`） |
| FluentValidation + 自动校验配置 | 低 | 校验失败统一 400 格式，与异常处理对齐 |

### 9.2 权衡说明

- **token 保持 localStorage**：接受 XSS 风险换取实现简单，适合内网管理系统场景。后续若上公网，再评估 httpOnly cookie。
- **失败锁定用内存**：单实例够用，多实例换 Redis。接口抽象 `ILockoutStore` 便于替换。
- **JwtTokenService/PasswordHasher 留 Infrastructure**：它们是技术细节（依赖加密库），非业务逻辑，留在基础设施层符合 Clean Architecture 精神，Application 通过接口依赖。
- **product:write 只补定义不绑 developer**：保持 developer 现有能力不变，权限定义完整即可，绑定由业务后续决定。

---

## 10. 验证标准（Definition of Done）

### 阶段 A（安全）
1. 启动时 SecretKey 为占位符/长度不足 → 启动失败并明确报错。
2. 同一账号连续登录失败 5 次 → 锁定 15 分钟，期内拒绝登录。
3. 登录端点超速请求 → 返回 429。
4. 生产环境 500 错误响应体不含 `exception.Message`。
5. 日志中可见登录成功/失败/锁定/权限拒绝记录。

### 阶段 B（架构）
6. `OneCup.Application` 项目零 EF Core NuGet 引用（`dotnet list package` 验证）。
7. 所有 Service 位于 `Application/Services/`。
8. Service 依赖 `IRepository`/`IUnitOfWork`，无直接 DbContext。
9. 现有单元测试全部通过（适配新依赖注入方式）。

### 阶段 C（校验+数据）
10. 提交空 username/password → 400 + 字段错误信息。
11. 弱密码 → 400。
12. `product:write` 权限存在于 seed。
13. DELETE 用户 → 列表不可见；DB 中 IsDeleted=true；其 refresh token 全部吊销。

### 阶段 E（清理）
14. `pages/{dashboard,visualization,list,form,profile,result,exception,user}` 目录删除。
15. NavBar 无角色切换入口；`generatePermission` 不存在。
16. 无 `userStatus`/`userRole` localStorage 读写。

### 阶段 D（升级）
17. `tsc --noEmit` 零错误（strict 模式）。
18. 路由用 RR6 API（Routes/element/Navigate/Outlet），无 Switch/Redirect。
19. 状态管理用 RTK createSlice，无手写 reducer。
20. 未登录访问受保护路由 → 重定向到 /login（由 PrivateRoute，非 window.location）。
21. developer 登录看不到系统管理页的新增/编辑/删除按钮（PermissionWrapper 收口）。
22. NavBar 头像正常显示（无空指针）；token 刷新走 api/auth.ts 封装。

### 阶段 F（测试）
23. 集成测试覆盖：401/403/通配/校验/异常/限流/软删除。
24. 前端单测覆盖：权限转换/判定/刷新排队/PermissionWrapper。

---

## 11. 后续演进

| 顺序 | 内容 |
|------|------|
| 1 | 多实例部署：限流/锁定换 Redis 实现（`ILockoutStore` 已预留） |
| 2 | token 改 httpOnly cookie（若上公网） |
| 3 | 操作日志/审计日志的业务化呈现 |
| 4 | ForwardedHeaders 配置（代理场景真实 IP） |
| 5 | 前端 E2E 测试（Playwright） |
