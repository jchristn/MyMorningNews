import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import i18next from '../i18n/index.js';
import { deletionCancelledBySignIn } from '../utils/accountDeletion.js';
import { ApiClient, defaultServerUrl } from '../utils/api.js';
import { intlLocale } from '../i18n/localeRegistry.js';

export const STORAGE_KEYS = {
  serverUrl: 'mymorningnews.serverUrl',
  token: 'mymorningnews.token',
  tenantId: 'mymorningnews.tenantId'
};

const AuthContext = createContext(null);

function read(key) {
  try {
    return window.localStorage.getItem(key);
  } catch {
    return null;
  }
}

function write(key, value) {
  try {
    if (value === null || value === undefined) window.localStorage.removeItem(key);
    else window.localStorage.setItem(key, value);
  } catch {
    // Storage may be unavailable; the session lasts until the tab closes.
  }
}

// An IANA time zone the browser can format in, or null (the browser's own zone is used).
export function validTimeZone(zone) {
  if (!zone || typeof zone !== 'string') return null;
  try {
    new Intl.DateTimeFormat('en', { timeZone: zone });
    return zone;
  } catch {
    return null;
  }
}

const localeGetter = () => intlLocale(i18next.resolvedLanguage || i18next.language || 'en');

export function createClient(serverUrl, token) {
  return new ApiClient(serverUrl, token, { getLocale: localeGetter });
}

// Session state: server URL, token, and the authenticated identity. Pages never parse or store credentials.
export function AuthProvider({ children, clientFactory = createClient }) {
  const [serverUrl, setServerUrl] = useState(() => read(STORAGE_KEYS.serverUrl) || defaultServerUrl());
  const [token, setToken] = useState(() => read(STORAGE_KEYS.token));
  const [identity, setIdentity] = useState(null);
  const [profile, setProfile] = useState(null);
  const [isLoading, setIsLoading] = useState(Boolean(read(STORAGE_KEYS.token)));
  const [sessionExpired, setSessionExpired] = useState(false);
  // Something to tell the user once they are signed in: 'deletionCancelled', or null.
  const [signInNotice, setSignInNotice] = useState(null);
  const factory = useRef(clientFactory);

  const apiClient = useMemo(() => factory.current(serverUrl, token), [serverUrl, token]);

  const clearSession = useCallback(() => {
    write(STORAGE_KEYS.token, null);
    setToken(null);
    setIdentity(null);
    setProfile(null);
  }, []);

  // The signed-in user's profile (first name, time zone). Administration keys and access keys have none.
  const refreshProfile = useCallback(async () => {
    if (!identity || identity.principalType !== 'User' || !identity.userId || typeof apiClient.getMe !== 'function') {
      setProfile(null);
      return null;
    }
    try {
      const me = await apiClient.getMe();
      const next = me && typeof me === 'object' && !Array.isArray(me.objects) ? me : null;
      setProfile(next);
      return next;
    } catch {
      setProfile(null);
      return null;
    }
  }, [apiClient, identity]);

  useEffect(() => {
    refreshProfile();
  }, [refreshProfile]);

  // Restore an existing session on mount.
  useEffect(() => {
    let cancelled = false;
    if (!token || identity) {
      setIsLoading(false);
      return undefined;
    }
    setIsLoading(true);
    apiClient
      .getTokenDetails()
      .then((details) => {
        if (!cancelled) setIdentity(details);
      })
      .catch(() => {
        if (!cancelled) clearSession();
      })
      .finally(() => {
        if (!cancelled) setIsLoading(false);
      });
    return () => {
      cancelled = true;
    };
  }, [apiClient, token, identity, clearSession]);

  useEffect(() => {
    // A 401 that arrives after the session was already ended here (a late request) is not an expired session.
    const onUnauthorized = () => {
      if (!read(STORAGE_KEYS.token)) return;
      setSessionExpired(true);
      clearSession();
    };
    window.addEventListener('auth:unauthorized', onUnauthorized);
    return () => window.removeEventListener('auth:unauthorized', onUnauthorized);
  }, [clearSession]);

  const login = useCallback(async (url, email, password, tenantId) => {
    const base = String(url || defaultServerUrl()).trim().replace(/\/+$/, '');
    const anonymous = factory.current(base, null);
    const issued = await anonymous.login(email, password, tenantId);
    const authed = factory.current(base, issued.token);
    const details = await authed.getTokenDetails();
    write(STORAGE_KEYS.serverUrl, base);
    write(STORAGE_KEYS.token, issued.token);
    write(STORAGE_KEYS.tenantId, issued.tenantId || tenantId || 'default');
    setServerUrl(base);
    setToken(issued.token);
    setIdentity(details);
    setSessionExpired(false);
    setSignInNotice(deletionCancelledBySignIn(email, issued) ? 'deletionCancelled' : null);
    return details;
  }, []);

  // End the session here without asking the server, for when the server already ended it (account deletion).
  const endSession = useCallback(() => {
    setSessionExpired(false);
    setSignInNotice(null);
    clearSession();
  }, [clearSession]);

  const dismissSignInNotice = useCallback(() => setSignInNotice(null), []);

  const logout = useCallback(async () => {
    try {
      if (token) await apiClient.logout();
    } catch {
      // The session may already be gone; logging out locally is still correct.
    }
    clearSession();
  }, [apiClient, token, clearSession]);

  const value = useMemo(
    () => ({
      isAuthenticated: Boolean(token && identity),
      isLoading,
      sessionExpired,
      apiClient,
      serverUrl,
      token,
      identity,
      profile,
      refreshProfile,
      timeZone: validTimeZone(profile && profile.timeZone),
      tenantId: identity ? identity.tenantId : read(STORAGE_KEYS.tenantId) || 'default',
      isAdmin: Boolean(identity && identity.isAdmin),
      isTenantAdmin: Boolean(identity && (identity.isTenantAdmin || identity.isAdmin)),
      lastTenantId: read(STORAGE_KEYS.tenantId) || 'default',
      login,
      logout,
      endSession,
      signInNotice,
      dismissSignInNotice
    }),
    [token, identity, profile, refreshProfile, isLoading, sessionExpired, apiClient, serverUrl, login, logout, endSession, signInNotice, dismissSignInNotice]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

AuthProvider.propTypes = {
  children: PropTypes.node,
  clientFactory: PropTypes.func
};

// The session when inside AuthProvider, else null. For shared hooks (formatting) that also render signed out.
export function useOptionalAuth() {
  return useContext(AuthContext);
}

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}
