# OneCup 计量单位管理设计文档

> 印染厂面料开发管理系统 — 基础数据字典：计量单位字典 + 同类换算
> 创建日期: 2026-07-04
> 状态: 待实现
> 并行开发: 遵循 [parallel-dev-contract.md](../../parallel-dev-contract.md)，worktree `feat/unit-mgmt`
> 关联: [编号字典化设计](../../specs/2026-07-03-numbering-dictionary-design.md)（已实现，本模块为其架构同构的参照模板）

---

## 1. 背景与目标

### 1.1 现状

系统已有编号管理（含业务类型/分类字典）、颜色管理等基础数据模块。计量单位是印染厂另一类基础数据——
面料按长度（米/码）、染化料按重量（千克/克）、纱线按细度（tex/denier）、成品按数量（件/卷/匹）。
当前系统**没有任何计量单位管理**，未来面料/原料等业务模块出现时各自硬编码单位，缺乏统一字典与换算能力。

### 1.2 目标

> **计量单位做成可配置字典，支持同类别单位间的换算。**

- 单位字典：有 code（英文标识符）+ 中文名 + 英文名 + 符号 + 类别 + 换算系数，可查可配可停用。
- 同类换算：同类别单位间通过"基准单位中转"换算（A→base→B），公式 `result = qty × factor(A) / factor(B)`。
- 独立字典：不绑定具体业务表（面料/原料等），不联动编号字典模块。未来业务模块按需引用单位 code。

### 1.3 本轮交付范围

| 模块 | 功能 |
|------|------|
| **单位字典** | 单表 + 完整 CRUD（列表/详情/新增/编辑/启停）+ 种子迁移 19 个默认单位 |
| **同类换算** | 后端换算计算接口（无副作用），同类单位中转换算 |
| **前端管理页** | 标准查询表格页 + 新建/编辑 Drawer + 换算 Drawer |

### 1.4 不在范围内

- 业务模块（面料/原料）对单位 code 的引用（业务模块尚未建，按编号字典模式留待各自接入）
- 单位的物理删除（统一只启停，与编号字典一致）
- 跨类别换算（明确拒绝，类别不同的单位不可换算）
- 单位类别的独立管理表（category 作为单位字符串属性，不单独成表）
- 单位换算历史记录（换算实时计算，无副作用，不留存）

### 1.5 成功标准

- 管理员可在前端纯配置新增一个计量单位（如 `bag` / 袋 / COUNT 类），全程不改代码。
- 同类单位换算正确：`10 yard = 9.144 meter`、`10 tex = 90 denier`。
- 跨类换算被拒并返回友好错误。
- 基准单位约束生效：每类别有且仅有一个基准，停用基准被拒。
- 种子 19 个单位迁移后即可用，覆盖印染厂主流计量场景。

---

## 2. 数据模型

新增一张表，遵循现有 `BaseEntity`（`id` / `created_at` / `updated_at`）模式。与现有编号/字典表一致，
**不建物理外键**，关联是应用层逻辑（保持架构一致）。

### 2.1 表：`measurement_units`（计量单位字典）

| 列 | 类型 | 约束 | 说明 |
|----|------|------|------|
| `id` | uuid | PK | 主键 |
| `code` | varchar(32) | NOT NULL, **UNIQUE** | 英文标识符，如 `kg`。创建后不可改 |
| `name_zh` | varchar(64) | NOT NULL | 中文名，如"千克" |
| `name_en` | varchar(64) | NOT NULL | 英文名，如"Kilogram" |
| `symbol` | varchar(16) | NOT NULL | 符号，如 `kg` / `m` / `tex` |
| `category` | varchar(32) | NOT NULL | 单位类别，如 `LENGTH`/`WEIGHT`/`YARN` |
| `is_base` | boolean | NOT NULL DEFAULT false | 是否该类别基准单位 |
| `factor` | numeric(18,8) | NOT NULL DEFAULT 1 | 相对基准的换算系数（基准=1） |
| `precision` | int | NOT NULL DEFAULT 2 | 展示小数位数（0–6） |
| `sort_order` | int | NOT NULL DEFAULT 0 | 排序号 |
| `is_active` | boolean | NOT NULL DEFAULT true | 启停状态 |
| `created_at` | timestamptz | NOT NULL | 审计 |
| `updated_at` | timestamptz | | 审计 |

唯一索引：`ux_measurement_units_code` ON `(code)`。
普通索引：`ix_measurement_units_category` ON `(category)`（按类别查询常用：列表筛选、换算校验）。

### 2.2 关键设计决策

**① factor 用 numeric(18,8) 高精度**

换算系数（如 磅=0.453592、平方码=0.836127）需要足够精度，避免累积误差。`numeric(18,8)` 给 8 位小数，
足够纺织计量。换算结果在 Service 层按 `precision` 四舍五入输出。

**② category 是字符串属性，不独立成表**

category 值由用户填（前端给常用枚举 + allowCreate）。类别列表通过 `SELECT DISTINCT category` 得出。
与编号字典 `code` 字段同理——单位字典只管单位，类别是单位的属性。

**③ 基准单位约束在应用层保证**

每 category 有且仅一个 `is_base=true`。DB 不加 partial unique index（保持与编号字典"应用层校验"约定一致）。
校验逻辑见第 3 节 Service。

**④ 数量类（COUNT）特殊语义**

件/卷/匹/套 factor 都为 1，换算时结果不变（10件→卷 = 10）。不禁止换算，保持公式统一简单。

**⑤ code 全局唯一（非 category 内唯一）**

与编号字典的 target_type code 一致——code 是全局唯一标识符。category 只是分组属性，不参与唯一性。

**⑥ 换算公式：基准中转**

```
result = quantity × factor(from) / factor(to)
```

基准单位 factor=1，非基准 factor 是相对基准的系数。例：长度类基准=米(factor=1)，码(factor=0.9144)，
则 `10 yard → meter = 10 × 0.9144 / 1 = 9.144`。纱线类基准=tex(factor=1)，denier(factor=9)，
则 `10 denier → tex = 10 × 9 / 1 = 90`，`10 tex → denier = 10 × 1 / 9 ≈ 1.11`。

---

## 3. 后端 API 与 Service 校验逻辑

新增独立控制器 `MeasurementUnitsController`（路由 `api/measurement-units`），复用新增权限
`unit-view` / `unit-manage`。结构参照 `NumberingDictionaryController`。

### 3.1 API 端点

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/api/measurement-units` | `unit-view` | 分页列表（keyword / category / isActive 筛选） |
| `GET` | `/api/measurement-units/all` | `unit-view` | 不分页全量启用项（按 SortOrder），前端下拉用 |
| `GET` | `/api/measurement-units/categories` | `unit-view` | 返回 `SELECT DISTINCT category` 去重类别列表，前端筛选下拉用 |
| `GET` | `/api/measurement-units/{id}` | `unit-view` | 详情 |
| `POST` | `/api/measurement-units` | `unit-manage` | 新增 |
| `PUT` | `/api/measurement-units/{id}` | `unit-manage` | 编辑（code/category 不可改，DTO 不暴露） |
| `PUT` | `/api/measurement-units/{id}/status` | `unit-manage` | 启停切换 |
| `POST` | `/api/measurement-units/convert` | `unit-view` | 换算计算（无副作用） |

> 比"标准字典 CRUD"多两个端点：`/categories`（类别去重下拉）和 `/convert`（换算计算）。
> 其余结构与 `NumberingDictionaryController` 完全平行。

### 3.2 Service 校验逻辑

**① CreateAsync(request)**

```
1. EnsureValidAsync（格式校验，见第 6 节 Validator）
2. code 唯一性：UnitByCodeSpec(code) 命中 → DomainException "单位 code '{code}' 已存在"
3. 基准处理：
   - 若 IsBase=true：
       a. UnitBaseByCategorySpec(category) 命中 → 拒绝 "类别 '{category}' 已有基准单位"
       b. 强制 Factor=1（防止用户传错）
   - 若 IsBase=false：
       Factor 保持用户输入（校验已保证 > 0）
4. 写入，IsActive=true，SaveChanges
```

**② UpdateAsync(id, request)**（DTO 不含 Code/Category，故两者不可改）

```
1. 取实体（UnitByIdSpec），不存在 → DomainException "单位不存在"
2. 若 request.Factor 有值：
   - 若该实体 IsBase=true → 拒绝 "基准单位的换算系数固定为 1，不可修改"
     （要改系数先取消其基准身份）
   - 否则赋值
3. 若 request.IsBase 有值（DTO 含此字段，允许切换基准身份）：
   - 从 false→true（指定为新基准）：
       a. UnitBaseByCategorySpec(category, excludingId=当前id) 命中 → 拒绝 "已有基准"
       b. 当前实体 IsBase=true, Factor=1
       c. 旧基准（若有）置 IsBase=false  ← 自动降级旧基准
   - 从 true→false（取消基准）：
       检查该 category 是否还有其他基准 → 若无则拒绝 "每个类别必须保留一个基准"
4. name/symbol/precision/sortOrder 按非空赋值
5. SaveChanges
```

> **基准切换的原子性**：`false→true` 时在同一 `SaveChanges` 内把旧基准降级，保证不出现
> "两个基准"或"零基准"中间态。

**③ UpdateStatusAsync(id, isActive)**

```
1. 取实体（UnitByIdSpec），不存在 → DomainException "单位不存在"
2. 若 isActive=false 且该实体 IsBase=true → 拒绝 "不能停用基准单位，请先将其他单位设为基准"
3. entity.IsActive = isActive; SaveChanges
```

**④ ConvertAsync(fromCode, toCode, quantity)**

```
1. 取 from 单位：用 UnitByCodeSpec(fromCode)（该 spec 仅按 code 过滤，不含 IsActive 过滤）
   - 查不到 → DomainException "单位 '{fromCode}' 不存在"
   - 查到但 !IsActive → DomainException "单位 '{fromCode}' 已停用"
   （注：spec 故意不过滤 IsActive，以便区分"不存在"与"已停用"两种错误）
2. 取 to 单位：同样用 UnitByCodeSpec(toCode) 取实体，再判 IsActive
3. 若 from.Category != to.Category → DomainException
   "单位 '{from}'({cat1}) 与 '{to}'({cat2}) 类别不同，无法换算"
4. result = quantity × from.Factor / to.Factor
5. 四舍五入到 to.Precision 位小数
6. 返回 { quantity: result, fromCode, toCode, precision: to.Precision }
```

> **数量类（COUNT）天然兼容**：件/卷/匹 factor 都为 1，`10 × 1 / 1 = 10`，结果不变，无需特判。

### 3.3 设计决策

**① 提供 `/categories` 去重接口而非前端写死**

类别是用户可自定义的（前端 allowCreate），所以类别下拉必须动态。`SELECT DISTINCT category WHERE is_active`
返回当前所有类别。比前端写死枚举更灵活，且实现简单（一次 `ListAsync` + 内存去重）。

**② 换算走 POST 而非 GET**

语义上是"计算"而非"取资源"。且 quantity 是数值参数放 body 比 query string 清晰。

**③ ConvertAsync 校验启用状态**

停用的单位不参与换算（与下拉不显示一致），避免用已停用单位算出误导性结果。
用不过滤 IsActive 的 `UnitByCodeSpec` 取实体后，在应用层判断启用状态，以区分"不存在"与"已停用"错误。

**④ 基准切换允许，但有保护**

管理员可以重新指定基准（比如想从"米"改为"码"作为长度基准），系统自动降级旧基准。比"锁定基准不可改"
更实用——初始种子后仍可调整。

---

## 4. 前端设计

### 4.1 页面结构

路由 `system/unit`，菜单挂在"系统管理"下（平级菜单项，非子菜单——遵循 AGENTS.md 导航规范）。
**单页查询表格 + 换算 Drawer**，从 `query-table-page.template.tsx` 复制布局骨架。

```
┌─────────────────────────────────────────────────────┐
│  计量单位管理                                          │
├─────────────────────────────────────────────────────┤
│  ┌─查询区(Form+Grid三列)────────────┐ ┌查询/重置┐    │
│  │ 关键词      类别▼      状态▼      │ │        │    │
│  └─────────────────────────────────┘ └────────┘    │
│                                                      │
│  [新建]                                    [换算]     │  ← 工具栏 space-between
│  ┌──────────────────────────────────────────────┐   │
│  │ code 符号 中文名 英文名 类别 基准 系数 精度 状态 操作│   │
│  │ kg  kg  千克  Kilogram WEIGHT ✓ 1    2   启用 编辑│   │
│  │ ...                                           │   │
│  └──────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────┘
```

### 4.2 查询区（遵守 frontend-standards 硬性规则）

- 单个 `<Card>` 整页，`<Typography.Title heading={6}>` 标题
- `<Form>` + `<Row gutter={24}>` + `<Col span={8}>`，三字段：
  - 关键词（`Input allowClear`，匹配 code/中英文名）
  - 类别（`Select allowClear allowCreate`，选项从 `/categories` 接口拉取）
  - 状态（`Select allowClear`，启用/停用）
- 查询/重置按钮在表单**外侧** `div.className={styles['right-button']}`
- 仅按钮触发查询（`getFieldsValue`），禁止字段 onChange 自动查询
- 重置按钮文案"重置" + `IconRefresh`

### 4.3 表格工具栏

`div.className={styles['button-group']}` flex space-between：
- 左 `<Space>`：新建按钮（`IconPlus`）
- 右 `<Space>`：换算按钮（`IconSwap` 图标）

### 4.4 列定义

| 列 | dataIndex | 宽 | 渲染 |
|----|-----------|-----|------|
| 编码 | code | 100 | |
| 符号 | symbol | 80 | |
| 中文名 | nameZh | 100 | |
| 英文名 | nameEn | 120 | |
| 类别 | category | 100 | `<Tag>` |
| 基准 | isBase | 80 | 是→`<Tag color="green">基准</Tag>`，否→空 |
| 换算系数 | factor | 100 | 基准显示 `1`，非基准显示 factor |
| 精度 | precision | 70 | |
| 状态 | isActive | 80 | 启用→绿 Tag，停用→灰 Tag |
| 操作 | — | 160 | 编辑 / 启用·停用（Popconfirm） |

### 4.5 新建/编辑 Drawer

`layout="vertical"`，字段：
- **code**（`Input`）：新建必填（正则校验）；编辑时**锁定禁显**（disabled 显示当前值）+ Alert 提示"创建后不可改"
- **category**（`Input`/`Select allowCreate`）：新建必填（大写枚举式）；编辑时锁定禁显
- **symbol**（`Input`）：必填
- **nameZh** / **nameEn**（`Input`）：必填
- **isBase**（`Switch`）：新建默认关；开启时 factor 输入框**禁用**并锁定为 1；编辑时切换会触发基准切换逻辑（后端处理）
- **factor**（`InputNumber min=0 step=0.00000001`）：isBase=true 时禁用显示 1
- **precision**（`InputNumber min=0 max=6`）
- **sortOrder**（`InputNumber min=0`）

### 4.6 换算 Drawer

工具栏"换算"按钮触发，独立 Drawer：
- **源单位**（`Select`）：从 `/all` 拉取启用单位，按 category 分组（`Select.OptGroup`）
- **目标单位**（`Select`）：同上。**联动过滤**——只显示与源单位同 category 的选项（避免选错类别）
- **数量**（`InputNumber`）：默认 1
- **结果**：实时显示。三个字段任一变化即调 `/convert` 接口（debounce 300ms），展示：
  `{quantity} {fromSymbol} = {result} {toSymbol}`

### 4.7 设计决策

**① 菜单平级而非子菜单**

单位管理与编号管理是不同系统级模块，按 AGENTS.md 导航规范用侧边栏平级菜单项，不嵌套 SubMenu。

**② 换算用独立 Drawer 而非页面内联**

换算是辅助工具非主流程，放 Drawer 不干扰主表格。且换算涉及两个下拉 + 实时计算，Drawer 空间足够。

**③ 目标单位联动过滤同 category**

后端虽会校验类别不一致并返回友好错误，但前端预先过滤更优体验——用户根本选不到不同类的目标单位。

**④ factor 输入框随 isBase 联动禁用**

基准 factor 恒为 1，UI 禁用输入框避免用户困惑。提交时若 isBase=true，前端可不传 factor（后端强制设 1）。

**⑤ 前端测试只做布局结构断言**

与编号字典一致（项目现有测试体系不覆盖前端业务交互），前端测试只做布局结构断言（Card/Form/按钮文案），
符合 frontend-standards。

---

## 5. 迁移与种子数据

### 5.1 一次迁移 `AddUnitModule`

建 `measurement_units` 表（带 code 唯一索引 + category 普通索引）+ 种子 19 个单位 + 2 个权限。

> EF 命令：`dotnet ef migrations add AddUnitModule --project src/OneCup.Infrastructure --startup-project src/OneCup.Api`
> （遵循 parallel-dev-contract §3.2，迁移命名防撞名）

### 5.2 种子单位（19 个，6 类）

全部 `is_active=true`，`created_at` 用 DbContext 的 `SeedTimestamp`（2026-07-01）：

| 类别 | code | 符号 | 中文名 | 英文名 | is_base | factor | precision | sort_order |
|------|------|------|--------|--------|---------|--------|-----------|------------|
| LENGTH | meter | m | 米 | Meter | true | 1 | 2 | 1 |
| LENGTH | decimeter | dm | 分米 | Decimeter | false | 0.1 | 2 | 2 |
| LENGTH | centimeter | cm | 厘米 | Centimeter | false | 0.01 | 2 | 3 |
| LENGTH | yard | yd | 码 | Yard | false | 0.9144 | 2 | 4 |
| LENGTH | foot | ft | 英尺 | Foot | false | 0.3048 | 2 | 5 |
| WEIGHT | kilogram | kg | 千克 | Kilogram | true | 1 | 2 | 1 |
| WEIGHT | gram | g | 克 | Gram | false | 0.001 | 2 | 2 |
| WEIGHT | ton | t | 吨 | Ton | false | 1000 | 2 | 3 |
| WEIGHT | pound | lb | 磅 | Pound | false | 0.453592 | 2 | 4 |
| AREA | square_meter | ㎡ | 平方米 | Square Meter | true | 1 | 2 | 1 |
| AREA | square_yard | yd² | 平方码 | Square Yard | false | 0.836127 | 2 | 2 |
| COUNT | piece | 件 | 件 | Piece | true | 1 | 0 | 1 |
| COUNT | roll | 卷 | 卷 | Roll | false | 1 | 0 | 2 |
| COUNT | bolt | 匹 | 匹 | Bolt | false | 1 | 0 | 3 |
| COUNT | set | 套 | 套 | Set | false | 1 | 0 | 4 |
| VOLUME | liter | L | 升 | Liter | true | 1 | 2 | 1 |
| VOLUME | milliliter | mL | 毫升 | Milliliter | false | 0.001 | 2 | 2 |
| YARN | tex | tex | 特 | Tex | true | 1 | 2 | 1 |
| YARN | dtex | dtex | 分特 | Decitex | false | 10 | 2 | 2 |
| YARN | denier | D | 旦尼尔 | Denier | false | 9 | 2 | 3 |

> **纱线类（YARN）说明**：采用定长制（tex/dtex/denier），三者正比线性换算。
> 1 tex = 10 dtex = 9 denier。不引入定重制（支数 Nm/Ne），因其与定长制是倒数关系，
> 无法用 factor 乘除中转公式正确换算。

### 5.3 种子权限（2 个）

```csharp
// ===== Unit 模块 =====
public static readonly Guid PermUnitRead  = Guid.Parse("00000000-0000-0000-0000-000000000121");
public static readonly Guid PermUnitWrite = Guid.Parse("00000000-0000-0000-0000-000000000122");
```

`Seed()` 追加：
- `PermUnitRead`（121）→ code `system:unit:view`，name "查看计量单位"
- `PermUnitWrite`（122）→ code `system:unit:manage`，name "管理计量单位"

> **Guid 分配严格遵守 parallel-dev-contract §3.1**：121–123 段归单位管理，118–120 缓冲段绝不碰。
> `TargetTypeUnit=...211` 属编号体系，本模块独立字典不联动编号，**不使用 211**。

---

## 6. 校验（FluentValidation）

`CreateUnitRequestValidator` 参照 `CreateUserRequestValidator` 模式，只做无 IO 的格式校验
（业务规则在 Service 层）：

| 字段 | 规则 |
|------|------|
| Code | NotEmpty，Length(1,32)，正则 `^[a-z][a-z0-9_]*$`（小写英文标识符） |
| Category | NotEmpty，Length(1,32)，正则 `^[A-Z][A-Z0-9_]*$`（大写枚举式） |
| NameZh / NameEn | NotEmpty，Length(1,64) |
| Symbol | NotEmpty，Length(1,16) |
| IsBase | 无（boolean） |
| Factor | GreaterThan(0) |
| Precision | InclusiveBetween(0, 6) |
| SortOrder | GreaterThanOrEqual(0) |

> UpdateRequest 不做独立 validator（字段全可空，格式由类型保证）；Service 层对非空字段直接赋值。

---

## 7. 测试策略

遵循现有测试模式（InMemory 单元测试，不用 mock），参照 `NumberingDictionaryServiceTests`。

### 7.1 后端 Service 测试

**`backend/tests/OneCup.UnitTests/MeasurementUnit/MeasurementUnitServiceTests.cs`**

| 测试 | 覆盖点 |
|------|--------|
| CreateAsync_CreatesUnit | 正常创建，IsActive=true |
| CreateAsync_DuplicateCode_Throws | code 唯一性 |
| CreateAsync_BaseFactor_ForcedToOne | IsBase=true 时 factor 强制为 1（即使传 5） |
| CreateAsync_SecondBaseInCategory_Throws | 同 category 第二个基准被拒 |
| CreateAsync_NonBase_InDifferentCategory_Ok | 不同 category 各自一个基准，不冲突 |
| UpdateAsync_CodeCategory_NotExposed | DTO 不含 code/category，不可改 |
| UpdateAsync_BaseFactorChange_Throws | 基准单位改 factor 被拒 |
| UpdateAsync_SwitchBase_DemotesOldBase | 切换基准：旧基准自动降级为非基准 |
| UpdateAsync_RemoveLastBase_Throws | 取消最后一个基准被拒 |
| UpdateStatusAsync_DeactivateBase_Throws | 停用基准被拒 |
| ConvertAsync_SameCategory_Ok | 同类换算正确（长度 yard→meter） |
| ConvertAsync_DifferentCategory_Throws | 不同类拒绝 |
| ConvertAsync_DeactivatedUnit_Throws | 停用单位拒绝 |
| ConvertAsync_NonExistent_Throws | 不存在的 code 拒绝 |
| ConvertAsync_CountClass_ResultUnchanged | 数量类 factor=1 结果不变 |
| ConvertAsync_YarnClass_Ok | 纱线类 tex↔denier 正比换算正确 |
| Specs（FilterSpec/PagedSpec） | keyword+category+isActive 多条件、分页/计数分离 |

### 7.2 后端 Validator 测试

**`backend/tests/OneCup.UnitTests/MeasurementUnit/CreateUnitRequestValidatorTests.cs`**（snake_case 命名）

| 测试 | 覆盖点 |
|------|--------|
| Valid_request_passes | 合法请求通过 |
| Empty_code_fails | 空 code |
| Uppercase_code_fails | 大写 code 不合规 |
| Empty_category_fails / Lowercase_category_fails | category 格式 |
| Zero_factor_fails | factor=0 不合规 |
| Precision_out_of_range_fails | precision=7 超界 |

### 7.3 前端测试

**`frontend/src/pages/system/unit/__tests__/index.test.tsx`**（参照 user 页模板）

| 测试 | 覆盖点 |
|------|--------|
| 渲染在单个 Card 内 | `.arco-card` 存在 |
| 查询区用 Form | `.arco-form` 存在 |
| 有查询和重置按钮 | findByRole button |
| 新建按钮在工具栏 | `计量单位` 文案出现后断言 |
| 换算按钮存在 | 换算按钮可点击 |

---

## 8. 涉及文件清单

### 8.1 后端新增（9）

- `OneCup.Domain/Entities/MeasurementUnit.cs`
- `OneCup.Infrastructure/Persistence/Configurations/MeasurementUnitConfiguration.cs`
- `OneCup.Application/Dtos/System/MeasurementUnitDtos.cs`
- `OneCup.Application/Validators/System/CreateUnitRequestValidator.cs`
- `OneCup.Application/Specifications/MeasurementUnitSpecs.cs`
- `OneCup.Application/Interfaces/IMeasurementUnitService.cs`
- `OneCup.Application/Services/MeasurementUnitService.cs`
- `OneCup.Api/Controllers/MeasurementUnitsController.cs`
- 测试 ×2（`MeasurementUnitServiceTests.cs` + `CreateUnitRequestValidatorTests.cs`）

### 8.2 后端共享文件修改（4，严格按 parallel-dev-contract §3）

- `OneCup.Infrastructure/Persistence/SeedData.cs` — 追加 `PermUnitRead=...121`、`PermUnitWrite=...122`
- `OneCup.Infrastructure/Persistence/OneCupDbContext.cs` — 追加 `DbSet<MeasurementUnit>` + `Seed()` 末尾追加 19 个单位 + 2 个权限
- `OneCup.Api/Program.cs` — 追加 DI 注册 + 2 个授权策略（`unit-view`/`unit-manage`）
- `OneCup.Infrastructure/Migrations/<timestamp>_AddUnitModule.cs`（+ Designer + ModelSnapshot 自动刷新）

### 8.3 前端新增（7）

- `frontend/src/api/measurementUnit.ts`
- `frontend/src/pages/system/unit/index.tsx`
- `frontend/src/pages/system/unit/locale/index.ts`
- `frontend/src/pages/system/unit/locale/zh-CN.ts`
- `frontend/src/pages/system/unit/locale/en-US.ts`
- `frontend/src/pages/system/unit/style/index.module.less`
- `frontend/src/pages/system/unit/__tests__/index.test.tsx`

### 8.4 前端共享文件修改（3，parallel-dev-contract §3.4 数组末尾追加）

- `frontend/src/routes.ts` — `menu.system.children` 末尾追加 unit 菜单项
- `frontend/src/router.tsx` — 追加 lazy import + element（`RequirePermission resource="system:unit"`）
- `frontend/src/locale/index.ts` — en-US + zh-CN 两处追加 `menu.system.unit`

### 8.5 验证

1. `dotnet build backend/OneCup.sln` 全绿
2. `dotnet test backend/OneCup.sln` 全绿
3. `cd frontend && npm run build` 全绿
4. `cd frontend && npm test` 全绿
5. `dotnet ef database update`（本地库验证迁移可应用）
