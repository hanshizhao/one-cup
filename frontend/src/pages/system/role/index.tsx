import React, { useEffect, useState, useCallback } from 'react';
import {
  Table,
  Button,
  Drawer,
  Form,
  Input,
  Tree,
  Tag,
  Popconfirm,
  Message,
  Space,
  Typography,
  Card,
} from '@arco-design/web-react';
import { IconPlus } from '@arco-design/web-react/icon';
import useLocale from '@/utils/useLocale';
import {
  getRoleList,
  getRoleById,
  createRole,
  updateRole,
  deleteRole,
  RoleListItem,
} from '@/api/role';
import { getPermissionList, PermissionItem } from '@/api/permission';
import locale from './locale';
import styles from './style/index.module.less';

const { Title } = Typography;
const FormItem = Form.Item;
const TreeNode = Tree.Node;

// 将权限列表按模块前缀（code 的第一段）分组为树结构
function buildPermissionTree(permissions: PermissionItem[]) {
  const groups: Record<string, PermissionItem[]> = {};
  permissions.forEach((p) => {
    const module = p.code.split(':')[0];
    if (!groups[module]) groups[module] = [];
    groups[module].push(p);
  });
  return Object.entries(groups).map(([module, perms]) => ({
    key: `group-${module}`,
    title: module,
    children: perms.map((p) => ({ key: p.id, title: p.name })),
  }));
}

export default function RoleManagement() {
  const t = useLocale(locale);
  const [data, setData] = useState<RoleListItem[]>([]);
  const [loading, setLoading] = useState(false);
  const [permissions, setPermissions] = useState<PermissionItem[]>([]);
  const [treeData, setTreeData] = useState<any[]>([]);

  const [drawerVisible, setDrawerVisible] = useState(false);
  const [editMode, setEditMode] = useState<'create' | 'edit'>('create');
  const [editingId, setEditingId] = useState<string | null>(null);
  const [drawerLoading, setDrawerLoading] = useState(false);
  const [form] = Form.useForm();
  const [checkedKeys, setCheckedKeys] = useState<string[]>([]);

  const fetchData = useCallback(async () => {
    setLoading(true);
    try {
      const res = await getRoleList();
      setData(res);
    } catch {
      // ignore
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchData();
    getPermissionList().then((perms) => {
      setPermissions(perms);
      setTreeData(buildPermissionTree(perms));
    });
  }, [fetchData]);

  function openCreate() {
    setEditMode('create');
    setEditingId(null);
    form.resetFields();
    setCheckedKeys([]);
    setDrawerVisible(true);
  }

  async function openEdit(record: RoleListItem) {
    setEditMode('edit');
    setEditingId(record.id);
    form.resetFields();
    try {
      const detail = await getRoleById(record.id);
      form.setFieldsValue({
        name: detail.name,
        code: detail.code,
        description: detail.description,
      });
      setCheckedKeys(detail.permissionIds);
    } catch {
      // ignore
    }
    setDrawerVisible(true);
  }

  async function handleOk() {
    try {
      const values = await form.validate();
      setDrawerLoading(true);
      if (editMode === 'create') {
        await createRole(values);
        Message.success(t['role.add.success']);
      } else {
        await updateRole(editingId!, {
          name: values.name,
          description: values.description,
          permissionIds: checkedKeys.filter((k) => !k.startsWith('group-')),
        });
        Message.success(t['role.edit.success']);
      }
      setDrawerVisible(false);
      fetchData();
    } catch {
      // ignore
    } finally {
      setDrawerLoading(false);
    }
  }

  async function handleDelete(id: string) {
    try {
      await deleteRole(id);
      Message.success(t['role.delete.success']);
      fetchData();
    } catch {
      // ignore
    }
  }

  const columns = [
    { title: t['role.name'], dataIndex: 'name', width: 120 },
    { title: t['role.code'], dataIndex: 'code', width: 120 },
    { title: t['role.description'], dataIndex: 'description' },
    { title: t['role.userCount'], dataIndex: 'userCount', width: 80 },
    { title: t['role.permissionCount'], dataIndex: 'permissionCount', width: 90 },
    {
      title: t['role.actions'],
      dataIndex: 'operations',
      width: 140,
      render: (_: unknown, record: RoleListItem) => (
        <Space>
          <Button type="text" size="small" onClick={() => openEdit(record)}>
            {t['role.edit']}
          </Button>
          <Popconfirm
            title={t['role.delete.confirm']}
            onOk={() => handleDelete(record.id)}
            disabled={record.code === 'admin'}
          >
            <Button type="text" size="small" status="danger" disabled={record.code === 'admin'}>
              {t['role.delete']}
            </Button>
          </Popconfirm>
        </Space>
      ),
    },
  ];

  return (
    <Card>
      <Title heading={6}>{t['role.title']}</Title>

      <div className={styles['button-group']}>
        <Space>
          <Button type="primary" icon={<IconPlus />} onClick={openCreate}>
            {t['role.add']}
          </Button>
        </Space>
        <Space />
      </div>

      <Table rowKey="id" columns={columns} data={data} loading={loading} pagination={false} />

      <Drawer
        title={editMode === 'create' ? t['role.add'] : t['role.edit']}
        visible={drawerVisible}
        onOk={handleOk}
        onCancel={() => setDrawerVisible(false)}
        confirmLoading={drawerLoading}
        width={480}
        unmountOnExit
      >
        <Form form={form} layout="vertical">
          <FormItem label={t['role.name']} field="name" rules={[{ required: true }]}>
            <Input placeholder={t['role.name']} />
          </FormItem>
          <FormItem label={t['role.code']} field="code" rules={[{ required: true }]}>
            <Input placeholder={t['role.code']} disabled={editMode === 'edit'} />
          </FormItem>
          <FormItem label={t['role.description']} field="description">
            <Input.TextArea placeholder={t['role.description']} />
          </FormItem>
          {editMode === 'edit' && (
            <FormItem label={t['role.assignPermissions']}>
              <Tree
                checkable
                checkedKeys={checkedKeys}
                onCheck={(keys) => setCheckedKeys(keys as string[])}
                treeData={treeData}
              />
            </FormItem>
          )}
        </Form>
      </Drawer>
    </Card>
  );
}
