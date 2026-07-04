import { useEffect, useMemo, useState } from 'react';
import {
  Badge,
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
  Typography,
} from '@arco-design/web-react';
import { IconPlus, IconRefresh, IconSearch } from '@arco-design/web-react/icon';
import {
  CustomerDetail,
  CustomerListItem,
  deleteCustomer,
  getCustomer,
  getCustomers,
} from '@/api/customer';
import useLocale from '@/utils/useLocale';
import PermissionWrapper from '@/components/PermissionWrapper';
import locale from './locale';
import styles from './style/index.module.less';
import CustomerFormModal from './form';
import CustomerDetailDrawer from './detail';

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
            <FormItem label={t['customer.search.name']} field="keyword">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['customer.search.code']} field="code">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['customer.search.status']} field="isActive">
              <Select allowClear>
                <Option value={true}>{t['customer.active']}</Option>
                <Option value={false}>{t['customer.inactive']}</Option>
              </Select>
            </FormItem>
          </Col>
        </Row>
      </Form>
      <div className={styles['right-button']}>
        <Button type="primary" icon={<IconSearch />} onClick={handleSubmit}>
          {t['customer.button.search']}
        </Button>
        <Button icon={<IconRefresh />} onClick={handleReset}>
          {t['customer.button.reset']}
        </Button>
      </div>
    </div>
  );
}

export default function CustomerPage() {
  const t = useLocale(locale);
  const [data, setData] = useState<CustomerListItem[]>([]);
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
  const [editing, setEditing] = useState<CustomerDetail | null>(null);
  const [detailVisible, setDetailVisible] = useState(false);
  const [detailData, setDetailData] = useState<CustomerDetail | null>(null);

  function fetchData() {
    setLoading(true);
    getCustomers({
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
  function openEdit(record: CustomerListItem) {
    // 加载完整 CustomerDetail（含 remark），避免编辑保存后清空备注
    const closeLoading = Message.loading({ content: t['customer.message.loading'] });
    getCustomer(record.id)
      .then((detail) => {
        setEditing(detail);
        setFormVisible(true);
      })
      .catch(() => Message.error(t['customer.message.loadFailed']))
      .finally(() => closeLoading());
  }
  function openDetail(record: CustomerListItem) {
    const closeLoading = Message.loading({ content: t['customer.message.loading'] });
    getCustomer(record.id)
      .then((detail) => {
        setDetailData(detail);
        setDetailVisible(true);
      })
      .catch(() => Message.error(t['customer.message.loadFailed']))
      .finally(() => closeLoading());
  }
  async function handleDelete(record: CustomerListItem) {
    try {
      await deleteCustomer(record.id);
      Message.success(t['customer.message.deleteSuccess']);
      fetchData();
    } catch {
      // ignore
    }
  }

  const columns = useMemo(
    () => [
      { title: t['customer.column.code'], dataIndex: 'code' },
      { title: t['customer.column.name'], dataIndex: 'name' },
      { title: t['customer.column.shortName'], dataIndex: 'shortName' },
      { title: t['customer.column.contactPerson'], dataIndex: 'contactPerson' },
      { title: t['customer.column.contactPhone'], dataIndex: 'contactPhone' },
      {
        title: t['customer.column.status'],
        dataIndex: 'isActive',
        render: (v: boolean) => (
          <Badge
            status={v ? 'success' : 'default'}
            text={v ? t['customer.active'] : t['customer.inactive']}
          />
        ),
      },
      { title: t['customer.column.createdAt'], dataIndex: 'createdAt' },
      {
        title: t['customer.column.operations'],
        dataIndex: 'operations',
        render: (_: any, record: CustomerListItem) => (
          <Space>
            <Button type="text" size="small" onClick={() => openDetail(record)}>
              {t['customer.button.view']}
            </Button>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'customer', actions: ['update'] }]}
            >
              <Button type="text" size="small" onClick={() => openEdit(record)}>
                {t['customer.button.edit']}
              </Button>
            </PermissionWrapper>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'customer', actions: ['delete'] }]}
            >
              <Popconfirm
                title={t['customer.message.deleteOk']}
                onOk={() => handleDelete(record)}
              >
                <Button type="text" size="small" status="danger">
                  {t['customer.button.delete']}
                </Button>
              </Popconfirm>
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
      <Title heading={6}>{t['customer.title']}</Title>
      <SearchForm onSearch={handleSearch} />
      <div className={styles['button-group']}>
        <Space>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'customer', actions: ['create'] }]}
          >
            <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
              {t['customer.button.create']}
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
      <CustomerFormModal
        visible={formVisible}
        editing={editing}
        onClose={() => setFormVisible(false)}
        onSuccess={fetchData}
      />
      <CustomerDetailDrawer
        visible={detailVisible}
        data={detailData}
        onClose={() => setDetailVisible(false)}
      />
    </Card>
  );
}
