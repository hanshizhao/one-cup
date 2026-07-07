# 设备编辑表单 Modal → Drawer 实施计划

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** 把设备新建/编辑表单的容器从 560px 居中 Modal 改为 480px 右侧 Drawer，与详情抽屉同形态，单列左标签布局。

**Architecture:** 重命名 `EquipmentFormModal.tsx` → `EquipmentFormDrawer.tsx`，外壳由 `Modal` 改为 `Drawer`，表单由 `layout="vertical"` + 两列网格改为 `layout="horizontal"` + 纯单列，分组标题复用详情抽屉的 `detail-group-title`。列表页 `EquipmentTab.tsx` 改引用名。`style/index.module.less` 移除上一轮为 Modal 加的冗余 `form-section-title` class。

**Tech Stack:** React + @arco-design/web-react（Drawer / Form / Grid）、TypeScript、CSS Modules (less)。

## Global Constraints

- 容器宽度固定 **480px**（与详情抽屉 `EquipmentDetail.tsx` 的 `width={480}` 一致）
- 表单 `layout="horizontal"`，`labelCol={{ span: 6 }}`（24 栅格，≈120px，对齐详情抽屉 label 宽 100px 量级）
- 纯单列布局，所有字段独占一行，无两列并排
- 分组标题用 `detail-group-title` class（已存在于 style 文件），不用新 class
- 运行状态 `Radio.Group direction="horizontal"`，确保三个选项横向
- c02 编号流程不变：编号只读 Input、分类码 `!editing && preview.includeCategory` 条件渲染、提交透传 `preview.categoryCode`、`preview.noRule` 时禁用表单 + 顶部 Alert
- 项目前端无单测（设计文档 8.3 节），验证手段为 `npm run build` 编译 + 人工核对验收要点

参考文档：`docs/superpowers/specs/2026-07-07-equipment-form-drawer-design.md`

---

## Task 1: 重写 EquipmentFormModal.tsx → EquipmentFormDrawer.tsx

把编辑表单从 Modal 改为 Drawer，单列左标签布局。这是本次改动的核心，独立可编译可验证。

**Files:**
- Create: `frontend/src/pages/business/equipment/equipment/EquipmentFormDrawer.tsx`（全新文件，内容见 Step 1）
- Delete: `frontend/src/pages/business/equipment/equipment/EquipmentFormModal.tsx`（被取代）
- Modify: `frontend/src/pages/business/equipment/equipment/EquipmentTab.tsx`（import 与 JSX 改名）

**Interfaces:**
- Consumes: `useNumberingPreview('equipment')` hook、`<CategorySelect>` 组件、`getActiveEquipmentTypes` / `createEquipment` / `updateEquipment` API（均来自现有 `@/api/equipment` 与 `@/components/Numbering/*`，签名不变）
- Produces: `EquipmentFormDrawer` 默认导出组件，props 为 `{ visible: boolean; editing: EquipmentDto | null; onClose: () => void; onSuccess: () => void }`（与原 `EquipmentFormModal` props 完全一致，调用方改名即可）

- [ ] **Step 1: 创建 EquipmentFormDrawer.tsx**

创建 `frontend/src/pages/business/equipment/equipment/EquipmentFormDrawer.tsx`，完整内容：

```tsx
import { useEffect, useState } from 'react';
import {
  Alert,
  Button,
  DatePicker,
  Drawer,
  Form,
  Input,
  InputNumber,
  Message,
  Radio,
  Select,
  Space,
  Switch,
} from '@arco-design/web-react';
import {
  CreateEquipmentRequest,
  EQUIPMENT_STATUSES,
  EquipmentDto,
  EquipmentTypeListItemDto,
  createEquipment,
  getActiveEquipmentTypes,
  updateEquipment,
} from '@/api/equipment';
import { useNumberingPreview } from '@/components/Numbering/useNumberingPreview';
import CategorySelect from '@/components/Numbering/CategorySelect';
import useLocale from '@/utils/useLocale';
import locale from '../locale';
import styles from '../style/index.module.less';

const FormItem = Form.Item;
const TextArea = Input.TextArea;
const Option = Select.Option;

/**
 * 设备实例新建/编辑 Drawer（受控）。
 * 与设备详情抽屉同形态：480px 右侧抽屉，单列左标签布局，4 分组。
 * props 与原 EquipmentFormModal 一致：visible / editing(null=新建) / onClose / onSuccess。
 */
export default function EquipmentFormDrawer({
  visible,
  editing,
  onClose,
  onSuccess,
}: {
  visible: boolean;
  editing: EquipmentDto | null; // null = 新建模式
  onClose: () => void;
  onSuccess: () => void;
}) {
  const t = useLocale(locale);
  const [form] = Form.useForm();
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  const [types, setTypes] = useState<EquipmentTypeListItemDto[]>([]);
  // 新建模式：编号预览 + 分类码自判（规则驱动）
  const preview = useNumberingPreview('equipment');

  // 拉类型列表（下拉用）：每次打开拉一次，保证最新
  useEffect(() => {
    if (visible) {
      getActiveEquipmentTypes()
        .then(setTypes)
        .catch(() => {});
    }
  }, [visible]);

  useEffect(() => {
    if (visible) {
      setErrorMsg('');
      if (editing) {
        // 编辑模式：回填完整字段（EquipmentDto 含 remark / 日期）
        form.setFieldsValue({
          name: editing.name,
          equipmentTypeId: editing.equipmentTypeId,
          specification: editing.specification,
          supplier: editing.supplier,
          location: editing.location,
          status: editing.status,
          purchaseDate: editing.purchaseDate,
          warrantyExpiry: editing.warrantyExpiry,
          remark: editing.remark,
          isActive: editing.isActive,
          sortOrder: editing.sortOrder,
        });
      } else {
        // 新建模式：预览下一个编号（只读，不消耗计数）+ 默认值
        preview.reload();
        form.resetFields();
        form.setFieldValue('isActive', true);
        form.setFieldValue('status', 'Running');
        form.setFieldValue('sortOrder', 0);
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [visible, editing, form]);

  const handleOk = async () => {
    try {
      const values = (await form.validate()) as Omit<CreateEquipmentRequest, 'categoryCode'>;
      setConfirmLoading(true);
      setErrorMsg('');
      if (editing) {
        await updateEquipment(editing.id, values);
        Message.success(t['equipment.item.message.updateSuccess']);
      } else {
        await createEquipment({ ...values, categoryCode: preview.categoryCode });
        Message.success(t['equipment.item.message.createSuccess']);
      }
      onSuccess();
      onClose();
    } catch (err: any) {
      const msg = err?.response?.data?.message || err?.message || '';
      if (msg.includes('编号') || msg.includes('rule') || msg.includes('numbering')) {
        setErrorMsg(t['equipment.item.error.noNumberingRule']);
      } else {
        setErrorMsg(msg);
      }
    } finally {
      setConfirmLoading(false);
    }
  };

  return (
    <Drawer
      title={editing ? t['equipment.item.form.title.edit'] : t['equipment.item.form.title.create']}
      visible={visible}
      onCancel={onClose}
      width={480}
      footer={
        <div style={{ display: 'flex', justifyContent: 'flex-end' }}>
          <Space>
            <Button onClick={onClose}>{t['equipment.item.form.page.cancel']}</Button>
            <Button
              type="primary"
              loading={confirmLoading}
              disabled={!editing && preview.noRule}
              onClick={handleOk}
            >
              {t['equipment.item.form.page.save']}
            </Button>
          </Space>
        </div>
      }
    >
      {!editing && preview.noRule && (
        <Alert type="warning" content={t['equipment.item.form.noRule.block']} style={{ marginBottom: 16 }} />
      )}
      {errorMsg && <Alert type="error" content={errorMsg} style={{ marginBottom: 16 }} />}

      <Form
        form={form}
        layout="horizontal"
        labelCol={{ span: 6 }}
        wrapperCol={{ span: 18 }}
        disabled={!editing && preview.noRule}
      >
        {/* ── 基础信息 ── */}
        <div className={styles['detail-group-title']}>{t['equipment.item.detail.group.base']}</div>
        <FormItem label={t['equipment.item.form.code']}>
          <Input
            value={(editing ? editing.code : preview.code) ?? undefined}
            readOnly
            placeholder={
              preview.codeLoading
                ? t['equipment.item.form.code.previewing']
                : t['equipment.item.form.code.placeholder']
            }
          />
        </FormItem>
        <FormItem label={t['equipment.item.form.name']} field="name" rules={[{ required: true }]}>
          <Input maxLength={50} />
        </FormItem>
        <FormItem
          label={t['equipment.item.form.type']}
          field="equipmentTypeId"
          rules={[{ required: true, message: t['equipment.item.form.type.required'] }]}
        >
          <Select placeholder={t['equipment.item.form.type.placeholder']} showSearch allowClear>
            {types.map((tp) => (
              <Option key={tp.id} value={tp.id}>
                {tp.name}
              </Option>
            ))}
          </Select>
        </FormItem>
        {/* 分类码：仅新建 + 规则要求时条件渲染（c02） */}
        {!editing && preview.includeCategory && (
          <FormItem
            label={t['equipment.item.form.categoryCode']}
            field="categoryCode"
            rules={[{ required: true }]}
          >
            <CategorySelect
              options={preview.categoryOptions}
              value={preview.categoryCode}
              onChange={preview.setCategoryCode}
              loading={preview.codeLoading}
              placeholder={t['equipment.item.form.categoryCode.placeholder']}
            />
          </FormItem>
        )}

        {/* ── 运行状态 ── */}
        <div className={styles['detail-group-title']}>{t['equipment.item.detail.group.runStatus']}</div>
        <FormItem label={t['equipment.item.form.status']} field="status" rules={[{ required: true }]}>
          <Radio.Group direction="horizontal">
            {EQUIPMENT_STATUSES.map((s) => (
              <Radio key={s} value={s}>
                {t[`equipment.item.status.${s.toLowerCase()}`]}
              </Radio>
            ))}
          </Radio.Group>
        </FormItem>
        <FormItem label={t['equipment.item.form.isActive']} field="isActive" triggerPropName="checked">
          <Switch />
        </FormItem>
        <FormItem label={t['equipment.item.form.sortOrder']} field="sortOrder" initialValue={0}>
          <InputNumber min={0} style={{ width: '100%' }} />
        </FormItem>

        {/* ── 资产时间 ── */}
        <div className={styles['detail-group-title']}>{t['equipment.item.detail.group.assetTime']}</div>
        <FormItem label={t['equipment.item.form.spec']} field="specification">
          <Input maxLength={200} />
        </FormItem>
        <FormItem label={t['equipment.item.form.supplier']} field="supplier">
          <Input maxLength={100} />
        </FormItem>
        <FormItem label={t['equipment.item.form.location']} field="location">
          <Input maxLength={100} />
        </FormItem>
        <FormItem label={t['equipment.item.form.purchaseDate']} field="purchaseDate">
          <DatePicker style={{ width: '100%' }} />
        </FormItem>
        <FormItem label={t['equipment.item.form.warrantyExpiry']} field="warrantyExpiry">
          <DatePicker style={{ width: '100%' }} />
        </FormItem>

        {/* ── 备注 ── */}
        <div className={styles['detail-group-title']}>{t['equipment.item.detail.group.remark']}</div>
        <FormItem label={t['equipment.item.form.remark']} field="remark">
          <TextArea maxLength={500} />
        </FormItem>
      </Form>
    </Drawer>
  );
}
```

- [ ] **Step 2: 改 EquipmentTab.tsx 引用**

把 `EquipmentTab.tsx` 里对 `EquipmentFormModal` 的引用改为 `EquipmentFormDrawer`。共两处：

第 1 处——import 语句（文件顶部 import 区）：

将
```tsx
import EquipmentFormModal from './EquipmentFormModal';
```
改为
```tsx
import EquipmentFormDrawer from './EquipmentFormDrawer';
```

第 2 处——JSX 渲染（文件末尾 `<Card>` 内，`<EquipmentDetailDrawer>` 之后）：

将
```tsx
      <EquipmentFormModal
        visible={formVisible}
        editing={editing}
        onClose={() => setFormVisible(false)}
        onSuccess={fetchData}
      />
```
改为
```tsx
      <EquipmentFormDrawer
        visible={formVisible}
        editing={editing}
        onClose={() => setFormVisible(false)}
        onSuccess={fetchData}
      />
```

- [ ] **Step 3: 删除旧 EquipmentFormModal.tsx**

```bash
rm frontend/src/pages/business/equipment/equipment/EquipmentFormModal.tsx
```

- [ ] **Step 4: 编译验证**

Run: `cd frontend && npm run build`
Expected: `✓ built in <N>s`，无报错。若报 `EquipmentFormModal` 未找到或 `EquipmentFormDrawer` 未导出，检查 Step 1/2 路径与拼写。

- [ ] **Step 5: 提交**

```bash
git add frontend/src/pages/business/equipment/equipment/EquipmentFormDrawer.tsx \
        frontend/src/pages/business/equipment/equipment/EquipmentFormModal.tsx \
        frontend/src/pages/business/equipment/equipment/EquipmentTab.tsx
git commit -m "refactor(equipment): 编辑表单 Modal → Drawer（480px 单列左标签）"
```

---

## Task 2: 清理 style 文件冗余 class

Task 1 已让 `form-section-title` class 不再被任何文件引用（Drawer 改用 `detail-group-title`）。本任务移除它，保持样式文件干净。

**Files:**
- Modify: `frontend/src/pages/business/equipment/style/index.module.less`（删除 `.form-section-title { ... }` 块，约第 38-49 行）

**Interfaces:**
- Consumes: 无
- Produces: 无（纯清理，不影响任何组件）

- [ ] **Step 1: 确认 form-section-title 无引用**

Run: `cd frontend && grep -rn "form-section-title" src/`
Expected: 无输出（空）。若仍有引用，说明 Task 1 未完全切换到 `detail-group-title`，需回 Task 1 检查。

- [ ] **Step 2: 删除 form-section-title 样式块**

在 `frontend/src/pages/business/equipment/style/index.module.less` 中删除以下整块（位于 `.form-page-card { ... }` 之后）：

```less
// ── 表单分组标题（设备 Modal / 表单页通用）──
.form-section-title {
  font-size: 14px;
  font-weight: 600;
  color: var(--color-text-1);
  margin: 16px 0 12px;
  padding-bottom: 8px;
  border-bottom: 1px solid var(--color-border-2);

  &:first-child {
    margin-top: 0;
  }
}
```

- [ ] **Step 3: 编译验证**

Run: `cd frontend && npm run build`
Expected: `✓ built in <N>s`，无报错。

- [ ] **Step 4: 提交**

```bash
git add frontend/src/pages/business/equipment/style/index.module.less
git commit -m "chore(equipment): 移除未引用的 form-section-title 样式"
```

---

## Task 3: 人工核对验收要点

项目前端无单测，最终验证靠人工核对 spec 的 7 条验收要点。

**Files:** 无（验证任务）

- [ ] **Step 1: 启动 dev 服务器**

Run: `cd frontend && npm run dev`
Expected: vite 启动，浏览器打开本地地址。

- [ ] **Step 2: 逐条核对验收要点**

登录后进入「业务 / 设备管理 / 设备」Tab，逐条核对：

1. 点「新建设备」→ 右侧滑出 **480px Drawer**，单列左标签布局，4 个分组标题清晰（基础信息 / 运行状态 / 资产时间 / 备注）
2. 点表格某行「编辑」→ 顶部 Message.loading → Drawer 弹出且回填全部字段（含备注、购买日期、保修到期）
3. 运行状态的三个 Radio（运行中 / 已停机 / 维护中）**横向一行不换行**
4. 编辑改个字段点「保存」→ 成功 Message → Drawer 关闭 → 列表刷新
5. 编辑 Drawer 与详情 Drawer 视觉一致：同宽 480px、同位置（右侧）、同款分组标题
6. 新建模式下：编号预览只读、若编号规则含分类码段则分类码下拉出现、无编号规则时表单禁用 + 顶部黄色警告
7. 关闭 Drawer（点取消 / 点遮罩 / Esc）能正常关闭，不残留

- [ ] **Step 3: 标记完成**

验收全过后，本计划完成。无需提交（无代码改动）。

---

## Self-Review 备注

- **Spec 覆盖**：spec 第 2 节改动清单 3 项 → Task 1（Drawer 主体）+ Task 2（样式清理），全覆盖；spec 第 4 节验收要点 → Task 3，全覆盖。
- **类型一致**：`EquipmentFormDrawer` props 与原 `EquipmentFormModal` 完全一致，`EquipmentTab.tsx` 仅改组件名不改 props 传递。
- **无占位符**：Step 1 给出完整文件内容，无 TODO。
