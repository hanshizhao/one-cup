import '@testing-library/jest-dom';

// jsdom 不实现 window.matchMedia，Arco 的响应式组件（Grid/Row/Col 等）依赖它。
// 提供最小 polyfill，供组件渲染测试使用。
if (typeof window !== 'undefined' && typeof window.matchMedia !== 'function') {
  window.matchMedia = (query: string): MediaQueryList => ({
    matches: false,
    media: query,
    onchange: null,
    addListener: () => {}, // 已废弃，保留兼容
    removeListener: () => {}, // 已废弃，保留兼容
    addEventListener: () => {},
    removeEventListener: () => {},
    dispatchEvent: () => false,
  });
}
