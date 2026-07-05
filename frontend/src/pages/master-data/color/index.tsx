import { useEffect, useMemo, useState, useCallback } from 'react';
import {
  Alert, Button, Card, Drawer, Form, Grid, Input, InputNumber, Select, Space,
  Table, Tag, Popconfirm, Message, Typography,
} from '@arco-design/web-react';
import { IconPlus, IconRefresh, IconSearch } from '@arco-design/web-react/icon';
import useLocale from '@/utils/useLocale';
import PermissionWrapper from '@/components/PermissionWrapper';
import {
  getColors, createColor, updateColor, updateColorStatus,
  Color,
} from '@/api/color';
import { useNumberingPreview } from '@/components/Numbering/useNumberingPreview';
import CategorySelect from '@/components/Numbering/CategorySelect';
import locale from './locale';
import styles from './style/index.module.less';

const { Title } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;
const { Option } = Select;

// 颜色系常用项（写死 + allowCreate）
const FAMILY_OPTIONS = [
  'red', 'orange', 'yellow', 'green', 'blue', 'purple', 'neutral', 'gray',
];

const HEX_PATTERN = /^#[0-9A-Fa-f]{6}$/;

function SearchForm({ onSearch }: { onSearch: (v: Record<string, any>) => void }) {
  const t = useLocale(locale);
  const [form] = Form.useForm();

  const handleSubmit = () => onSearch(form.getFieldsValue());
  const handleReset = () => { form.resetFields(); onSearch({}); };

  return (
    <div className={styles['search-form-wrapper']}>
      <Form
        form={form}
        className={styles['search-form']}
        labelAlign="left"
        labelCol={{ span: 8 }}
        wrapperCol={{ span: 16 }}
      >
        <Row gutter={24}>
          <Col span={8}>
            <FormItem label={t['color.search.keyword']} field="keyword">
              <Input allowClear placeholder="" />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['color.search.colorFamily']} field="colorFamily">
              <Select allowClear showSearch allowCreate>
                {FAMILY_OPTIONS.map((f) => (
                  <Option key={f} value={t[`color.family.${f}`]}>{t[`color.family.${f}`]}</Option>
                ))}
              </Select>
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['color.search.status']} field="isActive">
              <Select allowClear>
                <Option value="true">{t['color.active']}</Option>
                <Option value="false">{t['color.inactive']}</Option>
              </Select>
            </FormItem>
          </Col>
        </Row>
      </Form>
      <div className={styles['right-button']}>
        <Button type="primary" icon={<IconSearch />} onClick={handleSubmit}>
          {t['color.search.submit']}
        </Button>
        <Button icon={<IconRefresh />} onClick={handleReset}>
          {t['color.search.reset']}
        </Button>
      </div>
    </div>
  );
}

export default function ColorPage() {
  const t = useLocale(locale);
  const [data, setData] = useState<Color[]>([]);
  const [loading, setLoading] = useState(false);
  const [formParams, setFormParams] = useState<Record<string, any>>({});
  const [pagination, setPagination] = useState({
    sizeCanChange: true, showTotal: true, pageSize: 10, current: 1,
    pageSizeChangeResetCurrent: true,
  });

  // 抽屉
  const [drawerVisible, setDrawerVisible] = useState(false);
  const [editMode, setEditMode] = useState<'create' | 'edit'>('create');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [form] = Form.useForm();
  // 编辑态编号展示（新建态编号由 preview.code 提供）
  const [editingCode, setEditingCode] = useState<string | null>(null);
  // 新建模式：编号预览 + 分类码自判（规则驱动）
  const preview = useNumberingPreview('color');

  const fetchColors = useCallback(() => {
    const { current, pageSize } = pagination;
    setLoading(true);
    getColors({ page: current, pageSize, ...formParams })
      .then((res) => {
        setData(res.items);
        setPagination((p) => ({ ...p, total: res.total }));
      })
      .finally(() => setLoading(false));
  }, [pagination.current, pagination.pageSize, JSON.stringify(formParams)]);

  useEffect(() => { fetchColors(); }, [fetchColors]);

  function handleSearch(params: Record<string, any>) {
    setPagination((p) => ({ ...p, current: 1 }));
    setFormParams(params);
  }

  function onChangeTable(paginationState: { current?: number; pageSize?: number }) {
    const { current, pageSize } = paginationState;
    setPagination((p) => ({ ...p, current: current ?? p.current, pageSize: pageSize ?? p.pageSize }));
  }

  function openCreate() {
    setEditMode('create');
    setEditingId(null);
    setEditingCode(null);
    form.resetFields();
    form.setFieldsValue({ sortOrder: 0 });
    preview.reload();
    setDrawerVisible(true);
  }

  function openEdit(record: Color) {
    setEditMode('edit');
    setEditingId(record.id);
    setEditingCode(record.code);
    form.resetFields();
    form.setFieldsValue({
      nameZh: record.nameZh, nameEn: record.nameEn, hex: record.hex,
      colorFamily: record.colorFamily, sortOrder: record.sortOrder, remark: record.remark,
    });
    setDrawerVisible(true);
  }

  async function handleDrawerOk() {
    try {
      const values = await form.validate();
      if (editMode === 'create') {
        await createColor({ ...values, categoryCode: preview.categoryCode });
        Message.success(t['color.create.success']);
      } else {
        await updateColor(editingId!, {
          nameZh: values.nameZh, nameEn: values.nameEn, hex: values.hex,
          colorFamily: values.colorFamily, sortOrder: values.sortOrder, remark: values.remark,
        });
        Message.success(t['color.update.success']);
      }
      setDrawerVisible(false);
      fetchColors();
    } catch {
      // 校验失败或 API 错误
    }
  }

  async function handleToggleStatus(record: Color) {
    await updateColorStatus(record.id, !record.isActive);
    Message.success(t['color.status.success']);
    fetchColors();
  }

  const columns = useMemo(() => [
    {
      title: t['color.column.swatch'], dataIndex: 'hex', width: 70,
      render: (hex: string) => (
        <span className={styles['color-swatch']} style={{ background: hex }} />
      ),
    },
    { title: t['color.column.code'], dataIndex: 'code', width: 120 },
    { title: t['color.column.nameZh'], dataIndex: 'nameZh', width: 120 },
    { title: t['color.column.nameEn'], dataIndex: 'nameEn', width: 120 },
    { title: t['color.column.colorFamily'], dataIndex: 'colorFamily', width: 100 },
    { title: t['color.column.sortOrder'], dataIndex: 'sortOrder', width: 80 },
    {
      title: t['color.column.status'], dataIndex: 'isActive', width: 90,
      render: (v: boolean) => v
        ? <Tag color="green">{t['color.active']}</Tag>
        : <Tag>{t['color.inactive']}</Tag>,
    },
    {
      title: t['color.column.operations'], dataIndex: 'operations', width: 160,
      render: (_: unknown, record: Color) => (
        <Space>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'color', actions: ['update'] }]}
          >
            <Button type="text" size="small" onClick={() => openEdit(record)}>
              {t['color.edit']}
            </Button>
          </PermissionWrapper>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'color', actions: ['update'] }]}
          >
            <Popconfirm
              title={record.isActive
                ? t['color.disable.confirm'] : t['color.enable.confirm']}
              onOk={() => handleToggleStatus(record)}
            >
              <Button type="text" size="small" status={record.isActive ? 'warning' : 'success'}>
                {record.isActive ? t['color.disable'] : t['color.enable']}
              </Button>
            </Popconfirm>
          </PermissionWrapper>
        </Space>
      ),
    },
  ], [t]);

  return (
    <Card>
      <Title heading={6}>{t['color.title']}</Title>
      <SearchForm onSearch={handleSearch} />
      <div className={styles['button-group']}>
        <Space>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'color', actions: ['create'] }]}
          >
            <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
              {t['color.create']}
            </Button>
          </PermissionWrapper>
        </Space>
        <Space />
      </div>
      <Table
        rowKey="id"
        loading={loading}
        onChange={onChangeTable}
        pagination={pagination}
        columns={columns}
        data={data}
      />

      <Drawer
        title={editMode === 'create' ? t['color.form.create'] : t['color.form.edit']}
        visible={drawerVisible}
        onOk={handleDrawerOk}
        onCancel={() => setDrawerVisible(false)}
        okButtonProps={{ disabled: editMode === 'create' && preview.noRule }}
        width={440}
        unmountOnExit
      >
        {editMode === 'create' && preview.noRule && (
          <Alert type="warning" content={t['color.form.noRule.block']} style={{ marginBottom: 16 }} />
        )}
        <Form form={form} layout="vertical" disabled={editMode === 'create' && preview.noRule}>
          <FormItem label={t['color.form.code']}>
            <Input
              value={(editMode === 'edit' ? editingCode : preview.code) ?? undefined}
              readOnly
              placeholder={preview.codeLoading ? t['color.form.code.previewing'] : t['color.form.code.placeholder']}
            />
          </FormItem>
          {editMode === 'create' && preview.includeCategory && (
            <FormItem label={t['color.form.category']} field="categoryCode">
              <CategorySelect
                options={preview.categoryOptions}
                value={preview.categoryCode}
                onChange={preview.setCategoryCode}
                loading={preview.codeLoading}
                placeholder={t['color.form.category.placeholder']}
              />
            </FormItem>
          )}
          <FormItem
            label={t['color.form.nameZh']}
            field="nameZh"
            rules={[{ required: true, message: t['color.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem
            label={t['color.form.nameEn']}
            field="nameEn"
            rules={[{ required: true, message: t['color.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem
            label={t['color.form.hex']}
            field="hex"
            rules={[
              { required: true, message: t['color.form.required'] },
              {
                validator: (v, cb) =>
                  !v || HEX_PATTERN.test(v) ? cb() : cb(t['color.form.hex.invalid']),
              },
            ]}
          >
            <Input placeholder={t['color.form.hex.placeholder']} />
          </FormItem>
          <FormItem label={t['color.form.colorFamily']} field="colorFamily">
            <Select showSearch allowCreate>
              {FAMILY_OPTIONS.map((f) => (
                <Option key={f} value={t[`color.family.${f}`]}>{t[`color.family.${f}`]}</Option>
              ))}
            </Select>
          </FormItem>
          <FormItem label={t['color.form.sortOrder']} field="sortOrder">
            <InputNumber min={0} style={{ width: '100%' }} />
          </FormItem>
          <FormItem label={t['color.form.remark']} field="remark">
            <Input.TextArea />
          </FormItem>
        </Form>
      </Drawer>
    </Card>
  );
}
