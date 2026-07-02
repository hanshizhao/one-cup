import Mock from 'mockjs';
import { isSSR } from '@/utils/is';
import setupMock from '@/utils/setupMock';

if (!isSSR) {
  Mock.XHR.prototype.withCredentials = true;

  setupMock({
    setup: () => {
      // 登录与用户信息已改为真实后端接口，不再 mock。
      // message-box mock 仍保留在 message-box.ts。
    },
  });
}
