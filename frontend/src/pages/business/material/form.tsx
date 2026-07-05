import { useEffect, useState } from 'react';
import {
  Alert,
  Form,
  Input,
  InputNumber,
  Message,
  Modal,
  Select,
} from '@arco-design/web-react';
import {
  MaterialDetail,
  MaterialFormData,
  createMaterial,
  updateMaterial,
} from '@/api/material';
import { MeasurementUnit } from '@/api/measurementUnit';
import { useNumberingPreview } from '@/components/Numbering/useNumberingPreview';
import CategorySelect from '@/components/Numbering/CategorySelect';
import useLocale from '@/utils/useLocale';
import locale from './locale';

const FormItem = Form.Item;
const TextArea = Input.TextArea;
const Option = Select.Option;

export default function MaterialFormModal({
  visible,
  editing,
  units,
  onClose,
  onSuccess,
}: {
  visible: boolean;
  editing: MaterialDetail | null; // null = 新建模式
  units: MeasurementUnit[];
  onClose: () => void;
  onSuccess: () => void;
}) {
  const t = useLocale(locale);
  const [form] = Form.useForm();
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  // 新建模式：编号预览 + 分类码自判（规则驱动）
  const preview = useNumberingPreview('material');

  useEffect(() => {
    if (visible) {
      setErrorMsg('');
      if (editing) {
        // 编辑模式：展示实际编号
        form.setFieldsValue({
          name: editing.name,
          spec: editing.spec,
          category: editing.category,
          unitId: editing.unitId,
          sortOrder: editing.sortOrder,
          remark: editing.remark,
        });
      } else {
        // 新建模式：预览下一个编号（只读，不消耗计数）
        preview.reload();
        form.resetFields();
      }
    }
  }, [visible, editing, form]);

  const handleOk = async () => {
    try {
      const values = (await form.validate()) as MaterialFormData;
      setConfirmLoading(true);
      setErrorMsg('');
      if (editing) {
        await updateMaterial(editing.id, values);
        Message.success(t['material.message.updateSuccess']);
      } else {
        await createMaterial({ ...values, categoryCode: preview.categoryCode });
        Message.success(t['material.message.createSuccess']);
      }
      onSuccess();
      onClose();
    } catch (err: any) {
      // 后端 400:无编号规则等,展示在顶部 Alert
      const msg = err?.response?.data?.message || err?.message || '';
      if (msg.includes('编号') || msg.includes('rule') || msg.includes('numbering')) {
        setErrorMsg(t['material.error.noNumberingRule']);
      } else {
        setErrorMsg(msg);
      }
    } finally {
      setConfirmLoading(false);
    }
  };

  return (
    <Modal
      title={editing ? t['material.form.title.edit'] : t['material.form.title.create']}
      visible={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={confirmLoading}
      okButtonProps={{ disabled: !editing && preview.noRule }}
      unmountOnExit
    >
      {!editing && preview.noRule && (
        <Alert type="warning" content={t['material.form.noRule.block']} style={{ marginBottom: 16 }} />
      )}
      {errorMsg && <Alert type="error" content={errorMsg} style={{ marginBottom: 16 }} />}
      <Form form={form} layout="vertical" disabled={!editing && preview.noRule}>
        <FormItem label={t['material.form.code']}>
          <Input
            value={(editing ? editing.code : preview.code) ?? undefined}
            readOnly
            placeholder={preview.codeLoading ? t['material.form.code.previewing'] : t['material.form.code.placeholder']}
          />
        </FormItem>
        {!editing && preview.includeCategory && (
          <FormItem label={t['material.form.categoryCode']} field="categoryCode" rules={[{ required: true }]}>
            <CategorySelect
              options={preview.categoryOptions}
              value={preview.categoryCode}
              onChange={preview.setCategoryCode}
              loading={preview.codeLoading}
              placeholder={t['material.form.categoryCode.placeholder']}
            />
          </FormItem>
        )}
        <FormItem label={t['material.form.name']} field="name" rules={[{ required: true }]}>
          <Input maxLength={100} />
        </FormItem>
        <FormItem label={t['material.form.spec']} field="spec" rules={[{ required: true }]}>
          <Input maxLength={100} />
        </FormItem>
        <FormItem label={t['material.form.category']} field="category" rules={[{ required: true }]}>
          <Input maxLength={32} />
        </FormItem>
        <FormItem label={t['material.form.unit']} field="unitId">
          <Select allowClear placeholder={t['material.form.unit']}>
            {units.map((u) => (
              <Option key={u.id} value={u.id}>
                {u.nameZh}
                {u.symbol ? ` (${u.symbol})` : ''}
              </Option>
            ))}
          </Select>
        </FormItem>
        <FormItem label={t['material.form.sortOrder']} field="sortOrder" initialValue={0}>
          <InputNumber min={0} style={{ width: '100%' }} />
        </FormItem>
        <FormItem label={t['material.form.remark']} field="remark">
          <TextArea maxLength={256} />
        </FormItem>
      </Form>
    </Modal>
  );
}
