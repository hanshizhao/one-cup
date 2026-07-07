import { useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Collapse,
  Empty,
  Input,
  InputNumber,
  Select,
  Space,
  Tag,
} from '@arco-design/web-react';
import { IconDelete } from '@arco-design/web-react/icon';
import {
  EquipmentTemplateValueDto,
  EquipmentTypeParameterDto,
  ParameterValueType,
  TemplateValueDto,
} from '@/api/equipment';
import useLocale from '@/utils/useLocale';
import locale from '../../locale';
import styles from '../../style/index.module.less';

const CollapseItem = Collapse.Item;
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

/** 单值校验结果 */
interface CheckResult {
  ok: boolean;
  msg: string;
}

/**
 * 模板值动态表单（优化版）：按 valueType 分组、实时校验、状态汇总、一键清孤儿。
 * - Number → InputNumber（补齐 min/max/step/suffix，修复实锤缺陷）
 * - Enum   → Select
 * - Text   → Input
 * 编辑态：existingValues 提供后端校验状态（invalid/orphan）；
 * 用户改动过的值用前端实时校验覆盖后端状态。
 */
export default function TemplateValueEditor({
  parameters,
  values,
  existingValues,
  onChange,
}: Props) {
  const t = useLocale(locale);

  /** 数值参数前端实时校验（对应原型 validateNum） */
  const validateNumber = (
    param: EquipmentTypeParameterDto,
    raw: string | undefined
  ): CheckResult => {
    const v = raw == null ? '' : String(raw).trim();
    if (v === '') {
      return param.required
        ? { ok: false, msg: t['equipment.template.value.error.required'] }
        : { ok: true, msg: '' };
    }
    const n = Number(v);
    if (!Number.isFinite(n)) {
      return { ok: false, msg: t['equipment.template.value.error.notNumber'] };
    }
    if (param.minValue != null && n < Number(param.minValue)) {
      return { ok: false, msg: t['equipment.template.value.error.belowMin'].replace('{min}', String(param.minValue)) };
    }
    if (param.maxValue != null && n > Number(param.maxValue)) {
      return { ok: false, msg: t['equipment.template.value.error.aboveMax'].replace('{max}', String(param.maxValue)) };
    }
    if (param.precision != null) {
      const dp = v.includes('.') ? v.split('.')[1].length : 0;
      if (dp > param.precision) {
        return { ok: false, msg: t['equipment.template.value.error.precision'].replace('{n}', String(param.precision)) };
      }
    }
    return { ok: true, msg: '' };
  };

  // 追踪用户改动过的 parameterId（用前端校验覆盖后端 status）
  const [edited, setEdited] = useState<Set<string>>(new Set());

  // 编辑模式：parameterId → 带状态值
  const statusMap = useMemo(() => {
    const m: Record<string, EquipmentTemplateValueDto> = {};
    (existingValues || []).forEach((v) => {
      m[v.parameterId] = v;
    });
    return m;
  }, [existingValues]);

  // 当前值查找 map
  const valueMap = useMemo(() => {
    const m: Record<string, TemplateValueDto> = {};
    values.forEach((v) => {
      m[v.parameterId] = v;
    });
    return m;
  }, [values]);

  const updateValue = (parameterId: string, value?: string) => {
    setEdited((prev) => {
      const next = new Set(prev);
      next.add(parameterId);
      return next;
    });
    const exists = valueMap[parameterId];
    const nextValues = exists
      ? values.map((v) => (v.parameterId === parameterId ? { ...v, value } : v))
      : [...values, { parameterId, value }];
    onChange(nextValues);
  };

  /** 当前每行状态：用户编辑过用前端校验，否则用后端 serverStatus */
  const rowStatus = (param: EquipmentTypeParameterDto): CheckResult => {
    const v = valueMap[param.id];
    if (edited.has(param.id) && v) {
      if (param.valueType === 'Number') return validateNumber(param, v.value);
      if (param.valueType === 'Enum') {
        const ok = !v.value || (param.options || []).includes(v.value);
        return ok ? { ok: true, msg: '' } : { ok: false, msg: t['equipment.template.value.error.notOption'] };
      }
      return { ok: true, msg: '' };
    }
    const sv = statusMap[param.id];
    if (!sv) return { ok: true, msg: '' };
    if (sv.status === 'invalid') return { ok: false, msg: sv.statusMessage || t['equipment.template.value.invalidTip'] };
    if (sv.status === 'orphan') return { ok: false, msg: sv.statusMessage || t['equipment.template.value.orphanTip'] };
    return { ok: true, msg: '' };
  };

  // 孤儿值：参数定义已删除，模板残留（existingValues 里 status=orphan 的项）
  const orphans = useMemo(
    () => (existingValues || []).filter((v) => v.status === 'orphan'),
    [existingValues]
  );

  // 分组
  const numParams = parameters.filter((p) => p.valueType === 'Number');
  const enumParams = parameters.filter((p) => p.valueType === 'Enum');
  const textParams = parameters.filter((p) => p.valueType === 'Text');

  // 统计
  const invalidCount = parameters.filter((p) => !rowStatus(p).ok).length;
  const orphanCount = orphans.length;
  const totalIssue = invalidCount + orphanCount;

  // 一键清孤儿：从 existingValues 移除 orphan 项（这些值已不在 parameters 里，无需改 values）
  const clearOrphan = (parameterId: string) => {
    setEdited((prev) => {
      const next = new Set(prev);
      next.add(parameterId);
      return next;
    });
    // 孤儿值的 parameterId 不在 parameters 里，从 values 删掉对应项（如果有）
    onChange(values.filter((v) => v.parameterId !== parameterId));
  };
  const clearAllOrphans = () => {
    const orphanIds = new Set(orphans.map((o) => o.parameterId));
    onChange(values.filter((v) => !orphanIds.has(v.parameterId)));
    setEdited((prev) => {
      const next = new Set(prev);
      orphans.forEach((o) => next.add(o.parameterId));
      return next;
    });
  };

  const stepOf = (p: EquipmentTypeParameterDto) =>
    p.precision != null ? Math.pow(10, -p.precision) : 1;

  if (parameters.length === 0 && orphans.length === 0) {
    return <Empty description={t['equipment.template.form.values.empty']} />;
  }

  // 渲染单行值输入
  const renderRow = (param: EquipmentTypeParameterDto) => {
    const current = valueMap[param.id];
    const st = rowStatus(param);
    const isError = !st.ok;
    const label = param.required ? `${param.name} *` : param.name;
    const rangeText =
      param.valueType === 'Number'
        ? `${t['equipment.template.value.range']}: ${param.minValue ?? '−∞'} ~ ${param.maxValue ?? '+∞'}${
            param.precision != null ? ` · ${t['equipment.template.value.precisionLabel']} ${param.precision}` : ''
          }`
        : param.valueType === 'Enum'
        ? `${t['equipment.template.value.optionsLabel']}: ${(param.options || []).join(' / ')}`
        : `${t['equipment.template.value.textHint']}`;

    return (
      <div
        key={param.id}
        className={`${styles['tpl-value-row']} ${isError ? styles['is-invalid'] : ''}`}
      >
        <div className={styles['tpl-value-strip']} />
        <div className={styles['tpl-value-label']}>
          <div className={styles['tpl-value-name']}>{label}</div>
          <div className={styles['tpl-value-meta']}>{rangeText}</div>
        </div>
        <div className={styles['tpl-value-input']}>
          {param.valueType === 'Number' && (
            <InputNumber
              placeholder={t['equipment.template.value.placeholder']}
              value={current?.value != null ? Number(current.value) : undefined}
              style={{ width: '100%' }}
              min={param.minValue != null ? Number(param.minValue) : undefined}
              max={param.maxValue != null ? Number(param.maxValue) : undefined}
              step={stepOf(param)}
              precision={param.precision}
              suffix={param.unitSymbol || undefined}
              error={isError}
              onChange={(v) => updateValue(param.id, v == null ? undefined : String(v))}
            />
          )}
          {param.valueType === 'Enum' && (
            <Select
              placeholder={t['equipment.template.value.placeholder']}
              value={current?.value}
              allowClear
              error={isError}
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
              error={isError}
              onChange={(v) => updateValue(param.id, v || undefined)}
            />
          )}
        </div>
        <div className={styles['tpl-value-help']}>
          {isError ? (
            <span className={styles['tpl-value-help-error']}>{st.msg}</span>
          ) : (
            <span className={styles['tpl-value-help-meta']}>
              {param.valueType === 'Number'
                ? `${t['equipment.template.value.unitLabel']} ${param.unitSymbol || t['equipment.template.value.noUnit']} · ${t['equipment.template.value.stepLabel']} ${stepOf(param)}`
                : ''}
            </span>
          )}
        </div>
      </div>
    );
  };

  // 渲染孤儿行
  const renderOrphanRow = (o: EquipmentTemplateValueDto) => (
    <div key={o.parameterId} className={`${styles['tpl-value-row']} ${styles['is-orphan']}`}>
      <div className={styles['tpl-value-strip']} />
      <div className={styles['tpl-value-label']}>
        <div className={styles['tpl-value-name']}>
          <Tag color="warning" size="small" style={{ marginRight: 6 }}>
            {t['equipment.template.value.orphanTag']}
          </Tag>
          {o.parameterName}
        </div>
        <div className={styles['tpl-value-meta']}>{t['equipment.template.value.orphanMeta']}</div>
      </div>
      <div className={styles['tpl-value-input']}>
        <div className={styles['tpl-orphan-value']}>{o.value}</div>
      </div>
      <div className={styles['tpl-value-help']}>
        <span className={styles['tpl-value-help-warn']}>{o.statusMessage}</span>
        <Button
          type="text"
          status="danger"
          size="small"
          icon={<IconDelete />}
          onClick={() => clearOrphan(o.parameterId)}
        >
          {t['equipment.template.value.clearOrphan']}
        </Button>
      </div>
    </div>
  );

  // 分组配置
  const groups: { key: string; title: string; count: number; body: React.ReactNode; hasIssue: boolean }[] = [];
  if (numParams.length > 0) {
    groups.push({
      key: 'num',
      title: t['equipment.template.value.group.number'],
      count: numParams.length,
      body: <>{numParams.map(renderRow)}</>,
      hasIssue: numParams.some((p) => !rowStatus(p).ok),
    });
  }
  if (enumParams.length > 0) {
    groups.push({
      key: 'enum',
      title: t['equipment.template.value.group.enum'],
      count: enumParams.length,
      body: <>{enumParams.map(renderRow)}</>,
      hasIssue: enumParams.some((p) => !rowStatus(p).ok),
    });
  }
  if (textParams.length > 0) {
    groups.push({
      key: 'text',
      title: t['equipment.template.value.group.text'],
      count: textParams.length,
      body: <>{textParams.map(renderRow)}</>,
      hasIssue: false,
    });
  }
  if (orphans.length > 0) {
    groups.push({
      key: 'orphan',
      title: t['equipment.template.value.group.orphan'],
      count: orphans.length,
      body: <>{orphans.map(renderOrphanRow)}</>,
      hasIssue: true,
    });
  }

  return (
    <div>
      {/* 状态汇总横幅 */}
      {totalIssue === 0 ? (
        <Alert
          type="success"
          style={{ marginBottom: 12 }}
          content={t['equipment.template.value.summary.allValid']}
        />
      ) : (
        <Alert
          type="error"
          style={{ marginBottom: 12 }}
          content={
            <div className={styles['tpl-status-summary']}>
              <span style={{ fontWeight: 600 }}>
                {t['equipment.template.value.summary.hasIssue'].replace('{n}', String(totalIssue))}
              </span>
              <Space size={12} style={{ marginLeft: 12 }}>
                {invalidCount > 0 && (
                  <span>
                    <b style={{ color: 'var(--color-danger-6)' }}>{invalidCount}</b>{' '}
                    {t['equipment.template.value.summary.invalid']}
                  </span>
                )}
                {orphanCount > 0 && (
                  <span>
                    <b style={{ color: 'var(--color-warning-6)' }}>{orphanCount}</b>{' '}
                    {t['equipment.template.value.summary.orphan']}
                  </span>
                )}
              </Space>
              {orphanCount > 0 && (
                <Button
                  type="outline"
                  status="danger"
                  size="small"
                  style={{ marginLeft: 'auto' }}
                  onClick={clearAllOrphans}
                >
                  {t['equipment.template.value.summary.clearAllOrphan'].replace('{n}', String(orphanCount))}
                </Button>
              )}
            </div>
          }
        />
      )}

      <Collapse
        defaultActiveKey={groups.map((g) => g.key)}
        expandIconPosition="left"
        style={{ background: 'var(--color-fill-1)', borderRadius: 6 }}
      >
        {groups.map((g) => (
          <CollapseItem
            key={g.key}
            name={g.key}
            header={
              <Space size={8}>
                <span style={{ fontWeight: 500 }}>{g.title}</span>
                <span style={{ color: 'var(--color-text-3)' }}>{g.count}</span>
                {g.hasIssue && (
                  <Tag color="red" size="small">
                    {t['equipment.template.value.needFix']}
                  </Tag>
                )}
              </Space>
            }
          >
            <div style={{ background: 'var(--color-bg-1)', borderRadius: 4 }}>
              {g.body}
            </div>
          </CollapseItem>
        ))}
      </Collapse>
    </div>
  );
}
