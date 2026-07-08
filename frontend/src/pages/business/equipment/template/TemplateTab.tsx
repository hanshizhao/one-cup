import { useEffect, useMemo, useState } from 'react';
import { useNavigate, useSearchParams } from 'react-router-dom';
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
  EquipmentTemplateListItemDto,
  EquipmentTypeListItemDto,
  deleteEquipmentTemplate,
  getActiveEquipmentTypes,
  getEquipmentTemplatesPaged,
} from '@/api/equipment';
import { getProcesses, ProcessListItem } from '@/api/process';
import useLocale from '@/utils/useLocale';
import PermissionWrapper from '@/components/PermissionWrapper';
import locale from '../locale';
import styles from '../style/index.module.less';

const { Title } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;
const Option = Select.Option;

function SearchForm({
  types,
  processes,
  initialTypeId,
  onSearch,
}: {
  types: EquipmentTypeListItemDto[];
  processes: ProcessListItem[];
  initialTypeId?: string;
  onSearch: (v: Record<string, any>) => void;
}) {
  const [form] = Form.useForm();
  const t = useLocale(locale);
  useEffect(() => {
    if (initialTypeId) {
      form.setFieldsValue({ typeId: initialTypeId });
    }
  }, [initialTypeId, form]);
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
            <FormItem label={t['equipment.template.tab.search.type']} field="typeId">
              <Select allowClear placeholder={t['equipment.template.tab.search.type.all']}>
                {types.map((tp) => (
                  <Option key={tp.id} value={tp.id}>{tp.name}</Option>
                ))}
              </Select>
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['equipment.template.tab.search.keyword']} field="keyword">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label={t['equipment.template.tab.search.process']} field="processId">
              <Select allowClear>
                {processes.map((p) => (
                  <Option key={p.id} value={p.id}>{p.name}</Option>
                ))}
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

export default function TemplateTab() {
  const t = useLocale(locale);
  const navigate = useNavigate();
  const [searchParams] = useSearchParams();
  // 从 URL ?typeId= 初始化筛选（便于从设备类型详情抽屉跳转时自动定位类型）
  const initialTypeId = searchParams.get('typeId') || undefined;
  const [data, setData] = useState<EquipmentTemplateListItemDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [formParams, setFormParams] = useState<Record<string, any>>(
    initialTypeId ? { typeId: initialTypeId } : {}
  );
  const [pagination, setPagination] = useState({
    sizeCanChange: true,
    showTotal: true,
    pageSize: 10,
    current: 1,
    total: 0,
    pageSizeChangeResetCurrent: true,
  });
  const [types, setTypes] = useState<EquipmentTypeListItemDto[]>([]);
  const [processes, setProcesses] = useState<ProcessListItem[]>([]);

  useEffect(() => {
    getActiveEquipmentTypes().then(setTypes).catch(() => {});
    getProcesses({ page: 1, pageSize: 100, isActive: true })
      .then((res) => setProcesses(res.items || []))
      .catch(() => {});
  }, []);

  function fetchData() {
    setLoading(true);
    getEquipmentTemplatesPaged({
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
    navigate('/business/equipment/template/create');
  }
  function openEdit(record: EquipmentTemplateListItemDto) {
    navigate(`/business/equipment/template/edit/${record.id}`);
  }
  async function handleDelete(record: EquipmentTemplateListItemDto) {
    try {
      await deleteEquipmentTemplate(record.equipmentTypeId, record.id);
      Message.success(t['equipment.template.message.deleteSuccess']);
      fetchData();
    } catch {
      Message.error(t['equipment.template.message.loadFailed']);
    }
  }

  const columns = useMemo(
    () => [
      { title: t['equipment.template.column.name'], dataIndex: 'name' },
      { title: t['equipment.template.tab.column.type'], dataIndex: 'equipmentTypeName' },
      { title: t['equipment.template.column.process'], dataIndex: 'processName' },
      {
        title: t['equipment.template.column.status'],
        dataIndex: 'status',
        render: (s: string) => {
          const map: Record<string, string> = { valid: 'success', invalid: 'error', orphan: 'warning' };
          const labelMap: Record<string, string> = {
            valid: t['equipment.template.status.valid'],
            invalid: t['equipment.template.status.invalid'],
            orphan: t['equipment.template.status.orphan'],
          };
          return <Badge status={(map[s] || 'success') as any} text={labelMap[s] || s} />;
        },
      },
      { title: t['equipment.template.column.sortOrder'], dataIndex: 'sortOrder' },
      { title: t['equipment.template.column.createdAt'], dataIndex: 'createdAt' },
      {
        title: t['equipment.template.column.operations'],
        dataIndex: 'operations',
        render: (_: any, record: EquipmentTemplateListItemDto) => (
          <Space>
            <PermissionWrapper requiredPermissions={[{ resource: 'equipment-type', actions: ['update'] }]}>
              <Button type="text" size="small" onClick={() => openEdit(record)}>
                {t['equipment.template.button.edit']}
              </Button>
            </PermissionWrapper>
            <PermissionWrapper requiredPermissions={[{ resource: 'equipment-type', actions: ['delete'] }]}>
              <Popconfirm title={t['equipment.template.message.deleteOk']} onOk={() => handleDelete(record)}>
                <Button type="text" size="small" status="danger">
                  {t['equipment.template.button.delete']}
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
      <Title heading={6}>{t['equipment.template.tab.title']}</Title>
      <SearchForm types={types} processes={processes} initialTypeId={initialTypeId} onSearch={handleSearch} />
      <div className={styles['button-group']}>
        <Space>
          <PermissionWrapper requiredPermissions={[{ resource: 'equipment-type', actions: ['create'] }]}>
            <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
              {t['equipment.template.tab.button.create']}
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
    </Card>
  );
}
