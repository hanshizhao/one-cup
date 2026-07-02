import { useContext } from 'react';
import { GlobalContext } from '../context';
import defaultLocale from '../locale';

function useLocale(locale: Record<string, any> | null = null) {
  const { lang } = useContext(GlobalContext);

  return (locale || (defaultLocale as Record<string, any>))[lang as string] || {};
}

export default useLocale;
