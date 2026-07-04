import { useEffect, useState, useCallback, useMemo, useRef } from 'react';
import {
  Table, Button, Drawer, Form, Input, InputNumber, Select, Switch,
  Tag, Popconfirm, Message, Space, Alert, Typography, Card, Grid,
} from '@arco-design/web-react';
import type { PaginationProps } from '@arco-design/web-react';
import { IconPlus, IconSearch, IconRefresh, IconSwap } from '@arco-design/web-react/icon';
import useLocale from '@/utils/useLocale';
import {
  getUnits, createUnit, updateUnit, updateUnitStatus,
  getAllActiveUnits, getUnitCategories, convertUnit,
  MeasurementUnit, CreateUnitRequest, ConvertResult,
} from '@/api/measurementUnit';
import locale from './locale';
import styles from './style/index.module.less';

const { Title } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;

const DEFAULT_FORM: CreateUnitRequest = {
  code: '', nameZh: '', nameEn: '', symbol: '', category: '',
  isBase: false, factor: 1, precision: 2, sortOrder: 0,
};

export default function UnitManagementPage() {
  const t = useLocale(locale);
  const [searchForm] = Form.useForm();
  const [editForm] = Form.useForm();
  const [convertForm] = Form.useForm();

  // ── 列表 ──
  const [data, setData] = useState<MeasurementUnit[]>([]);
  const [loading, setLoading] = useState(false);
  const [pagination, setPagination] = useState({ current: 1, pageSize: 10, total: 0 });
  const [filters, setFilters] = useState<{ keyword?: string; category?: string; isActive?: boolean }>({});
  const [categoryOptions, setCategoryOptions] = useState<string[]>([]);

  // ── 编辑抽屉 ──
  const [editVisible, setEditVisible] = useState(false);
  const [editMode, setEditMode] = useState<'create' | 'edit'>('create');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editIsBase, setEditIsBase] = useState(false);

  // ── 换算抽屉 ──
  const [convertVisible, setConvertVisible] = useState(false);
  const [allUnits, setAllUnits] = useState<MeasurementUnit[]>([]);
  const [convertResult, setConvertResult] = useState<ConvertResult | null>(null);
  const [convertLoading, setConvertLoading] = useState(false);
  // 换算请求 300ms 防抖（spec §4.6）
  const convertTimer = useRef<ReturnType<typeof setTimeout>>();

  // ── 拉取列表 ──
  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const res = await getUnits({
        page: pagination.current,
        pageSize: pagination.pageSize,
        ...filters,
      });
      setData(res.items);
      setPagination((p) => ({ ...p, total: res.total }));
    } catch {
      // request 拦截器已处理错误提示
    } finally {
      setLoading(false);
    }
  }, [pagination.current, pagination.pageSize, filters]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  // 拉取类别选项
  useEffect(() => {
    getUnitCategories().then(setCategoryOptions).catch(() => {});
  }, []);

  // ── 查询 ──
  function handleSearch() {
    const v = searchForm.getFieldsValue();
    setFilters({
      keyword: v.keyword || undefined,
      category: v.category || undefined,
      isActive: v.isActive === undefined ? undefined : v.isActive === 'true',
    });
    setPagination((p) => ({ ...p, current: 1 }));
  }

  function handleReset() {
    searchForm.resetFields();
    setFilters({});
    setPagination((p) => ({ ...p, current: 1 }));
  }

  // ── 编辑 ──
  function openCreate() {
    setEditMode('create');
    setEditingId(null);
    setEditIsBase(false);
    editForm.resetFields();
    editForm.setFieldsValue(DEFAULT_FORM);
    setEditVisible(true);
  }

  function openEdit(record: MeasurementUnit) {
    setEditMode('edit');
    setEditingId(record.id);
    setEditIsBase(record.isBase);
    editForm.resetFields();
    editForm.setFieldsValue({
      nameZh: record.nameZh, nameEn: record.nameEn, symbol: record.symbol,
      isBase: record.isBase, factor: record.factor,
      precision: record.precision, sortOrder: record.sortOrder,
    });
    setEditVisible(true);
  }

  async function handleEditOk() {
    try {
      const values = await editForm.validate();
      if (editMode === 'create') {
        await createUnit(values as CreateUnitRequest);
        Message.success(t['unit.form.createSuccess']);
      } else {
        // isBase=true 时不传 factor（后端强制 1）
        const payload = { ...values };
        if (values.isBase) delete payload.factor;
        await updateUnit(editingId!, payload);
        Message.success(t['unit.form.updateSuccess']);
      }
      setEditVisible(false);
      fetchData();
    } catch {
      // 校验失败或 API 错误
    }
  }

  async function handleToggleStatus(record: MeasurementUnit) {
    try {
      await updateUnitStatus(record.id, !record.isActive);
      Message.success(t['unit.form.statusSuccess']);
      fetchData();
    } catch {
      // ignore
    }
  }

  // ── 换算 ──
  async function openConvert() {
    setConvertVisible(true);
    setConvertResult(null);
    convertForm.resetFields();
    convertForm.setFieldsValue({ quantity: 1 });
    try {
      const list = await getAllActiveUnits();
      setAllUnits(list);
    } catch {
      // ignore
    }
  }

  async function doConvert() {
    const v = convertForm.getFieldsValue();
    if (!v.fromCode || !v.toCode) {
      setConvertResult(null);
      return;
    }
    setConvertLoading(true);
    try {
      const result = await convertUnit({
        fromCode: v.fromCode, toCode: v.toCode, quantity: v.quantity || 1,
      });
      setConvertResult(result);
    } catch {
      setConvertResult(null);
    } finally {
      setConvertLoading(false);
    }
  }

  // 换算请求防抖：每次字段变化 300ms 后触发一次（spec §4.6）
  const debouncedConvert = useCallback(() => {
    clearTimeout(convertTimer.current);
    convertTimer.current = setTimeout(doConvert, 300);
  }, []);

  // 换算 Drawer：源单位选中后，目标单位只显示同 category
  const fromCode = Form.useWatch('fromCode', convertForm);
  const fromUnit = allUnits.find((u) => u.code === fromCode);
  const toOptions = useMemo(() => {
    if (!fromUnit) return allUnits;
    return allUnits.filter((u) => u.category === fromUnit.category);
  }, [fromUnit, allUnits]);

  // ── 列定义 ──
  const columns = [
    { title: t['unit.col.code'], dataIndex: 'code', width: 110 },
    { title: t['unit.col.symbol'], dataIndex: 'symbol', width: 70 },
    { title: t['unit.col.nameZh'], dataIndex: 'nameZh', width: 90 },
    { title: t['unit.col.nameEn'], dataIndex: 'nameEn', width: 110 },
    { title: t['unit.col.category'], dataIndex: 'category', width: 90, render: (v: string) => <Tag>{v}</Tag> },
    {
      title: t['unit.col.isBase'], dataIndex: 'isBase', width: 70,
      render: (v: boolean) => v ? <Tag color="green">{t['unit.tag.base']}</Tag> : null,
    },
    { title: t['unit.col.factor'], dataIndex: 'factor', width: 100 },
    { title: t['unit.col.precision'], dataIndex: 'precision', width: 60 },
    {
      title: t['unit.col.status'], dataIndex: 'isActive', width: 80,
      render: (v: boolean) => v
        ? <Tag color="green">{t['unit.tag.active']}</Tag>
        : <Tag>{t['unit.tag.inactive']}</Tag>,
    },
    {
      title: t['unit.col.operations'], dataIndex: 'operations', width: 160,
      render: (_: unknown, record: MeasurementUnit) => (
        <Space>
          <Button type="text" size="small" onClick={() => openEdit(record)}>
            {t['unit.action.edit']}
          </Button>
          <Popconfirm
            title={record.isActive ? t['unit.action.disableConfirm'] : t['unit.action.enableConfirm']}
            onOk={() => handleToggleStatus(record)}
          >
            <Button type="text" size="small" status={record.isActive ? 'warning' : 'success'}>
              {record.isActive ? t['unit.action.disable'] : t['unit.action.enable']}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <Card>
      <Title heading={6}>{t['unit.title']}</Title>

      {/* 查询区三件套 */}
      <div className={styles['search-form-wrapper']}>
        <Form
          form={searchForm}
          className={styles['search-form']}
          labelAlign="left"
          labelCol={{ span: 5 }}
          wrapperCol={{ span: 19 }}
        >
          <Row gutter={24}>
            <Col span={8}>
              <FormItem label={t['unit.search.keyword']} field="keyword">
                <Input allowClear placeholder={t['unit.search.keywordPlaceholder']} />
              </FormItem>
            </Col>
            <Col span={8}>
              <FormItem label={t['unit.search.category']} field="category">
                <Select allowClear allowCreate placeholder={t['unit.search.category']}>
                  {categoryOptions.map((c) => (
                    <Select.Option key={c} value={c}>{c}</Select.Option>
                  ))}
                </Select>
              </FormItem>
            </Col>
            <Col span={8}>
              <FormItem label={t['unit.search.status']} field="isActive">
                <Select allowClear placeholder={t['unit.search.allStatus']}>
                  <Select.Option value="true">{t['unit.tag.active']}</Select.Option>
                  <Select.Option value="false">{t['unit.tag.inactive']}</Select.Option>
                </Select>
              </FormItem>
            </Col>
          </Row>
        </Form>
        <div className={styles['right-button']}>
          <Button type="primary" icon={<IconSearch />} onClick={handleSearch}>
            {t['unit.search.submit']}
          </Button>
          <Button icon={<IconRefresh />} onClick={handleReset}>
            {t['unit.search.reset']}
          </Button>
        </div>
      </div>

      {/* 工具栏 */}
      <div className={styles['button-group']}>
        <Space>
          <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
            {t['unit.toolbar.create']}
          </Button>
        </Space>
        <Space>
          <Button icon={<IconSwap />} onClick={openConvert}>
            {t['unit.toolbar.convert']}
          </Button>
        </Space>
      </div>

      <Table
        rowKey="id"
        columns={columns}
        data={data}
        loading={loading}
        pagination={{
          ...pagination,
          showTotal: true,
          sizeCanChange: true,
          onChange: (current, pageSize) =>
            setPagination((p) => ({ ...p, current, pageSize })),
        } as PaginationProps}
      />

      {/* 新建/编辑抽屉 */}
      <Drawer
        title={editMode === 'create' ? t['unit.form.create'] : t['unit.form.edit']}
        visible={editVisible}
        onOk={handleEditOk}
        onCancel={() => setEditVisible(false)}
        width={480}
        unmountOnExit
      >
        {editMode === 'edit' && (
          <Alert type="info" content={t['unit.form.lockedHint']} style={{ marginBottom: 16 }} />
        )}
        <Form form={editForm} layout="vertical">
          {editMode === 'create' && (
            <>
              <FormItem
                label={t['unit.form.code']}
                field="code"
                rules={[{ required: true, message: t['unit.form.required'] }]}
              >
                <Input placeholder={t['unit.form.codePlaceholder']} />
              </FormItem>
              <FormItem
                label={t['unit.form.category']}
                field="category"
                rules={[{ required: true, message: t['unit.form.required'] }]}
              >
                <Select allowCreate placeholder={t['unit.form.categoryPlaceholder']}>
                  {categoryOptions.map((c) => (
                    <Select.Option key={c} value={c}>{c}</Select.Option>
                  ))}
                </Select>
              </FormItem>
            </>
          )}
          {editMode === 'edit' && (
            <>
              <FormItem label={t['unit.form.code']}>
                <Input disabled value={data.find((x) => x.id === editingId)?.code} />
              </FormItem>
              <FormItem label={t['unit.form.category']}>
                <Input disabled value={data.find((x) => x.id === editingId)?.category} />
              </FormItem>
            </>
          )}
          <FormItem
            label={t['unit.form.nameZh']}
            field="nameZh"
            rules={[{ required: true, message: t['unit.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem
            label={t['unit.form.nameEn']}
            field="nameEn"
            rules={[{ required: true, message: t['unit.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem
            label={t['unit.form.symbol']}
            field="symbol"
            rules={[{ required: true, message: t['unit.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem label={t['unit.form.isBase']} field="isBase" triggerPropName="checked">
            <Switch onChange={(v: boolean) => setEditIsBase(v)} />
          </FormItem>
          <FormItem label={t['unit.form.factor']} field="factor">
            <InputNumber min={0} step={0.00000001} disabled={editIsBase} style={{ width: '100%' }} />
          </FormItem>
          <FormItem label={t['unit.form.precision']} field="precision">
            <InputNumber min={0} max={6} style={{ width: '100%' }} />
          </FormItem>
          <FormItem label={t['unit.form.sortOrder']} field="sortOrder">
            <InputNumber min={0} style={{ width: '100%' }} />
          </FormItem>
        </Form>
      </Drawer>

      {/* 换算抽屉 */}
      <Drawer
        title={t['unit.convert.title']}
        visible={convertVisible}
        onOk={doConvert}
        onCancel={() => setConvertVisible(false)}
        okText={t['unit.search.submit']}
        width={440}
        unmountOnExit
      >
        <Form form={convertForm} layout="vertical">
          <FormItem label={t['unit.convert.from']} field="fromCode">
            <Select allowClear placeholder={t['unit.convert.from']} onChange={debouncedConvert}>
              {Object.entries(
                allUnits.reduce<Record<string, MeasurementUnit[]>>((acc, u) => {
                  (acc[u.category] ??= []).push(u);
                  return acc;
                }, {}),
              ).map(([cat, units]) => (
                <Select.OptGroup key={cat} label={cat}>
                  {units.map((u) => (
                    <Select.Option key={u.code} value={u.code}>
                      {u.nameZh} ({u.symbol})
                    </Select.Option>
                  ))}
                </Select.OptGroup>
              ))}
            </Select>
          </FormItem>
          <FormItem label={t['unit.convert.to']} field="toCode">
            <Select allowClear placeholder={t['unit.convert.to']} onChange={debouncedConvert}>
              {toOptions.map((u) => (
                <Select.Option key={u.code} value={u.code}>
                  {u.nameZh} ({u.symbol})
                </Select.Option>
              ))}
            </Select>
          </FormItem>
          <FormItem label={t['unit.convert.quantity']} field="quantity">
            <InputNumber min={0} style={{ width: '100%' }} onChange={debouncedConvert} />
          </FormItem>
        </Form>
        <div style={{ marginTop: 16 }}>
          <Typography.Text type="secondary">{t['unit.convert.result']}</Typography.Text>
          <div
            style={{
              marginTop: 6, padding: '12px 16px',
              background: 'var(--color-fill-2)', borderRadius: 4,
              fontSize: 18, fontFamily: 'monospace',
            }}
          >
            {convertLoading
              ? '...'
              : convertResult
                ? `${convertForm.getFieldValue('quantity') ?? 1} ${
                    allUnits.find((u) => u.code === convertResult.fromCode)?.symbol ?? ''
                  } = ${convertResult.quantity} ${
                    allUnits.find((u) => u.code === convertResult.toCode)?.symbol ?? ''
                  }`
                : t['unit.convert.resultEmpty']}
          </div>
        </div>
      </Drawer>
    </Card>
  );
}
