import { useEffect, useState } from 'react';
import {
  Alert,
  Form,
  Input,
  InputNumber,
  Message,
  Modal,
  Switch,
} from '@arco-design/web-react';
import {
  CreateEquipmentTypeRequest,
  EquipmentTypeDto,
  ParameterDefinitionDto,
  createEquipmentType,
  updateEquipmentType,
} from '@/api/equipment';
import { useNumberingPreview } from '@/components/Numbering/useNumberingPreview';
import CategorySelect from '@/components/Numbering/CategorySelect';
import useLocale from '@/utils/useLocale';
import locale from '../locale';
import ParameterEditor from './ParameterEditor';

const FormItem = Form.Item;
const TextArea = Input.TextArea;

export default function EquipmentTypeFormModal({
  visible,
  editing,
  onClose,
  onSuccess,
}: {
  visible: boolean;
  editing: EquipmentTypeDto | null; // null = 新建模式
  onClose: () => void;
  onSuccess: () => void;
}) {
  const t = useLocale(locale);
  const [form] = Form.useForm();
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  // 参数定义：受控状态，独立于 form（动态表格不适合塞进 Form field）
  const [parameters, setParameters] = useState<ParameterDefinitionDto[]>([]);
  // 新建模式：编号预览 + 分类码自判（规则驱动）
  const preview = useNumberingPreview('EquipmentType');

  useEffect(() => {
    if (visible) {
      setErrorMsg('');
      if (editing) {
        // 编辑模式：展示实际编号 + 现有参数
        // 注：后端 EquipmentTypeDto 未返回 SortOrder，编辑时沿用默认 0（与后端 DTO 契约一致）
        form.setFieldsValue({
          name: editing.name,
          remark: editing.remark,
          isActive: editing.isActive,
        });
        setParameters(editing.parameters || []);
      } else {
        // 新建模式：预览下一个编号（只读，不消耗计数）
        preview.reload();
        form.resetFields();
        form.setFieldValue('isActive', true);
        form.setFieldValue('sortOrder', 0);
        setParameters([]);
      }
    }
  }, [visible, editing, form]);

  const handleOk = async () => {
    try {
      const values = (await form.validate()) as Omit<
        CreateEquipmentTypeRequest,
        'parameters' | 'categoryCode'
      >;
      // 校验：每个参数必须有名称
      const hasBlankName = parameters.some((p) => !p.name || !p.name.trim());
      if (hasBlankName) {
        setErrorMsg(t['equipment.type.message.paramNameRequired']);
        return;
      }
      setConfirmLoading(true);
      setErrorMsg('');
      const payload = { ...values, parameters };
      if (editing) {
        await updateEquipmentType(editing.id, payload);
        Message.success(t['equipment.type.message.updateSuccess']);
      } else {
        await createEquipmentType({ ...payload, categoryCode: preview.categoryCode });
        Message.success(t['equipment.type.message.createSuccess']);
      }
      onSuccess();
      onClose();
    } catch (err: any) {
      // 后端 400：名称重复 / 无编号规则，展示在顶部 Alert
      const msg = err?.response?.data?.message || err?.message || '';
      if (msg.includes('编号') || msg.includes('rule') || msg.includes('numbering')) {
        setErrorMsg(t['equipment.type.error.noNumberingRule']);
      } else {
        setErrorMsg(msg);
      }
    } finally {
      setConfirmLoading(false);
    }
  };

  return (
    <Modal
      title={editing ? t['equipment.type.form.title.edit'] : t['equipment.type.form.title.create']}
      visible={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={confirmLoading}
      okButtonProps={{ disabled: !editing && preview.noRule }}
      unmountOnExit
      style={{ width: 760 }}
    >
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
        <FormItem label={t['equipment.type.form.code']}>
          <Input
            value={(editing ? editing.code : preview.code) ?? undefined}
            readOnly
            placeholder={
              preview.codeLoading
                ? t['equipment.type.form.code.previewing']
                : t['equipment.type.form.code.placeholder']
            }
          />
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
        <FormItem label={t['equipment.type.form.name']} field="name" rules={[{ required: true }]}>
          <Input maxLength={100} />
        </FormItem>
        <FormItem label={t['equipment.type.form.sortOrder']} field="sortOrder" initialValue={0}>
          <InputNumber min={0} style={{ width: '100%' }} />
        </FormItem>
        <FormItem label={t['equipment.type.form.remark']} field="remark">
          <TextArea maxLength={500} />
        </FormItem>
        <FormItem label={t['equipment.type.form.isActive']} field="isActive" triggerPropName="checked">
          <Switch />
        </FormItem>
        <FormItem label={t['equipment.type.form.parameters']}>
          <ParameterEditor value={parameters} onChange={setParameters} />
        </FormItem>
      </Form>
    </Modal>
  );
}
