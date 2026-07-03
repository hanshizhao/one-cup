import React, { useEffect, useState, useCallback } from 'react';
import {
  Table, Button, Space, Input, Select, DatePicker, Tag, Typography,
  Card, Form, Grid,
} from '@arco-design/web-react';
import type { PaginationProps } from '@arco-design/web-react';
import { IconRefresh, IconSearch } from '@arco-design/web-react/icon';
import RequirePermission from '@/components/RequirePermission';
import { getLoginLogs, type LoginLogItem, type LoginLogQuery } from '@/api/auditLog';
import styles from './style/index.module.less';

const { Title } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;
const { RangePicker } = DatePicker;

const EVENT_OPTIONS = [
  { label: '登录', value: 'Login' },
  { label: '登出', value: 'Logout' },
  { label: '刷新', value: 'Refresh' },
  { label: '锁定', value: 'Locked' },
];

const RESULT_OPTIONS = [
  { label: '成功', value: 'Success' },
  { label: '失败', value: 'Failed' },
];

const EVENT_LABEL: Record<string, string> = { Login: '登录', Logout: '登出', Refresh: '刷新', Locked: '锁定' };

export default function LoginLogPage() {
  const [formInstance] = Form.useForm();
  const [data, setData] = useState<LoginLogItem[]>([]);
  const [total, setTotal] = useState(0);
  const [loading, setLoading] = useState(false);
  const [query, setQuery] = useState<LoginLogQuery>({ page: 1, pageSize: 10 });

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const res = await getLoginLogs(query);
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
      username: values.username || undefined,
      eventType: values.eventType || undefined,
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

  const columns = [
    { title: '时间', dataIndex: 'createdAt', width: 170 },
    { title: '账号', dataIndex: 'username', width: 140 },
    { title: '事件', dataIndex: 'eventType', width: 90, render: (v: string) => EVENT_LABEL[v] ?? v },
    {
      title: '结果', dataIndex: 'result', width: 90,
      render: (v: string) => v === 'Success'
        ? <Tag color="green">成功</Tag>
        : <Tag color="red">失败</Tag>,
    },
    { title: '失败原因', dataIndex: 'failureReason', width: 150 },
    { title: 'IP', dataIndex: 'ipAddress', width: 140 },
    { title: 'User-Agent', dataIndex: 'userAgent', ellipsis: true },
  ];

  return (
    <RequirePermission resource="system:audit" actions={['view']}>
      <Card>
        <Title heading={6}>登录日志</Title>

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
                <FormItem label="账号" field="username">
                  <Input allowClear placeholder="账号模糊查询" />
                </FormItem>
              </Col>
              <Col span={8}>
                <FormItem label="事件" field="eventType">
                  <Select allowClear placeholder="选择事件" options={EVENT_OPTIONS} />
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
          <Space />
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
      </Card>
    </RequirePermission>
  );
}
