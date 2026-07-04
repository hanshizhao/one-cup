import { Select } from '@arco-design/web-react';
import type { Category } from '@/api/numberingDictionary';

const { Option } = Select;

interface Props {
  options: Category[];
  value?: string;
  onChange?: (code?: string) => void;
  loading?: boolean;
  placeholder?: string;
}

/**
 * 编号分类码下拉。option 显示「code · 中文名」，value 用 code。
 * 给带编号的业务对象创建表单在 includeCategory=true 时条件渲染。
 */
export default function CategorySelect({ options, value, onChange, loading, placeholder }: Props) {
  return (
    <Select
      value={value}
      onChange={onChange}
      loading={loading}
      placeholder={placeholder}
      allowClear
      showSearch
    >
      {options.map((c) => (
        <Option key={c.code} value={c.code}>
          {c.code} · {c.nameZh}
        </Option>
      ))}
    </Select>
  );
}
