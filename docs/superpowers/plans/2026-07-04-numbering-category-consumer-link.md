# 编号分类码消费链路打通 Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 打通业务对象创建表单（颜色/客户）↔ 编号分类码字典的消费链路，让带编号表单能按编号规则自判是否需要分类码选择器，并透传到后端引擎。

**Architecture:** 扩展 Preview 接口返回 `includeCategory`（表单自判），抽 `useNumberingPreview` hook + `<CategorySelect>` 组件（可复用），消费端 Service/DTO 透传 categoryCode 到编号引擎。不改 schema、不加迁移、不新增 Guid/权限/菜单。

**Tech Stack:** 后端 ASP.NET Core + EF Core + PostgreSQL；前端 React + Arco Design + TypeScript。

## Global Constraints

- 分支：`feat/category-code-optimize`，worktree `.worktrees/category-code-optimize/`。只动本任务文件（并行开发约定 v2 §4.3）。
- **不新增任何种子 Guid**（复用 `system:numbering:*` 权限）。不动 `SeedData.cs`/`Program.cs`/`NumberTargetTypes.cs`/`routes.ts`/`router.tsx`/全局 `locale/index.ts`（约定 v2 §3.1/§3.4/§3.5）。
- **不改 schema** → 无 EF 迁移。与任务①②零物理冲突。
- 守约：c02（带编号对象创建流程，编号只读回填 + 无规则禁表单）；前端列表页标准不涉及（本任务只改表单）。
- 后端测试：`dotnet test backend/OneCup.sln`；前端：`cd frontend && npm run build`。
- 提交粒度：每个 Task 末尾提交一次，提交信息用 `feat`/`refactor`/`test` 前缀。

**关键接口契约（跨任务对齐）：**
- 后端 `INumberingService.PreviewAsync` 返回值 `string?` → `PreviewResult`（新 record `{ string? Code, bool IncludeCategory }`）。`GenerateAsync` 签名不变。
- 前端 `previewCode` 返回类型加 `includeCategory: boolean`。
- hook `useNumberingPreview(targetType)` 返回 `{ code, codeLoading, noRule, includeCategory, categoryOptions, categoryCode, setCategoryCode, reload }`。

---

## File Structure

**后端（改 6 文件 + 测试 3 文件，无新增源文件）：**
- `Dtos/System/NumberingDtos.cs` — 加 `PreviewResult` record，扩展 `PreviewCodeResult` 加 `IncludeCategory`
- `Interfaces/INumberingService.cs` — `PreviewAsync` 返回 `PreviewResult`
- `Infrastructure/Services/NumberingService.cs` — `PreviewAsync` 实现返回 `PreviewResult`
- `Controllers/NumberingController.cs` — `Preview` 端点映射 `PreviewResult` → `PreviewCodeResult`
- `Dtos/System/ColorDtos.cs` + `CustomerDtos.cs` — `Create*Request` 加 `CategoryCode`
- `Services/ColorService.cs` + `CustomerService.cs` — `CreateAsync` 透传 `request.CategoryCode`
- 测试：`Color/ColorServiceTests.cs`、`Customer/CustomerServiceTests.cs` 的 `FakeNumberingService`；`Numbering/NumberingServiceConcurrencyTests.cs` 的 Preview 断言

**前端（新增 2 文件 + 改 6 文件）：**
- 新增 `components/Numbering/useNumberingPreview.ts` — 可复用 hook
- 新增 `components/Numbering/CategorySelect.tsx` — 分类下拉薄封装
- 改 `api/numbering.ts`、`api/color.ts`、`api/customer.ts` — 类型/签名
- 改 `pages/master-data/color/index.tsx`、`pages/business/customer/form.tsx` — 接入 hook + 组件
- 改 `pages/master-data/color/locale/{zh-CN,en-US}.ts`、`pages/business/customer/locale/{zh-CN,en-US}.ts` — 文案

---

## Task 1: 后端 PreviewAsync 返回 PreviewResult（含 includeCategory）

**Files:**
- Modify: `backend/src/OneCup.Application/Dtos/System/NumberingDtos.cs`（`PreviewCodeResult` 在 `:70-74`）
- Modify: `backend/src/OneCup.Application/Interfaces/INumberingService.cs`（`PreviewAsync` 在 `:18`）
- Modify: `backend/src/OneCup.Infrastructure/Services/NumberingService.cs`（`PreviewAsync` 在 `:123-160`）
- Modify: `backend/src/OneCup.Api/Controllers/NumberingController.cs`（`Preview` 端点在 `:77-83`）

**Interfaces:**
- Produces: `PreviewResult` record `{ string? Code, bool IncludeCategory }`；`PreviewAsync` → `Task<PreviewResult>`；`PreviewCodeResult` 加 `bool IncludeCategory`。

- [ ] **Step 1: 写失败测试 — PreviewAsync 返回 IncludeCategory**

修改 `backend/tests/OneCup.UnitTests/Numbering/NumberingServiceConcurrencyTests.cs`。现有两个 Preview 测试（`:235-254`）断言 `string?`，需改为断言 `PreviewResult`。先把 `PreviewAsync_NoRule_ReturnsNull` 改造为断言新返回类型：

```csharp
[Fact]
public async Task PreviewAsync_NoRule_ReturnsNullCode()
{
    using var db = NewDbContext();
    var svc = new NumberingService(db, new NumberingClock());
    var preview = await svc.PreviewAsync("nonexistent");
    Assert.Null(preview.Code);
    Assert.False(preview.IncludeCategory);
}
```

把 `PreviewAsync_ReturnsNextWithoutConsuming`（`:235-245`）改为：

```csharp
[Fact]
public async Task PreviewAsync_ReturnsNextWithoutConsuming()
{
    using var db = NewDbContext();
    var svc = new NumberingService(db, new NumberingClock());
    var preview = await svc.PreviewAsync("fabric", "COT");
    Assert.NotNull(preview.Code);
    // 预览不消耗计数：连续两次预览应相同
    var preview2 = await svc.PreviewAsync("fabric", "COT");
    Assert.Equal(preview.Code, preview2.Code);
}
```

注：种子数据里 fabric 规则的 `IncludeCategory` 取决于 `NewDbContext()` 的种子；若该规则 IncludeCategory=true，可补一个断言 `Assert.True(preview.IncludeCategory)`。若不确定种子值，先只断言 Code，IncludeCategory 留到下个测试显式覆盖。

- [ ] **Step 2: 跑测试确认编译失败**

Run: `dotnet test backend/OneCup.sln --filter "PreviewAsync"`
Expected: 编译失败 — `PreviewAsync` 当前返回 `string?`，`preview.Code` 不存在。

- [ ] **Step 3: 加 PreviewResult record + 扩展 PreviewCodeResult**

`backend/src/OneCup.Application/Dtos/System/NumberingDtos.cs`，把 `:68-74` 的 `PreviewCodeResult` 块改为：

```csharp
// ── 预览 ──

/// <summary>PreviewAsync 的返回（服务层）。Code=null 表示无启用规则。</summary>
public record PreviewResult
{
    public string? Code { get; init; }
    public bool IncludeCategory { get; init; }   // 规则是否要求分类码
}

/// <summary>预览端点的 HTTP 响应。</summary>
public record PreviewCodeResult
{
    public string? Code { get; init; }
    public bool IncludeCategory { get; init; }
    public string Note { get; init; } = "预览编号，实际保存时以系统分配为准";
}
```

- [ ] **Step 4: 改 INumberingService 接口签名**

`backend/src/OneCup.Application/Interfaces/INumberingService.cs`，把 `:18` 的 PreviewAsync 改为：

```csharp
Task<PreviewResult> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default);
```
（`GenerateAsync` 行不动。）

- [ ] **Step 5: 改 NumberingService.PreviewAsync 实现**

`backend/src/OneCup.Infrastructure/Services/NumberingService.cs`，把 `:123-160` 的 PreviewAsync 改为返回 `PreviewResult`。关键改动：方法签名 `Task<string?>` → `Task<PreviewResult>`；rule 为 null 时返回 `{ Code: null, IncludeCategory: false }`；末尾 return 包装。完整新方法：

```csharp
public async Task<PreviewResult> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
{
    var rule = await _db.NumberingRules
        .FirstOrDefaultAsync(r => r.TargetType == targetType && r.IsActive, ct);
    if (rule is null) return new PreviewResult { Code = null, IncludeCategory = false };

    // ── 字典强校验（Task 6）──
    var typeExists = await _db.NumberingTargetTypes
        .AnyAsync(t => t.Code == targetType && t.IsActive, ct);
    if (!typeExists)
        throw new DomainException($"业务类型 {targetType} 不存在或已停用");

    if (rule.IncludeCategory && !string.IsNullOrEmpty(categoryCode))
    {
        var catExists = await _db.NumberingCategories
            .AnyAsync(c => c.TargetTypeCode == targetType
                        && c.Code == categoryCode && c.IsActive, ct);
        if (!catExists)
            throw new DomainException($"分类码 {categoryCode} 不存在或已停用");
    }

    var effectiveCategory = rule.IncludeCategory ? categoryCode : null;

    var now = _clock.GetCurrentTime();
    var periodKey = PeriodKeyCalculator.Calc(rule.ResetPeriod, now);
    var bucketCategory = effectiveCategory ?? "";
    var bucketPeriod = periodKey;

    // 只读查询，不加锁
    var currentSeq = await _db.NumberingCounters
        .Where(c => c.RuleId == rule.Id && c.CategoryCode == bucketCategory && c.PeriodKey == bucketPeriod)
        .Select(c => (int?)c.CurrentSeq)
        .FirstOrDefaultAsync(ct) ?? 0;

    var code = CodeFormatter.Format(
        rule.Prefix, rule.IncludeCategory, rule.DateSegment,
        rule.SeqLength, rule.Separator, currentSeq + 1, effectiveCategory, now);

    return new PreviewResult { Code = code, IncludeCategory = rule.IncludeCategory };
}
```

- [ ] **Step 6: 改 NumberingController.Preview 端点映射**

`backend/src/OneCup.Api/Controllers/NumberingController.cs`，把 `:77-83` 的 Preview 改为：

```csharp
[HttpGet("preview")]
[Authorize]
public async Task<IActionResult> Preview([FromQuery] string targetType, [FromQuery] string? categoryCode = null, CancellationToken ct = default)
{
    var r = await _numberingService.PreviewAsync(targetType, categoryCode, ct);
    return Ok(new PreviewCodeResult { Code = r.Code, IncludeCategory = r.IncludeCategory });
}
```

- [ ] **Step 7: 修两个 FakeNumberingService（接口实现变更）**

`backend/tests/OneCup.UnitTests/Color/ColorServiceTests.cs:211-212`：
```csharp
public Task<PreviewResult> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
    => Task.FromResult(new PreviewResult { Code = NextCode ?? $"COL-{(_seq + 1):D4}", IncludeCategory = false });
```
（需在文件顶部 `using OneCup.Application.Dtos.System;` 已存在，`PreviewResult` 在该命名空间。若未 using 则加。）

`backend/tests/OneCup.UnitTests/Customer/CustomerServiceTests.cs:219-220`：
```csharp
public Task<PreviewResult> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
    => Task.FromResult(new PreviewResult { Code = NextCode, IncludeCategory = false });
```

- [ ] **Step 8: 跑测试确认全绿**

Run: `dotnet test backend/OneCup.sln`
Expected: PASS（所有原有测试 + 改造的两个 Preview 测试）。若 `ColorServiceTests.cs`/`CustomerServiceTests.cs` 顶部无 `using OneCup.Application.Dtos.System;`，编译会报 `PreviewResult` 未找到 → 加 using。

- [ ] **Step 9: 提交**

```bash
git add backend/src/OneCup.Application/Dtos/System/NumberingDtos.cs backend/src/OneCup.Application/Interfaces/INumberingService.cs backend/src/OneCup.Infrastructure/Services/NumberingService.cs backend/src/OneCup.Api/Controllers/NumberingController.cs backend/tests/OneCup.UnitTests/Numbering/NumberingServiceConcurrencyTests.cs backend/tests/OneCup.UnitTests/Color/ColorServiceTests.cs backend/tests/OneCup.UnitTests/Customer/CustomerServiceTests.cs
git commit -m "refactor(numbering): PreviewAsync 返回 PreviewResult(含 includeCategory)"
```

---

## Task 2: 后端消费侧 DTO + Service 透传 categoryCode

**Files:**
- Modify: `backend/src/OneCup.Application/Dtos/System/ColorDtos.cs`（`CreateColorRequest :4-12`）
- Modify: `backend/src/OneCup.Application/Dtos/System/CustomerDtos.cs`（`CreateCustomerRequest :24-32`）
- Modify: `backend/src/OneCup.Application/Services/ColorService.cs`（`CreateColorAsync :72`）
- Modify: `backend/src/OneCup.Application/Services/CustomerService.cs`（`CreateAsync :98`）
- Test: `backend/tests/OneCup.UnitTests/Color/ColorServiceTests.cs`（加透传断言）

**Interfaces:**
- Consumes: `INumberingService.GenerateAsync(string, string?, CancellationToken)`（已存在，不变）
- Produces: `CreateColorRequest.CategoryCode`、`CreateCustomerRequest.CategoryCode`（可空 string）

- [ ] **Step 1: 写失败测试 — ColorService 透传 categoryCode**

`backend/tests/OneCup.UnitTests/Color/ColorServiceTests.cs`。先增强 `FakeNumberingService`（`:194-213`）记录传入的 categoryCode。把 GenerateAsync 改为：

```csharp
public string? LastCategoryCode { get; private set; }
public Task<string> GenerateAsync(string targetType, string? categoryCode = null, CancellationToken ct = default)
{
    LastCategoryCode = categoryCode;
    if (NextCode is not null)
    {
        var code = NextCode;
        NextCode = null;
        return Task.FromResult(code);
    }
    _seq++;
    return Task.FromResult($"COL-{_seq:D4}");
}
```

加新测试方法（放在 `CreateColorAsync_CreatesColor_WithGeneratedCode` 之后）：

```csharp
[Fact]
public async Task CreateColorAsync_PassesCategoryCode_ToNumbering()
{
    var (_, svc, numbering) = Setup();
    numbering.NextCode = "COL-DARK-0001";
    var req = ValidCreate() with { CategoryCode = "DARK" };

    await svc.CreateColorAsync(req);
    Assert.Equal("DARK", numbering.LastCategoryCode);
}
```

- [ ] **Step 2: 跑测试确认失败**

Run: `dotnet test backend/OneCup.sln --filter "PassesCategoryCode"`
Expected: FAIL — `CreateColorRequest` 无 `CategoryCode` 属性，编译错误。

- [ ] **Step 3: 给 CreateColorRequest 加 CategoryCode**

`backend/src/OneCup.Application/Dtos/System/ColorDtos.cs`，`CreateColorRequest`（`:4-12`）末尾加字段：

```csharp
public record CreateColorRequest
{
    public string NameZh { get; init; } = string.Empty;
    public string NameEn { get; init; } = string.Empty;
    public string Hex { get; init; } = string.Empty;
    public string ColorFamily { get; init; } = string.Empty;
    public string? Remark { get; init; }
    public int SortOrder { get; init; }
    /// <summary>可选；编号规则要求分类码时必填，由引擎强校验。</summary>
    public string? CategoryCode { get; init; }
}
```

- [ ] **Step 4: ColorService.CreateColorAsync 透传**

`backend/src/OneCup.Application/Services/ColorService.cs:72`，把 `null` 换成 `request.CategoryCode`：

```csharp
var code = await _numbering.GenerateAsync(NumberTargetTypes.Color, request.CategoryCode, ct);
```

- [ ] **Step 5: 给 CreateCustomerRequest 加 CategoryCode**

`backend/src/OneCup.Application/Dtos/System/CustomerDtos.cs`，`CreateCustomerRequest`（`:24-32`）末尾加：

```csharp
/// <summary>可选；编号规则要求分类码时必填，由引擎强校验。</summary>
public string? CategoryCode { get; set; }
```

- [ ] **Step 6: CustomerService.CreateAsync 透传**

`backend/src/OneCup.Application/Services/CustomerService.cs:98`：

```csharp
var code = await _numbering.GenerateAsync(NumberTargetTypes.Customer, request.CategoryCode, ct);
```

- [ ] **Step 7: 跑测试确认全绿**

Run: `dotnet test backend/OneCup.sln`
Expected: PASS。

- [ ] **Step 8: 提交**

```bash
git add backend/src/OneCup.Application/Dtos/System/ColorDtos.cs backend/src/OneCup.Application/Dtos/System/CustomerDtos.cs backend/src/OneCup.Application/Services/ColorService.cs backend/src/OneCup.Application/Services/CustomerService.cs backend/tests/OneCup.UnitTests/Color/ColorServiceTests.cs
git commit -m "feat(color,customer): Create*Request 透传 categoryCode 到编号引擎"
```

---

## Task 3: 前端 API 类型对齐（previewCode 返回 + Create* 类型加 categoryCode）

**Files:**
- Modify: `frontend/src/api/numbering.ts`（`previewCode :73-77`）
- Modify: `frontend/src/api/color.ts`（`CreateColorRequest` 接口）
- Modify: `frontend/src/api/customer.ts`（`CustomerFormData :35-42`）

**Interfaces:**
- Produces: `previewCode` 返回 `{ code: string | null; includeCategory: boolean; note: string }`；`CreateColorRequest.categoryCode?`、`CustomerFormData.categoryCode?`。

- [ ] **Step 1: 改 previewCode 返回类型**

`frontend/src/api/numbering.ts:73-77`：

```ts
export function previewCode(targetType: string, categoryCode?: string) {
  return request.get<unknown, { code: string | null; includeCategory: boolean; note: string }>(
    '/api/numbering/preview',
    { params: { targetType, categoryCode } },
  );
}
```

- [ ] **Step 2: 给 CreateColorRequest 接口加 categoryCode**

查 `frontend/src/api/color.ts` 的 `CreateColorRequest` 接口，加可选字段 `categoryCode?: string;`。

- [ ] **Step 3: 给 CustomerFormData 加 categoryCode**

`frontend/src/api/customer.ts:35-42`：

```ts
export interface CustomerFormData {
  name: string;
  shortName?: string;
  contactPerson?: string;
  contactPhone?: string;
  remark?: string;
  isActive: boolean;
  categoryCode?: string;
}
```

- [ ] **Step 4: 跑 build 确认类型一致**

Run: `cd frontend && npm run build`
Expected: BUILD 成功（类型改动向后兼容，旧调用处 `previewCode('color')` 仍合法，多出的 `includeCategory` 字段暂未消费不报错）。

- [ ] **Step 5: 提交**

```bash
git add frontend/src/api/numbering.ts frontend/src/api/color.ts frontend/src/api/customer.ts
git commit -m "refactor(api): previewCode 返回 includeCategory; Create* 类型加 categoryCode"
```

---

## Task 4: 前端 useNumberingPreview hook

**Files:**
- Create: `frontend/src/components/Numbering/useNumberingPreview.ts`

**Interfaces:**
- Consumes: `previewCode(targetType, categoryCode?)` → `{ code, includeCategory, note }`（Task 3）；`getActiveCategories(targetTypeCode)` → `Category[]`（已存在 `api/numberingDictionary.ts:90`）
- Produces: `useNumberingPreview(targetType)` → `{ code, codeLoading, noRule, includeCategory, categoryOptions, categoryCode, setCategoryCode, reload }`

- [ ] **Step 1: 创建 hook 文件**

`frontend/src/components/Numbering/useNumberingPreview.ts`：

```ts
import { useState, useCallback } from 'react';
import { previewCode } from '@/api/numbering';
import { getActiveCategories, Category } from '@/api/numberingDictionary';

/**
 * 编号预览 + 分类码自判的可复用 hook。
 * 打开新建表单调 reload()；返回的 includeCategory 决定是否渲染分类选择器；
 * 选分类调 setCategoryCode()，hook 内自动重新 previewCode 刷新编号。
 * noRule=true 时调用方应禁表单 + Alert（守 Convention c02）。
 */
export function useNumberingPreview(targetType: string) {
  const [code, setCode] = useState<string | null>(null);
  const [codeLoading, setCodeLoading] = useState(false);
  const [noRule, setNoRule] = useState(false);
  const [includeCategory, setIncludeCategory] = useState(false);
  const [categoryOptions, setCategoryOptions] = useState<Category[]>([]);
  const [categoryCode, setCategoryCodeState] = useState<string | undefined>(undefined);

  // 表单打开新建时调用：首次预览 + 按需加载分类
  const reload = useCallback(() => {
    setCode(null);
    setNoRule(false);
    setIncludeCategory(false);
    setCategoryOptions([]);
    setCategoryCodeState(undefined);
    setCodeLoading(true);
    previewCode(targetType)
      .then((res) => {
        if (!res.code) {
          setNoRule(true);
          return;
        }
        setCode(res.code);
        setIncludeCategory(res.includeCategory);
        if (res.includeCategory) {
          getActiveCategories(targetType)
            .then(setCategoryOptions)
            .catch(() => setCategoryOptions([]));
        }
      })
      .catch(() => setNoRule(true))
      .finally(() => setCodeLoading(false));
  }, [targetType]);

  // 选分类 → 自动重新预览刷新编号
  const setCategoryCode = useCallback((c?: string) => {
    setCategoryCodeState(c);
    setCodeLoading(true);
    previewCode(targetType, c)
      .then((res) => setCode(res.code)) // 规则已在 reload 阶段确认存在
      .catch(() => setNoRule(true))
      .finally(() => setCodeLoading(false));
  }, [targetType]);

  return {
    code,
    codeLoading,
    noRule,
    includeCategory,
    categoryOptions,
    categoryCode,
    setCategoryCode,
    reload,
  };
}
```

- [ ] **Step 2: 跑 build 确认编译**

Run: `cd frontend && npm run build`
Expected: BUILD 成功（hook 暂未被调用，仅编译检查）。

- [ ] **Step 3: 提交**

```bash
git add frontend/src/components/Numbering/useNumberingPreview.ts
git commit -m "feat(numbering): useNumberingPreview 可复用 hook"
```

---

## Task 5: 前端 CategorySelect 组件

**Files:**
- Create: `frontend/src/components/Numbering/CategorySelect.tsx`

**Interfaces:**
- Consumes: `Category`（`api/numberingDictionary.ts`，字段 `{ code, nameZh, ... }`）
- Produces: `<CategorySelect options value onChange loading placeholder />`

- [ ] **Step 1: 创建组件文件**

`frontend/src/components/Numbering/CategorySelect.tsx`：

```tsx
import { Select } from '@arco-design/web-react';
import type { Category } from '@/api/numberingDictionary';

const { Option } = Select;

interface Props {
  options: Category[];
  value?: string;
  onChange?: (code?: string) => void;
  loading?: boolean;
  placeholder?: string;
}

/**
 * 编号分类码下拉。option 显示「code · 中文名」，value 用 code。
 * 给带编号的业务对象创建表单在 includeCategory=true 时条件渲染。
 */
export default function CategorySelect({ options, value, onChange, loading, placeholder }: Props) {
  return (
    <Select
      value={value}
      onChange={onChange}
      loading={loading}
      placeholder={placeholder}
      allowClear
      showSearch
    >
      {options.map((c) => (
        <Option key={c.code} value={c.code}>
          {c.code} · {c.nameZh}
        </Option>
      ))}
    </Select>
  );
}
```

- [ ] **Step 2: 跑 build 确认编译**

Run: `cd frontend && npm run build`
Expected: BUILD 成功。

- [ ] **Step 3: 提交**

```bash
git add frontend/src/components/Numbering/CategorySelect.tsx
git commit -m "feat(numbering): CategorySelect 可复用分类下拉组件"
```

---

## Task 6: 前端 — 颜色表单接入 hook + 分类选择器

**Files:**
- Modify: `frontend/src/pages/master-data/color/index.tsx`
- Modify: `frontend/src/pages/master-data/color/locale/zh-CN.ts`（`form.` 段，`:32-46`）
- Modify: `frontend/src/pages/master-data/color/locale/en-US.ts`（对应段）

**Interfaces:**
- Consumes: `useNumberingPreview('color')`（Task 4）、`<CategorySelect>`（Task 5）

- [ ] **Step 1: 加 locale 文案**

`frontend/src/pages/master-data/color/locale/zh-CN.ts`，在 `color.form.remark` 附近加：

```ts
'color.form.category': '分类',
'color.form.category.placeholder': '请选择分类',
```

`frontend/src/pages/master-data/color/locale/en-US.ts` 对应加：

```ts
'color.form.category': 'Category',
'color.form.category.placeholder': 'Select category',
```

- [ ] **Step 2: 改 color 页面 — 引入 hook + 组件，替换本地预览状态**

`frontend/src/pages/master-data/color/index.tsx`。

顶部 import 加（`:13` 附近，`previewCode` 行替换为 hook + 组件）：
```ts
import { useNumberingPreview } from '@/components/Numbering/useNumberingPreview';
import CategorySelect from '@/components/Numbering/CategorySelect';
```
（删除 `import { previewCode } from '@/api/numbering';` —— 不再直接用。）

在 `ColorPage` 组件内（`:82` 之后），把本地三段状态 `previewedCode/codeLoading/noRule`（`:98-101`）删除，替换为：
```ts
const preview = useNumberingPreview('color');
```

- [ ] **Step 3: 改 openCreate 调 reload**

`openCreate()`（`:126-148`）：删掉 `setNoRule(false)/setPreviewedCode(null)/setCodeLoading(true)` 和整段 `previewCode('color').then(...)`，改为：
```ts
function openCreate() {
  setEditMode('create');
  setEditingId(null);
  form.resetFields();
  form.setFieldsValue({ sortOrder: 0 });
  preview.reload();
  setDrawerVisible(true);
}
```
`openEdit` 里 `setPreviewedCode(record.code)` → 改用本地一个轻量状态展示编辑态编号，或直接保留一个 `editingCode` 状态。最简做法：新增 `const [editingCode, setEditingCode] = useState<string | null>(null);`，`openCreate` 时 `setEditingCode(null)`，`openEdit` 时 `setEditingCode(record.code)`。

- [ ] **Step 4: 改抽屉 — 编号只读 + 条件分类下拉 + noRule 接 hook**

抽屉内（`:266-280` 附近）：
- `okButtonProps={{ disabled: noRule }}` → `okButtonProps={{ disabled: preview.noRule }}`
- `{noRule && <Alert .../>}` → `{preview.noRule && <Alert .../>}`
- `<Form ... disabled={noRule}>` → `<Form ... disabled={preview.noRule}>`
- 编号只读 Input：
  ```tsx
  <FormItem label={t['color.form.code']}>
    <Input
      value={(editMode === 'edit' ? editingCode : preview.code) ?? undefined}
      readOnly
      placeholder={preview.codeLoading ? t['color.form.code.previewing'] : t['color.form.code.placeholder']}
    />
  </FormItem>
  ```
- 编号字段下方条件插入分类下拉（仅 create 模式 + includeCategory）：
  ```tsx
  {editMode === 'create' && preview.includeCategory && (
    <FormItem label={t['color.form.category']} field="categoryCode">
      <CategorySelect
        options={preview.categoryOptions}
        value={preview.categoryCode}
        onChange={preview.setCategoryCode}
        loading={preview.codeLoading}
        placeholder={t['color.form.category.placeholder']}
      />
    </FormItem>
  )}
  ```

- [ ] **Step 5: 改 handleDrawerOk 提交并入 categoryCode**

`handleDrawerOk`（`:163-181`）create 分支：
```ts
if (editMode === 'create') {
  await createColor({ ...values, categoryCode: preview.categoryCode });
  Message.success(t['color.create.success']);
}
```

- [ ] **Step 6: 跑 build 确认**

Run: `cd frontend && npm run build`
Expected: BUILD 成功（无 TS 报错；删除的 `previewedCode/codeLoading/noRule` 引用全部替换为 preview.* 或 editingCode）。

- [ ] **Step 7: 提交**

```bash
git add frontend/src/pages/master-data/color/index.tsx frontend/src/pages/master-data/color/locale/zh-CN.ts frontend/src/pages/master-data/color/locale/en-US.ts
git commit -m "feat(color): 新建表单接入分类码选择(规则驱动)"
```

---

## Task 7: 前端 — 客户表单接入 hook + 分类选择器

**Files:**
- Modify: `frontend/src/pages/business/customer/form.tsx`
- Modify: `frontend/src/pages/business/customer/locale/zh-CN.ts`（`form.` 段，`:22-33`）
- Modify: `frontend/src/pages/business/customer/locale/en-US.ts`

**Interfaces:**
- Consumes: `useNumberingPreview('customer')`（Task 4）、`<CategorySelect>`（Task 5）

- [ ] **Step 1: 加 locale 文案**

`frontend/src/pages/business/customer/locale/zh-CN.ts` 加：
```ts
'customer.form.category': '分类',
'customer.form.category.placeholder': '请选择分类',
```
en-US 对应：
```ts
'customer.form.category': 'Category',
'customer.form.category.placeholder': 'Select category',
```

- [ ] **Step 2: 改 customer form — 引入 hook + 组件，替换本地预览状态**

`frontend/src/pages/business/customer/form.tsx`。

顶部 import（`:16` 附近）：删 `import { previewCode } from '@/api/numbering';`，加：
```ts
import { useNumberingPreview } from '@/components/Numbering/useNumberingPreview';
import CategorySelect from '@/components/Numbering/CategorySelect';
```

组件内（`:34` 之后），删本地 `previewedCode/codeLoading/noRule`（`:39-42`），加：
```ts
const preview = useNumberingPreview('customer');
```

- [ ] **Step 3: 改 useEffect 新建分支调 reload**

`useEffect`（`:44-79`）新建分支（`:59-77`）：删掉 `setPreviewedCode(null)/setCodeLoading(true)` 和整段 `previewCode('customer').then(...)`，改为 `preview.reload();`。保留 `form.resetFields(); form.setFieldValue('isActive', true);`。编辑分支 `setPreviewedCode(editing.code)` 改用本地 `editingCode` 状态（同 Task 6 Step 3 做法）。

- [ ] **Step 4: 改 Modal — 编号只读 + 条件分类下拉 + noRule 接 hook**

`Modal`（`:108-120`）：
- `okButtonProps={{ disabled: noRule }}` → `okButtonProps={{ disabled: preview.noRule }}`
- `{noRule && <Alert .../>}` → `{preview.noRule && <Alert .../>}`
- `<Form ... disabled={noRule}>` → `<Form ... disabled={preview.noRule}>`
- 编号只读 Input（`:123-129`）value 接 `(editing ? editing.code : preview.code)`，placeholder 接 `preview.codeLoading`。
- 编号字段下方条件插入（编辑态不显示）：
  ```tsx
  {!editing && preview.includeCategory && (
    <FormItem label={t['customer.form.category']} field="categoryCode">
      <CategorySelect
        options={preview.categoryOptions}
        value={preview.categoryCode}
        onChange={preview.setCategoryCode}
        loading={preview.codeLoading}
        placeholder={t['customer.form.category.placeholder']}
      />
    </FormItem>
  )}
  ```

- [ ] **Step 5: 改 handleOk 提交并入 categoryCode**

`handleOk`（`:81-106`）新建分支：
```ts
await createCustomer({ ...values, categoryCode: preview.categoryCode });
```

- [ ] **Step 6: 跑 build 确认**

Run: `cd frontend && npm run build`
Expected: BUILD 成功。

- [ ] **Step 7: 提交**

```bash
git add frontend/src/pages/business/customer/form.tsx frontend/src/pages/business/customer/locale/zh-CN.ts frontend/src/pages/business/customer/locale/en-US.ts
git commit -m "feat(customer): 新建表单接入分类码选择(规则驱动)"
```

---

## Task 8: 全量验证 + 收尾

**Files:** 无（仅验证）

- [ ] **Step 1: 后端全量构建 + 测试**

Run: `dotnet build backend/OneCup.sln && dotnet test backend/OneCup.sln`
Expected: BUILD 成功 + 全部测试 PASS（含 Task 1/2 改造的 PreviewAsync 与 ColorService 透传测试）。

- [ ] **Step 2: 前端全量构建**

Run: `cd frontend && npm run build`
Expected: BUILD 成功。

- [ ] **Step 3: 手验路径（需本地起前后端）**

1. 编号管理页 → 给 color 配一条规则，勾「包含分类码」(`includeCategory=true`)，建几个分类（如 DARK/MID/LIGHT）。
2. 主数据 → 颜色 → 新建：表单应出现「分类」下拉；选不同分类，编号预览刷新带分类段；提交后编号正确。
3. customer 默认规则不勾分类码 → 新建客户表单**不出现**分类下拉，行为与改造前一致（向后兼容）。
4. 临时停用 color 规则 → 新建颜色表单 `noRule` 提示 + 禁用（守 c02）。

- [ ] **Step 4: 确认未越界**

Run: `git diff main --stat`
Expected: 改动文件仅限本计划 File Structure 列表 + design spec/plan 文档；**无** `SeedData.cs`/`Program.cs`/`NumberTargetTypes.cs`/`routes.ts`/`router.tsx`/全局 `locale/index.ts`/迁移文件。

- [ ] **Step 5: 最终提交（如有遗留改动）**

```bash
git add -A
git commit -m "chore: 分类码消费链路打通收尾验证"
```
（若 Step 1-2 全绿且无遗留改动，可跳过本步。）
