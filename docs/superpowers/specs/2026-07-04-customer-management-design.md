# 客户管理模块设计

> **状态**：已通过头脑风暴设计评审
> **日期**：2026-07-04
> **Worktree**：`feat/customer-mgmt`（`.worktrees/customer-mgmt/`）
> **协作约定**：遵守 `docs/parallel-dev-contract.md`

---

## 0. 背景与范围

OneCup 是印染厂面料开发管理系统。"客户"指向印染厂下单做面料开发/印染的甲方
（服装品牌、贸易商等）。

本模块范围：**客户档案 CRUD**。先把"客户"作为独立主数据建起来，后续面料开发单
再关联客户。联系人/地址等子表等真正有消费场景（下单）时再加（YAGNI）。

### 基线已就绪（main 已存在，直接复用，不新增任何 Guid）

| 资源 | 常量 | Guid | 位置 |
| --- | --- | --- | --- |
| 客户读权限 | `PermCustomerRead` | `...000000000107` | `SeedData.cs:20` |
| 客户写权限 | `PermCustomerWrite` | `...000000000108` | `SeedData.cs:21` |
| 编号目标类型 | `TargetTypeCustomer` | `...000000000204` | `SeedData.cs:41` |
| 目标类型常量 | `NumberTargetTypes.Customer` | `"customer"` | `NumberTargetTypes.cs:13` |

种子数据（`OneCupDbContext.Seed()` 内）也已存在：权限码 `customer:read`/`customer:write`
（`:85-86`）、`customer:read` 已授予 developer 角色（`:131`）、编号目标类型字典行
`customer`（`:147`）。

**客户管理是三个并行模块（unit/color/customer）里唯一完全不碰 `SeedData.cs` 和
`Seed()` 的模块**——不新增 Guid、不种子化客户数据、不种子化客户编号规则。

---

## 1. 数据模型与领域层

### 1.1 Customer 实体

**新建文件**：`OneCup.Domain/Entities/Customer.cs`（per-file 零冲突）

```csharp
public class Customer : BaseEntity, ISoftDeletable
{
    /// <summary>客户编号（编号系统生成，如 CUST-0001）</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>客户名称（全名，唯一）</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>客户简称（可重复）</summary>
    public string? ShortName { get; set; }

    /// <summary>联系人</summary>
    public string? ContactPerson { get; set; }

    /// <summary>联系电话</summary>
    public string? ContactPhone { get; set; }

    /// <summary>备注</summary>
    public string? Remark { get; set; }

    /// <summary>启用状态（停用的客户不再用于新业务，但保留历史）</summary>
    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; } = false;
}
```

### 1.2 字段约束

| 字段 | 类型 | 长度 | 必填 | 索引 |
| --- | --- | --- | --- | --- |
| `code` | string | 50 | 是 | 唯一 |
| `name` | string | 100 | 是 | 唯一 |
| `short_name` | string? | 50 | 否 | — |
| `contact_person` | string? | 50 | 否 | — |
| `contact_phone` | string? | 30 | 否 | — |
| `remark` | string? | 500 | 否 | — |
| `is_active` | bool | — | 是（默认 true） | — |
| `is_deleted` | bool | — | 是（默认 false） | — |
| `created_at` / `updated_at` | DateTime(?) | — | 由 EF 值生成器填 | — |

**`code` 设唯一索引的理由**：客户编号是系统按规则生成的，天然不重复。唯一索引是
防御性约束——即使编号服务出 bug 也不会产生重复编号的脏数据。

### 1.3 唯一性的软删除处理

- `name` 的唯一索引配合全局查询过滤器 `HasQueryFilter(c => !c.IsDeleted)`
- 创建/改名时的预检用 `AnyIgnoringFiltersAsync`（绕过过滤器），这样已被软删除占用的
  名称也会被识别为"已占用"，返回清晰的 400 而非触发数据库唯一索引冲突（500）
- 与现有 `UserService` 用户名唯一性的处理方式完全一致

### 1.4 EF 配置

**新建文件**：`OneCup.Infrastructure/Persistence/Configurations/CustomerConfiguration.cs`（per-file 零冲突）

沿用 `RoleConfiguration` 的列命名风格（snake_case 列名 + `HasColumnName`）：

- `ToTable("customers")`
- `code` / `name` 各建唯一索引
- `HasQueryFilter(c => !c.IsDeleted)` 全局软删除过滤
- 列名全 snake_case：`code`, `name`, `short_name`, `contact_person`, `contact_phone`,
  `remark`, `is_active`, `is_deleted`, `created_at`, `updated_at`

### 1.5 DbContext 改动（高冲突文件，contract 3.3）

`OneCupDbContext.cs` 末尾追加：

```csharp
// ===== Customer 模块 =====
public DbSet<Customer> Customers => Set<Customer>();
```

并在 `OnModelCreating` 末尾追加 `modelBuilder.ApplyConfiguration(new CustomerConfiguration());`。

**客户模块不碰 `Seed()` 方法**（不种子化任何数据，复用 main 基线已有种子）。

---

## 2. 应用层（DTO / 规范 / 服务 / 校验）

### 2.1 DTO

**新建文件**：`OneCup.Application/Dtos/System/CustomerDtos.cs`（per-file 零冲突）

放在 `Dtos/System/`（沿用现有目录，与 `RoleDtos.cs`/`UserDtos.cs` 并列）。

```csharp
// 列表项（表格行）
public class CustomerListItemDto
{
    public Guid Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreatedAt { get; set; }
}

// 详情（Drawer 只读）
public class CustomerDto : CustomerListItemDto
{
    public string? Remark { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

// 新建请求
public class CreateCustomerRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
}

// 编辑请求（字段同 Create，独立类以便 FluentValidation 区分规则）
public class UpdateCustomerRequest
{
    public string Name { get; set; } = string.Empty;
    public string? ShortName { get; set; }
    public string? ContactPerson { get; set; }
    public string? ContactPhone { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
}
```

**设计要点**：`Code` 不在请求体里——编号由系统在事务内生成，前端无权指定。

### 2.2 查询规范

**新建文件**：`OneCup.Application/Specifications/CustomerSpecs.cs`（per-file 零冲突）

沿用 `NumberingSpecs.cs` 的"过滤规范 + 分页规范分离"模式（因 `Specification.ApplyCriteria`
是覆盖语义，分页统计必须用不含 Skip/Take 的纯过滤规范）：

```csharp
// 纯过滤规范（用于 CountAsync 统计总数）
public class CustomerFilterSpec : Specification<Customer>
{
    public CustomerFilterSpec(string? keyword, string? code, bool? isActive)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(c =>
            (kw == null || c.Name.Contains(kw) || c.ShortName!.Contains(kw)) &&
            (string.IsNullOrEmpty(code) || c.Code.Contains(code)) &&
            (isActive == null || c.IsActive == isActive.Value));
    }
}

// 分页规范（共享同一 Where，加排序 + Skip/Take）
public class CustomerPagedSpec : Specification<Customer>
{
    public CustomerPagedSpec(string? keyword, string? code, bool? isActive, int page, int pageSize)
    {
        // 同 CustomerFilterSpec 的 Where 条件
        ApplyOrderByDescending(c => c.CreatedAt);
        ApplyPaging(page, pageSize);
    }
}

// 按 Id 查（tracked，详情/更新用）
public class CustomerByIdSpec : Specification<Customer> { /* ApplyCriteria(c => c.Id == id) */ }

// 名称唯一性预检（绕过软删除过滤器）
public class CustomerByNameSpec : Specification<Customer>
{
    public CustomerByNameSpec(string name, Guid? excludingId = null)
        => ApplyCriteria(c => c.Name == name && (excludingId == null || c.Id != excludingId.Value));
}
```

**关键字模糊匹配**同时查 `Name` 和 `ShortName`——用户输"XX服饰"能匹配到简称。`code`
单独作为精确包含匹配（编号是结构化的，没必要参与关键字）。

### 2.3 服务接口与实现

**新建文件**：
- `OneCup.Application/Interfaces/ICustomerService.cs`（per-file 零冲突）
- `OneCup.Application/Services/CustomerService.cs`（per-file 零冲突）

```csharp
public interface ICustomerService
{
    Task<PagedResult<CustomerListItemDto>> GetListAsync(
        string? keyword, string? code, bool? isActive, int page, int pageSize, CancellationToken ct = default);

    Task<CustomerDto?> GetByIdAsync(Guid id, CancellationToken ct = default);

    Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default);

    Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default);

    Task DeleteAsync(Guid id, CancellationToken ct = default);
}
```

#### CreateAsync 的事务流程（方案 A 核心）

`CustomerService` 注入 `INumberingService`。Create 用 `IUnitOfWork.ExecuteInTransactionAsync`
包裹"取号 + 建实体 + 保存"三步，与编号服务的契约完全契合（`GenerateAsync` 要求调用方
事务内调用，且自带 fail-fast 守卫检查 `_db.Database.CurrentTransaction is null`）：

```csharp
public async Task<CustomerDto> CreateAsync(CreateCustomerRequest request, CancellationToken ct = default)
{
    await _createValidator.EnsureValidAsync(request, ct);

    // 名称唯一性预检（绕过软删除过滤器）
    if (await _customers.AnyIgnoringFiltersAsync(new CustomerByNameSpec(request.Name), ct))
        throw new DomainException($"客户名称「{request.Name}」已存在");

    Guid createdId = Guid.Empty;
    await _uow.ExecuteInTransactionAsync(async () =>
    {
        var code = await _numbering.GenerateAsync(NumberTargetTypes.Customer, null, ct);  // 行锁取号
        var customer = new Customer { Code = code, Name = request.Name, /* ... */ };
        await _customers.AddAsync(customer, ct);
        await _uow.SaveChangesAsync(ct);  // 计数器增量 + 客户记录一起提交
        createdId = customer.Id;  // 闭包回传
    }, ct);

    return await GetByIdAsync(createdId, ct) ?? throw new DomainException("客户创建失败");
}
```

- ✅ **零新概念**：项目已有 `ExecuteInTransactionAsync`，编号服务就是按这个模式设计的
- ✅ **B+ 不跳号**：取号失败/建实体失败都回滚，计数器增量随之回滚

#### UpdateAsync（无需事务）

```csharp
public async Task<CustomerDto> UpdateAsync(Guid id, UpdateCustomerRequest request, CancellationToken ct = default)
{
    await _updateValidator.EnsureValidAsync(request, ct);
    var customer = await _customers.FirstOrDefaultAsync(new CustomerByIdSpec(id), ct)
        ?? throw new DomainException("客户不存在");

    // 改名查重（排除自身）
    if (await _customers.AnyIgnoringFiltersAsync(new CustomerByNameSpec(request.Name, id), ct))
        throw new DomainException($"客户名称「{request.Name}」已存在");

    customer.Name = request.Name;
    customer.ShortName = request.ShortName;
    customer.ContactPerson = request.ContactPerson;
    customer.ContactPhone = request.ContactPhone;
    customer.Remark = request.Remark;
    customer.IsActive = request.IsActive;

    await _uow.SaveChangesAsync(ct);  // 无编号操作，不需事务
    return await GetByIdAsync(id, ct) ?? throw new DomainException("客户更新失败");
}
```

Update 不需要事务——只改实体字段，单次 SaveChanges 足够；不涉及编号计数器，无需
B+ 不跳号保证。

#### DeleteAsync（幂等软删）

```csharp
public async Task DeleteAsync(Guid id, CancellationToken ct = default)
{
    var customer = await _customers.FirstOrDefaultAsync(new CustomerByIdSpec(id), ct)
        ?? throw new DomainException("客户不存在");
    customer.IsDeleted = true;  // 幂等软删（与 UserService 一致）
    await _uow.SaveChangesAsync(ct);
}
```

#### GetListAsync（分页查询）

对齐 `NumberingRuleService` 分页模式：
- 用 `CustomerFilterSpec` 调 `CountAsync` 取总数
- 用 `CustomerPagedSpec` 调 `ListAsync` 取当前页
- 投影成 `CustomerListItemDto`

### 2.4 校验（FluentValidation）

**新建文件**：`OneCup.Application/Validators/System/CustomerValidators.cs`（per-file 零冲突）

沿用 `ValidationExtensions.EnsureValidAsync` + 独立 Validator 类模式：

| 字段 | 规则 |
| --- | --- |
| `Name` | NotEmpty + MaximumLength(100) |
| `ShortName` | MaximumLength(50)（可选） |
| `ContactPerson` | MaximumLength(50) |
| `ContactPhone` | MaximumLength(30) + 可选正则（允许数字/`-`/空格/`+`，宽松匹配座机和手机） |
| `Remark` | MaximumLength(500) |

**联系电话用宽松正则**而非严格手机号校验——印染厂客户可能留座机、总机转分机，严格
校验会误伤。Create 和 Update 共用同一套字段约束规则（唯一性在 Service 层查重）。

---

## 3. API 层（控制器 / 授权策略 / 审计 / 依赖注册）

### 3.1 控制器

**新建文件**：`OneCup.Api/Controllers/CustomersController.cs`（per-file 零冲突）

沿用 `RolesController` 模式：`[ApiController]` + `[Route]` + 类级 `[Authorize]` + 方法级 `[Audit]`。

```csharp
[ApiController]
[Route("api/customers")]
[Authorize(Policy = "customer-read")]   // 整个控制器默认需读权限
public class CustomersController : ControllerBase
{
    [HttpGet]                            // 列表（分页查询）
    [HttpGet("{id:guid}")]               // 详情

    [Audit(Module = "Customer", Action = "Create", TargetType = "Customer")]
    [Authorize(Policy = "customer-write")]  // 方法级叠加：写操作需写权限
    [HttpPost]                           // 新建

    [Audit(Module = "Customer", Action = "Update", TargetType = "Customer")]
    [Authorize(Policy = "customer-write")]
    [HttpPut("{id:guid}")]               // 编辑

    [Audit(Module = "Customer", Action = "Delete", TargetType = "Customer")]
    [Authorize(Policy = "customer-write")]
    [HttpDelete("{id:guid}")]            // 删除
}
```

**权限分层逻辑**：
- 类级 `[Authorize(Policy = "customer-read")]` 兜底——进控制器至少要读权限
- 写操作（POST/PUT/DELETE）方法级叠加 `[Authorize(Policy = "customer-write")]`——两层
  Authorize 是 AND 关系，必须同时满足 read + write
- admin 角色由 `WildcardAuthorizationHandler` 通配放行

### 3.2 端点契约

| 方法 | 路由 | 请求 | 响应 | 审计 |
| --- | --- | --- | --- | --- |
| GET | `/api/customers` | `?keyword&code&isActive&page&pageSize` | `PagedResult<CustomerListItemDto>` | 否 |
| GET | `/api/customers/{id}` | — | `CustomerDto` / 404 | 否 |
| POST | `/api/customers` | `CreateCustomerRequest` | 201 + `CustomerDto` | ✅ |
| PUT | `/api/customers/{id}` | `UpdateCustomerRequest` | `CustomerDto` | ✅ |
| DELETE | `/api/customers/{id}` | — | 204 | ✅ |

**无规则时的错误传播**：`CustomerService.CreateAsync` 调 `GenerateAsync` 时，若 customer
未配启用规则，编号服务抛 `DomainException("未找到 customer 的启用编码规则")`。这个
异常被现有全局异常中间件转成 **400 + 中文错误信息**。前端拿到后引导"请先在编号管理为
客户配置启用规则"。

### 3.3 授权策略注册（Program.cs，contract 3.5 🟢低冲突）

`AddAuthorization` 块末尾纯追加两条策略：

```csharp
options.AddPolicy("customer-read", policy =>
    policy.RequireClaim("perm_codes", "customer:read"));
options.AddPolicy("customer-write", policy =>
    policy.RequireClaim("perm_codes", "customer:write"));
```

### 3.4 依赖注册（Program.cs，contract 3.5 🟢低冲突）

`AddScoped` 区末尾纯追加：

```csharp
builder.Services.AddScoped<ICustomerService, CustomerService>();
```

> `INumberingService` 已在 main 注册（编号模块自带），`CustomerService` 依赖它时直接
> 注入即可，无需重复注册。`IRepository<Customer>` 由现有泛型仓储注册自动满足
>（DbContext 加了 `DbSet<Customer>` 后，EF 的仓储实现会自动识别）。

### 3.5 审计

`[Audit]` 特性已存在（`OneCup.Api.Filters`），`Module`/`Action`/`TargetType` 是自由字符串：

- `Module = "Customer"`、`TargetType = "Customer"`（与 `Role` 模块的 `"Role"` 风格一致）
- `Action` 分别为 `Create`/`Update`/`Delete`
- 写操作自动记录到 `operation_logs` 表，在操作日志页可查

---

## 4. 前端（路由 / 菜单 / API / 页面 / 权限）

### 4.1 权限码到前端映射（已验证）

后端 JWT claim `perm_codes: ["customer:read"]` → 前端 `router.tsx` 的 `transformPermissions`
拆成 `{ customer: ['read'] }`（最后一段是 action，前面拼成 resource）。

因此：
- 路由/菜单：`{ resource: 'customer', actions: ['read'] }` ✅
- `<RequirePermission resource="customer" actions={['read']}>` ✅
- `<RequirePermission resource="customer" actions={['write']}>` ✅

### 4.2 路由与菜单（共享文件改动，contract 3.4）

**`frontend/src/routes.ts`** —— `routes` 数组顶部新增 `menu.business` 分组（业务在前、
系统在后，符合日常使用频次），与 `menu.system` 平级：

```typescript
export const routes: IRoute[] = [
  {
    name: 'menu.business',
    key: 'business',
    children: [
      {
        name: 'menu.business.customer',
        key: 'business/customer',
        requiredPermissions: [{ resource: 'customer', actions: ['read'] }],
      },
    ],
  },
  { name: 'menu.system', key: 'system', children: [ /* 现有不变 */ ] },
];
```

**`frontend/src/router.tsx`** —— 追加 lazy import + 路由 element（放 `index` 之后、
`system/*` 之前）：

```typescript
const CustomerPage = lazy(() => import('@/pages/business/customer'));
// children 内：
{
  path: 'business/customer',
  element: withSuspense(
    <RequirePermission resource="customer" actions={['read']}>
      <CustomerPage />
    </RequirePermission>
  ),
},
```

**`frontend/src/locale/index.ts`** —— en-US 和 zh-CN 两个对象都加（contract 3.4）：
- `menu.business` → `'Business'` / `'业务管理'`
- `menu.business.customer` → `'Customer'` / `'客户'`

### 4.3 API 模块

**新建文件**：`frontend/src/api/customer.ts`（per-file 零冲突）

沿用 `role.ts` 模式，基于 `request.ts` 封装：

```typescript
import axios from './request';

export interface CustomerListItem {
  id: string; code: string; name: string; shortName?: string;
  contactPerson?: string; contactPhone?: string;
  isActive: boolean; createdAt: string;
}
export interface CustomerDetail extends CustomerListItem { remark?: string; updatedAt?: string; }
export interface CustomerQuery { keyword?: string; code?: string; isActive?: boolean; page: number; pageSize: number; }
export interface CustomerFormData {
  name: string; shortName?: string; contactPerson?: string;
  contactPhone?: string; remark?: string; isActive: boolean;
}

export const getCustomers = (params: CustomerQuery) => axios.get('/api/customers', { params });
export const getCustomer = (id: string) => axios.get(`/api/customers/${id}`);
export const createCustomer = (data: CustomerFormData) => axios.post('/api/customers', data);
export const updateCustomer = (id: string, data: CustomerFormData) => axios.put(`/api/customers/${id}`, data);
export const deleteCustomer = (id: string) => axios.delete(`/api/customers/${id}`);
```

### 4.4 页面

**新建目录**：`frontend/src/pages/business/customer/`（per-file 零冲突）

```
frontend/src/pages/business/customer/
├── index.tsx          ← 列表页（复制 query-table-page 模板改）
├── form.tsx           ← 新建/编辑 Modal 表单
├── detail.tsx         ← 只读详情 Drawer
├── locale/
│   ├── en-US.ts
│   └── zh-CN.ts
└── style/
    └── index.module.less
```

**列表页 `index.tsx`**（严格遵守 query-table 标准 + AGENTS.md）：
- 单个 `<Card>` 包整页
- 查询区 `Form` + `Row gutter={24}` + 3 个 `Col span={8}`：客户名称（关键字）、客户编号、
  启用状态（Select）
- 查询/重置按钮放表单外侧兄弟 flex div（不用 Space wrap，不塞最后一个 Col）
- 工具栏 flex `space-between` + 左 `<Space>`（新建按钮，仅 write 权限可见）+ 右 `<Space>`
- 表格列：客户编号、客户名称、简称、联系人、联系电话、启用状态（Badge）、创建时间、
  操作（查看/编辑/删除）
- 分页变化/筛选变化自动重查（合并 formParams 进请求）
- 操作列按权限显隐：查看（read 即可）、编辑/删除（write 才显示）

**表单 `form.tsx`**（Modal 新建/编辑共用）：
- Arco `<Modal>` + `<Form>`，字段：名称*、简称、联系人、联系电话、备注（TextArea）、
  启用状态（Switch）
- 编辑时预填、提交调 create/update
- 名称后端查重报错时在表单字段下展示
- 无规则报错（创建时）：捕获 400，Modal 不关闭，顶部 Alert 提示"请先在编号管理为客户
  配置启用规则"

**详情 `detail.tsx`**（Drawer 只读）：
- `<Drawer>` 展示客户全部字段，纯只读 `Descriptions` 展示

**权限控制**：前端用 `<RequirePermission resource="customer" actions={['write']}>` 包裹
"新建/编辑/删除"按钮，read-only 用户看不到写操作入口（后端策略兜底）。

**国际化**：页面级 locale 文件（en-US/zh-CN），与 `pages/system/numbering/locale/` 模式
一致，不挤进全局 locale。

---

## 5. EF 迁移与测试策略

### 5.1 EF 迁移（contract 3.2 🟡防撞）

迁移命名 `AddCustomerModule`，让 EF 自动加时间戳前缀：

```bash
dotnet ef migrations add AddCustomerModule \
  --project src/OneCup.Infrastructure --startup-project src/OneCup.Api
```

**迁移内容**（EF 自动生成）：
- `Up()`：建 `customers` 表（含 `code`/`name` 唯一索引、`is_deleted`、`is_active` 等列）
- `Down()`：删 `customers` 表
- `OneCupDbContextModelSnapshot.cs`：EF 自动追加 Customer 实体到全量快照

**客户模块不碰种子**：
- `customers` 表的 `HasData` **不写**
- 完全跳过 `Seed()` 方法
- 这是客户模块改动面最小的根本原因

**时间戳防撞**：EF 时间戳精确到秒，且客户 worktree 独立开发。合并时若意外撞名，按
contract 4.3 手动改时间戳后缀（改大）。

### 5.2 测试策略

#### 单元测试（核心）

**新建目录**：`backend/tests/OneCup.UnitTests/Customer/`

`CustomerServiceTests.cs` —— 用 InMemory + Repository/UnitOfWork 实例，`INumberingService`
**用测试桩注入**（真实 NumberingService 依赖 `FromSqlRaw` 行锁和 PG 事务，InMemory 不支持）：

```csharp
class FakeNumberingService : INumberingService
{
    public string NextCode { get; set; } = "CUST-0001";
    public Task<string> GenerateAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
        => Task.FromResult(NextCode);
    public Task<string?> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
        => Task.FromResult<string?>(NextCode);
}
```

测试用例（对齐 `RoleServiceTests` + `UserServiceTests` 软删除测试）：

| 用例 | 验证点 |
| --- | --- |
| `CreateAsync_CreatesCustomer` | 事务内取号、落库、返回带编号的 DTO |
| `CreateAsync_AssignsGeneratedCode` | Code 来自编号服务，非空 |
| `CreateAsync_DuplicateName_Throws` | 名称查重抛 DomainException |
| `CreateAsync_DuplicateName_IgnoresSoftDeleted` | 已软删除占用的名称也被识别为已占用 |
| `UpdateAsync_UpdatesFields` | 字段更新正确 |
| `UpdateAsync_DuplicateName_ExcludingSelf_Allowed` | 改名查重排除自身，同名不报错 |
| `UpdateAsync_DuplicateName_OnOtherCustomer_Throws` | 改成别的客户名时拒绝 |
| `DeleteAsync_SoftDeletes` | 置 IsDeleted=true |
| `DeleteAsync_NotFound_Throws` | 不存在的客户抛异常 |
| `DeleteAsync_Idempotent` | 已软删客户再删不报错 |
| `GetListAsync_AppliesFiltersAndPaging` | keyword/code/isActive 过滤 + 分页 |
| `GetListAsync_ExcludesSoftDeleted` | 全局查询过滤器隐藏已删客户 |

`CustomerValidatorsTests.cs` —— FluentValidation 规则测试（对齐
`CreateRoleRequestValidatorTests`）：Name 非空/长度、各字段长度上限、联系电话宽松格式。

**事务处理**：`ExecuteInTransactionAsync` 在 InMemory 下是 no-op（抑制
`TransactionIgnoredWarning`），验证的是控制流——取号→建实体→保存的调用顺序和异常传播，
不依赖真实事务隔离。与 `UnitOfWorkTransactionTests` 的策略一致。

#### 集成测试（可选，视基础设施而定）

`backend/tests/OneCup.IntegrationTests/CustomerApiTests.cs` —— 端到端 HTTP 测试，用
`IntegrationTestFactory`，覆盖：
- 权限策略（无 read 权限 → 403；有 read 无 write → 写操作 403）
- 无编号规则时创建 → 400 + 引导文案
- 完整的 create→get→update→delete 循环
- 审计日志写入验证

> 若 IntegrationTests 基础设施需要真实 PG 且本机不具备，这部分可后置，优先保证单元测试覆盖。

#### 前端测试

对齐现有前端测试惯例，优先：
- API 模块的 mock 测试（请求参数/响应解析）
- 权限按钮显隐测试（RequirePermission 包裹）

---

## 6. 协作约定遵守清单与验收标准

### 6.1 协作约定（`parallel-dev-contract.md`）遵守清单

| 约定项 | 客户模块的落实 | 冲突等级 |
| --- | --- | --- |
| 3.1 种子 Guid | ✅ **不新增任何 Guid**。复用 107/108、204，全部已在 main | — |
| 3.1 不种子化 | ✅ `customers` 表无 `HasData`；编号规则不种子化 | — |
| 3.2 迁移命名 | ✅ `AddCustomerModule`，EF 自动加时间戳 | 🟡 防撞 |
| 3.3 DbContext 改法 | ✅ 末尾追加 `DbSet<Customer>` + `ApplyConfiguration`；**不碰 Seed()** | 🔴 高（改动极小） |
| 3.4 前端三文件 | ✅ `routes.ts` 顶部加 business 组；`router.tsx` 追加路由；`locale/index.ts` 两处加文案 | 🟡 中 |
| 3.5 Program.cs | ✅ 末尾追加 2 条授权策略 + 1 条 AddScoped | 🟢 低 |
| 合并顺序 | ✅ 客户建议**第一个合入**（改动面最小、不新增 Guid） | — |

**零冲突文件**（per-file，全部新建）：`Customer.cs`、`CustomerDtos.cs`、`CustomerSpecs.cs`、
`CustomerValidators.cs`、`ICustomerService.cs`、`CustomerService.cs`、`CustomersController.cs`、
`CustomerConfiguration.cs`、前端 `api/customer.ts`、`pages/business/customer/` 整个目录。

**高冲突文件**（合并时协调）：`OneCupDbContext.cs`、`OneCupDbContextModelSnapshot.cs`、
`_AddCustomerModule.cs`、`routes.ts` / `router.tsx` / `locale/index.ts`、`Program.cs`。

### 6.2 验收标准（Definition of Done）

**后端**：
- [ ] `dotnet build backend/OneCup.sln` 通过
- [ ] `dotnet test backend/OneCup.sln` 全绿（含新增 Customer 单元测试）
- [ ] `dotnet ef database update` 能正确应用 AddCustomerModule 迁移
- [ ] 客户编号依赖编号系统：未配规则时创建报 400 + 引导文案

**前端**：
- [ ] `npm run build` 通过
- [ ] 侧边栏出现"业务管理 > 客户"菜单（read 权限用户可见）
- [ ] 列表页符合 query-table 标准（单 Card、三列 Form、按钮外侧）
- [ ] 新建/编辑 Modal + 详情 Drawer 交互正常
- [ ] write 权限用户可见新建/编辑/删除，read-only 用户只可见查看

**协作**：
- [ ] 未新增任何 Guid，未碰 SeedData.cs 和 Seed()
- [ ] 未越界修改 unit/color 模块的文件
- [ ] 迁移命名 `AddCustomerModule`

### 6.3 已知风险与边界

| 风险 | 应对 |
| --- | --- |
| 用户首次进客户模块时未配编号规则 → 创建报错 | 前端 Modal 顶部 Alert 提示"请先在编号管理为客户配置启用规则"，不阻塞列表查看 |
| ModelSnapshot 合并冲突 | 按 contract 4.4：接受当前分支版本占位 → 重生成空迁移刷新快照 → 删除临时迁移 |
| `code` 唯一索引 + 软删除交互 | 唯一索引建立在数据库层（含软删记录），查重在应用层用 AnyIgnoringFilters。已被软删客户的 code 仍占用——符合预期（编号一旦分配永不复用） |

---

## 附录：新建文件清单

| 层 | 文件 | 性质 |
| --- | --- | --- |
| Domain | `Entities/Customer.cs` | 新建 |
| Application | `Dtos/System/CustomerDtos.cs` | 新建 |
| Application | `Specifications/CustomerSpecs.cs` | 新建 |
| Application | `Validators/System/CustomerValidators.cs` | 新建 |
| Application | `Interfaces/ICustomerService.cs` | 新建 |
| Application | `Services/CustomerService.cs` | 新建 |
| Infrastructure | `Persistence/Configurations/CustomerConfiguration.cs` | 新建 |
| Infrastructure | `Migrations/{ts}_AddCustomerModule.cs` | 新建（EF 生成） |
| Api | `Controllers/CustomersController.cs` | 新建 |
| 前端 | `src/api/customer.ts` | 新建 |
| 前端 | `src/pages/business/customer/`（index/form/detail/locale/style） | 新建整个目录 |
| 测试 | `OneCup.UnitTests/Customer/CustomerServiceTests.cs` | 新建 |
| 测试 | `OneCup.UnitTests/Customer/CustomerValidatorsTests.cs` | 新建 |
