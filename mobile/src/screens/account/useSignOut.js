import { useCallback } from 'react';
import { unregisterPush } from '@/push/push';
import { useAuth } from '@/state/AuthContext';
import { usePrefs } from '@/state/PrefsContext';

/** Sign out: remove this device's push registration first (while the session still works), then end the session. */
export function useSignOut() {
  const { auth, client } = useAuth();
  const { prefs, update } = usePrefs();
  return useCallback(async () => {
    await unregisterPush(client, prefs.pushDeviceId);
    update({ pushDeviceId: null });
    await auth.signOut();
  }, [auth, client, prefs.pushDeviceId, update]);
}
