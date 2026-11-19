import React, { useState } from 'react';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { authApi } from '@/api/auth';
import { isNotAvailable } from '@/api/errors';
import { Banner, Button, Field, Screen, T } from '@/components/ui';
import { NotAvailableState } from '@/components/states';
import { useAuth } from '@/state/AuthContext';
import { usePrefs } from '@/state/PrefsContext';

/**
 * Request a password reset email (plan section 33.1, Phase 14): `POST /v1.0/public/password/forgot`. The answer is
 * the same whether or not the email has an account. 404 until the server supports it.
 */
export function ForgotPasswordScreen() {
  const { t } = useTranslation();
  const { client } = useAuth();
  const { prefs } = usePrefs();
  const [email, setEmail] = useState(prefs.lastEmail);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);

  if (result === 'unavailable') {
    return (
      <Screen>
        <NotAvailableState feature={t('mobile.forgot.feature')} />
        <T variant="muted" style={{ textAlign: 'center' }}>
          {t('mobile.forgot.askAdmin')}
        </T>
      </Screen>
    );
  }

  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      await authApi.forgotPassword(client, email.trim());
      setResult('sent');
    } catch (e) {
      if (isNotAvailable(e)) setResult('unavailable');
      else setError(e.message);
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen testID="forgot-screen">
      {result === 'sent' ? (
        <>
          <Banner kind="success" title={t('mobile.forgot.sentTitle')}>{t('mobile.forgot.sentBody')}</Banner>
          <Button title={t('mobile.signUp.backToSignIn')} onPress={() => router.replace('/sign-in')} />
        </>
      ) : (
        <>
          <T variant="muted">{t('mobile.forgot.intro')}</T>
          <Field label={t('mobile.signIn.email')} value={email} onChangeText={setEmail} autoCapitalize="none" keyboardType="email-address" autoComplete="email" />
          {error ? <Banner kind="danger">{error}</Banner> : null}
          <Button title={t('mobile.forgot.submit')} busy={busy} disabled={!email.trim()} onPress={() => void submit()} />
        </>
      )}
    </Screen>
  );
}
