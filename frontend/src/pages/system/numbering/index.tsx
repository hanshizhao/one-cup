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
import locale from './locale';

const FormItem = Form.Item;
const { RangePicker } = DatePicker;
const { Text } = Typography;

// 业务类型选项（来自后端 NumberTargetTypes，仅作下拉提示，不强制）
const TARGET_TYPE_OPTIONS = [
  'fabric',
  'material',
  'equipment',
  'customer',
  'color',
  'product',
];

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
  const [activeTab, setActiveTab] = useState('rules');

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
    if (v.includeCategory) segments.push('CAT');
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
  }, [formValues]);

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
        // 编辑模式下若规则启用则仅可改备注（关键字段已置灰，这里照常提交被锁定的值）
        await updateNumberingRule(editingId!, {
          name: values.name,
          prefix: values.prefix,
          targetType: values.targetType,
          includeCategory: values.includeCategory,
          dateSegment: values.dateSegment,
          seqLength: values.seqLength,
          separator: values.separator,
          resetPeriod: values.resetPeriod,
          remark: values.remark,
        });
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
          <Button type="text" size="small" onClick={() => openEdit(record)}>
            {t['numbering.rules.edit']}
          </Button>
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
      render: (val: string) => t[`numbering.targetType.${val}`] || val,
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
    <div>
      <Tabs activeTab={activeTab} onChange={setActiveTab}>
        {/* ───────────── 规则配置 Tab ───────────── */}
        <Tabs.TabPane key="rules" title={t['numbering.tab.rules']}>
          <Space
            style={{
              marginBottom: 16,
              width: '100%',
              justifyContent: 'space-between',
            }}
          >
            <Space>
              <Input.Search
                placeholder={t['numbering.rules.search']}
                onSearch={(v) => {
                  setKeyword(v);
                  setRulePagination((p) => ({ ...p, current: 1 }));
                }}
                style={{ width: 240 }}
                prefix={<IconSearch />}
                allowClear
              />
              <Select
                placeholder={t['numbering.rules.allTargetType']}
                style={{ width: 150 }}
                allowClear
                value={filterTargetType}
                onChange={(v) => {
                  setFilterTargetType(v);
                  setRulePagination((p) => ({ ...p, current: 1 }));
                }}
              >
                {TARGET_TYPE_OPTIONS.map((tp) => (
                  <Select.Option key={tp} value={tp}>
                    {t[`numbering.targetType.${tp}`] || tp}
                  </Select.Option>
                ))}
              </Select>
              <Select
                placeholder={t['numbering.rules.allStatus']}
                style={{ width: 130 }}
                allowClear
                value={filterIsActive}
                onChange={(v) => {
                  setFilterIsActive(v);
                  setRulePagination((p) => ({ ...p, current: 1 }));
                }}
              >
                <Select.Option value={true}>
                  {t['numbering.rules.active']}
                </Select.Option>
                <Select.Option value={false}>
                  {t['numbering.rules.inactive']}
                </Select.Option>
              </Select>
            </Space>
            <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
              {t['numbering.rules.create']}
            </Button>
          </Space>

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

        {/* ───────────── 生成日志 Tab ───────────── */}
        <Tabs.TabPane key="logs" title={t['numbering.tab.logs']}>
          <Space style={{ marginBottom: 16 }} wrap>
            <Select
              placeholder={t['numbering.rules.allTargetType']}
              style={{ width: 150 }}
              allowClear
              value={logFilter.targetType}
              onChange={(v) =>
                setLogFilter((f) => ({ ...f, targetType: v }))
              }
            >
              {TARGET_TYPE_OPTIONS.map((tp) => (
                <Select.Option key={tp} value={tp}>
                  {t[`numbering.targetType.${tp}`] || tp}
                </Select.Option>
              ))}
            </Select>
            <Input
              placeholder={t['numbering.logs.category.placeholder']}
              style={{ width: 160 }}
              value={logFilter.categoryCode}
              onChange={(v) =>
                setLogFilter((f) => ({ ...f, categoryCode: v }))
              }
              allowClear
            />
            <Input
              placeholder={t['numbering.logs.code.placeholder']}
              style={{ width: 200 }}
              value={logFilter.code}
              onChange={(v) => setLogFilter((f) => ({ ...f, code: v }))}
              allowClear
            />
            <RangePicker
              style={{ width: 260 }}
              value={logFilter.dateRange as never}
              onChange={(_, dateStrings) =>
                setLogFilter((f) => ({ ...f, dateRange: dateStrings }))
              }
            />
            <Button
              type="primary"
              icon={<IconSearch />}
              onClick={() => {
                setLogPagination((p) => ({ ...p, current: 1 }));
                // 因依赖 logFilter 变化，直接触发拉取
                fetchLogs();
              }}
            >
              {t['numbering.logs.search']}
            </Button>
            <Button
              icon={<IconRefresh />}
              onClick={() => {
                setLogFilter({
                  targetType: undefined,
                  categoryCode: '',
                  code: '',
                  dateRange: [],
                });
                setLogPagination((p) => ({ ...p, current: 1 }));
              }}
            >
              {t['numbering.logs.reset']}
            </Button>
          </Space>

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
          onValuesChange={(_, all) => setFormValues(all)}
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
              {TARGET_TYPE_OPTIONS.map((tp) => (
                <Select.Option key={tp} value={tp}>
                  {t[`numbering.targetType.${tp}`] || tp}
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
    </div>
  );
}
