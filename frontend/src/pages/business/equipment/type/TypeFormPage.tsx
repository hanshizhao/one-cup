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
  Spin,
  Switch,
  Typography,
} from '@arco-design/web-react';
import {
  CreateEquipmentTypeRequest,
  ParameterDefinitionDto,
  createEquipmentType,
  getEquipmentTypeById,
  updateEquipmentType,
} from '@/api/equipment';
import { useNumberingPreview } from '@/components/Numbering/useNumberingPreview';
import CategorySelect from '@/components/Numbering/CategorySelect';
import useLocale from '@/utils/useLocale';
import locale from '../locale';
import ParameterEditor from './ParameterEditor';
import styles from '../style/index.module.less';

const { Title } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;
const TextArea = Input.TextArea;

/**
 * 设备类型新建/编辑页面（页面化表单）。
 * 路由：/business/equipment/type/create（新建）、/business/equipment/type/edit/:id（编辑）。
 */
export default function EquipmentTypeFormPage() {
  const t = useLocale(locale);
  const navigate = useNavigate();
  const { id } = useParams<{ id?: string }>();
  const editing = !!id; // 有 id = 编辑模式

  const [form] = Form.useForm();
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  const [pageLoading, setPageLoading] = useState(editing); // 编辑模式需 fetch
  const [code, setCode] = useState<string | undefined>(); // 编号（编辑=实际值，新建=预览值）
  const [parameters, setParameters] = useState<ParameterDefinitionDto[]>([]);
  const preview = useNumberingPreview('equipment-type');

  // 编辑模式：fetch 现有数据
  useEffect(() => {
    if (!editing || !id) return;
    setPageLoading(true);
    getEquipmentTypeById(id)
      .then((detail) => {
        setCode(detail.code);
        form.setFieldsValue({
          name: detail.name,
          remark: detail.remark,
          isActive: detail.isActive,
          sortOrder: detail.sortOrder,
        });
        setParameters(detail.parameters || []);
      })
      .catch(() => Message.error(t['equipment.type.message.loadFailed']))
      .finally(() => setPageLoading(false));
  }, [id, editing, form]);

  // 新建模式：预览编号
  useEffect(() => {
    if (!editing) {
      preview.reload();
      form.setFieldValue('isActive', true);
      form.setFieldValue('sortOrder', 0);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editing]);

  const handleSave = async () => {
    try {
      const values = (await form.validate()) as Omit<
        CreateEquipmentTypeRequest,
        'parameters' | 'categoryCode'
      >;
      const hasBlankName = parameters.some((p) => !p.name || !p.name.trim());
      if (hasBlankName) {
        setErrorMsg(t['equipment.type.message.paramNameRequired']);
        return;
      }
      setConfirmLoading(true);
      setErrorMsg('');
      const payload = { ...values, parameters };
      if (editing && id) {
        await updateEquipmentType(id, payload);
        Message.success(t['equipment.type.message.updateSuccess']);
      } else {
        await createEquipmentType({ ...payload, categoryCode: preview.categoryCode });
        Message.success(t['equipment.type.message.createSuccess']);
      }
      navigate(-1);
    } catch (err: any) {
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

  if (pageLoading) {
    return <Spin style={{ display: 'block', margin: '60px auto' }} />;
  }

  const pageTitle = editing
    ? t['equipment.type.form.page.title.edit']
    : t['equipment.type.form.page.title.create'];

  return (
    <div>
      {/* 页内面包屑 */}
      <Breadcrumb style={{ marginBottom: 12 }}>
        <Breadcrumb.Item>{t['equipment.tab.type']}</Breadcrumb.Item>
        <Breadcrumb.Item>{pageTitle}</Breadcrumb.Item>
      </Breadcrumb>

      {/* 页头 */}
      <div className={styles['form-page-head']}>
        <div>
          <Title heading={5} style={{ marginBottom: 4 }}>
            {pageTitle}
            {code && <span className={styles['form-page-code']}>{code}</span>}
          </Title>
          <div className={styles['form-page-sub']}>
            {editing
              ? t['equipment.type.form.page.sub.edit']
              : t['equipment.type.form.page.sub.create']}
          </div>
        </div>
        <div className={styles['form-page-actions']}>
          <Button onClick={() => navigate(-1)} style={{ marginRight: 8 }}>
            {t['equipment.type.form.page.cancel']}
          </Button>
          <Button
            type="primary"
            loading={confirmLoading}
            disabled={!editing && preview.noRule}
            onClick={handleSave}
          >
            {t['equipment.type.form.page.save']}
          </Button>
        </div>
      </div>

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
        {/* 基础信息 */}
        <Card className={styles['form-page-card']}>
          <Row gutter={24}>
            <Col span={8}>
              <FormItem label={t['equipment.type.form.code']}>
                <Input
                  value={(editing ? code : preview.code) ?? undefined}
                  readOnly
                  placeholder={
                    preview.codeLoading
                      ? t['equipment.type.form.code.previewing']
                      : t['equipment.type.form.code.placeholder']
                  }
                />
              </FormItem>
            </Col>
            <Col span={8}>
              <FormItem label={t['equipment.type.form.name']} field="name" rules={[{ required: true }]}>
                <Input maxLength={50} />
              </FormItem>
            </Col>
            <Col span={8}>
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
            </Col>
          </Row>
          <Row gutter={24}>
            <Col span={8}>
              <FormItem label={t['equipment.type.form.sortOrder']} field="sortOrder" initialValue={0}>
                <InputNumber min={0} style={{ width: '100%' }} />
              </FormItem>
            </Col>
            <Col span={8}>
              <FormItem label={t['equipment.type.form.isActive']} field="isActive" triggerPropName="checked">
                <Switch />
              </FormItem>
            </Col>
            <Col span={8}>
              <FormItem label={t['equipment.type.form.remark']} field="remark">
                <Input maxLength={500} />
              </FormItem>
            </Col>
          </Row>
        </Card>

        {/* 参数定义 */}
        <Card className={styles['form-page-card']} title={t['equipment.type.form.parameters']}>
          <FormItem>
            <ParameterEditor value={parameters} onChange={setParameters} />
          </FormItem>
        </Card>
      </Form>
    </div>
  );
}
