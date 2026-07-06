import { useEffect, useState } from 'react';
import {
  Alert,
  DatePicker,
  Form,
  Input,
  InputNumber,
  Message,
  Modal,
  Radio,
  Select,
  Switch,
} from '@arco-design/web-react';
import {
  CreateEquipmentRequest,
  EQUIPMENT_STATUSES,
  EquipmentDto,
  EquipmentTypeListItemDto,
  createEquipment,
  updateEquipment,
} from '@/api/equipment';
import { useNumberingPreview } from '@/components/Numbering/useNumberingPreview';
import CategorySelect from '@/components/Numbering/CategorySelect';
import useLocale from '@/utils/useLocale';
import locale from '../locale';

const FormItem = Form.Item;
const TextArea = Input.TextArea;
const Option = Select.Option;

export default function EquipmentFormModal({
  visible,
  editing,
  types,
  onClose,
  onSuccess,
}: {
  visible: boolean;
  editing: EquipmentDto | null; // null = 新建模式
  types: EquipmentTypeListItemDto[];
  onClose: () => void;
  onSuccess: () => void;
}) {
  const t = useLocale(locale);
  const [form] = Form.useForm();
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  // 新建模式：编号预览 + 分类码自判（规则驱动）
  const preview = useNumberingPreview('Equipment');

  useEffect(() => {
    if (visible) {
      setErrorMsg('');
      if (editing) {
        // 编辑模式：展示实际编号 + 现有字段
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
        });
      } else {
        // 新建模式：预览下一个编号（只读，不消耗计数）
        preview.reload();
        form.resetFields();
        form.setFieldValue('isActive', true);
        form.setFieldValue('status', 'Running');
        form.setFieldValue('sortOrder', 0);
      }
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [visible, editing]);

  const handleOk = async () => {
    try {
      const values = (await form.validate()) as Omit<
        CreateEquipmentRequest,
        'categoryCode'
      >;
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
      // 后端 400：名称重复 / 无编号规则，展示在顶部 Alert
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
    <Modal
      title={editing ? t['equipment.item.form.title.edit'] : t['equipment.item.form.title.create']}
      visible={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={confirmLoading}
      okButtonProps={{ disabled: !editing && preview.noRule }}
      unmountOnExit
      style={{ width: 720 }}
    >
      {!editing && preview.noRule && (
        <Alert
          type="warning"
          content={t['equipment.item.form.noRule.block']}
          style={{ marginBottom: 16 }}
        />
      )}
      {errorMsg && <Alert type="error" content={errorMsg} style={{ marginBottom: 16 }} />}
      <Form
        form={form}
        layout="vertical"
        disabled={!editing && preview.noRule}
      >
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
        <FormItem
          label={t['equipment.item.form.name']}
          field="name"
          rules={[{ required: true }]}
        >
          <Input maxLength={100} />
        </FormItem>
        <FormItem
          label={t['equipment.item.form.type']}
          field="equipmentTypeId"
          rules={[{ required: true, message: t['equipment.item.form.type.required'] }]}
        >
          <Select
            placeholder={t['equipment.item.form.type.placeholder']}
            showSearch
            allowClear
          >
            {types.map((tp) => (
              <Option key={tp.id} value={tp.id}>
                {tp.name}
              </Option>
            ))}
          </Select>
        </FormItem>
        <FormItem label={t['equipment.item.form.spec']} field="specification">
          <Input maxLength={200} />
        </FormItem>
        <FormItem label={t['equipment.item.form.supplier']} field="supplier">
          <Input maxLength={100} />
        </FormItem>
        <FormItem label={t['equipment.item.form.location']} field="location">
          <Input maxLength={100} />
        </FormItem>
        <FormItem
          label={t['equipment.item.form.status']}
          field="status"
          rules={[{ required: true }]}
        >
          <Radio.Group>
            {EQUIPMENT_STATUSES.map((s) => (
              <Radio key={s} value={s}>
                {t[`equipment.item.status.${s.toLowerCase()}`]}
              </Radio>
            ))}
          </Radio.Group>
        </FormItem>
        <FormItem label={t['equipment.item.form.purchaseDate']} field="purchaseDate">
          <DatePicker style={{ width: '100%' }} />
        </FormItem>
        <FormItem label={t['equipment.item.form.warrantyExpiry']} field="warrantyExpiry">
          <DatePicker style={{ width: '100%' }} />
        </FormItem>
        <FormItem label={t['equipment.item.form.sortOrder']} field="sortOrder" initialValue={0}>
          <InputNumber min={0} style={{ width: '100%' }} />
        </FormItem>
        <FormItem label={t['equipment.item.form.remark']} field="remark">
          <TextArea maxLength={500} />
        </FormItem>
        <FormItem label={t['equipment.item.form.isActive']} field="isActive" triggerPropName="checked">
          <Switch />
        </FormItem>
      </Form>
    </Modal>
  );
}
