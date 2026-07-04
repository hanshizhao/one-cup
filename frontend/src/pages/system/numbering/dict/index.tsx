import { useEffect, useState, useCallback } from 'react';
import {
  Table, Button, Drawer, Form, Input, InputNumber, Switch,
  Tag, Popconfirm, Message, Space, Alert, Typography, Card,
} from '@arco-design/web-react';
import { IconPlus } from '@arco-design/web-react/icon';
import useLocale from '@/utils/useLocale';
import {
  getTargetTypes, createTargetType, updateTargetType, updateTargetTypeStatus,
  getCategories, createCategory, updateCategory, updateCategoryStatus,
  TargetType, Category,
  CreateTargetTypeRequest, CreateCategoryRequest,
} from '@/api/numberingDictionary';
import locale from './locale';
import styles from './style/index.module.less';
import PermissionWrapper from '@/components/PermissionWrapper';

const { Title } = Typography;
const FormItem = Form.Item;

export default function NumberingDictionaryPage() {
  const t = useLocale(locale);

  // ── 业务类型 ──
  const [typeData, setTypeData] = useState<TargetType[]>([]);
  const [typeLoading, setTypeLoading] = useState(false);
  const [selectedTypeId, setSelectedTypeId] = useState<string | null>(null);
  const selectedType = typeData.find((x) => x.id === selectedTypeId);

  // 业务类型抽屉
  const [typeDrawerVisible, setTypeDrawerVisible] = useState(false);
  const [typeEditMode, setTypeEditMode] = useState<'create' | 'edit'>('create');
  const [editingTypeId, setEditingTypeId] = useState<string | null>(null);
  const [typeForm] = Form.useForm();

  // ── 分类 ──
  const [categoryData, setCategoryData] = useState<Category[]>([]);
  const [categoryLoading, setCategoryLoading] = useState(false);

  // 分类抽屉
  const [catDrawerVisible, setCatDrawerVisible] = useState(false);
  const [catEditMode, setCatEditMode] = useState<'create' | 'edit'>('create');
  const [editingCatId, setEditingCatId] = useState<string | null>(null);
  const [catForm] = Form.useForm();

  // ── 拉取业务类型 ──
  const fetchTypes = useCallback(async () => {
    setTypeLoading(true);
    try {
      // 拉全量（不分页），字典项通常不多
      const res = await getTargetTypes({ page: 1, pageSize: 200 });
      setTypeData(res.items);
    } finally {
      setTypeLoading(false);
    }
  }, []);

  useEffect(() => {
    fetchTypes();
  }, [fetchTypes]);

  // ── 拉取选中类型的分类 ──
  const fetchCategories = useCallback(async () => {
    if (!selectedType) return;
    setCategoryLoading(true);
    try {
      const res = await getCategories({
        page: 1, pageSize: 200, targetTypeCode: selectedType.code,
      });
      setCategoryData(res.items);
    } finally {
      setCategoryLoading(false);
    }
  }, [selectedType]);

  useEffect(() => {
    if (selectedType) {
      fetchCategories();
    } else {
      setCategoryData([]);
    }
  }, [selectedType, fetchCategories]);

  // ── 业务类型操作 ──
  function openCreateType() {
    setTypeEditMode('create');
    setEditingTypeId(null);
    typeForm.resetFields();
    typeForm.setFieldsValue({ sortOrder: 0 });
    setTypeDrawerVisible(true);
  }

  function openEditType(record: TargetType) {
    setTypeEditMode('edit');
    setEditingTypeId(record.id);
    typeForm.resetFields();
    typeForm.setFieldsValue({
      nameZh: record.nameZh, nameEn: record.nameEn, sortOrder: record.sortOrder,
    });
    setTypeDrawerVisible(true);
  }

  async function handleTypeOk() {
    try {
      const values = await typeForm.validate();
      if (typeEditMode === 'create') {
        await createTargetType(values as CreateTargetTypeRequest);
        Message.success(t['numbering.dict.create.success']);
      } else {
        await updateTargetType(editingTypeId!, {
          nameZh: values.nameZh, nameEn: values.nameEn, sortOrder: values.sortOrder,
        });
        Message.success(t['numbering.dict.update.success']);
      }
      setTypeDrawerVisible(false);
      fetchTypes();
    } catch {
      // 校验失败或 API 错误
    }
  }

  async function handleToggleTypeStatus(record: TargetType) {
    await updateTargetTypeStatus(record.id, !record.isActive);
    Message.success(t['numbering.dict.status.success']);
    fetchTypes();
  }

  // ── 分类操作 ──
  function openCreateCategory() {
    setCatEditMode('create');
    setEditingCatId(null);
    catForm.resetFields();
    catForm.setFieldsValue({ sortOrder: 0 });
    setCatDrawerVisible(true);
  }

  function openEditCategory(record: Category) {
    setCatEditMode('edit');
    setEditingCatId(record.id);
    catForm.resetFields();
    catForm.setFieldsValue({
      nameZh: record.nameZh, nameEn: record.nameEn, sortOrder: record.sortOrder,
    });
    setCatDrawerVisible(true);
  }

  async function handleCategoryOk() {
    try {
      const values = await catForm.validate();
      if (catEditMode === 'create') {
        await createCategory({
          ...values,
          targetTypeCode: selectedType!.code,
        } as CreateCategoryRequest);
        Message.success(t['numbering.dict.create.success']);
      } else {
        await updateCategory(editingCatId!, {
          nameZh: values.nameZh, nameEn: values.nameEn, sortOrder: values.sortOrder,
        });
        Message.success(t['numbering.dict.update.success']);
      }
      setCatDrawerVisible(false);
      fetchCategories();
    } catch {
      // ignore
    }
  }

  async function handleToggleCatStatus(record: Category) {
    await updateCategoryStatus(record.id, !record.isActive);
    Message.success(t['numbering.dict.status.success']);
    fetchCategories();
  }

  // ── 列定义 ──
  const typeColumns = [
    { title: t['numbering.dict.type.code'], dataIndex: 'code', width: 120 },
    { title: t['numbering.dict.type.nameZh'], dataIndex: 'nameZh', width: 120 },
    { title: t['numbering.dict.type.nameEn'], dataIndex: 'nameEn', width: 120 },
    { title: t['numbering.dict.type.sortOrder'], dataIndex: 'sortOrder', width: 80 },
    {
      title: t['numbering.dict.type.status'],
      dataIndex: 'isActive',
      width: 80,
      render: (v: boolean) => v
        ? <Tag color="green">{t['numbering.dict.active']}</Tag>
        : <Tag>{t['numbering.dict.inactive']}</Tag>,
    },
    {
      title: t['numbering.dict.type.operations'],
      dataIndex: 'operations',
      width: 160,
      render: (_: unknown, record: TargetType) => (
        <Space>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'system:numbering', actions: ['update'] }]}
          >
            <Button type="text" size="small" onClick={() => openEditType(record)}>
              {t['numbering.dict.edit']}
            </Button>
          </PermissionWrapper>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'system:numbering', actions: ['update'] }]}
          >
            <Popconfirm
              title={record.isActive
                ? t['numbering.dict.disable.confirm']
                : t['numbering.dict.enable.confirm']}
              onOk={() => handleToggleTypeStatus(record)}
            >
              <Button type="text" size="small" status={record.isActive ? 'warning' : 'success'}>
                {record.isActive ? t['numbering.dict.disable'] : t['numbering.dict.enable']}
              </Button>
            </Popconfirm>
          </PermissionWrapper>
        </Space>
      ),
    },
  ];

  const categoryColumns = [
    { title: t['numbering.dict.category.code'], dataIndex: 'code', width: 120 },
    { title: t['numbering.dict.category.nameZh'], dataIndex: 'nameZh', width: 120 },
    { title: t['numbering.dict.category.nameEn'], dataIndex: 'nameEn', width: 120 },
    { title: t['numbering.dict.category.sortOrder'], dataIndex: 'sortOrder', width: 80 },
    {
      title: t['numbering.dict.category.status'],
      dataIndex: 'isActive',
      width: 80,
      render: (v: boolean) => v
        ? <Tag color="green">{t['numbering.dict.active']}</Tag>
        : <Tag>{t['numbering.dict.inactive']}</Tag>,
    },
    {
      title: t['numbering.dict.category.operations'],
      dataIndex: 'operations',
      width: 160,
      render: (_: unknown, record: Category) => (
        <Space>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'system:numbering', actions: ['update'] }]}
          >
            <Button type="text" size="small" onClick={() => openEditCategory(record)}>
              {t['numbering.dict.edit']}
            </Button>
          </PermissionWrapper>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'system:numbering', actions: ['update'] }]}
          >
            <Popconfirm
              title={record.isActive
                ? t['numbering.dict.disable.confirm']
                : t['numbering.dict.enable.confirm']}
              onOk={() => handleToggleCatStatus(record)}
            >
              <Button type="text" size="small" status={record.isActive ? 'warning' : 'success'}>
                {record.isActive ? t['numbering.dict.disable'] : t['numbering.dict.enable']}
              </Button>
            </Popconfirm>
          </PermissionWrapper>
        </Space>
      ),
    },
  ];

  return (
    <Card>
      <Title heading={6}>{t['numbering.dict.title']}</Title>

      {/* ── 业务类型区 ── */}
      <div style={{ marginBottom: 8, fontWeight: 600 }}>
        {t['numbering.dict.type.title']}
      </div>
      <div className={styles['button-group']}>
        <Space />
        <Space>
          <PermissionWrapper
            requiredPermissions={[{ resource: 'system:numbering', actions: ['create'] }]}
          >
            <Button type="primary" icon={<IconPlus />} onClick={openCreateType}>
              {t['numbering.dict.type.create']}
            </Button>
          </PermissionWrapper>
        </Space>
      </div>
      <Table
        rowKey="id"
        columns={typeColumns}
        data={typeData}
        loading={typeLoading}
        pagination={false}
        size="small"
        rowSelection={{
          type: 'radio',
          selectedRowKeys: selectedTypeId ? [selectedTypeId] : [],
          onChange: (keys) => setSelectedTypeId(keys[0] as string),
        }}
        onRow={(record: TargetType) => ({
          onClick: () => setSelectedTypeId(record.id),
        })}
      />

      {/* ── 分类区 ── */}
      <div style={{ marginTop: 24, marginBottom: 8, fontWeight: 600 }}>
        {t['numbering.dict.category.title']}
        {selectedType && (
          <span style={{ marginLeft: 8, color: 'var(--color-text-3)', fontWeight: 400 }}>
            ({selectedType.nameZh} / {selectedType.code})
          </span>
        )}
      </div>
      {selectedType ? (
        <>
          <div className={styles['button-group']}>
            <Space />
            <Space>
              <PermissionWrapper
                requiredPermissions={[{ resource: 'system:numbering', actions: ['create'] }]}
              >
                <Button type="primary" icon={<IconPlus />} onClick={openCreateCategory}>
                  {t['numbering.dict.category.create']}
                </Button>
              </PermissionWrapper>
            </Space>
          </div>
          <Table
            rowKey="id"
            columns={categoryColumns}
            data={categoryData}
            loading={categoryLoading}
            pagination={false}
            size="small"
          />
        </>
      ) : (
        <Alert type="info" content={t['numbering.dict.category.selectType']} />
      )}

      {/* ── 业务类型抽屉 ── */}
      <Drawer
        title={typeEditMode === 'create'
          ? t['numbering.dict.type.create']
          : t['numbering.dict.edit']}
        visible={typeDrawerVisible}
        onOk={handleTypeOk}
        onCancel={() => setTypeDrawerVisible(false)}
        width={440}
        unmountOnExit
      >
        <Form form={typeForm} layout="vertical">
          {typeEditMode === 'create' && (
            <FormItem
              label={t['numbering.dict.form.code']}
              field="code"
              rules={[{ required: true, message: t['numbering.dict.form.required'] }]}
            >
              <Input placeholder={t['numbering.dict.form.code.placeholder']} />
            </FormItem>
          )}
          {typeEditMode === 'edit' && (
            <Alert type="info" content={t['numbering.dict.form.lockedHint']} style={{ marginBottom: 16 }} />
          )}
          {typeEditMode === 'edit' && (
            <FormItem label={t['numbering.dict.form.code']}>
              <Input disabled value={typeData.find((x) => x.id === editingTypeId)?.code} />
            </FormItem>
          )}
          <FormItem
            label={t['numbering.dict.form.nameZh']}
            field="nameZh"
            rules={[{ required: true, message: t['numbering.dict.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem
            label={t['numbering.dict.form.nameEn']}
            field="nameEn"
            rules={[{ required: true, message: t['numbering.dict.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem label={t['numbering.dict.form.sortOrder']} field="sortOrder">
            <InputNumber min={0} style={{ width: '100%' }} />
          </FormItem>
        </Form>
      </Drawer>

      {/* ── 分类抽屉 ── */}
      <Drawer
        title={catEditMode === 'create'
          ? t['numbering.dict.category.create']
          : t['numbering.dict.edit']}
        visible={catDrawerVisible}
        onOk={handleCategoryOk}
        onCancel={() => setCatDrawerVisible(false)}
        width={440}
        unmountOnExit
      >
        {catEditMode === 'edit' && (
          <Alert type="info" content={t['numbering.dict.form.lockedHint']} style={{ marginBottom: 16 }} />
        )}
        <Form form={catForm} layout="vertical">
          {catEditMode === 'create' && (
            <>
              <FormItem label={t['numbering.dict.form.targetType']}>
                <Input disabled value={`${selectedType?.nameZh} (${selectedType?.code})`} />
              </FormItem>
              <FormItem
                label={t['numbering.dict.form.code']}
                field="code"
                rules={[{ required: true, message: t['numbering.dict.form.required'] }]}
              >
                <Input placeholder="如 COT" />
              </FormItem>
            </>
          )}
          {catEditMode === 'edit' && (
            <FormItem label={t['numbering.dict.form.code']}>
              <Input disabled value={categoryData.find((x) => x.id === editingCatId)?.code} />
            </FormItem>
          )}
          <FormItem
            label={t['numbering.dict.form.nameZh']}
            field="nameZh"
            rules={[{ required: true, message: t['numbering.dict.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem
            label={t['numbering.dict.form.nameEn']}
            field="nameEn"
            rules={[{ required: true, message: t['numbering.dict.form.required'] }]}
          >
            <Input />
          </FormItem>
          <FormItem label={t['numbering.dict.form.sortOrder']} field="sortOrder">
            <InputNumber min={0} style={{ width: '100%' }} />
          </FormItem>
        </Form>
      </Drawer>
    </Card>
  );
}
