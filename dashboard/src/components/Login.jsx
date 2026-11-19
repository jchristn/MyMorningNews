import { useEffect, useState } from 'react';
import { useLocation, useNavigate } from 'react-router-dom';
import { LogIn, Moon, Sun } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import { useTheme } from '../context/ThemeContext.jsx';
import LanguageSelector from '../i18n/LanguageSelector.jsx';
import { describeError } from '../utils/errors.js';
import FormField from './FormField.jsx';
import PasswordInput from './PasswordInput.jsx';

export default function Login() {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const { login, serverUrl: savedUrl, lastTenantId, sessionExpired } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const [serverUrl, setServerUrl] = useState(savedUrl);
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [tenantId, setTenantId] = useState(lastTenantId || 'default');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    document.title = t('login.title') + ' | ' + t('app.name');
  }, [t]);

  async function onSubmit(event) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      await login(serverUrl, email.trim(), password, tenantId.trim() || 'default');
      const from = location.state && location.state.from ? location.state.from : '/dashboard/home';
      navigate(from, { replace: true });
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  const info = error ? describeError(error, t) : null;

  return (
    <div className="login-page">
      <section className="login-brand" aria-hidden="false">
        <img src="/assets/logo.png" alt={t('app.name')} className="login-logo" />
        <p className="login-tagline">{t('login.tagline')}</p>
      </section>
      <section className="login-panel">
        <div className="login-controls">
          <LanguageSelector compact />
          <button type="button" className="icon-button" onClick={toggleTheme} aria-label={theme === 'dark' ? t('theme.light') : t('theme.dark')} title={theme === 'dark' ? t('theme.light') : t('theme.dark')}>
            {theme === 'dark' ? <Sun size={16} aria-hidden="true" /> : <Moon size={16} aria-hidden="true" />}
          </button>
        </div>
        <form className="login-form" onSubmit={onSubmit} noValidate>
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
          <FormField id="login-tenant" label={t('login.tenant')} help={t('login.tenantHelp')}>
            <input id="login-tenant" type="text" value={tenantId} onChange={(e) => setTenantId(e.target.value)} required className="mono" />
          </FormField>
          {info ? (
            <div className="notice notice-danger" role="alert">
              <strong>{info.title}</strong> {info.message}
            </div>
          ) : null}
          <button type="submit" className="button primary block" disabled={busy || !email || !password}>
            <LogIn size={16} aria-hidden="true" /> {busy ? t('login.signingIn') : t('login.signIn')}
          </button>
          <p className="login-defaults muted small">{t('login.defaults', { email: 'admin@mymorningnews', password: 'password', tenant: 'default' })}</p>
        </form>
      </section>
    </div>
  );
}
