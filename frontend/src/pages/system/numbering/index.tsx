import { useEffect, useState, useCallback, useMemo } from 'react';
import {
  Table,
  Button,
  Input,
  Drawer,
  Form,
  Select,
  InputNumber,
  Switch,
  Tag,
  Popconfirm,
  Message,
  Space,
  Tabs,
  DatePicker,
  Alert,
  Typography,
  Card,
  Grid,
} from '@arco-design/web-react';
import { IconPlus, IconSearch, IconRefresh } from '@arco-design/web-react/icon';
import useLocale from '@/utils/useLocale';
import {
  getNumberingRules,
  getNumberingRule,
  createNumberingRule,
  updateNumberingRule,
  updateNumberingRuleStatus,
  getNumberingLogs,
  NumberingRuleListItem,
  NumberingRule,
  NumberingLogItem,
  CreateNumberingRuleRequest,
} from '@/api/numbering';
import {
  getAllActiveTargetTypes,
  getActiveCategories,
  TargetType,
} from '@/api/numberingDictionary';
import NumberingDictionary from './dict';
import locale from './locale';
import styles from './style/index.module.less';
import PermissionWrapper from '@/components/PermissionWrapper';

const FormItem = Form.Item;
const { Row, Col } = Grid;
const { RangePicker } = DatePicker;
const { Text, Title } = Typography;

// 日期段 / 重置周期选项（对应后端枚举字符串）
const DATE_SEGMENT_OPTIONS = ['None', 'Year', 'YearMonth', 'YearMonthDay'];
const RESET_PERIOD_OPTIONS = ['None', 'Yearly', 'Monthly', 'Daily'];

// 默认表单值
const DEFAULT_FORM_VALUES: CreateNumberingRuleRequest = {
  targetType: 'fabric',
  name: '',
  prefix: '',
  includeCategory: true,
  dateSegment: 'YearMonth',
  seqLength: 4,
  separator: '-',
  resetPeriod: 'Yearly',
  remark: '',
};

export default function NumberingManagement() {
  const t = useLocale(locale);
  const [ruleForm] = Form.useForm();
  const [logForm] = Form.useForm();
  const [activeTab, setActiveTab] = useState('rules');

  // ───────────────── 动态下拉：业务类型 ─────────────────
  const [targetTypeOptions, setTargetTypeOptions] = useState<TargetType[]>([]);

  useEffect(() => {
    getAllActiveTargetTypes().then(setTargetTypeOptions).catch(() => {});
  }, []);

  // 日志列显示辅助：code → 中文名映射（兜底显示 code 本身，停用项也能显示）
  const targetTypeNameMap = useMemo(() => {
    const m: Record<string, string> = {};
    targetTypeOptions.forEach((it) => {
      m[it.code] = it.nameZh;
    });
    return m;
  }, [targetTypeOptions]);

  // ───────────────── 规则配置 Tab 状态 ─────────────────
  const [ruleData, setRuleData] = useState<NumberingRuleListItem[]>([]);
  const [ruleLoading, setRuleLoading] = useState(false);
  const [rulePagination, setRulePagination] = useState({
    current: 1,
    pageSize: 10,
    total: 0,
  });
  const [keyword, setKeyword] = useState('');
  const [filterTargetType, setFilterTargetType] = useState<string | undefined>(
    undefined,
  );
  const [filterIsActive, setFilterIsActive] = useState<boolean | undefined>(
    undefined,
  );

  // 抽屉状态
  const [editVisible, setEditVisible] = useState(false);
  const [editMode, setEditMode] = useState<'create' | 'edit'>('create');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editLoading, setEditLoading] = useState(false);
  const [editForm] = Form.useForm();
  // 实时预览：表单当前值
  const [formValues, setFormValues] = useState<CreateNumberingRuleRequest>(
    DEFAULT_FORM_VALUES,
  );

  // 动态下拉：分类（依赖表单 targetType）
  const [categoryOptions, setCategoryOptions] = useState<
    { code: string; nameZh: string }[]
  >([]);

  // 当表单 targetType 变化时拉取该类型的分类
  useEffect(() => {
    if (formValues.targetType) {
      getActiveCategories(formValues.targetType)
        .then((list) =>
          setCategoryOptions(
            list.map((c) => ({ code: c.code, nameZh: c.nameZh })),
          ),
        )
        .catch(() => setCategoryOptions([]));
    } else {
      setCategoryOptions([]);
    }
  }, [formValues.targetType]);

  // 编辑模式下当前记录是否启用 → 锁定关键配置字段
  const [editingIsActive, setEditingIsActive] = useState(true);

  // ───────────────── 生成日志 Tab 状态 ─────────────────
  const [logData, setLogData] = useState<NumberingLogItem[]>([]);
  const [logLoading, setLogLoading] = useState(false);
  const [logPagination, setLogPagination] = useState({
    current: 1,
    pageSize: 10,
    total: 0,
  });
  const [logFilter, setLogFilter] = useState({
    targetType: undefined as string | undefined,
    categoryCode: '',
    code: '',
    dateRange: [] as string[],
  });

  // ───────────────── 规则列表拉取 ─────────────────
  const fetchRules = useCallback(async () => {
    setRuleLoading(true);
    try {
      const res = await getNumberingRules({
        page: rulePagination.current,
        pageSize: rulePagination.pageSize,
        keyword: keyword || undefined,
        targetType: filterTargetType,
        isActive: filterIsActive,
      });
      setRuleData(res.items);
      setRulePagination((prev) => ({ ...prev, total: res.total }));
    } catch {
      // request 拦截器已处理错误提示
    } finally {
      setRuleLoading(false);
    }
  }, [rulePagination.current, rulePagination.pageSize, keyword, filterTargetType, filterIsActive]);

  useEffect(() => {
    fetchRules();
  }, [fetchRules]);

  // ───────────────── 实时预览（纯前端拼接）─────────────────
  // 段顺序：前缀 [+ 分类码占位 "CAT"] [+ 日期] [+ 流水号 "0001"]
  // 以 separator 连接（仅在有多个段时）
  const previewCode = useMemo(() => {
    const v = formValues;
    const sep = v.separator ?? '';
    const segments: string[] = [];
    if (v.prefix) segments.push(v.prefix);
    if (v.includeCategory) segments.push(categoryOptions[0]?.code || 'CAT');
    if (v.dateSegment && v.dateSegment !== 'None') {
      const now = new Date();
      const y = now.getFullYear();
      const m = String(now.getMonth() + 1).padStart(2, '0');
      const d = String(now.getDate()).padStart(2, '0');
      let dateStr = '';
      if (v.dateSegment === 'Year') dateStr = `${y}`;
      else if (v.dateSegment === 'YearMonth') dateStr = `${y}${m}`;
      else if (v.dateSegment === 'YearMonthDay') dateStr = `${y}${m}${d}`;
      if (dateStr) segments.push(dateStr);
    }
    const seqLen = Math.max(1, Math.min(8, v.seqLength || 4));
    segments.push(String(1).padStart(seqLen, '0')); // 示例流水号 1
    return segments.join(sep);
  }, [formValues, categoryOptions]);

  // ───────────────── 抽屉打开 ─────────────────
  function openCreate() {
    setEditMode('create');
    setEditingId(null);
    setEditingIsActive(true);
    const defaults = { ...DEFAULT_FORM_VALUES };
    editForm.resetFields();
    editForm.setFieldsValue(defaults);
    setFormValues(defaults);
    setEditVisible(true);
  }

  async function openEdit(record: NumberingRuleListItem) {
    setEditMode('edit');
    setEditingId(record.id);
    setEditingIsActive(record.isActive);
    editForm.resetFields();
    try {
      const detail: NumberingRule = await getNumberingRule(record.id);
      const values: CreateNumberingRuleRequest = {
        targetType: detail.targetType,
        name: detail.name,
        prefix: detail.prefix,
        includeCategory: detail.includeCategory,
        dateSegment: detail.dateSegment,
        seqLength: detail.seqLength,
        separator: detail.separator,
        resetPeriod: detail.resetPeriod,
        remark: detail.remark || '',
      };
      editForm.setFieldsValue(values);
      setFormValues(values);
    } catch {
      // ignore
    }
    setEditVisible(true);
  }

  // ───────────────── 提交 ─────────────────
  async function handleEditOk() {
    try {
      const values: CreateNumberingRuleRequest = await editForm.validate();
      setEditLoading(true);
      if (editMode === 'create') {
        await createNumberingRule(values);
        Message.success(t['numbering.rules.create.success']);
      } else {
        // When editing an active rule, only remark is editable (other fields are disabled in UI).
        // Send only { remark } to avoid the backend's key-field-presence rejection.
        const payload = (editMode === 'edit' && editingIsActive)
          ? { remark: values.remark }
          : {
              name: values.name,
              prefix: values.prefix,
              targetType: values.targetType,
              includeCategory: values.includeCategory,
              dateSegment: values.dateSegment,
              seqLength: values.seqLength,
              separator: values.separator,
              resetPeriod: values.resetPeriod,
              remark: values.remark,
            };
        await updateNumberingRule(editingId!, payload);
        Message.success(t['numbering.rules.update.success']);
      }
      setEditVisible(false);
      fetchRules();
    } catch {
      // 校验失败或 API 错误
    } finally {
      setEditLoading(false);
    }
  }

  // ───────────────── 状态切换 ─────────────────
  async function handleToggleStatus(record: NumberingRuleListItem) {
    try {
      await updateNumberingRuleStatus(record.id, !record.isActive);
      Message.success(t['numbering.rules.status.success']);
      fetchRules();
    } catch {
      // ignore
    }
  }

  // ───────────────── 日志列表拉取 ─────────────────
  const fetchLogs = useCallback(async () => {
    setLogLoading(true);
    try {
      const res = await getNumberingLogs({
        page: logPagination.current,
        pageSize: logPagination.pageSize,
        targetType: logFilter.targetType,
        categoryCode: logFilter.categoryCode || undefined,
        code: logFilter.code || undefined,
        startDate: logFilter.dateRange?.[0],
        endDate: logFilter.dateRange?.[1],
      });
      setLogData(res.items);
      setLogPagination((prev) => ({ ...prev, total: res.total }));
    } catch {
      // ignore
    } finally {
      setLogLoading(false);
    }
  }, [logPagination.current, logPagination.pageSize, logFilter]);

  useEffect(() => {
    if (activeTab === 'logs') {
      fetchLogs();
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [activeTab, logPagination.current, logPagination.pageSize]);

  // ───────────────── 规则列表列定义 ─────────────────
  const ruleColumns = [
    { title: t['numbering.rules.name'], dataIndex: 'name', width: 160 },
    {
      title: t['numbering.rules.targetType'],
      dataIndex: 'targetType',
      width: 120,
      render: (val: string) =>
        t[`numbering.targetType.${val}`] || val,
    },
    { title: t['numbering.rules.prefix'], dataIndex: 'prefix', width: 100 },
    {
      title: t['numbering.rules.sampleFormat'],
      dataIndex: 'sampleFormat',
      render: (val: string) =>
        val ? (
          <Text copyable code>
            {val}
          </Text>
        ) : (
          '-'
        ),
    },
    {
      title: t['numbering.rules.status'],
      dataIndex: 'isActive',
      width: 80,
      render: (isActive: boolean) =>
        isActive ? (
          <Tag color="green">{t['numbering.rules.active']}</Tag>
        ) : (
          <Tag>{t['numbering.rules.inactive']}</Tag>
        ),
    },
    {
      title: t['numbering.rules.operations'],
      dataIndex: 'operations',
      width: 160,
      render: (_: unknown, record: NumberingRuleListItem) => (
        <Space>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'system:numbering', actions: ['update'] }]}
          >
            <Button type="text" size="small" onClick={() => openEdit(record)}>
              {t['numbering.rules.edit']}
            </Button>
          </PermissionWrapper>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'system:numbering', actions: ['update'] }]}
          >
            <Popconfirm
              title={
                record.isActive
                  ? t['numbering.rules.disable.confirm']
                  : t['numbering.rules.enable.confirm']
              }
              onOk={() => handleToggleStatus(record)}
            >
              <Button
                type="text"
                size="small"
                status={record.isActive ? 'warning' : 'success'}
              >
                {record.isActive
                  ? t['numbering.rules.disable']
                  : t['numbering.rules.enable']}
              </Button>
            </Popconfirm>
          </PermissionWrapper>
        </Space>
      ),
    },
  ];

  // ───────────────── 日志列表列定义 ─────────────────
  const logColumns = [
    {
      title: t['numbering.logs.code'],
      dataIndex: 'generatedCode',
      render: (val: string) => (
        <Text copyable code>
          {val}
        </Text>
      ),
    },
    {
      title: t['numbering.logs.targetType'],
      dataIndex: 'targetType',
      width: 120,
      render: (val: string) => targetTypeNameMap[val] || val,
    },
    {
      title: t['numbering.logs.category'],
      dataIndex: 'categoryCode',
      width: 120,
      render: (val?: string) => val || '-',
    },
    {
      title: t['numbering.logs.period'],
      dataIndex: 'periodKey',
      width: 120,
      render: (val?: string) => val || '-',
    },
    {
      title: t['numbering.logs.seq'],
      dataIndex: 'seqValue',
      width: 90,
    },
    {
      title: t['numbering.logs.time'],
      dataIndex: 'createdAt',
      width: 180,
    },
    {
      title: t['numbering.logs.ruleName'],
      dataIndex: 'ruleName',
      width: 160,
      render: (val?: string) => val || '-',
    },
  ];

  return (
    <Card>
      <Title heading={6}>编号管理</Title>
      <Tabs activeTab={activeTab} onChange={setActiveTab}>
        {/* ───────────── 规则配置 Tab ───────────── */}
        <Tabs.TabPane key="rules" title={t['numbering.tab.rules']}>
          <div className={styles['search-form-wrapper']}>
            <Form
              form={ruleForm}
              className={styles['search-form']}
              labelAlign="left"
              labelCol={{ span: 5 }}
              wrapperCol={{ span: 19 }}
            >
              <Row gutter={24}>
                <Col span={8}>
                  <FormItem label="关键词" field="keyword">
                    <Input allowClear placeholder={t['numbering.rules.search']} />
                  </FormItem>
                </Col>
                <Col span={8}>
                  <FormItem label="业务类型" field="targetType">
                    <Select allowClear placeholder={t['numbering.rules.allTargetType']}>
                      {targetTypeOptions.map((tp) => (
                        <Select.Option key={tp.code} value={tp.code}>
                          {tp.nameZh}
                        </Select.Option>
                      ))}
                    </Select>
                  </FormItem>
                </Col>
                <Col span={8}>
                  <FormItem label="状态" field="isActive">
                    <Select allowClear placeholder={t['numbering.rules.allStatus']}>
                      <Select.Option value="true">{t['numbering.rules.active']}</Select.Option>
                      <Select.Option value="false">{t['numbering.rules.inactive']}</Select.Option>
                    </Select>
                  </FormItem>
                </Col>
              </Row>
            </Form>
            <div className={styles['right-button']}>
              <Button type="primary" icon={<IconSearch />} onClick={() => {
                const v = ruleForm.getFieldsValue();
                setKeyword(v.keyword || '');
                setFilterTargetType(v.targetType);
                setFilterIsActive(
                  v.isActive === undefined ? undefined : v.isActive === 'true',
                );
                setRulePagination((p) => ({ ...p, current: 1 }));
              }}>
                查询
              </Button>
              <Button icon={<IconRefresh />} onClick={() => {
                ruleForm.resetFields();
                setKeyword('');
                setFilterTargetType(undefined);
                setFilterIsActive(undefined);
                setRulePagination((p) => ({ ...p, current: 1 }));
              }}>
                重置
              </Button>
            </div>
          </div>

          <div className={styles['button-group']}>
            <Space>
              <PermissionWrapper
                requiredPermissions={[{ resource: 'system:numbering', actions: ['create'] }]}
              >
                <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
                  {t['numbering.rules.create']}
                </Button>
              </PermissionWrapper>
            </Space>
            <Space />
          </div>

          <Table
            rowKey="id"
            columns={ruleColumns}
            data={ruleData}
            loading={ruleLoading}
            pagination={{
              ...rulePagination,
              showTotal: true,
              sizeCanChange: true,
              onChange: (current, pageSize) =>
                setRulePagination((p) => ({ ...p, current, pageSize })),
            }}
          />
        </Tabs.TabPane>

        {/* ───────────── 业务字典 Tab ───────────── */}
        <Tabs.TabPane key="dict" title={t['numbering.tab.dict']}>
          <NumberingDictionary />
        </Tabs.TabPane>

        {/* ───────────── 生成日志 Tab ───────────── */}
        <Tabs.TabPane key="logs" title={t['numbering.tab.logs']}>
          <div className={styles['search-form-wrapper']}>
            <Form
              form={logForm}
              className={styles['search-form']}
              labelAlign="left"
              labelCol={{ span: 5 }}
              wrapperCol={{ span: 19 }}
            >
              <Row gutter={24}>
                <Col span={8}>
                  <FormItem label="业务类型" field="targetType">
                    <Select allowClear placeholder={t['numbering.rules.allTargetType']}>
                      {targetTypeOptions.map((tp) => (
                        <Select.Option key={tp.code} value={tp.code}>
                          {tp.nameZh}
                        </Select.Option>
                      ))}
                    </Select>
                  </FormItem>
                </Col>
                <Col span={8}>
                  <FormItem label="分类码" field="categoryCode">
                    <Input allowClear placeholder={t['numbering.logs.category.placeholder']} />
                  </FormItem>
                </Col>
                <Col span={8}>
                  <FormItem label="编号" field="code">
                    <Input allowClear placeholder={t['numbering.logs.code.placeholder']} />
                  </FormItem>
                </Col>
                <Col span={8}>
                  <FormItem label="时间" field="dateRange">
                    <RangePicker style={{ width: '100%' }} />
                  </FormItem>
                </Col>
              </Row>
            </Form>
            <div className={styles['right-button']}>
              <Button type="primary" icon={<IconSearch />} onClick={() => {
                const v = logForm.getFieldsValue();
                setLogFilter({
                  targetType: v.targetType,
                  categoryCode: v.categoryCode || '',
                  code: v.code || '',
                  dateRange: (v.dateRange as string[]) || [],
                });
                setLogPagination((p) => ({ ...p, current: 1 }));
                // 日志 effect 的依赖刻意排除了 logFilter，故从 page 1 再次查询时需手动触发拉取
                fetchLogs();
              }}>
                {t['numbering.logs.search']}
              </Button>
              <Button icon={<IconRefresh />} onClick={() => {
                logForm.resetFields();
                setLogFilter({
                  targetType: undefined,
                  categoryCode: '',
                  code: '',
                  dateRange: [],
                });
                setLogPagination((p) => ({ ...p, current: 1 }));
              }}>
                {t['numbering.logs.reset']}
              </Button>
            </div>
          </div>

          <Table
            rowKey="id"
            columns={logColumns}
            data={logData}
            loading={logLoading}
            pagination={{
              ...logPagination,
              showTotal: true,
              sizeCanChange: true,
              onChange: (current, pageSize) =>
                setLogPagination((p) => ({ ...p, current, pageSize })),
            }}
          />
        </Tabs.TabPane>
      </Tabs>

      {/* ───────────── 新增/编辑抽屉 ───────────── */}
      <Drawer
        title={
          editMode === 'create'
            ? t['numbering.form.title.create']
            : t['numbering.form.title.edit']
        }
        visible={editVisible}
        onOk={handleEditOk}
        onCancel={() => setEditVisible(false)}
        confirmLoading={editLoading}
        width={520}
        unmountOnExit
      >
        {/* 实时预览 */}
        <div style={{ marginBottom: 16 }}>
          <Text type="secondary">{t['numbering.form.preview']}</Text>
          <div
            style={{
              marginTop: 6,
              padding: '8px 12px',
              background: 'var(--color-fill-2)',
              borderRadius: 4,
              fontFamily: 'monospace',
              fontSize: 16,
              wordBreak: 'break-all',
            }}
          >
            {previewCode || t['numbering.form.preview.empty']}
          </div>
        </div>

        {/* 编辑模式下启用规则锁定提示 */}
        {editMode === 'edit' && editingIsActive && (
          <Alert
            type="warning"
            content={t['numbering.form.lockedHint']}
            style={{ marginBottom: 16 }}
          />
        )}

        <Form
          form={editForm}
          layout="vertical"
          onValuesChange={(_, all) =>
            setFormValues(all as CreateNumberingRuleRequest)
          }
        >
          <FormItem
            label={t['numbering.form.targetType']}
            field="targetType"
            rules={[{ required: true, message: t['numbering.form.required'] }]}
          >
            <Select
              placeholder={t['numbering.form.targetType.placeholder']}
              disabled={editMode === 'edit' && editingIsActive}
              showSearch
              allowCreate
            >
              {targetTypeOptions.map((tp) => (
                <Select.Option key={tp.code} value={tp.code}>
                  {tp.nameZh}
                </Select.Option>
              ))}
            </Select>
          </FormItem>

          <FormItem
            label={t['numbering.form.name']}
            field="name"
            rules={[{ required: true, message: t['numbering.form.required'] }]}
          >
            <Input placeholder={t['numbering.form.name']} />
          </FormItem>

          <FormItem
            label={t['numbering.form.prefix']}
            field="prefix"
            rules={[{ required: true, message: t['numbering.form.required'] }]}
          >
            <Input placeholder={t['numbering.form.prefix']} disabled={editMode === 'edit' && editingIsActive} />
          </FormItem>

          <FormItem
            label={t['numbering.form.includeCategory']}
            field="includeCategory"
            triggerPropName="checked"
          >
            <Switch disabled={editMode === 'edit' && editingIsActive} />
          </FormItem>

          <FormItem label={t['numbering.form.dateSegment']} field="dateSegment">
            <Select disabled={editMode === 'edit' && editingIsActive}>
              {DATE_SEGMENT_OPTIONS.map((ds) => (
                <Select.Option key={ds} value={ds}>
                  {t[`numbering.enum.dateSegment.${ds}`]}
                </Select.Option>
              ))}
            </Select>
          </FormItem>

          <FormItem
            label={t['numbering.form.seqLength']}
            field="seqLength"
            rules={[{ required: true, message: t['numbering.form.required'] }]}
          >
            <InputNumber
              min={1}
              max={8}
              style={{ width: '100%' }}
              disabled={editMode === 'edit' && editingIsActive}
            />
          </FormItem>

          <FormItem label={t['numbering.form.separator']} field="separator">
            <Input
              placeholder={t['numbering.form.separator']}
              disabled={editMode === 'edit' && editingIsActive}
            />
          </FormItem>

          <FormItem label={t['numbering.form.resetPeriod']} field="resetPeriod">
            <Select disabled={editMode === 'edit' && editingIsActive}>
              {RESET_PERIOD_OPTIONS.map((rp) => (
                <Select.Option key={rp} value={rp}>
                  {t[`numbering.enum.resetPeriod.${rp}`]}
                </Select.Option>
              ))}
            </Select>
          </FormItem>

          <FormItem label={t['numbering.form.remark']} field="remark">
            <Input.TextArea placeholder={t['numbering.form.remark']} rows={2} />
          </FormItem>
        </Form>
      </Drawer>
    </Card>
  );
}
