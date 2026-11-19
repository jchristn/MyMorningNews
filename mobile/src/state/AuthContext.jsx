import React, { createContext, useContext, useEffect, useMemo, useSyncExternalStore } from 'react';
import { AuthController } from '@/auth/AuthController';
import { secureTokenStorage } from '@/auth/tokens';
import { currentLanguage } from '@/i18n/runtime';
import { usePrefs } from './PrefsContext';

const AuthContext = createContext(null);

/** One AuthController per server URL. Changing the server swaps controllers; each server keeps its own tokens. */
export function AuthProvider({ children, controller }) {
  const { prefs } = usePrefs();
  const auth = useMemo(
    () => controller || new AuthController({ serverUrl: prefs.serverUrl, storage: secureTokenStorage, getLocale: () => currentLanguage() }),
    [controller, prefs.serverUrl],
  );
  const subscribe = useMemo(() => (listener) => auth.subscribe(listener), [auth]);
  const state = useSyncExternalStore(subscribe, () => auth.getState());

  useEffect(() => {
    if (auth.getState().status === 'loading') void auth.restore();
  }, [auth]);

  const value = useMemo(() => ({ auth, client: auth.client, state }), [auth, state]);
  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

/** `{ auth, client, state }` for the active server. */
export function useAuth() {
  const value = useContext(AuthContext);
  if (!value) throw new Error('useAuth must be used inside AuthProvider');
  return value;
}
