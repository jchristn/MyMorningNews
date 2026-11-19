import { useEffect, useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../context/AppContext.jsx';
import { useAuth } from '../context/AuthContext.jsx';
import { fieldErrors } from '../utils/errors.js';
import CopyableId from '../components/CopyableId.jsx';
import ErrorState from '../components/ErrorState.jsx';
import FormField from '../components/FormField.jsx';
import LoadingState from '../components/LoadingState.jsx';
import PageHeader from '../components/PageHeader.jsx';
import PasswordInput from '../components/PasswordInput.jsx';
import LanguageSelector from '../i18n/LanguageSelector.jsx';
import PodcastFeeds from './profile/PodcastFeeds.jsx';

export default function ProfileView() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [me, setMe] = useState(null);
  const [form, setForm] = useState({ firstName: '', lastName: '', timeZone: '', locale: '' });
  const [error, setError] = useState(null);
  const [saveError, setSaveError] = useState(null);
  const [pw, setPw] = useState({ currentPassword: '', newPassword: '', confirm: '' });
  const [pwError, setPwError] = useState(null);

  useEffect(() => {
    apiClient
      .getMe()
      .then((m) => {
        setMe(m);
        setForm({ firstName: m.firstName || '', lastName: m.lastName || '', timeZone: m.timeZone || '', locale: m.locale || '' });
      })
      .catch(setError);
  }, [apiClient]);

  async function saveProfile(e) {
    e.preventDefault();
    setSaveError(null);
    try {
      const body = { ...form };
      for (const k of ['timeZone', 'locale']) if (!body[k]) delete body[k];
      setMe(await apiClient.updateMe(body));
      addNotification(t('profile.saved'), 'success');
    } catch (err) {
      setSaveError(err);
    }
  }

  async function changePassword(e) {
    e.preventDefault();
    setPwError(null);
    try {
      await apiClient.changePassword(pw.currentPassword, pw.newPassword);
      setPw({ currentPassword: '', newPassword: '', confirm: '' });
      addNotification(t('profile.passwordChanged'), 'success');
    } catch (err) {
      setPwError(err);
    }
  }

  if (error) return <ErrorState error={error} />;
  if (!me) return <LoadingState />;
  const pe = fieldErrors(pwError);
  const mismatch = pw.confirm && pw.confirm !== pw.newPassword;
  const browserTz = Intl.DateTimeFormat().resolvedOptions().timeZone;

  return (
    <div className="page">
      <PageHeader title={t('nav.items.profile')} summary={t('routes.profile.summary')} meta={<CopyableId value={me.id} />} />
      <div className="two-column">
        <form className="panel" onSubmit={saveProfile}>
          <h2>{t('profile.details')}</h2>
          <ErrorState error={saveError} inline onDismiss={() => setSaveError(null)} />
          <div className="form-grid">
            <FormField id="me-email" label={t('users.columns.email')} help={t('profile.emailHelp')}>
              <input id="me-email" value={me.email} disabled />
            </FormField>
            <FormField id="me-first" label={t('users.firstName')}>
              <input id="me-first" value={form.firstName} onChange={(e) => setForm({ ...form, firstName: e.target.value })} />
            </FormField>
            <FormField id="me-last" label={t('users.lastName')}>
              <input id="me-last" value={form.lastName} onChange={(e) => setForm({ ...form, lastName: e.target.value })} />
            </FormField>
            <FormField id="me-tz" label={t('users.timeZone')} help={t('profile.timeZoneHelp', { zone: browserTz })} error={fieldErrors(saveError).timeZone}>
              <input id="me-tz" value={form.timeZone} onChange={(e) => setForm({ ...form, timeZone: e.target.value })} placeholder={browserTz} className="mono" />
            </FormField>
          </div>
          <LanguageSelector />
          <div className="form-actions">
            <button type="submit" className="button primary">
              {t('common.save')}
            </button>
          </div>
        </form>
        <form className="panel" onSubmit={changePassword}>
          <h2>{t('profile.password')}</h2>
          <ErrorState error={pwError} inline onDismiss={() => setPwError(null)} />
          <FormField id="pw-current" label={t('profile.currentPassword')} error={pe.currentPassword}>
            <PasswordInput id="pw-current" value={pw.currentPassword} onChange={(v) => setPw({ ...pw, currentPassword: v })} />
          </FormField>
          <FormField id="pw-new" label={t('profile.newPassword')} help={t('users.passwordHelp')} error={pe.newPassword}>
            <PasswordInput id="pw-new" value={pw.newPassword} onChange={(v) => setPw({ ...pw, newPassword: v })} autoComplete="new-password" />
          </FormField>
          <FormField id="pw-confirm" label={t('profile.confirmPassword')} error={mismatch ? t('profile.mismatch') : undefined}>
            <PasswordInput id="pw-confirm" value={pw.confirm} onChange={(v) => setPw({ ...pw, confirm: v })} autoComplete="new-password" />
          </FormField>
          <div className="form-actions">
            <button type="submit" className="button primary" disabled={!pw.currentPassword || pw.newPassword.length < 8 || mismatch}>
              {t('profile.changePassword')}
            </button>
          </div>
        </form>
      </div>
      <PodcastFeeds />
    </div>
  );
}
