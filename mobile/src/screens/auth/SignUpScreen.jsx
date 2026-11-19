import React, { useState } from 'react';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { authApi } from '@/api/auth';
import { fieldErrors, isNotAvailable } from '@/api/errors';
import { Banner, Button, Field, Screen, SwitchRow, T } from '@/components/ui';
import { LegalFooter, openLink } from '@/components/LegalFooter';
import { NotAvailableState } from '@/components/states';
import { currentLanguage } from '@/i18n/runtime';
import { useAuth } from '@/state/AuthContext';
import { useServer } from '@/state/ServerContext';

function timeZone() {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
  } catch {
    return 'UTC';
  }
}

/**
 * Create an account (plan section 33.1, Phase 14): `POST /v1.0/public/signup`, then the server emails a verification
 * link. Servers without public sign-up (self-hosted defaults, or before Phase 14) answer 404, and the screen says so.
 */
export function SignUpScreen() {
  const { t } = useTranslation();
  const { client } = useAuth();
  const { config, links } = useServer();
  const [form, setForm] = useState({ displayName: '', email: '', password: '' });
  const [accepted, setAccepted] = useState(false);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState(null);
  const [errors, setErrors] = useState({});

  if (config && config.signupEnabled === false && result !== 'unavailable') {
    return (
      <Screen>
        <NotAvailableState feature={t('mobile.signUp.feature')} />
        <T variant="muted" style={{ textAlign: 'center' }}>
          {t('mobile.signUp.disabled')}
        </T>
      </Screen>
    );
  }
  if (result === 'unavailable') {
    return (
      <Screen>
        <NotAvailableState feature={t('mobile.signUp.feature')} />
      </Screen>
    );
  }
  if (result === 'sent') {
    return (
      <Screen>
        <Banner kind="success" title={t('mobile.signUp.sentTitle')}>{t('mobile.signUp.sentBody', { email: form.email.trim() })}</Banner>
        <Button title={t('mobile.signUp.backToSignIn')} onPress={() => router.replace('/sign-in')} />
      </Screen>
    );
  }

  const submit = async () => {
    setBusy(true);
    setErrors({});
    try {
      await authApi.signUp(client, { email: form.email.trim(), password: form.password, displayName: form.displayName.trim(), timeZone: timeZone(), locale: currentLanguage() || 'en', acceptTerms: accepted });
      setResult('sent');
    } catch (error) {
      if (isNotAvailable(error)) setResult('unavailable');
      else setErrors({ ...fieldErrors(error), form: error.message });
    } finally {
      setBusy(false);
    }
  };

  const set = (key) => (value) => setForm({ ...form, [key]: value });
  return (
    <Screen testID="sign-up-screen">
      <T variant="muted">{t('mobile.signUp.intro')}</T>
      <Field label={t('mobile.signUp.displayName')} value={form.displayName} onChangeText={set('displayName')} autoComplete="name" error={errors.displayName} />
      <Field label={t('mobile.signIn.email')} value={form.email} onChangeText={set('email')} autoCapitalize="none" keyboardType="email-address" autoComplete="email" error={errors.email} />
      <Field label={t('mobile.signIn.password')} value={form.password} onChangeText={set('password')} secureTextEntry autoComplete="new-password" textContentType="newPassword" hint={t('mobile.password.rule')} error={errors.password} />
      <SwitchRow title={t('mobile.signUp.acceptTerms')} detail={t('mobile.signUp.acceptTermsDetail')} value={accepted} onValueChange={setAccepted} />
      <Button kind="ghost" title={t('mobile.legal.terms')} onPress={() => void openLink(links.terms)} />
      {errors.form ? <Banner kind="danger">{errors.form}</Banner> : null}
      <Button title={t('mobile.signUp.submit')} busy={busy} disabled={!accepted || !form.email || !form.password} onPress={() => void submit()} />
      <LegalFooter />
    </Screen>
  );
}
