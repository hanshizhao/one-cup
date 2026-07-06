import { useEffect, useMemo, useState } from 'react';
import {
  Button,
  Card,
  Form,
  Grid,
  Input,
  Message,
  Popconfirm,
  Select,
  Space,
  Table,
  Tag,
  Typography,
} from '@arco-design/web-react';
import { IconPlus, IconRefresh, IconSearch } from '@arco-design/web-react/icon';
import {
  EQUIPMENT_STATUSES,
  EquipmentDto,
  EquipmentListItemDto,
  EquipmentStatus,
  EquipmentTypeListItemDto,
  deleteEquipment,
  getActiveEquipmentTypes,
  getEquipmentById,
  getEquipments,
} from '@/api/equipment';
import useLocale from '@/utils/useLocale';
import PermissionWrapper from '@/components/PermissionWrapper';
import locale from '../locale';
import styles from '../style/index.module.less';
import EquipmentFormModal from './EquipmentForm';
import EquipmentDetailDrawer from './EquipmentDetail';

const { Title } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;
const Option = Select.Option;

const STATUS_COLORS: Record<EquipmentStatus, string> = {
  Running: 'green',
  Stopped: 'gray',
  Maintenance: 'orange',
};

function SearchForm({
  types,
  onSearch,
}: {
  types: EquipmentTypeListItemDto[];
  onSearch: (v: Record<string, any>) => void;
}) {
  const [form] = Form.useForm();
  const t = useLocale(locale);

  const handleSubmit = () => onSearch(form.getFieldsValue());
  const handleReset = () => {
    form.resetFields();
    onSearch({});
  };

  return (
    <div className={styles['search-form-wrapper']}>
      <Form
        form={form}
        className={styles['search-form']}
        labelAlign="left"
        labelCol={{ span: 7 }}
        wrapperCol={{ span: 17 }}
      >
        <Row gutter={24}>
          <Col span={8}>
            <FormItem label={t['equipment.item.search.keyword']} field="keyword">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['equipment.item.search.type']} field="typeId">
              <Select
                allowClear
                placeholder={t['equipment.item.search.type.placeholder']}
                showSearch
              >
                {types.map((tp) => (
                  <Option key={tp.id} value={tp.id}>
                    {tp.name}
                  </Option>
                ))}
              </Select>
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['equipment.item.search.status']} field="status">
              <Select allowClear>
                {EQUIPMENT_STATUSES.map((s) => (
                  <Option key={s} value={s}>
                    {t[`equipment.item.status.${s.toLowerCase()}`]}
                  </Option>
                ))}
              </Select>
            </FormItem>
          </Col>
        </Row>
        <Row gutter={24}>
          <Col span={8}>
            <FormItem label={t['equipment.item.search.isActive']} field="isActive">
              <Select allowClear>
                <Option value={true}>{t['equipment.item.active']}</Option>
                <Option value={false}>{t['equipment.item.inactive']}</Option>
              </Select>
            </FormItem>
          </Col>
        </Row>
      </Form>
      <div className={styles['right-button']}>
        <Button type="primary" icon={<IconSearch />} onClick={handleSubmit}>
          {t['equipment.item.button.search']}
        </Button>
        <Button icon={<IconRefresh />} onClick={handleReset}>
          {t['equipment.item.button.reset']}
        </Button>
      </div>
    </div>
  );
}

export default function EquipmentTab() {
  const t = useLocale(locale);
  const [data, setData] = useState<EquipmentListItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [formParams, setFormParams] = useState<Record<string, any>>({});
  const [pagination, setPagination] = useState({
    sizeCanChange: true,
    showTotal: true,
    pageSize: 10,
    current: 1,
    total: 0,
    pageSizeChangeResetCurrent: true,
  });

  const [types, setTypes] = useState<EquipmentTypeListItemDto[]>([]);

  const [formVisible, setFormVisible] = useState(false);
  const [editing, setEditing] = useState<EquipmentDto | null>(null);
  const [detailVisible, setDetailVisible] = useState(false);
  const [detailData, setDetailData] = useState<EquipmentDto | null>(null);

  useEffect(() => {
    getActiveEquipmentTypes()
      .then(setTypes)
      .catch(() => {
        // 拉类型失败不阻塞主流程
      });
  }, []);

  function fetchData() {
    setLoading(true);
    getEquipments({
      page: pagination.current,
      pageSize: pagination.pageSize,
      ...formParams,
    })
      .then((res) => {
        setData(res.items || []);
        setPagination((p) => ({ ...p, total: res.total || 0 }));
      })
      .finally(() => setLoading(false));
  }

  function openCreate() {
    setEditing(null);
    setFormVisible(true);
  }
  function openEdit(record: EquipmentListItemDto) {
    const closeLoading = Message.loading({ content: t['equipment.item.message.loading'] });
    getEquipmentById(record.id)
      .then((detail) => {
        setEditing(detail);
        setFormVisible(true);
      })
      .catch(() => Message.error(t['equipment.item.message.loadFailed']))
      .finally(() => closeLoading());
  }
  function openDetail(record: EquipmentListItemDto) {
    const closeLoading = Message.loading({ content: t['equipment.item.message.loading'] });
    getEquipmentById(record.id)
      .then((detail) => {
        setDetailData(detail);
        setDetailVisible(true);
      })
      .catch(() => Message.error(t['equipment.item.message.loadFailed']))
      .finally(() => closeLoading());
  }
  async function handleDelete(record: EquipmentListItemDto) {
    try {
      await deleteEquipment(record.id);
      Message.success(t['equipment.item.message.deleteSuccess']);
      fetchData();
    } catch {
      Message.error(t['equipment.item.message.loadFailed']);
    }
  }

  const columns = useMemo(
    () => [
      { title: t['equipment.item.column.code'], dataIndex: 'code' },
      { title: t['equipment.item.column.name'], dataIndex: 'name' },
      { title: t['equipment.item.column.type'], dataIndex: 'equipmentTypeName' },
      {
        title: t['equipment.item.column.status'],
        dataIndex: 'status',
        render: (s: EquipmentStatus) => (
          <Tag color={STATUS_COLORS[s]}>
            {t[`equipment.item.status.${s.toLowerCase()}`]}
          </Tag>
        ),
      },
      {
        title: t['equipment.item.column.supplier'],
        dataIndex: 'supplier',
        render: (v?: string) => v || '-',
      },
      {
        title: t['equipment.item.column.location'],
        dataIndex: 'location',
        render: (v?: string) => v || '-',
      },
      {
        title: t['equipment.item.column.status2'],
        dataIndex: 'isActive',
        render: (v: boolean) =>
          v ? t['equipment.item.active'] : t['equipment.item.inactive'],
      },
      { title: t['equipment.item.column.createdAt'], dataIndex: 'createdAt' },
      {
        title: t['equipment.item.column.operations'],
        dataIndex: 'operations',
        render: (_: any, record: EquipmentListItemDto) => (
          <Space>
            <Button type="text" size="small" onClick={() => openDetail(record)}>
              {t['equipment.item.button.view']}
            </Button>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'equipment', actions: ['update'] }]}
            >
              <Button type="text" size="small" onClick={() => openEdit(record)}>
                {t['equipment.item.button.edit']}
              </Button>
            </PermissionWrapper>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'equipment', actions: ['delete'] }]}
            >
              <Popconfirm
                title={t['equipment.item.message.deleteOk']}
                onOk={() => handleDelete(record)}
              >
                <Button type="text" size="small" status="danger">
                  {t['equipment.item.button.delete']}
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

  function handleSearch(params: Record<string, any>) {
    setPagination((p) => ({ ...p, current: 1 }));
    setFormParams(params);
  }

  function onChangeTable({ current, pageSize }: any) {
    setPagination((p) => ({ ...p, current, pageSize }));
  }

  useEffect(() => {
    fetchData();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [pagination.current, pagination.pageSize, JSON.stringify(formParams)]);

  return (
    <Card>
      <Title heading={6}>{t['equipment.item.title']}</Title>
      <SearchForm types={types} onSearch={handleSearch} />
      <div className={styles['button-group']}>
        <Space>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'equipment', actions: ['create'] }]}
          >
            <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
              {t['equipment.item.button.create']}
            </Button>
          </PermissionWrapper>
        </Space>
        <Space />
      </div>
      <Table
        rowKey="id"
        loading={loading}
        onChange={onChangeTable}
        pagination={pagination}
        columns={columns}
        data={data}
      />
      <EquipmentFormModal
        visible={formVisible}
        editing={editing}
        types={types}
        onClose={() => setFormVisible(false)}
        onSuccess={fetchData}
      />
      <EquipmentDetailDrawer
        visible={detailVisible}
        data={detailData}
        onClose={() => setDetailVisible(false)}
      />
    </Card>
  );
}
