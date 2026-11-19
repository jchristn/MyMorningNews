import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import i18next from '../i18n/index.js';
import { ApiClient, DEFAULT_SERVER_URL } from '../utils/api.js';
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

const localeGetter = () => intlLocale(i18next.resolvedLanguage || i18next.language || 'en');

export function createClient(serverUrl, token) {
  return new ApiClient(serverUrl, token, { getLocale: localeGetter });
}

// Session state: server URL, token, and the authenticated identity. Pages never parse or store credentials.
export function AuthProvider({ children, clientFactory = createClient }) {
  const [serverUrl, setServerUrl] = useState(() => read(STORAGE_KEYS.serverUrl) || DEFAULT_SERVER_URL);
  const [token, setToken] = useState(() => read(STORAGE_KEYS.token));
  const [identity, setIdentity] = useState(null);
  const [isLoading, setIsLoading] = useState(Boolean(read(STORAGE_KEYS.token)));
  const [sessionExpired, setSessionExpired] = useState(false);
  const factory = useRef(clientFactory);

  const apiClient = useMemo(() => factory.current(serverUrl, token), [serverUrl, token]);

  const clearSession = useCallback(() => {
    write(STORAGE_KEYS.token, null);
    setToken(null);
    setIdentity(null);
  }, []);

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
    const onUnauthorized = () => {
      setSessionExpired(true);
      clearSession();
    };
    window.addEventListener('auth:unauthorized', onUnauthorized);
    return () => window.removeEventListener('auth:unauthorized', onUnauthorized);
  }, [clearSession]);

  const login = useCallback(async (url, email, password, tenantId) => {
    const base = String(url || DEFAULT_SERVER_URL).trim().replace(/\/+$/, '');
    const anonymous = factory.current(base, null);
    const issued = await anonymous.login(email, password, tenantId);
    const authed = factory.current(base, issued.token);
    const details = await authed.getTokenDetails();
    write(STORAGE_KEYS.serverUrl, base);
    write(STORAGE_KEYS.token, issued.token);
    write(STORAGE_KEYS.tenantId, tenantId);
    setServerUrl(base);
    setToken(issued.token);
    setIdentity(details);
    setSessionExpired(false);
    return details;
  }, []);

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
      tenantId: identity ? identity.tenantId : read(STORAGE_KEYS.tenantId) || 'default',
      isAdmin: Boolean(identity && identity.isAdmin),
      isTenantAdmin: Boolean(identity && (identity.isTenantAdmin || identity.isAdmin)),
      lastTenantId: read(STORAGE_KEYS.tenantId) || 'default',
      login,
      logout
    }),
    [token, identity, isLoading, sessionExpired, apiClient, serverUrl, login, logout]
  );

  return <AuthContext.Provider value={value}>{children}</AuthContext.Provider>;
}

AuthProvider.propTypes = {
  children: PropTypes.node,
  clientFactory: PropTypes.func
};

export function useAuth() {
  const ctx = useContext(AuthContext);
  if (!ctx) throw new Error('useAuth must be used inside AuthProvider');
  return ctx;
}
