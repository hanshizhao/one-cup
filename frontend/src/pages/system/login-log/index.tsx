import React, { useEffect, useState, useCallback } from 'react';
import { Table, Button, Space, Input, Select, DatePicker, Tag } from '@arco-design/web-react';
import type { PaginationProps } from '@arco-design/web-react';
import { IconRefresh } from '@arco-design/web-react/icon';
import RequirePermission from '@/components/RequirePermission';
import { getLoginLogs, type LoginLogItem, type LoginLogQuery } from '@/api/auditLog';

const { RangePicker } = DatePicker;

const EVENT_OPTIONS = [
  { label: '全部', value: '' },
  { label: '登录', value: 'Login' },
  { label: '登出', value: 'Logout' },
  { label: '刷新', value: 'Refresh' },
  { label: '锁定', value: 'Locked' },
];

const RESULT_OPTIONS = [
  { label: '全部', value: '' },
  { label: '成功', value: 'Success' },
  { label: '失败', value: 'Failed' },
];

const EVENT_LABEL: Record<string, string> = { Login: '登录', Logout: '登出', Refresh: '刷新', Locked: '锁定' };

export default function LoginLogPage() {
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

  const onPageChange = (page: number, pageSize: number) => setQuery(q => ({ ...q, page, pageSize }));

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
      <div style={{ padding: 16 }}>
        <Space style={{ marginBottom: 16 }} wrap>
          <Input
            allowClear
            placeholder="账号模糊查询"
            style={{ width: 180 }}
            onPressEnter={(e) => setQuery(q => ({ ...q, username: (e.target as HTMLInputElement).value || undefined, page: 1 }))}
          />
          <Select
            placeholder="事件"
            allowClear
            style={{ width: 120 }}
            options={EVENT_OPTIONS}
            onChange={(v) => setQuery(q => ({ ...q, eventType: (v || undefined) as LoginLogQuery['eventType'], page: 1 }))}
          />
          <Select
            placeholder="结果"
            style={{ width: 120 }}
            options={RESULT_OPTIONS}
            onChange={(v) => setQuery(q => ({ ...q, result: (v || undefined) as LoginLogQuery['result'], page: 1 }))}
          />
          <RangePicker
            showTime
            onChange={(range) => setQuery(q => ({
              ...q,
              startTime: range?.[0] || undefined,
              endTime: range?.[1] || undefined,
              page: 1,
            }))}
          />
          <Button icon={<IconRefresh />} onClick={fetchData}>刷新</Button>
        </Space>

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
      </div>
    </RequirePermission>
  );
}
