import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { useAuth } from './AuthContext.jsx';
import { SETUP_REQUIRED_EVENT } from '../utils/api.js';

const SetupContext = createContext(null);

// The setup status in a shape views can rely on. Throws when the server sent something that is not a setup status,
// so the gate shows an error instead of letting the user in.
export function normalizeSetupStatus(raw) {
  if (!raw || typeof raw !== 'object' || typeof raw.ready !== 'boolean') throw new Error('The server did not return a setup status.');
  return {
    ready: raw.ready,
    canConfigure: Boolean(raw.canConfigure),
    checkedUtc: typeof raw.checkedUtc === 'string' ? raw.checkedUtc : null,
    checks: Array.isArray(raw.checks) ? raw.checks.filter((c) => c && typeof c === 'object') : []
  };
}

// Required checks first, then optional ones, each group in the server's order.
export function orderedChecks(checks) {
  const list = Array.isArray(checks) ? checks : [];
  return { required: list.filter((c) => c.required), optional: list.filter((c) => !c.required) };
}

// The tenant's setup status (GET /v1.0/api/setup) for the signed-in session, with the administrator actions that
// change it. A SetupRequired refusal from any request re-reads the status, so the gate re-engages without a reload.
export function SetupProvider({ children }) {
  const { apiClient, identity } = useAuth();
  const [status, setStatus] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const [checkedAt, setCheckedAt] = useState(null);
  // The message of the last SetupRequired refusal, shown on the Setup screen until setup is ready.
  const [blockedMessage, setBlockedMessage] = useState(null);
  const sequence = useRef(0);

  const apply = useCallback((raw) => {
    const next = normalizeSetupStatus(raw);
    sequence.current += 1;
    setStatus(next);
    setError(null);
    setLoading(false);
    setCheckedAt(new Date());
    if (next.ready) setBlockedMessage(null);
    return next;
  }, []);

  const refresh = useCallback(async () => {
    const id = ++sequence.current;
    setLoading(true);
    try {
      const raw = await apiClient.getSetupStatus();
      if (id !== sequence.current) return null;
      return apply(raw);
    } catch (err) {
      if (id === sequence.current) {
        setError(err);
        setLoading(false);
        setCheckedAt(new Date());
      }
      return null;
    }
  }, [apiClient, apply]);

  // Administrators only: live calls for every check. Resolves to the new status; rejects with the API error.
  const verify = useCallback(async () => apply(await apiClient.verifySetup()), [apiClient, apply]);

  // Administrators only: store, attach, and verify a model key. Resolves to the new status; rejects with the API error.
  const saveModelKey = useCallback(async (modelEndpointId, apiKey) => apply(await apiClient.setSetupModelKey(modelEndpointId, apiKey)), [apiClient, apply]);

  useEffect(() => {
    refresh();
  }, [refresh, identity]);

  useEffect(() => {
    if (typeof window === 'undefined') return undefined;
    const onRequired = (event) => {
      const message = event && event.detail && event.detail.message ? event.detail.message : null;
      setBlockedMessage(message || '');
      refresh();
    };
    window.addEventListener(SETUP_REQUIRED_EVENT, onRequired);
    return () => window.removeEventListener(SETUP_REQUIRED_EVENT, onRequired);
  }, [refresh]);

  const value = useMemo(
    () => ({
      status,
      error,
      loading,
      checkedAt,
      ready: Boolean(status && status.ready),
      blockedMessage,
      dismissBlocked: () => setBlockedMessage(null),
      refresh,
      verify,
      saveModelKey
    }),
    [status, error, loading, checkedAt, blockedMessage, refresh, verify, saveModelKey]
  );

  return <SetupContext.Provider value={value}>{children}</SetupContext.Provider>;
}

SetupProvider.propTypes = { children: PropTypes.node };

// The setup state inside SetupProvider, else null (components that also render outside the dashboard shell).
export function useSetup() {
  return useContext(SetupContext);
}
