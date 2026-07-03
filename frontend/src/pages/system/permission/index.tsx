import React, { useEffect, useState } from 'react';
import { Table, Typography, Card } from '@arco-design/web-react';
import useLocale from '@/utils/useLocale';
import { getPermissionList, PermissionItem } from '@/api/permission';
import locale from './locale';

const { Title } = Typography;

export default function PermissionList() {
  const t = useLocale(locale);
  const [data, setData] = useState<PermissionItem[]>([]);
  const [loading, setLoading] = useState(false);

  useEffect(() => {
    setLoading(true);
    getPermissionList()
      .then(setData)
      .catch(() => {})
      .finally(() => setLoading(false));
  }, []);

  const columns = [
    { title: t['permission.code'], dataIndex: 'code', width: 200 },
    { title: t['permission.name'], dataIndex: 'name', width: 150 },
    { title: t['permission.description'], dataIndex: 'description' },
  ];

  return (
    <Card>
      <Title heading={6}>{t['permission.title']}</Title>

      <Table
        rowKey="id"
        columns={columns}
        data={data}
        loading={loading}
        pagination={false}
      />
    </Card>
  );
}
