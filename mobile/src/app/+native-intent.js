import { mapDeepLink } from '@/lib/deepLinks';

/**
 * Expo Router hook for every link the system hands the app (custom scheme and universal links): map dashboard and
 * email URLs onto app routes. Unknown links open Today.
 */
export function redirectSystemPath({ path }) {
  try {
    return mapDeepLink(path) || '/today';
  } catch {
    return '/today';
  }
}
