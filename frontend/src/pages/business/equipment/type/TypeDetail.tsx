import { Descriptions, Drawer, Table, Tag, Typography } from '@arco-design/web-react';
import {
  EquipmentTypeDto,
  EquipmentTypeParameterDto,
} from '@/api/equipment';
import useLocale from '@/utils/useLocale';
import locale from '../locale';
import TemplateList from './template/TemplateList';

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
      width: 50,
      render: (_: unknown, _record: EquipmentTypeParameterDto, idx: number) => idx + 1,
    },
    { title: t['equipment.type.param.name'], dataIndex: 'name', width: 100 },
    {
      title: t['equipment.type.param.valueType'],
      dataIndex: 'valueType',
      width: 70,
      render: (vt: string) => (
        <Tag
          size="small"
          color={vt === 'Number' ? 'arcoblue' : vt === 'Enum' ? 'green' : 'gray'}
        >
          {t[`equipment.type.param.valueType.${vt.toLowerCase()}`]}
        </Tag>
      ),
    },
    {
      title: t['equipment.type.param.required'],
      dataIndex: 'required',
      width: 50,
      render: (v: boolean) => (v ? '✓' : '-'),
    },
    {
      title: t['equipment.type.detail.column.constraint'],
      key: 'constraint',
      render: (_: unknown, record: EquipmentTypeParameterDto) => {
        if (record.valueType !== 'Number') return <span style={{ color: 'var(--color-text-4)' }}>—</span>;
        const range =
          record.minValue != null || record.maxValue != null
            ? `${record.minValue ?? '−∞'} ~ ${record.maxValue ?? '+∞'}`
            : t['equipment.type.param.preview.noRange'];
        const unit = record.unitSymbol ? ` ${record.unitSymbol}` : '';
        const prec = record.precision != null ? ` · ${t['equipment.template.value.precisionLabel']} ${record.precision}` : '';
        return <span style={{ fontSize: 12 }}>{range}{unit}<span style={{ color: 'var(--color-text-4)' }}>{prec}</span></span>;
      },
    },
    {
      title: t['equipment.type.detail.column.options'],
      key: 'options',
      render: (_: unknown, record: EquipmentTypeParameterDto) => {
        if (record.valueType !== 'Enum') return <span style={{ color: 'var(--color-text-4)' }}>—</span>;
        return (
          <span>
            {(record.options || []).map((o) => (
              <Tag key={o} size="small" style={{ marginRight: 4 }}>
                {o}
              </Tag>
            ))}
          </span>
        );
      },
    },
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
          <TemplateList typeId={data.id} />
        </>
      )}
    </Drawer>
  );
}
