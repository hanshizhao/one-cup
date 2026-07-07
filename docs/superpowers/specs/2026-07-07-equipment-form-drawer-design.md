# 设备编辑表单：Modal → Drawer 设计

> 日期：2026-07-07
> 状态：已批准（待实现）
> 范围：仅设备（Equipment）新建/编辑表单的容器形态变更

---

## 1. 背景与决策

设备新建/编辑表单当前为 **560px 居中 Modal**，12 个字段垂直堆叠导致弹窗过高。Modal 居中弹窗形态下用户对纵向滚动有抗拒，体感不佳。

**决策：改为 480px 右侧 Drawer**，与设备详情抽屉同形态、同宽度、同位置。

### 1.1 动机

| 维度 | Modal（现状） | Drawer（目标） |
|---|---|---|
| 滚动预期 | 居中弹窗，用户抗拒滚动 | 抽屉，用户天然接受纵向滚动 |
| 与详情形态 | 不一致（详情 Drawer + 编辑 Modal） | 一致（都是 480px Drawer） |
| 列表上下文 | 遮挡列表 | 保留列表可见 |
| 字段量适配 | 偏挤 | 流式单列，舒适 |

**诚实代价**：偏离设计稿 `interaction-flow.html` 容器决策表「设备编辑用 Modal · 520px」的原文。但设计稿对**同样 12 字段的设备详情**判定用 Drawer 480px，编辑表单同理合理。属「理论与体感偏差」的修正。

### 1.2 明确不做的事

- 不改设备详情抽屉（仍只读 480px Drawer）
- 不改设备类型 / 模板表单（仍独立页）
- 不合并详情与编辑（保持两套界面：只读详情 + 可编辑表单）
- 不改列表页查询/删除逻辑、编号 hook（c02 流程不变）

---

## 2. 改动清单

| 文件 | 改动 |
|---|---|
| `equipment/EquipmentFormModal.tsx` → 重命名 `EquipmentFormDrawer.tsx` | 外壳 `Modal` → `Drawer`（width 480，footer 放取消/保存）；`layout="vertical"` → `layout="horizontal"`（左标签）；移除两列网格，所有字段纯单列；分组标题复用详情抽屉 `detail-group-title` |
| `equipment/EquipmentTab.tsx` | import 与 JSX 渲染处改名 `EquipmentFormDrawer` |
| `style/index.module.less` | 移除上次为 Modal 加的 `form-section-title` class（Drawer 统一用 `detail-group-title`） |

### 2.1 不需要改动的文件

- `locale/zh-CN.ts`、`en-US.ts`：`.form.title.create/edit` key 复用（Drawer title 同样适用）；分类码、无规则提示等 key 不变
- `api/equipment.ts`、`useNumberingPreview`、`CategorySelect`：c02 资产原样复用
- `router.tsx`：设备 create/edit 路由已在上一步移除，无需再改
- 设备详情抽屉 `EquipmentDetail.tsx`：不动

---

## 3. 表单布局（单列 · 左标签 · 4 分组）

```
┌── 480px Drawer ──────────────────┐
│ 新建设备                    [×]  │
├──────────────────────────────────┤
│ ▌基础信息                         │  ← detail-group-title
│   设备编码    [EQ-... 只读]        │
│   设备名称 *  [____________]       │
│   所属类型 *  [▼ 定型机]           │
│   编号分类码  [▼ ...]（条件渲染）   │
│ ▌运行状态                         │
│   运行状态 *  ○运行中 ○已停机 ○维护 │
│   启用状态    [Switch]             │
│   排序号      [0]                  │
│ ▌资产时间                         │
│   规格型号    [____________]       │
│   供应商      [____________]       │
│   安装位置    [____________]       │
│   购买日期    [📅]                 │
│   保修到期    [📅]                 │
│ ▌备注                             │
│   备注        [____________]       │
├──────────────────────────────────┤
│                    [取消]  [保存]  │  ← Drawer footer
└──────────────────────────────────┘
```

### 3.1 布局规范

- **标签对齐**：`Form layout="horizontal"`，标签左对齐；labelCol 宽度约 96–100px，与详情抽屉 Descriptions 的 label 列对齐
- **字段排列**：纯单列，所有字段独占一行（无两列并排）
- **分组标题**：复用 `detail-group-title`（加粗 14px + 下边距 12px），与详情抽屉完全一致
- **运行状态**：`Radio.Group direction="horizontal"`，三个选项横向一行
- **分组顺序**（与详情抽屉一致）：
  1. 基础信息（编码 / 名称 / 类型 / 分类码）
  2. 运行状态（状态 / 启用 / 排序）
  3. 资产时间（规格 / 供应商 / 位置 / 购买日期 / 保修到期）
  4. 备注

### 3.2 Drawer 属性

- `width={480}`（与详情抽屉同宽）
- `title`：编辑用 `equipment.item.form.title.edit`，新建用 `equipment.item.form.title.create`
- `footer`：取消 + 保存按钮；保存 `loading={confirmLoading}`，`disabled={!editing && preview.noRule}`
- `onCancel={onClose}`
- 无规则 / 错误 Alert 置顶（Drawer body 顶部）

### 3.3 c02 编号流程（不变）

- 设备编码：编辑显示实际 code，新建显示 `preview.code`（只读 Input）
- 分类码：`!editing && preview.includeCategory` 时条件渲染 `<CategorySelect>`
- 提交：`createEquipment({ ...values, categoryCode: preview.categoryCode })`，禁止 null 硬编码
- 无规则：`preview.noRule` 时禁用表单 + 顶部警告 Alert

---

## 4. 验收要点

1. 列表「新建设备」→ 右侧滑出 480px Drawer，单列左标签布局，分组清晰
2. 列表「编辑」→ Message.loading → Drawer 回填全部字段（含备注/日期）
3. 运行状态三个 Radio 横向一行不换行
4. 保存成功 → Drawer 关闭 + 列表刷新
5. Drawer 形态与详情抽屉视觉统一（同宽、同位、同分组标题样式）
6. c02 编号预览 / 分类码 / 无规则禁用全流程正常
7. `npm run build` 通过
