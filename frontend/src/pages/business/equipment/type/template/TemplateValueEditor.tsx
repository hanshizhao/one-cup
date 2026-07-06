import { useMemo } from 'react';
import {
  Alert,
  Empty,
  Form,
  Input,
  InputNumber,
  Select,
  Space,
} from '@arco-design/web-react';
import {
  EquipmentTemplateValueDto,
  EquipmentTypeParameterDto,
  TemplateValueDto,
} from '@/api/equipment';
import useLocale from '@/utils/useLocale';
import locale from '../../locale';

const FormItem = Form.Item;
const Option = Select.Option;

interface Props {
  /** 该设备类型的参数定义（行来源） */
  parameters: EquipmentTypeParameterDto[];
  /** 当前编辑中的值（受控） */
  values: TemplateValueDto[];
  /** 编辑模式：后端返回的带状态值（用于展示 invalid/orphan 标记） */
  existingValues?: EquipmentTemplateValueDto[];
  onChange: (values: TemplateValueDto[]) => void;
}

/**
 * 模板值动态表单：按参数定义遍历渲染输入控件。
 * - Number → InputNumber（带单位后缀展示）
 * - Enum   → Select（options）
 * - Text   → Input
 * 编辑场景下，existingValues 提供 status：
 * - valid  → 正常
 * - invalid → 值越界 → 标红 + Alert 提示
 * - orphan  → 参数定义已删除（孤儿行）→ 标红
 */
export default function TemplateValueEditor({
  parameters,
  values,
  existingValues,
  onChange,
}: Props) {
  const t = useLocale(locale);

  // 编辑模式：parameterId → 带状态值（用于读取 status / statusMessage）
  const statusMap = useMemo(() => {
    const m: Record<string, EquipmentTemplateValueDto> = {};
    (existingValues || []).forEach((v) => {
      m[v.parameterId] = v;
    });
    return m;
  }, [existingValues]);

  // 当前值的查找 map
  const valueMap = useMemo(() => {
    const m: Record<string, TemplateValueDto> = {};
    values.forEach((v) => {
      m[v.parameterId] = v;
    });
    return m;
  }, [values]);

  const updateValue = (parameterId: string, value?: string) => {
    const exists = valueMap[parameterId];
    const next = exists
      ? values.map((v) => (v.parameterId === parameterId ? { ...v, value } : v))
      : [...values, { parameterId, value }];
    onChange(next);
  };

  // 收集需提示的异常项（编辑模式）
  const invalidItems = (existingValues || []).filter(
    (v) => v.status === 'invalid' || v.status === 'orphan'
  );
  const orphanCount = invalidItems.filter((v) => v.status === 'orphan').length;
  const invalidCount = invalidItems.filter((v) => v.status === 'invalid').length;

  if (parameters.length === 0) {
    return <Empty description={t['equipment.template.form.values.empty']} />;
  }

  return (
    <div>
      {orphanCount > 0 && (
        <Alert
          type="warning"
          style={{ marginBottom: 12 }}
          content={`${t['equipment.template.value.orphanTip']}（${orphanCount}）`}
        />
      )}
      {invalidCount > 0 && (
        <Alert
          type="error"
          style={{ marginBottom: 12 }}
          content={
            <Space direction="vertical" size={4}>
              {invalidItems
                .filter((v) => v.status === 'invalid')
                .map((v) => (
                  <span key={v.parameterId}>
                    {v.parameterName}：{v.statusMessage || t['equipment.template.value.invalidTip']}
                  </span>
                ))}
            </Space>
          }
        />
      )}
      {parameters.map((param) => {
        const current = valueMap[param.id];
        const statusItem = statusMap[param.id];
        const isError = statusItem?.status === 'invalid' || statusItem?.status === 'orphan';
        const label = param.required ? `${param.name} *` : param.name;
        return (
          <FormItem
            key={param.id}
            label={label}
            validateStatus={isError ? 'error' : undefined}
            help={
              isError && statusItem?.statusMessage
                ? statusItem.statusMessage
                : param.unitSymbol
                  ? `(${param.unitSymbol})`
                  : undefined
            }
          >
            {param.valueType === 'Number' && (
              <InputNumber
                placeholder={t['equipment.template.value.placeholder']}
                value={current?.value != null ? Number(current.value) : undefined}
                style={{ width: '100%' }}
                precision={param.precision}
                error={isError}
                onChange={(v) =>
                  updateValue(param.id, v == null ? undefined : String(v))
                }
              />
            )}
            {param.valueType === 'Enum' && (
              <Select
                placeholder={t['equipment.template.value.placeholder']}
                value={current?.value}
                allowClear
                onChange={(v: string) => updateValue(param.id, v || undefined)}
              >
                {(param.options || []).map((o) => (
                  <Option key={o} value={o}>
                    {o}
                  </Option>
                ))}
              </Select>
            )}
            {param.valueType === 'Text' && (
              <Input
                placeholder={t['equipment.template.value.placeholder']}
                value={current?.value}
                maxLength={200}
                status={isError ? 'error' : undefined}
                onChange={(v) => updateValue(param.id, v || undefined)}
              />
            )}
          </FormItem>
        );
      })}
    </div>
  );
}
