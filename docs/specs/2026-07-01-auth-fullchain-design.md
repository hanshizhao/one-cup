# OneCup 认证全链路设计文档

> 印染厂面料开发管理系统 — 第一个开发目标：认证全链路打通
> 创建日期: 2026-07-01
> 状态: 已确认
> 关联 ADR: [ADR-0006 JWT 认证](../adr/0006-jwt-authentication.md)

---

## 1. 目标与范围

### 1.1 目标

打通"前端登录 → 后端颁发 JWT → 后端校验 token → 前端据权限渲染"的完整认证闭环，为后续 12 个业务模块提供统一的身份与权限基座。

### 1.2 本轮交付范围（完整闭环）

| 层 | 交付内容 |
|----|---------|
| **数据库** | 用户 / 角色 / 权限 / 刷新令牌 共 6 张表（含 2 张关联表）+ 首次迁移 + 种子数据 |
| **后端** | RBAC 实体、认证服务、JWT 签发与校验、4 个认证端点、JWT 中间件、统一 401 处理 |
| **前端** | axios 封装、token 管理、登录页对接真实 API、登录态守卫改造、菜单权限对接 |
| **种子数据** | 1 个管理员账号 + 2 个角色 + 一批业务权限 code |

### 1.3 不在范围内（留待后续）

- 用户/角色/权限的**管理界面**（增删改查页面）—— 本轮只做认证链路，管理后台后续随"系统管理"模块开发
- 第三方登录（SSO / OAuth）
- 密码找回 / 修改密码功能
- Token 黑名单 + 强制下线（登出仅吊销 refresh token，access token 自然过期）
- 前端 demo 菜单的清理与业务菜单替换（后续随各业务模块开发）

---

## 2. 数据模型

### 2.1 ER 关系

```
users ──< user_roles >── roles ──< role_permissions >── permissions
  │
  └──< refresh_tokens
```

- `users` ↔ `roles`：多对多（一个用户可有多角色）
- `roles` ↔ `permissions`：多对多（一个角色聚合多个权限）
- `users` ↔ `refresh_tokens`：一对多（一个用户可有多条历史刷新令牌）

### 2.2 表结构

#### users（用户）

| 字段 | 类型 | 约束 | 说明 |
|------|------|------|------|
| id | uuid | PK | 主键，Guid |
| username | varchar(50) | unique, not null | 登录用户名 |
| password_hash | varchar(255) | not null | BCrypt 哈希 |
| display_name | varchar(50) | not null | 显示名 |
| email | varchar(100) | nullable | 邮箱 |
| is_active | boolean | not null, default true | 启用状态 |
| created_at | timestamp | not null | 创建时间 |
| updated_at | timestamp | nullable | 更新时间 |

#### roles（角色）

| 字段 | 类型 | 约束 | 说明 |
|------|------|------|------|
| id | uuid | PK | 主键 |
| name | varchar(50) | unique, not null | 显示名（如"管理员"） |
| code | varchar(50) | unique, not null | 角色编码（如 `admin`） |
| description | varchar(200) | nullable | 描述 |
| created_at | timestamp | not null | |
| updated_at | timestamp | nullable | |

#### permissions（权限）

| 字段 | 类型 | 约束 | 说明 |
|------|------|------|------|
| id | uuid | PK | 主键 |
| code | varchar(100) | unique, not null | 权限编码，格式 `资源:动作`（如 `fabric:read`） |
| name | varchar(100) | not null | 显示名（如"查看面料"） |
| description | varchar(200) | nullable | |
| created_at | timestamp | not null | |
| updated_at | timestamp | nullable | |

#### user_roles（用户-角色关联）

| 字段 | 类型 | 约束 |
|------|------|------|
| user_id | uuid | FK → users.id, PK(联合) |
| role_id | uuid | FK → roles.id, PK(联合) |

#### role_permissions（角色-权限关联）

| 字段 | 类型 | 约束 |
|------|------|------|
| role_id | uuid | FK → roles.id, PK(联合) |
| permission_id | uuid | FK → permissions.id, PK(联合) |

#### refresh_tokens（刷新令牌）

| 字段 | 类型 | 约束 | 说明 |
|------|------|------|------|
| id | uuid | PK | 主键 |
| token | varchar(64) | unique, not null | 随机 opaque token |
| user_id | uuid | FK → users.id, not null | 归属用户 |
| expires_at | timestamp | not null | 过期时间 |
| is_revoked | boolean | not null, default false | 是否已吊销 |
| created_at | timestamp | not null | 签发时间 |

### 2.3 命名约定

- 所有表名、列名使用 **snake_case**（PostgreSQL 惯例），已在 `OneCupDbContext.OnModelCreating` 约定。
- 关联表命名：`<主体>_<客体>`（如 `user_roles`）。
- 所有实体继承 `BaseEntity`（提供 `Id`/`CreatedAt`/`UpdatedAt`）。

### 2.4 权限编码体系

权限编码格式：`<模块>:<资源>:<动作>` 或 `<模块>:<动作>`，与前端 `routes.ts` 现有的 `{ resource, actions }` 权限模型对齐。

本轮预置的业务权限（按 [技术架构文档](2026-07-01-tech-stack-design.md) 的 12 模块规划，先覆盖系统管理 + 一批占位）：

| code | name |
|------|------|
| `system:user:manage` | 管理用户 |
| `system:role:manage` | 管理角色与权限 |
| `fabric:read` | 查看面料开发 |
| `fabric:write` | 录入/编辑面料开发 |
| `material:read` | 查看原料物料 |
| `material:write` | 维护原料物料 |
| `equipment:read` | 查看设备 |
| `equipment:write` | 维护设备 |
| `customer:read` | 查看客户 |
| `customer:write` | 维护客户 |
| `color:read` | 查看颜色对色 |
| `color:write` | 维护颜色对色 |
| `product:read` | 查看产品 |

> 注：`admin` 角色通过通配 `*` 拥有全部权限（见 5.3），无需逐条绑定。上述权限本轮主要预置入库 + 绑定给 `developer` 角色，供前端菜单权限过滤演示。

---

## 3. 后端设计

### 3.1 分层职责

严格遵循 [ADR-0004 Clean Architecture](../adr/0004-clean-architecture-layering.md)，依赖方向单向。

#### Domain 层（OneCup.Domain）— 零依赖

| 文件 | 内容 |
|------|------|
| `Entities/User.cs` | `User` 实体 + `UserRoles` 导航集合 |
| `Entities/Role.cs` | `Role` 实体 + `RolePermissions` 导航集合 |
| `Entities/Permission.cs` | `Permission` 实体 |
| `Entities/RefreshToken.cs` | `RefreshToken` 实体 |

Domain 层只含 POCO 实体与导航属性，不含 EF Core 特性（配置全部放在 Infrastructure 的 IEntityTypeConfiguration 中）。

#### Application 层（OneCup.Application）

| 文件 | 内容 |
|------|------|
| `Interfaces/IAuthService.cs` | 认证服务接口：Login / Refresh / Logout / GetCurrentUser |
| `Interfaces/IJwtTokenService.cs` | JWT 签发接口 |
| `Interfaces/IPasswordHasher.cs` | 密码哈希接口 |
| `Options/JwtOptions.cs` | JWT 配置（SecretKey / AccessTokenMinutes / RefreshTokenDays / Issuer / Audience） |
| `Dtos/Auth/LoginRequest.cs` | `{ Username, Password }` |
| `Dtos/Auth/RefreshRequest.cs` | `{ RefreshToken }` |
| `Dtos/Auth/TokenResponse.cs` | `{ AccessToken, RefreshToken, ExpiresIn }` |
| `Dtos/Auth/CurrentUser.cs` | `{ Id, Username, DisplayName, Roles[], Permissions[] }` |

Application 层定义接口与 DTO，不引用 EF Core / JWT 库。

#### Infrastructure 层（OneCup.Infrastructure）

| 文件 | 内容 |
|------|------|
| `Persistence/Configurations/UserConfiguration.cs` 等 | 6 张表的 EF Core Fluent 配置（snake_case 表名列名、关联关系、唯一索引） |
| `Persistence/OneCupDbContext.cs` | 注册 `DbSet<>` + `ApplyConfigurationsFromAssembly` + 种子数据（`HasData`） |
| `Services/AuthRepository.cs` | 按用户名查用户（含 Roles.Permissions 预加载）、刷新令牌的增删改查 |
| `Services/JwtTokenService.cs` | 实现 `IJwtTokenService`：用 `System.IdentityModel.Tokens.Jwt` 签发 HS256 token，Claims 含 sub/username/role_codes/permission_codes |
| `Services/PasswordHasher.cs` | 实现 `IPasswordHasher`：BCrypt 哈希与校验 |
| `Services/AuthService.cs` | 实现 `IAuthService`：编排登录/刷新/登出/获取当前用户的业务逻辑 |

#### Api 层（OneCup.Api）

| 文件 | 内容 |
|------|------|
| `Controllers/AuthController.cs` | 4 个端点：`POST /api/auth/login`、`/refresh`、`/logout`、`GET /api/auth/me` |
| `Services/CurrentUserService.cs` | 从 `HttpContext.User` Claims 提取当前用户 id/username/permissions，供其他 Controller 注入使用 |
| `Program.cs` | 新增：`AddJwtBearer` 认证、`AddAuthorization`、注册 `JwtOptions`/`IAuthService`/`IJwtTokenService`/`IPasswordHasher`/`CurrentUserService` |

### 3.2 认证端点契约

#### POST `/api/auth/login`

**请求体：**
```json
{ "username": "admin", "password": "Admin@123" }
```

**成功响应（200）：**
```json
{
  "accessToken": "eyJhbG...",
  "refreshToken": "a1b2c3...",
  "expiresIn": 1800
}
```

**失败响应（401）：**
```json
{ "message": "用户名或密码错误" }
```

逻辑：按 username 查用户 → 校验 is_active → BCrypt 校验密码 → 生成 access token（含 claims） → 生成 refresh token 存库 → 返回。

#### POST `/api/auth/refresh`

**请求体：**
```json
{ "refreshToken": "a1b2c3..." }
```

**成功响应（200）：** 同 login 的成功响应。

**失败响应（401）：**
```json
{ "message": "刷新令牌无效或已过期" }
```

逻辑：按 token 查 `refresh_tokens` → 校验未吊销 + 未过期 → 吊销旧 token（轮换）→ 签发新 access token + 新 refresh token → 返回。

#### POST `/api/auth/logout`

**请求头：** `Authorization: Bearer <accessToken>`

**成功响应（204）：** 无 body。

逻辑：从 access token 取 userId → 吊销该用户所有未过期的 refresh token。Access token 不处理（自然过期）。

#### GET `/api/auth/me`

**请求头：** `Authorization: Bearer <accessToken>`

**成功响应（200）：**
```json
{
  "id": "guid",
  "username": "admin",
  "displayName": "管理员",
  "roles": ["admin"],
  "permissions": ["*"]
}
```

逻辑：从 Claims 取 userId → 查用户（含 Roles.Permissions）→ 返回 `CurrentUser`。

### 3.3 JWT Token 设计

| 属性 | 值 |
|------|-----|
| 签名算法 | HS256 |
| SecretKey | 从配置读取（`Jwt:SecretKey`，开发态用 user-secrets，生产态用环境变量） |
| Issuer / Audience | 从配置读取（`Jwt:Issuer` / `Jwt:Audience`） |
| Access Token 有效期 | **30 分钟**（`Jwt:AccessTokenMinutes`） |
| Refresh Token 有效期 | **7 天**（`Jwt:RefreshTokenDays`），opaque 随机串存库 |

**Access Token Claims：**

| Claim | 来源 |
|-------|------|
| `sub` | user.id |
| `username` | user.username |
| `role_codes` | string[]，用户的角色编码集合 |
| `perm_codes` | string[]，用户的权限编码集合（admin 为 `["*"]`） |
| `exp` / `iat` | 标准签发/过期时间 |

**Refresh Token：**
- 生成方式：32 字节随机数 + Base64URL 编码（64 字符）
- 存储：`refresh_tokens` 表，含 userId / expiresAt / isRevoked
- 轮换：每次 refresh 后吊销旧 token、签发新 token
- 登出：吊销该用户所有有效 refresh token

### 3.4 密码安全

- 哈希算法：**BCrypt**（`BCrypt.Net-Next` 包，工作因子默认 12）
- 密码**绝不**明文存储、明文传输（生产环境强制 HTTPS）
- 登录校验：`BCrypt.Net.BCrypt.Verify(input, storedHash)`

### 3.5 统一错误响应格式

沿用 `Program.cs` 现有的全局异常处理中间件，认证错误统一为：

```json
{ "message": "<可读的错误描述>" }
```

HTTP 状态码：
- `401 Unauthorized`：未认证 / token 无效或过期 / 用户名密码错误
- `403 Forbidden`：已认证但无权限（本轮暂不触发，预留给后续业务接口）

### 3.6 后端新增依赖

| 包 | 安装到 | 用途 |
|----|--------|------|
| `Microsoft.AspNetCore.Authentication.JwtBearer` | OneCup.Api | JWT 中间件 |
| `BCrypt.Net-Next` | OneCup.Infrastructure | 密码哈希 |

> `System.IdentityModel.Tokens.Jwt` 已包含在 JwtBearer 的依赖链中，无需单独引用。

---

## 4. 前端设计

### 4.1 现状结论（探索结果）

经探索确认，当前 Arco Design Pro 脚手架在数据请求层是**裸的**：

- ❌ 无 axios 封装（全项目 `axios.create` / `interceptors` 零命中）
- ❌ 无 `src/api/` 目录
- ❌ 无 baseURL / proxy 配置（`vite.config.ts` 无 proxy，无 `.env` 文件）
- ❌ 无 token 概念（登录态仅是 localStorage 字符串 `userStatus = 'login'`，可伪造）
- 所有"接口"靠 mockjs 劫持 XHR 返回假数据，axios 是裸 import 裸调用

"开箱即用"只体现在 UI 骨架（布局/菜单/权限路由/国际化/主题），**数据请求层需从零搭建**。

### 4.2 新增基础设施

| 文件 | 内容 |
|------|------|
| `src/api/request.ts` | axios 实例封装（见 4.3） |
| `src/api/auth.ts` | 认证 API 调用：login / refresh / logout / getCurrentUser |
| `src/utils/token.ts` | token 存取工具：getToken / setToken / removeToken / getRefreshToken |
| `.env.development` | `VITE_API_BASE_URL=http://localhost:5000` |
| `.env.production` | `VITE_API_BASE_URL=/`（生产由反向代理同源） |

### 4.3 axios 封装（`src/api/request.ts`）

```
axios 实例
├── 配置: baseURL = import.meta.env.VITE_API_BASE_URL
├── 请求拦截器
│    └── 自动注入 Authorization: Bearer <accessToken>（登录/刷新接口除外）
├── 响应拦截器
│    ├── 2xx: 返回 response.data
│    ├── 401: 尝试用 refreshToken 调 /api/auth/refresh
│    │        ├── 成功: 更新 token，重放原请求
│    │        └── 失败: 清除 token，跳转 /login
│    └── 其他: 全局 Message.error 提示，reject
```

- **并发 refresh 防抖**：refresh 进行中时，后续 401 请求挂起等待同一个 refresh Promise，避免并发刷新。
- 使用项目已有的 `@arco-design/web-react` 的 `Message` 组件做错误提示，保持 UI 一致。

### 4.4 登录态改造

#### token 存储（`src/utils/token.ts`）

| localStorage key | 内容 |
|------------------|------|
| `onecup_access_token` | accessToken 字符串 |
| `onecup_refresh_token` | refreshToken 字符串 |

#### 改造点

| 文件 | 改造内容 |
|------|---------|
| `src/utils/checkLogin.tsx` | 从判 `userStatus === 'login'` 改为判 `!!getAccessToken()` |
| `src/pages/login/form.tsx` | mock 校验换为调用 `login(params)` 真实 API；成功后 `setToken(res)` 替代 `localStorage.setItem('userStatus','login')` |
| `src/main.tsx` | `fetchUserInfo` 从 mock `/api/user/userInfo` 改为真实 `GET /api/auth/me`；删除 `import './mock'`（或改为仅 mock 非认证数据） |
| `src/components/NavBar/index.tsx` | 登出逻辑：调 `logout()` API → `removeToken()` → 跳 `/login`，替代原 `setUserStatus('logout')` |

#### mock 处理策略

- **登录相关 mock（`src/mock/user.ts` 的 login/userInfo）**：移除，改走真实后端。
- **非认证 mock（`src/mock/message-box.ts`）**：暂保留，与认证无关，后续接真实消息接口时再替换。
- `src/main.tsx` 中 `import './mock'` 暂保留（message-box mock 仍用），但 `user.ts` 内的 login/userInfo 注册删除。

### 4.5 权限对接

前端 `routes.ts` 现有的权限模型是 `{ resource, actions }`（`src/utils/authentication.ts`），后端返回的 `permissions` 是 `["fabric:read", "system:user:manage"]` 形式。对接方式：

- 后端 `GET /api/auth/me` 返回 `permissions: string[]`。
- 前端在 `main.tsx` 的 `fetchUserInfo` 中，把后端权限数组转换为前端 store 期望的 `Record<string, string[]>` 格式（key 为 `resource`，value 为 `actions[]`），供现有 `routes.ts` 的 `useRoute` 权限过滤直接使用。
- admin 角色返回 `permissions: ["*"]`，前端 `authentication.ts` 已处理 `*` 通配（perm.join('') === '*' 返回 true）。

> 权限映射的具体实现细节（`perm_codes` → `{resource: actions[]}` 的转换函数）在实现计划中细化。

### 4.6 前端 demo 菜单处理

本轮**不清理** demo 菜单（dashboard/visualization/list/form 等）。原因：
- 清理菜单属于"业务模块开发"的范畴，需逐模块替换为真实页面。
- 本轮聚焦认证链路，demo 菜单保留可作为登录后的默认着陆验证（能登录、能看到菜单、能跳页即可证明链路通）。
- 登录守卫和权限过滤机制改造后，demo 菜单自然受真实权限控制。

---

## 5. 种子数据

### 5.1 管理员账号

| 字段 | 值 |
|------|-----|
| username | `admin` |
| password | `Admin@123`（BCrypt 哈希后存库） |
| display_name | 管理员 |
| is_active | true |

### 5.2 角色

| code | name | description | 绑定权限 |
|------|------|-------------|---------|
| `admin` | 管理员 | 系统超级管理员，拥有全部权限 | 通配 `*`（不逐条绑定，在 `AuthService` 中特殊处理） |
| `developer` | 开发员 | 面料开发相关权限 | `fabric:read`、`fabric:write`、`material:read`、`equipment:read`、`customer:read`、`color:read`、`product:read` |

admin 用户绑定 `admin` 角色。

### 5.3 种子数据实现方式

通过 EF Core 的 `OnModelCreating` → `HasData()` 实现，随首次迁移自动入库。由于 `HasData` 要求主键固定，种子数据的 Guid 使用确定性值（硬编码常量）。

密码哈希在种子数据中需预计算一个固定的 BCrypt 哈希值（因 `HasData` 是声明式的，不能在迁移时调用运行时哈希函数）。实现时用 `BCrypt.Net.BCrypt.HashPassword("Admin@123")` 预先生成哈希字符串硬编码入种子。

---

## 6. EF Core 迁移

### 6.1 迁移策略

- 项目已有 EF Core Code-First 配置（`OneCupDbContext`），但尚无任何迁移文件。
- 本轮创建第一个迁移 `InitialCreate`，包含上述 6 张表。
- 迁移命令在 Api 项目目录执行（`UserSecretsId` 已配置）：

```bash
cd backend/src/OneCup.Api
dotnet ef migrations add InitialCreate --project ../OneCup.Infrastructure --startup-project .
dotnet ef database update --project ../OneCup.Infrastructure --startup-project .
```

### 6.2 snake_case 映射

所有实体配置中显式映射表名与列名为 snake_case（PostgreSQL 惯例），与 `OneCupDbContext` 注释中声明的约定一致。例如 `users` 表、`password_hash` 列。

---

## 7. 配置项

### 7.1 后端配置（`appsettings.json` + user-secrets）

新增 `Jwt` 配置节：

```json
{
  "Jwt": {
    "Issuer": "OneCup",
    "Audience": "OneCup",
    "AccessTokenMinutes": 30,
    "RefreshTokenDays": 7,
    "SecretKey": "<敏感值，用 user-secrets 或环境变量>"
  }
}
```

- `SecretKey` 通过 `dotnet user-secrets set "Jwt:SecretKey" "<32+字符随机串>"` 设置，**不进 git**。
- 其余 Jwt 配置（Issuer/Audience/有效期）可放 appsettings.json 明文。

### 7.2 前端配置

| 文件 | 内容 |
|------|------|
| `.env.development` | `VITE_API_BASE_URL=http://localhost:5000` |
| `.env.production` | `VITE_API_BASE_URL=` （空，走同源反向代理） |

`vite.config.ts` **暂不加 proxy**——后端 CORS 已配好白名单（`http://localhost:5173` 在 `appsettings.json` 的 `Cors:AllowedOrigins` 中），前端直接跨域请求后端即可。

---

## 8. 验证标准（Definition of Done）

本轮交付完成且可通过以下验证：

1. **后端启动**：`dotnet run --project src/OneCup.Api` 成功，OpenAPI 页面可访问。
2. **迁移成功**：`dotnet ef database update` 执行后，数据库生成 6 张表 + 种子数据（admin 用户、2 角色、权限）。
3. **登录接口**：用 `admin / Admin@123` 调 `POST /api/auth/login`，返回 accessToken + refreshToken。
4. **鉴权接口**：带 accessToken 调 `GET /api/auth/me`，返回 admin 用户信息和 `permissions: ["*"]`。
5. **刷新接口**：用 refreshToken 调 `POST /api/auth/refresh`，返回新 token 对。
6. **登出接口**：带 accessToken 调 `POST /api/auth/logout` 返回 204，旧 refreshToken 失效。
7. **前端登录**：启动前端，用 `admin / Admin@123` 登录成功，跳转首页，菜单正常渲染。
8. **token 守卫**：清除 localStorage 的 token 后刷新页面，被重定向到 `/login`。
9. **401 自动刷新**：access token 过期后，前端自动用 refresh token 续期，用户无感（可通过缩短 token 有效期手动验证）。

---

## 9. 后续演进路径

本轮完成后的自然延伸（非本轮范围）：

| 顺序 | 内容 |
|------|------|
| 1 | 系统管理模块：用户/角色/权限的 CRUD 管理界面 |
| 2 | 密码修改 / 密码重置功能 |
| 3 | Token 黑名单（Redis）实现 access token 主动失效 |
| 4 | 登录日志 / 审计日志 |
| 5 | 前端 demo 菜单替换为真实业务菜单 |
