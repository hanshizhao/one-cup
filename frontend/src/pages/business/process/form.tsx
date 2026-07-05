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
  ProcessDetail,
  ProcessFormData,
  createProcess,
  updateProcess,
} from '@/api/process';
import { useNumberingPreview } from '@/components/Numbering/useNumberingPreview';
import CategorySelect from '@/components/Numbering/CategorySelect';
import useLocale from '@/utils/useLocale';
import locale from './locale';

const FormItem = Form.Item;
const TextArea = Input.TextArea;

export default function ProcessFormModal({
  visible,
  editing,
  onClose,
  onSuccess,
}: {
  visible: boolean;
  editing: ProcessDetail | null; // null = 新建模式
  onClose: () => void;
  onSuccess: () => void;
}) {
  const t = useLocale(locale);
  const [form] = Form.useForm();
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  // 新建模式：编号预览 + 分类码自判（规则驱动）
  const preview = useNumberingPreview('process');

  useEffect(() => {
    if (visible) {
      setErrorMsg('');
      if (editing) {
        // 编辑模式：展示实际编号
        form.setFieldsValue({
          name: editing.name,
          category: editing.category,
          sortOrder: editing.sortOrder,
          remark: editing.remark,
          isActive: editing.isActive,
        });
      } else {
        // 新建模式：预览下一个编号（只读，不消耗计数）
        preview.reload();
        form.resetFields();
        form.setFieldValue('isActive', true);
        form.setFieldValue('sortOrder', 0);
      }
    }
  }, [visible, editing, form]);

  const handleOk = async () => {
    try {
      const values = (await form.validate()) as ProcessFormData;
      setConfirmLoading(true);
      setErrorMsg('');
      if (editing) {
        await updateProcess(editing.id, values);
        Message.success(t['process.message.updateSuccess']);
      } else {
        await createProcess({ ...values, categoryCode: preview.categoryCode });
        Message.success(t['process.message.createSuccess']);
      }
      onSuccess();
      onClose();
    } catch (err: any) {
      // 后端 400：名称重复 / 无编号规则，展示在顶部 Alert
      const msg = err?.response?.data?.message || err?.message || '';
      if (msg.includes('编号') || msg.includes('rule') || msg.includes('numbering')) {
        setErrorMsg(t['process.error.noNumberingRule']);
      } else {
        setErrorMsg(msg);
      }
    } finally {
      setConfirmLoading(false);
    }
  };

  return (
    <Modal
      title={editing ? t['process.form.title.edit'] : t['process.form.title.create']}
      visible={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={confirmLoading}
      okButtonProps={{ disabled: !editing && preview.noRule }}
      unmountOnExit
    >
      {!editing && preview.noRule && (
        <Alert type="warning" content={t['process.form.noRule.block']} style={{ marginBottom: 16 }} />
      )}
      {errorMsg && <Alert type="error" content={errorMsg} style={{ marginBottom: 16 }} />}
      <Form form={form} layout="vertical" disabled={!editing && preview.noRule}>
        <FormItem label={t['process.form.code']}>
          <Input
            value={(editing ? editing.code : preview.code) ?? undefined}
            readOnly
            placeholder={preview.codeLoading ? t['process.form.code.previewing'] : t['process.form.code.placeholder']}
          />
        </FormItem>
        {!editing && preview.includeCategory && (
          <FormItem label={t['process.form.categoryCode']} field="categoryCode" rules={[{ required: true }]}>
            <CategorySelect
              options={preview.categoryOptions}
              value={preview.categoryCode}
              onChange={preview.setCategoryCode}
              loading={preview.codeLoading}
              placeholder={t['process.form.categoryCode.placeholder']}
            />
          </FormItem>
        )}
        <FormItem label={t['process.form.name']} field="name" rules={[{ required: true }]}>
          <Input maxLength={50} />
        </FormItem>
        <FormItem label={t['process.form.category']} field="category">
          <Input maxLength={50} />
        </FormItem>
        <FormItem label={t['process.form.sortOrder']} field="sortOrder">
          <InputNumber min={0} style={{ width: '100%' }} />
        </FormItem>
        <FormItem label={t['process.form.remark']} field="remark">
          <TextArea maxLength={500} />
        </FormItem>
        <FormItem label={t['process.form.isActive']} field="isActive" triggerPropName="checked">
          <Switch />
        </FormItem>
      </Form>
    </Modal>
  );
}
