import { useEffect, useState } from 'react';
import {
  Alert,
  Form,
  Input,
  Message,
  Modal,
  Switch,
} from '@arco-design/web-react';
import {
  CustomerDetail,
  CustomerFormData,
  createCustomer,
  updateCustomer,
} from '@/api/customer';
import { previewCode } from '@/api/numbering';
import useLocale from '@/utils/useLocale';
import locale from './locale';

const FormItem = Form.Item;
const TextArea = Input.TextArea;

export default function CustomerFormModal({
  visible,
  editing,
  onClose,
  onSuccess,
}: {
  visible: boolean;
  editing: CustomerDetail | null; // null = 新建模式
  onClose: () => void;
  onSuccess: () => void;
}) {
  const t = useLocale(locale);
  const [form] = Form.useForm();
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  // 新建模式：预览下一个客户编号（不消耗计数）。null = 无启用规则 / 预览中。
  const [previewedCode, setPreviewedCode] = useState<string | null>(null);
  const [codeLoading, setCodeLoading] = useState(false);
  // 无编号规则：阻塞新建（用户填了也提交不了）
  const [noRule, setNoRule] = useState(false);

  useEffect(() => {
    if (visible) {
      setErrorMsg('');
      setNoRule(false);
      if (editing) {
        // 编辑模式：展示实际编号
        setPreviewedCode(editing.code);
        form.setFieldsValue({
          name: editing.name,
          shortName: editing.shortName,
          contactPerson: editing.contactPerson,
          contactPhone: editing.contactPhone,
          remark: editing.remark,
          isActive: editing.isActive,
        });
      } else {
        // 新建模式：预览下一个编号（只读，不消耗计数）
        setPreviewedCode(null);
        setCodeLoading(true);
        previewCode('customer')
          .then((res) => {
            // null 表示无启用规则 → 阻塞新建
            if (res.code) {
              setPreviewedCode(res.code);
              setNoRule(false);
            } else {
              setNoRule(true);
            }
          })
          .catch(() => setNoRule(true))
          .finally(() => setCodeLoading(false));
        form.resetFields();
        form.setFieldValue('isActive', true);
      }
    }
  }, [visible, editing, form]);

  const handleOk = async () => {
    try {
      const values = (await form.validate()) as CustomerFormData;
      setConfirmLoading(true);
      setErrorMsg('');
      if (editing) {
        await updateCustomer(editing.id, values);
        Message.success(t['customer.message.updateSuccess']);
      } else {
        await createCustomer(values);
        Message.success(t['customer.message.createSuccess']);
      }
      onSuccess();
      onClose();
    } catch (err: any) {
      // 后端 400：名称重复 / 无编号规则，展示在顶部 Alert
      const msg = err?.response?.data?.message || err?.message || '';
      if (msg.includes('编号') || msg.includes('rule') || msg.includes('numbering')) {
        setErrorMsg(t['customer.error.noNumberingRule']);
      } else {
        setErrorMsg(msg);
      }
    } finally {
      setConfirmLoading(false);
    }
  };

  return (
    <Modal
      title={editing ? t['customer.form.title.edit'] : t['customer.form.title.create']}
      visible={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={confirmLoading}
      okButtonProps={{ disabled: noRule }}
      unmountOnExit
    >
      {noRule && (
        <Alert type="warning" content={t['customer.form.noRule.block']} style={{ marginBottom: 16 }} />
      )}
      {errorMsg && <Alert type="error" content={errorMsg} style={{ marginBottom: 16 }} />}
      <Form form={form} layout="vertical" disabled={noRule}>
        <FormItem label={t['customer.form.code']}>
          <Input
            value={previewedCode ?? undefined}
            readOnly
            placeholder={codeLoading ? t['customer.form.code.previewing'] : t['customer.form.code.placeholder']}
          />
        </FormItem>
        <FormItem label={t['customer.form.name']} field="name" rules={[{ required: true }]}>
          <Input maxLength={100} />
        </FormItem>
        <FormItem label={t['customer.form.shortName']} field="shortName">
          <Input maxLength={50} />
        </FormItem>
        <FormItem label={t['customer.form.contactPerson']} field="contactPerson">
          <Input maxLength={50} />
        </FormItem>
        <FormItem label={t['customer.form.contactPhone']} field="contactPhone">
          <Input maxLength={30} placeholder="0755-12345678 / 13800138000" />
        </FormItem>
        <FormItem label={t['customer.form.remark']} field="remark">
          <TextArea maxLength={500} />
        </FormItem>
        <FormItem label={t['customer.form.isActive']} field="isActive" triggerPropName="checked">
          <Switch />
        </FormItem>
      </Form>
    </Modal>
  );
}
