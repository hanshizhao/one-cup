# 设备类型表单页左右分栏布局实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把设备类型表单页改为左右分栏（基础信息左 35% + 参数定义右 65%），并删除参数编辑器的「在模板里长什么样」预览面板。

**Architecture:** Task 1 先把 ParameterEditor 从左右双栏（卡片+预览）精简为单列（仅卡片列表），删除预览相关 state/逻辑/JSX/样式；Task 2 再把 TypeFormPage 的两个 Card（基础信息 + 参数定义）包进新的左右分栏容器，基础信息内部从 3 列网格改为顶标签单列。

**Tech Stack:** React + @arco-design/web-react、TypeScript、CSS Modules (less)。

## Global Constraints

- 左右分栏宽度比例：左 35% : 右 65%，gap 16px
- 左栏（基础信息）内部：`Form layout="vertical"`（顶标签），6 字段单列无网格
- 右栏（参数定义）：参数卡片占满栏宽
- 两侧顶部对齐（`align-items: flex-start`）
- 窄屏降级：`@media (max-width: 1199px)` 时左右堆叠为上下
- 底部 sticky 操作栏保持不变（不改 `form-page-actions-bar`）
- 删除预览面板时，locale 里的 `equipment.type.param.preview.*` key **保留**（不删 locale，仅删代码引用）
- 参数卡片的拖拽/类型切换/必填/校验/统计条功能全部保留
- c02 编号流程不变
- 项目前端无单测（设计文档 8.3），验证手段为 `npm run build` + 人工核对
- `npm` 可用，`yarn` 不可用

参考文档：`docs/superpowers/specs/2026-07-07-equipment-type-form-layout-design.md`

---

## Task 1: ParameterEditor 删除预览面板

把 ParameterEditor 从「左卡片列表 + 右预览面板」的双栏，精简为「单列卡片列表」。删除预览相关的 state、逻辑、JSX 和样式。独立可编译可验证。

**Files:**
- Modify: `frontend/src/pages/business/equipment/type/ParameterEditor.tsx`
- Modify: `frontend/src/pages/business/equipment/style/index.module.less`（清理预览样式）

**Interfaces:**
- Consumes: 无新依赖
- Produces: ParameterEditor 组件对外接口（`{ value, onChange, unitOptions? }`）不变；内部布局从双栏变单列

**背景：预览面板相关的代码标识（供 implementer 精确识别删除范围）**

ParameterEditor.tsx 中与预览相关的、需删除的代码：
1. `selectedKey` state（`useState<string | null>(null)`）—— 仅为预览跟踪选中参数
2. `selectedParam` useMemo —— 仅为预览计算当前选中参数
3. `handleSelect` 函数中的选中逻辑（`__add__` 分支保留，选中分支删除）
4. ParamCard 的 `selected` / `onSelect` props 及其在 SortableList/ParamCard 的传递链
5. 卡片的 `onClick` 选中逻辑（ParamCard 内 `onSelect()` 调用）
6. 整个右栏 `<div className={styles['preview-panel']}>...</div>` JSX 块
7. `editor-layout` flex 容器 + `param-list-col` 包裹（改为直接单列）

style/index.module.less 中需清理的 class（预览删除后无用）：
- `.editor-layout`（第 175 行）、`.param-list-col`（第 181 行）
- `.preview-panel`（第 397 行）及内部 `.preview-head`/`.preview-head-title`/`.preview-head-sub`/`.preview-body`/`.preview-mock-label`/`.preview-hint`/`.preview-foot`（第 397-446 行整块）
- `.param-card` 的 `&.is-selected`（第 235 行）选中态样式

- [ ] **Step 1: 删除 ParameterEditor.tsx 的预览相关代码**

对 `frontend/src/pages/business/equipment/type/ParameterEditor.tsx` 做以下删除（保留其余所有功能）：

**1a. 删除 `selectedKey` state**（找到并删除这一行）：
```tsx
  const [selectedKey, setSelectedKey] = useState<string | null>(null);
```

**1b. 删除 `selectedParam` useMemo**（整块删除）：
```tsx
  // 选中的参数（用于右栏预览）
  const selectedParam = useMemo(() => {
    if (!selectedKey) return value[0] || null;
    return value.find((p) => (p.id || `${p.name}-${p.sortOrder}`) === selectedKey) || value[0] || null;
  }, [value, selectedKey]);
```

**1c. 简化 `handleSelect`**——只保留 `__add__` 分支（添加参数），删除选中分支：
将：
```tsx
  const handleSelect = (id: string) => {
    if (id === '__add__') {
      addRow();
      return;
    }
    setSelectedKey(id);
  };
```
改为（注意：handleSelect 现在只处理添加，可重命名为更清晰的名称，但为减少改动量保留函数名）：
```tsx
  const handleSelect = (id: string) => {
    if (id === '__add__') {
      addRow();
    }
  };
```

**1d. 删除 `addRow` 中的 `setSelectedKey` 调用**——找到 addRow 函数，删除其中这一行：
```tsx
    setSelectedKey(`-${value.length + 1}-`);
```
（保留 addRow 的其余逻辑：构造 newParam、onChange、）

**1e. 删除 ParamCard 组件的 `selected`/`onSelect` props**

在 ParamCard 的 props 类型定义中，删除 `selected` 和 `onSelect`：
将：
```tsx
  ({
    param,
    idx,
    errors,
    unitOptions,
    selected,
    onSelect,
    onUpdate,
    onRemove,
    onTypeChange,
  }: {
    param: ParameterDefinitionDto;
    idx: number;
    errors: string[];
    unitOptions: { label: string; value: string }[];
    selected: boolean;
    onSelect: () => void;
    onUpdate: (patch: Partial<ParameterDefinitionDto>) => void;
    onRemove: () => void;
    onTypeChange: (vt: ParameterValueType) => void;
  }) => {
```
改为：
```tsx
  ({
    param,
    idx,
    errors,
    unitOptions,
    onUpdate,
    onRemove,
    onTypeChange,
  }: {
    param: ParameterDefinitionDto;
    idx: number;
    errors: string[];
    unitOptions: { label: string; value: string }[];
    onUpdate: (patch: Partial<ParameterDefinitionDto>) => void;
    onRemove: () => void;
    onTypeChange: (vt: ParameterValueType) => void;
  }) => {
```

**1f. 删除 ParamCard 根 div 的 onClick 与 selected className**

将 ParamCard 的 return 根 div（当前约这样）：
```tsx
    return (
      <div
        className={`${styles['param-card']} ${isError ? styles['is-error'] : ''} ${
          selected ? styles['is-selected'] : ''
        }`}
        onClick={(e) => {
          if ((e.target as HTMLElement).closest('input, select, button, .arco-select, .arco-switch, .' + styles['drag-handle'])) return;
          onSelect();
        }}
      >
```
改为：
```tsx
    return (
      <div
        className={`${styles['param-card']} ${isError ? styles['is-error'] : ''}`}
      >
```

**1g. 删除 SortableList 的 selectedId/onSelect 传递**

在 SortableList 的 props 类型和函数签名中，删除 `selectedId`、`onSelect`。将 SortableList 的 props 类型：
```tsx
  ({
    items,
    errorMap,
    unitOptions,
    selectedId,
    onSelect,
    onUpdate,
    onRemove,
    onTypeChange,
  }: {
    items: ParameterDefinitionDto[];
    errorMap: ErrorMap;
    unitOptions: { label: string; value: string }[];
    selectedId: string | null;
    onSelect: (id: string) => void;
    onUpdate: (idx: number, patch: Partial<ParameterDefinitionDto>) => void;
    onRemove: (idx: number) => void;
    onTypeChange: (idx: number, vt: ParameterValueType) => void;
  }) => {
```
改为：
```tsx
  ({
    items,
    errorMap,
    unitOptions,
    onUpdate,
    onRemove,
    onTypeChange,
  }: {
    items: ParameterDefinitionDto[];
    errorMap: ErrorMap;
    unitOptions: { label: string; value: string }[];
    onUpdate: (idx: number, patch: Partial<ParameterDefinitionDto>) => void;
    onRemove: (idx: number) => void;
    onTypeChange: (idx: number, vt: ParameterValueType) => void;
  }) => {
```

并在 SortableList 内部 map 中，删除传给 ParamCard 的 `selected` 和 `onSelect` 属性。将：
```tsx
            <ParamCard
              key={key}
              index={idx}
              param={p}
              idx={idx}
              errors={errorMap[key] || []}
              unitOptions={unitOptions}
              selected={selectedId === key}
              onSelect={() => onSelect(key)}
              onUpdate={(patch: Partial<ParameterDefinitionDto>) => onUpdate(idx, patch)}
              onRemove={() => onRemove(idx)}
              onTypeChange={(vt: ParameterValueType) => onTypeChange(idx, vt)}
            />
```
改为：
```tsx
            <ParamCard
              key={key}
              index={idx}
              param={p}
              idx={idx}
              errors={errorMap[key] || []}
              unitOptions={unitOptions}
              onUpdate={(patch: Partial<ParameterDefinitionDto>) => onUpdate(idx, patch)}
              onRemove={() => onRemove(idx)}
              onTypeChange={(vt: ParameterValueType) => onTypeChange(idx, vt)}
            />
```

**1h. 删除 SortableList 内「+ 添加参数」按钮对 onSelect 的调用**

SortableList 里的添加按钮当前用 `onClick={() => onSelect('__add__')}`。由于 onSelect 已删，改为直接用 addRow。但 addRow 定义在 ParameterEditor 主组件里，SortableList 访问不到。

**解决方案**：把 SortableList 的添加按钮 onClick 改为调用一个新增的 `onAdd` prop。

在 SortableList 的 props 类型中加回 `onAdd`（替代被删的 onSelect）：
```tsx
  onAdd: () => void;
```
并将 SortableList 内的添加按钮：
```tsx
        <Button
          className={styles['param-add-btn']}
          type="dashed"
          long
          icon={<IconPlus />}
          onClick={() => onSelect('__add__')}
        >
```
改为：
```tsx
        <Button
          className={styles['param-add-btn']}
          type="dashed"
          long
          icon={<IconPlus />}
          onClick={onAdd}
        >
```

**1i. 删除主组件 return 中的预览面板 + 调整容器结构**

主组件（ParameterEditor 函数）的 return 当前是 `editor-layout` > `param-list-col`（含统计条+列表）+ `preview-panel`。

将整个 return 块（从 `return (` 到对应 `);`）替换为下面去掉预览、去掉分栏容器的版本：

```tsx
  return (
    <div>
      {/* 统计条 */}
      <div className={styles['param-stats']}>
        <div className={styles['param-stat']}>
          <b>{stats.total}</b>
          <span>{t['equipment.type.param.stat.total']}</span>
        </div>
        <span className={styles['param-stat-divider']} />
        <div className={styles['param-stat']}>
          <b>{stats.number}</b>
          <span>{t['equipment.type.param.stat.number']}</span>
        </div>
        <span className={styles['param-stat-divider']} />
        <div className={styles['param-stat']}>
          <b>{stats.enum}</b>
          <span>{t['equipment.type.param.stat.enum']}</span>
        </div>
        <span className={styles['param-stat-divider']} />
        <div className={styles['param-stat']}>
          <b>{stats.text}</b>
          <span>{t['equipment.type.param.stat.text']}</span>
        </div>
        <span className={styles['param-stat-divider']} />
        <div className={`${styles['param-stat']} ${errorCount > 0 ? styles['stat-error'] : ''}`}>
          <b>{errorCount}</b>
          <span>{t['equipment.type.param.stat.error']}</span>
        </div>
        <span style={{ marginLeft: 'auto', color: 'var(--color-text-3)', fontSize: 12 }}>
          {t['equipment.type.param.dragHint']}
        </span>
      </div>

      {value.length === 0 ? (
        <>
          <Empty description={t['equipment.type.param.empty']} />
          <Button
            className={styles['param-add-btn']}
            type="dashed"
            long
            icon={<IconPlus />}
            onClick={addRow}
          >
            {t['equipment.type.button.addParameter']}
          </Button>
        </>
      ) : (
        // SortableList 的类型因 react-sortable-hoc 的 @types 限制无法精确推断业务 props，用 as any 绕过
        <SortableList
          items={value}
          onSortEnd={onSortEnd}
          useDragHandle
          lockAxis="y"
          errorMap={errorMap}
          unitOptions={unitOptions}
          onAdd={addRow}
          onUpdate={updateRow}
          onRemove={removeRow}
          onTypeChange={changeValueType}
        />
      )}

      {errorCount > 0 && (
        <Alert
          type="error"
          style={{ marginTop: 12 }}
          content={t['equipment.type.param.error.summary'].replace('{n}', String(errorCount))}
        />
      )}
    </div>
  );
```

**注意**：上面 SortableList 调用里，删除了 `selectedId={selectedKey}` 和 `onSelect={handleSelect}`，新增了 `onAdd={addRow}`。同时移除了 `param-list-col` 和 `editor-layout` 的 className 包裹（改为裸 `<div>`）。

**1j. 清理未使用的 import**

删除预览面板后，以下 import 可能变成未使用，检查并删除（若 build 报 unused 则删，不报则保留）：
- `Tag`（预览面板用过，删除后检查 ParamCard 是否还用——ParamCard 没用 Tag，可删）
- `useState`（删了 selectedKey 后，若主组件无其它 useState 则删）

- [ ] **Step 2: 清理 style 中的预览与分栏样式**

在 `frontend/src/pages/business/equipment/style/index.module.less` 中删除以下 class 块：

**2a. 删除 `.editor-layout` 和 `.param-list-col`**（约第 175-184 行）：
```less
.editor-layout {
  display: flex;
  gap: 16px;
  align-items: flex-start;
}

.param-list-col {
  flex: 1;
  min-width: 0;
}
```

**2b. 删除 `.param-card` 的 `&.is-selected` 块**（在 `.param-card` 内部，约第 235 行）。找到 `.param-card` 规则内的：
```less
  &.is-selected {
    border-color: var(--color-primary-6);
    box-shadow: 0 0 0 2px rgba(22, 93, 255, 0.12);
  }
```
删除这一段（保留 `.param-card` 的其它样式和 `&.is-error`）。

**2c. 删除整个预览面板样式块**（`.preview-panel` 及其内部所有子规则，约第 397-446 行）。从：
```less
// 右栏预览
.preview-panel {
```
一直到 `.preview-foot { ... }` 的闭合括号，整块删除。

- [ ] **Step 3: 编译验证**

Run: `cd frontend && npm run build`
Expected: `✓ built in <N>s`，无报错。若报 `selectedKey`/`selectedParam`/`onSelect` undefined，检查 Step 1 是否有遗漏引用。若报 unused import，按 Step 1j 清理。

- [ ] **Step 4: 提交**

```bash
git add frontend/src/pages/business/equipment/type/ParameterEditor.tsx \
        frontend/src/pages/business/equipment/style/index.module.less
git commit -m "refactor(equipment): 参数编辑器删除预览面板（单列布局）"
```

---

## Task 2: TypeFormPage 左右分栏 + 基础信息顶标签单列

把 TypeFormPage 的两个 Card（基础信息 + 参数定义）包进左右分栏容器（35:65），基础信息内部从 3 列网格改为顶标签单列。

**Files:**
- Modify: `frontend/src/pages/business/equipment/type/TypeFormPage.tsx`
- Modify: `frontend/src/pages/business/equipment/style/index.module.less`（新增分栏样式）

**Interfaces:**
- Consumes: Task 1 后的 ParameterEditor（单列，无预览）
- Produces: 左右分栏的设备类型表单页

- [ ] **Step 1: 新增左右分栏样式**

在 `frontend/src/pages/business/equipment/style/index.module.less` 中，在 `.form-page-actions-bar { ... }` 块之后，插入分栏样式：

```less
// 表单页左右分栏（基础信息 + 参数定义），宽屏左右、窄屏堆叠
.form-split-layout {
  display: flex;
  gap: 16px;
  align-items: flex-start;

  .form-split-left {
    flex: 0 0 35%;
    min-width: 0;
  }
  .form-split-right {
    flex: 1;
    min-width: 0;
  }

  @media (max-width: 1199px) {
    flex-direction: column;

    .form-split-left {
      flex: 1 1 auto;
    }
  }
}
```

- [ ] **Step 2: 改 TypeFormPage 的 Form 区为左右分栏 + 基础信息顶标签单列**

当前 `frontend/src/pages/business/equipment/type/TypeFormPage.tsx` 的 `<Form>...</Form>` 块（约第 157-231 行）含两个 Card。把它改为：外层包 `form-split-layout`，左栏放基础信息 Card（内部改顶标签单列无网格），右栏放参数定义 Card。

将整个 `<Form ...>...</Form>` 块替换为：

```tsx
      <Form
        form={form}
        layout="vertical"
        disabled={!editing && preview.noRule}
      >
        <div className={styles['form-split-layout']}>
          {/* 左栏：基础信息（顶标签单列） */}
          <div className={styles['form-split-left']}>
            <Card className={styles['form-page-card']}>
              <Title heading={6} style={{ marginTop: 0 }}>
                {t['equipment.type.detail.baseInfo']}
              </Title>
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
              <FormItem label={t['equipment.type.form.name']} field="name" rules={[{ required: true }]}>
                <Input maxLength={50} />
              </FormItem>
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
              <FormItem label={t['equipment.type.form.sortOrder']} field="sortOrder" initialValue={0}>
                <InputNumber min={0} style={{ width: '100%' }} />
              </FormItem>
              <FormItem label={t['equipment.type.form.isActive']} field="isActive" triggerPropName="checked">
                <Switch />
              </FormItem>
              <FormItem label={t['equipment.type.form.remark']} field="remark">
                <TextArea maxLength={500} showWordLimit />
              </FormItem>
            </Card>
          </div>

          {/* 右栏：参数定义 */}
          <div className={styles['form-split-right']}>
            <Card className={styles['form-page-card']}>
              <Title heading={6} style={{ marginTop: 0 }}>
                {t['equipment.type.form.parameters']}
              </Title>
              <FormItem>
                <ParameterEditor value={parameters} onChange={setParameters} />
              </FormItem>
            </Card>
          </div>
        </div>
      </Form>
```

**关键变化（供 implementer 核对）：**
1. 移除了所有 `<Row gutter={48}>` 和 `<Col span={8}>` 网格
2. 基础信息字段全部改为顶标签单列（`layout="vertical"` 下每个 FormItem 独占一行，无 Col 包装）
3. 两个 Card 分别包进 `form-split-left` / `form-split-right` div
4. 外层包 `form-split-layout` div
5. 分类码条件渲染从 Col 内移出，直接作为 FormItem（但仍受 `!editing && preview.includeCategory` 控制）
6. `Form` 的 `layout` 保持 `"vertical"`（顶标签）

**检查 import**：删除网格后，`Grid`/`Row`/`Col` 可能不再使用。检查 TypeFormPage 顶部 import，若 `Grid` 仅用于此处则删除 `Grid` import 和 `const { Row, Col } = Grid;`。

- [ ] **Step 3: 编译验证**

Run: `cd frontend && npm run build`
Expected: `✓ built in <N>s`，无报错。若报 `Row`/`Col` undefined，说明 import 没清理干净；若报 unused，按提示清理。

- [ ] **Step 4: 提交**

```bash
git add frontend/src/pages/business/equipment/type/TypeFormPage.tsx \
        frontend/src/pages/business/equipment/style/index.module.less
git commit -m "refactor(equipment): 设备类型表单页左右分栏布局（基础信息顶标签单列）"
```

---

## Task 3: 人工核对验收要点

项目前端无单测，最终验证靠人工核对 spec 的 10 条验收要点。

**Files:** 无（验证任务）

- [ ] **Step 1: 启动 dev 服务器**

Run: `cd frontend && npm run dev`

- [ ] **Step 2: 逐条核对验收要点**

登录后进「业务 / 设备管理」，切到设备类型 Tab，点新建/编辑，核对：

1. 宽屏下表单呈**左右分栏**（基础信息左 35%、参数定义右 65%），控件宽度合理
2. 左栏基础信息：顶标签单列，6 字段，控件等宽占满栏宽（无留白、无长短不一）
3. 右栏参数定义：**无预览面板**，统计条 + 卡片列表占满栏宽
4. 参数卡片功能正常：拖拽排序、类型切换（数值/枚举/文本）、必填开关、min/max/精度/单位、实时校验、删除、添加
5. 两侧顶部对齐；参数多时右栏自然向下延伸
6. 窄屏（窗口宽拉到 <1200px）：左右**堆叠为上下**
7. 底部 sticky 操作栏正常（取消/保存，无规则时保存禁用）
8. 返回/取消正确跳回设备类型 Tab（不回归）
9. c02 编号预览/分类码/无规则禁用全流程正常
10. 整体视觉：无过宽控件、无大块留白

- [ ] **Step 3: 标记完成**

验收全过后本计划完成。

---

## Self-Review 备注

- **Spec 覆盖**：spec §3.2（基础信息顶标签单列）+§3.3（参数定义删预览）→ Task 1+2；spec §3.1（左右分栏）→ Task 2；spec §3.4（响应式）→ Task 2 Step 1 的 @media；spec §6（验收）→ Task 3。全覆盖。
- **类型一致**：ParameterEditor 对外 props（value/onChange/unitOptions）不变；SortableList 新增 onAdd prop 替代被删的 onSelect。
- **无占位符**：所有步骤含完整代码。
- **风险点**：Task 1 删除较多代码（预览面板 + 选中态链路），implementer 需仔细检查无残留引用；Step 1 拆成 a-j 小步逐项删除，降低出错率。
