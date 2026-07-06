import {
  Button,
  Input,
  InputNumber,
  InputTag,
  Select,
  Space,
  Switch,
  Table,
} from '@arco-design/web-react';
import { IconDelete, IconPlus } from '@arco-design/web-react/icon';
import {
  PARAMETER_VALUE_TYPES,
  ParameterDefinitionDto,
  ParameterValueType,
} from '@/api/equipment';
import useLocale from '@/utils/useLocale';
import locale from '../locale';
import styles from '../style/index.module.less';

const Option = Select.Option;

interface Props {
  value: ParameterDefinitionDto[];
  onChange: (value: ParameterDefinitionDto[]) => void;
  /** 数值类型可选的单位列表（id → 名称 / 符号）。可选，未传则不显示单位列 */
  unitOptions?: { label: string; value: string }[];
}

/**
 * 参数定义动态表格（受控组件）。
 * - 每行：名称、值类型、按类型展开的约束（Number→min/max/precision/unit；Enum→options）、必填、删除。
 * - ValueType 切换时重置不相关字段。
 */
export default function ParameterEditor({ value, onChange, unitOptions = [] }: Props) {
  const t = useLocale(locale);

  const addRow = () =>
    onChange([
      ...value,
      {
        name: '',
        valueType: 'Number',
        required: false,
        sortOrder: value.length + 1,
      },
    ]);

  const removeRow = (idx: number) => onChange(value.filter((_, i) => i !== idx));

  const updateRow = (idx: number, patch: Partial<ParameterDefinitionDto>) =>
    onChange(value.map((v, i) => (i === idx ? { ...v, ...patch } : v)));

  // 切换值类型：清掉与该类型无关的约束字段
  const changeValueType = (idx: number, next: ParameterValueType) =>
    updateRow(idx, {
      valueType: next,
      minValue: undefined,
      maxValue: undefined,
      precision: undefined,
      unitId: undefined,
      options: undefined,
    });

  const columns = [
    {
      title: t['equipment.type.param.name'],
      dataIndex: 'name',
      width: 180,
      render: (_: unknown, record: ParameterDefinitionDto, idx: number) => (
        <Input
          value={record.name}
          placeholder={t['equipment.type.param.name.placeholder']}
          maxLength={50}
          onChange={(v) => updateRow(idx, { name: v })}
        />
      ),
    },
    {
      title: t['equipment.type.param.valueType'],
      dataIndex: 'valueType',
      width: 130,
      render: (_: unknown, record: ParameterDefinitionDto, idx: number) => (
        <Select
          value={record.valueType}
          onChange={(v: ParameterValueType) => changeValueType(idx, v)}
        >
          {PARAMETER_VALUE_TYPES.map((vt) => (
            <Option key={vt} value={vt}>
              {t[`equipment.type.param.valueType.${vt.toLowerCase()}`]}
            </Option>
          ))}
        </Select>
      ),
    },
    {
      title: t['equipment.type.detail.column.index'],
      key: 'constraint',
      render: (_: unknown, record: ParameterDefinitionDto, idx: number) => {
        if (record.valueType === 'Number') {
          return (
            <Space wrap>
              <InputNumber
                placeholder={t['equipment.type.param.min']}
                value={record.minValue ? Number(record.minValue) : undefined}
                style={{ width: 100 }}
                onChange={(v) =>
                  updateRow(idx, { minValue: v == null ? undefined : String(v) })
                }
              />
              <InputNumber
                placeholder={t['equipment.type.param.max']}
                value={record.maxValue ? Number(record.maxValue) : undefined}
                style={{ width: 100 }}
                onChange={(v) =>
                  updateRow(idx, { maxValue: v == null ? undefined : String(v) })
                }
              />
              <InputNumber
                placeholder={t['equipment.type.param.precision']}
                min={0}
                value={record.precision}
                style={{ width: 90 }}
                onChange={(v) => updateRow(idx, { precision: v == null ? undefined : v })}
              />
              {unitOptions.length > 0 && (
                <Select
                  allowClear
                  placeholder={t['equipment.type.param.unit']}
                  value={record.unitId}
                  style={{ width: 120 }}
                  onChange={(v) => updateRow(idx, { unitId: v })}
                >
                  {unitOptions.map((o) => (
                    <Option key={o.value} value={o.value}>
                      {o.label}
                    </Option>
                  ))}
                </Select>
              )}
            </Space>
          );
        }
        if (record.valueType === 'Enum') {
          return (
            <InputTag
              allowClear
              placeholder={t['equipment.type.param.options.placeholder']}
              value={record.options || []}
              style={{ width: '100%' }}
              onChange={(v) => updateRow(idx, { options: v })}
            />
          );
        }
        return '-';
      },
    },
    {
      title: t['equipment.type.param.required'],
      dataIndex: 'required',
      width: 90,
      render: (_: unknown, record: ParameterDefinitionDto, idx: number) => (
        <Switch
          checked={record.required}
          onChange={(checked: boolean) => updateRow(idx, { required: checked })}
        />
      ),
    },
    {
      title: t['equipment.type.param.operation'],
      key: 'operation',
      width: 80,
      render: (_: unknown, _record: ParameterDefinitionDto, idx: number) => (
        <Button
          type="text"
          status="danger"
          size="small"
          icon={<IconDelete />}
          onClick={() => removeRow(idx)}
        />
      ),
    },
  ];

  return (
    <div className={styles['parameter-editor']}>
      <Table
        size="small"
        pagination={false}
        data={value}
        columns={columns}
        rowKey={(record: ParameterDefinitionDto) => record.id ?? `${record.name}-${record.sortOrder}`}
        borderCell
      />
      <Button
        className={styles['param-add-btn']}
        type="dashed"
        long
        icon={<IconPlus />}
        onClick={addRow}
      >
        {t['equipment.type.button.addParameter']}
      </Button>
    </div>
  );
}
