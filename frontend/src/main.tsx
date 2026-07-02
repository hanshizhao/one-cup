import './style/global.less';
import React, { useEffect } from 'react';
import { createRoot } from 'react-dom/client';
import { Provider } from 'react-redux';
import { ConfigProvider } from '@arco-design/web-react';
import zhCN from '@arco-design/web-react/es/locale/zh-CN';
import enUS from '@arco-design/web-react/es/locale/en-US';
import { BrowserRouter, Switch, Route } from 'react-router-dom';
import { getCurrentUser } from '@/api/auth';
import { store, setUserInfo } from './store';
import PageLayout from './layout';
import { GlobalContext } from './context';
import Login from './pages/login';
import checkLogin from './utils/checkLogin';
import changeTheme from './utils/changeTheme';
import useStorage from './utils/useStorage';



/**
 * 把后端的 permCodes (["fabric:read", ...] 或 ["*"])
 * 转为前端 routes.ts 期望的 Record<resource, actions[]> 格式。
 * admin 角色返回 {"*": ["*"]} 的形式由 authentication.ts 的 * 通配处理。
 */
function transformPermissions(
  permCodes: string[],
  roles: string[],
): Record<string, string[]> {
  // admin 通配
  if (roles.includes('admin') || permCodes.includes('*')) {
    return { '*': ['*'] };
  }
  const result: Record<string, string[]> = {};
  permCodes.forEach((code) => {
    // code 格式: "资源:动作" 或 "模块:资源:动作"
    const parts = code.split(':');
    if (parts.length >= 2) {
      // 最后一段是 action，前面拼起来是 resource
      const action = parts[parts.length - 1];
      const resource = parts.slice(0, -1).join(':');
      if (!result[resource]) {
        result[resource] = [];
      }
      result[resource].push(action);
    }
  });
  return result;
}

function Index() {
  const [lang, setLang] = useStorage('arco-lang', 'en-US');
  const [theme, setTheme] = useStorage('arco-theme', 'light');

  function getArcoLocale() {
    switch (lang) {
      case 'zh-CN':
        return zhCN;
      case 'en-US':
        return enUS;
      default:
        return zhCN;
    }
  }

  function fetchUserInfo() {
    store.dispatch(setUserInfo({ userLoading: true }));
    getCurrentUser()
      .then((user) => {
        // 后端 permissions 是 ["fabric:read", ...] 或 ["*"]
        // 前端 store 期望 Record<resource, actions[]>
        const permissions = transformPermissions(user.permissions, user.roles);
        store.dispatch(setUserInfo({
          userInfo: { name: user.displayName, permissions },
          userLoading: false,
        }));
      })
      .catch(() => {
        store.dispatch(setUserInfo({ userLoading: false }));
      });
  }

  useEffect(() => {
    if (checkLogin()) {
      fetchUserInfo();
    } else if (window.location.pathname.replace(/\//g, '') !== 'login') {
      window.location.pathname = '/login';
    }
  }, []);

  useEffect(() => {
    changeTheme(theme || 'light');
  }, [theme]);

  const contextValue = {
    lang,
    setLang,
    theme,
    setTheme,
  };

  return (
    <BrowserRouter>
      <ConfigProvider
        locale={getArcoLocale()}
        componentConfig={{
          Card: {
            bordered: false,
          },
          List: {
            bordered: false,
          },
          Table: {
            border: false,
          },
        }}
      >
        <Provider store={store}>
          <GlobalContext.Provider value={contextValue}>
            <Switch>
              <Route path="/login" component={Login} />
              <Route path="/" component={PageLayout} />
            </Switch>
          </GlobalContext.Provider>
        </Provider>
      </ConfigProvider>
    </BrowserRouter>
  );
}

createRoot(document.getElementById('root')!).render(<Index />);
