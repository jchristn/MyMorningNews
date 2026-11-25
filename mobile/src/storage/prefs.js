import AsyncStorage from '@react-native-async-storage/async-storage';

/** The hosted service. Self-hosted installs change it on the sign-in screen ("Change server"). */
export const DEFAULT_SERVER_URL = 'https://app.mymorningnews.ai';

/** Lock the app after it has been in the background this long (seconds). 0 locks on every return. */
export const LOCK_TIMEOUT_CHOICES = [0, 60, 300, 900];

/** Non-secret preferences (AsyncStorage). Every read tolerates missing or corrupt values. */
export const DEFAULT_PREFS = Object.freeze({
  serverUrl: DEFAULT_SERVER_URL,
  /** 'system', 'light', or 'dark'. */
  theme: 'system',
  /** A supported locale code, or null to follow the device. */
  locale: null,
  biometricLock: false,
  lockTimeoutSeconds: 0,
  lastEmail: '',
  lastTenantId: '',
  /** The user turned notifications on in Account > Notifications (the app never prompts at launch). */
  pushEnabled: false,
  pushDeviceId: null,
  /** The user chose Not now on the Today card that offers notifications for new briefings; it never shows again. */
  pushPromptDismissed: false,
  playbackRate: 1,
  /** Who chose to go on without finished setup: `{ [setupSkipKey]: true }`, per server, workspace, and user. */
  setupSkipped: {},
});

/** The key of a signed-in identity in `setupSkipped`, or null when nobody is signed in. */
export function setupSkipKey(state) {
  if (!state || !state.tenantId || !state.user || !state.user.id) return null;
  return [state.serverUrl || '', state.tenantId, state.user.id].join('|');
}

const KEY = 'mymorningnews.prefs';

export async function loadPrefs() {
  try {
    const text = await AsyncStorage.getItem(KEY);
    if (!text) return { ...DEFAULT_PREFS };
    const parsed = JSON.parse(text) || {};
    const merged = { ...DEFAULT_PREFS, ...parsed };
    merged.serverUrl = normalizeServerUrl(merged.serverUrl) || DEFAULT_SERVER_URL;
    if (!['system', 'light', 'dark'].includes(merged.theme)) merged.theme = 'system';
    if (!LOCK_TIMEOUT_CHOICES.includes(merged.lockTimeoutSeconds)) merged.lockTimeoutSeconds = 0;
    if (!merged.setupSkipped || typeof merged.setupSkipped !== 'object' || Array.isArray(merged.setupSkipped)) merged.setupSkipped = {};
    return merged;
  } catch {
    return { ...DEFAULT_PREFS };
  }
}

export async function savePrefs(prefs) {
  try {
    await AsyncStorage.setItem(KEY, JSON.stringify(prefs));
  } catch {
    // Preferences are a convenience; failing to save one never blocks the app.
  }
}

/** Trim, default the scheme to https, and drop trailing slashes. Returns '' for input that is not a URL. */
export function normalizeServerUrl(input) {
  let value = String(input ?? '').trim();
  if (!value) return '';
  if (!/^[a-z][a-z0-9+.-]*:\/\//i.test(value)) value = 'https://' + value;
  if (!/^https?:\/\/[^/\s]+/i.test(value)) return '';
  return value.replace(/\/+$/, '');
}

/** True for plain-HTTP servers, which the UI warns about. */
export function isInsecureServer(url) {
  return /^http:\/\//i.test(String(url || ''));
}
