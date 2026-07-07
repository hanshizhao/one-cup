# 设备类型表单页标准化 + 返回 bug 修复实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 修复设备类型表单页返回按钮跳错 Tab 的 bug，并按 Arco Pro 官方表单页范式重排表单布局。

**Architecture:** ①容器页 Tab 状态改为读写 URL query（`?tab=equipment|type`），表单页返回改为显式跳转带 tab 参数；②表单页重排为「面包屑+页头+双Card(各含Title)+底部操作栏」，备注改全宽 TextArea，底部操作栏用 sticky 定位（适配本系统动态侧边栏）。

**Tech Stack:** React + @arco-design/web-react、react-router-dom v6（useSearchParams）、TypeScript、CSS Modules (less)。

## Global Constraints

- Tab URL query 参数名固定 `tab`，取值 `equipment` | `type`，缺省 `equipment`
- 表单页返回显式跳转目标：`/business/equipment?tab=type`
- 栅格 `Grid.Row gutter={48}`，三列 `Col span={8}`（与 Arco 官方 form/group 同构，gutter 取 48 而非官方 80）
- 双 Card 每个内含 `<Typography.Title heading={6}>` 分组标题
- 备注字段全宽 `Input.TextArea`，独立成行，不进三列网格
- 底部操作栏用 `position: sticky; bottom: 0`（**不用 fixed**），因本系统侧边栏宽度动态可变（collapsed?48:220），fixed 的 left:0 会遮挡侧边栏；sticky 相对内容滚动容器，自动避开侧边栏
- 底部操作栏含取消 + 保存两个按钮，右对齐（`flex-direction: row-reverse` 或 `justify-content: flex-end`）
- c02 编号流程不变：编号只读 Input、分类码 `!editing && preview.includeCategory` 条件渲染、提交透传 `preview.categoryCode`、`preview.noRule` 时禁用表单+顶部 Alert+保存按钮 disabled
- 项目前端无单测（设计文档 8.3 节），验证手段为 `npm run build` 编译 + 人工核对验收要点
- ParameterEditor 组件（参数定义卡片编辑器）本次不动，只改它外层 Card 标题

参考文档：`docs/superpowers/specs/2026-07-07-equipment-type-form-standardize-design.md`

---

## Task 1: 容器页 Tab 状态 URL 持久化

把 EquipmentPage 的 Tab 选中状态从纯 useState 改为读写 URL query，让刷新/返回都能恢复正确 Tab。独立可编译可验证，是返回 bug 修复的前置依赖。

**Files:**
- Modify: `frontend/src/pages/business/equipment/index.tsx`

**Interfaces:**
- Consumes: `useSearchParams` from `react-router-dom`（v6，项目已装 `react-router-dom@^6.26.0`）
- Produces: 容器页在 URL `?tab=equipment|type` 驱动下渲染对应 Tab；切换 Tab 时同步更新 URL

- [ ] **Step 1: 改 index.tsx 的 Tab 状态实现**

当前 `frontend/src/pages/business/equipment/index.tsx` 完整内容如下（21 行附近）：

```tsx
export default function EquipmentPage() {
  const t = useLocale(locale);
  const [activeTab, setActiveTab] = useState('equipment');
  // ... 其余不变
```

将其中的 Tab 状态部分（`const [activeTab, setActiveTab] = useState('equipment');`）改为读写 URL query。同时 import 区加入 `useSearchParams`。

import 改动——将第 1 行：
```tsx
import { useState } from 'react';
```
改为：
```tsx
import { useState } from 'react';
import { useSearchParams } from 'react-router-dom';
```

组件内 Tab 状态——将：
```tsx
  const [activeTab, setActiveTab] = useState('equipment');
```
改为：
```tsx
  // Tab 状态持久化到 URL query（?tab=equipment|type），刷新/返回可恢复
  const [searchParams, setSearchParams] = useSearchParams();
  const activeTab = searchParams.get('tab') === 'type' ? 'type' : 'equipment';
  const setActiveTab = (key: string) => {
    setSearchParams({ tab: key }, { replace: true });
  };
```

> 说明：用 `replace: true` 避免每次切 Tab 都在历史栈堆积记录。`get('tab')` 缺省时回落 `'equipment'`。

- [ ] **Step 2: 编译验证**

Run: `cd frontend && npm run build`
Expected: `✓ built in <N>s`，无报错。

- [ ] **Step 3: 提交**

```bash
git add frontend/src/pages/business/equipment/index.tsx
git commit -m "refactor(equipment): Tab 状态持久化到 URL query（?tab=）"
```

---

## Task 2: 表单页返回 bug 修复 + 按官方标准重排

这是核心任务。修复 `navigate(-1)` 为显式跳转，同时按 Arco 官方 form/group 范式重排整个表单页：页头去掉操作按钮、双 Card 加 Title、备注改全宽 TextArea、底部加 sticky 操作栏、gutter 24→48。

**Files:**
- Modify: `frontend/src/pages/business/equipment/type/TypeFormPage.tsx`（整体重排）
- Modify: `frontend/src/pages/business/equipment/style/index.module.less`（新增底部操作栏样式）

**Interfaces:**
- Consumes: Task 1 的 `?tab=type` query 约定（返回跳转目标）
- Produces: 标准化的设备类型表单页，结构与 Arco form/group 一致

- [ ] **Step 1: 新增底部操作栏样式**

在 `frontend/src/pages/business/equipment/style/index.module.less` 中，找到 `.form-page-card { margin-bottom: 16px; }` 这一行，在其**之后**插入底部操作栏样式。

将：
```less
.form-page-card {
  margin-bottom: 16px;
}
```
改为：
```less
.form-page-card {
  margin-bottom: 16px;
}

// ── 表单页底部操作栏（sticky，适配动态侧边栏，参照 Arco form/group）──
.form-page-actions-bar {
  padding: 12px 0;
  background: var(--color-bg-2);
  display: flex;
  justify-content: flex-end;
  gap: 8px;
  position: sticky;
  bottom: 0;
  z-index: 10;
  margin-top: 16px;
}
```

> 说明：用 `sticky; bottom: 0` 而非 Arco 官方的 `fixed; left:0; right:0`，因本系统侧边栏宽度动态可变（layout.tsx:87 `collapsed ? 48 : 220`），fixed 的 left:0 会遮挡侧边栏。sticky 相对内容滚动容器定位，自动避开侧边栏。

- [ ] **Step 2: 重排 TypeFormPage.tsx**

将 `frontend/src/pages/business/equipment/type/TypeFormPage.tsx` 中从 `return (` 开始到文件结尾（即整个 JSX return 块，约第 125-242 行）替换为下面的标准化版本。

**先处理 import**——当前文件第 1-2 行：
```tsx
import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
```
保持不变（`useNavigate` 仍需要，只是用法改）。

**替换整个 return 块**（从 `return (` 到文件末尾的 `}`）为：

```tsx
  return (
    <div>
      {/* 页内面包屑 */}
      <Breadcrumb style={{ marginBottom: 12 }}>
        <Breadcrumb.Item>{t['equipment.tab.type']}</Breadcrumb.Item>
        <Breadcrumb.Item>{pageTitle}</Breadcrumb.Item>
      </Breadcrumb>

      {/* 页头：标题 + 副标题（操作按钮移到底部栏）*/}
      <div className={styles['form-page-head']}>
        <div>
          <Title heading={5} style={{ marginBottom: 4 }}>
            {pageTitle}
            {code && <span className={styles['form-page-code']}>{code}</span>}
          </Title>
          <div className={styles['form-page-sub']}>
            {editing
              ? t['equipment.type.form.page.sub.edit']
              : t['equipment.type.form.page.sub.create']}
          </div>
        </div>
      </div>

      {!editing && preview.noRule && (
        <Alert
          type="warning"
          content={t['equipment.type.form.noRule.block']}
          style={{ marginBottom: 16 }}
        />
      )}
      {errorMsg && <Alert type="error" content={errorMsg} style={{ marginBottom: 16 }} />}

      <Form
        form={form}
        layout="vertical"
        disabled={!editing && preview.noRule}
      >
        {/* Card ① 基础信息 */}
        <Card className={styles['form-page-card']}>
          <Title heading={6} style={{ marginTop: 0 }}>
            {t['equipment.type.detail.baseInfo']}
          </Title>
          <Row gutter={48}>
            <Col span={8}>
              <FormItem label={t['equipment.type.form.code']}>
                <Input
                  value={(editing ? code : preview.code) ?? undefined}
                  readOnly
                  placeholder={
                    preview.codeLoading
                      ? t['equipment.type.form.code.previewing']
                      : t['equipment.type.form.code.placeholder']
                  }
                />
              </FormItem>
            </Col>
            <Col span={8}>
              <FormItem label={t['equipment.type.form.name']} field="name" rules={[{ required: true }]}>
                <Input maxLength={50} />
              </FormItem>
            </Col>
            <Col span={8}>
              {!editing && preview.includeCategory && (
                <FormItem
                  label={t['equipment.type.form.categoryCode']}
                  field="categoryCode"
                  rules={[{ required: true }]}
                >
                  <CategorySelect
                    options={preview.categoryOptions}
                    value={preview.categoryCode}
                    onChange={preview.setCategoryCode}
                    loading={preview.codeLoading}
                    placeholder={t['equipment.type.form.categoryCode.placeholder']}
                  />
                </FormItem>
              )}
            </Col>
          </Row>
          <Row gutter={48}>
            <Col span={8}>
              <FormItem label={t['equipment.type.form.sortOrder']} field="sortOrder" initialValue={0}>
                <InputNumber min={0} style={{ width: '100%' }} />
              </FormItem>
            </Col>
            <Col span={8}>
              <FormItem label={t['equipment.type.form.isActive']} field="isActive" triggerPropName="checked">
                <Switch />
              </FormItem>
            </Col>
          </Row>
          {/* 备注：全宽 TextArea，独立成行（不进三列网格） */}
          <FormItem label={t['equipment.type.form.remark']} field="remark">
            <TextArea maxLength={500} showWordLimit />
          </FormItem>
        </Card>

        {/* Card ② 参数定义 */}
        <Card className={styles['form-page-card']}>
          <Title heading={6} style={{ marginTop: 0 }}>
            {t['equipment.type.form.parameters']}
          </Title>
          <FormItem>
            <ParameterEditor value={parameters} onChange={setParameters} />
          </FormItem>
        </Card>
      </Form>

      {/* 底部操作栏（sticky，右对齐 取消/保存） */}
      <div className={styles['form-page-actions-bar']}>
        <Button onClick={() => navigate('/business/equipment?tab=type')} style={{ marginRight: 8 }}>
          {t['equipment.type.form.page.cancel']}
        </Button>
        <Button
          type="primary"
          loading={confirmLoading}
          disabled={!editing && preview.noRule}
          onClick={handleSave}
        >
          {t['equipment.type.form.page.save']}
        </Button>
      </div>
    </div>
  );
}
```

**关键变化点（供 implementer 核对）：**
1. `handleSave` 成功后的 `navigate(-1)`（第 104 行）改为 `navigate('/business/equipment?tab=type')`
2. 取消按钮的 `navigate(-1)`（原第 147 行）改为 `navigate('/business/equipment?tab=type')`
3. 页头 `form-page-head` div 去掉了 `form-page-actions` 子 div（操作按钮移到底部）
4. 基础信息 Card 内加了 `<Title heading={6}>` 分组标题（用 locale key `equipment.type.detail.baseInfo`）
5. 参数定义 Card 从 `title={...}` 属性改为 Card 内 `<Title heading={6}>`（用 `equipment.type.form.parameters`）
6. 栅格 `gutter={24}` → `gutter={48}`（两处 Row）
7. 备注从「三列网格一格的 Input」改为「全宽 TextArea 独立成行」，移除了备注原来所在的 Col 包装
8. 底部新增 `form-page-actions-bar` div，含取消+保存按钮

注意：`handleSave` 函数体内的 `navigate(-1)` 改动不在上面的 return 块里，需单独改。找到 `handleSave` 函数中（约第 104 行）的：
```tsx
      navigate(-1);
```
改为：
```tsx
      navigate('/business/equipment?tab=type');
```

- [ ] **Step 3: 编译验证**

Run: `cd frontend && npm run build`
Expected: `✓ built in <N>s`，无报错。若报 locale key 缺失（如 `equipment.type.detail.baseInfo`），到 `frontend/src/pages/business/equipment/locale/zh-CN.ts` 和 `en-US.ts` 确认该 key 存在（应已存在，详情抽屉在用）。

- [ ] **Step 4: 提交**

```bash
git add frontend/src/pages/business/equipment/type/TypeFormPage.tsx \
        frontend/src/pages/business/equipment/style/index.module.less
git commit -m "refactor(equipment): 设备类型表单页标准化（官方范式 + 返回bug修复）"
```

---

## Task 3: 人工核对验收要点

项目前端无单测，最终验证靠人工核对 spec 的 9 条验收要点。

**Files:** 无（验证任务）

- [ ] **Step 1: 启动 dev 服务器**

Run: `cd frontend && npm run dev`

- [ ] **Step 2: 逐条核对验收要点**

登录后进「业务 / 设备管理」，逐条核对：

1. 切到「设备类型」Tab → URL 变为 `?tab=type`；切回「设备」→ `?tab=equipment`
2. 设备类型列表点「新建类型」→ 进入表单页 → 点「取消」→ **回到设备类型 Tab**（不是设备 Tab）
3. 编辑模式保存后 → **回到设备类型 Tab**
4. 浏览器刷新 `?tab=type` → 仍停在设备类型 Tab
5. 表单页布局：面包屑 + 页头（标题+副标题，**无操作按钮**）+ 双 Card（各有 Title heading=6）+ 底部操作栏
6. 备注是**全宽 TextArea**（带字数统计），不再挤网格一格
7. 栅格 gutter=48，三列对齐
8. 底部操作栏：随内容滚动到底部时贴底（sticky），取消/保存右对齐，保存 disabled 条件同现状（无编号规则时禁用）
9. 参数定义编辑器（ParameterEditor）功能正常：增删行、拖拽排序、类型切换、校验提示
10. c02 编号预览 / 分类码条件渲染 / 无规则禁用 全流程正常

- [ ] **Step 3: 标记完成**

验收全过后本计划完成。无需提交。

---

## Self-Review 备注

- **Spec 覆盖**：spec §2.1（Tab query 持久化）→ Task 1；spec §2.2（表单重排）+§2.1（返回跳转）→ Task 2；spec §5（验收）→ Task 3。全覆盖。
- **类型一致**：`setSearchParams({ tab: key }, { replace: true })` 签名符合 react-router-dom v6；`navigate('/business/equipment?tab=type')` 字符串路径符合 useNavigate。
- **无占位符**：所有步骤含完整代码。
- **偏离 spec 之处（已主动决策）**：spec §3.2 写的是 Arco 官方 `fixed; left:0; right:0`，但本系统侧边栏动态宽度会让 fixed 遮挡侧边栏。计划改用 `sticky; bottom:0`，更适配。这一偏离已在 Task 2 Step 1 的说明里讲清楚，是 spec 落地时的合理修正。
