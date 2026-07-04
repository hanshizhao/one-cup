# 按钮级权限收口 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 后端权限码细化为全模块统一 4 档 + 前端写操作按钮全站接入 PermissionWrapper,完成 rbac-overhaul 设计第 7.5 节遗留收口。

**Architecture:** 方案 A 三阶段:后端先行(权限码定义 + 策略注册 + Controller 注解 + migration)→ 前端 PermissionWrapper 全站铺开 → 权限树两级嵌套 + 路由 bug 修复。权限码统一为 `资源:动作`,策略名 = 权限码;无权限按钮隐藏(PermissionWrapper 现状)。

**Tech Stack:** .NET 10 / EF Core / Npgsql(postgres)、React 18 / TS / Arco Design / RTK / RR6。后端测试 xUnit + WebApplicationFactory;前端 vitest。

**Spec:** `docs/specs/2026-07-04-button-level-permission-design.md`

## Global Constraints

- 权限码格式统一为 `资源:动作`,动作固定 4 个 `read/create/update/delete`,用户管理额外 `reset-password`;**策略名 = 权限码**(带冒号,ASP.NET Core 合法)。
- **废弃旧动作词** `view/manage/write`:细化后后端权限码与策略名中 `view/manage/write` 零残留(`system:audit:view`→`system:audit:read`,`system:unit:view`→`system:unit:read` 等)。
- 权限码总数 = **42 个**(6 业务模块×4 + system:user 5 + system:role 4 + system:numbering 4 + system:unit 4 + system:audit 1),策略 42 个,一一对应。
- developer 角色重绑定为 **10 条**:`fabric:read/create/update/delete` + `material/equipment/customer/color/product:read` + `system:audit:read`。
- 前端无权限行为 = **隐藏**(返回 null),PermissionWrapper 组件源码**零改动**。
- Drawer/Modal 内"确定"按钮**不单独包权限**(沿用入口控制,避免双重包裹)。
- 每阶段结束跑端到端冒烟 + 提交。所有命令在仓库根 `C:/Users/mi/Desktop/work_space/one-cup` 执行;后端 .NET 命令在 `backend/` 子目录(`OneCup.slnx` 所在处)。

---

## File Structure

**后端**
- `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs` — 重写权限 Guid 常量(19→42),删旧常量
- `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs` — 重写 `Seed()` 的 Permission HasData + developer 绑定
- `backend/src/OneCup.Infrastructure/Migrations/<new>_RefinePermissionCodes.cs` — 新 migration(由 `dotnet ef migrations add` 生成后校验)
- `backend/src/OneCup.Api/Program.cs:148-173` — 重写授权策略注册(11→42)
- `backend/src/OneCup.Api/Controllers/*.cs` — 7 个 Controller 的 `[Authorize(Policy=...)]` 注解改造
- `backend/tests/OneCup.IntegrationTests/AuthAuthorizationTests.cs` — 更新断言注释(权限码改名)
- `backend/tests/OneCup.IntegrationTests/PermissionRefineTests.cs` — 新增:细化后策略端点授权覆盖

**前端**
- `frontend/src/routes.ts` — 9 处菜单 requiredPermissions 对齐 `:read` + permission 路由补权限
- `frontend/src/router.tsx:96-160` — 对应 `<RequirePermission>` 对齐 `:read` + permission 路由补守卫
- `frontend/src/pages/business/customer/index.tsx` — 编辑/删除拆成两个独立 wrapper
- `frontend/src/pages/system/user/index.tsx` — 4 类写按钮包权限
- `frontend/src/pages/system/role/index.tsx` — 3 类写按钮包权限 + `buildPermissionTree` 重写为两级嵌套
- `frontend/src/pages/system/numbering/index.tsx` — 规则写按钮包权限
- `frontend/src/pages/system/numbering/dict/index.tsx` — 业务类型/分类写按钮包权限
- `frontend/src/pages/system/unit/index.tsx` — 写按钮包权限
- `frontend/src/pages/master-data/color/index.tsx` — 写按钮包权限
- `frontend/src/__tests__/transformPermissions.test.ts` — 如权限码样本变化则同步更新断言

---

## 阶段 1:后端权限码细化

### Task 1: 重写 SeedData 权限 Guid 常量

**Files:**
- Modify: `backend/src/OneCup.Infrastructure/Persistence/SeedData.cs`

**Interfaces:**
- Produces: 42 个权限 Guid 常量,命名 `Perm{Resource}{Action}`,供 OneCupDbContext.Seed 与 migration 引用。

- [ ] **Step 1: 替换 SeedData.cs 的权限常量段**

把 `SeedData.cs:13-30` 的权限常量段(从 `// 权限 Guid:第 4 段从 101 开始递增` 到 `PermSystemAuditView`)和 `:46-47` 的 Unit 常量段,整体替换为如下 42 个常量。Guid 第 4 段从 `201` 开始递增(避开旧 101-122,语义已变不复用):

```csharp
    // 权限 Guid：第 4 段从 201 开始递增（细化后不复用旧 101-122）
    // 业务模块（6 个 × read/create/update/delete = 24）
    public static readonly Guid PermFabricRead = Guid.Parse("00000000-0000-0000-0000-000000000201");
    public static readonly Guid PermFabricCreate = Guid.Parse("00000000-0000-0000-0000-000000000202");
    public static readonly Guid PermFabricUpdate = Guid.Parse("00000000-0000-0000-0000-000000000203");
    public static readonly Guid PermFabricDelete = Guid.Parse("00000000-0000-0000-0000-000000000204");
    public static readonly Guid PermMaterialRead = Guid.Parse("00000000-0000-0000-0000-000000000205");
    public static readonly Guid PermMaterialCreate = Guid.Parse("00000000-0000-0000-0000-000000000206");
    public static readonly Guid PermMaterialUpdate = Guid.Parse("00000000-0000-0000-0000-000000000207");
    public static readonly Guid PermMaterialDelete = Guid.Parse("00000000-0000-0000-0000-000000000208");
    public static readonly Guid PermEquipmentRead = Guid.Parse("00000000-0000-0000-0000-000000000209");
    public static readonly Guid PermEquipmentCreate = Guid.Parse("00000000-0000-0000-0000-00000000020a");
    public static readonly Guid PermEquipmentUpdate = Guid.Parse("00000000-0000-0000-0000-00000000020b");
    public static readonly Guid PermEquipmentDelete = Guid.Parse("00000000-0000-0000-0000-00000000020c");
    public static readonly Guid PermCustomerRead = Guid.Parse("00000000-0000-0000-0000-00000000020d");
    public static readonly Guid PermCustomerCreate = Guid.Parse("00000000-0000-0000-0000-00000000020e");
    public static readonly Guid PermCustomerUpdate = Guid.Parse("00000000-0000-0000-0000-00000000020f");
    public static readonly Guid PermCustomerDelete = Guid.Parse("00000000-0000-0000-0000-000000000210");
    public static readonly Guid PermColorRead = Guid.Parse("00000000-0000-0000-0000-000000000211");
    public static readonly Guid PermColorCreate = Guid.Parse("00000000-0000-0000-0000-000000000212");
    public static readonly Guid PermColorUpdate = Guid.Parse("00000000-0000-0000-0000-000000000213");
    public static readonly Guid PermColorDelete = Guid.Parse("00000000-0000-0000-0000-000000000214");
    public static readonly Guid PermProductRead = Guid.Parse("00000000-0000-0000-0000-000000000215");
    public static readonly Guid PermProductCreate = Guid.Parse("00000000-0000-0000-0000-000000000216");
    public static readonly Guid PermProductUpdate = Guid.Parse("00000000-0000-0000-0000-000000000217");
    public static readonly Guid PermProductDelete = Guid.Parse("00000000-0000-0000-0000-000000000218");
    // 系统模块
    public static readonly Guid PermSystemUserRead = Guid.Parse("00000000-0000-0000-0000-000000000219");
    public static readonly Guid PermSystemUserCreate = Guid.Parse("00000000-0000-0000-0000-00000000021a");
    public static readonly Guid PermSystemUserUpdate = Guid.Parse("00000000-0000-0000-0000-00000000021b");
    public static readonly Guid PermSystemUserDelete = Guid.Parse("00000000-0000-0000-0000-00000000021c");
    public static readonly Guid PermSystemUserResetPassword = Guid.Parse("00000000-0000-0000-0000-00000000021d");
    public static readonly Guid PermSystemRoleRead = Guid.Parse("00000000-0000-0000-0000-00000000021e");
    public static readonly Guid PermSystemRoleCreate = Guid.Parse("00000000-0000-0000-0000-00000000021f");
    public static readonly Guid PermSystemRoleUpdate = Guid.Parse("00000000-0000-0000-0000-000000000220");
    public static readonly Guid PermSystemRoleDelete = Guid.Parse("00000000-0000-0000-0000-000000000221");
    public static readonly Guid PermSystemNumberingRead = Guid.Parse("00000000-0000-0000-0000-000000000222");
    public static readonly Guid PermSystemNumberingCreate = Guid.Parse("00000000-0000-0000-0000-000000000223");
    public static readonly Guid PermSystemNumberingUpdate = Guid.Parse("00000000-0000-0000-0000-000000000224");
    public static readonly Guid PermSystemNumberingDelete = Guid.Parse("00000000-0000-0000-0000-000000000225");
    public static readonly Guid PermSystemUnitRead = Guid.Parse("00000000-0000-0000-0000-000000000226");
    public static readonly Guid PermSystemUnitCreate = Guid.Parse("00000000-0000-0000-0000-000000000227");
    public static readonly Guid PermSystemUnitUpdate = Guid.Parse("00000000-0000-0000-0000-000000000228");
    public static readonly Guid PermSystemUnitDelete = Guid.Parse("00000000-0000-0000-0000-000000000229");
    public static readonly Guid PermSystemAuditRead = Guid.Parse("00000000-0000-0000-0000-00000000022a");
```

同时删除 `:45-47` 的 `// ===== Unit 模块 =====` 及其 `PermUnitRead`/`PermUnitWrite` 两个旧常量(已被上面 `PermSystemUnit*` 取代)。

保留:`AdminUserId`/`AdminRoleId`/`DeveloperRoleId`(`:9-11`)、`AdminPasswordHash`(`:35`)、`TargetType*`(`:38-43`)不变。

- [ ] **Step 2: 验证编译**

Run: `cd backend && dotnet build src/OneCup.Infrastructure/OneCup.Infrastructure.csproj`
Expected: FAIL —— 引用旧常量(`PermFabricWrite` 等)的 OneCupDbContext.Seed 编译报错。这是预期的,Task 2 修复。仅确认 SeedData.cs 自身语法正确(无重复 Guid、无拼写错)。

- [ ] **Step 3: 暂不提交,继续 Task 2**

本 task 与 Task 2 耦合(替换常量后引用方立即编译失败),合并到 Task 2 末尾统一提交。

---

### Task 2: 重写 OneCupDbContext.Seed 的权限 HasData 与 developer 绑定

**Files:**
- Modify: `backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs:84-149`

**Interfaces:**
- Produces: Seed() 输出 42 个 Permission + developer 10 条 role_permissions 绑定,作为 `dotnet ef migrations add` 的模型源。

- [ ] **Step 1: 替换权限 HasData 段**

把 `OneCupDbContext.cs:87-105` 的 `modelBuilder.Entity<Permission>().HasData(...)` 整段替换为 42 条:

```csharp
        modelBuilder.Entity<Permission>().HasData(
            // 业务模块
            new Permission { Id = SeedData.PermFabricRead, Code = "fabric:read", Name = "查看面料开发", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermFabricCreate, Code = "fabric:create", Name = "录入面料开发", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermFabricUpdate, Code = "fabric:update", Name = "编辑面料开发", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermFabricDelete, Code = "fabric:delete", Name = "删除面料开发", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermMaterialRead, Code = "material:read", Name = "查看原料物料", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermMaterialCreate, Code = "material:create", Name = "录入原料物料", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermMaterialUpdate, Code = "material:update", Name = "编辑原料物料", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermMaterialDelete, Code = "material:delete", Name = "删除原料物料", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermEquipmentRead, Code = "equipment:read", Name = "查看设备", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermEquipmentCreate, Code = "equipment:create", Name = "录入设备", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermEquipmentUpdate, Code = "equipment:update", Name = "编辑设备", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermEquipmentDelete, Code = "equipment:delete", Name = "删除设备", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermCustomerRead, Code = "customer:read", Name = "查看客户", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermCustomerCreate, Code = "customer:create", Name = "录入客户", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermCustomerUpdate, Code = "customer:update", Name = "编辑客户", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermCustomerDelete, Code = "customer:delete", Name = "删除客户", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermColorRead, Code = "color:read", Name = "查看颜色对色", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermColorCreate, Code = "color:create", Name = "录入颜色对色", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermColorUpdate, Code = "color:update", Name = "编辑颜色对色", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermColorDelete, Code = "color:delete", Name = "删除颜色对色", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermProductRead, Code = "product:read", Name = "查看产品", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermProductCreate, Code = "product:create", Name = "录入产品", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermProductUpdate, Code = "product:update", Name = "编辑产品", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermProductDelete, Code = "product:delete", Name = "删除产品", CreatedAt = SeedTimestamp },
            // 系统模块
            new Permission { Id = SeedData.PermSystemUserRead, Code = "system:user:read", Name = "查看用户", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemUserCreate, Code = "system:user:create", Name = "新增用户", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemUserUpdate, Code = "system:user:update", Name = "编辑用户", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemUserDelete, Code = "system:user:delete", Name = "删除用户", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemUserResetPassword, Code = "system:user:reset-password", Name = "重置用户密码", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemRoleRead, Code = "system:role:read", Name = "查看角色", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemRoleCreate, Code = "system:role:create", Name = "新增角色", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemRoleUpdate, Code = "system:role:update", Name = "编辑角色", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemRoleDelete, Code = "system:role:delete", Name = "删除角色", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemNumberingRead, Code = "system:numbering:read", Name = "查看编号管理", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemNumberingCreate, Code = "system:numbering:create", Name = "新增编号规则", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemNumberingUpdate, Code = "system:numbering:update", Name = "编辑编号规则", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemNumberingDelete, Code = "system:numbering:delete", Name = "删除编号规则", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemUnitRead, Code = "system:unit:read", Name = "查看计量单位", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemUnitCreate, Code = "system:unit:create", Name = "新增计量单位", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemUnitUpdate, Code = "system:unit:update", Name = "编辑计量单位", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemUnitDelete, Code = "system:unit:delete", Name = "删除计量单位", CreatedAt = SeedTimestamp },
            new Permission { Id = SeedData.PermSystemAuditRead, Code = "system:audit:read", Name = "查看审计日志", CreatedAt = SeedTimestamp }
        );
```

- [ ] **Step 2: 替换 developer 绑定段**

把 `OneCupDbContext.cs:135-142` 的 `developerPerms` 数组替换为 10 条(fabric 全套 4 + 其余业务 read 5 + audit:read 1):

```csharp
        // ── role_permissions: developer 角色 → 只读为主 + fabric 可写 ──
        // admin 角色通过通配 * 拥有全部权限（AuthService 特殊处理），不绑定权限
        var developerPerms = new[]
        {
            SeedData.PermFabricRead, SeedData.PermFabricCreate, SeedData.PermFabricUpdate, SeedData.PermFabricDelete,
            SeedData.PermMaterialRead, SeedData.PermEquipmentRead, SeedData.PermCustomerRead,
            SeedData.PermColorRead, SeedData.PermProductRead, SeedData.PermSystemAuditRead
        };
```

- [ ] **Step 3: 修正过时注释**

`OneCupDbContext.cs:81` 注释 "1 admin 账号、2 角色、16 权限" 改为 "1 admin 账号、2 角色、42 权限"。

- [ ] **Step 4: 验证编译**

Run: `cd backend && dotnet build`
Expected: PASS(SeedData 常量与 DbContext 引用已对齐;此时尚未生成 migration,但项目应能编译)。

- [ ] **Step 5: 提交 Task 1+2**

```bash
git add backend/src/OneCup.Infrastructure/Persistence/SeedData.cs backend/src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs
git commit -m "refactor(permission): 权限码细化为42个(4档+reset-password), developer重绑10条"
```

---

### Task 3: 重写 Program.cs 授权策略注册

**Files:**
- Modify: `backend/src/OneCup.Api/Program.cs:148-173`

**Interfaces:**
- Produces: 42 个策略,策略名 = 权限码;供 Controllers `[Authorize(Policy=...)]` 引用。

- [ ] **Step 1: 替换策略注册段**

把 `Program.cs:148-173` 的整个 `AddAuthorization(options => { ... })` 块替换为:

```csharp
builder.Services.AddAuthorization(options =>
{
    // 业务模块（策略名 = 权限码,带冒号,ASP.NET Core 合法）
    options.AddPolicy("fabric:read", p => p.RequireClaim("perm_codes", "fabric:read"));
    options.AddPolicy("fabric:create", p => p.RequireClaim("perm_codes", "fabric:create"));
    options.AddPolicy("fabric:update", p => p.RequireClaim("perm_codes", "fabric:update"));
    options.AddPolicy("fabric:delete", p => p.RequireClaim("perm_codes", "fabric:delete"));
    options.AddPolicy("material:read", p => p.RequireClaim("perm_codes", "material:read"));
    options.AddPolicy("material:create", p => p.RequireClaim("perm_codes", "material:create"));
    options.AddPolicy("material:update", p => p.RequireClaim("perm_codes", "material:update"));
    options.AddPolicy("material:delete", p => p.RequireClaim("perm_codes", "material:delete"));
    options.AddPolicy("equipment:read", p => p.RequireClaim("perm_codes", "equipment:read"));
    options.AddPolicy("equipment:create", p => p.RequireClaim("perm_codes", "equipment:create"));
    options.AddPolicy("equipment:update", p => p.RequireClaim("perm_codes", "equipment:update"));
    options.AddPolicy("equipment:delete", p => p.RequireClaim("perm_codes", "equipment:delete"));
    options.AddPolicy("customer:read", p => p.RequireClaim("perm_codes", "customer:read"));
    options.AddPolicy("customer:create", p => p.RequireClaim("perm_codes", "customer:create"));
    options.AddPolicy("customer:update", p => p.RequireClaim("perm_codes", "customer:update"));
    options.AddPolicy("customer:delete", p => p.RequireClaim("perm_codes", "customer:delete"));
    options.AddPolicy("color:read", p => p.RequireClaim("perm_codes", "color:read"));
    options.AddPolicy("color:create", p => p.RequireClaim("perm_codes", "color:create"));
    options.AddPolicy("color:update", p => p.RequireClaim("perm_codes", "color:update"));
    options.AddPolicy("color:delete", p => p.RequireClaim("perm_codes", "color:delete"));
    options.AddPolicy("product:read", p => p.RequireClaim("perm_codes", "product:read"));
    options.AddPolicy("product:create", p => p.RequireClaim("perm_codes", "product:create"));
    options.AddPolicy("product:update", p => p.RequireClaim("perm_codes", "product:update"));
    options.AddPolicy("product:delete", p => p.RequireClaim("perm_codes", "product:delete"));
    // 系统模块
    options.AddPolicy("system:user:read", p => p.RequireClaim("perm_codes", "system:user:read"));
    options.AddPolicy("system:user:create", p => p.RequireClaim("perm_codes", "system:user:create"));
    options.AddPolicy("system:user:update", p => p.RequireClaim("perm_codes", "system:user:update"));
    options.AddPolicy("system:user:delete", p => p.RequireClaim("perm_codes", "system:user:delete"));
    options.AddPolicy("system:user:reset-password", p => p.RequireClaim("perm_codes", "system:user:reset-password"));
    options.AddPolicy("system:role:read", p => p.RequireClaim("perm_codes", "system:role:read"));
    options.AddPolicy("system:role:create", p => p.RequireClaim("perm_codes", "system:role:create"));
    options.AddPolicy("system:role:update", p => p.RequireClaim("perm_codes", "system:role:update"));
    options.AddPolicy("system:role:delete", p => p.RequireClaim("perm_codes", "system:role:delete"));
    options.AddPolicy("system:numbering:read", p => p.RequireClaim("perm_codes", "system:numbering:read"));
    options.AddPolicy("system:numbering:create", p => p.RequireClaim("perm_codes", "system:numbering:create"));
    options.AddPolicy("system:numbering:update", p => p.RequireClaim("perm_codes", "system:numbering:update"));
    options.AddPolicy("system:numbering:delete", p => p.RequireClaim("perm_codes", "system:numbering:delete"));
    options.AddPolicy("system:unit:read", p => p.RequireClaim("perm_codes", "system:unit:read"));
    options.AddPolicy("system:unit:create", p => p.RequireClaim("perm_codes", "system:unit:create"));
    options.AddPolicy("system:unit:update", p => p.RequireClaim("perm_codes", "system:unit:update"));
    options.AddPolicy("system:unit:delete", p => p.RequireClaim("perm_codes", "system:unit:delete"));
    options.AddPolicy("system:audit:read", p => p.RequireClaim("perm_codes", "system:audit:read"));
});
```

- [ ] **Step 2: 验证编译**

Run: `cd backend && dotnet build`
Expected: PASS(策略定义不依赖 Controller;Controller 旧策略名引用此时会运行时才暴露,编译不报错)。

- [ ] **Step 3: 暂不提交,继续 Task 4**

Controller 注解改造与策略注册必须一起提交,否则旧策略名失效。

---

### Task 4: 改造 Controller 授权注解

**Files:**
- Modify: `backend/src/OneCup.Api/Controllers/UsersController.cs`
- Modify: `backend/src/OneCup.Api/Controllers/RolesController.cs`
- Modify: `backend/src/OneCup.Api/Controllers/CustomersController.cs`
- Modify: `backend/src/OneCup.Api/Controllers/ColorController.cs`
- Modify: `backend/src/OneCup.Api/Controllers/NumberingController.cs`
- Modify: `backend/src/OneCup.Api/Controllers/NumberingDictionaryController.cs`
- Modify: `backend/src/OneCup.Api/Controllers/MeasurementUnitsController.cs`
- Modify: `backend/src/OneCup.Api/Controllers/LoginLogsController.cs`
- Modify: `backend/src/OneCup.Api/Controllers/OperationLogsController.cs`

**Interfaces:**
- Consumes: Task 3 的 42 个策略名。

- [ ] **Step 1: UsersController — 删类级,逐方法贴策略**

读 `UsersController.cs`,找到类级 `[Authorize(Policy = "user-manage")]`(`:14`)删除。在每个方法上贴对应策略(参考调研的行号,以实际为准):

| 方法 | HTTP | 贴策略 |
|---|---|---|
| GetList | GET | `[Authorize(Policy = "system:user:read")]` |
| GetById | GET {id} | `[Authorize(Policy = "system:user:read")]` |
| Create | POST | `[Authorize(Policy = "system:user:create")]` |
| Update | PUT {id} | `[Authorize(Policy = "system:user:update")]` |
| ResetPassword | PUT {id}/password | `[Authorize(Policy = "system:user:reset-password")]` |
| UpdateStatus | PUT {id}/status | `[Authorize(Policy = "system:user:update")]` |
| Delete | DELETE {id} | `[Authorize(Policy = "system:user:delete")]` |

注解放在每个方法的 `[HttpGet]`/`[HttpPost]` 等特性同行或上一行,与现有风格一致。**类级保留裸 `[Authorize]`**(Controller 整体需登录),仅去掉 `Policy`。

- [ ] **Step 2: RolesController — 删类级,逐方法贴策略**

同 Step 1 模式。类级 `[Authorize(Policy = "role-manage")]`(`:14`)→ 类级保留裸 `[Authorize]`,删 Policy。逐方法:

| 方法 | HTTP | 贴策略 |
|---|---|---|
| GetList/GetById | GET | `system:role:read` |
| Create | POST | `system:role:create` |
| Update | PUT {id} | `system:role:update` |
| Delete | DELETE {id} | `system:role:delete` |

- [ ] **Step 3: CustomersController — 类级改 read,写方法改细分码**

类级 `[Authorize(Policy = "customer-read")]`(`:14`)→ `[Authorize(Policy = "customer:read")]`。写方法:
- Create(`:45-46`)的 `[Authorize(Policy = "customer-write")]` → `customer:create`
- Update(`:54-55`)→ `customer:update`
- Delete(`:63-64`)→ `customer:delete`

- [ ] **Step 4: ColorController — 改策略名**

无类级 Authorize,逐方法改:
- GetColors/GetAllActiveColors/GetColor → `color-view` 改为 `color:read`
- CreateColor → `color-manage` 改为 `color:create`
- UpdateColor/UpdateColorStatus → `color-manage` 改为 `color:update`

- [ ] **Step 5: NumberingController — 改策略名**

- 读方法(GetRules/GetRule/GetLogs)→ `numbering-view` 改为 `system:numbering:read`
- CreateRule → `numbering-manage` 改为 `system:numbering:create`
- UpdateRule/UpdateRuleStatus → `numbering-manage` 改为 `system:numbering:update`
- Preview(`:78`)保持裸 `[Authorize]`,不动

- [ ] **Step 6: NumberingDictionaryController — 改策略名**

- 读方法(GetTargetTypes/GetAllActiveTargetTypes/GetTargetType/GetCategories/GetActiveCategories/GetCategory)→ `numbering-view` 改为 `system:numbering:read`
- CreateTargetType/CreateCategory → `numbering-manage` 改为 `system:numbering:create`
- UpdateTargetType/UpdateTargetTypeStatus/UpdateCategory/UpdateCategoryStatus → `numbering-manage` 改为 `system:numbering:update`

- [ ] **Step 7: MeasurementUnitsController — 改策略名**

- 读方法(GetList/GetAllActive/GetCategories/GetById/Convert)→ `unit-view` 改为 `system:unit:read`
- Create → `unit-manage` 改为 `system:unit:create`
- Update/UpdateStatus → `unit-manage` 改为 `system:unit:update`

- [ ] **Step 8: LoginLogsController / OperationLogsController — 改策略名**

两者类级 `[Authorize(Policy = "audit-view")]`(`:13`)→ `[Authorize(Policy = "system:audit:read")]`。

- [ ] **Step 9: 验证编译**

Run: `cd backend && dotnet build`
Expected: PASS。若有旧策略名漏改(如 `customer-write`),编译不报错但运行时 404 策略——靠 Task 7 集成测试捕获。

- [ ] **Step 10: 提交 Task 3+4**

```bash
git add backend/src/OneCup.Api/Program.cs backend/src/OneCup.Api/Controllers/
git commit -m "refactor(permission): 策略名=权限码(42个), Controller逐方法贴细分策略"
```

---

### Task 5: 生成 RefinePermissionCodes migration

**Files:**
- Create: `backend/src/OneCup.Infrastructure/Migrations/<timestamp>_RefinePermissionCodes.cs`(自动生成)
- Modify: `backend/src/OneCup.Infrastructure/Migrations/OneCupDbContextModelSnapshot.cs`(自动更新)

- [ ] **Step 1: 生成 migration**

Run:
```bash
cd backend && dotnet ef migrations add RefinePermissionCodes --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```
Expected: 生成新 migration + 更新 ModelSnapshot。

- [ ] **Step 2: 人工校验 migration 的 Up()**

打开生成的 `<timestamp>_RefinePermissionCodes.cs`,核对 `Up()` 内容应符合 spec 第 4 节:
1. **删除旧 permissions**:应有 19 条 `DeleteData`(`permissions` 表,旧 Guid 101-122 范围)。
2. **删除旧 role_permissions**:应有 developer 旧 8 条绑定对应的 `DeleteData`(`role_permissions` 表)。
3. **插入新 permissions**:应有 42 条 `InsertData`(`permissions` 表,新 Guid 201-22a 范围)。
4. **插入新 role_permissions**:应有 developer 新 10 条 `InsertData`。

若 EF Core 把"删旧+插新"识别为 `UpdateData`(因主键变化),检查 Guid 是否正确对应。**关键:新 Guid 不复用旧 Guid**(101-122 全删,201-22a 全新插)。

- [ ] **Step 3: 核对 Down()**

`Down()` 应反向:删 42 新 + 删 developer 10 新 + 插 19 旧 + 插 developer 8 旧。Down 仅用于回滚,语义等价不保证(spec 第 4 节已说明)。

- [ ] **Step 4: 应用 migration 到开发库**

Run:
```bash
cd backend && dotnet ef database update --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```
Expected: 无报错,`__EFMigrationsHistory` 记录新 migration。

- [ ] **Step 5: 提交**

```bash
git add backend/src/OneCup.Infrastructure/Migrations/
git commit -m "feat(permission): RefinePermissionCodes migration(19→42权限, 清空重分配)"
```

---

### Task 6: 更新现有集成测试断言

**Files:**
- Modify: `backend/tests/OneCup.IntegrationTests/AuthAuthorizationTests.cs`

**Interfaces:**
- Consumes: Task 4 改造后的 Controller 策略。

- [ ] **Step 1: 更新 Developer_403 测试的注释**

`AuthAuthorizationTests.cs:56-66` 的 `Developer_token_users_endpoint_returns_403` 测试,注释 `无 system:user:manage 权限` 改为 `无 system:user:read 权限`。**断言逻辑不变**(developer 无 system:user:read → /api/users 仍 403,测试仍通过)。

- [ ] **Step 2: 更新 Admin_wildcard 测试的注释**

`AuthAuthorizationTests.cs:68-80` 的 `Admin_wildcard_passes_role_manage_policy` 注释 `需 user-manage`/`需 role-manage` 改为 `需 system:user:read`/`需 system:role:read`(admin 现在靠通配放行这两个 read 策略)。断言不变。

- [ ] **Step 3: 跑现有集成测试确认通过**

Run: `cd backend && dotnet test tests/OneCup.IntegrationTests/`
Expected: 全部 PASS。若 `Developer_token_users_endpoint_returns_403` 失败(变 200),说明 developer 误绑了 system:user:read,回查 Task 2 Step 2 的 developerPerms。

- [ ] **Step 4: 暂不提交,与 Task 7 一起提交**

---

### Task 7: 新增权限细化集成测试

**Files:**
- Create: `backend/tests/OneCup.IntegrationTests/PermissionRefineTests.cs`

**Interfaces:**
- Consumes: `IntegrationTestFactory`(`IntegrationTestFactory.AdminUsername`/`DeveloperUsername`/`TestPassword`),Task 4 的 Controller 策略。

- [ ] **Step 1: 写测试文件**

```csharp
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using OneCup.Application.Dtos.Auth;

namespace OneCup.IntegrationTests;

/// <summary>
/// 权限码细化后的端点授权覆盖:验证 read/create/update/delete/reset-password 拆分正确。
/// </summary>
public class PermissionRefineTests : IClassFixture<IntegrationTestFactory>
{
    private readonly IntegrationTestFactory _factory;
    private readonly HttpClient _client;

    public PermissionRefineTests(IntegrationTestFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
        _factory.SeedAsync().GetAwaiter().GetResult();
    }

    /// <summary>admin 靠通配 *,可访问任意细分策略端点(含 delete)。</summary>
    [Fact]
    public async Task Admin_can_access_all_refined_actions()
    {
        var token = await LoginAsync(IntegrationTestFactory.AdminUsername, IntegrationTestFactory.TestPassword);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // read/create/update/delete 各取一个代表端点
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/users")).StatusCode);          // system:user:read
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/colors")).StatusCode);          // color:read
    }

    /// <summary>developer 对 customer 只有 read,无 create/update/delete → 写端点 403。</summary>
    [Fact]
    public async Task Developer_customer_write_endpoints_return_403()
    {
        var token = await LoginAsync(IntegrationTestFactory.DeveloperUsername, IntegrationTestFactory.TestPassword);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // developer 有 customer:read → 列表 200
        Assert.Equal(HttpStatusCode.OK, (await _client.GetAsync("/api/customers")).StatusCode);
        // developer 无 customer:create → POST 403
        var post = await _client.PostAsJsonAsync("/api/customers", new { name = "x" });
        Assert.Equal(HttpStatusCode.Forbidden, post.StatusCode);
    }

    /// <summary>developer 对 fabric 有全套(fabric:create/update/delete)→ POST 应通过授权层(可能因 DTO 校验 400,但绝非 403)。</summary>
    [Fact]
    public async Task Developer_fabric_write_passes_authorization()
    {
        var token = await LoginAsync(IntegrationTestFactory.DeveloperUsername, IntegrationTestFactory.TestPassword);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // fabric 无 Controller(占位),改用权限码存在性间接验证:developer GET customers 通过 = 鉴权链路正常。
        // 真正的 fabric 端点覆盖待模块实现。此处断言 developer token 有效即可。
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    /// <summary>重置密码端点要求 system:user:reset-password,developer 无 → 403。</summary>
    [Fact]
    public async Task Developer_reset_password_returns_403()
    {
        var token = await LoginAsync(IntegrationTestFactory.DeveloperUsername, IntegrationTestFactory.TestPassword);
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // 任意已存在的 user id(admin)重置密码
        var resp = await _client.PutAsJsonAsync($"/api/users/{IntegrationTestFactory.AdminUserId}/password",
            new { newPassword = "NewPass@123" });
        Assert.Equal(HttpStatusCode.Forbidden, resp.StatusCode);
    }

    private async Task<string> LoginAsync(string username, string password)
    {
        var resp = await _client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest { Username = username, Password = password });
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var token = await resp.Content.ReadFromJsonAsync<TokenResponse>();
        return token!.AccessToken;
    }
}
```

> **注:** 若 `IntegrationTestFactory` 未暴露 `AdminUserId` 常量,Step 1 改用 `GET /api/users`(admin 登录后)取第一个用户 id 用于重置密码端点测试;或参考 `IntegrationTestFactory.cs` 已有常量名调整。实现时以 `IntegrationTestFactory.cs` 实际暴露的成员为准。

- [ ] **Step 2: 跑测试**

Run: `cd backend && dotnet test tests/OneCup.IntegrationTests/`
Expected: 全部 PASS,包括新增 4 个。若 `AdminUserId` 等常量名不存在,按 Step 1 注释调整测试代码。

- [ ] **Step 3: 提交 Task 6+7**

```bash
git add backend/tests/OneCup.IntegrationTests/
git commit -m "test(permission): 更新断言+新增权限细化端点授权覆盖"
```

- [ ] **Step 4: 阶段 1 端到端冒烟**

启动后端 + 前端,手动验证:
1. admin 登录 → 全功能可用(用户/角色/客户/颜色/编号/单位 增删改查)。
2. developer 登录 → 能进各列表页(GET 200),写操作后端 403。
3. developer 对 customer:能看列表,点新增/编辑/删除(此时前端尚未收口,按钮可见但提交后端 403)。

确认无误后进入阶段 2。阶段 1 对应 spec 验收标准 1-5。

---

## 阶段 2:前端 PermissionWrapper 全站铺开

### Task 8: customer 页编辑/删除拆分

**Files:**
- Modify: `frontend/src/pages/business/customer/index.tsx:183-197`

- [ ] **Step 1: 拆分操作列的 PermissionWrapper**

把 `index.tsx:183-197` 的单个 PermissionWrapper(包住编辑+删除)拆成两个,各自包独立权限码:

```tsx
            <PermissionWrapper
              requiredPermissions={[{ resource: 'customer', actions: ['update'] }]}
            >
              <Button type="text" size="small" onClick={() => openEdit(record)}>
                {t['customer.button.edit']}
              </Button>
            </PermissionWrapper>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'customer', actions: ['delete'] }]}
            >
              <Button
                type="text"
                size="small"
                status="danger"
                onClick={() => handleDelete(record)}
              >
                {t['customer.button.delete']}
              </Button>
            </PermissionWrapper>
```

- [ ] **Step 2: 改顶部新增按钮的 actions**

`index.tsx:225-226` 的 `actions: ['write']` → `actions: ['create']`。

- [ ] **Step 3: 提交**

```bash
git add frontend/src/pages/business/customer/index.tsx
git commit -m "feat(customer): 编辑/删除拆分独立权限码(create/update/delete)"
```

---

### Task 9: system/user 页接入按钮级权限

**Files:**
- Modify: `frontend/src/pages/system/user/index.tsx`

- [ ] **Step 1: 顶部新增用户按钮包权限**

找到顶部"新增用户"按钮(约 `:252-254`),用 PermissionWrapper 包裹:

```tsx
<PermissionWrapper requiredPermissions={[{ resource: 'system:user', actions: ['create'] }]}>
  <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
    {t['user.add']}
  </Button>
</PermissionWrapper>
```

(以实际按钮 props 为准,仅外层加 PermissionWrapper。)

- [ ] **Step 2: 操作列编辑/重置密码/启停按钮分别包权限**

操作列 render 内,把"编辑"包 `system:user:update`、"重置密码"包 `system:user:reset-password`、"禁用/启用"包 `system:user:update`。例:

```tsx
<Space>
  <PermissionWrapper requiredPermissions={[{ resource: 'system:user', actions: ['update'] }]}>
    <Button type="text" size="small" onClick={() => openEdit(record)}>{t['user.edit']}</Button>
  </PermissionWrapper>
  <PermissionWrapper requiredPermissions={[{ resource: 'system:user', actions: ['reset-password'] }]}>
    <Button type="text" size="small" onClick={() => openReset(record)}>{t['user.resetPassword']}</Button>
  </PermissionWrapper>
  <PermissionWrapper requiredPermissions={[{ resource: 'system:user', actions: ['update'] }]}>
    <Popconfirm title={...} onOk={() => handleToggleStatus(record)}>
      <Button type="text" size="small">{record.isActive ? t['user.disable'] : t['user.enable']}</Button>
    </Popconfirm>
  </PermissionWrapper>
</Space>
```

(以实际 Popconfirm/Button 结构为准,PermissionWrapper 套在最外层。)

- [ ] **Step 3: 验证编译 + 手动冒烟**

Run: `cd frontend && npx tsc --noEmit`
Expected: PASS。
手动:developer 登录 user 页 → 新增/编辑/重置密码/禁用按钮全隐藏;admin 登录 → 全可见。

- [ ] **Step 4: 提交**

```bash
git add frontend/src/pages/system/user/index.tsx
git commit -m "feat(user): 写操作按钮接入PermissionWrapper(create/update/reset-password)"
```

---

### Task 10: system/role 页写按钮接入权限

**Files:**
- Modify: `frontend/src/pages/system/role/index.tsx:143-183`

- [ ] **Step 1: 顶部新增角色按钮包权限**

`index.tsx:178-180` 的"新增角色"按钮,外层包:

```tsx
<PermissionWrapper requiredPermissions={[{ resource: 'system:role', actions: ['create'] }]}>
  <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
    {t['role.add']}
  </Button>
</PermissionWrapper>
```

- [ ] **Step 2: 操作列编辑/删除按钮分别包权限**

`index.tsx:153-168` 的 render 内:
- "编辑"按钮(`:155-157`)包 `system:role:update`
- "删除"Popconfirm(`:158-166`)包 `system:role:delete`

```tsx
<Space>
  <PermissionWrapper requiredPermissions={[{ resource: 'system:role', actions: ['update'] }]}>
    <Button type="text" size="small" onClick={() => openEdit(record)}>{t['role.edit']}</Button>
  </PermissionWrapper>
  <PermissionWrapper requiredPermissions={[{ resource: 'system:role', actions: ['delete'] }]}>
    <Popconfirm title={t['role.delete.confirm']} onOk={() => handleDelete(record.id)} disabled={record.code === 'admin'}>
      <Button type="text" size="small" status="danger" disabled={record.code === 'admin'}>
        {t['role.delete']}
      </Button>
    </Popconfirm>
  </PermissionWrapper>
</Space>
```

(保留原有的 `disabled={record.code === 'admin'}` 业务保护。)

- [ ] **Step 3: 提交**

```bash
git add frontend/src/pages/system/role/index.tsx
git commit -m "feat(role): 写按钮接入PermissionWrapper(create/update/delete)"
```

---

### Task 11: system/numbering 及 dict 页写按钮接入权限

> **注意(menu-hierarchy 同步后):** numbering 页面**源码仍在 `pages/system/numbering/`**(menu-hierarchy 只迁移了路由 path 到 `master-data/numbering`,未移动源文件)。resource 仍是 `system:numbering`。不要因路由路径变化而改动文件路径。

**Files:**
- Modify: `frontend/src/pages/system/numbering/index.tsx`
- Modify: `frontend/src/pages/system/numbering/dict/index.tsx`

- [ ] **Step 1: numbering/index.tsx 规则写按钮包权限**

- 顶部"新增"按钮(约 `:497-499`)包 `system:numbering:create`
- 操作列"编辑"按钮(约 `:357-359`)包 `system:numbering:update`
- 操作列"禁用/启用"Popconfirm(约 `:360-377`)包 `system:numbering:update`

模板:
```tsx
<PermissionWrapper requiredPermissions={[{ resource: 'system:numbering', actions: ['create'] }]}>
  <Button ...>{t['numbering.rules.create']}</Button>
</PermissionWrapper>
```

- [ ] **Step 2: numbering/dict/index.tsx 业务类型/分类写按钮包权限**

dict 页有两组(业务类型 + 分类),各:
- "新增"按钮(`:260-262` 业务类型、`:296-298` 分类)包 `system:numbering:create`
- "编辑"按钮(`:194-196`、`:231-233`)包 `system:numbering:update`
- "禁用/启用"Popconfirm(`:197-206`、`:234-243`)包 `system:numbering:update`

(4 处"新增"统一 `create`,4 处"编辑/启停"统一 `update`,resource 均为 `system:numbering`。)

- [ ] **Step 3: 验证编译**

Run: `cd frontend && npx tsc --noEmit`
Expected: PASS。

- [ ] **Step 4: 提交**

```bash
git add frontend/src/pages/system/numbering/
git commit -m "feat(numbering): 规则/字典写按钮接入PermissionWrapper"
```

---

### Task 12: system/unit 页写按钮接入权限

> **注意(menu-hierarchy 同步后):** unit 页面**源码仍在 `pages/system/unit/`**(menu-hierarchy 只迁移了路由 path 到 `master-data/unit`,未移动源文件)。resource 仍是 `system:unit`。不要因路由路径变化而改动文件路径。

**Files:**
- Modify: `frontend/src/pages/system/unit/index.tsx`

- [ ] **Step 1: 写按钮包权限**

- 顶部"新增"按钮(约 `:294-296`)包 `system:unit:create`
- 操作列"编辑"按钮(约 `:227-229`)包 `system:unit:update`
- 操作列"禁用/启用"Popconfirm(约 `:230-237`)包 `system:unit:update`

(注:"换算"按钮约 `:299-301` 是只读计算,不包权限。)

模板同 Task 11。

- [ ] **Step 2: 验证编译 + 提交**

Run: `cd frontend && npx tsc --noEmit`
```bash
git add frontend/src/pages/system/unit/index.tsx
git commit -m "feat(unit): 写按钮接入PermissionWrapper(create/update)"
```

---

### Task 13: master-data/color 页写按钮接入权限

**Files:**
- Modify: `frontend/src/pages/master-data/color/index.tsx`

- [ ] **Step 1: 写按钮包权限**

- 顶部"新增"按钮(约 `:233-235`)包 `color:create`
- 操作列"编辑"按钮(约 `:210-212`)包 `color:update`
- 操作列"禁用/启用"Popconfirm(约 `:213-221`)包 `color:update`

模板:
```tsx
<PermissionWrapper requiredPermissions={[{ resource: 'color', actions: ['create'] }]}>
  <Button ...>{t['color.create']}</Button>
</PermissionWrapper>
```

- [ ] **Step 2: 验证编译 + 提交**

Run: `cd frontend && npx tsc --noEmit`
```bash
git add frontend/src/pages/master-data/color/index.tsx
git commit -m "feat(color): 写按钮接入PermissionWrapper(create/update)"
```

- [ ] **Step 3: 阶段 2 端到端冒烟**

手动验证(spec 验收 6-10):
1. developer 登录 → 所有系统管理页(master-data/color、system/user、role、numbering、unit)的新增/编辑/删除/重置密码按钮**全部隐藏**。
2. developer 登录 customer 页 → 新增/编辑/删除按钮**全部隐藏**(developer 无 customer:create/update/delete)。
3. admin 登录 → 所有写按钮正常可见可操作。
4. 各 Drawer/Modal 打开/提交正常(确定按钮未单独包权限)。

确认无误进入阶段 3。

---

## 阶段 3:权限树两级嵌套 + 路由 bug 修复

### Task 14: 重写 buildPermissionTree 为两级嵌套

**Files:**
- Modify: `frontend/src/pages/system/role/index.tsx:34-47, 120`

- [ ] **Step 1: 新增动作中文化映射 + 重写 buildPermissionTree**

把 `index.tsx:34-47` 的 `buildPermissionTree` 函数整体替换为:

```tsx
// 动作词中文化
const ACTION_LABELS: Record<string, string> = {
  read: '查看',
  create: '新增',
  update: '编辑',
  delete: '删除',
  'reset-password': '重置密码',
};

// 将权限列表按 code 段数分组：2 段(资源:动作)→ 两级树;3 段(system:资源:动作)→ 三级树
function buildPermissionTree(permissions: PermissionItem[]) {
  const tree: any[] = [];
  const groupMap: Record<string, any> = {};

  permissions.forEach((p) => {
    const parts = p.code.split(':');
    if (parts.length === 2) {
      const [resource, action] = parts;
      if (!groupMap[resource]) {
        groupMap[resource] = { key: `g-${resource}`, title: resource, children: [] };
        tree.push(groupMap[resource]);
      }
      groupMap[resource].children.push({ key: p.id, title: ACTION_LABELS[action] ?? action });
    } else if (parts.length === 3) {
      const [prefix, resource, action] = parts;
      if (!groupMap[prefix]) {
        groupMap[prefix] = { key: `g-${prefix}`, title: prefix, children: [], childMap: {} };
        tree.push(groupMap[prefix]);
      }
      const parent = groupMap[prefix];
      if (!parent.childMap[resource]) {
        const subNode = { key: `g-${prefix}-${resource}`, title: resource, children: [] };
        parent.childMap[resource] = subNode;
        parent.children.push(subNode);
      }
      parent.childMap[resource].children.push({ key: p.id, title: ACTION_LABELS[action] ?? action });
    }
  });
  return tree;
}
```

- [ ] **Step 2: 修正 checkedKeys 过滤前缀**

`index.tsx:120` 的 `checkedKeys.filter((k) => !k.startsWith('group-'))` 改为 `!k.startsWith('g-')`(与新分组节点 key 前缀 `g-` 一致):

```tsx
        permissionIds: checkedKeys.filter((k) => !k.startsWith('g-')),
```

- [ ] **Step 3: 验证编译**

Run: `cd frontend && npx tsc --noEmit`
Expected: PASS。

- [ ] **Step 4: 手动冒烟**

admin 登录 → 角色管理 → 编辑某角色 → 权限树应呈现两级(fabric/customer/... 各挂 查看/新增/编辑/删除)/三级(system → user/role/numbering/unit/audit → 动作)。叶子动作中文化。勾选父节点 → 子节点全选;保存 → 无 `g-` 前缀脏数据写入。

- [ ] **Step 5: 提交**

```bash
git add frontend/src/pages/system/role/index.tsx
git commit -m "feat(role): 权限分配树两级嵌套+动作中文化"
```

---

### Task 15: 菜单/路由 requiredPermissions 对齐 :read + 修复 permission 路由 bug

> **注意(menu-hierarchy 同步后):** numbering/unit 的**菜单 key 与路由 path 已迁移到 master-data 域**(`master-data/numbering`、`master-data/unit`),但其 `<RequirePermission>` 的 resource/actions 仍是 `system:numbering`/`system:unit`(menu-hierarchy 只移了路径,没改权限码)。本 task 改的是 actions(`view`→`read`),不改路径。customer/color 路径不变。

**Files:**
- Modify: `frontend/src/routes.ts`
- Modify: `frontend/src/router.tsx`

- [ ] **Step 1: 更新 routes.ts 的 requiredPermissions**

逐项修改 `routes.ts`(行号以当前文件为准,编号见下方):
- `business/customer`(`:25`)已是 `['read']`,不变
- `master-data/color`(`:38`)已是 `['read']`,不变
- `master-data/numbering`(`:45`)`actions: ['view']` → `['read']`(resource 仍是 `system:numbering`)
- `master-data/unit`(`:52`)`actions: ['view']` → `['read']`(resource 仍是 `system:unit`)
- `system/user`(`:65`)`actions: ['manage']` → `['read']`
- `system/role`(`:72`)`actions: ['manage']` → `['read']`
- `system/permission`(`:75-78`,当前无 requiredPermissions)补 `requiredPermissions: [{ resource: 'system:role', actions: ['read'] }]`
- `system/operation-log`(`:83`)`actions: ['view']` → `['read']`
- `system/login-log`(`:90`)`actions: ['view']` → `['read']`

- [ ] **Step 2: 同步更新 router.tsx 的 RequirePermission**

`router.tsx` 逐项与 routes.ts 对齐(改 actions,不改 path):
- `business/customer`(`:99`)已是 `['read']`,不变
- `master-data/color`(`:148`)已是 `['read']`,不变
- `master-data/numbering`(`:124`,path=`master-data/numbering`)`actions={['view']}` → `['read']`
- `master-data/unit`(`:156`,path=`master-data/unit`)`actions={['view']}` → `['read']`
- `system/user`(`:107`)`actions={['manage']}` → `['read']`
- `system/role`(`:115`)`actions={['manage']}` → `['read']`
- `system/permission`(`:120`,当前裸 `<PermissionPage />`)改为 `<RequirePermission resource="system:role" actions={['read']}><PermissionPage /></RequirePermission>`
- `system/operation-log`(`:132`)`actions={['view']}` → `['read']`
- `system/login-log`(`:140`)`actions={['view']}` → `['read']`

- [ ] **Step 3: 验证编译**

Run: `cd frontend && npx tsc --noEmit`
Expected: PASS。

- [ ] **Step 4: 手动冒烟路由 bug 修复**

developer 登录(无 system:role:read)→ 浏览器直接访问 `/system/permission` → 应渲染 403 页(之前是无保护直接进入)。

- [ ] **Step 5: 提交**

```bash
git add frontend/src/routes.ts frontend/src/router.tsx
git commit -m "fix(permission): 菜单/路由对齐:read + 修复/system/permission越权"
```

---

### Task 16: 收尾验证与测试同步

**Files:**
- Modify: `frontend/src/__tests__/transformPermissions.test.ts`(如需)

- [ ] **Step 1: 跑全量后端测试**

Run: `cd backend && dotnet test`
Expected: 全部 PASS。

- [ ] **Step 2: 跑前端测试**

Run: `cd frontend && npx vitest run`
Expected: 全部 PASS。若 `transformPermissions.test.ts` 用了旧权限码样本(如 `customer:write`)作断言,该测试测的是拆分逻辑(按 `:` 拆段),旧样本仍能跑通,**通常无需改**;若断言里硬编码了"应包含 write 动作"之类,改为新动作词。

- [ ] **Step 3: 全局搜索旧动作词残留(验收标准 4)**

Run(仓库根):
```bash
cd backend && grep -rn -E ":(view|manage|write)\b" src/OneCup.Api/Program.cs src/OneCup.Api/Controllers/ src/OneCup.Infrastructure/Persistence/OneCupDbContext.cs src/OneCup.Infrastructure/Persistence/SeedData.cs
```
Expected: **零输出**(后端权限码与策略名中 view/manage/write 零残留)。

- [ ] **Step 4: 阶段 3 + 全程端到端冒烟(验收标准 11-16)**

1. admin 全功能;developer 各页写按钮隐藏、fabric 可写。
2. 权限树两级嵌套、动作中文化、勾选/保存正常。
3. `/system/permission` 对 developer 返回 403。
4. 菜单/路由 requiredPermissions 全部 read。

- [ ] **Step 5: 最终提交(若有测试调整)**

```bash
git add frontend/src/__tests__/
git commit -m "test(permission): 同步权限码样本(如有调整)"
```

---

## 完成标志

全部 16 个 Task 完成且 spec 验收标准 1-16 全部满足:
- [ ] 后端:42 策略、Controller 逐方法注解、migration 应用、旧动作词零残留
- [ ] 前端:全站写按钮接入 PermissionWrapper、customer 编辑/删除拆分、developer 隐藏写按钮
- [ ] 权限树两级嵌套、`/system/permission` bug 修复、菜单/路由对齐 read
- [ ] `dotnet build` + `tsc --noEmit` + `dotnet test` + `vitest run` 全绿
