import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Alert,
  Breadcrumb,
  Button,
  Card,
  Form,
  Grid,
  Input,
  InputNumber,
  Message,
  Select,
  Spin,
  Typography,
} from '@arco-design/web-react';
import {
  CreateEquipmentTemplateRequest,
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
import styles from '../../style/index.module.less';

const { Title } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;
const TextArea = Input.TextArea;
const Option = Select.Option;

/**
 * 运行模板新建/编辑页面（页面化表单）。
 * 路由：/business/equipment/type/:typeId/template/create、.../template/edit/:id。
 */
export default function EquipmentTemplateFormPage() {
  const t = useLocale(locale);
  const navigate = useNavigate();
  const { typeId, id } = useParams<{ typeId: string; id?: string }>();
  const editing = !!id;

  const [form] = Form.useForm();
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  const [pageLoading, setPageLoading] = useState(true);

  const [processes, setProcesses] = useState<ProcessListItem[]>([]);
  const [parameters, setParameters] = useState<EquipmentTypeParameterDto[]>([]);
  const [typeName, setTypeName] = useState<string>('');
  const [values, setValues] = useState<TemplateValueDto[]>([]);
  const [existingValues, setExistingValues] = useState<EquipmentTemplateValueDto[] | undefined>(undefined);

  // 拉 processes + 父类型参数定义
  useEffect(() => {
    if (!typeId) return;
    setPageLoading(true);
    Promise.all([
      getProcesses({ page: 1, pageSize: 100, isActive: true }),
      getEquipmentTypeById(typeId),
    ])
      .then(([procRes, typeDetail]) => {
        setProcesses(procRes.items || []);
        setParameters(typeDetail.parameters || []);
        setTypeName(typeDetail.name);
        // 编辑模式：再拉模板详情
        if (editing && id) {
          return getEquipmentTemplateById(typeId, id);
        }
        return null;
      })
      .then((tpl) => {
        if (tpl) {
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
        } else {
          form.setFieldValue('sortOrder', 0);
        }
      })
      .catch(() => Message.error(t['equipment.template.message.loadFailed']))
      .finally(() => setPageLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [typeId, id]);

  const handleSave = async () => {
    try {
      const formValues = (await form.validate()) as Pick<
        CreateEquipmentTemplateRequest,
        'processId' | 'name' | 'remark' | 'sortOrder'
      >;
      const missingNames = parameters
        .filter((p) => p.required)
        .filter((p) => {
          const v = values.find((x) => x.parameterId === p.id);
          return !v || v.value == null || v.value === '';
        })
        .map((p) => p.name);
      if (missingNames.length > 0) {
        setErrorMsg(`${t['equipment.template.message.paramRequired']}${missingNames.join('、')}`);
        return;
      }
      setConfirmLoading(true);
      setErrorMsg('');
      const payload: CreateEquipmentTemplateRequest = { ...formValues, values };
      if (editing && id) {
        await updateEquipmentTemplate(typeId!, id, payload);
        Message.success(t['equipment.template.message.updateSuccess']);
      } else {
        await createEquipmentTemplate(typeId!, payload);
        Message.success(t['equipment.template.message.createSuccess']);
      }
      navigate(-1);
    } catch (err: any) {
      const msg = err?.response?.data?.message || err?.message || '';
      setErrorMsg(msg);
    } finally {
      setConfirmLoading(false);
    }
  };

  if (pageLoading) {
    return <Spin style={{ display: 'block', margin: '60px auto' }} />;
  }

  const pageTitle = editing
    ? t['equipment.template.form.page.title.edit']
    : t['equipment.template.form.page.title.create'];

  return (
    <div>
      <Breadcrumb style={{ marginBottom: 12 }}>
        <Breadcrumb.Item>{t['equipment.tab.type']}</Breadcrumb.Item>
        <Breadcrumb.Item>{typeName}</Breadcrumb.Item>
        <Breadcrumb.Item>{pageTitle}</Breadcrumb.Item>
      </Breadcrumb>

      <div className={styles['form-page-head']}>
        <div>
          <Title heading={5} style={{ marginBottom: 4 }}>
            {pageTitle}
          </Title>
          <div className={styles['form-page-sub']}>
            {t['equipment.template.form.page.sub'].replace('{type}', typeName)}
          </div>
        </div>
        <div className={styles['form-page-actions']}>
          <Button onClick={() => navigate(-1)} style={{ marginRight: 8 }}>
            {t['equipment.template.form.page.cancel']}
          </Button>
          <Button type="primary" loading={confirmLoading} onClick={handleSave}>
            {t['equipment.template.form.page.save']}
          </Button>
        </div>
      </div>

      {errorMsg && <Alert type="error" content={errorMsg} style={{ marginBottom: 16 }} />}

      <Form form={form} layout="vertical">
        <Card className={styles['form-page-card']}>
          <Row gutter={24}>
            <Col span={6}>
              <FormItem
                label={t['equipment.template.form.name']}
                field="name"
                rules={[{ required: true }]}
              >
                <Input maxLength={50} />
              </FormItem>
            </Col>
            <Col span={6}>
              <FormItem
                label={t['equipment.template.form.process']}
                field="processId"
                rules={[{ required: true, message: t['equipment.template.form.process.required'] }]}
              >
                <Select placeholder={t['equipment.template.form.process.placeholder']} allowClear showSearch>
                  {processes.map((p) => (
                    <Option key={p.id} value={p.id}>
                      {p.name}
                    </Option>
                  ))}
                </Select>
              </FormItem>
            </Col>
            <Col span={6}>
              <FormItem label={t['equipment.template.form.sortOrder']} field="sortOrder" initialValue={0}>
                <InputNumber min={0} style={{ width: '100%' }} />
              </FormItem>
            </Col>
            <Col span={6}>
              <FormItem label={t['equipment.template.form.remark']} field="remark">
                <Input maxLength={500} />
              </FormItem>
            </Col>
          </Row>
        </Card>

        <Card className={styles['form-page-card']} title={t['equipment.template.form.values']}>
          <FormItem>
            <TemplateValueEditor
              parameters={parameters}
              values={values}
              existingValues={existingValues}
              onChange={setValues}
            />
          </FormItem>
        </Card>
      </Form>
    </div>
  );
}
