# OneCup 编号模块字典化设计文档

> 印染厂面料开发管理系统 — 编号引擎增强：业务类型与分类的可配置字典
> 创建日期: 2026-07-03
> 状态: 待实现
> 关联: [编号管理（编码规则引擎）设计](2026-07-02-numbering-design.md)（已实现，本设计为其增强）

---

## 1. 背景与目标

### 1.1 现状痛点

编号引擎（`2026-07-02-numbering-design.md`）已上线，但存在两个痛点：

**痛点 1：业务类型不可查询、命名不友好**
- `TargetType` 是裸字符串（`fabric` / `material` …），仅存在于 `NumberingRule` / `NumberingLog` / `NumberingCounter` 表的业务字段里。
- 前端下拉选项 `TARGET_TYPE_OPTIONS` 写死在 `pages/system/numbering/index.tsx`，新增业务类型必须改代码。
- 引擎**故意不校验** target_type 合法性（设计 6.1），所以任何字符串都能用——包括拼写错误。
- 用户在前端虽然能 `allowCreate` 动态输入，但输入中文不优雅（拼进编码乱码），输入英文用户又不认识。

**痛点 2：分类码（categoryCode）无处定义**
- 分类码是编号拼码中的第二段（`[前缀][分类码][日期][流水号]`），如 `FAB-COT-2026-0001` 中的 `COT`。
- 但**当前没有任何地方定义分类码**。它是一根"自由字符串"，由未来业务模块临时传入（`GenerateAsync(targetType, categoryCode)`）。
- 现有业务模块一个都还没建，分类码是无主孤魂——无从新增、无从查询、无从校验。
- 前端规则抽屉里的预览分类段写死为占位符 `CAT`（`CodeFormatter.FormatSample`），不是真实分类。

### 1.2 最终目标

> **业务类型与分类都做成可配置字典，未来任何业务模块出现时纯配置、不改代码。**

- 业务类型字典：有 code（英文标识符）+ 中文名 + 英文名，可查可配可停用。
- 分类字典：挂载在业务类型下，有 code + 中文名 + 英文名，可查可配可停用。
- 编号引擎生成时**强校验** target_type 与 category_code 必须存在于字典且启用。

### 1.3 本轮交付范围

| 模块 | 功能 |
|------|------|
| **业务类型字典** | 实体表 + 完整 CRUD（列表/详情/新增/编辑/启停）+ 种子迁移 6 个默认类型 |
| **分类字典** | 实体表 + 完整 CRUD（列表/详情/新增/编辑/启停）+ 按业务类型分桶 |
| **引擎强校验** | `GenerateAsync` / `PreviewAsync` 取号前校验字典合法性 |
| **前端字典管理页** | 独立页面，主从表联动（类型→分类） |
| **前端规则抽屉改造** | 业务类型/分类下拉改为从字典接口动态拉取 |

### 1.4 不在范围内

- 面料/物料等业务模块的接入（仍等各业务模块独立设计时再调用引擎）
- 分类字典的批量导入/导出
- 字典项的物理删除（统一只启停）
- 业务类型/分类的多级嵌套（明确只两级：类型→分类）

### 1.5 成功标准

- 管理员可在前端纯配置新增一个业务类型（如 `order`）和其下分类（如 `VIP`/`NORMAL`），全程不改代码。
- 引擎对字典外的 target_type / category_code 取号时抛 `DomainException`（映射 400）。
- 存量编号规则（target_type = fabric/material 等 6 个）迁移后无需任何修改即可继续取号。
- 前端规则抽屉的业务类型下拉显示中文名（如"面料"），值为 code（`fabric`）。

---

## 2. 数据模型

新增两张表，都遵循现有 `BaseEntity`（`id` / `created_at` / `updated_at`）模式。与现有编号三表一样，**不建物理外键**，关联是应用层逻辑外键（保持架构一致）。

### 2.1 表：`numbering_target_types`（业务类型字典）

| 列 | 类型 | 约束 | 说明 |
|----|------|------|------|
| `id` | uuid | PK | 主键 |
| `code` | varchar(32) | NOT NULL, **UNIQUE** | 英文标识符，如 `fabric`。创建后不可改 |
| `name_zh` | varchar(64) | NOT NULL | 中文名，如"面料" |
| `name_en` | varchar(64) | NOT NULL | 英文名，如"Fabric" |
| `sort_order` | int | NOT NULL DEFAULT 0 | 排序号，下拉显示顺序 |
| `is_active` | boolean | NOT NULL DEFAULT true | 启停状态 |
| `created_at` | timestamptz | NOT NULL | 审计 |
| `updated_at` | timestamptz | NOT NULL | 审计 |

唯一索引：`ux_numbering_target_types_code` ON `(code)`。

### 2.2 表：`numbering_categories`（分类字典）

| 列 | 类型 | 约束 | 说明 |
|----|------|------|------|
| `id` | uuid | PK | 主键 |
| `target_type_code` | varchar(32) | NOT NULL | 所属业务类型 code（如 `fabric`） |
| `code` | varchar(32) | NOT NULL | 分类码，如 `COT`。创建后不可改 |
| `name_zh` | varchar(64) | NOT NULL | 中文名，如"棉" |
| `name_en` | varchar(64) | NOT NULL | 英文名，如"Cotton" |
| `sort_order` | int | NOT NULL DEFAULT 0 | 排序号 |
| `is_active` | boolean | NOT NULL DEFAULT true | 启停状态 |
| `created_at` | timestamptz | NOT NULL | 审计 |
| `updated_at` | timestamptz | NOT NULL | 审计 |

唯一索引：`ux_numbering_categories_type_code` ON `(target_type_code, code)`。

### 2.3 关键设计决策

**① 分类表存 `target_type_code` 字符串而非 FK id**

业务类型的 `code` 不可改（见 2.1），它本身就是稳定标识。而 `numbering_rules.target_type`、`numbering_logs.target_type`、`numbering_counters.category_code` 存的都是 code 字符串。分类表也存 code，**整条链路用同一个 code 串贯穿**，引擎强校验时直接 `WHERE target_type_code = ?` 即可，无需 JOIN 翻译 id。

**② 不建物理外键**

与现有编号三表（rules/counters/logs 之间也无物理 FK）保持一致。业务类型停用/迁移时不被 FK 锁死。关联正确性由应用层校验保证。

**③ 分类码唯一性范围：同一业务类型内唯一**

`(target_type_code, code)` 组合唯一。即"面料"下不能有两个 `COT`，但"面料"和"原料"下都可以有各自的 `COT`。符合"分类从属于业务类型"的语义。

---

## 3. 引擎改造（强校验）

### 3.1 改造点

`NumberingService.GenerateAsync` 和 `PreviewAsync` 在**找到规则之后、取号之前**插入字典校验。

**`GenerateAsync` 新流程：**

```
GenerateAsync(targetType, categoryCode):
  ① fail-fast 事务守卫（保持不变）
  ② 找规则（保持不变）
  ③ 【新增】校验 targetType：字典里存在且 is_active=true
  ④ 【新增】若 rule.IncludeCategory：校验 categoryCode
       - 非空（已有此校验）
       - 字典里 (targetType, categoryCode) 存在且 is_active=true
  ⑤ 取号、拼码、写日志（保持不变）
```

`PreviewAsync` 同理加 ③④（预览也校验合法性，避免预览出字典外分类的码误导用户）。

### 3.2 实现方式

不引入缓存端口，直接用 DbContext 查（走唯一索引，开销可忽略）：

```csharp
// ③ 校验 targetType
var typeExists = await _db.NumberingTargetTypes
    .AnyAsync(t => t.Code == targetType && t.IsActive, ct);
if (!typeExists)
    throw new DomainException($"业务类型 {targetType} 不存在或已停用");

// ④ 校验 categoryCode（仅当规则要求分类码时）
if (rule.IncludeCategory)
{
    var catExists = await _db.NumberingCategories
        .AnyAsync(c => c.TargetTypeCode == targetType
                    && c.Code == categoryCode && c.IsActive, ct);
    if (!catExists)
        throw new DomainException($"分类码 {categoryCode} 不存在或已停用");
}
```

### 3.3 `NumberTargetTypes.cs` 的处理

该常量类从"引擎默认清单"**降级为"种子迁移用的初始值清单"**：
- 迁移脚本引用它来种入 6 个默认类型。
- 业务代码不再硬编码引用它（改用字典查询）。
- 文件保留，加注释说明其降级用途。

### 3.4 保持不变的行为

- 规则不要求分类码（`IncludeCategory=false`）时，即使传了 categoryCode 也忽略（现有"宽容忽略"语义保持）。
- 取号的事务内行锁、唯一约束兜底重试、不跳号保证——全部不变。

---

## 4. 后端 API 设计

新增独立控制器 `NumberingDictionaryController`（路由 `api/numbering/dict`），与现有 `NumberingController` 分开，职责清晰。复用 `numbering-view` / `numbering-manage` 权限（不新增权限点）。

### 4.1 业务类型字典接口

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/api/numbering/dict/target-types` | `numbering-view` | 分页列表（keyword / isActive 筛选） |
| `GET` | `/api/numbering/dict/target-types/all` | `numbering-view` | 不分页全量（仅启用+排序），前端下拉用 |
| `GET` | `/api/numbering/dict/target-types/{id}` | `numbering-view` | 详情 |
| `POST` | `/api/numbering/dict/target-types` | `numbering-manage` | 新增（code / nameZh / nameEn / sortOrder） |
| `PUT` | `/api/numbering/dict/target-types/{id}` | `numbering-manage` | 编辑（code 不可改，name / sortOrder / isActive 可改） |
| `PUT` | `/api/numbering/dict/target-types/{id}/status` | `numbering-manage` | 启停切换 |

### 4.2 分类字典接口

| 方法 | 路由 | 权限 | 说明 |
|------|------|------|------|
| `GET` | `/api/numbering/dict/categories` | `numbering-view` | 分页列表（targetTypeCode / keyword / isActive 筛选） |
| `GET` | `/api/numbering/dict/categories/all` | `numbering-view` | 按业务类型返回启用分类，参数 `targetTypeCode=fabric`，前端联动下拉用 |
| `GET` | `/api/numbering/dict/categories/{id}` | `numbering-view` | 详情 |
| `POST` | `/api/numbering/dict/categories` | `numbering-manage` | 新增（targetTypeCode / code / nameZh / nameEn / sortOrder） |
| `PUT` | `/api/numbering/dict/categories/{id}` | `numbering-manage` | 编辑（code / targetTypeCode 不可改，name / sortOrder / isActive 可改） |
| `PUT` | `/api/numbering/dict/categories/{id}/status` | `numbering-manage` | 启停切换 |

### 4.3 设计决策

**① 提供 `/all` 不分页接口**
前端下拉框需一次拿全（启用的）。走分页接口下拉要翻页，体验差。字典类接口的常规做法，与现有角色/权限下拉一致。

**② 不提供删除接口**
与 `NumberingRule` 一致——只启停不物理删除。停用后：引擎校验拒绝、下拉不显示，但存量日志/计数器的 code 仍可追溯（存的是 code 快照字符串，不依赖字典存在）。

**③ 停用业务类型不级联停用其下分类**
分类独立启停。但前端下拉逻辑：业务类型停用后，其下分类即使启用也不会出现在分类下拉（分类联动依赖一个启用的业务类型）。引擎校验也会因 targetType 不存在/停用而拒绝。

**④ 新增分类时校验 targetTypeCode 存在**
应用层校验：新增分类的 `targetTypeCode` 必须指向一个存在且启用的业务类型，防止孤儿分类。

**⑤ 启停与编辑的关系**
业务类型的 `is_active` 通过独立的 `/status` 接口切换，与编辑接口解耦。停用的业务类型可通过编辑接口改名称/排序，也可通过 `/status` 重新启用——启停状态不锁定其他字段的编辑（区别于 `NumberingRule` 启用规则的字段锁定机制，字典项无此锁定）。

---

## 5. 前端设计

### 5.1 新增页面：业务字典管理

路由 `system/numbering/dict`，菜单挂在"编号管理"下。**单页主从表联动**（非双 Tab，因为类型与分类是主从关系）：

```
┌─────────────────────────────────────────────┐
│ 编号管理 > 业务字典                            │
├─────────────────────────────────────────────┤
│ ── 业务类型 ──                  [+ 新增类型]  │
│ ┌─────────────────────────────────────────┐  │
│ │ code    中文名  英文名  排序  状态  操作  │  │
│ │ fabric  面料   Fabric   1    启用  编辑  │  │
│ │ material 原料  Material  2   启用  编辑  │  │
│ │ customer 客户  Customer  3   停用  启用  │  │
│ └─────────────────────────────────────────┘  │
│                                              │
│ ── 分类（点击上方某行类型 → 联动） ──          │
│   当前：面料 (fabric)          [+ 新增分类]   │
│ ┌─────────────────────────────────────────┐  │
│ │ code  中文名 英文名  排序 状态 操作       │  │
│ │ COT   棉    Cotton   1   启用 编辑       │  │
│ │ POL   涤纶  Polyester 2  启用 编辑       │  │
│ └─────────────────────────────────────────┘  │
└─────────────────────────────────────────────┘
```

交互：点击上方业务类型某行 → 选中态高亮 → 下方分类表格联动显示该类型下的分类。经典主从表模式，直观体现"分类从属于类型"。

### 5.2 改造现有"编号规则"抽屉

痛点 1 的核心——业务类型下拉从写死改为动态拉取：

- 删除前端写死的 `TARGET_TYPE_OPTIONS` 数组。
- `useEffect` 拉取 `/api/numbering/dict/target-types/all`。
- 业务类型下拉：显示中文名（`name_zh`），值为 `code`。
- 实时预览：规则开启 `includeCategory` 时，分类段占位符 `CAT` 改为显示该业务类型下第一个启用分类的 code（无分类时显示提示文案）。

### 5.3 日志 Tab 的显示优化

- 业务类型列、分类列：现在直接显示 code（如 `fabric` / `COT`）。改为优先显示中文名（如"面料"/"棉"），code 作为次要信息或 tooltip。
- 保留 code 兜底：字典里已停用的项，日志仍能显示（用 code 本身）。

### 5.4 国际化

新增 `locale/zh-CN.ts` / `en-US.ts`，覆盖：页面标题、表头、表单标签、操作按钮、校验提示。业务类型/分类的名称本身存在字典（`name_zh` / `name_en`），前端按当前语言取对应字段显示。

---

## 6. 迁移与种子数据

### 6.1 一次迁移 `AddNumberingDictionary`

**① 建两张表**（带唯一索引）：
- `numbering_target_types`：`code` 唯一索引
- `numbering_categories`：`(target_type_code, code)` 组合唯一索引

**② 种子迁移 6 个默认业务类型**（引用 `NumberTargetTypes` 常量值，保证存量数据无缝兼容）：

| code | name_zh | name_en | sort_order |
|------|---------|---------|------------|
| `fabric` | 面料 | Fabric | 1 |
| `material` | 原料 | Material | 2 |
| `equipment` | 设备 | Equipment | 3 |
| `customer` | 客户 | Customer | 4 |
| `color` | 颜色 | Color | 5 |
| `product` | 产品 | Product | 6 |

全部 `is_active = true`。

**③ 不种子任何分类**——分类由管理员按需添加（每个工厂分类体系不同，种子无意义）。

### 6.2 存量数据兼容性

迁移后，现有 `numbering_rules` / `numbering_logs` / `numbering_counters` 的 `target_type` 值全部命中字典（种子 code 与原 `NumberTargetTypes` 常量完全一致）。引擎强校验上线后，存量规则不会因"字典里找不到"而失败。

### 6.3 边界保护：自动补种非标准 target_type

迁移脚本检测 `numbering_rules` 中存在、但 6 个种子之外的自定义 target_type（如用户曾用前端 `allowCreate` 加过的 `order`），**自动补种**（name 用 code 本身，`is_active=true`），避免强校验上线后取号失败。

---

## 7. 测试策略

遵循现有测试模式（单元测试 + Testcontainers 集成测试），分三层覆盖。

### 7.1 单元测试（`OneCup.UnitTests`）

新增 `NumberingDictionary/` 目录：

**① 字典服务测试**
- 业务类型 CRUD：新增 / 编辑 / 启停 / 列表筛选
- code 不可改校验（编辑传 code 时拒绝或忽略）
- code 重复校验（唯一约束 → 友好错误）
- 分类 CRUD：同上
- 分类 `(targetType, code)` 组合唯一校验
- 分类 `targetTypeCode` 必须指向存在且启用的业务类型（防孤儿分类）

**② 字典规格测试**
- 多条件筛选（keyword + isActive）
- 分页/计数分离 spec（沿用 `ApplyCriteria` 覆盖语义 bug 模式）

### 7.2 集成测试（扩展 `NumberingServiceConcurrencyTests.cs`）

本次改造最高风险点——强校验引入后引擎行为变化，必须用真实 PG（Testcontainers）验证：

- ✅ 合法 targetType + 合法 category → 正常取号
- ❌ 字典里不存在的 targetType → 抛 `DomainException`
- ❌ 停用的 targetType → 抛异常
- ❌ 规则要求分类码但传了字典里不存在的 categoryCode → 抛异常
- ❌ 停用的 categoryCode → 抛异常
- ✅ 规则不要求分类码时，即使传了 categoryCode 也不校验（保持宽容忽略）
- ✅ Preview 同样走校验

### 7.3 不测试

前端（项目现有测试体系无前端单测覆盖此模块，保持一致，不新增）。

---

## 8. 涉及文件清单

### 后端新增
- `OneCup.Domain/Entities/NumberingTargetType.cs`
- `OneCup.Domain/Entities/NumberingCategory.cs`
- `OneCup.Infrastructure/Persistence/Configurations/NumberingTargetTypeConfiguration.cs`
- `OneCup.Infrastructure/Persistence/Configurations/NumberingCategoryConfiguration.cs`
- `OneCup.Infrastructure/Migrations/<timestamp>_AddNumberingDictionary.cs`
- `OneCup.Application/Dtos/System/NumberingDictionaryDtos.cs`
- `OneCup.Application/Interfaces/INumberingDictionaryService.cs`
- `OneCup.Application/Services/NumberingDictionaryService.cs`
- `OneCup.Application/Specifications/NumberingDictionarySpecs.cs`
- `OneCup.Api/Controllers/NumberingDictionaryController.cs`

### 后端修改
- `OneCup.Infrastructure/Persistence/OneCupDbContext.cs`（新增两个 DbSet）
- `OneCup.Infrastructure/Services/NumberingService.cs`（GenerateAsync/PreviewAsync 加校验）
- `OneCup.Application/Common/NumberTargetTypes.cs`（注释降级说明）
- `OneCup.Api/Program.cs`（注册新服务 DI）

### 前端新增
- `frontend/src/pages/system/numbering/dict/index.tsx`
- `frontend/src/pages/system/numbering/dict/locale/{zh-CN,en-US,index}.ts`
- `frontend/src/api/numberingDictionary.ts`

### 前端修改
- `frontend/src/pages/system/numbering/index.tsx`（下拉改动态、预览占位符、日志显示中文名）
- `frontend/src/pages/system/numbering/locale/{zh-CN,en-US}.ts`
- `frontend/src/routes.ts` + `frontend/src/router.tsx`（新增字典页路由）

### 测试新增
- `backend/tests/OneCup.UnitTests/NumberingDictionary/NumberingDictionaryServiceTests.cs`
- `backend/tests/OneCup.UnitTests/NumberingDictionary/NumberingDictionarySpecsTests.cs`
- 扩展 `backend/tests/OneCup.UnitTests/Numbering/NumberingServiceConcurrencyTests.cs`
