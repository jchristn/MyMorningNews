import { createContext, useCallback, useContext, useEffect, useMemo, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { useAuth } from './AuthContext.jsx';
import { SETUP_REQUIRED_EVENT } from '../utils/api.js';

const SetupContext = createContext(null);

// Where the browser remembers, per workspace and user, that the first-run setup guide is over: 'skipped' when the
// person chose Skip for now, 'ready' once setup was seen complete. Either way the guide never comes back on its own.
export const SETUP_PASSED_KEY = 'mymorningnews.setup.passed';

// The key of a signed-in identity in the remembered choices, or null when nobody is signed in.
export function setupPassId(identity) {
  if (!identity || !identity.tenantId) return null;
  return identity.tenantId + '|' + (identity.userId || identity.principalId || '');
}

export function readSetupPasses() {
  try {
    const raw = window.localStorage.getItem(SETUP_PASSED_KEY);
    const parsed = raw ? JSON.parse(raw) : null;
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed) ? parsed : {};
  } catch {
    return {};
  }
}

function writeSetupPasses(passes) {
  try {
    window.localStorage.setItem(SETUP_PASSED_KEY, JSON.stringify(passes));
  } catch {
    // Storage is unavailable (private mode): the choice lasts for this page only.
  }
}

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

// The required checks that are not ready yet: what is still missing.
export function pendingChecks(status) {
  return orderedChecks(status ? status.checks : []).required.filter((c) => c.state !== 'Ready');
}

// The tenant's setup status (GET /v1.0/api/setup) for the signed-in session, with the administrator actions that
// change it. While setup is not ready the first-run guide is on (gated): routes lead to the Setup screen. The guide
// ends when setup is ready or when the person skips it, and after that it only comes back if they ask for it; a
// SetupRequired refusal from a request then shows as a warning instead of a redirect.
export function SetupProvider({ children }) {
  const { apiClient, identity } = useAuth();
  const [status, setStatus] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const [checkedAt, setCheckedAt] = useState(null);
  // The message of the last SetupRequired refusal, shown until setup is ready or it is dismissed.
  const [blockedMessage, setBlockedMessage] = useState(null);
  const [passes, setPasses] = useState(readSetupPasses);
  // True from the moment setup turns ready in this session until the note about it is dismissed.
  const [justCompleted, setJustCompleted] = useState(false);
  const [warningDismissed, setWarningDismissed] = useState(false);
  const sequence = useRef(0);
  const lastReady = useRef(null);
  const passId = setupPassId(identity);
  const passed = passId ? passes[passId] || null : null;

  const remember = useCallback(
    (value) => {
      if (!passId) return;
      setPasses(() => {
        const next = { ...readSetupPasses() };
        if (value) next[passId] = value;
        else delete next[passId];
        writeSetupPasses(next);
        return next;
      });
    },
    [passId]
  );

  const apply = useCallback(
    (raw) => {
      const next = normalizeSetupStatus(raw);
      sequence.current += 1;
      setStatus(next);
      setError(null);
      setLoading(false);
      setCheckedAt(new Date());
      if (next.ready) {
        setBlockedMessage(null);
        if (lastReady.current === false) setJustCompleted(true);
        // Setup is done: the first-run guide is over for good, even if a check fails later.
        if (passId && readSetupPasses()[passId] !== 'ready') remember('ready');
      }
      lastReady.current = next.ready;
      return next;
    },
    [passId, remember]
  );

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

  // Administrators only: live calls for every check, or for one kind (for example 'Speech'). Resolves to the new
  // status; rejects with the API error.
  const verify = useCallback(async (kind) => apply(await apiClient.verifySetup(kind)), [apiClient, apply]);

  // Administrators only: connect the chat model (test the new sign-in with one prompt, which can take up to a minute
  // for a CLI, and switch every chat task to it only when it passes). Resolves to the new status plus the server's
  // connection result ({ modelEndpointId, succeeded, keptPrevious, message }, or null); rejects with the API error
  // (400 with details.field for a bad secret, model, or URL).
  const connect = useCallback(
    async (body) => {
      const raw = await apiClient.connectSetup(body);
      const next = apply(raw);
      return { ...next, connection: raw && raw.connection && typeof raw.connection === 'object' ? raw.connection : null };
    },
    [apiClient, apply]
  );

  // Leave the setup guide without finishing: every page opens, and the choice is remembered for this person in this
  // workspace on this browser.
  const skip = useCallback(() => {
    setWarningDismissed(false);
    remember('skipped');
  }, [remember]);

  // The person's own choice to turn the guide back on until setup is finished.
  const resume = useCallback(() => remember(null), [remember]);

  // The status is read again only when the session or the client changes, not when a remembered choice does.
  const refreshRef = useRef(refresh);
  useEffect(() => {
    refreshRef.current = refresh;
  }, [refresh]);

  useEffect(() => {
    refreshRef.current();
  }, [apiClient, identity]);

  useEffect(() => {
    if (typeof window === 'undefined') return undefined;
    const onRequired = (event) => {
      const message = event && event.detail && event.detail.message ? event.detail.message : null;
      setBlockedMessage(message || '');
      setWarningDismissed(false);
      refreshRef.current();
    };
    window.addEventListener(SETUP_REQUIRED_EVENT, onRequired);
    return () => window.removeEventListener(SETUP_REQUIRED_EVENT, onRequired);
  }, []);

  const ready = Boolean(status && status.ready);
  const gated = Boolean(status && !status.ready && !passed);

  const value = useMemo(
    () => ({
      status,
      error,
      loading,
      checkedAt,
      ready,
      gated,
      skipped: passed === 'skipped',
      passed,
      blockedMessage,
      dismissBlocked: () => setBlockedMessage(null),
      justCompleted,
      dismissCompleted: () => setJustCompleted(false),
      warningDismissed,
      dismissWarning: () => {
        setWarningDismissed(true);
        setBlockedMessage(null);
      },
      refresh,
      verify,
      connect,
      skip,
      resume
    }),
    [status, error, loading, checkedAt, ready, gated, passed, blockedMessage, justCompleted, warningDismissed, refresh, verify, connect, skip, resume]
  );

  return <SetupContext.Provider value={value}>{children}</SetupContext.Provider>;
}

SetupProvider.propTypes = { children: PropTypes.node };

// The setup state inside SetupProvider, else null (components that also render outside the dashboard shell).
export function useSetup() {
  return useContext(SetupContext);
}
