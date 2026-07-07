import { useEffect, useState } from 'react';
import { Popover, Spin, Tag } from '@arco-design/web-react';
import { getEquipmentTemplateById } from '@/api/equipment';
import type { EquipmentTemplateValueDto } from '@/api/equipment';
import useLocale from '@/utils/useLocale';
import locale from '../locale';
import styles from '../style/index.module.less';

interface Props {
  /** 子元素（模板项） */
  children: React.ReactNode;
  /** 所属设备类型 Id（用于拼接口路径） */
  typeId: string;
  /** 模板 Id */
  templateId: string;
  /** 模板名称（气泡标题） */
  templateName: string;
}

/**
 * 模板值气泡：点击模板项时 fetch 该模板的参数值，只读展示。
 * 不跳转、不可编辑——设备详情 Drawer 的纯浏览关联。
 */
export default function TemplateValuePopover({ children, typeId, templateId, templateName }: Props) {
  const t = useLocale(locale);
  const [visible, setVisible] = useState(false);
  const [loading, setLoading] = useState(false);
  const [values, setValues] = useState<EquipmentTemplateValueDto[] | null>(null);

  // 首次展开时拉取数据；后续重复展开沿用缓存
  useEffect(() => {
    if (!visible || values !== null) return;
    setLoading(true);
    getEquipmentTemplateById(typeId, templateId)
      .then((res) => setValues(res.values || []))
      .catch(() => setValues([]))
      .finally(() => setLoading(false));
  }, [visible, typeId, templateId, values]);

  const vtColor = (vt: string) =>
    vt === 'Number' ? 'arcoblue' : vt === 'Enum' ? 'green' : 'gray';

  return (
    <Popover
      trigger="click"
      position="top"
      popupVisible={visible}
      onVisibleChange={setVisible}
      title={templateName}
      content={
        <Spin loading={loading} style={{ display: 'block', minHeight: 40 }}>
          <div className={styles['template-value-list']}>
            {values && values.length === 0 && !loading && (
              <div className={styles['template-value-empty']}>-</div>
            )}
            {values?.map((v) => (
              <div key={v.parameterId} className={styles['template-value-row']}>
                <span className={styles['template-value-name']}>{v.parameterName}</span>
                <Tag size="small" color={vtColor(v.valueType)} style={{ marginRight: 0 }}>
                  {t[`equipment.type.param.valueType.${v.valueType.toLowerCase()}`]}
                </Tag>
                {v.value ? (
                  <span className={styles['template-value-text']}>
                    {v.value}
                    {v.unitSymbol ? ` ${v.unitSymbol}` : ''}
                  </span>
                ) : (
                  <span className={styles['template-value-empty']}>-</span>
                )}
                {v.status && v.status !== 'valid' && (
                  <span
                    className={`${styles['template-value-status']} ${
                      v.status === 'invalid' ? styles.invalid : styles.orphan
                    }`}
                  >
                    {v.statusMessage}
                  </span>
                )}
              </div>
            ))}
          </div>
        </Spin>
      }
    >
      {children}
    </Popover>
  );
}
