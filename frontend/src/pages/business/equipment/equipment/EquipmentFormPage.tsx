import { useEffect, useState } from 'react';
import { useNavigate, useParams } from 'react-router-dom';
import {
  Alert,
  Breadcrumb,
  Button,
  Card,
  DatePicker,
  Form,
  Grid,
  Input,
  InputNumber,
  Message,
  Radio,
  Select,
  Spin,
  Switch,
  Typography,
} from '@arco-design/web-react';
import {
  CreateEquipmentRequest,
  EQUIPMENT_STATUSES,
  EquipmentTypeListItemDto,
  createEquipment,
  getActiveEquipmentTypes,
  getEquipmentById,
  updateEquipment,
} from '@/api/equipment';
import { useNumberingPreview } from '@/components/Numbering/useNumberingPreview';
import CategorySelect from '@/components/Numbering/CategorySelect';
import useLocale from '@/utils/useLocale';
import locale from '../locale';
import styles from '../style/index.module.less';

const { Title } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;
const TextArea = Input.TextArea;
const Option = Select.Option;

/**
 * 设备实例新建/编辑页面（页面化表单）。
 * 路由：/business/equipment/create、/business/equipment/edit/:id。
 */
export default function EquipmentFormPage() {
  const t = useLocale(locale);
  const navigate = useNavigate();
  const { id } = useParams<{ id?: string }>();
  const editing = !!id;

  const [form] = Form.useForm();
  const [confirmLoading, setConfirmLoading] = useState(false);
  const [errorMsg, setErrorMsg] = useState('');
  const [pageLoading, setPageLoading] = useState(editing);
  const [code, setCode] = useState<string | undefined>();
  const [types, setTypes] = useState<EquipmentTypeListItemDto[]>([]);
  const preview = useNumberingPreview('equipment');

  // 拉类型列表（下拉用）
  useEffect(() => {
    getActiveEquipmentTypes()
      .then(setTypes)
      .catch(() => {});
  }, []);

  // 编辑模式：fetch 现有数据
  useEffect(() => {
    if (!editing || !id) return;
    setPageLoading(true);
    getEquipmentById(id)
      .then((detail) => {
        setCode(detail.code);
        form.setFieldsValue({
          name: detail.name,
          equipmentTypeId: detail.equipmentTypeId,
          specification: detail.specification,
          supplier: detail.supplier,
          location: detail.location,
          status: detail.status,
          purchaseDate: detail.purchaseDate,
          warrantyExpiry: detail.warrantyExpiry,
          remark: detail.remark,
          isActive: detail.isActive,
          sortOrder: detail.sortOrder,
        });
      })
      .catch(() => Message.error(t['equipment.item.message.loadFailed']))
      .finally(() => setPageLoading(false));
  }, [id, editing, form]);

  // 新建模式：预览编号
  useEffect(() => {
    if (!editing) {
      preview.reload();
      form.setFieldValue('isActive', true);
      form.setFieldValue('status', 'Running');
      form.setFieldValue('sortOrder', 0);
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [editing]);

  const handleSave = async () => {
    try {
      const values = (await form.validate()) as Omit<CreateEquipmentRequest, 'categoryCode'>;
      setConfirmLoading(true);
      setErrorMsg('');
      if (editing && id) {
        await updateEquipment(id, values);
        Message.success(t['equipment.item.message.updateSuccess']);
      } else {
        await createEquipment({ ...values, categoryCode: preview.categoryCode });
        Message.success(t['equipment.item.message.createSuccess']);
      }
      navigate(-1);
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

  if (pageLoading) {
    return <Spin style={{ display: 'block', margin: '60px auto' }} />;
  }

  const pageTitle = editing
    ? t['equipment.item.form.page.title.edit']
    : t['equipment.item.form.page.title.create'];

  return (
    <div>
      <Breadcrumb style={{ marginBottom: 12 }}>
        <Breadcrumb.Item>{t['equipment.tab.equipment']}</Breadcrumb.Item>
        <Breadcrumb.Item>{pageTitle}</Breadcrumb.Item>
      </Breadcrumb>

      <div className={styles['form-page-head']}>
        <div>
          <Title heading={5} style={{ marginBottom: 4 }}>
            {pageTitle}
            {code && <span className={styles['form-page-code']}>{code}</span>}
          </Title>
          <div className={styles['form-page-sub']}>
            {editing
              ? t['equipment.item.form.page.sub.edit']
              : t['equipment.item.form.page.sub.create']}
          </div>
        </div>
        <div className={styles['form-page-actions']}>
          <Button onClick={() => navigate(-1)} style={{ marginRight: 8 }}>
            {t['equipment.item.form.page.cancel']}
          </Button>
          <Button
            type="primary"
            loading={confirmLoading}
            disabled={!editing && preview.noRule}
            onClick={handleSave}
          >
            {t['equipment.item.form.page.save']}
          </Button>
        </div>
      </div>

      {!editing && preview.noRule && (
        <Alert type="warning" content={t['equipment.item.form.noRule.block']} style={{ marginBottom: 16 }} />
      )}
      {errorMsg && <Alert type="error" content={errorMsg} style={{ marginBottom: 16 }} />}

      <Form form={form} layout="vertical" disabled={!editing && preview.noRule}>
        <Card className={styles['form-page-card']}>
          <Row gutter={24}>
            <Col span={8}>
              <FormItem label={t['equipment.item.form.code']}>
                <Input
                  value={(editing ? code : preview.code) ?? undefined}
                  readOnly
                  placeholder={
                    preview.codeLoading
                      ? t['equipment.item.form.code.previewing']
                      : t['equipment.item.form.code.placeholder']
                  }
                />
              </FormItem>
            </Col>
            <Col span={8}>
              <FormItem label={t['equipment.item.form.name']} field="name" rules={[{ required: true }]}>
                <Input maxLength={50} />
              </FormItem>
            </Col>
            <Col span={8}>
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
            </Col>
          </Row>

          {!editing && preview.includeCategory && (
            <Row gutter={24}>
              <Col span={8}>
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
              </Col>
            </Row>
          )}

          <Row gutter={24}>
            <Col span={8}>
              <FormItem label={t['equipment.item.form.spec']} field="specification">
                <Input maxLength={200} />
              </FormItem>
            </Col>
            <Col span={8}>
              <FormItem label={t['equipment.item.form.supplier']} field="supplier">
                <Input maxLength={100} />
              </FormItem>
            </Col>
            <Col span={8}>
              <FormItem label={t['equipment.item.form.location']} field="location">
                <Input maxLength={100} />
              </FormItem>
            </Col>
          </Row>

          <Row gutter={24}>
            <Col span={8}>
              <FormItem label={t['equipment.item.form.status']} field="status" rules={[{ required: true }]}>
                <Radio.Group>
                  {EQUIPMENT_STATUSES.map((s) => (
                    <Radio key={s} value={s}>
                      {t[`equipment.item.status.${s.toLowerCase()}`]}
                    </Radio>
                  ))}
                </Radio.Group>
              </FormItem>
            </Col>
            <Col span={8}>
              <FormItem label={t['equipment.item.form.isActive']} field="isActive" triggerPropName="checked">
                <Switch />
              </FormItem>
            </Col>
            <Col span={8}>
              <FormItem label={t['equipment.item.form.sortOrder']} field="sortOrder" initialValue={0}>
                <InputNumber min={0} style={{ width: '100%' }} />
              </FormItem>
            </Col>
          </Row>

          <Row gutter={24}>
            <Col span={8}>
              <FormItem label={t['equipment.item.form.purchaseDate']} field="purchaseDate">
                <DatePicker style={{ width: '100%' }} />
              </FormItem>
            </Col>
            <Col span={8}>
              <FormItem label={t['equipment.item.form.warrantyExpiry']} field="warrantyExpiry">
                <DatePicker style={{ width: '100%' }} />
              </FormItem>
            </Col>
          </Row>

          <Row gutter={24}>
            <Col span={24}>
              <FormItem label={t['equipment.item.form.remark']} field="remark">
                <TextArea maxLength={500} />
              </FormItem>
            </Col>
          </Row>
        </Card>
      </Form>
    </div>
  );
}
