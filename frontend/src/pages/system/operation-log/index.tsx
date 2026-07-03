import React, { useEffect, useState, useCallback } from 'react';
import { Table, Button, Space, Input, Select, DatePicker, Drawer, Tag, Typography } from '@arco-design/web-react';
import type { PaginationProps } from '@arco-design/web-react';
import { IconRefresh } from '@arco-design/web-react/icon';
import RequirePermission from '@/components/RequirePermission';
import {
  getOperationLogs,
  getOperationLog,
  type OperationLogListItem,
  type OperationLogDetail,
  type OperationLogQuery,
} from '@/api/auditLog';

const { RangePicker } = DatePicker;
const { Paragraph } = Typography;

const RESULT_OPTIONS = [
  { label: '全部', value: '' },
  { label: '成功', value: 'Success' },
  { label: '失败', value: 'Failed' },
];

export default function OperationLogPage() {
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

  const onPageChange = (page: number, pageSize: number) => setQuery(q => ({ ...q, page, pageSize }));

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
      <div style={{ padding: 16 }}>
        <Space style={{ marginBottom: 16 }} wrap>
          <Input.Search
            allowClear
            placeholder="搜索 路径/目标/错误信息"
            style={{ width: 260 }}
            onSearch={(v) => setQuery(q => ({ ...q, keyword: v || undefined, page: 1 }))}
          />
          <Select
            placeholder="模块"
            allowClear
            style={{ width: 140 }}
            onChange={(v) => setQuery(q => ({ ...q, module: v || undefined, page: 1 }))}
          />
          <Select
            placeholder="结果"
            style={{ width: 120 }}
            options={RESULT_OPTIONS}
            onChange={(v) => setQuery(q => ({ ...q, result: (v || undefined) as OperationLogQuery['result'], page: 1 }))}
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
      </div>
    </RequirePermission>
  );
}
