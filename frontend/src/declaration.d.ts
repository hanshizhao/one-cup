declare module '*.svg' {
  const content: React.FunctionComponent<React.SVGAttributes<SVGElement>>;
  export default content;
}

declare module '*.less' {
  const classes: { [className: string]: string };
  export default classes;
}

declare module '*/settings.json' {
  const value: {
    colorWeek: boolean;
    navbar: boolean;
    menu: boolean;
    footer: boolean;
    themeColor: string;
    menuWidth: number;
  };

  export default value;
}

declare module '*.png' {
  const value: string;
  export default value;
}

declare module '@loadable/component' {
  import { ComponentType } from 'react';
  const loadable: <P = any>(
    loadFn: (props?: P) => Promise<{ default: ComponentType<any> }>,
    options?: {
      fallback?: React.ReactNode;
      cacheKey?: (props?: P) => string;
    }
  ) => ComponentType<P> & {
    preload: () => Promise<void>;
  };
  export default loadable;
}

declare module 'react-router-dom' {
  import { ComponentType } from 'react';
  export interface RouteComponentProps {
    history: {
      push: (path: string, state?: any) => void;
      location: { pathname: string; search: string; hash: string };
    };
    match: { path: string; url: string; params: Record<string, string> };
    location: { pathname: string; search: string; hash: string };
  }
  export function Switch(props: { children: React.ReactNode }): null;
  export function Route(props: {
    path?: string | string[];
    exact?: boolean;
    strict?: boolean;
    component?: ComponentType<RouteComponentProps>;
    children?: React.ReactNode;
  }): null;
  export function Redirect(props: { to: string; from?: string }): null;
  export function useHistory(): RouteComponentProps['history'];
  export function BrowserRouter(
    props: { children: React.ReactNode }
  ): React.ReactElement | null;
  export function Link(props: {
    to: string;
    children?: React.ReactNode;
  }): React.ReactElement | null;
}

declare module 'nprogress' {
  interface NProgress {
    start: () => NProgress;
    done: (force?: boolean) => NProgress;
    configure: (options?: Record<string, any>) => NProgress;
  }
  const progress: NProgress;
  export default progress;
}
