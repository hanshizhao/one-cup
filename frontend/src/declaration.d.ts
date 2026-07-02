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

declare module 'nprogress' {
  interface NProgress {
    start: () => NProgress;
    done: (force?: boolean) => NProgress;
    configure: (options?: Record<string, any>) => NProgress;
  }
  const progress: NProgress;
  export default progress;
}
