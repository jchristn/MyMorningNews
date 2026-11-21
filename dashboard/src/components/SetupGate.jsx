import { useState } from 'react';
import PropTypes from 'prop-types';
import { LogOut, RefreshCw } from 'lucide-react';
import { useNavigate } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import { orderedChecks, useSetup } from '../context/SetupContext.jsx';
import useFormat from '../hooks/useFormat.js';
import AuthShell from './auth/AuthShell.jsx';
import ErrorState from './ErrorState.jsx';
import LoadingState from './LoadingState.jsx';
import SetupStateBadge, { checkTitle } from './SetupStateBadge.jsx';

// Check again and Sign out, shared by the full-screen setup messages.
function GateActions() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const { logout } = useAuth();
  const setup = useSetup();
  const [busy, setBusy] = useState(false);

  async function checkAgain() {
    setBusy(true);
    await setup.refresh();
    setBusy(false);
  }

  async function signOut() {
    await logout();
    navigate('/', { replace: true });
  }

  return (
    <div className="setup-gate-actions">
      <button type="button" className="button primary block" onClick={checkAgain} disabled={busy}>
        <RefreshCw size={14} className={busy ? 'spin' : undefined} aria-hidden="true" /> {busy ? t('setup.gate.checking') : t('setup.gate.checkAgain')}
      </button>
      <button type="button" className="button secondary block" onClick={signOut}>
        <LogOut size={14} aria-hidden="true" /> {t('setup.gate.signOut')}
      </button>
    </div>
  );
}

// The workspace is not ready and this user cannot configure it: nothing in the product works until an
// administrator adds and verifies the model key.
export function SetupBlockedScreen() {
  const { t } = useTranslation();
  const f = useFormat();
  const setup = useSetup();
  const { required } = orderedChecks(setup.status ? setup.status.checks : []);
  const pending = required.filter((c) => c.state !== 'Ready');
  const title = t('setup.gate.title');
  return (
    <AuthShell title={title}>
      <div className="auth-form" data-testid="setup-blocked">
        <h1>{title}</h1>
        <div className="notice notice-warning">{t('setup.gate.message')}</div>
        {pending.length > 0 ? (
          <ul className="setup-gate-list">
            {pending.map((check) => (
              <li key={check.key}>
                <span>{checkTitle(check, t)}</span> <SetupStateBadge state={check.state} />
              </li>
            ))}
          </ul>
        ) : null}
        <p className="muted small">{t('setup.gate.askAdmin')}</p>
        {setup.error ? <ErrorState error={setup.error} inline /> : null}
        <GateActions />
        {setup.checkedAt ? <p className="muted small">{t('setup.gate.checked', { time: f.time(setup.status && setup.status.checkedUtc ? setup.status.checkedUtc : setup.checkedAt) })}</p> : null}
      </div>
    </AuthShell>
  );
}

// The setup status could not be read. The product stays closed: without it there is no way to know it would work.
export function SetupLoadErrorScreen() {
  const { t } = useTranslation();
  const setup = useSetup();
  const title = t('setup.loadError.title');
  return (
    <AuthShell title={title}>
      <div className="auth-form" data-testid="setup-load-error">
        <h1>{title}</h1>
        <p>{t('setup.loadError.message')}</p>
        <ErrorState error={setup.error} inline />
        <GateActions />
      </div>
    </AuthShell>
  );
}

// Between sign-in and the product: waits for the setup status, shows an error when it cannot be read, and shows
// members a full-screen message while setup is not ready. Administrators get the dashboard, where every route but
// the setup and configuration pages redirects to the Setup screen (see RouteElement in App.jsx).
export default function SetupGate({ children }) {
  const setup = useSetup();
  if (!setup || !setup.status) {
    if (setup && setup.error && !setup.loading) return <SetupLoadErrorScreen />;
    return <LoadingState />;
  }
  if (!setup.status.ready && !setup.status.canConfigure) return <SetupBlockedScreen />;
  return children;
}

SetupGate.propTypes = { children: PropTypes.node };
