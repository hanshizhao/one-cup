# c02 — 带编号的业务对象创建流程

> 一句话标准：用户永不手填编号；编号由系统预览后只读回填，无规则则禁用表单 + 提示；
> 规则开启分类码段时，表单条件渲染分类码选择器，选中后透传到后端编号引擎。

## 1. 什么时候适用 / 什么时候不适用

**适用：**
- 任何业务对象，其"编号"字段由编号引擎生成（颜色 color / 客户 customer / 物料 material /
  工序 process / 商品 product / 设备 equipment / 未来任何带系统生成编号的对象）。
- 编号规则是否开启"分类码段"（`IncludeCategory`）皆适用——选择器是条件渲染的，
  规则没开就自动隐藏，不影响简单场景。

**不适用：**
- 用户自定义的自由字段（name、备注、描述等）。
- 无编号概念的对象。
- **业务分类字段**（如物料的"原料类别"、工序的"工序分类"）——它们是自由文本/枚举，
  与本标准的"编号分类码"是不同概念，共存而非替换（见 4.反模式最后一条）。

## 2. 标准（固定流程）

带编号对象的创建是**前端表单 + 后端引擎**的一条完整链路，两边都要按标准做，
否则分类码透传不到引擎（这是并行开发最易漏的一环）。

### 2.1 前端表单（6 步，照抄 customer/material 模式）

打开"新建"表单时，按序：

1. **挂载可复用 hook**：`const preview = useNumberingPreview(targetType);`
   （`targetType` 与 `NumberTargetTypes.cs` 常量一致，如 `'material'`）。
   表单打开新建分支时调 `preview.reload()`（首次预览 + 按需加载分类列表）。

2. **编号字段只读回填**：
   ```tsx
   <Input value={(editing ? editing.code : preview.code) ?? undefined} readOnly
     placeholder={preview.codeLoading ? '编号预览中…' : '创建时自动生成'} />
   ```

3. **无规则守门**（`preview.noRule === true`）→ 表单整体 `disabled` + 顶部 `Alert(type="warning")`
   提示"未配置编号规则"（带跳转编号管理的入口）+ 确定按钮 `disabled`。

4. **分类码选择器（规则驱动条件渲染）**：仅当 `!editing && preview.includeCategory` 时渲染：
   ```tsx
   {!editing && preview.includeCategory && (
     <FormItem label="编号分类码" field="categoryCode" rules={[{ required: true }]}>
       <CategorySelect options={preview.categoryOptions} value={preview.categoryCode}
         onChange={preview.setCategoryCode} loading={preview.codeLoading}
         placeholder="请选择分类码" />
     </FormItem>
   )}
   ```
   选分类 → hook 内部自动重新 `previewCode(targetType, c)` 刷新编号预览（带分类码段）。

5. **CategorySelect 加 required 校验**（项目统一约定，比靠后端报错更友好）。

6. **提交时透传 categoryCode**（**从 preview 取，不从 form 取**）：
   ```ts
   await createXxx({ ...values, categoryCode: preview.categoryCode });
   ```

### 2.2 后端必做（2 步——这是最容易漏的一环）

1. **`CreateXxxRequest` DTO 加可选字段**：
   ```csharp
   /// <summary>可选；编号规则要求分类码时必填，由引擎强校验。</summary>
   public string? CategoryCode { get; init; }
   ```
   （`UpdateXxxRequest` **不加**——编号创建后不可改，编辑无需 categoryCode。）

2. **`XxxService.CreateAsync` 透传给编号引擎**（事务内）：
   ```csharp
   var code = await _numbering.GenerateAsync(NumberTargetTypes.Xxx, request.CategoryCode, ct);
   ```
   ⚠️ **禁止硬编码 `null`**——物料/工序并行开发时就是因为写了 `GenerateAsync(..., null, ct)`
   导致前端即使接了选择器，分类码也透传不到引擎。

> **不需要做的**：`categoryCode` 不持久化到实体（编号拼码已含分类段），无需加 EF 字段、
> 无需加 Validator 规则（引擎在 `GenerateAsync` 内强校验缺失/非法分类码）。

## 3. 参考实现

### 3.1 前端参考实例（按表单形式选用）

| 表单形式 | 主参考文件 | 说明 |
| --- | --- | --- |
| Modal 子组件 | `frontend/src/pages/business/customer/form.tsx` | 最完整范例，新建/编辑共用一个 Modal |
| Drawer 内 form | `frontend/src/pages/master-data/color/index.tsx` | 页面内 Drawer 形式 |
| 业务 Category 共存 | `frontend/src/pages/business/material/form.tsx` | 同时有"业务类别"和"编号分类码"两个字段的范例 |

### 3.2 后端参考实例

- DTO：`backend/src/OneCup.Application/Dtos/System/ColorDtos.cs` 的 `CreateColorRequest.CategoryCode`
- Service：`backend/src/OneCup.Application/Services/ColorService.cs` 的 `CreateColorAsync`
  （事务内 `GenerateAsync(NumberTargetTypes.Color, request.CategoryCode, ct)`）

### 3.3 可复用资产契约（直接 import 即用）

**`useNumberingPreview(targetType)` hook**
（`frontend/src/components/Numbering/useNumberingPreview.ts`）

| 返回字段 | 类型 | 用途 |
| --- | --- | --- |
| `code` | `string \| null` | 预览编号；null=无规则/预览中 |
| `codeLoading` | `boolean` | 编号预览 loading |
| `noRule` | `boolean` | true → 禁表单 + Alert |
| `includeCategory` | `boolean` | 规则是否要求分类码 → 决定是否渲染 CategorySelect |
| `categoryOptions` | `Category[]` | 分类下拉选项（includeCategory=true 时才加载） |
| `categoryCode` | `string \| undefined` | 当前选中分类码（提交时透传给后端） |
| `setCategoryCode(c?)` | function | 选分类，hook 内自动重新预览刷新编号 |
| `reload()` | function | 表单打开新建时调用：重置 + 首次预览 + 按需加载分类 |

**`<CategorySelect>` 组件**（`frontend/src/components/Numbering/CategorySelect.tsx`）
- props：`{ options: Category[]; value?: string; onChange?: (code?) => void; loading?; placeholder? }`
- option 显示「code · 中文名」，value 用 code。

**`previewCode` 接口签名**（`frontend/src/api/numbering.ts`）
```ts
previewCode(targetType: string, categoryCode?: string)
  : Promise<{ code: string | null; includeCategory: boolean; note: string }>
```
⚠️ **不要在前端表单里直接调 `previewCode`**——已经被 hook 封装，直调会漏掉
`includeCategory` 处理和分类码重选重预览逻辑。

## 4. 反模式（禁止这样做）

**编号生成相关：**
- ❌ 让用户手动输入编号
- ❌ 编号字段可编辑（即使用预览值，也不应允许修改）
- ❌ 预览失败/无规则时表单仍可用（用户填完才发现提交失败）
- ❌ 只灰掉表单，不提示原因（用户不知道为什么禁用）
- ❌ 不提供跳转到编号规则配置的入口（用户被卡住无处可去）

**分类码选择相关（本次升级新增）：**
- ❌ 前端直接调 `previewCode` API + 手写 `previewedCode/codeLoading/noRule` 三件套 state
  （已被 `useNumberingPreview` hook 取代，手写会漏 `includeCategory` 处理和分类码重选重预览）
- ❌ 后端 `GenerateAsync` 硬编码 `null`（即使前端接了分类码选择器也透传不到引擎）
- ❌ `CreateXxxRequest` DTO 缺 `CategoryCode` 字段（前端选了分类码后端接不住）
- ❌ 把业务 Category 字段和编号 categoryCode 混为一谈。它们是不同语义：
  - 业务 Category（如物料"原料类别"、工序"工序分类"）：自由文本/枚举，独立存在
  - 编号 categoryCode：编号字典里的分类码，由规则决定是否拼进编号
  - 两者共存于同一表单时，文案要区分清楚（如"类别" vs "编号分类码"）

## 5. 与并行开发的协作（重要经验）

编号模块（`useNumberingPreview` / `CategorySelect` / `previewCode` / `INumberingService`）
会随业务演进而变更签名。新实体若与编号模块的变更**并行开发**，合并时会撞"语义冲突"
（git 检测不到，只有编译能发现）。

**血泪案例**：编号分类码优化任务把 `INumberingService.PreviewAsync` 返回类型从
`Task<string?>` 改成 `Task<PreviewResult>`；同期并行开发的物料、工序分支的测试替身
（`FakeNumberingService`）还用旧签名。合并后两次都编译失败（`CS0738` 接口返回类型不匹配）。

**因此并行开发带编号的新实体时：**
1. 测试替身（实现 `INumberingService` 的 Fake）尽量少写——能用 mock 就别手实现接口。
2. 实在要写 Fake，**合并后必须跑 `dotnet build` 验证编译**，单靠 git 无冲突不能保证语义一致。
3. 关注 `useNumberingPreview` / `previewCode` 的签名，它们也可能变。新实体表单**优先用 hook**
   而非直调 API，这样 hook 升级时只需改一处。
