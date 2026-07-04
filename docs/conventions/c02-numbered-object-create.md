# c02 — 带"编号"的业务对象创建流程

> 一句话标准：用户永不手填编号；编号由系统预览后只读回填，无规则则禁用表单 + 提示。

## 1. 什么时候适用 / 什么时候不适用

**适用：**
- 任何业务对象，其"编号"字段由编号引擎生成：
  - 颜色（color）
  - 客户（customer）
  - 商品（product，未来）
  - 计量单位（measurement unit）
  - 其他任何带系统生成编号的对象

**不适用：**
- 用户自定义的自由字段（name、备注、描述等）
- 无编号概念的对象

## 2. 标准（固定流程）

打开"新建"表单时，按序执行：

1. 调用编号预览接口 `previewCode(targetType, categoryCode?)`（来自 `frontend/src/api/numbering.ts`，对应后端 `GET /api/numbering/preview`）。
2. 预览编号写入表单"编号"字段，设为 `readOnly`（不可编辑）。
3. 判断返回值：
   - **非空** → 编号字段只读展示预览值，表单其余字段正常可用，用户填写后提交。
   - **`null`** → 表单整体 `disabled`，顶部显示 `Alert`（`type="warning"`）提示：
     "未找到可用编号规则，请先配置编号规则后再新增"，提示中带跳转到编号规则配置的入口；
     同时确定按钮 `disabled`。
4. 提交成功后关闭表单、刷新列表。

## 3. 参考实现

- **主参考实例**：`frontend/src/pages/master-data/color/index.tsx`
  - 状态：`previewedCode` / `codeLoading` / `noRule`
  - `openCreate()`：调 `previewCode('color')`，`code` 为 null 或失败时 `setNoRule(true)`
  - 无规则时：`okButtonProps={{ disabled: noRule }}` + 顶部 `Alert` + `Form disabled={noRule}`
  - 编号字段：`Input readOnly value={previewedCode ?? undefined}`，占位符区分"预览中/无规则"

- **第二实例（同模式）**：`frontend/src/pages/business/customer/form.tsx`

- **预览接口签名**：

```ts
// frontend/src/api/numbering.ts
export function previewCode(targetType: string, categoryCode?: string)
  : Promise<{ code: string | null; note: string }>
```

## 4. 反模式（禁止这样做）

- ❌ 让用户手动输入编号
- ❌ 编号字段可编辑（即使用预览值，也不应允许修改）
- ❌ 预览失败时表单仍可用（用户填完才发现提交失败）
- ❌ 只灰掉表单，不提示原因（用户不知道为什么禁用）
- ❌ 不提供跳转到编号规则配置的入口（用户被卡住无处可去）
