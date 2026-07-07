# 设备类型表单页标准化 + 返回 bug 修复设计

> 日期：2026-07-07
> 状态：已批准（待实现）
> 范围：设备类型（EquipmentType）新建/编辑表单页

---

## 1. 问题与目标

当前设备类型新建/编辑表单页（`TypeFormPage.tsx`）有两个问题：

### 1.1 返回按钮跳错 Tab（bug）

**现象**：在设备类型列表点「新建类型」进入表单页，点「返回」/「取消」后，回到的是设备管理页的「设备」Tab，而非「设备类型」Tab。

**根因**：返回用的是 `navigate(-1)`（回退浏览器历史）。但设备/设备类型双 Tab 的选中状态只存在容器页 `EquipmentPage` 的 `useState` 里，不随 URL 持久化。回退后容器页重新挂载，`useState` 默认值 `'equipment'` 让 Tab 停在「设备」。

### 1.2 表单布局不符合官方标准

当前表单页是手写布局，与 Arco Design Pro 官方表单页范式（`form/group`）不一致：
- 操作按钮挤在页头右上角（官方是底部 fixed 操作栏）
- 备注字段被塞进三列网格一格，且是单行 `Input`（备注应全宽 TextArea）
- Card 分组层级不明确（缺少 Card 内 `Typography.Title heading={6}` 分组标题）

**目标**：参照 Arco Pro `form/group` 官方范式重排，形成本系统表单页标准。

---

## 2. 解决方案

### 2.1 返回 bug 修复：Tab 状态 URL 持久化 + 显式跳转

**机制**：
- 容器页 `EquipmentPage`（`index.tsx`）：Tab 的 `activeTab` 改为读写 URL query 参数 `?tab=equipment|type`
  - 初始值：从 `useSearchParams` 读 `tab`，缺省 `'equipment'`
  - `onChange`：同步写回 query（用 `setSearchParams`）
- 表单页 `TypeFormPage.tsx`：`navigate(-1)` 改为 `navigate('/business/equipment?tab=type')`

**收益**：无论从哪进、刷新与否，「设备类型」Tab 都能可靠恢复。附带让设备类型详情、模板等所有跳转都能用 `?tab=type` 精确回退。

### 2.2 表单页按 Arco 官方标准重排

对照 Arco Pro `form/group` 范式：

| 维度 | 现状 | 改为（官方标准） |
|---|---|---|
| 整体结构 | 自定义 `form-page-head` + Card | 面包屑 + 页头 + 多 Card 分组 + 底部 fixed 操作栏 |
| 分组方式 | 基础信息 Card（无内标题）+ 参数定义 Card（有 title） | 两个 Card，每个内含 `Typography.Title heading={6}` 分组标题 |
| 栅格 | `Grid.Row gutter={24}` 三列 | 三列不变，`gutter={48}`（官方 80 偏宽，48 协调） |
| 备注字段 | 三列网格一格 + 单行 `Input` | **独立全宽行** + `TextArea` |
| 操作按钮 | 页头右上角 | **底部 fixed 操作栏**（白底 + 上阴影，右对齐 取消/保存） |
| 页头 | 自定义 div 含操作按钮 | 面包屑 + 标题 + 副标题（操作按钮移到底部栏） |

**布局示意**：
```
面包屑：设备类型 / 新建设备类型
─────────────────────────────────
[页头] 新建设备类型  EQT-...（编号）
       编号由系统生成 · 参数定义随类型整表提交
─────────────────────────────────
┌ Card ① ─ 基础信息（Title heading=6）─┐
│ [编码]  [名称]  [分类码]              │  ← 三列 gutter=48
│ [排序]  [启用]  (空)                  │
│ 备注：[________全宽 TextArea_______]  │
└───────────────────────────────────────┘
┌ Card ② ─ 参数定义（Title heading=6）─┐
│ (ParameterEditor 卡片列表)            │
│ [+ 添加参数]                          │
└───────────────────────────────────────┘
─────────────────────────────────
[底部 fixed 栏]          [取消] [保存]
```

---

## 3. 改动清单

| 文件 | 改动 |
|---|---|
| `equipment/index.tsx` | Tab 状态读写 URL query：`useState` → `useSearchParams` 读 `tab`；`onChange` 同步写回 |
| `type/TypeFormPage.tsx` | ① `navigate(-1)` → `navigate('/business/equipment?tab=type')`（2 处：保存后、取消按钮）；② 表单按官方标准重排：页头去掉操作按钮、双 Card 加 `Typography.Title heading={6}`、备注改全宽 TextArea、底部加 fixed 操作栏；③ `gutter` 24→48 |
| `style/index.module.less` | 新增 `.form-page-actions-bar` fixed 操作栏样式（白底 + 上阴影 + 右对齐） |

### 3.1 不需要改动的文件

- `ParameterEditor.tsx`（参数定义卡片编辑器，屏1已精装修）—— 不动，只改它外层 Card 的标题层级
- `api/equipment.ts`、`useNumberingPreview`、`CategorySelect` —— c02 资产原样复用
- `router.tsx` —— 设备类型 create/edit 路由保留（仍是独立页）
- 设备表单 Drawer、设备详情抽屉 —— 不动

### 3.2 底部操作栏样式（参照 Arco 官方 form/group/style）

```less
.form-page-actions-bar {
  padding: 12px 40px;
  background: var(--color-bg-2);
  display: flex;
  flex-direction: row-reverse;
  position: fixed;
  left: 0;
  right: 0;
  bottom: 0;
  box-shadow: 0 -3px 12px rgb(0 0 0 / 10%);
  z-index: 10;
}
```

> 注意：fixed 定位的 `left:0; right:0` 会横跨整个视口。本系统侧边栏宽度由 layout 控制，操作栏 fixed 在视口底部右对齐即可（Arco 官方同款做法）。若与侧边栏有遮挡，实现时再调 left 偏移。

---

## 4. c02 编号流程（不变）

- 类型编号：编辑显示实际 code，新建显示 `preview.code`（只读 Input）
- 分类码：`!editing && preview.includeCategory` 时条件渲染 `<CategorySelect>`
- 提交：`createEquipmentType({ ...payload, categoryCode: preview.categoryCode })`
- 无规则：`preview.noRule` 时禁用表单 + 顶部警告 Alert + 保存按钮 disabled

---

## 5. 验收要点

1. 设备类型列表点「新建类型」→ 进入表单页 → 点「取消」/保存后 → **回到设备类型 Tab**（不是设备 Tab）
2. 手动刷新 `/business/equipment?tab=type` → 仍停在设备类型 Tab
3. 表单页布局：面包屑 + 页头（标题+副标题，无操作按钮）+ 双 Card（各有 Title heading=6）+ 底部 fixed 操作栏
4. 备注是**全宽 TextArea**，不再挤在网格一格
5. 栅格 gutter=48，三列对齐
6. 底部操作栏：白底 + 上阴影，取消/保存右对齐，保存 disabled 条件同现状
7. 参数定义编辑器（ParameterEditor）功能正常，未受外层重排影响
8. c02 编号预览 / 分类码 / 无规则禁用全流程正常
9. `npm run build` 通过
