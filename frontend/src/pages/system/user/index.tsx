import { useEffect, useState, useCallback } from 'react';
import {
  Table,
  Button,
  Input,
  Drawer,
  Form,
  Select,
  Switch,
  Tag,
  Popconfirm,
  Message,
  Space,
} from '@arco-design/web-react';
import { IconPlus, IconSearch } from '@arco-design/web-react/icon';
import useLocale from '@/utils/useLocale';
import {
  getUserList,
  getUserById,
  createUser,
  updateUser,
  resetPassword,
  updateUserStatus,
  UserListItem,
  RoleOption,
} from '@/api/user';
import { getRoleList } from '@/api/role';
import locale from './locale';

const FormItem = Form.Item;

export default function UserManagement() {
  const t = useLocale(locale);
  const [data, setData] = useState<UserListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [pagination, setPagination] = useState({
    current: 1,
    pageSize: 10,
    total: 0,
  });
  const [keyword, setKeyword] = useState('');

  // 抽屉状态
  const [editVisible, setEditVisible] = useState(false);
  const [editMode, setEditMode] = useState<'create' | 'edit'>('create');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editLoading, setEditLoading] = useState(false);
  const [editForm] = Form.useForm();

  const [resetVisible, setResetVisible] = useState(false);
  const [resetId, setResetId] = useState<string | null>(null);
  const [resetLoading, setResetLoading] = useState(false);
  const [resetForm] = Form.useForm();

  // 角色选项
  const [roleOptions, setRoleOptions] = useState<RoleOption[]>([]);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const res = await getUserList(pagination.current, pagination.pageSize, keyword || undefined);
      setData(res.items);
      setPagination((prev) => ({ ...prev, total: res.total }));
    } catch {
      // request 拦截器已处理错误提示
    } finally {
      setLoading(false);
    }
  }, [pagination.current, pagination.pageSize, keyword]);

  useEffect(() => {
    getRoleList().then(setRoleOptions).catch(() => {});
  }, []);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  function openCreate() {
    setEditMode('create');
    setEditingId(null);
    editForm.resetFields();
    editForm.setFieldsValue({ isActive: true, roleIds: [] });
    setEditVisible(true);
  }

  async function openEdit(record: UserListItem) {
    setEditMode('edit');
    setEditingId(record.id);
    editForm.resetFields();
    try {
      const detail = await getUserById(record.id);
      editForm.setFieldsValue({
        username: detail.username,
        displayName: detail.displayName,
        email: detail.email,
        isActive: detail.isActive,
        roleIds: detail.roleIds,
      });
    } catch {
      // ignore
    }
    setEditVisible(true);
  }

  async function handleEditOk() {
    try {
      const values = await editForm.validate();
      setEditLoading(true);
      if (editMode === 'create') {
        await createUser(values);
        Message.success(t['user.add.success']);
      } else {
        await updateUser(editingId!, {
          displayName: values.displayName,
          email: values.email,
          isActive: values.isActive,
          roleIds: values.roleIds || [],
        });
        Message.success(t['user.edit.success']);
      }
      setEditVisible(false);
      fetchData();
    } catch {
      // 校验失败或 API 错误
    } finally {
      setEditLoading(false);
    }
  }

  function openReset(record: UserListItem) {
    setResetId(record.id);
    resetForm.resetFields();
    setResetVisible(true);
  }

  async function handleResetOk() {
    try {
      const values = await resetForm.validate();
      setResetLoading(true);
      await resetPassword(resetId!, values.newPassword);
      Message.success(t['user.reset.success']);
      setResetVisible(false);
    } catch {
      // ignore
    } finally {
      setResetLoading(false);
    }
  }

  async function handleToggleStatus(record: UserListItem) {
    try {
      await updateUserStatus(record.id, !record.isActive);
      Message.success(t['user.status.success']);
      fetchData();
    } catch {
      // ignore
    }
  }

  const columns = [
    { title: t['user.username'], dataIndex: 'username', width: 120 },
    { title: t['user.displayName'], dataIndex: 'displayName', width: 120 },
    { title: t['user.email'], dataIndex: 'email', width: 180 },
    {
      title: t['user.roles'],
      dataIndex: 'roleNames',
      render: (roleNames: string[]) =>
        roleNames?.map((n) => <Tag key={n} color="arcoblue">{n}</Tag>) || '-',
    },
    {
      title: t['user.status'],
      dataIndex: 'isActive',
      width: 80,
      render: (isActive: boolean) =>
        isActive ? <Tag color="green">{t['user.active']}</Tag> : <Tag>{t['user.inactive']}</Tag>,
    },
    {
      title: t['user.actions'],
      dataIndex: 'operations',
      width: 220,
      render: (_: unknown, record: UserListItem) => (
        <Space>
          <Button type="text" size="small" onClick={() => openEdit(record)}>
            {t['user.edit']}
          </Button>
          <Button type="text" size="small" onClick={() => openReset(record)}>
            {t['user.resetPassword']}
          </Button>
          <Popconfirm
            title={record.isActive ? t['user.disable'] : t['user.enable']}
            onOk={() => handleToggleStatus(record)}
          >
            <Button type="text" size="small" status={record.isActive ? 'warning' : 'success'}>
              {record.isActive ? t['user.disable'] : t['user.enable']}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <div>
      <Space style={{ marginBottom: 16, width: '100%', justifyContent: 'space-between' }}>
        <Input.Search
          placeholder={t['user.search.placeholder']}
          onSearch={(v) => { setKeyword(v); setPagination((p) => ({ ...p, current: 1 })); }}
          style={{ width: 300 }}
          prefix={<IconSearch />}
          allowClear
        />
        <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
          {t['user.add']}
        </Button>
      </Space>

      <Table
        rowKey="id"
        columns={columns}
        data={data}
        loading={loading}
        pagination={{
          ...pagination,
          showTotal: true,
          sizeCanChange: true,
          onChange: (current, pageSize) => setPagination((p) => ({ ...p, current, pageSize })),
        }}
      />

      {/* 新增/编辑抽屉 */}
      <Drawer
        title={editMode === 'create' ? t['user.add'] : t['user.edit']}
        visible={editVisible}
        onOk={handleEditOk}
        onCancel={() => setEditVisible(false)}
        confirmLoading={editLoading}
        width={480}
        unmountOnExit
      >
        <Form form={editForm} layout="vertical">
          <FormItem
            label={t['user.username']}
            field="username"
            rules={[{ required: true }]}
          >
            <Input placeholder={t['user.username']} disabled={editMode === 'edit'} />
          </FormItem>
          <FormItem
            label={t['user.displayName']}
            field="displayName"
            rules={[{ required: true }]}
          >
            <Input placeholder={t['user.displayName']} />
          </FormItem>
          <FormItem label={t['user.email']} field="email">
            <Input placeholder={t['user.email']} />
          </FormItem>
          {editMode === 'create' && (
            <FormItem
              label={t['user.password']}
              field="password"
              rules={[{ required: true }]}
            >
              <Input.Password placeholder={t['user.password']} />
            </FormItem>
          )}
          <FormItem label={t['user.assignRoles']} field="roleIds">
            <Select
              placeholder={t['user.assignRoles']}
              mode="multiple"
              allowClear
            >
              {roleOptions.map((r) => (
                <Select.Option key={r.id} value={r.id}>
                  {r.name}
                </Select.Option>
              ))}
            </Select>
          </FormItem>
          {editMode === 'edit' && (
            <FormItem
              label={t['user.status']}
              field="isActive"
              triggerPropName="checked"
              rules={[{ type: 'boolean' }]}
            >
              <Switch />
            </FormItem>
          )}
        </Form>
      </Drawer>

      {/* 重置密码抽屉 */}
      <Drawer
        title={t['user.resetPassword']}
        visible={resetVisible}
        onOk={handleResetOk}
        onCancel={() => setResetVisible(false)}
        confirmLoading={resetLoading}
        width={400}
        unmountOnExit
      >
        <Form form={resetForm} layout="vertical">
          <FormItem
            label={t['user.newPassword']}
            field="newPassword"
            rules={[{ required: true }]}
          >
            <Input.Password placeholder={t['user.newPassword']} />
          </FormItem>
          <FormItem
            label={t['user.confirmPassword']}
            field="confirmPassword"
            rules={[
              { required: true },
              {
                validator: (value, cb) => {
                  if (value !== resetForm.getFieldValue('newPassword')) {
                    cb(t['user.password.mismatch']);
                  } else {
                    cb();
                  }
                },
              },
            ]}
          >
            <Input.Password placeholder={t['user.confirmPassword']} />
          </FormItem>
        </Form>
      </Drawer>
    </div>
  );
}
