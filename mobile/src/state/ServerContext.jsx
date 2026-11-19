import React, { createContext, useCallback, useContext, useEffect, useMemo, useState } from 'react';
import { serverApi } from '@/api/account';
import { useAuth } from './AuthContext';

/** Links used when the server does not publish its own (contract D not deployed, or offline). */
export const DEFAULT_LINKS = Object.freeze({
  privacy: 'https://mymorningnews.ai/privacy.html',
  terms: 'https://mymorningnews.ai/terms.html',
  support: 'https://mymorningnews.ai/support.html',
  deleteAccount: 'https://mymorningnews.ai/delete-account.html',
});

export const SUPPORT_EMAIL = 'hello@latticetech.ai';

const ServerContext = createContext({ config: null, available: false, links: DEFAULT_LINKS, reload: () => undefined });

/**
 * Client configuration for the active server (`GET /v1.0/api/client-config`, contract D, no auth): product name,
 * server version, sign-up and push switches, minimum app version, and legal links. Servers without the route answer
 * 404 (or 401, since unknown routes require authentication); the app then uses defaults.
 */
export function ServerProvider({ children }) {
  const { auth } = useAuth();
  const [state, setState] = useState({ config: null, available: false });
  const [version, setVersion] = useState(0);
  const reload = useCallback(() => setVersion((v) => v + 1), []);

  useEffect(() => {
    let alive = true;
    serverApi
      .clientConfig(auth.client)
      .then((config) => {
        if (alive) setState({ config: config && typeof config === 'object' ? config : null, available: Boolean(config) });
      })
      .catch(() => {
        if (alive) setState({ config: null, available: false });
      });
    return () => {
      alive = false;
    };
  }, [auth, version]);

  const value = useMemo(() => {
    const links = { ...DEFAULT_LINKS, ...((state.config && state.config.links) || {}) };
    return { config: state.config, available: state.available, links, reload };
  }, [state, reload]);
  return <ServerContext.Provider value={value}>{children}</ServerContext.Provider>;
}

export function useServer() {
  return useContext(ServerContext);
}
