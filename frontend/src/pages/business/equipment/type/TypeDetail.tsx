import { Descriptions, Drawer, Empty, Table, Typography } from '@arco-design/web-react';
import {
  EquipmentTypeDto,
  EquipmentTypeParameterDto,
} from '@/api/equipment';
import useLocale from '@/utils/useLocale';
import locale from '../locale';

const { Title } = Typography;

export default function EquipmentTypeDetailDrawer({
  visible,
  data,
  onClose,
}: {
  visible: boolean;
  data: EquipmentTypeDto | null;
  onClose: () => void;
}) {
  const t = useLocale(locale);

  const paramColumns = [
    {
      title: t['equipment.type.detail.column.index'],
      key: 'index',
      width: 60,
      render: (_: unknown, _record: EquipmentTypeParameterDto, idx: number) => idx + 1,
    },
    { title: t['equipment.type.param.name'], dataIndex: 'name' },
    {
      title: t['equipment.type.param.valueType'],
      dataIndex: 'valueType',
      render: (vt: string) => t[`equipment.type.param.valueType.${vt.toLowerCase()}`],
    },
    {
      title: t['equipment.type.param.required'],
      dataIndex: 'required',
      render: (v: boolean) => (v ? '✓' : '-'),
    },
    { title: t['equipment.type.param.unit'], dataIndex: 'unitSymbol', render: (v?: string) => v || '-' },
    { title: t['equipment.type.form.remark'], dataIndex: 'remark', render: (v?: string) => v || '-' },
  ];

  return (
    <Drawer
      title={t['equipment.type.detail.title']}
      visible={visible}
      onCancel={onClose}
      footer={null}
      width={640}
    >
      {data && (
        <>
          <Descriptions
            column={1}
            data={[
              { label: t['equipment.type.column.code'], value: data.code },
              { label: t['equipment.type.column.name'], value: data.name },
              {
                label: t['equipment.type.column.status'],
                value: data.isActive ? t['equipment.type.active'] : t['equipment.type.inactive'],
              },
              { label: t['equipment.type.form.remark'], value: data.remark || '-' },
              { label: t['equipment.type.column.createdAt'], value: data.createdAt },
            ]}
            style={{ marginBottom: 24 }}
          />

          <Title heading={6} style={{ marginBottom: 12 }}>
            {t['equipment.type.detail.parameters']}
          </Title>
          <Table
            size="small"
            pagination={false}
            rowKey="id"
            columns={paramColumns}
            data={data.parameters || []}
            style={{ marginBottom: 24 }}
          />

          <Title heading={6} style={{ marginBottom: 12 }}>
            {t['equipment.type.detail.templates']}
            {`（${data.templateCount ?? (data.templates?.length || 0)}）`}
          </Title>
          {data.templates && data.templates.length > 0 ? (
            <Table
              size="small"
              pagination={false}
              rowKey="id"
              columns={[
                { title: t['equipment.type.column.name'], dataIndex: 'name' },
                { title: '工序', dataIndex: 'processName' },
                { title: t['equipment.type.column.sortOrder'], dataIndex: 'sortOrder' },
              ]}
              data={data.templates}
            />
          ) : (
            <Empty description={t['equipment.type.detail.templates.empty']} />
          )}
        </>
      )}
    </Drawer>
  );
}
