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
  Switch,
  Table,
  Typography,
} from '@arco-design/web-react';
import { IconPlus, IconRefresh, IconSearch } from '@arco-design/web-react/icon';
import {
  MaterialDetail,
  MaterialListItem,
  deleteMaterial,
  getMaterial,
  getMaterials,
  updateMaterialStatus,
} from '@/api/material';
import { getAllActiveUnits, MeasurementUnit } from '@/api/measurementUnit';
import useLocale from '@/utils/useLocale';
import PermissionWrapper from '@/components/PermissionWrapper';
import locale from './locale';
import styles from './style/index.module.less';
import MaterialFormModal from './form';
import MaterialDetailDrawer from './detail';

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
            <FormItem label={t['material.search.keyword']} field="keyword">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['material.search.category']} field="category">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['material.search.status']} field="isActive">
              <Select allowClear>
                <Option value={true}>{t['material.active']}</Option>
                <Option value={false}>{t['material.inactive']}</Option>
              </Select>
            </FormItem>
          </Col>
        </Row>
      </Form>
      <div className={styles['right-button']}>
        <Button type="primary" icon={<IconSearch />} onClick={handleSubmit}>
          {t['material.button.search']}
        </Button>
        <Button icon={<IconRefresh />} onClick={handleReset}>
          {t['material.button.reset']}
        </Button>
      </div>
    </div>
  );
}

export default function MaterialPage() {
  const t = useLocale(locale);
  const [data, setData] = useState<MaterialListItem[]>([]);
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

  const [formVisible, setFormVisible] = useState(false);
  const [editing, setEditing] = useState<MaterialDetail | null>(null);
  const [detailVisible, setDetailVisible] = useState(false);
  const [detailData, setDetailData] = useState<MaterialDetail | null>(null);

  // 单位 map:进页面拉一次,列表列 + 详情 + 表单共用
  const [units, setUnits] = useState<MeasurementUnit[]>([]);
  const unitMap = useMemo(() => {
    const m: Record<string, string> = {};
    units.forEach((u) => (m[u.id] = u.nameZh)); // 注意:单位名字段是 nameZh
    return m;
  }, [units]);

  useEffect(() => {
    getAllActiveUnits()
      .then(setUnits)
      .catch(() => {
        // 单位拉取失败不阻塞主流程,单位列展示 '-'
      });
  }, []);

  function fetchData() {
    setLoading(true);
    getMaterials({
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
  function openEdit(record: MaterialListItem) {
    const closeLoading = Message.loading({ content: t['material.message.loading'] });
    getMaterial(record.id)
      .then((detail) => {
        setEditing(detail);
        setFormVisible(true);
      })
      .catch(() => Message.error(t['material.message.loadFailed']))
      .finally(() => closeLoading());
  }
  function openDetail(record: MaterialListItem) {
    const closeLoading = Message.loading({ content: t['material.message.loading'] });
    getMaterial(record.id)
      .then((detail) => {
        setDetailData(detail);
        setDetailVisible(true);
      })
      .catch(() => Message.error(t['material.message.loadFailed']))
      .finally(() => closeLoading());
  }
  async function handleDelete(record: MaterialListItem) {
    try {
      await deleteMaterial(record.id);
      Message.success(t['material.message.deleteSuccess']);
      fetchData();
    } catch {
      Message.error(t['material.message.loadFailed']);
    }
  }

  // 状态启停:走独立的 status 端点(非 UpdateMaterialRequest)。失败时刷新回滚。
  async function handleToggleStatus(record: MaterialListItem, checked: boolean) {
    try {
      await updateMaterialStatus(record.id, checked);
      Message.success(t['material.message.updateSuccess']);
      fetchData();
    } catch {
      Message.error(t['material.message.loadFailed']);
      fetchData();
    }
  }

  const columns = useMemo(
    () => [
      { title: t['material.column.code'], dataIndex: 'code' },
      { title: t['material.column.name'], dataIndex: 'name' },
      { title: t['material.column.spec'], dataIndex: 'spec' },
      { title: t['material.column.category'], dataIndex: 'category' },
      {
        title: t['material.column.unit'],
        dataIndex: 'unitId',
        render: (id: string | null) => (id ? unitMap[id] ?? '-' : '-'),
      },
      { title: t['material.column.sortOrder'], dataIndex: 'sortOrder' },
      {
        title: t['material.column.status'],
        dataIndex: 'isActive',
        render: (v: boolean, record: MaterialListItem) => (
          <PermissionWrapper
            requiredPermissions={[{ resource: 'material', actions: ['update'] }]}
          >
            <Switch
              checked={v}
              onChange={(checked: boolean) => handleToggleStatus(record, checked)}
            />
          </PermissionWrapper>
        ),
      },
      { title: t['material.column.createdAt'], dataIndex: 'createdAt' },
      {
        title: t['material.column.operations'],
        dataIndex: 'operations',
        render: (_: any, record: MaterialListItem) => (
          <Space>
            <Button type="text" size="small" onClick={() => openDetail(record)}>
              {t['material.button.view']}
            </Button>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'material', actions: ['update'] }]}
            >
              <Button type="text" size="small" onClick={() => openEdit(record)}>
                {t['material.button.edit']}
              </Button>
            </PermissionWrapper>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'material', actions: ['delete'] }]}
            >
              <Popconfirm
                title={t['material.message.deleteOk']}
                onOk={() => handleDelete(record)}
              >
                <Button type="text" size="small" status="danger">
                  {t['material.button.delete']}
                </Button>
              </Popconfirm>
            </PermissionWrapper>
          </Space>
        ),
      },
    ],
    [t, unitMap],
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
      <Title heading={6}>{t['material.title']}</Title>
      <SearchForm onSearch={handleSearch} />
      <div className={styles['button-group']}>
        <Space>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'material', actions: ['create'] }]}
          >
            <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
              {t['material.button.create']}
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
      <MaterialFormModal
        visible={formVisible}
        editing={editing}
        units={units}
        onClose={() => setFormVisible(false)}
        onSuccess={fetchData}
      />
      <MaterialDetailDrawer
        visible={detailVisible}
        data={detailData}
        unitMap={unitMap}
        onClose={() => setDetailVisible(false)}
      />
    </Card>
  );
}
