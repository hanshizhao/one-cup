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
  ProcessDetail,
  ProcessListItem,
  deleteProcess,
  getProcess,
  getProcesses,
} from '@/api/process';
import useLocale from '@/utils/useLocale';
import PermissionWrapper from '@/components/PermissionWrapper';
import locale from './locale';
import styles from './style/index.module.less';
import ProcessFormModal from './form';
import ProcessDetailDrawer from './detail';

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
            <FormItem label={t['process.search.name']} field="keyword">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['process.search.category']} field="category">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['process.search.status']} field="isActive">
              <Select allowClear>
                <Option value={true}>{t['process.active']}</Option>
                <Option value={false}>{t['process.inactive']}</Option>
              </Select>
            </FormItem>
          </Col>
        </Row>
      </Form>
      <div className={styles['right-button']}>
        <Button type="primary" icon={<IconSearch />} onClick={handleSubmit}>
          {t['process.button.search']}
        </Button>
        <Button icon={<IconRefresh />} onClick={handleReset}>
          {t['process.button.reset']}
        </Button>
      </div>
    </div>
  );
}

export default function ProcessPage() {
  const t = useLocale(locale);
  const [data, setData] = useState<ProcessListItem[]>([]);
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
  const [editing, setEditing] = useState<ProcessDetail | null>(null);
  const [detailVisible, setDetailVisible] = useState(false);
  const [detailData, setDetailData] = useState<ProcessDetail | null>(null);

  function fetchData() {
    setLoading(true);
    getProcesses({
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
  function openEdit(record: ProcessListItem) {
    // 加载完整 ProcessDetail（含 remark），避免编辑保存后清空备注
    const closeLoading = Message.loading({ content: t['process.message.loading'] });
    getProcess(record.id)
      .then((detail) => {
        setEditing(detail);
        setFormVisible(true);
      })
      .catch(() => Message.error(t['process.message.loadFailed']))
      .finally(() => closeLoading());
  }
  function openDetail(record: ProcessListItem) {
    const closeLoading = Message.loading({ content: t['process.message.loading'] });
    getProcess(record.id)
      .then((detail) => {
        setDetailData(detail);
        setDetailVisible(true);
      })
      .catch(() => Message.error(t['process.message.loadFailed']))
      .finally(() => closeLoading());
  }
  async function handleDelete(record: ProcessListItem) {
    try {
      await deleteProcess(record.id);
      Message.success(t['process.message.deleteSuccess']);
      fetchData();
    } catch {
      // ignore
    }
  }

  const columns = useMemo(
    () => [
      { title: t['process.column.code'], dataIndex: 'code' },
      { title: t['process.column.name'], dataIndex: 'name' },
      { title: t['process.column.category'], dataIndex: 'category', render: (v: string) => v || '-' },
      { title: t['process.column.sortOrder'], dataIndex: 'sortOrder' },
      {
        title: t['process.column.status'],
        dataIndex: 'isActive',
        render: (v: boolean) => (
          <Badge
            status={v ? 'success' : 'default'}
            text={v ? t['process.active'] : t['process.inactive']}
          />
        ),
      },
      { title: t['process.column.createdAt'], dataIndex: 'createdAt' },
      {
        title: t['process.column.operations'],
        dataIndex: 'operations',
        render: (_: any, record: ProcessListItem) => (
          <Space>
            <Button type="text" size="small" onClick={() => openDetail(record)}>
              {t['process.button.view']}
            </Button>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'process', actions: ['update'] }]}
            >
              <Button type="text" size="small" onClick={() => openEdit(record)}>
                {t['process.button.edit']}
              </Button>
            </PermissionWrapper>
            <PermissionWrapper
              requiredPermissions={[{ resource: 'process', actions: ['delete'] }]}
            >
              <Popconfirm
                title={t['process.message.deleteOk']}
                onOk={() => handleDelete(record)}
              >
                <Button type="text" size="small" status="danger">
                  {t['process.button.delete']}
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
      <Title heading={6}>{t['process.title']}</Title>
      <SearchForm onSearch={handleSearch} />
      <div className={styles['button-group']}>
        <Space>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'process', actions: ['create'] }]}
          >
            <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
              {t['process.button.create']}
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
      <ProcessFormModal
        visible={formVisible}
        editing={editing}
        onClose={() => setFormVisible(false)}
        onSuccess={fetchData}
      />
      <ProcessDetailDrawer
        visible={detailVisible}
        data={detailData}
        onClose={() => setDetailVisible(false)}
      />
    </Card>
  );
}
