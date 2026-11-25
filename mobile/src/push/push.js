import { Platform } from 'react-native';
import Constants from 'expo-constants';
import * as Device from 'expo-device';
import * as Notifications from 'expo-notifications';
import { AndroidImportance } from 'expo-notifications';
import { devicesApi } from '@/api/account';
import { isNotAvailable } from '@/api/errors';

/** Android notification channels; the server's PushWorker sends to these IDs. */
export const CHANNELS = Object.freeze([
  { id: 'briefings', importance: 'HIGH' },
  { id: 'predictions', importance: 'DEFAULT' },
  { id: 'account', importance: 'DEFAULT' },
]);

/**
 * Push setup results:
 * registered, denied (the user said no), simulator (no push on simulators), notConfigured (no EAS project id in this
 * build), unsupported (the server has no devices route yet), failed.
 */
export function easProjectId() {
  const extra = (Constants.expoConfig && Constants.expoConfig.extra) || {};
  return (extra.eas && extra.eas.projectId) || (Constants.easConfig && Constants.easConfig.projectId) || null;
}

/** Create the Android channels (no prompt; safe at launch). Names come from the app's strings. */
export async function ensureChannels(names) {
  if (Platform.OS !== 'android') return;
  for (const channel of CHANNELS) {
    await Notifications.setNotificationChannelAsync(channel.id, {
      name: (names && names[channel.id]) || channel.id,
      importance: AndroidImportance[channel.importance],
    });
  }
}

export async function permissionGranted() {
  const permission = await Notifications.getPermissionsAsync();
  return Boolean(permission.granted);
}

/**
 * Whether this device could receive push, without asking: `granted`, `undetermined` (the system has not asked yet,
 * or may ask again), `denied` (the user said no in the system settings), `simulator`, or `notConfigured` (no EAS
 * project id in this build). The Today card (PushPromptCard) offers push only for `granted` and `undetermined`.
 * @returns {Promise<'granted' | 'undetermined' | 'denied' | 'simulator' | 'notConfigured'>}
 */
export async function pushPermissionStatus() {
  if (!Device.isDevice) return 'simulator';
  if (!easProjectId()) return 'notConfigured';
  const permission = await Notifications.getPermissionsAsync();
  if (permission.granted) return 'granted';
  if (permission.status === 'denied' && permission.canAskAgain !== true) return 'denied';
  return 'undetermined';
}

/**
 * Whether this device can register for push, asking for permission when `prompt` is true and it is not granted yet.
 * Nothing is sent to the server. The app passes `prompt` only after a tap: a Phone switch in Account > Notifications
 * or Turn on in the Today card, never at launch.
 * @returns {Promise<{ status: 'ready' | 'simulator' | 'notConfigured' | 'denied', projectId?: string }>}
 */
export async function preparePush({ prompt } = {}) {
  if (!Device.isDevice) return { status: 'simulator' };
  const projectId = easProjectId();
  if (!projectId) return { status: 'notConfigured' };
  let permission = await Notifications.getPermissionsAsync();
  if (!permission.granted && prompt) permission = await Notifications.requestPermissionsAsync();
  if (!permission.granted) return { status: 'denied' };
  return { status: 'ready', projectId };
}

/**
 * Register this device for push with the server (contract C). `prompt` asks for permission when it is not granted
 * yet; the app asks first with preparePush after a tap and then passes false here, never prompting at launch.
 * @returns {Promise<{ status: string, device?: object, error?: Error }>}
 */
export async function registerForPush(client, { prompt, name, locale }) {
  const ready = await preparePush({ prompt });
  if (ready.status !== 'ready') return { status: ready.status };
  try {
    const token = await Notifications.getExpoPushTokenAsync({ projectId: ready.projectId });
    const device = await devicesApi.register(client, {
      expoPushToken: token.data,
      platform: Platform.OS === 'ios' ? 'ios' : 'android',
      name: name || Device.deviceName || Device.modelName || 'Mobile device',
      locale: locale || 'en',
    });
    return { status: 'registered', device };
  } catch (error) {
    return { status: isNotAvailable(error) ? 'unsupported' : 'failed', error };
  }
}

/** Remove this device's registration (sign-out, or turning notifications off). Best effort. */
export async function unregisterPush(client, deviceId) {
  if (!deviceId) return;
  try {
    await devicesApi.remove(client, deviceId);
  } catch {
    // The server prunes devices whose tokens Expo reports as invalid.
  }
}
