import { useEffect, useState } from 'react';
import { Link, useLocation, useNavigate } from 'react-router-dom';
import { LogIn } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { STORAGE_KEYS, useAuth } from '../context/AuthContext.jsx';
import useClientConfig from '../hooks/useClientConfig.js';
import { ApiError } from '../utils/api.js';
import { describeError } from '../utils/errors.js';
import AuthShell from './auth/AuthShell.jsx';
import FormField from './FormField.jsx';
import PasswordInput from './PasswordInput.jsx';

function storedTenant() {
  try {
    return window.localStorage.getItem(STORAGE_KEYS.tenantId) || '';
  } catch {
    return '';
  }
}

// Sign in. The tenant is optional: without one, an email in a single tenant signs in and an email in several gets a
// list to choose from (409). Unverified sign-up accounts get a link to send the confirmation email again.
export default function Login() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const { login, apiClient, serverUrl: savedUrl, sessionExpired } = useAuth();
  const { config } = useClientConfig();
  const signupOpen = Boolean(config && config.signupEnabled);
  const [serverUrl, setServerUrl] = useState(savedUrl);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [tenantId, setTenantId] = useState(() => storedTenant() || 'default');
  const [tenantTouched, setTenantTouched] = useState(false);
  const [choices, setChoices] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [resent, setResent] = useState(false);

  useEffect(() => {
    if (signupOpen && !tenantTouched && !storedTenant()) setTenantId('');
  }, [signupOpen, tenantTouched]);

  async function signIn(tenant) {
    setBusy(true);
    setError(null);
    setResent(false);
    try {
      await login(serverUrl, email.trim(), password, tenant ? tenant.trim() : '');
      const from = location.state && location.state.from ? location.state.from : '/dashboard/home';
      navigate(from, { replace: true });
    } catch (err) {
      if (err instanceof ApiError && err.status === 409 && err.body && Array.isArray(err.body.tenants)) {
        setChoices(err.body.tenants);
        if (err.body.tenants.length) setTenantId(err.body.tenants[0].id);
      } else {
        setError(err);
      }
    } finally {
      setBusy(false);
    }
  }

  function onSubmit(event) {
    event.preventDefault();
    signIn(tenantId);
  }

  async function resend() {
    try {
      await apiClient.resendVerification(email.trim());
    } finally {
      setResent(true);
    }
  }

  const unverified = error instanceof ApiError && error.status === 403 && error.code === 'EmailNotVerified';
  const info = error && !unverified ? describeError(error, t) : null;

  return (
    <AuthShell title={t('login.title')}>
      <form className="auth-form" onSubmit={onSubmit} noValidate>
        <h1>{t('login.title')}</h1>
        <p className="muted">{t('login.subtitle')}</p>
        {sessionExpired ? <div className="notice notice-warning">{t('login.sessionExpired')}</div> : null}
        <FormField id="login-server" label={t('login.serverUrl')} help={t('login.serverUrlHelp')}>
          <input id="login-server" type="url" value={serverUrl} onChange={(e) => setServerUrl(e.target.value)} autoComplete="url" required className="mono" />
        </FormField>
        <FormField id="login-email" label={t('login.email')}>
          <input id="login-email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} autoComplete="username" required />
        </FormField>
        <FormField id="login-password" label={t('login.password')}>
          <PasswordInput id="login-password" value={password} onChange={setPassword} required />
        </FormField>
        {choices ? (
          <FormField id="login-tenant-choice" label={t('login.chooseTenant')} help={t('login.chooseTenantHelp')}>
            <select id="login-tenant-choice" value={tenantId} onChange={(e) => setTenantId(e.target.value)}>
              {choices.map((c) => (
                <option key={c.id} value={c.id}>
                  {c.name}
                </option>
              ))}
            </select>
          </FormField>
        ) : (
          <FormField id="login-tenant" label={t('login.tenant')} help={t('login.tenantOptionalHelp')}>
            <input
              id="login-tenant"
              type="text"
              value={tenantId}
              onChange={(e) => {
                setTenantTouched(true);
                setTenantId(e.target.value);
              }}
              className="mono"
            />
          </FormField>
        )}
        {unverified ? (
          <div className="notice notice-warning" role="alert">
            <strong>{t('login.unverifiedTitle')}</strong> {t('login.unverifiedMessage')}{' '}
            {resent ? (
              <span>{t('auth.resent')}</span>
            ) : (
              <button type="button" className="link-button" onClick={resend}>
                {t('auth.resend')}
              </button>
            )}
          </div>
        ) : null}
        {info ? (
          <div className="notice notice-danger" role="alert">
            <strong>{info.title}</strong> {info.message}
          </div>
        ) : null}
        <button type="submit" className="button primary block" disabled={busy || !email || !password}>
          <LogIn size={16} aria-hidden="true" /> {busy ? t('login.signingIn') : t('login.signIn')}
        </button>
        <div className="auth-links small">
          <Link to="/forgot-password">{t('login.forgot')}</Link>
          {signupOpen ? <Link to="/signup">{t('login.createAccount')}</Link> : null}
        </div>
        {signupOpen ? null : <p className="login-defaults muted small">{t('login.defaults', { email: 'admin@mymorningnews', password: 'password', tenant: 'default' })}</p>}
      </form>
    </AuthShell>
  );
}
