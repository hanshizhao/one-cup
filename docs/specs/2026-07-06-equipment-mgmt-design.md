# 设备管理模块设计（Equipment Management）

> 日期：2026-07-06
> 状态：设计稿（待评审）
> 范围：设备类型（含参数定义 + 运行模板）+ 设备实例，一个完整模块交付
> 参考：Customer 模块（c02 编号主实例）、Material 模块（nullable FK + 自由文本分类）、Process 模块（工序，被模板引用）

---

## 1. 背景与目标

印染厂的设备（定型机、染色机、烧毛机等）每台都有不同的运行参数，且同一台设备在不同工序下有不同的运行方式（"烧法"/"开法"）。本模块解决三件事：

1. **管理设备本身**——编号、名称、供应商、地点、状态等常规资产信息。
2. **定义设备的参数 schema**——每类设备有哪些参数（车速、温度、压力、档位），由设备类型承载，避免逐台重复定义。
3. **定义运行模板**——同一设备类型在同一工序下的多种运行方案（如烧毛机在烧毛工序下有"轻烧/中烧/重烧"），本质是一组命名参数值预设，是工艺预设的最小单位。

### 设计决策摘要

| 决策点 | 结论 | 理由 |
|---|---|---|
| 参数定义归属层 | 设备类型层（非设备实例、非全局字典） | 同型号设备共享 schema，批量高效、名称可标准化 |
| 参数值存哪里 | 只存运行模板（设备实例不存参数值） | 设备是资产、模板是工艺，职责清晰 |
| 运行模板作用域 | 类型级，绑定 (设备类型, 工序) | 同型号共享，工艺标准化，查询直接 |
| 参数值类型 | 多类型（数值/文本/枚举） | 覆盖连续量、离散量、文本说明 |
| 参数值存储列 | 统一字符串列 + 代码层校验 | 与项目"语义靠代码、存储用基础类型"风格一致 |
| 参数定义变更策略 | 自由改/删 + 读时实时校验提示 + 保存时强校验 | 兼顾定义侧灵活性与数据一致性 |
| 设备类型是否编号 | 是，独立 targetType（`equipment-type`） | 与设备实例解耦，可被引用 |
| 模板是否编号 | 否，人工填名称 | 模板量小，`(TypeId,ProcessId,Name)` 唯一约束够用 |
| 删除策略 | 设备实例软删除；类型/参数/模板/值物理删除 | 设备未来被工单引用需审计；其余是配置数据 |
| 前端导航 | 侧边栏一项"设备" + 页面内 Tabs（设备/设备类型） | 遵循 AGENTS.md：同模块子视图用 Tabs 不用 SubMenu |

---

## 2. 数据模型

### 2.1 实体关系总览

```
EquipmentType (设备类型)
├─ 基础: Code / Name / Remark / IsActive / SortOrder
├─ EquipmentTypeParameter[] (参数定义 schema, 子集合, 随类型整表替换)
└─ EquipmentTemplate[] (运行模板, 独立资源)
     ├─ Name + ProcessId (唯一键: TypeId + ProcessId + Name)
     └─ EquipmentTemplateValue[] (参数值集合, 统一字符串列承载)
          └─ ParameterId → EquipmentTypeParameter

Equipment (设备实例)
├─ 基础: Code / Name / EquipmentTypeId(FK) / Specification / Remark / IsActive / SortOrder
├─ Supplier / Location
├─ Status(运行/停机/维修) / PurchaseDate? / WarrantyExpiry?
└─ ISoftDeletable (软删除)
```

### 2.2 实体定义

#### EquipmentType（设备类型）

| 字段 | 类型 | 约束 | 说明 |
|---|---|---|---|
| Id | Guid | PK | BaseEntity |
| Code | string(50) | 必填, 唯一 | 编号引擎生成 |
| Name | string(50) | 必填, 唯一 | 如"定型机" |
| Remark | string(500)? | | |
| IsActive | bool | | |
| SortOrder | int | | |
| CreatedAt / UpdatedAt | DateTime / DateTime? | BaseEntity | |
| Parameters | List\<EquipmentTypeParameter\> | 子集合 | 导航属性 |
| Templates | List\<EquipmentTemplate\> | 子集合 | 导航属性 |

> 不软删除（物理删除）。删除前校验：无设备实例引用 + 无运行模板。

#### EquipmentTypeParameter（参数定义）— 子实体

| 字段 | 类型 | 约束 | 说明 |
|---|---|---|---|
| Id | Guid | PK | |
| EquipmentTypeId | Guid | FK → equipment_types | 所属类型 |
| Name | string(50) | 必填, (TypeId+Name) 唯一 | 如"车速" |
| ValueType | ParameterValueType enum | 必填 | Number / Text / Enum |
| UnitId | Guid? | FK → measurement_units, 可空 | Number 类型用 |
| MinValue | string(50)? | | Number 类型数值下限 |
| MaxValue | string(50)? | | Number 类型数值上限 |
| Precision | int? | | Number 类型小数位限制 |
| Options | string? | JSON 数组 `["低","中","高"]` | Enum 类型可选值 |
| Required | bool | | 是否必填 |
| SortOrder | int | | 展示顺序 |
| Remark | string(500)? | | 说明 |
| CreatedAt / UpdatedAt | | BaseEntity | |

> 物理删除。MinValue/MaxValue 都可空，支持单边约束或无约束。Options 存 JSON 字符串于 text 列（项目无 jsonb 先例，保持基础类型 + 代码层语义）。

#### EquipmentTemplate（运行模板）— 独立资源

| 字段 | 类型 | 约束 | 说明 |
|---|---|---|---|
| Id | Guid | PK | |
| EquipmentTypeId | Guid | FK → equipment_types | 所属类型 |
| ProcessId | Guid | FK → processes | 适用工序 |
| Name | string(50) | 必填, (TypeId+ProcessId+Name) 唯一 | 如"轻烧" |
| Remark | string(500)? | | |
| SortOrder | int | | |
| CreatedAt / UpdatedAt | | BaseEntity | |
| Values | List\<EquipmentTemplateValue\> | 子集合 | 参数值 |

> 物理删除。不走编号引擎。复用 `equipment-type:*` 权限码（模板是类型的工艺配置）。

#### EquipmentTemplateValue（模板参数值）— 子实体

| 字段 | 类型 | 约束 | 说明 |
|---|---|---|---|
| Id | Guid | PK | |
| EquipmentTemplateId | Guid | FK → equipment_templates | 所属模板 |
| ParameterId | Guid | FK → equipment_type_parameters | 引用参数定义 |
| Value | string(200)? | | 统一字符串承载所有类型值 |
| CreatedAt / UpdatedAt | | BaseEntity | |

> (TemplateId + ParameterId) 唯一——一个模板对同一参数只有一个值。Value 可空（配合参数定义 Required=false）。

#### Equipment（设备实例）

| 字段 | 类型 | 约束 | 说明 |
|---|---|---|---|
| Id | Guid | PK | |
| Code | string(50) | 必填, 唯一 | 编号引擎生成 |
| Name | string(50) | 必填, 唯一 | |
| EquipmentTypeId | Guid | FK → equipment_types | 所属类型 |
| Specification | string(200)? | | 规格型号文本 |
| Supplier | string(100)? | | 供应商 |
| Location | string(100)? | | 地点 |
| Status | EquipmentStatus enum | | Running / Stopped / Maintenance |
| PurchaseDate | DateOnly? | | 购买日期 |
| WarrantyExpiry | DateOnly? | | 保修到期 |
| Remark | string(500)? | | |
| IsActive | bool | | |
| SortOrder | int | | |
| IsDeleted | bool | | ISoftDeletable |
| CreatedAt / UpdatedAt | | BaseEntity | |

> 软删除（HasQueryFilter）。FK 到 EquipmentType 保持宽松（nullable Guid 无反向导航，DDD 风格，与 Material→MeasurementUnit 一致）。

### 2.3 新增枚举

```csharp
// OneCup.Domain/Enums/ParameterValueType.cs
namespace OneCup.Domain.Enums;

/// <summary>
/// 设备参数的值类型，决定输入控件与校验分支。
/// </summary>
public enum ParameterValueType
{
    /// <summary>数值型 — 带 Min/Max/Precision 范围校验</summary>
    Number = 0,
    /// <summary>文本型 — 自由文本</summary>
    Text = 1,
    /// <summary>枚举型 — 值必须在 Options 列表内</summary>
    Enum = 2,
}

// OneCup.Domain/Enums/EquipmentStatus.cs
namespace OneCup.Domain.Enums;

/// <summary>
/// 设备实例的运行状态。
/// </summary>
public enum EquipmentStatus
{
    /// <summary>运行中</summary>
    Running = 0,
    /// <summary>停机</summary>
    Stopped = 1,
    /// <summary>维修中</summary>
    Maintenance = 2,
}
```

---

## 3. 校验逻辑（核心复杂度）

### 3.1 参数定义变更策略

参数定义被模板引用后**可自由修改/删除**，不做"被引用即锁定"。保证一致性的机制是：

1. **读时实时校验**：每次读取模板（列表/详情）时，按当前参数定义实时校验存量值，返回状态标记。不维护"待复核"脏标记——参数数量少，校验开销可忽略，且永远反映最新定义。
2. **保存时强校验**：编辑模板保存时，强制全量校验所有值。越界/孤儿值必须先清理或修正才能保存成功。

### 3.2 读时校验状态判定

| 情况 | status | statusMessage | UI |
|---|---|---|---|
| 值通过当前定义校验 | `valid` | 无 | 正常 |
| 数值超出 Min/Max，或小数位超限 | `invalid` | "超出最大值200" | 列表标"需更新"徽标；编辑页输入框标红 + 顶部横幅 |
| ValueType 改了导致旧值解析失败 | `invalid` | "不是有效数值" | 同上 |
| Enum 的 Options 改了，旧值不在新选项内 | `invalid` | "不是有效选项" | 同上 |
| 参数定义被删除，parameterId 变孤儿 | `orphan` | "参数已删除，请清除" | 编辑页该行显示"参数已删除"，允许删除该值 |

### 3.3 保存时校验规则（按 ValueType 分支）

| ValueType | 校验规则 |
|---|---|
| Number | `decimal.TryParse(value)` 失败→报错；`MinValue ≤ v ≤ MaxValue`（设了的才查）；小数位 ≤ `Precision` |
| Enum | `value` 必须在 `Options` 列表内 |
| Text | 长度上限（可选） |
| 通用 | `Required=true` 时 value 不得为空；每个 value 的 `parameterId` 必须属于该设备类型的参数定义；孤儿值（parameterId 对应的定义已删除）必须先清除 |

### 3.4 删除约束

| 删除对象 | 前置校验 |
|---|---|
| EquipmentType | 有设备实例引用 → 拒绝；有运行模板 → 拒绝（按 c01 走 Modal） |
| EquipmentTypeParameter | 无前置校验（物理删除，引用它的模板值变孤儿，靠读时校验检测） |
| EquipmentTemplate | 无前置校验（物理删除） |
| Equipment | 软删除，无前置校验（本轮无引用方；未来被工单引用时再加阻止逻辑） |

---

## 4. API 设计

### 4.1 设备类型（EquipmentType）— 参数定义随类型整表替换

参数定义是类型的子集合，不单独成资源。创建/更新类型时整表提交 `Parameters[]`，PUT 时按 Id 做 diff（Id=null 新增、Id 有值更新、未出现的存量 Id 删除）。

| 方法 | 路径 | 权限 | 说明 |
|---|---|---|---|
| GET | `/api/equipment-types` | `equipment-type:read` | 分页列表（keyword / code / isActive） |
| GET | `/api/equipment-types/{id}` | `equipment-type:read` | 详情：含 `Parameters[]` + `Templates[]`（模板带值 + 校验状态） |
| POST | `/api/equipment-types` | `equipment-type:create` | 创建：基础信息 + `Parameters[]`，事务内取类型编号 |
| PUT | `/api/equipment-types/{id}` | `equipment-type:update` | 更新：基础信息 + `Parameters[]` 整表替换 |
| DELETE | `/api/equipment-types/{id}` | `equipment-type:delete` | 删除：校验无设备引用 + 无模板（c01 Modal） |

### 4.2 运行模板（EquipmentTemplate）— 独立资源

模板有跨聚合 ProcessId 引用、独立唯一性键、复杂校验，独立成资源。模板不走编号引擎、复用 `equipment-type:*` 权限码。

| 方法 | 路径 | 权限 | 说明 |
|---|---|---|---|
| GET | `/api/equipment-types/{typeId}/templates` | `equipment-type:read` | 列出某类型下模板（可按 ProcessId 筛选） |
| GET | `/api/equipment-types/{typeId}/templates/{id}` | `equipment-type:read` | 详情：含 `Values[]` + 校验状态 |
| POST | `/api/equipment-types/{typeId}/templates` | `equipment-type:create` | 创建：Name + ProcessId + `Values[]`，强校验 |
| PUT | `/api/equipment-types/{typeId}/templates/{id}` | `equipment-type:update` | 更新：`Values[]` 整表替换，保存时强校验 |
| DELETE | `/api/equipment-types/{typeId}/templates/{id}` | `equipment-type:delete` | 删除 |

### 4.3 设备实例（Equipment）— 独立资源，走编号引擎

| 方法 | 路径 | 权限 | 说明 |
|---|---|---|---|
| GET | `/api/equipment` | `equipment:read` | 分页列表（keyword / code / typeId / isActive / status） |
| GET | `/api/equipment/{id}` | `equipment:read` | 详情（含类型名称） |
| POST | `/api/equipment` | `equipment:create` | 创建：事务内取设备编号（c02） |
| PUT | `/api/equipment/{id}` | `equipment:update` | 更新 |
| DELETE | `/api/equipment/{id}` | `equipment:delete` | 软删除（c01 Popconfirm） |

> 设备列表用扁平投影（不返回 Parameters/Templates），只返回 `EquipmentTypeName`、`StatusName`。详情才返回完整类型信息。

### 4.4 关键 DTO 结构

```csharp
// 创建/更新设备类型 —— 参数定义整表提交
public class CreateEquipmentTypeRequest {
    public string Name { get; set; }
    public string? Remark { get; set; }
    public bool IsActive { get; set; } = true;
    public string? CategoryCode { get; set; }       // c02: 类型编号引擎
    public List<ParameterDefinitionDto> Parameters { get; set; } = new();
}

public class ParameterDefinitionDto {
    public Guid? Id { get; set; }       // null=新增, 有值=更新; 未出现在数组里的存量 Id = 删除
    public string Name { get; set; }
    public ParameterValueType ValueType { get; set; }
    public Guid? UnitId { get; set; }
    public string? MinValue { get; set; }
    public string? MaxValue { get; set; }
    public int? Precision { get; set; }
    public List<string>? Options { get; set; }  // Enum 类型用
    public bool Required { get; set; }
    public int SortOrder { get; set; }
    public string? Remark { get; set; }
}

// 创建/更新运行模板 —— 值整表提交
public class CreateEquipmentTemplateRequest {
    public string Name { get; set; }
    public Guid ProcessId { get; set; }
    public string? Remark { get; set; }
    public int SortOrder { get; set; }
    public List<TemplateValueDto> Values { get; set; } = new();
}

public class TemplateValueDto {
    public Guid ParameterId { get; set; }
    public string? Value { get; set; }
}

// 模板详情返回 —— 带实时校验状态
public class EquipmentTemplateValueDto {
    public Guid ParameterId { get; set; }
    public string ParameterName { get; set; }
    public ParameterValueType ValueType { get; set; }
    public string? UnitSymbol { get; set; }
    public string? Value { get; set; }
    public string Status { get; set; }            // valid / invalid / orphan
    public string? StatusMessage { get; set; }
}
```

### 4.5 编号引擎接入（c02 双 targetType）

| 实体 | targetType | 前端 hook |
|---|---|---|
| EquipmentType | `NumberTargetTypes.EquipmentType = "equipment-type"`（新增） | `useNumberingPreview('EquipmentType')` |
| Equipment | `NumberTargetTypes.Equipment = "equipment"`（已有） | `useNumberingPreview('Equipment')` |

> 后端 Service.CreateAsync 必须 `_numbering.GenerateAsync(NumberTargetTypes.EquipmentType, request.CategoryCode, ct)`，禁止硬编码 null（c02 反模式）。

---

## 5. 前端设计

### 5.1 导航（遵循 AGENTS.md）

侧边栏 `menu.business` 下新增一项"设备"（不拆 SubMenu）。页面内用 Tabs 切"设备 / 设备类型"——同模块子视图用 Tabs，不用侧边栏多级嵌套。

菜单项挂 `equipment`（设备实例）读权限。设备类型 tab 可见性由 `equipment-type:read` 控制（页面内权限判断）。

### 5.2 页面结构

```
frontend/src/pages/business/equipment/
├── index.tsx                       # Tabs 容器页（设备 / 设备类型）
├── equipment/
│   ├── EquipmentTab.tsx            # 设备列表（标准 Query Table，从模板复制）
│   ├── EquipmentForm.tsx           # 设备新建/编辑 Modal（c02 + useNumberingPreview('Equipment')）
│   └── EquipmentDetail.tsx         # 设备详情 Drawer
├── type/
│   ├── TypeTab.tsx                 # 类型列表（标准 Query Table）
│   ├── TypeForm.tsx                # 类型新建/编辑 Modal（c02 + 参数定义动态表格）
│   ├── TypeDetail.tsx              # 类型详情 Drawer（参数 + 模板列表）
│   ├── ParameterEditor.tsx         # 参数定义动态表格组件（增删行 + ValueType 切换控件）
│   └── template/
│       ├── TemplateList.tsx        # 模板列表（类型详情内嵌或独立 Modal）
│       ├── TemplateForm.tsx        # 模板新建/编辑（值输入 + 实时校验状态）
│       └── TemplateValueEditor.tsx # 模板值动态表单（按参数定义渲染控件）
├── api/
│   └── equipment.ts                # 三个资源 API client
├── locale/ (index.ts + zh-CN.ts + en-US.ts)
└── style/ (index.module.less)
```

### 5.3 关键交互

**类型表单（TypeForm）— 参数定义动态表格**：类型表单内嵌可增删行的参数定义表格。`ValueType` 选"数值"展开单位/Min/Max/Precision 子控件；选"枚举"展开 Options 输入；选"文本"收起。`Parameters[]` 随类型一起提交。

**模板表单（TemplateForm）— 值动态表单 + 校验状态**：输入控件由所属设备类型的参数定义驱动——Number=InputNumber、Enum=Select、Text=Input。编辑时读取带 `status` 的值列表，`invalid/orphan` 行标红 + 顶部 Alert 汇总。保存前端可即时校验，但**后端强校验是唯一真相**。

**设备表单（EquipmentForm）— 标准 c02 表单**：走 c02 hook，结构与客户表单一致。设备类型选择器异步拉 `/api/equipment-types?isActive=true`。

### 5.4 复用资产

| 资产 | 用途 |
|---|---|
| `useNumberingPreview('Equipment')` / `useNumberingPreview('EquipmentType')` | 两个表单编号预览 |
| `<CategorySelect>` | 两个表单条件渲染（`!editing && preview.includeCategory`） |
| `customer/form.tsx` | c02 主参考实例 |
| `query-table-page.template.tsx` | 两个列表页模板来源 |

---

## 6. 迁移与 Seed

### 6.1 新增 Seed 常量（SeedData.cs 追加）

```csharp
// === Equipment 模块（feat/equipment-mgmt）===
// 设备实例 Equipment 权限码已存在: PermEquipmentRead/Create/Update/Delete (...0309-030c)
// 设备实例 targetType 已存在: TargetTypeEquipment (...0203, code="equipment")
// 以下为设备类型 EquipmentType 新增:
public static readonly Guid PermEquipmentTypeRead   = Guid.Parse("00000000-0000-0000-0000-00000000032f");
public static readonly Guid PermEquipmentTypeCreate = Guid.Parse("00000000-0000-0000-0000-000000000330");
public static readonly Guid PermEquipmentTypeUpdate = Guid.Parse("00000000-0000-0000-0000-000000000331");
public static readonly Guid PermEquipmentTypeDelete = Guid.Parse("00000000-0000-0000-0000-000000000332");
public static readonly Guid TargetTypeEquipmentType = Guid.Parse("00000000-0000-0000-0000-000000000208");
```

### 6.2 DbContext Seed() 追加

```csharp
// ── EquipmentType 权限 ──
new Permission { Id = SeedData.PermEquipmentTypeRead,   Code = "equipment-type:read",   Name = "查看设备类型", CreatedAt = SeedTimestamp },
new Permission { Id = SeedData.PermEquipmentTypeCreate, Code = "equipment-type:create", Name = "录入设备类型", CreatedAt = SeedTimestamp },
new Permission { Id = SeedData.PermEquipmentTypeUpdate, Code = "equipment-type:update", Name = "编辑设备类型", CreatedAt = SeedTimestamp },
new Permission { Id = SeedData.PermEquipmentTypeDelete, Code = "equipment-type:delete", Name = "删除设备类型", CreatedAt = SeedTimestamp },

// ── role_permissions: developerPerms 数组追加 PermEquipmentRead + PermEquipmentTypeRead ──

// ── EquipmentType targetType 字典 ──
new NumberingTargetType { Id = SeedData.TargetTypeEquipmentType, Code = "equipment-type",
    NameZh = "设备类型", NameEn = "EquipmentType", SortOrder = 8, IsActive = true, CreatedAt = SeedTimestamp },
```

> 不 seed 业务数据（设备实例/类型/参数/模板），不 seed 编号规则（用户在 UI 配置）。

### 6.3 NumberTargetTypes 常量追加

```csharp
public const string EquipmentType = "equipment-type";   // 新增；Equipment 已存在
```

### 6.4 EF 迁移：单次 `AddEquipmentModule`

遵循现有约定（snake_case 表/列名、`PK_`/`IX_` 前缀、`timestamp with time zone` 时间戳、`uuid` 主键）。按 FK 依赖建表：

1. `equipment_types`（无 FK）
2. `equipment_type_parameters`（FK → equipment_types；measurement_units 可空 FK）
3. `equipment_templates`（FK → equipment_types, processes）
4. `equipment_template_values`（FK → equipment_templates, equipment_type_parameters）
5. `equipments`（FK → equipment_types）

唯一索引：

| 表 | 唯一索引 |
|---|---|
| equipment_types | `(code)`、`(name)` |
| equipment_type_parameters | `(equipment_type_id, name)` |
| equipment_templates | `(equipment_type_id, process_id, name)` |
| equipment_template_values | `(equipment_template_id, parameter_id)` |
| equipments | `(code)`、`(name)` |

软删除：仅 `equipments` 加 `is_deleted` + `HasQueryFilter`。其余 4 表物理删除。

---

## 7. 后端文件清单（遵循分层约定）

| 层 | 文件 |
|---|---|
| 实体 | `Domain/Entities/EquipmentType.cs`、`EquipmentTypeParameter.cs`、`EquipmentTemplate.cs`、`EquipmentTemplateValue.cs`、`Equipment.cs` |
| 枚举 | `Domain/Enums/ParameterValueType.cs`、`EquipmentStatus.cs` |
| DTO | `Application/Dtos/System/EquipmentDtos.cs`（含所有 DTO） |
| 接口 | `Application/Interfaces/IEquipmentTypeService.cs`、`IEquipmentTemplateService.cs`、`IEquipmentService.cs` |
| 服务 | `Application/Services/EquipmentTypeService.cs`、`EquipmentTemplateService.cs`、`EquipmentService.cs` |
| 查询规范 | `Application/Specifications/EquipmentTypeSpecs.cs`、`EquipmentSpecs.cs`（模板用类型服务内查询） |
| 校验器 | `Application/Validators/Equipment/`（Create/Update 各模块） |
| EF 配置 | `Infrastructure/Persistence/Configurations/`（5 个实体各一个） |
| 迁移 | `Infrastructure/Migrations/<timestamp>_AddEquipmentModule.cs` |
| 控制器 | `Api/Controllers/EquipmentTypesController.cs`、`EquipmentTemplatesController.cs`、`EquipmentsController.cs` |
| DI 注册 | `Api/Program.cs`（3 个服务 + 校验器自动注册） |
| DbSet | `OneCupDbContext.cs`（5 个 DbSet） |

---

## 8. 测试范围

### 8.1 后端单元测试（`OneCup.UnitTests/Equipment/`）

参照 `OneCup.UnitTests/Customer/`、`Material/` 结构，用 Fake 服务替身。

| 测试类 | 覆盖点 |
|---|---|
| `EquipmentTypeServiceTests` | CRUD；参数定义整表替换 diff（增/改/删）；名称唯一性；类型编号取号走事务；删除校验（无设备引用 + 无模板） |
| `EquipmentTemplateServiceTests` | 创建按 ValueType 校验（数值范围/小数位/枚举选项/必填）；`(TypeId,ProcessId,Name)` 唯一性；编辑实时校验状态返回（valid/invalid/orphan）；保存强校验拦截越界值；参数定义删除后值标记 orphan |
| `EquipmentServiceTests` | CRUD；编号取号走 c02（categoryCode 透传，禁止 null 硬编码）；唯一性；软删除幂等 |

### 8.2 校验逻辑必须覆盖的场景

```
数值参数:
  ✓ 值在 [Min, Max] 内 → 通过
  ✗ 值 > Max → 报错"超出最大值"
  ✗ 值 < Min → 报错"低于最小值"
  ✗ 非数字字符串 → 报错"不是有效数值"
  ✗ 小数位 > Precision → 报错"小数位超限"
  ✓ Min/Max 为空 → 任意数值通过

枚举参数:
  ✓ 值在 Options 内 → 通过
  ✗ 值不在 Options 内 → 报错"不是有效选项"

通用:
  ✓ Required=false 且值为空 → 通过
  ✗ Required=true 且值为空 → 报错"必填"
  ✗ parameterId 不属于该设备类型 → 报错"参数不属于此类型"

孤儿值（编辑场景）:
  ✓ 参数定义已删除 → 读时 status=orphan；保存时必须清除才能保存成功
```

### 8.3 前端

项目现状前端无单测（Material/Process 无），本轮不新增，保持一致。

---

## 9. 遵循的约定

| 约定 ID | 应用点 |
|---|---|
| c01 | 设备类型删除走 Modal（影响范围大：可能影响多台设备 + 模板，不可逆）；设备实例删除走 Popconfirm（单条、软删除可恢复）；模板删除走 Popconfirm（单条、物理删除但影响范围小） |
| c02 | 设备类型、设备实例两个表单走 `useNumberingPreview` + `<CategorySelect>`；后端透传 categoryCode；禁止 null 硬编码 |
| 列表页标准 | 设备列表、类型列表从 `query-table-page.template.tsx` 复制，单 Card + Form+Grid 三列 + 按钮表单外侧 |
| 导航架构 | 侧边栏一项 + 页面内 Tabs，不拆 SubMenu |
