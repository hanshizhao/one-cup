# 运行模板独立 Tab 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把运行模板从「设备类型详情抽屉里的段落」提升为设备管理页的第三个 Tab，支持跨类型查询、表单页内选类型。

**Architecture:** 后端先新增跨类型分页查询端点（`GET /api/equipment-templates`）+ 补 DTO 字段；前端新增 API client、模板 Tab 列表页、容器页第 3 Tab；改造模板表单页（路由去 typeId、类型选择器、动态加载参数定义）；详情抽屉模板段落降级为只读概览。

**Tech Stack:** 后端 .NET 8 + EF Core + Specification 模式；前端 React + @arco-design/web-react + react-router-dom v6 + TypeScript。

## Global Constraints

- 后端新端点 `GET /api/equipment-templates`，支持 typeId/keyword/processId/page/pageSize，返回 `PagedResult<EquipmentTemplateListItemDto>`
- DTO 补字段：`EquipmentTemplateListItemDto` 加 `EquipmentTypeId` + `EquipmentTypeName`；`EquipmentTemplateDto` 加 `EquipmentTypeId`
- 跨类型查询的 status（valid/invalid/orphan）需按各模板所属类型的参数定义实时校验（复用现有 WorstStatus 逻辑，但要按 typeId 分组算）
- 前端模板 Tab 遵循 Query Table 标准（单 Card + Form/Grid 三列 + 按钮外侧 + 仅按钮触发查询）
- 模板表单路由：`/business/equipment/template/create`、`/business/equipment/template/edit/:id`（去掉 typeId）
- 表单页新建模式：类型可选（选完动态拉参数定义），切换类型清空已填值；编辑模式：类型锁定不可改
- 详情抽屉模板段落：只读列表（名称/工序/状态）+ 跳转链接（`?tab=template&typeId=xxx`），移除操作按钮和新建按钮
- Tab 状态 `?tab=` 扩展支持 `template`；模板 Tab 类型筛选用 `?typeId=`
- 权限复用 `equipment-type:read/create/update/delete`（模板是类型工艺配置，不拆权限码）
- 项目前端无单测；后端有单测（Fake 替身）。验证：后端 `dotnet build` + 相关单测；前端 `npm run build`
- 删除策略不变：模板删除走 Popconfirm（单条物理删除，c01）

参考文档：`docs/superpowers/specs/2026-07-07-equipment-template-tab-design.md`

---

## Task 1: 后端 DTO 补字段 + 跨类型分页查询

后端基础。补 DTO 字段（前后端契约），新增跨类型分页查询端点。前端所有改动依赖此任务的 DTO 字段。

**Files:**
- Modify: `backend/src/OneCup.Application/Dtos/System/EquipmentDtos.cs`（补 DTO 字段 + 新增 TemplatePagedQuery）
- Create: `backend/src/OneCup.Application/Specifications/EquipmentTemplateSpecs.cs`（新增 Spec 文件）
- Modify: `backend/src/OneCup.Application/Interfaces/IEquipmentTemplateService.cs`（新增 GetPagedAsync）
- Modify: `backend/src/OneCup.Application/Services/EquipmentTemplateService.cs`（实现 GetPagedAsync）
- Modify: `backend/src/OneCup.Api/Controllers/EquipmentTemplatesController.cs`（新增顶层端点）

**Interfaces:**
- Consumes: 现有 `IRepository<EquipmentTemplate>`、`IRepository<EquipmentType>`、`IRepository<Process>`、`PagedResult<T>`、`Specification<T>` 基类、现有 `WorstStatus` 私有方法
- Produces: `GET /api/equipment-templates` 端点；`EquipmentTemplateListItemDto.EquipmentTypeId/EquipmentTypeName` 字段；`EquipmentTemplateDto.EquipmentTypeId` 字段

- [ ] **Step 1: 补 DTO 字段**

在 `backend/src/OneCup.Application/Dtos/System/EquipmentDtos.cs` 中：

**1a.** `EquipmentTemplateListItemDto`（约 L104-114）加两个字段。在 `public Guid Id { get; set; }` 之后加：
```csharp
    public Guid EquipmentTypeId { get; set; }
    public string EquipmentTypeName { get; set; } = string.Empty;
```

**1b.** `EquipmentTemplateDto`（约 L117-122）加 typeId（继承自 ListItemDto，但详情查询现有实现可能没填，确保填）。由于 `EquipmentTemplateDto : EquipmentTemplateListItemDto`，`EquipmentTypeId` 已继承，无需重复声明。只需确保 GetByIdAsync 实现里填了它（Step 4 检查）。

**1c.** 文件末尾新增分页查询参数类：
```csharp
/// <summary>模板跨类型分页查询参数。</summary>
public class TemplatePagedQuery
{
    public Guid? TypeId { get; set; }
    public string? Keyword { get; set; }
    public Guid? ProcessId { get; set; }
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 10;
}
```

- [ ] **Step 2: 新增 EquipmentTemplateSpecs.cs**

创建 `backend/src/OneCup.Application/Specifications/EquipmentTemplateSpecs.cs`，参照 `EquipmentSpecs.cs` 的 FilterSpec + PagedSpec 双 Spec 模式：

```csharp
using OneCup.Domain.Entities;

namespace OneCup.Application.Specifications;

/// <summary>模板过滤规格（仅过滤，不含分页）。用于 CountAsync。</summary>
public class EquipmentTemplateFilterSpec : Specification<EquipmentTemplate>
{
    public EquipmentTemplateFilterSpec(Guid? typeId, string? keyword, Guid? processId)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(t =>
            (typeId == null || t.EquipmentTypeId == typeId.Value) &&
            (kw == null || t.Name.Contains(kw)) &&
            (processId == null || t.ProcessId == processId.Value));
    }
}

/// <summary>模板跨类型分页查询（含过滤，按 SortOrder 升序）。</summary>
public class EquipmentTemplatePagedSpec : Specification<EquipmentTemplate>
{
    public EquipmentTemplatePagedSpec(Guid? typeId, string? keyword, Guid? processId, int page, int pageSize)
    {
        var kw = string.IsNullOrWhiteSpace(keyword) ? null : keyword.Trim();
        ApplyCriteria(t =>
            (typeId == null || t.EquipmentTypeId == typeId.Value) &&
            (kw == null || t.Name.Contains(kw)) &&
            (processId == null || t.ProcessId == processId.Value));
        ApplyOrderBy(t => t.SortOrder);
        ApplyPaging(page, pageSize);
    }
}
```

> 注意：确认 `Specification<T>` 基类有 `ApplyCriteria/ApplyOrderBy/ApplyPaging` 方法（参照 EquipmentSpecs.cs 用法）。若基类在别的命名空间，import 它。

- [ ] **Step 3: Service 接口加方法**

在 `backend/src/OneCup.Application/Interfaces/IEquipmentTemplateService.cs` 的接口体里加：
```csharp
    Task<PagedResult<EquipmentTemplateListItemDto>> GetPagedAsync(
        Guid? typeId, string? keyword, Guid? processId, int page, int pageSize, CancellationToken ct = default);
```

- [ ] **Step 4: Service 实现 GetPagedAsync**

在 `backend/src/OneCup.Application/Services/EquipmentTemplateService.cs` 实现类里加方法。关键：跨类型查询时，每个模板的 status 要按其所属类型的参数定义算（按 typeId 分组）。

```csharp
    public async Task<PagedResult<EquipmentTemplateListItemDto>> GetPagedAsync(
        Guid? typeId, string? keyword, Guid? processId, int page, int pageSize, CancellationToken ct = default)
    {
        var total = await _templates.CountAsync(
            new EquipmentTemplateFilterSpec(typeId, keyword, processId), ct);
        var templates = await _templates.ListAsync(
            new EquipmentTemplatePagedSpec(typeId, keyword, processId, page, pageSize), ct);

        if (templates.Count == 0)
            return new PagedResult<EquipmentTemplateListItemDto>(new List<EquipmentTemplateListItemDto>(), total, page, pageSize);

        // 涉及的类型（用于类型名 + 参数定义算 status）
        var typeIds = templates.Select(t => t.EquipmentTypeId).Distinct().ToList();
        var types = await _types.ListAsync(new EquipmentTypesByIdsSpec(typeIds), ct);
        var typesById = types.ToDictionary(t => t.Id);
        var processNames = await GetProcessNames(templates.Select(t => t.ProcessId).Distinct(), ct);

        var items = templates.Select(t =>
        {
            var type = typesById.GetValueOrDefault(t.EquipmentTypeId);
            var paramsById = type?.Parameters.ToDictionary(p => p.Id) ?? new();
            var worst = WorstStatus(t.Values, paramsById);
            return new EquipmentTemplateListItemDto
            {
                Id = t.Id,
                EquipmentTypeId = t.EquipmentTypeId,
                EquipmentTypeName = type?.Name ?? string.Empty,
                Name = t.Name,
                ProcessId = t.ProcessId,
                ProcessName = processNames.GetValueOrDefault(t.ProcessId) ?? string.Empty,
                Status = worst.Status,
                StatusMessage = worst.Message,
                SortOrder = t.SortOrder,
                CreatedAt = t.CreatedAt,
            };
        }).ToList();

        return new PagedResult<EquipmentTemplateListItemDto>(items, total, page, pageSize);
    }
```

**需新增辅助 Spec** `EquipmentTypesByIdsSpec`（按多个 id 批量查类型）。在 `EquipmentTypeSpecs.cs`（若存在）或新建文件里加：
```csharp
public class EquipmentTypesByIdsSpec : Specification<EquipmentType>
{
    public EquipmentTypesByIdsSpec(IEnumerable<Guid> ids)
        => ApplyCriteria(t => ids.Contains(t.Id));
}
```

> 注意：确认 `PagedResult<T>` 构造函数签名（items, total, page, pageSize）——参照 EquipmentService.GetListAsync 的用法。确认 `GetValueOrDefault` 在项目里可用（.NET 8 Dictionary 扩展）。

**同时**：检查现有 `GetByIdAsync`（详情）实现，确保填了 `EquipmentTypeId`（因为 EquipmentTemplateDto 继承了该字段，前端编辑表单页要读它）。若现有实现返回的 EquipmentTemplateDto 没填 EquipmentTypeId，补上。

- [ ] **Step 5: Controller 新增顶层端点**

在 `backend/src/OneCup.Api/Controllers/EquipmentTemplatesController.cs` 加一个**非嵌套路由**的端点。由于类级 Route 是 `api/equipment-types/{typeId:guid}/templates`，顶层端点要单独用 `[Route]` 或 `[HttpGet("~/api/equipment-templates")]`（`~` 覆盖类级前缀）：

```csharp
    [HttpGet("~/api/equipment-templates")]
    public async Task<IActionResult> GetPaged(
        [FromQuery] Guid? typeId,
        [FromQuery] string? keyword,
        [FromQuery] Guid? processId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        CancellationToken ct = default)
    {
        var result = await _service.GetPagedAsync(typeId, keyword, processId, page, pageSize, ct);
        return Ok(result);
    }
```

- [ ] **Step 5b: 新增顶层详情端点（Task 7 编辑模式的依赖）**

路由去掉 typeId 后，编辑模式进表单页时只知道模板 id、不知道 typeId。需配套一个**顶层详情端点** `GET /api/equipment-templates/{id}`（不带 typeId），返回的 DTO 含 `equipmentTypeId`（前端据此回填 + 锁定类型）。

**Service 接口** `IEquipmentTemplateService.cs` 加：
```csharp
    Task<EquipmentTemplateDto?> GetByIdAsync(Guid id, CancellationToken ct = default);
```

**Service 实现** `EquipmentTemplateService.cs` 加（直接查模板表，再按 EquipmentTypeId 取类型参数定义算 status）：
```csharp
    public async Task<EquipmentTemplateDto?> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var template = await _templates.FirstOrDefaultAsync(new EquipmentTemplateByIdSpec(id), ct);
        if (template is null) return null;

        var type = await _types.FirstOrDefaultAsync(new EquipmentTypeByIdSpec(template.EquipmentTypeId), ct);
        var paramsById = type?.Parameters.ToDictionary(p => p.Id) ?? new();
        var processNames = await GetProcessNames(new[] { template.ProcessId }, ct);

        // 复用现有值校验逻辑组装 Values（参照现有 GetByIdAsync(typeId, id) 的 Values 映射）
        var values = template.Values.Select(v =>
        {
            var (st, msg) = paramsById.TryGetValue(v.ParameterId, out var p)
                ? ValidateValue(v, p)  // 复用现有单值校验私有方法
                : ("orphan", "参数定义已删除");
            return new EquipmentTemplateValueDto { /* 参照现有映射 */ };
        }).ToList();

        return new EquipmentTemplateDto
        {
            Id = template.Id,
            EquipmentTypeId = template.EquipmentTypeId,
            EquipmentTypeName = type?.Name ?? string.Empty,
            Name = template.Name,
            ProcessId = template.ProcessId,
            ProcessName = processNames.GetValueOrDefault(template.ProcessId) ?? string.Empty,
            Remark = template.Remark,
            SortOrder = template.SortOrder,
            CreatedAt = template.CreatedAt,
            Values = values,
        };
    }
```

> **implementer 注意**：上面 Values 映射里的 `ValidateValue` 和字段映射要**参照现有 `GetByIdAsync(Guid typeId, Guid id, ...)` 的实现**（它已有完整的值校验 + DTO 映射逻辑），复用其私有方法。新增的 `EquipmentTemplateByIdSpec`（按 id 查模板）加到 Task 1 Step 2 的 Spec 文件里：
> ```csharp
> public class EquipmentTemplateByIdSpec : Specification<EquipmentTemplate>
> {
>     public EquipmentTemplateByIdSpec(Guid id) => ApplyCriteria(t => t.Id == id);
> }
> ```

**Controller** 加顶层详情端点：
```csharp
    [HttpGet("~/api/equipment-templates/{id:guid}")]
    public async Task<IActionResult> GetByIdTopLevel(Guid id, CancellationToken ct)
    {
        var template = await _service.GetByIdAsync(id, ct);
        return template is null ? NotFound() : Ok(template);
    }
```

**前端 API**（Task 2 一并加）：
```ts
export function getEquipmentTemplateByIdTopLevel(id: string) {
  return request.get<unknown, EquipmentTemplateDto>(`/api/equipment-templates/${id}`);
}
```

- [ ] **Step 6: 后端编译 + 单测验证**

Run: `cd backend && dotnet build`
Expected: build 成功，无错误。

Run: `cd backend && dotnet test --filter "FullyQualifiedName~EquipmentTemplate"`
Expected: 现有模板单测全通过（不应回归）。

- [ ] **Step 7: 提交**

```bash
git add backend/
git commit -m "feat(equipment): 后端跨类型模板分页查询端点 + DTO 补字段"
```

---

## Task 2: 前端 API client + 类型定义

前端基础。新增调新端点的 API 函数，补类型字段。前端所有页面改动依赖此任务。

**Files:**
- Modify: `frontend/src/api/equipment.ts`

**Interfaces:**
- Consumes: Task 1 的后端端点 + DTO 字段
- Produces: `getEquipmentTemplatesPaged(params)` 函数；`EquipmentTemplateListItemDto` 补 `equipmentTypeId/equipmentTypeName`；`EquipmentTemplateDto` 补 `equipmentTypeId`

- [ ] **Step 1: 补类型字段 + 新增 API 函数**

在 `frontend/src/api/equipment.ts` 中：

**1a.** `EquipmentTemplateListItemDto`（约 L85-94）加两个字段。在 `id` 之后加：
```ts
  equipmentTypeId: string;
  equipmentTypeName: string;
```

**1b.** `EquipmentTemplateDto`（约 L96-100）加 typeId（继承自 ListItemDto，TS interface 用 extends 已继承，无需重复声明）。确认即可。

**1c.** 文件末尾（模板 API 区）新增分页查询函数：
```ts
export interface EquipmentTemplatePagedQuery {
  typeId?: string;
  keyword?: string;
  processId?: string;
  page?: number;
  pageSize?: number;
}

export function getEquipmentTemplatesPaged(params: EquipmentTemplatePagedQuery) {
  return request.get<unknown, PagedResult<EquipmentTemplateListItemDto>>(
    '/api/equipment-templates',
    { params }
  );
}
```

> 确认 `PagedResult<T>` 已在本文件定义（设备列表在用）。若未定义，参照现有 `PagedResult` 定义补。

- [ ] **Step 2: 编译验证**

Run: `cd frontend && npm run build`
Expected: `✓ built in <N>s`，无错误。

- [ ] **Step 3: 提交**

```bash
git add frontend/src/api/equipment.ts
git commit -m "feat(equipment): 前端模板跨类型查询 API + DTO 补字段"
```

---

## Task 3: 前端路由调整（模板表单去 typeId）

调整模板表单页路由，去掉 typeId 路径段。为 Task 6（表单页改造）铺路。

**Files:**
- Modify: `frontend/src/router.tsx`

**Interfaces:**
- Produces: 新路由 `/business/equipment/template/create`、`/business/equipment/template/edit/:id`

- [ ] **Step 1: 改路由定义**

在 `frontend/src/router.tsx` 中，找到现有模板路由（约 L152-166）：
```tsx
      {
        path: 'business/equipment/type/:typeId/template/create',
        ...
      },
      {
        path: 'business/equipment/type/:typeId/template/edit/:id',
        ...
      },
```
改为：
```tsx
      {
        path: 'business/equipment/template/create',
        element: withSuspense(
          <RequirePermission resource="equipment-type" actions={['create']}>
            <EquipmentTemplateFormPage />
          </RequirePermission>
        ),
      },
      {
        path: 'business/equipment/template/edit/:id',
        element: withSuspense(
          <RequirePermission resource="equipment-type" actions={['update']}>
            <EquipmentTemplateFormPage />
          </RequirePermission>
        ),
      },
```
（权限、组件不变，只改 path）

- [ ] **Step 2: 编译验证**

Run: `cd frontend && npm run build`
Expected: 成功（此时 TemplateFormPage 还用 useParams 取 typeId，会报 undefined 但不阻断编译；Task 6 修复）。

- [ ] **Step 3: 提交**

```bash
git add frontend/src/router.tsx
git commit -m "refactor(equipment): 模板表单路由去掉 typeId 路径段"
```

---

## Task 4: locale 补 key

为模板 Tab、查询字段、提示文案补 locale key。独立任务，后续页面任务依赖这些 key。

**Files:**
- Modify: `frontend/src/pages/business/equipment/locale/zh-CN.ts`
- Modify: `frontend/src/pages/business/equipment/locale/en-US.ts`

**Interfaces:**
- Produces: 一组 locale key，供 Task 5/6/7 使用

- [ ] **Step 1: zh-CN 补 key**

在 `frontend/src/pages/business/equipment/locale/zh-CN.ts` 的「运行模板 列表」段落附近补充：

```ts
  // ── 运行模板 Tab ──
  'equipment.tab.template': '运行模板',
  'equipment.template.tab.title': '运行模板',
  'equipment.template.tab.search.type': '设备类型',
  'equipment.template.tab.search.type.all': '全部类型',
  'equipment.template.tab.search.keyword': '模板名称',
  'equipment.template.tab.search.process': '工序',
  'equipment.template.tab.button.create': '新建模板',
  'equipment.template.tab.column.type': '所属类型',
  'equipment.template.tab.selectTypeFirst': '请先选择设备类型',
  'equipment.template.tab.manageInTab': '在「运行模板」Tab 中管理',
```

- [ ] **Step 2: en-US 补对应 key**

在 `frontend/src/pages/business/equipment/locale/en-US.ts` 对应位置补英文：

```ts
  'equipment.tab.template': 'Templates',
  'equipment.template.tab.title': 'Templates',
  'equipment.template.tab.search.type': 'Equipment Type',
  'equipment.template.tab.search.type.all': 'All Types',
  'equipment.template.tab.search.keyword': 'Template Name',
  'equipment.template.tab.search.process': 'Process',
  'equipment.template.tab.button.create': 'New Template',
  'equipment.template.tab.column.type': 'Type',
  'equipment.template.tab.selectTypeFirst': 'Please select an equipment type first',
  'equipment.template.tab.manageInTab': 'Manage in Templates tab',
```

- [ ] **Step 3: 提交**

```bash
git add frontend/src/pages/business/equipment/locale/
git commit -m "i18n(equipment): 模板 Tab 相关 locale key"
```

---

## Task 5: 新建 TemplateTab（模板列表页）

模板 Tab 的主页面。标准 Query Table + 类型筛选（支持全部）+ 跳转表单页。

**Files:**
- Create: `frontend/src/pages/business/equipment/template/TemplateTab.tsx`

**Interfaces:**
- Consumes: Task 2 的 `getEquipmentTemplatesPaged`、`getActiveEquipmentTypes`；Task 4 的 locale key；`@/api/process` 的 `getProcesses`
- Produces: `TemplateTab` 默认导出组件（无 props，自管状态），供 Task 8 容器页渲染

- [ ] **Step 1: 创建 TemplateTab.tsx**

创建 `frontend/src/pages/business/equipment/template/TemplateTab.tsx`，参照项目 Query Table 标准（参照 `EquipmentTab.tsx` 结构）：

```tsx
import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
import {
  Badge,
  Button,
  Card,
  Form,
  Grid,
  Input,
  Popconfirm,
  Select,
  Space,
  Table,
  Typography,
} from '@arco-design/web-react';
import { IconPlus, IconRefresh, IconSearch } from '@arco-design/web-react/icon';
import {
  EquipmentTemplateListItemDto,
  EquipmentTypeListItemDto,
  deleteEquipmentTemplate,
  getEquipmentTemplatesPaged,
} from '@/api/equipment';
import { getProcesses, ProcessListItem } from '@/api/process';
import useLocale from '@/utils/useLocale';
import PermissionWrapper from '@/components/PermissionWrapper';
import locale from '../locale';
import styles from '../style/index.module.less';

const { Title } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;
const Option = Select.Option;

function SearchForm({
  types,
  processes,
  onSearch,
}: {
  types: EquipmentTypeListItemDto[];
  processes: ProcessListItem[];
  onSearch: (v: Record<string, any>) => void;
}) {
  const [form] = Form.useForm();
  const t = useLocale(locale);
  const handleSubmit = () => onSearch(form.getFieldsValue());
  const handleReset = () => {
    form.resetFields();
    onSearch({});
  };
  return (
    <div className={styles['search-form-wrapper']}>
      <Form
        form={form}
        className={styles['search-form']}
        labelAlign="left"
        labelCol={{ span: 7 }}
        wrapperCol={{ span: 17 }}
      >
        <Row gutter={24}>
          <Col span={8}>
            <FormItem label={t['equipment.template.tab.search.type']} field="typeId">
              <Select allowClear placeholder={t['equipment.template.tab.search.type.all']}>
                {types.map((tp) => (
                  <Option key={tp.id} value={tp.id}>{tp.name}</Option>
                ))}
              </Select>
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['equipment.template.tab.search.keyword']} field="keyword">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['equipment.template.tab.search.process']} field="processId">
              <Select allowClear>
                {processes.map((p) => (
                  <Option key={p.id} value={p.id}>{p.name}</Option>
                ))}
              </Select>
            </FormItem>
          </Col>
        </Row>
      </Form>
      <div className={styles['right-button']}>
        <Button type="primary" icon={<IconSearch />} onClick={handleSubmit}>
          {t['equipment.type.button.search']}
        </Button>
        <Button icon={<IconRefresh />} onClick={handleReset}>
          {t['equipment.type.button.reset']}
        </Button>
      </div>
    </div>
  );
}

export default function TemplateTab() {
  const t = useLocale(locale);
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  const [data, setData] = useState<EquipmentTemplateListItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [formParams, setFormParams] = useState<Record<string, any>>({});
  const [pagination, setPagination] = useState({
    sizeCanChange: true,
    showTotal: true,
    pageSize: 10,
    current: 1,
    total: 0,
    pageSizeChangeResetCurrent: true,
  });
  const [types, setTypes] = useState<EquipmentTypeListItemDto[]>([]);
  const [processes, setProcesses] = useState<ProcessListItem[]>([]);

  useEffect(() => {
    getActiveEquipmentTypes().then(setTypes).catch(() => {});
    getProcesses({ page: 1, pageSize: 100, isActive: true })
      .then((res) => setProcesses(res.items || []))
      .catch(() => {});
  }, []);

  function fetchData() {
    setLoading(true);
    getEquipmentTemplatesPaged({
      page: pagination.current,
      pageSize: pagination.pageSize,
      ...formParams,
    })
      .then((res) => {
        setData(res.items || []);
        setPagination((p) => ({ ...p, total: res.total || 0 }));
      })
      .finally(() => setLoading(false));
  }

  function openCreate() {
    navigate('/business/equipment/template/create');
  }
  function openEdit(record: EquipmentTemplateListItemDto) {
    navigate(`/business/equipment/template/edit/${record.id}`);
  }
  async function handleDelete(record: EquipmentTemplateListItemDto) {
    try {
      await deleteEquipmentTemplate(record.equipmentTypeId, record.id);
      Message.success(t['equipment.template.message.deleteSuccess']);
      fetchData();
    } catch {
      // ignore
    }
  }

  const columns = useMemo(
    () => [
      { title: t['equipment.template.column.name'], dataIndex: 'name' },
      { title: t['equipment.template.tab.column.type'], dataIndex: 'equipmentTypeName' },
      { title: t['equipment.template.column.process'], dataIndex: 'processName' },
      {
        title: t['equipment.template.column.status'],
        dataIndex: 'status',
        render: (s: string) => {
          const map: Record<string, string> = { valid: 'success', invalid: 'error', orphan: 'warning' };
          const labelMap: Record<string, string> = {
            valid: t['equipment.template.status.valid'],
            invalid: t['equipment.template.status.invalid'],
            orphan: t['equipment.template.status.orphan'],
          };
          return <Badge status={(map[s] || 'success') as any} text={labelMap[s] || s} />;
        },
      },
      { title: t['equipment.template.column.sortOrder'], dataIndex: 'sortOrder' },
      { title: t['equipment.template.column.createdAt'], dataIndex: 'createdAt' },
      {
        title: t['equipment.template.column.operations'],
        dataIndex: 'operations',
        render: (_: any, record: EquipmentTemplateListItemDto) => (
          <Space>
            <PermissionWrapper requiredPermissions={[{ resource: 'equipment-type', actions: ['update'] }]}>
              <Button type="text" size="small" onClick={() => openEdit(record)}>
                {t['equipment.template.button.edit']}
              </Button>
            </PermissionWrapper>
            <PermissionWrapper requiredPermissions={[{ resource: 'equipment-type', actions: ['delete'] }]}>
              <Popconfirm title={t['equipment.template.message.deleteOk']} onOk={() => handleDelete(record)}>
                <Button type="text" size="small" status="danger">
                  {t['equipment.template.button.delete']}
                </Button>
              </Popconfirm>
            </PermissionWrapper>
          </Space>
        ),
      },
    ],
    [t],
  );

  function handleSearch(params: Record<string, any>) {
    setPagination((p) => ({ ...p, current: 1 }));
    setFormParams(params);
  }
  function onChangeTable({ current, pageSize }: any) {
    setPagination((p) => ({ ...p, current, pageSize }));
  }

  useEffect(() => {
    fetchData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pagination.current, pagination.pageSize, JSON.stringify(formParams)]);

  return (
    <Card>
      <Title heading={6}>{t['equipment.template.tab.title']}</Title>
      <SearchForm types={types} processes={processes} onSearch={handleSearch} />
      <div className={styles['button-group']}>
        <Space>
          <PermissionWrapper requiredPermissions={[{ resource: 'equipment-type', actions: ['create'] }]}>
            <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
              {t['equipment.template.tab.button.create']}
            </Button>
          </PermissionWrapper>
        </Space>
        <Space />
      </div>
      <Table
        rowKey="id"
        loading={loading}
        onChange={onChangeTable}
        pagination={pagination}
        columns={columns}
        data={data}
      />
    </Card>
  );
}
```

> 注意：需 import `Message` from arco（handleDelete 用）。确认 `ProcessListItem` 类型名（可能叫 `Process` 或 `ProcessListItemDto`，参照 `@/api/process` 导出）。确认 `getActiveEquipmentTypes` 已在 equipment.ts 导出（设备 Tab 在用）。`searchParams` 暂未使用（预留 typeId 持久化，可在 Task 8 接入）。

- [ ] **Step 2: 编译验证**

Run: `cd frontend && npm run build`
Expected: 成功。修正任何 import/类型错误。

- [ ] **Step 3: 提交**

```bash
git add frontend/src/pages/business/equipment/template/TemplateTab.tsx
git commit -m "feat(equipment): 模板 Tab 列表页（标准 Query Table + 类型筛选）"
```

---

## Task 6: 容器页加第 3 Tab

把模板 Tab 接入容器页。依赖 Task 5。

**Files:**
- Modify: `frontend/src/pages/business/equipment/index.tsx`

**Interfaces:**
- Consumes: Task 5 的 `TemplateTab`；现有 `?tab=` 持久化机制
- Produces: 容器页 3 Tab

- [ ] **Step 1: 加第 3 Tab**

在 `frontend/src/pages/business/equipment/index.tsx`：

import 加：`import TemplateTab from './template/TemplateTab';`

`activeTab` 的取值判断扩展（当前是 `=== 'type' ? 'type' : 'equipment'`），改为支持三值：
```ts
  const tab = searchParams.get('tab');
  const activeTab = tab === 'type' ? 'type' : tab === 'template' ? 'template' : 'equipment';
```

JSX 的 `<Tabs>` 里，在「设备类型」TabPane 之后加第 3 个：
```tsx
        <Tabs.TabPane
          key="template"
          title={t['equipment.tab.template']}
        >
          <PermissionWrapper
            requiredPermissions={[{ resource: 'equipment-type', actions: ['read'] }]}
          >
            <TemplateTab />
          </PermissionWrapper>
        </Tabs.TabPane>
```

- [ ] **Step 2: 编译 + 手测**

Run: `cd frontend && npm run build`
Expected: 成功。

- [ ] **Step 3: 提交**

```bash
git add frontend/src/pages/business/equipment/index.tsx
git commit -m "feat(equipment): 容器页新增运行模板 Tab"
```

---

## Task 7: TemplateFormPage 改造（类型选择器 + 动态加载）

最复杂的任务。模板表单页去掉对路由 typeId 的依赖，改为表单内选类型。依赖 Task 1（详情 DTO 含 typeId）、Task 2（前端类型字段）、Task 3（路由）。

**Files:**
- Modify: `frontend/src/pages/business/equipment/type/template/TemplateFormPage.tsx`

**Interfaces:**
- Consumes: `getEquipmentTemplateById`（返回含 equipmentTypeId，Task 1/2 补）；`getEquipmentTypeById`（按选的类型拉参数定义）；`getActiveEquipmentTypes`；`getProcesses`
- Produces: 类型选择器驱动的模板表单页

- [ ] **Step 1: 改造 TemplateFormPage**

对 `frontend/src/pages/business/equipment/type/template/TemplateFormPage.tsx` 做以下改造：

**1a. useParams 改造**：去掉 typeId，只取 id：
```ts
const { id } = useParams<{ id?: string }>();
const editing = !!id;
```

**1b. 新增类型选择 state + 类型列表**：
```ts
const [selectedTypeId, setSelectedTypeId] = useState<string>('');
const [types, setTypes] = useState<EquipmentTypeListItemDto[]>([]);
```

**1c. 工序列表 + 类型列表加载**（拆分原组合 useEffect）：
```ts
useEffect(() => {
  getProcesses({ page: 1, pageSize: 100, isActive: true })
    .then((res) => setProcesses(res.items || []))
    .catch(() => {});
  getActiveEquipmentTypes().then(setTypes).catch(() => {});
}, []);
```

**1d. 编辑模式：拉模板详情读 typeId**：
```ts
useEffect(() => {
  if (!editing || !id) return;
  setPageLoading(true);
  getEquipmentTemplateById('', id)  // 注意：路由已无 typeId，需确认 API 能否按 id 单查
    .then((detail) => {
      setSelectedTypeId(detail.equipmentTypeId);
      // 回填表单字段...
    })
    .finally(() => setPageLoading(false));
}, [id, editing]);
```

> **关键阻塞点**：现有 `getEquipmentTemplateById(typeId, id)` 调的是嵌套端点 `/api/equipment-types/{typeId}/templates/{id}`，需要 typeId。但路由去掉 typeId 后，编辑模式进页面时还不知道 typeId。**解决方案**：Task 1 的后端应同时新增一个顶层详情端点 `GET /api/equipment-templates/{id}`（不带 typeId），或前端先查列表定位。**推荐后端加顶层详情端点**（与 Task 1 的列表端点配套）。若 Task 1 未加，此处需回 Task 1 补。

**1e. 类型选择后拉参数定义**（新建模式动态加载）：
```ts
useEffect(() => {
  if (!selectedTypeId || editing) return;  // 编辑模式参数由详情一次性带回
  setSelectedTypeId 选中后：
  getEquipmentTypeById(selectedTypeId)
    .then((detail) => {
      setParameters(detail.parameters || []);
      setTypeName(detail.name);
      setValues([]);  // 切换类型清空已填值
      setExistingValues(undefined);
    });
}, [selectedTypeId, editing]);
```

**1f. 表单加「设备类型」Select**（新建可选/编辑禁用）：
在 Card 1 的字段行里加设备类型选择（放第一个字段）：
```tsx
<Col span={6}>
  <FormItem label={t['equipment.item.form.type']} field="equipmentTypeId" rules={[{ required: true }]}>
    <Select
      placeholder={t['equipment.item.form.type.placeholder']}
      showSearch
      disabled={editing}  // 编辑锁定
      onChange={(v) => setSelectedTypeId(v)}
    >
      {types.map((tp) => (
        <Option key={tp.id} value={tp.id}>{tp.name}</Option>
      ))}
    </Select>
  </FormItem>
</Col>
```

**1g. 参数值表单区条件渲染**（未选类型时提示）：
```tsx
{selectedTypeId ? (
  <FormItem>
    <TemplateValueEditor parameters={parameters} values={values} existingValues={existingValues} onChange={setValues} />
  </FormItem>
) : (
  <Empty description={t['equipment.template.tab.selectTypeFirst']} />
)}
```

**1h. 提交逻辑**：typeId 从 selectedTypeId 取（不再从路由）：
```ts
if (editing && id) {
  await updateEquipmentTemplate(selectedTypeId, id, payload);
} else {
  await createEquipmentTemplate(selectedTypeId, payload);
}
navigate('/business/equipment?tab=template');  // 回模板 Tab
```

**1i. 面包屑简化**：去掉类型层级：
```tsx
<Breadcrumb.Item>{t['equipment.tab.template']}</Breadcrumb.Item>
<Breadcrumb.Item>{pageTitle}</Breadcrumb.Item>
```

- [ ] **Step 2: 编译验证**

Run: `cd frontend && npm run build`
Expected: 成功。修正 import/类型错误。

- [ ] **Step 3: 提交**

```bash
git add frontend/src/pages/business/equipment/type/template/TemplateFormPage.tsx
git commit -m "refactor(equipment): 模板表单页类型选择器（新建可选/编辑锁定）+ 动态加载参数定义"
```

---

## Task 8: 设备类型详情抽屉模板段落降级

把抽屉里的可编辑 TemplateList 改为只读概览 + 跳转链接。

**Files:**
- Modify: `frontend/src/pages/business/equipment/type/TypeDetail.tsx`
- Modify: `frontend/src/pages/business/equipment/type/template/TemplateList.tsx`（可能改造为只读模式，或被内联取代）

**Interfaces:**
- Consumes: `useNavigate`（跳 `?tab=template&typeId=xxx`）；`data.templates`（EquipmentTypeDto 自带的模板摘要数组）

- [ ] **Step 1: TypeDetail 模板段落改只读**

在 `frontend/src/pages/business/equipment/type/TypeDetail.tsx`：

import 加 `useNavigate`。

模板段落（约 L118-124）替换为只读列表 + 跳转链接。注意 `EquipmentTypeDto` 自带 `templates` 摘要数组（`EquipmentTemplateSummaryDto[]`，含 name/processName/status），可直接渲染，不需要再 fetch：

```tsx
<Title heading={6} style={{ marginBottom: 12, marginTop: 16 }}>
  {t['equipment.type.detail.templates']}
  {`（${data.templateCount ?? (data.templates?.length || 0)}）`}
</Title>
{(data.templates?.length || 0) > 0 ? (
  <div style={{ marginBottom: 12 }}>
    {data.templates!.map((tpl) => (
      <div key={tpl.id} style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', padding: '6px 0', borderBottom: '1px solid var(--color-fill-2)' }}>
        <span style={{ fontSize: 13 }}>{tpl.name}</span>
        <span style={{ fontSize: 12, color: 'var(--color-text-3)' }}>{tpl.processName}</span>
      </div>
    ))}
  </div>
) : (
  <div style={{ fontSize: 13, color: 'var(--color-text-4)', marginBottom: 12 }}>暂无运行模板</div>
)}
<Button type="text" size="small" onClick={() => navigate(`/business/equipment?tab=template&typeId=${data.id}`)}>
  {t['equipment.template.tab.manageInTab']} →
</Button>
```

移除 `import TemplateList from './template/TemplateList';`（不再用）。

- [ ] **Step 2: TemplateList 去留处理**

`TemplateList.tsx` 现在没人引用了（抽屉不用了，Tab 用的是新 TemplateTab）。检查引用：
```bash
grep -rn "TemplateList" frontend/src/ | grep -v "TemplateList.tsx"
```
若无引用，删除 `frontend/src/pages/business/equipment/type/template/TemplateList.tsx`。

- [ ] **Step 3: 编译验证**

Run: `cd frontend && npm run build`
Expected: 成功。

- [ ] **Step 4: 提交**

```bash
git add frontend/src/pages/business/equipment/type/TypeDetail.tsx
git rm frontend/src/pages/business/equipment/type/template/TemplateList.tsx  # 若已删
git commit -m "refactor(equipment): 详情抽屉模板段落降级为只读 + 跳转链接"
```

---

## Task 9: 人工核对验收要点

最终验证。

- [ ] **Step 1: 启动后端 + 前端**

后端：`cd backend && dotnet run`（或配套的启动方式）
前端：`cd frontend && npm run dev`

- [ ] **Step 2: 逐条核对 spec 的 11 条验收要点**

1. 设备管理页 3 个 Tab：设备 / 设备类型 / 运行模板
2. 模板 Tab：类型筛选（Select，支持「全部」）+ 查询按钮 + 标准 Query Table
3. 选「全部」→ 列表展示所有类型模板（带所属类型列）
4. 选具体类型 → 筛到该类型模板
5. 点「新建模板」→ 表单页 → 选设备类型 → 参数值表单动态出现 → 保存成功
6. 新建时切换类型 → 已填值清空，按新类型重渲染
7. 点「编辑」→ 类型锁定不可改 → 回填 → 保存成功
8. 设备类型详情抽屉：模板段落只读 + 跳转链接正确（带 typeId）
9. Tab 切换/刷新保持状态
10. 后端新端点支持分页 + 筛选（可用 Postman/curl 验）
11. `npm run build` + `dotnet build` 通过

---

## Self-Review 备注

- **Spec 覆盖**：spec §2（后端）→ Task 1；spec §3.1-3.2（Tab + 列表页）→ Task 5+6；spec §3.3（表单页）→ Task 7；spec §3.4（抽屉降级）→ Task 8；spec §4（API）→ Task 2；spec §5（路由）→ Task 3；spec §6（locale）→ Task 4。全覆盖。
- **关键依赖链**：Task 1（后端 DTO + 端点）→ Task 2（前端 API）→ Task 5/7（页面）；Task 3（路由）独立先行；Task 4（locale）独立先行。执行顺序：1→2→3/4 并行→5→6→7→8→9。
- **风险点**：Task 7 的 1d 有阻塞点——编辑模式进页面时无 typeId，需后端配套顶层详情端点 `GET /api/equipment-templates/{id}`。Task 1 应一并加上（与列表端点配套）。**implementer 在 Task 1 务必同时加顶层详情端点**，否则 Task 7 卡住。
- **类型一致性**：`EquipmentTemplateListItemDto.equipmentTypeId/equipmentTypeName` 前后端字段名一致（camelCase 前端 / PascalCase 后端，JSON 序列化自动转换）。
