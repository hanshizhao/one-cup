# 运行模板独立 Tab 设计

> 日期：2026-07-07
> 状态：已批准（待实现）
> 范围：设备管理模块导航架构调整——运行模板从「设备类型详情抽屉里的段落」提升为独立 Tab

---

## 1. 背景与目标

### 1.1 问题

当前运行模板（EquipmentTemplate）的入口藏得过深，达 4 层：

```
侧边栏「设备管理」→ Tab「设备类型」→ 表格某行点「详情」(Drawer)
  → 类型详情抽屉里滚到下方 → 「运行模板」段落（内嵌 TemplateList）
    → 点某模板「编辑」→ 独立页 TemplateFormPage
```

模板本身是**独立资源**（有独立路由、内容多需独立页编辑），却埋在设备类型详情抽屉的一个段落里。用户要管理模板，必须先进设备类型详情。

### 1.2 目标

把「运行模板」提升为设备管理页的**第三个 Tab**，与「设备」「设备类型」平级。进模板 Tab 即可直接查询、新建、编辑模板，不再需要先钻进设备类型详情。

### 1.3 设计决策摘要

| 决策点 | 结论 | 理由 |
|---|---|---|
| Tab 形态 | 独立 Tab + 顶部类型筛选器（Select） | 解决「藏得深」；保留模板从属类型的约束 |
| 类型筛选 | 支持「全部类型」 | 一眼看所有模板，体验更好 |
| 全部查询 | 后端新增顶层端点 `GET /api/equipment-templates` | 现有嵌套端点必须指定 typeId，无法跨类型 |
| 类型选择位置 | 表单页内选（新建可选/编辑锁定） | 不用先在下拉选类型才能新建；符合「新建一个模板」心智流 |
| 详情抽屉模板段落 | 降级为只读列表 + 跳转链接 | 管理收敛到 Tab，避免两个入口；保留类型详情完整性 |
| 分页 | 支持 | 跨所有类型聚合后可能上百，与项目列表页标准一致 |
| 权限 | 复用 `equipment-type:read/create/update/delete` | 模板是类型的工艺配置，权限码不拆分 |

---

## 2. 后端设计

### 2.1 新增端点：跨类型分页查询模板

`GET /api/equipment-templates`

| 参数 | 类型 | 说明 |
|---|---|---|
| typeId | Guid? (query) | 可选。不传=跨所有类型；传=单类型 |
| keyword | string? (query) | 模板名称模糊匹配 |
| processId | Guid? (query) | 工序筛选 |
| page | int (query) | 页码，默认 1 |
| pageSize | int (query) | 每页条数，默认 10 |

**返回**：`PagedResult<EquipmentTemplateListItemDto>`

**DTO 字段确认**：`EquipmentTemplateListItemDto` 必须包含 `EquipmentTypeName`（跨类型查询时列表要显示所属类型名）。若现有 DTO 缺此字段，需补。

### 2.2 实现层级

| 层 | 文件 | 改动 |
|---|---|---|
| Controller | `Api/Controllers/EquipmentTemplatesController.cs`（现有）或新建顶层 Controller | 新增 `GET /api/equipment-templates` 端点 |
| Service 接口 | `Application/Interfaces/IEquipmentTemplateService.cs` | 新增 `GetPagedAsync(TemplatePagedQuery query, CancellationToken ct)` |
| Service 实现 | `Application/Services/EquipmentTemplateService.cs` | 实现跨类型分页查询，JOIN equipment_types 取类型名 |
| Specification | `Application/Specifications/EquipmentTemplateSpecs.cs`（新增文件） | 按 typeId / keyword / processId 过滤 |
| DTO | `Application/Dtos/System/EquipmentDtos.cs` | 确认/补 `EquipmentTemplateListItemDto.EquipmentTypeName`；新增 `TemplatePagedQuery` |

### 2.3 保留

现有嵌套端点 `GET /api/equipment-types/{typeId}/templates` **不删**——设备类型详情抽屉的只读概览仍用它按类型查（返回少量字段即可）。新建/编辑/删除端点不变（仍走嵌套路由，typeId 从请求取）。

### 2.4 权限

新端点用 `equipment-type:read`（与现有嵌套端点一致）。

---

## 3. 前端设计

### 3.1 导航：3 Tab

`equipment/index.tsx` 容器页新增第 3 个 Tab：

```
设备管理
├─ Tab「设备」     (equipment)
├─ Tab「设备类型」  (type)
└─ Tab「运行模板」  (template) ← 新增
```

Tab 状态持久化 `?tab=` 扩展支持 `template` 取值。

### 3.2 模板 Tab 页面（新建 `template/TemplateTab.tsx`）

遵循项目 Query Table 标准（单 Card + Form/Grid 三列 + 按钮表单外侧）：

**查询区**：
- 设备类型（Select，支持「全部」，选项来自 `getActiveEquipmentTypes()`）
- 模板名称（Input，keyword）
- 工序（Select，processId）

**工具栏**：「+ 新建模板」按钮（跳 `/business/equipment/template/create`）

**表格列**：
| 列 | 字段 | 说明 |
|---|---|---|
| 模板名称 | name | |
| 所属类型 | equipmentTypeName | 跨类型查询时关键 |
| 工序 | processName | |
| 状态 | status | valid/invalid/orphan（带 Tag） |
| 排序 | sortOrder | |
| 创建时间 | createdAt | |
| 操作 | — | 详情 / 编辑 / 删除 |

**查询触发**：仅按钮触发（`getFieldsValue`），符合列表页标准。

**类型筛选持久化**：`?typeId=xxx`（便于从抽屉跳转链接定位类型）。

### 3.3 模板表单页改造（`type/template/TemplateFormPage.tsx`）

**路由调整**（去掉 typeId）：

| 场景 | 现路由 | 新路由 |
|---|---|---|
| 新建 | `/business/equipment/type/:typeId/template/create` | `/business/equipment/template/create` |
| 编辑 | `/business/equipment/type/:typeId/template/edit/:id` | `/business/equipment/template/edit/:id` |

**表单页逻辑**：

- **新建模式**：
  - 顶部「设备类型」Select（必填），选项来自 `getActiveEquipmentTypes()`
  - **未选类型时**：参数值表单区为空，提示「请先选择设备类型」
  - **选完类型后**：动态拉取该类型参数定义（`getEquipmentTypeById(typeId)`），渲染参数值表单（复用 `TemplateValueEditor`）
  - **切换类型时**：清空已填参数值（类型变了，旧值无意义），重新渲染
- **编辑模式**：
  - 从模板数据读 typeId（`getTemplateById(id)` 返回含 typeId）
  - 「设备类型」Select **锁定不可改**（改类型=换参数定义，已填值全废）
  - 回填参数值表单
- **提交**：
  - 新建：`POST /api/equipment-types/{typeId}/templates`（typeId 从表单选的值取）
  - 编辑：`PUT /api/equipment-types/{typeId}/templates/{id}`（typeId 从模板自带）

**面包屑**：简化为「运行模板 / 新建」或「运行模板 / 编辑 · {name}」（去掉中间的类型层级，因类型在表单内）。

### 3.4 设备类型详情抽屉降级（`type/TypeDetail.tsx`）

模板段落从「内嵌可编辑 TemplateList」改为「只读列表 + 跳转链接」：

- 仍显示「运行模板（N）」标题
- 内容：模板名列表（只读，带工序 + 状态标签），无操作按钮、无新建按钮
- 底部链接：「在「运行模板」Tab 中管理 →」，点击跳转 `?tab=template&typeId={当前类型id}`
- **移除**：抽屉里的「新建模板」按钮、TemplateList 的操作列

### 3.5 TemplateList 组件去留

`type/template/TemplateList.tsx` 原是抽屉内嵌的可编辑列表。改造方向：

- 模板 Tab 主列表页**新建 `TemplateTab.tsx`**（标准 Query Table），不复用 TemplateList（容器和职责不同）
- `TemplateList.tsx` 若不再被引用，删除；若抽屉只读概览复用其部分逻辑，保留精简版
- 实施时确认引用关系后决定

---

## 4. 前端 API client 改动

`api/equipment.ts`：

- 新增 `getEquipmentTemplatesPaged(params)`：调 `GET /api/equipment-templates`，返回 `PagedResult<EquipmentTemplateListItemDto>`
- 确认 `EquipmentTemplateListItemDto` 含 `equipmentTypeName`
- 现有 `getEquipmentTypeTemplates(typeId, processId?)`（嵌套端点）保留

---

## 5. 路由改动

`router.tsx`：

- **删除**：`business/equipment/type/:typeId/template/create`、`business/equipment/type/:typeId/template/edit/:id`
- **新增**：`business/equipment/template/create`、`business/equipment/template/edit/:id`
- 模板 Tab 是容器页内 Tab（`?tab=template`），不增路由

---

## 6. locale 改动

新增 key（zh-CN / en-US）：
- `equipment.tab.template`：运行模板 / Templates
- 模板 Tab 查询字段：类型/名称/工序的 label
- 模板表格列标题（部分可复用现有 `equipment.template.column.*`）
- 「请先选择设备类型」提示
- 「在运行模板 Tab 中管理」链接文案

---

## 7. 遵循的约定

| 约定/标准 | 应用点 |
|---|---|
| 列表页标准（Query Table） | 模板 Tab 主列表：单 Card + Form/Grid 三列 + 按钮外侧 + 仅按钮触发查询 |
| 导航架构（AGENTS.md） | 模板作为同模块子视图用页面内 Tab，不拆侧边栏 SubMenu |
| c01 删除确认 | 模板删除：单条物理删除但影响范围小 → Popconfirm（与现状一致） |
| Tab 状态持久化 | `?tab=` 扩展，复用现有机制 |

---

## 8. 验收要点

1. 设备管理页有 3 个 Tab：设备 / 设备类型 / 运行模板
2. 模板 Tab：顶部类型筛选（Select，支持「全部」）+ 查询按钮 + 标准 Query Table
3. 选「全部」→ 列表展示所有类型下的模板（带所属类型列）
4. 选具体类型 → 列表筛到该类型模板
5. 点「新建模板」→ 进表单页 → 选设备类型 → 参数值表单动态出现 → 填值保存成功
6. 新建时切换类型 → 已填值清空，参数值表单按新类型重渲染
7. 点「编辑」→ 表单页类型锁定不可改 → 回填值 → 保存成功
8. 设备类型详情抽屉：模板段落是只读列表（无操作按钮），点「在运行模板 Tab 管理」跳转且自动定位类型
9. Tab 切换/刷新保持状态（`?tab=`、`?typeId=`）
10. 后端新端点 `GET /api/equipment-templates` 支持 typeId/keyword/processId/page/pageSize，返回分页 + 类型名
11. `npm run build`（前端）+ `dotnet build`（后端）通过
