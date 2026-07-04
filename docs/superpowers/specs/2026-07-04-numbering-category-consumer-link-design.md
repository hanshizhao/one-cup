# 编号分类码消费链路打通 — 设计规格

> 任务:`feat/category-code-optimize`(并行开发约定 v2 · 任务③)
> 日期:2026-07-04
> 状态:待实现

---

## 1. 背景与问题

编号分类码(NumberingCategory)的**字典侧**已完整实现:`NumberingCategory` 实体 + CRUD +
引擎强校验 + 前端管理页(`pages/system/numbering/dict/`),以及
`GET /api/numbering/dict/categories/all?targetTypeCode=...` 查询接口、
`previewCode(targetType, categoryCode?)`/`GenerateAsync(targetType, categoryCode?)`
的 categoryCode 可选参支持。

但**消费侧**(业务对象创建表单 → 编擎)从未接通。4 个断点:

| 层 | 现状 | 断点 |
|---|---|---|
| 后端 Service | `ColorService.CreateColorAsync`(:72)、`CustomerService.CreateAsync`(:98) 调 `GenerateAsync(..., null, ct)` 硬编码 categoryCode=null | 不接受也不透传 categoryCode |
| 后端 DTO | `CreateColorRequest`/`CreateCustomerRequest` 无 categoryCode 字段 | 无法接收 |
| 前端表单 | `color/index.tsx:135`、`customer/form.tsx:63` 调 `previewCode('color')`/`previewCode('customer')` 不传第二参;无分类下拉 | 不传 categoryCode、无选择器 |
| Preview 接口 | `PreviewCodeResult` 只有 `{code, note}` | 表单无法自判「是否需要分类选择器」 |

典型场景:颜色编号规则下有多个分类码(深色/中色/浅色),新建颜色时用户需**选择**用哪个分类码,
而非系统默认。

## 2. 目标

打通消费链路,且做到**规则驱动 + 自驱动 + 可复用**:

- **规则驱动**:只有用户在编号管理页把某 targetType 的规则勾上「包含分类码」
  (`rule.IncludeCategory = true`),对应表单才出现分类下拉;未勾选的表单行为不变(向后兼容)。
- **自驱动**:每个带编号表单打开时一次 `previewCode()` 调用即可从返回值自判
  「要不要渲染分类选择器」,无需额外查规则接口、无需硬编码「哪些表单要分类」。
- **可复用**:把「预览 + 自判 + 选后重预览」抽成 hook + 组件,以后 material/process 等新表单
  只加一行即可接入,避免每次手改。

## 3. 边界(对齐并行开发约定 v2)

- **不改 schema** → 无 EF 迁移 → 与任务①②零物理冲突(FAQ Q6 独立路径)。
- **不新增 Guid/权限/菜单**:复用 `system:numbering:*` 权限;分类码是 numbering 页内已有 Tab,
  不动 `routes.ts`/`router.tsx`。
- **不动共享文件**:`SeedData.cs`/`Program.cs`/`NumberTargetTypes.cs`/全局 `locale/index.ts` 都不改。
- 守约引用:c02(带编号对象创建流程)、c01(删除/启停确认 — 本任务不涉及删除,启停沿用现状)。

## 4. 方案选型

### 4.1 子问题 A:表单如何自判「要不要分类选择器」

| 方案 | 做法 | 取舍 |
|---|---|---|
| **A1 扩展 Preview 返回值(采用)** | `PreviewCodeResult` 加 `includeCategory: bool`;`PreviewAsync` 返回 `PreviewResult{Code, IncludeCategory}` | 一次请求同时拿到「有无规则 + 是否需分类」;调用次数最少;前端零额外查询。代价:`PreviewAsync` 签名 `string?` → `PreviewResult`(后端 internal breaking,但唯一调用方是 1 个端点,可控) |
| A2 前端额外查规则接口 | 表单打开先 `getNumberingRules({targetType,isActive:true})` 读 includeCategory | 不动后端 Preview DTO,但每次开表单多一次请求,逻辑分散 |
| A3 始终显示选择器 | 永远渲染下拉,不要分类的规则由后端忽略 | 实现最简,但对不要分类的业务对象(如客户)造成多余 UI,违背规则驱动 |

**选 A1**:契合「让表单从 preview 返回值自行判断」的诉求,hook 内部一次调用完成判断。

### 4.2 子问题 B:复用方式

| 方案 | 做法 | 取舍 |
|---|---|---|
| **B1 抽 hook + 组件(采用)** | `useNumberingPreview(targetType)` + `<CategorySelect>` | 新表单只加一行 hook + 条件渲染;逻辑集中可测。代价:新增 2 文件 |
| B2 内联实现 | 每个表单内联写完整逻辑 | 无新文件,但逻辑在 color/customer 重复,以后还要再抄 |

**选 B1**:直接对应「以后新表单不用每次手改代码」的目标。

### 4.3 子问题 C:`PreviewAsync` 签名怎么改

- **C1(采用)**:返回 `Task<PreviewResult>`(新 record `{ string? Code, bool IncludeCategory }`)。
  「无规则」=`{Code:null, IncludeCategory:false}`。唯一调用方 `NumberingController.Preview` 一并改。
  `GenerateAsync` 签名**不变**(仍 `string? categoryCode` 可选参,已正常工作)。
- C2:`out bool` 参数 — async 不支持 `out`,排除。

## 5. 后端详设

全部为纯扩展 + 一处 internal 签名调整,不改 schema、不加权限、不加迁移。

### 5.1 Preview 返回值扩展

**`Dtos/System/NumberingDtos.cs`**

新增 record + 扩展 `PreviewCodeResult`:
```csharp
// PreviewAsync 的返回(服务层)
public record PreviewResult
{
    public string? Code { get; init; }            // null = 无启用规则
    public bool IncludeCategory { get; init; }    // 规则是否要求分类码
}

// HTTP 响应(控制器层,字段名对齐前端)
public record PreviewCodeResult
{
    public string? Code { get; init; }
    public bool IncludeCategory { get; init; }    // 新增
    public string Note { get; init; } = "预览编号，实际保存时以系统分配为准";
}
```

**`Interfaces/INumberingService.cs`**
```csharp
Task<PreviewResult> PreviewAsync(string targetType, string? categoryCode = null, CancellationToken ct = default);  // string? → PreviewResult
```
`GenerateAsync` 签名不变。

**`Infrastructure/Services/NumberingService.cs`** — `PreviewAsync` 实现:
- rule 为 null → 返回 `{ Code: null, IncludeCategory: false }`
- rule 存在 → 返回 `{ Code: <预览码 currentSeq+1 格式化>, IncludeCategory: rule.IncludeCategory }`
- 现有「传了 categoryCode 才校验分类存在性」逻辑保留(分类校验照旧,不依赖 includeCategory 字段)。

**`Controllers/NumberingController.cs`** — `Preview` 端点映射(一行):
```csharp
var r = await _numberingService.PreviewAsync(targetType, categoryCode, ct);
return Ok(new PreviewCodeResult { Code = r.Code, IncludeCategory = r.IncludeCategory });
```

### 5.2 消费侧 Service + DTO 透传 categoryCode

**`Dtos/System/ColorDtos.cs`** + **`Dtos/System/CustomerDtos.cs`** — 各加一个可选字段:
```csharp
public string? CategoryCode { get; init; }   // 可选;规则要求分类码时必填,否则引擎抛 DomainException
```

**`Services/ColorService.cs`** `CreateColorAsync`(:72):
```csharp
var code = await _numbering.GenerateAsync(NumberTargetTypes.Color, request.CategoryCode, ct);
```
**`Services/CustomerService.cs`** `CreateAsync`(:98):同理换 `request.CategoryCode`。

**Controller 层不动**(`ColorController`/`CustomersController` 的 `[FromBody] CreateXxxRequest`
自动绑定新字段)。

### 5.3 不改的部分(明确边界)

- **FluentValidation 的 CreateColorRequest/CreateCustomerRequest Validator**:不改。
  `CategoryCode` 可选;「规则要分类码但没传」的强校验由引擎 `GenerateAsync`(:47-48)负责并抛
  `DomainException`,Controller 全局异常过滤器已转 400 友好提示。在 validator 里重复校验需注入
  INumberingService,破坏分层。
- **`NumberingCategory` 实体 / Configuration / 迁移**:不动。
- **SeedData / Program.cs / NumberTargetTypes**:不动(守 §3.1/§3.5)。

### 5.4 后端单测

在 `backend/tests/OneCup.UnitTests/`(Numbering 相关测试目录)补:
1. `PreviewAsync` 返回 `IncludeCategory` 三例:无规则、`includeCategory=true`、`includeCategory=false`。
2. `ColorService.CreateColorAsync` 透传 categoryCode:mock `INumberingService`,
   验证 `GenerateAsync` 收到的第二参 == request 的 CategoryCode。

## 6. 前端详设

### 6.1 新增:`components/Numbering/useNumberingPreview.ts`

项目无 `hooks/` 目录(仅 Redux 一个 hook),故编号复用件放 `components/Numbering/`,贴近使用场景。

```ts
import { useState, useCallback } from 'react';
import { previewCode } from '@/api/numbering';
import { getActiveCategories, Category } from '@/api/numberingDictionary';

export function useNumberingPreview(targetType: string) {
  const [code, setCode] = useState<string | null>(null);
  const [codeLoading, setCodeLoading] = useState(false);
  const [noRule, setNoRule] = useState(false);
  const [includeCategory, setIncludeCategory] = useState(false);
  const [categoryOptions, setCategoryOptions] = useState<Category[]>([]);
  const [categoryCode, setCategoryCodeState] = useState<string | undefined>(undefined);

  // 表单打开新建时调用:首次预览 + 按需加载分类
  const reload = useCallback(() => {
    setCode(null); setNoRule(false); setCategoryCodeState(undefined);
    setCodeLoading(true);
    previewCode(targetType)
      .then((res) => {
        if (!res.code) { setNoRule(true); return; }
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
      .then((res) => setCode(res.code))   // 规则已在 reload 阶段确认存在
      .catch(() => setNoRule(true))
      .finally(() => setCodeLoading(false));
  }, [targetType]);

  return { code, codeLoading, noRule, includeCategory, categoryOptions, categoryCode, setCategoryCode, reload };
}
```

**契约要点**:
- `noRule` 为 true 时,调用方据此 `disabled` 表单 + Alert(守 c02)。
- `includeCategory` 由 preview 返回值驱动,调用方据此条件渲染 `<CategorySelect>`。
- 选分类的副作用(重预览)封在 hook 内,表单层只调 `setCategoryCode`。

### 6.2 新增:`components/Numbering/CategorySelect.tsx`

薄封装 Arco `<Select>`,props:`{ options: Category[]; value?: string;
onChange:(c?:string)=>void; loading?: boolean }`。option 显示 `${code} · ${nameZh}`,
value 用 code。封装成组件便于 material/process 复用且样式统一。

### 6.3 改造:`api/numbering.ts`

`previewCode` 返回类型加字段:
```ts
export function previewCode(targetType: string, categoryCode?: string) {
  return request.get<unknown, { code: string | null; includeCategory: boolean; note: string }>(
    '/api/numbering/preview', { params: { targetType, categoryCode } },
  );
}
```

### 6.4 改造:`api/color.ts` + `api/customer.ts`

`CreateColorRequest` / `CustomerFormData` 接口各加 `categoryCode?: string`。

### 6.5 改造消费端表单(2 个,模式相同)

**`pages/master-data/color/index.tsx`**(Drawer 型):
- 删本地 `previewedCode/codeLoading/noRule` 三段状态 → 改 `const preview = useNumberingPreview('color')`。
- `openCreate()`:删掉内联的 `previewCode('color')...` 块,改为 `preview.reload()`。
- 抽屉内:
  - 编号只读 Input 的 `value` 接 `preview.code`。
  - `includeCategory` 为真时,在编号字段下方插
    `<FormItem field="categoryCode" label={t['color.form.category']}><CategorySelect options={preview.categoryOptions} value={preview.categoryCode} onChange={preview.setCategoryCode} loading={preview.codeLoading} /></FormItem>`。
  - `okButtonProps={{ disabled: preview.noRule }}`、`Alert`/`Form disabled={preview.noRule}`(守 c02)。
- `handleDrawerOk` create 分支:提交 payload 并入 `{ ...values, categoryCode: preview.categoryCode }`。

**`pages/business/customer/form.tsx`**(Modal 型):同模式。结构对齐
(customer 用 `useEffect[visible,editing]` 触发,新建分支调 `preview.reload()`)。

**编辑模式**:`reload()` 不触发(编辑态编号是 record.code 已存值,无分类选择器)。

### 6.6 改造:模块级 locale(不动全局 locale/index.ts,守 §3.4)

`pages/master-data/color/locale/{zh-CN,en-US}.ts` +
`pages/business/customer/locale/{zh-CN,en-US}.ts` 各加:
- `xxx.form.category`:`分类` / `Category`
- `xxx.form.category.placeholder`:`请选择分类` / `Select category`

### 6.7 不改的部分

- 前端无单测框架(仅 `npm test` 预留),不强行加前端测试,以 `npm run build` 通过 + 手验为准。
- 不动 `routes.ts`/`router.tsx`/全局 `locale/index.ts`(分类码是 numbering 页内已有 Tab)。

## 7. 数据流(打通后)

```
打开新建颜色表单
  → useNumberingPreview('color').reload()
  → previewCode('color') → 后端 PreviewAsync 返回 {code:"CLR001", includeCategory:true}
  → hook 见 includeCategory=true → getActiveCategories('color') → [{code:"DARK",...},...]
  → 表单渲染分类下拉;用户选 "DARK"
  → setCategoryCode("DARK") → hook 内 previewCode('color','DARK') → code 刷新为 "CLR-DARK-001"
  → 提交 → createColor({...values, categoryCode:"DARK"})
  → ColorService.CreateColorAsync → GenerateAsync("color","DARK") → 落库正确分类码编号
```

无规则路径(rule 不存在)→ preview 返回 `{code:null,...}` → `noRule=true` → 表单 disabled + Alert(守 c02)。
规则不要分类路径(`IncludeCategory=false`)→ 不渲染下拉,行为与改造前一致(向后兼容)。

## 8. 风险

- **PreviewAsync 签名变更**(`string?` → `PreviewResult`)是后端 internal breaking change,
  但唯一调用方是 NumberingController 一个端点,影响面可控,一并改。
- **不改 schema** 意味着分类码选择是「运行时规则驱动」:只有用户在编号管理页把某 targetType
  的规则勾上「包含分类码」,对应表单才会出现分类下拉。规则未勾选的表单行为完全不变 → 向后兼容。
- **不新增 Guid/权限/菜单/迁移**,与任务①②完全并行无冲突,合并时走约定 v2 §6.2 第一个合入即可。

## 9. 验证标准

- `dotnet build backend/OneCup.sln` 通过;`dotnet test` 全绿(含新增 PreviewAsync / ColorService 单测)。
- `cd frontend && npm run build` 通过。
- 手验:在编号管理页给 color 配一条 `includeCategory=true` 的规则 + 建几个分类 →
  新建颜色表单出现分类下拉,选不同分类编号预览刷新,提交后编号正确。
- 手验:customer 默认规则 `includeCategory=false` → 客户表单不出现分类下拉,行为与改造前一致。

## 10. 改动文件清单

**后端(改 6 文件,无新增):**
- `backend/src/OneCup.Application/Dtos/System/NumberingDtos.cs`(加 PreviewResult、扩展 PreviewCodeResult)
- `backend/src/OneCup.Application/Interfaces/INumberingService.cs`(PreviewAsync 返回 PreviewResult)
- `backend/src/OneCup.Infrastructure/Services/NumberingService.cs`(PreviewAsync 实现)
- `backend/src/OneCup.Api/Controllers/NumberingController.cs`(Preview 端点映射)
- `backend/src/OneCup.Application/Dtos/System/ColorDtos.cs` + `CustomerDtos.cs`(加 CategoryCode)
- `backend/src/OneCup.Application/Services/ColorService.cs` + `CustomerService.cs`(透传 CategoryCode)
- `backend/tests/OneCup.UnitTests/...`(补单测)

**前端(新增 2、改 6):**
- 新增 `frontend/src/components/Numbering/useNumberingPreview.ts`
- 新增 `frontend/src/components/Numbering/CategorySelect.tsx`
- 改 `frontend/src/api/numbering.ts`、`api/color.ts`、`api/customer.ts`
- 改 `frontend/src/pages/master-data/color/index.tsx`、`pages/business/customer/form.tsx`
- 改 `pages/master-data/color/locale/{zh-CN,en-US}.ts`、`pages/business/customer/locale/{zh-CN,en-US}.ts`
