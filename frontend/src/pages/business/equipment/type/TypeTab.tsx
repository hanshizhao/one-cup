import { useEffect, useMemo, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import {
  Button,
  Card,
  Form,
  Grid,
  Input,
  Message,
  Modal,
  Select,
  Space,
  Table,
  Typography,
} from '@arco-design/web-react';
import { IconPlus, IconRefresh, IconSearch } from '@arco-design/web-react/icon';
import {
  EquipmentTypeDto,
  EquipmentTypeListItemDto,
  deleteEquipmentType,
  getEquipmentTypeById,
  getEquipmentTypes,
} from '@/api/equipment';
import useLocale from '@/utils/useLocale';
import PermissionWrapper from '@/components/PermissionWrapper';
import locale from '../locale';
import styles from '../style/index.module.less';
import EquipmentTypeDetailDrawer from './TypeDetail';

const { Title } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;
const Option = Select.Option;

function SearchForm({ onSearch }: { onSearch: (v: Record<string, any>) => void }) {
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
            <FormItem label={t['equipment.type.search.keyword']} field="keyword">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['equipment.type.search.status']} field="isActive">
              <Select allowClear>
                <Option value={true}>{t['equipment.type.active']}</Option>
                <Option value={false}>{t['equipment.type.inactive']}</Option>
              </Select>
            </FormItem>
          </Col>
        </Row>
      </Form>
      <div className={styles['right-button']}>
        <Button type="primary" icon={<IconSearch />} onClick={handleSubmit}>
          {t['equipment.type.button.search']}
        </Button>
        <Button icon={<IconRefresh />} onClick={handleReset}>
          {t['equipment.type.button.reset']}
        </Button>
      </div>
    </div>
  );
}

export default function EquipmentTypeTab() {
  const t = useLocale(locale);
  const [data, setData] = useState<EquipmentTypeListItemDto[]>([]);
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

  const navigate = useNavigate();
  const [detailVisible, setDetailVisible] = useState(false);
  const [detailData, setDetailData] = useState<EquipmentTypeDto | null>(null);

  function fetchData() {
    setLoading(true);
    getEquipmentTypes({
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
    navigate('/business/equipment/type/create');
  }
  function openEdit(record: EquipmentTypeListItemDto) {
    navigate(`/business/equipment/type/edit/${record.id}`);
  }
  function openDetail(record: EquipmentTypeListItemDto) {
    const closeLoading = Message.loading({ content: t['equipment.type.message.loading'] });
    getEquipmentTypeById(record.id)
      .then((detail) => {
        setDetailData(detail);
        setDetailVisible(true);
      })
      .catch(() => Message.error(t['equipment.type.message.loadFailed']))
      .finally(() => closeLoading());
  }

  // 删除：类型删除会级联影响运行模板与设备实例 → 按 c01 走 Modal.confirm（列出影响范围）
  function handleDelete(record: EquipmentTypeListItemDto) {
    Modal.confirm({
      title: t['equipment.type.message.deleteOk'],
      content: `${t['equipment.type.message.deleteImpact']}\n（${record.code} · ${record.name}，含 ${record.parameterCount} 参数 / ${record.templateCount} 模板）`,
      okText: t['equipment.type.message.deleteConfirm'],
      cancelText: t['equipment.type.message.deleteCancel'],
      okButtonProps: { status: 'danger' },
      onOk: async () => {
        try {
          await deleteEquipmentType(record.id);
          Message.success(t['equipment.type.message.deleteSuccess']);
          fetchData();
        } catch {
          Message.error(t['equipment.type.message.loadFailed']);
        }
      },
    });
  }

  const columns = useMemo(
    () => [
      { title: t['equipment.type.column.code'], dataIndex: 'code' },
      { title: t['equipment.type.column.name'], dataIndex: 'name' },
      { title: t['equipment.type.column.parameterCount'], dataIndex: 'parameterCount' },
      { title: t['equipment.type.column.templateCount'], dataIndex: 'templateCount' },
      { title: t['equipment.type.column.sortOrder'], dataIndex: 'sortOrder' },
      {
        title: t['equipment.type.column.status'],
        dataIndex: 'isActive',
        render: (v: boolean) => (v ? t['equipment.type.active'] : t['equipment.type.inactive']),
      },
      { title: t['equipment.type.column.createdAt'], dataIndex: 'createdAt' },
      {
        title: t['equipment.type.column.operations'],
        dataIndex: 'operations',
        render: (_: any, record: EquipmentTypeListItemDto) => (
          <Space>
            <Button type="text" size="small" onClick={() => openDetail(record)}>
              {t['equipment.type.button.view']}
            </Button>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'equipment-type', actions: ['update'] }]}
            >
              <Button type="text" size="small" onClick={() => openEdit(record)}>
                {t['equipment.type.button.edit']}
              </Button>
            </PermissionWrapper>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'equipment-type', actions: ['delete'] }]}
            >
              <Button
                type="text"
                size="small"
                status="danger"
                onClick={() => handleDelete(record)}
              >
                {t['equipment.type.button.delete']}
              </Button>
            </PermissionWrapper>
          </Space>
        ),
      },
    ],
    [t],
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
      <Title heading={6}>{t['equipment.type.title']}</Title>
      <SearchForm onSearch={handleSearch} />
      <div className={styles['button-group']}>
        <Space>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'equipment-type', actions: ['create'] }]}
          >
            <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
              {t['equipment.type.button.create']}
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
      <EquipmentTypeDetailDrawer
        visible={detailVisible}
        data={detailData}
        onClose={() => setDetailVisible(false)}
      />
    </Card>
  );
}
