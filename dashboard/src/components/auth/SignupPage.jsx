import { useState } from 'react';
import { Link, useSearchParams } from 'react-router-dom';
import { UserPlus } from 'lucide-react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useClientConfig from '../../hooks/useClientConfig.js';
import { describeError } from '../../utils/errors.js';
import FormField from '../FormField.jsx';
import LoadingState from '../LoadingState.jsx';
import PasswordInput from '../PasswordInput.jsx';
import AuthShell from './AuthShell.jsx';
import TurnstileWidget from './TurnstileWidget.jsx';

export const MIN_PASSWORD = 8;
const TERMS_URL = 'https://mymorningnews.ai/terms.html';
const PRIVACY_URL = 'https://mymorningnews.ai/privacy.html';

const TERMS_MARK = '\u0001';
const PRIVACY_MARK = '\u0002';

// The consent sentence with its two links. The translated sentence carries markers where the links go, so each
// language can order the words its own way.
export function ConsentText({ sentence, terms, privacy }) {
  const parts = [];
  let text = '';
  for (const ch of String(sentence)) {
    if (ch === TERMS_MARK || ch === PRIVACY_MARK) {
      if (text) parts.push(text);
      parts.push(ch);
      text = '';
    } else {
      text += ch;
    }
  }
  if (text) parts.push(text);
  return (
    <>
      {parts.map((part, i) => {
        if (part === TERMS_MARK)
          return (
            <a key={i} href={TERMS_URL} target="_blank" rel="noreferrer">
              {terms}
            </a>
          );
        if (part === PRIVACY_MARK)
          return (
            <a key={i} href={PRIVACY_URL} target="_blank" rel="noreferrer">
              {privacy}
            </a>
          );
        return part;
      })}
    </>
  );
}

ConsentText.propTypes = { sentence: PropTypes.string.isRequired, terms: PropTypes.string.isRequired, privacy: PropTypes.string.isRequired };

// Public sign-up (POST /v1.0/public/signup). The server answers the same way whether or not the email already has
// an account, so the page always ends on "check your email" (or "sign in" when verification is off).
export default function SignupPage() {
  const { t, i18n } = useTranslation();
  const { apiClient } = useAuth();
  const [params] = useSearchParams();
  const { config, loaded } = useClientConfig();
  const [form, setForm] = useState({ email: '', password: '', displayName: '', promoCode: params.get('code') || '', acceptTerms: false, confirmAge: false });
  const [captcha, setCaptcha] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [done, setDone] = useState(null);

  if (!loaded) return <LoadingState />;
  const title = t('auth.signup.title');
  if (!config || !config.signupEnabled) {
    return (
      <AuthShell title={title}>
        <h1>{title}</h1>
        <div className="notice notice-info">{t('auth.signup.closed')}</div>
        <Link to="/">{t('auth.backToSignIn')}</Link>
      </AuthShell>
    );
  }

  const age = config.minimumAge || 18;
  const needsCaptcha = Boolean(config.turnstileSiteKey);
  const valid = form.email.includes('@') && form.password.length >= MIN_PASSWORD && form.acceptTerms && form.confirmAge && (!needsCaptcha || captcha);

  async function submit(e) {
    e.preventDefault();
    if (!valid) return;
    setBusy(true);
    setError(null);
    try {
      const result = await apiClient.signUp({
        email: form.email.trim(),
        password: form.password,
        displayName: form.displayName.trim() || undefined,
        timeZone: Intl.DateTimeFormat().resolvedOptions().timeZone,
        locale: i18n.language,
        acceptTerms: form.acceptTerms,
        confirmAge: form.confirmAge,
        promoCode: form.promoCode.trim() || undefined,
        turnstileToken: captcha || undefined
      });
      setDone({ verify: !result || result.verificationRequired !== false, email: form.email.trim() });
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  if (done) {
    return (
      <AuthShell title={title}>
        <h1>{done.verify ? t('auth.signup.checkEmailTitle') : t('auth.signup.readyTitle')}</h1>
        <p>{done.verify ? t('auth.signup.checkEmail', { email: done.email }) : t('auth.signup.ready')}</p>
        <Link to="/" className="button primary block">
          {t('auth.backToSignIn')}
        </Link>
      </AuthShell>
    );
  }

  const info = error ? describeError(error, t) : null;
  const set = (key) => (e) => setForm({ ...form, [key]: e && e.target ? (e.target.type === 'checkbox' ? e.target.checked : e.target.value) : e });

  return (
    <AuthShell title={title}>
      <form className="auth-form" onSubmit={submit} noValidate>
        <h1>{title}</h1>
        <p className="muted">{t('auth.signup.subtitle')}</p>
        <FormField id="signup-email" label={t('login.email')}>
          <input id="signup-email" type="email" value={form.email} onChange={set('email')} autoComplete="email" required />
        </FormField>
        <FormField id="signup-password" label={t('login.password')} help={t('auth.passwordHelp', { count: MIN_PASSWORD })}>
          <PasswordInput id="signup-password" value={form.password} onChange={set('password')} autoComplete="new-password" required />
        </FormField>
        <FormField id="signup-name" label={t('auth.signup.displayName')}>
          <input id="signup-name" type="text" value={form.displayName} onChange={set('displayName')} autoComplete="name" />
        </FormField>
        <FormField id="signup-code" label={t('account.plan.code')} help={t('auth.signup.codeHelp')}>
          <input id="signup-code" type="text" value={form.promoCode} onChange={set('promoCode')} className="mono" />
        </FormField>
        <label className="auth-check small" htmlFor="signup-terms">
          <input id="signup-terms" type="checkbox" checked={form.acceptTerms} onChange={set('acceptTerms')} />
          <span>
            <ConsentText terms={t('auth.signup.termsLink')} privacy={t('auth.signup.privacyLink')} sentence={t('auth.signup.consent', { terms: TERMS_MARK, privacy: PRIVACY_MARK })} />
          </span>
        </label>
        <label className="auth-check small" htmlFor="signup-age">
          <input id="signup-age" type="checkbox" checked={form.confirmAge} onChange={set('confirmAge')} />
          <span>{t('auth.signup.confirmAge', { age })}</span>
        </label>
        {needsCaptcha ? <TurnstileWidget siteKey={config.turnstileSiteKey} onToken={setCaptcha} /> : null}
        {info ? (
          <div className="notice notice-danger" role="alert">
            <strong>{info.title}</strong> {info.message}
          </div>
        ) : null}
        <button type="submit" className="button primary block" disabled={busy || !valid}>
          <UserPlus size={16} aria-hidden="true" /> {busy ? t('auth.signup.creating') : t('auth.signup.create')}
        </button>
        <div className="auth-links small">
          <Link to="/">{t('auth.haveAccount')}</Link>
        </div>
      </form>
    </AuthShell>
  );
}
