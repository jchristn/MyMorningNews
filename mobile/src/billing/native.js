import { requireOptionalNativeModule } from 'expo';
import Constants from 'expo-constants';

let cached;
/** Existing binaries and Expo Go can still read billing, without loading an absent native module. */
export function getIap() {
  if (cached !== undefined) return cached;
  cached = null;
  if (Constants.executionEnvironment === 'storeClient') return cached;
  try {
    if (requireOptionalNativeModule('ExpoIap')) cached = require('expo-iap');
  } catch { /* A new native build is required to purchase. */ }
  return cached;
}
