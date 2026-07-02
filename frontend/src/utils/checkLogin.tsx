import { getAccessToken } from '@/utils/token';

export default function checkLogin() {
  return !!getAccessToken();
}
