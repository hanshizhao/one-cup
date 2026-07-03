/**
 * 列表查询页 —— 整页骨架模板
 * 标准：docs/specs/2026-07-03-query-table-standard-design.md
 * 用法：复制本文件到目标页面目录，按注释标记的【替换点】修改字段/列/接口。
 *
 * 文件结构约定（与 Arco Pro search-table 一致）：
 *   pages/xxx/index.tsx   ← 本文件（页面：Card + Title + SearchForm + 工具栏 + Table）
 *   pages/xxx/form.tsx    ← 查询表单组件（见下方 SearchForm）
 *   pages/xxx/style/index.module.less  ← 样式（见 .template.less）
 */
import React, { useEffect, useMemo, useState } from 'react';
import {
  Button,
  Card,
  Form,
  Grid,
  Input,
  Select,
  Space,
  Table,
  Typography,
} from '@arco-design/web-react';
import {
  IconDownload,
  IconPlus,
  IconRefresh,
  IconSearch,
} from '@arco-design/web-react/icon';
import axios from 'axios';
import styles from './style/index.module.less';

const { Title } = Typography;
const { Row, Col } = Grid;
const FormItem = Form.Item;

// 【替换点】列定义：按实际业务改
function getColumns() {
  return [
    { title: '名称', dataIndex: 'name' },
    { title: '类型', dataIndex: 'type' },
    { title: '状态', dataIndex: 'status' },
    // { title: '操作', dataIndex: 'operations', render: () => <Button type="text">查看</Button> },
  ];
}

/**
 * 查询表单组件（页面级，受控于 useForm）
 * 标准 2.2 / 2.3 / 2.4
 */
function SearchForm({ onSearch }: { onSearch: (v: Record<string, any>) => void }) {
  const [form] = Form.useForm();

  const handleSubmit = () => {
    onSearch(form.getFieldsValue());
  };

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
        labelCol={{ span: 5 }}
        wrapperCol={{ span: 19 }}
      >
        <Row gutter={24}>
          {/* 【替换点】按实际查询字段增删下面的 <Col> */}
          <Col span={8}>
            <FormItem label="名称" field="name">
              <Input allowClear />
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label="类型" field="type">
              <Select allowClear>
                {/* <Option value="x">x</Option> */}
              </Select>
            </FormItem>
          </Col>
          <Col span={8}>
            <FormItem label="状态" field="status">
              <Select allowClear>
                {/* <Option value="x">x</Option> */}
              </Select>
            </FormItem>
          </Col>
        </Row>
      </Form>
      <div className={styles['right-button']}>
        <Button type="primary" icon={<IconSearch />} onClick={handleSubmit}>
          查询
        </Button>
        <Button icon={<IconRefresh />} onClick={handleReset}>
          重置
        </Button>
      </div>
    </div>
  );
}

export default function QueryTablePageTemplate() {
  const [data, setData] = useState<any[]>([]);
  const [loading, setLoading] = useState(true);
  const [formParams, setFormParams] = useState<Record<string, any>>({});
  const [pagination, setPagination] = useState({
    sizeCanChange: true,
    showTotal: true,
    pageSize: 10,
    current: 1,
    pageSizeChangeResetCurrent: true,
  });

  const columns = useMemo(() => getColumns(), []);

  // 标准 2.5：分页/筛选变化即重查；筛选条件合并进每次请求
  useEffect(() => {
    fetchData();
  }, [pagination.current, pagination.pageSize, JSON.stringify(formParams)]);

  function fetchData() {
    const { current, pageSize } = pagination;
    setLoading(true);
    axios
      .get('/api/your-list', {
        params: { page: current, pageSize, ...formParams },
      })
      .then((res) => {
        setData(res.data.list);
        setPagination((p) => ({ ...p, total: res.data.total }));
      })
      .finally(() => setLoading(false));
  }

  function handleSearch(params: Record<string, any>) {
    setPagination((p) => ({ ...p, current: 1 })); // 新查询回到第 1 页
    setFormParams(params);
  }

  function onChangeTable({ current, pageSize }) {
    setPagination((p) => ({ ...p, current, pageSize }));
  }

  return (
    <Card>
      <Title heading={6}>页面标题</Title>

      <SearchForm onSearch={handleSearch} />

      {/* 标准 2.6：工具栏 = flex space-between + 左右两个 Space */}
      <div className={styles['button-group']}>
        <Space>
          <Button type="primary" icon={<IconPlus />}>
            新建
          </Button>
        </Space>
        <Space>
          <Button icon={<IconDownload />}>下载</Button>
        </Space>
      </div>

      {/* 标准 2.7：表格外部受控 */}
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
