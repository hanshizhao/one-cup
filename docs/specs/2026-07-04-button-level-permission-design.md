# OneCup 按钮级权限收口设计文档

> 印染厂面料开发管理系统 — 权限码细化 + 前端按钮级权限全站收口
> 创建日期: 2026-07-04
> 状态: 已确认
> 关联:
> - [用户/角色/权限整改设计](2026-07-02-rbac-overhaul-design.md)(第 7.5 节遗留收口)

---

## 1. 背景与目标

### 1.1 背景

权限系统已具备 RBAC 基础(后端 JWT claim + ASP.NET Core 策略授权;前端 PermissionWrapper 三层防御),但存在两个问题:

- **前端按钮级权限覆盖严重不完整**:`PermissionWrapper` 机制已就位,但全站仅 `business/customer` 一页接入。用户管理、角色管理、编号管理、计量单位、颜色对色等含写操作的页面,写按钮全部裸露——部分页面路由层只要求 `:view` 却允许页内全部写操作(越权隐患)。
- **后端权限码粒度粗且命名不一致**:现有权限码仅 `read/view` + `write/manage` 两档(`view/manage/write` 三种动作词混用),无法表达"能编辑不能删除"这类细分;策略名与权限码两套映射(`color-view`↔`color:read`)增加维护负担。

### 1.2 目标

1. **后端权限码细化为全模块统一 4 档**(`:read/:create/:update/:delete`),用户管理额外 `:reset-password`。
2. **前端所有写操作按钮统一接入 PermissionWrapper**,做到"凡写操作必包权限"。
3. **统一权限码命名规范**(`资源:动作`,策略名 = 权限码),废弃 `view/manage/write`。
4. **角色页权限分配树优化为两级嵌套**,适配细化后的权限码数量。
5. **顺手修复路由级权限 bug**(`/system/permission` 无保护)。

### 1.3 已确认决策(来自头脑风暴)

| 维度 | 决策 |
|------|------|
| 范围 | 全站写操作按钮收口 + 后端权限码细化 |
| 无权限行为 | 隐藏(PermissionWrapper 现状,返回 null) |
| 权限码粒度 | 全模块统一 4 档,无例外;用户管理额外 `:reset-password` |
| 命名规范 | 统一 `资源:动作`,策略名 = 权限码(带冒号);废弃 `view/manage/write` |
| 权限树 | 角色页权限分配树优化为两级嵌套 |
| 数据迁移 | 清空重分配(migration 重建权限种子,角色-权限关联清空后重分配 developer) |
| 工程组织 | 方案 A:后端先行(策略+Controller+migration)→ 前端铺开 → 权限树+路由修复 |
| 占位模块 | fabric/material/equipment/product 一同拆为 4 档(保证规则统一) |

### 1.4 不在范围内

- 权限分配树的拖拽排序/搜索
- 资源节点(一级/二级)的中文化 i18n(仅叶子动作中文化)
- 数据权限(行级/字段级)
- 前端 E2E 测试
- 后端权限码的动态增删(运行时管理)
- fabric/material/equipment/product 的 Controller 与页面实现

---

## 2. 权限码矩阵(后端细化方案)

全模块统一为 `资源:动作`,动作固定 4 个 `read/create/update/delete`,用户管理额外 `reset-password`。废弃现有 `view/manage/write`。

**10 个资源模块 × 4 档 = 40 个权限码 + system:user 的 reset-password 特例 + system:audit 仅 read(1 个)= 42 个权限码。**

| 资源 (resource) | read | create | update | delete | 特例 |
|---|---|---|---|---|---|
| `fabric` | ✅ | ✅ | ✅ | ✅ | — |
| `material` | ✅ | ✅ | ✅ | ✅ | — |
| `equipment` | ✅ | ✅ | ✅ | ✅ | — |
| `customer` | ✅ | ✅ | ✅ | ✅ | — |
| `color` | ✅ | ✅ | ✅ | ✅ | — |
| `product` | ✅ | ✅ | ✅ | ✅ | — |
| `system:user` | ✅ | ✅ | ✅ | ✅ | ✅ `reset-password` |
| `system:role` | ✅ | ✅ | ✅ | ✅ | — |
| `system:numbering` | ✅ | ✅ | ✅ | ✅ | — |
| `system:unit` | ✅ | ✅ | ✅ | ✅ | — |
| `system:audit` | ✅ | — | — | — | (只读模块,仅 read) |

**说明**:
- `system:audit`(审计/操作日志/登录日志)是纯只读模块,只保留 `read`,不拆写三档(无写操作可保护)。基于"有对应操作才设权限码"的务实原则。
- 现有 `view/manage/write` 全部废弃:旧 `color:write`→拆为 `color:create+update+delete`;旧 `system:user:manage`→拆为 `system:user:create+update+delete+reset-password`;旧 `system:numbering:view`→`system:numbering:read`。
- **命名规范:策略名 = 权限码**(如策略 `customer:create` 的 RequireClaim 权限码也是 `customer:create`),消除现有 `color-view↔color:read` 这种两套映射。策略名带冒号在 ASP.NET Core 合法。

**策略注册清单(全量,替换 `Program.cs:148-173` 现有 11 个策略)**:
- 业务模块 6 个(fabric/material/equipment/customer/color/product)× 4 = 24 个策略
- 系统模块:`system:user` 5 个 + `system:role` 4 个 + `system:numbering` 4 个 + `system:unit` 4 个 + `system:audit` 1 个 = 18 个策略
- **合计 42 个策略**(与 42 个权限码一一对应,audit 只 1 个)

---

## 3. Controller 注解改造

策略改了,Controller 的 `[Authorize(Policy=...)]` 同步。改造原则:

**读操作(GET)→ `:read`;新增(POST)→ `:create`;编辑/修改/启停(PUT)→ `:update`;删除(DELETE)→ `:delete`;重置密码 → `:reset-password`。**

逐 Controller 改造表(只列变化的):

| Controller | 现状 | 改为 |
|---|---|---|
| **UsersController** | 类级 `user-manage`(=`system:user:manage`)统管全部 | **删类级**,逐方法:GetList/GetById → `system:user:read`;Create → `system:user:create`;Update/UpdateStatus → `system:user:update`;ResetPassword → `system:user:reset-password`;Delete → `system:user:delete` |
| **RolesController** | 类级 `role-manage` 统管全部 | **删类级**,逐方法:GetList/GetById → `system:role:read`;Create → `system:role:create`;Update → `system:role:update`;Delete → `system:role:delete` |
| **ColorController** | 无类级;读 `color-view`(=`color:read`),写 `color-manage`(=`color:write`) | 读方法策略名改 `color:read`;CreateColor → `color:create`;UpdateColor/UpdateColorStatus → `color:update`(注:无 Delete 方法) |
| **CustomersController** | 类级 `customer-read`;写方法叠加 `customer-write` | 类级改 `customer:read`;Create → `customer:create`;Update → `customer:update`;Delete → `customer:delete` |
| **NumberingController** | 读 `numbering-view`,写 `numbering-manage`,Preview 裸 Authorize | 读方法 → `system:numbering:read`;CreateRule → `system:numbering:create`;UpdateRule/UpdateRuleStatus → `system:numbering:update`;Preview 保持裸 Authorize(只读预览,不涉权限码);GetLogs → `system:numbering:read` |
| **NumberingDictionaryController** | 读 `numbering-view`,写 `numbering-manage` | 读 → `system:numbering:read`;Create* → `system:numbering:create`;Update*Status → `system:numbering:update` |
| **MeasurementUnitsController** | 读 `unit-view`(=`system:unit:view`),写 `unit-manage`,Convert `unit-view` | 读 → `system:unit:read`;Create → `system:unit:create`;Update/UpdateStatus → `system:unit:update`;Convert → `system:unit:read` |
| **LoginLogsController / OperationLogsController** | 类级 `audit-view`(=`system:audit:view`) | 类级策略名改 `system:audit:read`(权限码同步改) |
| **AuthController / PermissionsController** | 裸 Authorize | **不变** |

**关键决策**:
- **UsersController / RolesController 从"类级统管"改为"逐方法"**:类级会把"查看用户列表"和"删除用户"绑定到同一权限码,无法体现细化。改为逐方法后,每个端点对应独立权限码。
- **Preview 端点(NumberingController)保持裸 `[Authorize]`**:编号预览属只读计算,不归属任何写权限码,保持"仅登录"即可。

---

## 4. Migration 与数据迁移(清空重分配)

权限码从 19 个 → 42 个,且命名全改。数据迁移策略为**清空重分配**。

**新增 EF Core migration(命名 `RefinePermissionCodes`)**:

**Up()**:
1. 删除旧 role_permissions 关联:`DELETE FROM role_permissions`(developer 的 8 条绑定清空;admin 本不绑,无影响)
2. 删除旧 permissions 记录:`DELETE FROM permissions`(清掉 19 个旧码)
3. 插入新 permissions 记录:42 个新权限码(42 行 `InsertData`,Guid 全部重新分配,不复用旧的)
4. 重建 developer 角色绑定(10 条,见下)

**Down()**:反向操作(删新码、插回旧 19 码、恢复 developer 8 条绑定)。因命名已变,Down 仅用于回滚到旧状态,不保证语义等价。

**developer 重分配(10 条绑定)**:
- fabric 全套:`fabric:read`、`fabric:create`、`fabric:update`、`fabric:delete`(沿用现状 developer 有 fabric:write,拆为写三档)
- 其余业务模块只读:`material:read`、`equipment:read`、`customer:read`、`color:read`、`product:read`
- 审计读:`system:audit:read`

**关键技术细节**:
- **Guid 策略**:42 个新权限码各分配固定 Guid,在 `SeedData.cs` 定义常量(命名 `Perm{Resource}{Action}`,如 `PermCustomerCreate`、`PermSystemUserResetPassword`),migration 的 `InsertData` 引用这些常量。**不复用旧 Guid**(旧码语义已变,复用造成困惑)。
- **SeedData.cs 同步重写**:现有 19 个 `Perm*` 常量替换为 42 个。
- **OneCupDbContext.cs 的 HasData 同步更新**:seed 数据源唯一真相是 SeedData 常量 + DbContext HasData。三者(SeedData 常量、DbContext HasData、ModelSnapshot)一致。
- **ModelSnapshot 自动更新**:`dotnet ef migrations add` 自动重生成,无需手改。
- **admin 角色**:不绑权限(靠通配 `*`),migration 无需处理 admin 的 role_permissions。
- **migration 原子执行**:Up 内一次完成清空+重建+重分配,迁移期间无中间态。

---

## 5. 前端 PermissionWrapper 全站铺开

### 5.1 基础设施无需改动

| 组件 | 是否需改 | 原因 |
|---|---|---|
| `PermissionWrapper` | **不改** | 现有 props(`requiredPermissions`/`oneOfPerm`/`backup`)满足需求,无权限隐藏行为不变 |
| `authentication.ts` | **不改** | 判定逻辑按 resource+actions 匹配,与动作名无关 |
| `transformPermissions()` | **不改** | 按最后一段拆 action,`customer:create` → `{customer:['create']}`,天然兼容细化码 |
| `RequirePermission` | **不改** | 仅路由级,菜单/路由权限配置单独更新 |

### 5.2 全站写操作按钮接入清单

每个写操作按钮包对应权限码。规则:`新增→:create`、`编辑/启停→:update`、`删除→:delete`、`重置密码→:reset-password`。

| 页面 | 写操作 | 包权限码 | 现状 | 改造 |
|---|---|---|---|---|
| **customer** | 新增 | `customer:create` | 已包 `customer:write` | 改 actions |
| | 编辑 | `customer:update` | 与删除共包 `customer:write` | **拆开**,单独包 update |
| | 删除 | `customer:delete` | 与编辑共包 | **拆开**,单独包 delete |
| **system/user** | 新增用户 | `system:user:create` | 未包 | 新增包装 |
| | 编辑 | `system:user:update` | 未包 | 新增 |
| | 重置密码 | `system:user:reset-password` | 未包 | 新增 |
| | 禁用/启用 | `system:user:update` | 未包 | 新增(复用 update) |
| **system/role** | 新增角色 | `system:role:create` | 未包 | 新增 |
| | 编辑 | `system:role:update` | 未包 | 新增 |
| | 删除 | `system:role:delete` | 未包 | 新增 |
| **system/numbering** | 规则新增 | `system:numbering:create` | 未包 | 新增 |
| | 规则编辑/启停 | `system:numbering:update` | 未包 | 新增 |
| **numbering/dict** | 业务类型/分类 新增 | `system:numbering:create` | 未包 | 新增 |
| | 业务类型/分类 编辑/启停 | `system:numbering:update` | 未包 | 新增 |
| **system/unit** | 新增 | `system:unit:create` | 未包 | 新增 |
| | 编辑/启停 | `system:unit:update` | 未包 | 新增 |
| **master-data/color** | 新增 | `color:create` | 未包 | 新增 |
| | 编辑/启停 | `color:update` | 未包 | 新增 |

**说明**:
- **Drawer/Modal 内的"确定"按钮不再单独包权限**:必须先点入口按钮(已包权限)才能打开 Modal。沿用 customer 页现有模式,避免双重包裹。
- **customer 页的"拆开"改造**:现状(行 183-197)编辑和删除共包一个 `customer:['write']`。细化后两个按钮权限不同(编辑→update,删除→delete),拆成两个独立 `PermissionWrapper`。
- **读操作按钮(查看/查询/重置/换算/预览)不包权限**:不改数据,菜单/路由级 `:read` 已控制可见性。

### 5.3 统一接入模板

所有"未包"的按钮按下述模板接入,保持全站一致:

```tsx
import PermissionWrapper from '@/components/PermissionWrapper';

<PermissionWrapper requiredPermissions={[{ resource: 'system:user', actions: ['create'] }]}>
  <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
    {t['user.add']}
  </Button>
</PermissionWrapper>
```

---

## 6. 菜单/路由级权限配置同步 + 路由 bug 修复

### 6.1 菜单/路由 requiredPermissions 更新

`routes.ts` 的 `requiredPermissions` 用 `{ resource, actions }` 形式,菜单可见只需 `:read`。

| 菜单 key | 现状 requiredPermissions | 改为 |
|---|---|---|
| `business/customer` | `{resource:'customer',actions:['read']}` | 不变(`customer:read` 保留) |
| `system/user` | `{resource:'system:user',actions:['manage']}` | `{resource:'system:user',actions:['read']}` |
| `system/role` | `{resource:'system:role',actions:['manage']}` | `{resource:'system:role',actions:['read']}` |
| `system/permission` | **无(BUG)** | `{resource:'system:role',actions:['read']}`(见 6.2) |
| `system/numbering` | `{resource:'system:numbering',actions:['view']}` | `{resource:'system:numbering',actions:['read']}` |
| `system/operation-log` | `{resource:'system:audit',actions:['view']}` | `{resource:'system:audit',actions:['read']}` |
| `system/login-log` | `{resource:'system:audit',actions:['view']}` | `{resource:'system:audit',actions:['read']}` |
| `system/unit` | `{resource:'system:unit',actions:['view']}` | `{resource:'system:unit',actions:['read']}` |
| `master-data/color` | `{resource:'color',actions:['read']}` | 不变 |

**原则:菜单可见性统一对齐 `:read`**。现状 user/role 菜单要求 `:manage`(等价"要有写权限才能看见菜单"),细化后不合理——只读用户应能看到并进入列表页(只是看不到写按钮)。改为 `:read` 后,菜单可见性 = 能否进入页面,写操作可见性交给按钮级 PermissionWrapper。三层职责清晰。

`router.tsx:96-160` 里每个 `<RequirePermission>` 要与 routes.ts 逐项对齐(同一份配置,两处防御,必须一致)。

### 6.2 顺手修复 `/system/permission` 路由无保护 bug

调研发现:权限列表页 `routes.ts:48-51` 和 `router.tsx:120` 都没配权限,任何登录用户都能访问。权限码清单虽是只读数据,但属敏感信息。

**修复**:菜单级和路由级都补 `{resource:'system:role',actions:['read']}`。理由:权限列表是角色管理的从属查看页,挂在角色管理菜单下,用 `system:role:read` 与角色页保持一致(能看到角色,就能看到权限清单)。

---

## 7. 角色页权限分配树两级嵌套

权限码从 19 → 42,现有 `buildPermissionTree`(`role/index.tsx:35-47`)按 `code.split(':')[0]` 分组,会把 `system:*` 的 18 个权限全平铺在一个 `system` 大节点下,user/role/numbering/unit/audit 混在一起,可读性差。本节优化为两级嵌套。

### 7.1 目标树结构

```
□ fabric              ← 一级:资源分组(无 system 前缀的业务模块)
  □ read / create / update / delete
□ customer
  □ read / create / update / delete
□ color
  □ read / create / update / delete
□ system              ← 一级:system 前缀归为一组
  □ user              ← 二级:system 下的子资源
    □ read / create / update / delete / reset-password
  □ role
    □ read / create / update / delete
  □ numbering
    □ read / create / update / delete
  □ unit
    □ read / create / update / delete
  □ audit
    □ read
```

**分组规则**:
- 权限码无 `system:` 前缀的(`fabric:read`、`customer:create` 等)→ 一级节点直接是资源名,动作挂为二级叶子。
- 权限码有 `system:` 前缀的(`system:user:create` 等)→ 一级 `system`,二级是 `system:` 后的下一段(user/role/numbering/unit/audit),动作挂为三级叶子。

### 7.2 buildPermissionTree 改造

`role/index.tsx:35-47` 重写分组逻辑:

```ts
// 按 code 段数分组:2 段(资源:动作)→ 两级树;3 段(system:资源:动作)→ 三级树
function buildPermissionTree(permissions: PermissionItem[]) {
  const tree: TreeNode[] = [];
  const groupMap: Record<string, TreeNode> = {};

  permissions.forEach((p) => {
    const parts = p.code.split(':');
    if (parts.length === 2) {
      // fabric:read → 一级 fabric,二级动作
      const [resource, action] = parts;
      if (!groupMap[resource]) {
        groupMap[resource] = { key: `g-${resource}`, title: resource, children: [] };
        tree.push(groupMap[resource]);
      }
      groupMap[resource].children.push({ key: p.id, title: actionLabel(action) });
    } else if (parts.length === 3) {
      // system:user:read → 一级 system,二级 user,三级动作
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
      parent.childMap[resource].children.push({ key: p.id, title: actionLabel(action) });
    }
  });
  return tree;
}
```

### 7.3 节点 title 显示

叶子节点 title 用动作的中文化映射(`read`→"查看"、`create`→"新增"、`update`→"编辑"、`delete`→"删除"、`reset-password`→"重置密码")。一级/二级资源节点暂用原始英文资源名(本轮不做资源名 i18n)。

### 7.4 checkedKeys 处理

`role/index.tsx:120` 提交时过滤 `group-` 前缀(`checkedKeys.filter(k => !k.startsWith('group-'))`改为 `!k.startsWith('g-')`,统一前缀)。两级嵌套后父节点 key 仍是 `g-xxx` / `g-system-user` 形式,过滤逻辑天然兼容。**需确保所有分组节点 key 统一 `g-` 前缀**。

---

## 8. 风险与权衡

### 8.1 已识别风险

| 风险 | 影响 | 缓解 |
|---|---|---|
| 权限码命名全改(`view/manage/write`→`read/create/update/delete`),涉及面广 | 中,后端 Program.cs + 7 个 Controller + 前端 routes.ts/router.tsx 多处同步,漏改一处即菜单失配或 403 | 改造表逐项列出(第 3、6 节),完成后按"策略注册→Controller→路由→菜单"四点交叉核对;阶段1结束跑端到端冒烟 |
| migration 清空重分配后,developer 角色权限中间态丢失 | 低,迁移期间 developer 暂时无业务权限 | migration 原子执行(Up 内一次完成清空+重建+重分配);项目早期无生产数据,影响可接受 |
| UsersController/RolesController 删类级改逐方法,授权行为变化 | 低,逐方法注解后语义更精确,但需确认每个端点都贴了注解(漏贴=裸 Authorize=仅登录可访问,放权过大) | 改造表(第 3 节)逐方法列出;code review 重点核对无遗漏 |
| 前端细化码与后端不同步会导致按钮全隐藏或全显示 | 中,若前端先行/后端未跟上,PermissionWrapper 判定失配 | 方案 A 严格"后端先行",阶段1后端策略+数据落定且冒烟通过,阶段2前端才接入;每接一个页面用真实后端验证按钮显隐 |
| 权限分配树两级嵌套的 key 体系与现有 checkedKeys 过滤逻辑兼容 | 低 | 第 7.4 节已说明;实现时确保所有分组节点 key 统一 `g-` 前缀 |

### 8.2 权衡说明

- **全模块统一 4 档,包括无页面的 fabric/material/equipment/product**:接受权限码"暂时空置"(无 Controller 保护)换取规则统一。这些模块未来开发时权限码已就位,无需回头补拆。空置权限码不影响系统运行(无策略消费它们,admin 通配覆盖)。
- **system:audit 只保留 read**:审计模块本质只读(无写端点),强行拆 create/update/delete 是无意义的空码。基于"有对应操作才设权限码"的务实原则。
- **菜单可见性对齐 :read**:让只读用户也能进入用户/角色列表页(但看不到写按钮)。牺牲了"完全看不到敏感模块"的隐蔽性,换取职责分层清晰(菜单=能否进,按钮=能否操作)。对内部管理系统,可进入但不可操作是常见且合理的。
- **策略名 = 权限码(带冒号)**:消除两套映射的记忆负担。代价是策略名带冒号(`customer:create`),在 ASP.NET Core 策略命名里合法但少见。团队已确认接受。

---

## 9. 验证标准(Definition of Done)

### 阶段 1:后端权限码细化(策略+Controller+migration)

1. `Program.cs` 注册的策略数为 42 个,策略名 = 权限码(一一对应),全部带冒号。
2. **逐 Controller 核对无裸 Authorize 漏贴**(UsersController/RolesController 改逐方法后):每个业务 Action 都有对应 `:read/:create/:update/:delete/:reset-password` 策略注解。
3. migration `RefinePermissionCodes` 可正常 `database update`;`permissions` 表有 42 条记录,`role_permissions` 表 developer 绑定为 10 条。
4. **策略命名一致性**:全局搜索 `view/manage/write` 三个旧动作词,后端权限码与策略名中**零残留**(旧的 `system:audit:view` 已改 `:read`)。
5. 端到端冒烟:admin 登录全功能可用;developer 登录能进入各列表页、能对 fabric 增改删、对其他模块只读(后端 403 拦截写操作)。

### 阶段 2:前端 PermissionWrapper 全站铺开

6. **接入完整性**:第 5.2 节清单中所有"未包"的写操作按钮均已包对应权限码;customer 页编辑/删除已拆为两个独立 wrapper。
7. **developer 视角冒烟**:developer 登录后,各系统管理页的新增/编辑/删除/重置密码按钮**全部隐藏**;fabric 页的新增/编辑/删除按钮**可见可操作**。
8. **admin 视角冒烟**:admin 登录后所有写操作按钮正常可见可操作(通配放行)。
9. **Drawer/Modal 确定按钮**:全站未单独包权限(沿用入口控制),打开/提交行为正常。
10. `PermissionWrapper`/`authentication.ts`/`transformPermissions` 三处源码**零改动**(基础设施无需改的承诺兑现)。

### 阶段 3:权限树两级嵌套 + 路由修复

11. 角色编辑抽屉的权限树呈现两级(业务模块)/三级(system)结构,叶子节点动作中文化(查看/新增/编辑/删除/重置密码)。
12. 权限树勾选父节点自动勾选子节点;提交时 `g-` 前缀节点正确过滤(无脏数据写入 role_permissions)。
13. **`/system/permission` 路由 bug 修复**:未持有 `system:role:read` 的用户访问该路由 → 渲染 403。
14. 菜单/路由 requiredPermissions 全部对齐 `:read`,与 `router.tsx` 的 `<RequirePermission>` 逐项一致。

### 全程
15. `dotnet build` + `tsc --noEmit` 零错误。
16. 现有测试(`transformPermissions.test.ts` 等)全部通过;若权限码样本变化导致测试断言失效,同步更新测试数据。
