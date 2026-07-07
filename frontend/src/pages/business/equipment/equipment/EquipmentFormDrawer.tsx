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
 * props：visible / editing(null=新建) / onClose / onSuccess。
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
