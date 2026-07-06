import { useEffect, useState } from 'react';
import {
  Alert,
  Form,
  Input,
  InputNumber,
  Message,
  Modal,
  Select,
  Spin,
} from '@arco-design/web-react';
import {
  CreateEquipmentTemplateRequest,
  EquipmentTemplateDto,
  EquipmentTemplateValueDto,
  EquipmentTypeParameterDto,
  TemplateValueDto,
  createEquipmentTemplate,
  getEquipmentTemplateById,
  getEquipmentTypeById,
  updateEquipmentTemplate,
} from '@/api/equipment';
import { ProcessListItem, getProcesses } from '@/api/process';
import useLocale from '@/utils/useLocale';
import locale from '../../locale';
import TemplateValueEditor from './TemplateValueEditor';

const FormItem = Form.Item;
const TextArea = Input.TextArea;
const Option = Select.Option;

interface Props {
  visible: boolean;
  /** 所属设备类型 ID */
  typeId: string;
  /** 编辑模式：现有模板（null = 新建） */
  editing: EquipmentTemplateDto | null;
  onClose: () => void;
  onSuccess: () => void;
}

export default function TemplateFormModal({
  visible,
  typeId,
  editing,
  onClose,
  onSuccess,
}: Props) {
  const t = useLocale(locale);
  const [form] = Form.useForm();
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');

  const [processes, setProcesses] = useState<ProcessListItem[]>([]);
  const [parameters, setParameters] = useState<EquipmentTypeParameterDto[]>([]);
  const [paramsLoading, setParamsLoading] = useState(false);
  // 受控的参数值（独立于 form）
  const [values, setValues] = useState<TemplateValueDto[]>([]);
  // 编辑模式：后端返回的带状态值（用于 invalid/orphan 标记）
  const [existingValues, setExistingValues] = useState<
    EquipmentTemplateValueDto[] | undefined
  >(undefined);

  // 拉启用工序列表（下拉用）
  useEffect(() => {
    if (visible) {
      getProcesses({ page: 1, pageSize: 100, isActive: true })
        .then((res) => setProcesses(res.items || []))
        .catch(() => setProcesses([]));
    }
  }, [visible]);

  // 拉父类型的参数定义（行来源）+ 编辑模式下回填值
  useEffect(() => {
    if (!visible || !typeId) return;
    setParamsLoading(true);
    getEquipmentTypeById(typeId)
      .then((detail) => {
        setParameters(detail.parameters || []);
        if (editing) {
          // 编辑模式：再次拉模板详情拿到带状态的值
          getEquipmentTemplateById(typeId, editing.id)
            .then((tpl) => {
              form.setFieldsValue({
                processId: tpl.processId,
                name: tpl.name,
                remark: tpl.remark,
                sortOrder: tpl.sortOrder,
              });
              const vals: TemplateValueDto[] = (tpl.values || []).map((v) => ({
                parameterId: v.parameterId,
                value: v.value,
              }));
              setValues(vals);
              setExistingValues(tpl.values);
            })
            .catch(() => Message.error(t['equipment.template.message.loadFailed']));
        } else {
          form.resetFields();
          form.setFieldValue('sortOrder', 0);
          setValues([]);
          setExistingValues(undefined);
        }
      })
      .catch(() => Message.error(t['equipment.template.message.loadFailed']))
      .finally(() => setParamsLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [visible, typeId, editing]);

  const handleOk = async () => {
    try {
      const formValues = (await form.validate()) as Pick<
        CreateEquipmentTemplateRequest,
        'processId' | 'name' | 'remark' | 'sortOrder'
      >;
      // 校验必填参数
      const missingNames = parameters
        .filter((p) => p.required)
        .filter((p) => {
          const v = values.find((x) => x.parameterId === p.id);
          return !v || v.value == null || v.value === '';
        })
        .map((p) => p.name);
      if (missingNames.length > 0) {
        setErrorMsg(
          `${t['equipment.template.message.paramRequired']}${missingNames.join('、')}`
        );
        return;
      }
      setConfirmLoading(true);
      setErrorMsg('');
      const payload: CreateEquipmentTemplateRequest = {
        ...formValues,
        values,
      };
      if (editing) {
        await updateEquipmentTemplate(typeId, editing.id, payload);
        Message.success(t['equipment.template.message.updateSuccess']);
      } else {
        await createEquipmentTemplate(typeId, payload);
        Message.success(t['equipment.template.message.createSuccess']);
      }
      onSuccess();
      onClose();
    } catch (err: any) {
      const msg = err?.response?.data?.message || err?.message || '';
      setErrorMsg(msg);
    } finally {
      setConfirmLoading(false);
    }
  };

  return (
    <Modal
      title={
        editing
          ? t['equipment.template.form.title.edit']
          : t['equipment.template.form.title.create']
      }
      visible={visible}
      onOk={handleOk}
      onCancel={onClose}
      confirmLoading={confirmLoading}
      unmountOnExit
      style={{ width: 720 }}
    >
      {errorMsg && <Alert type="error" content={errorMsg} style={{ marginBottom: 16 }} />}
      <Spin loading={paramsLoading}>
        <Form form={form} layout="vertical">
          <FormItem
            label={t['equipment.template.form.process']}
            field="processId"
            rules={[{ required: true, message: t['equipment.template.form.process.required'] }]}
          >
            <Select
              placeholder={t['equipment.template.form.process.placeholder']}
              allowClear
              showSearch
            >
              {processes.map((p) => (
                <Option key={p.id} value={p.id}>
                  {p.name}
                </Option>
              ))}
            </Select>
          </FormItem>
          <FormItem
            label={t['equipment.template.form.name']}
            field="name"
            rules={[{ required: true }]}
          >
            <Input maxLength={100} />
          </FormItem>
          <FormItem label={t['equipment.template.form.sortOrder']} field="sortOrder" initialValue={0}>
            <InputNumber min={0} style={{ width: '100%' }} />
          </FormItem>
          <FormItem label={t['equipment.template.form.remark']} field="remark">
            <TextArea maxLength={500} />
          </FormItem>
          <FormItem label={t['equipment.template.form.values']}>
            <TemplateValueEditor
              parameters={parameters}
              values={values}
              existingValues={existingValues}
              onChange={setValues}
            />
          </FormItem>
        </Form>
      </Spin>
    </Modal>
  );
}
