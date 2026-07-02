# OneCup 系统管理界面设计文档

> 印染厂面料开发管理系统 — 第二个开发目标：系统管理界面
> 创建日期: 2026-07-02
> 状态: 已确认
> 关联: [认证全链路设计](2026-07-01-auth-fullchain-design.md)（已完成）

---

## 1. 目标与范围

### 1.1 目标

为已完成认证系统补充可视化的 RBAC 管理后台：管理员能在界面上新增/编辑用户、创建角色并分配权限，而不再依赖直接操作数据库。

### 1.2 本轮交付范围

| 模块 | 功能 |
|------|------|
| **用户管理** | 分页列表 + 关键词搜索；新增（抽屉表单）；编辑（抽屉表单）；重置密码（抽屉表单）；禁用/启用（行内切换） |
| **角色管理** | 列表；新增（抽屉表单）；编辑（抽屉表单）；分配权限（抽屉表单 + 权限树勾选）；删除（确认） |
| **权限列表** | 只读列表（展示 code/name/description），不提供增删改 |
| **菜单重构** | 删除全部 Arco Pro demo 菜单，替换为「系统管理」一级菜单 + 三个子菜单 |

### 1.3 不在范围内

- 权限的运行时增删改（权限 code 随业务模块在代码/SeedData 中预置，详见设计决策 6.1）
- 用户个人信息修改页（用户自己改密码/资料，留待后续）
- 操作日志 / 审计日志
- 其他业务模块（设备/原料/客户等）

---

## 2. 后端 API 设计

RESTful 风格，复用已有 Clean Architecture 分层 + `IRepository` + `IUnitOfWork` + `PasswordHasher`。

### 2.1 用户管理 API

| 方法 | 路径 | 说明 | 请求体 / 参数 |
|------|------|------|--------------|
| GET | `/api/users` | 用户分页列表 | query: `page`, `pageSize`, `keyword`(可选) |
| GET | `/api/users/{id}` | 用户详情（含角色） | — |
| POST | `/api/users` | 新增用户 | `CreateUserRequest` |
| PUT | `/api/users/{id}` | 编辑用户（含重新分配角色） | `UpdateUserRequest` |
| PUT | `/api/users/{id}/password` | 重置密码 | `ResetPasswordRequest` |
| PUT | `/api/users/{id}/status` | 禁用/启用 | `UpdateStatusRequest` |

**分页响应格式：** 复用已有 `PagedResult<T>`（`Items` + `Total` + `Page` + `PageSize`）。

### 2.2 角色管理 API

| 方法 | 路径 | 说明 | 请求体 / 参数 |
|------|------|------|--------------|
| GET | `/api/roles` | 角色列表 | — |
| GET | `/api/roles/{id}` | 角色详情（含权限） | — |
| POST | `/api/roles` | 新增角色 | `CreateRoleRequest` |
| PUT | `/api/roles/{id}` | 编辑角色（含重新分配权限） | `UpdateRoleRequest` |
| DELETE | `/api/roles/{id}` | 删除角色 | — |

### 2.3 权限 API

| 方法 | 路径 | 说明 |
|------|------|------|
| GET | `/api/permissions` | 权限列表（只读，登录即可访问） |

### 2.4 DTO 定义

```
// 用户
CreateUserRequest:    { username, displayName, email?, password, roleIds[] }
UpdateUserRequest:    { displayName, email?, isActive, roleIds[] }   // username 不可改
ResetPasswordRequest: { newPassword }
UpdateStatusRequest:  { isActive }
UserDto:              { id, username, displayName, email, isActive, createdAt, roles[] }
UserListItemDto:      { id, username, displayName, email, isActive, createdAt, roleNames[] }

// 角色
CreateRoleRequest:    { name, code, description? }
UpdateRoleRequest:    { name, description?, permissionIds[] }
RoleDto:              { id, name, code, description, createdAt, permissions[] }
RoleListItemDto:      { id, name, code, description, createdAt, userCount, permissionCount }

// 权限
PermissionDto:        { id, code, name, description }
```

### 2.5 权限要求

所有 `/api/users/*` 接口要求 `system:user:manage` 权限。
所有 `/api/roles/*` 接口要求 `system:role:manage` 权限。
`/api/permissions` 仅需登录（任意已认证用户可查看权限列表）。

> 后端权限校验通过 `[Authorize]` + 自定义 Policy 实现（基于 JWT claims 中的 `perm_codes`）。

---

## 3. 后端分层落地

严格遵循 Clean Architecture，沿用认证开发已建立的模式。

### 3.1 Application 层

| 文件 | 内容 |
|------|------|
| `Dtos/System/UserDtos.cs` | `UserDto` / `UserListItemDto` / `CreateUserRequest` / `UpdateUserRequest` / `ResetPasswordRequest` / `UpdateStatusRequest` |
| `Dtos/System/RoleDtos.cs` | `RoleDto` / `RoleListItemDto` / `CreateRoleRequest` / `UpdateRoleRequest` |
| `Dtos/System/PermissionDto.cs` | `PermissionDto` |
| `Interfaces/IUserService.cs` | 用户管理服务接口（GetList/GetById/Create/Update/ResetPassword/UpdateStatus） |
| `Interfaces/IRoleService.cs` | 角色管理服务接口（GetList/GetById/Create/Update/Delete） |
| `Interfaces/IPermissionService.cs` | 权限查询接口（GetList） |

### 3.2 Infrastructure 层

| 文件 | 内容 |
|------|------|
| `Services/UserService.cs` | 实现 `IUserService`：用户 CRUD + 角色关联 + 密码哈希 + admin 保护逻辑 |
| `Services/RoleService.cs` | 实现 `IRoleService`：角色 CRUD + 权限关联 + 删除前校验 |
| `Services/PermissionService.cs` | 实现 `IPermissionService`：只读查询 |

### 3.3 Api 层

| 文件 | 内容 |
|------|------|
| `Controllers/UsersController.cs` | 6 个用户管理端点 |
| `Controllers/RolesController.cs` | 5 个角色管理端点 |
| `Controllers/PermissionsController.cs` | 1 个权限查询端点 |
| `Program.cs` | 注册 `IUserService`/`IRoleService`/`IPermissionService` + 权限 Policy |
| `Services/PermissionPolicyProvider.cs` | 自定义授权策略（从 JWT claims 的 `perm_codes` 校验） |

---

## 4. 前端设计

### 4.1 菜单重构（`routes.ts`）

删除全部 demo 菜单（dashboard/visualization/list/form/profile/result/exception/user），替换为：

```typescript
export const routes: IRoute[] = [
  {
    name: 'menu.system',
    key: 'system',
    children: [
      { name: 'menu.system.user', key: 'system/user' },
      { name: 'menu.system.role', key: 'system/role' },
      { name: 'menu.system.permission', key: 'system/permission' },
    ],
  },
];
```

同步更新 `locale/index.ts`，新增系统管理相关 i18n 条目，删除 demo 菜单的 i18n 条目。

### 4.2 页面结构

三个页面统一采用「表格 + 抽屉」模式：

#### 用户管理页（`pages/system/user/`）

```
┌─────────────────────────────────────────────────────┐
│ 用户管理          [搜索框]              [+ 新增用户] │
├─────────────────────────────────────────────────────┤
│ 用户名 │ 显示名 │ 邮箱 │ 角色 │ 状态 │ 操作          │
│ admin  │ 管理员 │ —    │ admin│ 启用 │ 编辑 重置密码  │
│ ...    │        │      │      │      │ 禁用          │
├─────────────────────────────────────────────────────┤
│                    < 1 2 3 >                        │
└─────────────────────────────────────────────────────┘
```

- 表格列：用户名 / 显示名 / 邮箱 / 角色名（逗号分隔）/ 状态（启用-绿色Tag / 禁用-灰色Tag）/ 操作
- 操作列：编辑（打开编辑抽屉）/ 重置密码（打开重置抽屉）/ 禁用|启用（Popconfirm 确认）
- 搜索：按用户名或显示名模糊搜索
- 新增/编辑：**Drawer 抽屉**（从右侧滑出）

#### 用户新增/编辑抽屉

```
                                ┌──────────────────┐
                                │ 新增用户      [×] │
                                │                  │
                                │ 用户名 *         │
                                │ [____________]   │  ← 编辑时只读
                                │                  │
                                │ 显示名 *         │
                                │ [____________]   │
                                │                  │
                                │ 邮箱             │
                                │ [____________]   │
                                │                  │
                                │ 初始密码 *       │  ← 仅新增时显示
                                │ [____________]   │
                                │                  │
                                │ 分配角色         │
                                │ [Select 多选]    │
                                │                  │
                                │ 启用状态  [开关]  │  ← 仅编辑时显示
                                │                  │
                                │    [取消] [保存]  │
                                └──────────────────┘
```

#### 重置密码抽屉

```
                                ┌──────────────────┐
                                │ 重置密码      [×] │
                                │                  │
                                │ 用户：admin      │
                                │                  │
                                │ 新密码 *         │
                                │ [____________]   │
                                │                  │
                                │ 确认密码 *       │
                                │ [____________]   │
                                │                  │
                                │    [取消] [保存]  │
                                └──────────────────┘
```

#### 角色管理页（`pages/system/role/`）

- 表格列：角色名 / 编码 / 描述 / 用户数 / 权限数 / 操作
- 操作列：编辑（抽屉）/ 删除（Popconfirm，含"是否有关联用户"提示）
- 新增/编辑：Drawer 抽屉（名称/编码/描述 + 权限分配树）

#### 角色新增/编辑抽屉

```
                                ┌──────────────────┐
                                │ 编辑角色      [×] │
                                │                  │
                                │ 角色名 *         │
                                │ [____________]   │
                                │                  │
                                │ 编码 *           │  ← 编辑时只读
                                │ [____________]   │
                                │                  │
                                │ 描述             │
                                │ [____________]   │
                                │                  │
                                │ 分配权限         │
                                │ ┌ 系统管理 ────┐ │
                                │   ☑ 管理用户   │ │
                                │   ☑ 管理角色   │ │
                                │ ├ 面料 ───────┤ │
                                │   ☑ 查看面料   │ │
                                │   ☐ 录入面料   │ │
                                │ └──────────────┘ │
                                │                  │
                                │    [取消] [保存]  │
                                └──────────────────┘
```

权限分配用 Arco `Tree` 组件，按模块分组（系统管理/面料/原料/设备/客户/颜色/产品），支持勾选。

#### 权限列表页（`pages/system/permission/`）

- 只读表格：权限编码 / 名称 / 描述
- 无操作列，无新增按钮

### 4.3 新增前端文件

```
src/api/user.ts                    # 用户 CRUD API
src/api/role.ts                    # 角色 CRUD API
src/api/permission.ts              # 权限列表 API
src/pages/system/user/index.tsx    # 用户管理页
src/pages/system/user/locale/      # 用户页 i18n
src/pages/system/role/index.tsx    # 角色管理页
src/pages/system/role/locale/      # 角色页 i18n
src/pages/system/permission/index.tsx  # 权限列表页
src/pages/system/permission/locale/    # 权限页 i18n
```

### 4.4 使用的 Arco 组件

Table（表格）、Drawer（抽屉）、Form（表单）、Input、Select（角色多选）、Tree（权限树）、Switch（启用状态）、Tag（状态标签）、Popconfirm（删除/禁用确认）、Message（操作反馈）。

---

## 5. 关键设计决策

### 5.1 权限是代码预置的，不可运行时增删

权限（Permission）的本质是控制代码行为——前端菜单/按钮的可见性、后端接口的访问控制，都是写在代码里的。运行时"新建"一个权限不会有任何效果（没有代码检查它）。因此：
- 权限 code 随业务模块开发时在 SeedData 里预置，随版本发布
- 管理员只能"分配"已有权限给角色，不能创建新权限
- 权限列表页是只读的

**开发约定：** 每新增一个业务模块，需同步在 SeedData 添加对应权限 code，并在前端绑定到菜单/按钮。

### 5.2 用户名创建后不可修改

用户名是系统内的唯一标识，被日志、审计、关联引用。创建后锁定，编辑时该字段只读。

### 5.3 密码单独管理，不在编辑表单里

编辑用户信息（改显示名/邮箱/角色）时不应被迫设置密码。密码通过独立的"重置密码"入口管理。新增用户时需填初始密码。

### 5.4 admin 账号保护

- admin 用户的 `isActive` 不可设为 false（防止把自己锁在外面）
- admin 用户的角色不可移除 admin 角色
- 后端 UserService 中强制校验，前端禁用对应操作

### 5.5 角色删除前校验

删除角色前检查是否有用户仍绑定该角色：
- 有关联用户 → 拒绝删除，返回错误提示"该角色下还有 N 个用户，请先解绑"
- 无关联用户 → 允许删除（同时清理 role_permissions 关联）

### 5.6 admin 角色的 code 不可重复或删除

`admin` 角色（code = "admin"）是系统内置超级管理员角色，不可删除。后端 RoleService.Delete 校验。

---

## 6. 后端权限校验机制

### 6.1 基于 JWT claims 的 Policy 授权

认证系统中 JWT 已携带 `perm_codes` claim（admin 为 `["*"]`）。系统管理接口通过自定义授权策略校验：

```csharp
// Program.cs 注册策略
builder.Services.AddAuthorization(options =>
{
    options.AddPolicy("user-manage", policy =>
        policy.RequireClaim("perm_codes", "system:user:manage"));
    options.AddPolicy("role-manage", policy =>
        policy.RequireClaim("perm_codes", "system:role:manage"));
});
```

> **admin 通配处理：** admin 的 `perm_codes` 是 `["*"]`，不包含具体的 `system:user:manage`。需要自定义 `IAuthorizationHandler` 处理 `*` 通配——如果用户 claims 含 `*`，直接放行所有策略。

### 6.2 Controller 上标注

```csharp
[Authorize(Policy = "user-manage")]
public class UsersController : ControllerBase { ... }

[Authorize(Policy = "role-manage")]
public class RolesController : ControllerBase { ... }

[Authorize]  // 仅需登录
public class PermissionsController : ControllerBase { ... }
```

---

## 7. 验证标准（Definition of Done）

1. **菜单**：登录后左侧菜单只有「系统管理」一级菜单 + 三个子菜单，demo 菜单全部消失。
2. **用户管理**：能分页查看用户列表、搜索、新增用户（填表单→保存→列表刷新）、编辑用户、重置密码、禁用/启用。
3. **角色管理**：能查看角色列表、新增角色、编辑角色、为角色分配权限（树形勾选）、删除无关联用户的角色。
4. **权限列表**：能查看所有权限 code 及名称（只读）。
5. **权限隔离**：用 developer 账号登录看不到系统管理菜单（无 system 权限）。
6. **admin 保护**：不能禁用 admin 用户、不能删除 admin 角色。
7. **API 鉴权**：不带 token 或权限不足访问 `/api/users` 返回 401/403。
8. **端到端**：新增一个用户 → 分配 developer 角色 → 用该用户登录 → 验证其看到的菜单符合 developer 权限。

---

## 8. 后续演进

| 顺序 | 内容 |
|------|------|
| 1 | 用户自己修改个人信息 / 修改密码 |
| 2 | 操作日志（谁在什么时候做了什么操作） |
| 3 | 登录日志 |
| 4 | 角色按模块分组的批量权限模板 |
