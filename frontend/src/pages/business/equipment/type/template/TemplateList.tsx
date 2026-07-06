import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Badge,
  Button,
  Empty,
  Message,
  Popconfirm,
  Space,
  Table,
} from '@arco-design/web-react';
import { IconPlus } from '@arco-design/web-react/icon';
import {
  EquipmentTemplateListItemDto,
  deleteEquipmentTemplate,
  getEquipmentTemplates,
} from '@/api/equipment';
import useLocale from '@/utils/useLocale';
import PermissionWrapper from '@/components/PermissionWrapper';
import locale from '../../locale';

interface Props {
  /** 所属设备类型 ID */
  typeId: string;
}

/**
 * 运行模板列表（嵌入 TypeDetail 的模板区域）。
 * 列：name、processName、status（徽标 valid/invalid/orphan）、操作（编辑/删除）。
 * 删除走 Popconfirm（c01，单条物理删除）。
 */
export default function TemplateList({ typeId }: Props) {
  const t = useLocale(locale);
  const navigate = useNavigate();
  const [data, setData] = useState<EquipmentTemplateListItemDto[]>([]);
  const [loading, setLoading] = useState(false);

  function fetchData() {
    if (!typeId) return;
    setLoading(true);
    getEquipmentTemplates(typeId)
      .then((items) => setData(items || []))
      .catch(() => Message.error(t['equipment.template.message.loadFailed']))
      .finally(() => setLoading(false));
  }

  useEffect(() => {
    fetchData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [typeId]);

  function openCreate() {
    navigate(`/business/equipment/type/${typeId}/template/create`);
  }
  function openEdit(record: EquipmentTemplateListItemDto) {
    navigate(`/business/equipment/type/${typeId}/template/edit/${record.id}`);
  }
  async function handleDelete(record: EquipmentTemplateListItemDto) {
    try {
      await deleteEquipmentTemplate(typeId, record.id);
      Message.success(t['equipment.template.message.deleteSuccess']);
      fetchData();
    } catch {
      Message.error(t['equipment.template.message.loadFailed']);
    }
  }

  const statusBadge = (status?: string) => {
    if (status === 'invalid') {
      return <Badge status="error" text={t['equipment.template.status.invalid']} />;
    }
    if (status === 'orphan') {
      return <Badge status="warning" text={t['equipment.template.status.orphan']} />;
    }
    return <Badge status="success" text={t['equipment.template.status.valid']} />;
  };

  const columns = useMemo(
    () => [
      { title: t['equipment.template.column.name'], dataIndex: 'name' },
      { title: t['equipment.template.column.process'], dataIndex: 'processName' },
      {
        title: t['equipment.template.column.status'],
        dataIndex: 'status',
        render: (status?: string) => statusBadge(status),
      },
      { title: t['equipment.template.column.sortOrder'], dataIndex: 'sortOrder' },
      {
        title: t['equipment.template.column.operations'],
        dataIndex: 'operations',
        render: (_: unknown, record: EquipmentTemplateListItemDto) => (
          <Space>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'equipment-type', actions: ['update'] }]}
            >
              <Button type="text" size="small" onClick={() => openEdit(record)}>
                {t['equipment.template.button.edit']}
              </Button>
            </PermissionWrapper>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'equipment-type', actions: ['delete'] }]}
            >
              <Popconfirm
                title={t['equipment.template.message.deleteOk']}
                onOk={() => handleDelete(record)}
              >
                <Button type="text" size="small" status="danger">
                  {t['equipment.template.button.delete']}
                </Button>
              </Popconfirm>
            </PermissionWrapper>
          </Space>
        ),
      },
    ],
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [t]
  );

  return (
    <div>
      <div style={{ display: 'flex', justifyContent: 'flex-end', marginBottom: 12 }}>
        <PermissionWrapper
          requiredPermissions={[{ resource: 'equipment-type', actions: ['update'] }]}
        >
          <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
            {t['equipment.template.button.create']}
          </Button>
        </PermissionWrapper>
      </div>
      {data.length === 0 && !loading ? (
        <Empty description={t['equipment.template.empty']} />
      ) : (
        <Table
          size="small"
          pagination={false}
          rowKey="id"
          loading={loading}
          columns={columns}
          data={data}
        />
      )}
    </div>
  );
}
