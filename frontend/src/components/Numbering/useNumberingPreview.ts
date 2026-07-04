import { useState, useCallback } from 'react';
import { previewCode } from '@/api/numbering';
import { getActiveCategories, Category } from '@/api/numberingDictionary';

/**
 * 编号预览 + 分类码自判的可复用 hook。
 * 打开新建表单调 reload()；返回的 includeCategory 决定是否渲染分类选择器；
 * 选分类调 setCategoryCode()，hook 内自动重新 previewCode 刷新编号。
 * noRule=true 时调用方应禁表单 + Alert（守 Convention c02）。
 */
export function useNumberingPreview(targetType: string) {
  const [code, setCode] = useState<string | null>(null);
  const [codeLoading, setCodeLoading] = useState(false);
  const [noRule, setNoRule] = useState(false);
  const [includeCategory, setIncludeCategory] = useState(false);
  const [categoryOptions, setCategoryOptions] = useState<Category[]>([]);
  const [categoryCode, setCategoryCodeState] = useState<string | undefined>(undefined);

  // 表单打开新建时调用：首次预览 + 按需加载分类
  const reload = useCallback(() => {
    setCode(null);
    setNoRule(false);
    setIncludeCategory(false);
    setCategoryOptions([]);
    setCategoryCodeState(undefined);
    setCodeLoading(true);
    previewCode(targetType)
      .then((res) => {
        if (!res.code) {
          setNoRule(true);
          return;
        }
        setCode(res.code);
        setIncludeCategory(res.includeCategory);
        if (res.includeCategory) {
          getActiveCategories(targetType)
            .then(setCategoryOptions)
            .catch(() => setCategoryOptions([]));
        }
      })
      .catch(() => setNoRule(true))
      .finally(() => setCodeLoading(false));
  }, [targetType]);

  // 选分类 → 自动重新预览刷新编号
  const setCategoryCode = useCallback((c?: string) => {
    setCategoryCodeState(c);
    setCodeLoading(true);
    previewCode(targetType, c)
      .then((res) => setCode(res.code)) // 规则已在 reload 阶段确认存在
      .catch(() => setNoRule(true))
      .finally(() => setCodeLoading(false));
  }, [targetType]);

  return {
    code,
    codeLoading,
    noRule,
    includeCategory,
    categoryOptions,
    categoryCode,
    setCategoryCode,
    reload,
  };
}
