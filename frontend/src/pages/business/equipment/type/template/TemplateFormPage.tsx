import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Alert,
  Breadcrumb,
  Button,
  Card,
  Empty,
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
  EquipmentTypeListItemDto,
  EquipmentTypeParameterDto,
  TemplateValueDto,
  createEquipmentTemplate,
  getActiveEquipmentTypes,
  getEquipmentTemplateByIdTopLevel,
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
const Option = Select.Option;

/**
 * 运行模板新建/编辑页面（页面化表单）。
 * 路由：/business/equipment/template/create、/business/equipment/template/edit/:id。
 * 设备类型在表单内选择（新建可选 / 编辑锁定），不再由路由携带 typeId。
 */
export default function EquipmentTemplateFormPage() {
  const t = useLocale(locale);
  const navigate = useNavigate();
  const { id } = useParams<{ id?: string }>();
  const editing = !!id;

  const [form] = Form.useForm();
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  const [pageLoading, setPageLoading] = useState(editing);

  const [processes, setProcesses] = useState<ProcessListItem[]>([]);
  const [types, setTypes] = useState<EquipmentTypeListItemDto[]>([]);
  const [selectedTypeId, setSelectedTypeId] = useState<string>('');
  const [parameters, setParameters] = useState<EquipmentTypeParameterDto[]>([]);
  const [typeName, setTypeName] = useState<string>('');
  const [values, setValues] = useState<TemplateValueDto[]>([]);
  const [existingValues, setExistingValues] = useState<EquipmentTemplateValueDto[] | undefined>(undefined);

  // 挂载：拉工序 + 启用的设备类型（类型选择器用）
  useEffect(() => {
    getProcesses({ page: 1, pageSize: 100, isActive: true })
      .then((res) => setProcesses(res.items || []))
      .catch(() => {});
    getActiveEquipmentTypes()
      .then(setTypes)
      .catch(() => {});
  }, []);

  // 编辑模式：用顶层详情端点拉模板（路由已无 typeId），读到 typeId 后拉参数定义 + 回填
  useEffect(() => {
    if (!editing || !id) return;
    setPageLoading(true);
    getEquipmentTemplateByIdTopLevel(id)
      .then(async (tpl) => {
        setSelectedTypeId(tpl.equipmentTypeId);
        form.setFieldsValue({
          equipmentTypeId: tpl.equipmentTypeId,
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
        // 拉参数定义（用于渲染值表单 + 校验）
        const typeDetail = await getEquipmentTypeById(tpl.equipmentTypeId);
        setParameters(typeDetail.parameters || []);
        setTypeName(typeDetail.name);
      })
      .catch(() => Message.error(t['equipment.template.message.loadFailed']))
      .finally(() => setPageLoading(false));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [id, editing]);

  // 新建模式：选中类型后拉参数定义；切换类型清空已填值（编辑模式不受影响，编辑的 typeId 走上面的 effect）
  useEffect(() => {
    if (!selectedTypeId || editing) return;
    setValues([]);
    setExistingValues(undefined);
    getEquipmentTypeById(selectedTypeId)
      .then((detail) => {
        setParameters(detail.parameters || []);
        setTypeName(detail.name);
      })
      .catch(() => Message.error(t['equipment.template.message.loadFailed']));
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [selectedTypeId, editing]);

  const handleSave = async () => {
    try {
      if (!selectedTypeId) {
        setErrorMsg(t['equipment.template.tab.selectTypeFirst']);
        return;
      }
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
        await updateEquipmentTemplate(selectedTypeId, id, payload);
        Message.success(t['equipment.template.message.updateSuccess']);
      } else {
        await createEquipmentTemplate(selectedTypeId, payload);
        Message.success(t['equipment.template.message.createSuccess']);
      }
      navigate('/business/equipment?tab=template');
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
        <Breadcrumb.Item>{t['equipment.tab.template']}</Breadcrumb.Item>
        <Breadcrumb.Item>{pageTitle}</Breadcrumb.Item>
      </Breadcrumb>

      <div className={styles['form-page-head']}>
        <div>
          <Title heading={5} style={{ marginBottom: 4 }}>
            {pageTitle}
          </Title>
          <div className={styles['form-page-sub']}>
            {editing
              ? t['equipment.template.form.page.sub'].replace('{type}', typeName)
              : t['equipment.template.tab.selectTypeFirst']}
          </div>
        </div>
        <div className={styles['form-page-actions']}>
          <Button onClick={() => navigate('/business/equipment?tab=template')} style={{ marginRight: 8 }}>
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
                label={t['equipment.item.form.type']}
                field="equipmentTypeId"
                rules={[{ required: true, message: t['equipment.item.form.type.required'] }]}
              >
                <Select
                  placeholder={t['equipment.item.form.type.placeholder']}
                  showSearch
                  disabled={editing}
                  onChange={(v: string) => setSelectedTypeId(v)}
                >
                  {types.map((tp) => (
                    <Option key={tp.id} value={tp.id}>
                      {tp.name}
                    </Option>
                  ))}
                </Select>
              </FormItem>
            </Col>
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
          </Row>
          <Row gutter={24}>
            <Col span={6}>
              <FormItem label={t['equipment.template.form.remark']} field="remark">
                <Input maxLength={500} />
              </FormItem>
            </Col>
          </Row>
        </Card>

        <Card className={styles['form-page-card']} title={t['equipment.template.form.values']}>
          {selectedTypeId && parameters.length > 0 ? (
            <FormItem>
              <TemplateValueEditor
                parameters={parameters}
                values={values}
                existingValues={existingValues}
                onChange={setValues}
              />
            </FormItem>
          ) : selectedTypeId ? (
            <Empty description={t['equipment.template.form.values.empty']} />
          ) : (
            <Empty description={t['equipment.template.tab.selectTypeFirst']} />
          )}
        </Card>
      </Form>
    </div>
  );
}
