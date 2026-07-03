import React, { useEffect, useState, useCallback } from 'react';
import {
  Table, Button, Space, Input, Select, DatePicker, Drawer, Tag, Typography,
  Card, Form, Grid,
} from '@arco-design/web-react';
import type { PaginationProps } from '@arco-design/web-react';
import { IconRefresh, IconSearch } from '@arco-design/web-react/icon';
import RequirePermission from '@/components/RequirePermission';
import {
  getOperationLogs,
  getOperationLog,
  type OperationLogListItem,
  type OperationLogDetail,
  type OperationLogQuery,
} from '@/api/auditLog';
import styles from './style/index.module.less';

const { Title, Paragraph } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;
const { RangePicker } = DatePicker;

const RESULT_OPTIONS = [
  { label: '全部', value: '' },
  { label: '成功', value: 'Success' },
  { label: '失败', value: 'Failed' },
];

const MODULE_OPTIONS = [
  { label: '用户', value: 'User' },
  { label: '角色', value: 'Role' },
  { label: '编号', value: 'Numbering' },
  { label: '认证', value: 'Auth' },
];

export default function OperationLogPage() {
  const [formInstance] = Form.useForm();
  const [data, setData] = useState<OperationLogListItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [query, setQuery] = useState<OperationLogQuery>({ page: 1, pageSize: 10 });
  const [detail, setDetail] = useState<OperationLogDetail | null>(null);
  const [detailVisible, setDetailVisible] = useState(false);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const res = await getOperationLogs(query);
      setData(res.items);
      setTotal(res.total);
    } finally {
      setLoading(false);
    }
  }, [query]);

  useEffect(() => { fetchData(); }, [fetchData]);

  // 标准 2.4：仅按钮触发查询
  const handleSearch = () => {
    const values = formInstance.getFieldsValue();
    setQuery((q) => ({
      ...q,
      keyword: values.keyword || undefined,
      module: values.module || undefined,
      result: values.result || undefined,
      startTime: values.timeRange?.[0] || undefined,
      endTime: values.timeRange?.[1] || undefined,
      page: 1,
    }));
  };

  const handleReset = () => {
    formInstance.resetFields();
    setQuery({ page: 1, pageSize: 10 });
  };

  const onPageChange = (page: number, pageSize: number) =>
    setQuery((q) => ({ ...q, page, pageSize }));

  const openDetail = async (id: string) => {
    const d = await getOperationLog(id);
    setDetail(d);
    setDetailVisible(true);
  };

  const columns = [
    { title: '时间', dataIndex: 'createdAt', width: 170 },
    { title: '用户', dataIndex: 'username', width: 120 },
    { title: '模块', dataIndex: 'module', width: 110 },
    { title: '动作', dataIndex: 'action', width: 120 },
    { title: '目标', dataIndex: 'targetName', width: 140 },
    {
      title: '结果', dataIndex: 'result', width: 90,
      render: (v: string) => v === 'Success'
        ? <Tag color="green">成功</Tag>
        : <Tag color="red">失败</Tag>,
    },
    { title: '状态码', dataIndex: 'statusCode', width: 80 },
    { title: '耗时(ms)', dataIndex: 'durationMs', width: 90 },
    {
      title: '操作', width: 80,
      render: (_: unknown, record: OperationLogListItem) =>
        <Button type="text" size="small" onClick={() => openDetail(record.id)}>详情</Button>,
    },
  ];

  return (
    <RequirePermission resource="system:audit" actions={['view']}>
      <Card>
        <Title heading={6}>操作日志</Title>

        <div className={styles['search-form-wrapper']}>
          <Form
            form={formInstance}
            className={styles['search-form']}
            labelAlign="left"
            labelCol={{ span: 5 }}
            wrapperCol={{ span: 19 }}
          >
            <Row gutter={24}>
              <Col span={8}>
                <FormItem label="关键词" field="keyword">
                  <Input allowClear placeholder="搜索 路径/目标/错误信息" />
                </FormItem>
              </Col>
              <Col span={8}>
                <FormItem label="模块" field="module">
                  <Select allowClear placeholder="选择模块" options={MODULE_OPTIONS} />
                </FormItem>
              </Col>
              <Col span={8}>
                <FormItem label="结果" field="result">
                  <Select allowClear placeholder="选择结果" options={RESULT_OPTIONS} />
                </FormItem>
              </Col>
              <Col span={8}>
                <FormItem label="时间" field="timeRange">
                  <RangePicker showTime style={{ width: '100%' }} />
                </FormItem>
              </Col>
            </Row>
          </Form>
          <div className={styles['right-button']}>
            <Button type="primary" icon={<IconSearch />} onClick={handleSearch}>查询</Button>
            <Button icon={<IconRefresh />} onClick={handleReset}>重置</Button>
          </div>
        </div>

        <div className={styles['button-group']}>
          <Space />
          <Space>
            <Button icon={<IconRefresh />} onClick={fetchData}>刷新</Button>
          </Space>
        </div>

        <Table
          rowKey="id"
          loading={loading}
          columns={columns}
          data={data}
          pagination={{
            current: query.page, pageSize: query.pageSize, total,
            onChange: onPageChange, showTotal: true, sizeCanChange: true,
          } as PaginationProps}
        />

        <Drawer
          title="操作日志详情"
          visible={detailVisible}
          width={640}
          onCancel={() => setDetailVisible(false)}
          footer={null}
        >
          {detail && (
            <div>
              {([
                ['时间', detail.createdAt],
                ['用户', `${detail.username} (${detail.userId ?? '-'})`],
                ['模块/动作', `${detail.module} / ${detail.action}`],
                ['目标', detail.targetName ? `${detail.targetName} (${detail.targetId ?? '-'})` : '-'],
                ['请求', `${detail.httpMethod} ${detail.requestPath}`],
                ['状态码', String(detail.statusCode)],
                ['耗时', `${detail.durationMs} ms`],
                ['IP', detail.ipAddress ?? '-'],
                ['TraceId', detail.traceId ?? '-'],
              ] as [string, string][]).map(([k, v]) => (
                <Paragraph key={k} style={{ marginBottom: 8 }}><b>{k}：</b>{v}</Paragraph>
              ))}
              {detail.errorMessage && (
                <Paragraph><b>错误：</b><span style={{ color: 'red' }}>{detail.errorMessage}</span></Paragraph>
              )}
              {detail.requestPayload && (
                <div>
                  <b>请求体（已脱敏）：</b>
                  <pre style={{ background: '#f5f5f5', padding: 8, borderRadius: 4, maxHeight: 200, overflow: 'auto' }}>
                    {detail.requestPayload}
                  </pre>
                </div>
              )}
              {detail.stackTrace && (
                <div>
                  <b>堆栈：</b>
                  <pre style={{ background: '#fff1f0', padding: 8, borderRadius: 4, maxHeight: 240, overflow: 'auto', fontSize: 12 }}>
                    {detail.stackTrace}
                  </pre>
                </div>
              )}
            </div>
          )}
        </Drawer>
      </Card>
    </RequirePermission>
  );
}
