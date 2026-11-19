import * as SecureStore from 'expo-secure-store';

/**
 * Secrets (session and refresh tokens) live in the iOS Keychain and Android Keystore, readable after the first unlock
 * and never migrated to another device by backup.
 */
const OPTIONS = { keychainAccessible: SecureStore.AFTER_FIRST_UNLOCK_THIS_DEVICE_ONLY };

/** Secure-store keys allow only [A-Za-z0-9._-]; server URLs are folded into that alphabet. */
export function secureKey(prefix, scope) {
  return prefix + '.' + String(scope).toLowerCase().replace(/[^a-z0-9._-]/g, '_');
}

export async function readSecret(key) {
  try {
    return await SecureStore.getItemAsync(key, OPTIONS);
  } catch {
    return null;
  }
}

export async function writeSecret(key, value) {
  if (value === null || value === undefined) {
    await SecureStore.deleteItemAsync(key, OPTIONS);
    return;
  }
  await SecureStore.setItemAsync(key, value, OPTIONS);
}
