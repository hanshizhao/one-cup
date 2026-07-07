import { useMemo, useState } from 'react';
import {
  Alert,
  Button,
  Dropdown,
  Empty,
  Input,
  InputNumber,
  InputTag,
  Select,
  Space,
  Switch,
  Tag,
  Tooltip,
} from '@arco-design/web-react';
import {
  IconDelete,
  IconDragDotVertical,
  IconPlus,
} from '@arco-design/web-react/icon';
import {
  SortableContainer,
  SortableElement,
  SortableHandle,
  arrayMove,
} from 'react-sortable-hoc';
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

/** 校验错误结果：parameterId → 错误消息数组 */
type ErrorMap = Record<string, string[]>;

/** 参数定义实时校验（对应原型 computeErrors） */
function computeErrors(params: ParameterDefinitionDto[], t: Record<string, string>): ErrorMap {
  const errs: ErrorMap = {};
  const cnt: Record<string, number> = {};
  params.forEach((p) => {
    if (p.name && p.name.trim()) cnt[p.name] = (cnt[p.name] || 0) + 1;
  });
  params.forEach((p) => {
    const e: string[] = [];
    const key = p.id || `${p.name}-${p.sortOrder}`;
    if (!p.name || !p.name.trim()) {
      e.push(t['equipment.type.param.error.nameEmpty']);
    } else if (cnt[p.name] > 1) {
      e.push(t['equipment.type.param.error.nameDup'].replace('{name}', p.name));
    }
    if (p.valueType === 'Number') {
      const hasMin = p.minValue != null && p.minValue !== '';
      const hasMax = p.maxValue != null && p.maxValue !== '';
      if (hasMin && hasMax && Number(p.minValue) > Number(p.maxValue)) {
        e.push(
          t['equipment.type.param.error.minGtMax']
            .replace('{min}', String(p.minValue))
            .replace('{max}', String(p.maxValue))
        );
      }
      if (
        p.precision != null &&
        (Number(p.precision) < 0 || !Number.isInteger(Number(p.precision)))
      ) {
        e.push(t['equipment.type.param.error.precisionInvalid']);
      }
    }
    if (e.length) errs[key] = e;
  });
  return errs;
}

const DragHandle = SortableHandle(() => (
  <span className={styles['drag-handle']} title="拖动排序">
    <IconDragDotVertical />
  </span>
));

/** 单张参数卡片 */
// react-sortable-hoc 的 @types 对函数组件 + 自定义 props 的类型推断不友好（HOC 包裹后丢失业务 props 类型），
// 此处用 as any 绕过；运行时行为不受影响。组件内部 props 已显式标注类型，类型安全有保障。
const ParamCard: any = SortableElement(
  ({
    param,
    idx,
    errors,
    unitOptions,
    selected,
    onSelect,
    onUpdate,
    onRemove,
    onTypeChange,
  }: {
    param: ParameterDefinitionDto;
    idx: number;
    errors: string[];
    unitOptions: { label: string; value: string }[];
    selected: boolean;
    onSelect: () => void;
    onUpdate: (patch: Partial<ParameterDefinitionDto>) => void;
    onRemove: () => void;
    onTypeChange: (vt: ParameterValueType) => void;
  }) => {
    const t = useLocale(locale);
    const key = param.id || `${param.name}-${param.sortOrder}`;
    const isError = errors.length > 0;
    const tagClass =
      param.valueType === 'Number' ? 'num' : param.valueType === 'Enum' ? 'enum' : 'text';

    const constraintRender = () => {
      if (param.valueType === 'Number') {
        return (
          <div className={styles['constraint-grid']}>
            <div className={styles['field-cell']}>
              <div className={styles['field-cell-label']}>{t['equipment.type.param.min']}</div>
              <InputNumber
                placeholder={t['equipment.type.param.min.placeholder']}
                value={param.minValue != null ? Number(param.minValue) : undefined}
                onChange={(v) => onUpdate({ minValue: v == null ? undefined : String(v) })}
                style={{ width: '100%' }}
              />
            </div>
            <div className={styles['field-cell']}>
              <div className={styles['field-cell-label']}>{t['equipment.type.param.max']}</div>
              <InputNumber
                placeholder={t['equipment.type.param.max.placeholder']}
                value={param.maxValue != null ? Number(param.maxValue) : undefined}
                onChange={(v) => onUpdate({ maxValue: v == null ? undefined : String(v) })}
                style={{ width: '100%' }}
              />
            </div>
            <div className={styles['field-cell']}>
              <div className={styles['field-cell-label']}>{t['equipment.type.param.precision']}</div>
              <InputNumber
                placeholder="如 0"
                min={0}
                value={param.precision}
                onChange={(v) => onUpdate({ precision: v == null ? undefined : v })}
                style={{ width: '100%' }}
              />
            </div>
            {unitOptions.length > 0 && (
              <div className={styles['field-cell']}>
                <div className={styles['field-cell-label']}>{t['equipment.type.param.unit']}</div>
                <Select
                  allowClear
                  placeholder={t['equipment.type.param.unit.placeholder']}
                  value={param.unitId}
                  onChange={(v) => onUpdate({ unitId: v })}
                  style={{ width: '100%' }}
                >
                  {unitOptions.map((o) => (
                    <Option key={o.value} value={o.value}>
                      {o.label}
                    </Option>
                  ))}
                </Select>
              </div>
            )}
          </div>
        );
      }
      if (param.valueType === 'Enum') {
        return (
          <InputTag
            allowClear
            placeholder={t['equipment.type.param.options.placeholder']}
            value={param.options || []}
            onChange={(v) => onUpdate({ options: v })}
            style={{ width: '100%' }}
          />
        );
      }
      return <div className={styles['text-hint']}>{t['equipment.type.param.textHint']}</div>;
    };

    return (
      <div
        className={`${styles['param-card']} ${isError ? styles['is-error'] : ''} ${
          selected ? styles['is-selected'] : ''
        }`}
        onClick={(e) => {
          if ((e.target as HTMLElement).closest('input, select, button, .arco-select, .arco-switch, .' + styles['drag-handle'])) return;
          onSelect();
        }}
      >
        <div className={styles['param-card-head']}>
          <DragHandle />
          <span className={styles['param-order']}>{idx + 1}</span>
          <div className={styles['param-name-wrap']}>
            <Input
              value={param.name}
              placeholder={t['equipment.type.param.name.placeholder']}
              maxLength={50}
              onChange={(v) => onUpdate({ name: v })}
              style={{ background: 'transparent' }}
            />
          </div>
          <Dropdown
            position="bl"
            droplist={
              <div className={styles['vt-menu']}>
                {PARAMETER_VALUE_TYPES.map((vt) => (
                  <div
                    key={vt}
                    className={`${styles['vt-menu-item']} ${param.valueType === vt ? styles['on'] : ''}`}
                    onClick={() => onTypeChange(vt)}
                  >
                    <span className={`${styles['type-tag']} ${styles[vt.toLowerCase()]}`}>
                      {t[`equipment.type.param.valueType.${vt.toLowerCase()}`]}
                    </span>
                    {param.valueType === vt && <span style={{ marginLeft: 'auto', color: 'var(--color-primary-6)' }}>✓</span>}
                  </div>
                ))}
              </div>
            }
          >
            <span className={`${styles['type-tag']} ${styles[tagClass]} ${styles['vt-toggle']}`}>
              {t[`equipment.type.param.valueType.${param.valueType.toLowerCase()}`]}
            </span>
          </Dropdown>
          <div className={styles['param-required']}>
            <span className={styles['typo-small']}>{t['equipment.type.param.required']}</span>
            <Switch
              checked={param.required}
              onChange={(checked: boolean) => onUpdate({ required: checked })}
            />
          </div>
          <Tooltip content={t['equipment.type.param.delete']}>
            <Button
              type="text"
              status="danger"
              size="small"
              icon={<IconDelete />}
              onClick={onRemove}
            />
          </Tooltip>
        </div>
        <div className={styles['param-card-body']}>{constraintRender()}</div>
        {isError && (
          <div className={styles['param-card-error']}>{errors.join('；')}</div>
        )}
      </div>
    );
  }
);

const SortableList: any = SortableContainer(
  ({
    items,
    errorMap,
    unitOptions,
    selectedId,
    onSelect,
    onUpdate,
    onRemove,
    onTypeChange,
  }: {
    items: ParameterDefinitionDto[];
    errorMap: ErrorMap;
    unitOptions: { label: string; value: string }[];
    selectedId: string | null;
    onSelect: (id: string) => void;
    onUpdate: (idx: number, patch: Partial<ParameterDefinitionDto>) => void;
    onRemove: (idx: number) => void;
    onTypeChange: (idx: number, vt: ParameterValueType) => void;
  }) => {
    const t = useLocale(locale);
    return (
      <div>
        {items.map((p, idx) => {
          const key = p.id || `${p.name}-${p.sortOrder}`;
          return (
            <ParamCard
              key={key}
              index={idx}
              param={p}
              idx={idx}
              errors={errorMap[key] || []}
              unitOptions={unitOptions}
              selected={selectedId === key}
              onSelect={() => onSelect(key)}
              onUpdate={(patch: Partial<ParameterDefinitionDto>) => onUpdate(idx, patch)}
              onRemove={() => onRemove(idx)}
              onTypeChange={(vt: ParameterValueType) => onTypeChange(idx, vt)}
            />
          );
        })}
        <Button
          className={styles['param-add-btn']}
          type="dashed"
          long
          icon={<IconPlus />}
          onClick={() => onSelect('__add__')}
        >
          {t['equipment.type.button.addParameter']}
        </Button>
      </div>
    );
  }
);

/**
 * 参数定义编辑器（优化版）：竖向参数卡片 + 拖拽排序 + 实时校验 + 右栏预览。
 */
export default function ParameterEditor({ value, onChange, unitOptions = [] }: Props) {
  const t = useLocale(locale);
  const [selectedKey, setSelectedKey] = useState<string | null>(null);

  const errorMap = useMemo(() => computeErrors(value, t), [value, t]);
  const errorCount = Object.keys(errorMap).length;

  const stats = useMemo(() => ({
    total: value.length,
    number: value.filter((p) => p.valueType === 'Number').length,
    enum: value.filter((p) => p.valueType === 'Enum').length,
    text: value.filter((p) => p.valueType === 'Text').length,
  }), [value]);

  const addRow = () => {
    const newParam: ParameterDefinitionDto = {
      name: '',
      valueType: 'Number',
      required: false,
      sortOrder: value.length + 1,
    };
    onChange([...value, newParam]);
    setSelectedKey(`-${value.length + 1}-`);
  };

  const removeRow = (idx: number) => onChange(value.filter((_, i) => i !== idx));

  const updateRow = (idx: number, patch: Partial<ParameterDefinitionDto>) =>
    onChange(value.map((v, i) => (i === idx ? { ...v, ...patch } : v)));

  const changeValueType = (idx: number, next: ParameterValueType) =>
    updateRow(idx, {
      valueType: next,
      minValue: undefined,
      maxValue: undefined,
      precision: undefined,
      unitId: undefined,
      options: undefined,
    });

  const onSortEnd = ({ oldIndex, newIndex }: { oldIndex: number; newIndex: number }) => {
    if (oldIndex !== newIndex) {
      const reordered = arrayMove(value, oldIndex, newIndex).map((p, i) => ({
        ...p,
        sortOrder: i + 1,
      }));
      onChange(reordered);
    }
  };

  const handleSelect = (id: string) => {
    if (id === '__add__') {
      addRow();
      return;
    }
    setSelectedKey(id);
  };

  // 选中的参数（用于右栏预览）
  const selectedParam = useMemo(() => {
    if (!selectedKey) return value[0] || null;
    return value.find((p) => (p.id || `${p.name}-${p.sortOrder}`) === selectedKey) || value[0] || null;
  }, [value, selectedKey]);

  const stepOf = (p: ParameterDefinitionDto) =>
    p.precision != null ? Math.pow(10, -p.precision) : 1;

  return (
    <div className={styles['editor-layout']}>
      <div className={styles['param-list-col']}>
        {/* 统计条 */}
        <div className={styles['param-stats']}>
          <div className={styles['param-stat']}>
            <b>{stats.total}</b>
            <span>{t['equipment.type.param.stat.total']}</span>
          </div>
          <span className={styles['param-stat-divider']} />
          <div className={styles['param-stat']}>
            <b>{stats.number}</b>
            <span>{t['equipment.type.param.stat.number']}</span>
          </div>
          <span className={styles['param-stat-divider']} />
          <div className={styles['param-stat']}>
            <b>{stats.enum}</b>
            <span>{t['equipment.type.param.stat.enum']}</span>
          </div>
          <span className={styles['param-stat-divider']} />
          <div className={styles['param-stat']}>
            <b>{stats.text}</b>
            <span>{t['equipment.type.param.stat.text']}</span>
          </div>
          <span className={styles['param-stat-divider']} />
          <div className={`${styles['param-stat']} ${errorCount > 0 ? styles['stat-error'] : ''}`}>
            <b>{errorCount}</b>
            <span>{t['equipment.type.param.stat.error']}</span>
          </div>
          <span style={{ marginLeft: 'auto', color: 'var(--color-text-3)', fontSize: 12 }}>
            {t['equipment.type.param.dragHint']}
          </span>
        </div>

        {value.length === 0 ? (
          <>
            <Empty description={t['equipment.type.param.empty']} />
            <Button
              className={styles['param-add-btn']}
              type="dashed"
              long
              icon={<IconPlus />}
              onClick={addRow}
            >
              {t['equipment.type.button.addParameter']}
            </Button>
          </>
        ) : (
          // SortableList 的类型因 react-sortable-hoc 的 @types 限制无法精确推断业务 props，用 as any 绕过
          <SortableList
            items={value}
            onSortEnd={onSortEnd}
            useDragHandle
            lockAxis="y"
            errorMap={errorMap}
            unitOptions={unitOptions}
            selectedId={selectedKey}
            onSelect={handleSelect}
            onUpdate={updateRow}
            onRemove={removeRow}
            onTypeChange={changeValueType}
          />
        )}

        {errorCount > 0 && (
          <Alert
            type="error"
            style={{ marginTop: 12 }}
            content={t['equipment.type.param.error.summary'].replace('{n}', String(errorCount))}
          />
        )}
      </div>

      {/* 右栏预览 */}
      <div className={styles['preview-panel']}>
        <div className={styles['preview-head']}>
          <div className={styles['preview-head-title']}>
            {t['equipment.type.param.preview.title']}
          </div>
          <div className={styles['preview-head-sub']}>
            {t['equipment.type.param.preview.sub']}
          </div>
        </div>
        <div className={styles['preview-body']}>
          {selectedParam ? (
            <>
              <div className={styles['preview-mock-label']}>
                {selectedParam.name || t['equipment.type.param.preview.unnamed']}
                {selectedParam.required && (
                  <span style={{ color: 'var(--color-danger-6)' }}>*</span>
                )}
                <Tag
                  size="small"
                  color={
                    selectedParam.valueType === 'Number'
                      ? 'arcoblue'
                      : selectedParam.valueType === 'Enum'
                      ? 'green'
                      : 'gray'
                  }
                  style={{ marginLeft: 6 }}
                >
                  {t[`equipment.type.param.valueType.${selectedParam.valueType.toLowerCase()}`]}
                </Tag>
              </div>
              {selectedParam.valueType === 'Number' && (
                <>
                  <InputNumber
                    placeholder={t['equipment.template.value.placeholder']}
                    min={selectedParam.minValue != null ? Number(selectedParam.minValue) : undefined}
                    max={selectedParam.maxValue != null ? Number(selectedParam.maxValue) : undefined}
                    step={stepOf(selectedParam)}
                    precision={selectedParam.precision}
                    suffix={selectedParam.unitId || undefined}
                    style={{ width: '100%' }}
                  />
                  <div className={styles['preview-hint']}>
                    {selectedParam.minValue != null || selectedParam.maxValue != null
                      ? `${t['equipment.template.value.range']} ${selectedParam.minValue ?? '−∞'} ~ ${selectedParam.maxValue ?? '+∞'}`
                      : t['equipment.type.param.preview.noRange']}
                    {selectedParam.precision != null && ` · ${t['equipment.template.value.precisionLabel']} ${selectedParam.precision}`}
                    {` · ${t['equipment.template.value.stepLabel']} ${stepOf(selectedParam)}`}
                  </div>
                </>
              )}
              {selectedParam.valueType === 'Enum' && (
                <>
                  <Select placeholder={t['equipment.template.value.placeholder']} style={{ width: '100%' }}>
                    {(selectedParam.options || []).map((o) => (
                      <Option key={o} value={o}>
                        {o}
                      </Option>
                    ))}
                  </Select>
                  <div className={styles['preview-hint']}>
                    {t['equipment.template.value.optionsLabel']}：{' '}
                    {(selectedParam.options || []).length > 0
                      ? (selectedParam.options || []).join(' / ')
                      : t['equipment.type.param.preview.noOptions']}
                  </div>
                </>
              )}
              {selectedParam.valueType === 'Text' && (
                <>
                  <Input placeholder={t['equipment.template.value.placeholder']} maxLength={200} />
                  <div className={styles['preview-hint']}>{t['equipment.template.value.textHint']}</div>
                </>
              )}
              <div className={styles['preview-foot']}>
                {t['equipment.type.param.preview.order'].replace('{n}', String((value.indexOf(selectedParam) || 0) + 1))}
              </div>
            </>
          ) : (
            <Empty description={t['equipment.type.param.preview.empty']} />
          )}
        </div>
      </div>
    </div>
  );
}
