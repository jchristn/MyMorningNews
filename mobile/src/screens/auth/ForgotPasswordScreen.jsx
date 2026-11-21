import React, { useState } from 'react';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { authApi } from '@/api/auth';
import { ApiError, isNetworkError, isNotAvailable } from '@/api/errors';
import { Banner, Button, Field, Screen, T } from '@/components/ui';
import { NotAvailableState } from '@/components/states';
import { useAuth } from '@/state/AuthContext';
import { usePrefs } from '@/state/PrefsContext';
import { useServer } from '@/state/ServerContext';

/**
 * Request a password reset email (plan section 33.1): `POST /v1.0/public/password/forgot`. The answer is the same
 * whether or not the email has an account. The emailed link (`/reset-password?token=...`) opens the reset screen in
 * the app. A server that sends no email (client config `emailEnabled: false`) cannot deliver the link, so the screen
 * says to ask its administrator; a server without the route says it arrives later.
 */
export function ForgotPasswordScreen() {
  const { t } = useTranslation();
  const { client } = useAuth();
  const { config } = useServer();
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
      else if (isNetworkError(e)) setError(t('mobile.signIn.errorUnreachable'));
      else if (e instanceof ApiError && e.status === 429) setError(t('mobile.signIn.errorRateLimited'));
      else setError(e.message);
    } finally {
      setBusy(false);
    }
  };

  const noEmail = Boolean(config && config.emailEnabled === false);
  return (
    <Screen testID="forgot-screen">
      {result === 'sent' ? (
        <>
          <Banner kind="success" title={t('mobile.forgot.sentTitle')}>
            {t('mobile.forgot.sentBody')}
          </Banner>
          <Button title={t('mobile.signUp.backToSignIn')} onPress={() => router.replace('/sign-in')} testID="forgot-back" />
        </>
      ) : (
        <>
          <T variant="muted">{t('mobile.forgot.intro')}</T>
          {noEmail ? (
            <Banner kind="warning" testID="forgot-no-email">
              {t('mobile.forgot.askAdmin')}
            </Banner>
          ) : null}
          <Field label={t('mobile.signIn.email')} value={email} onChangeText={setEmail} autoCapitalize="none" autoCorrect={false} keyboardType="email-address" autoComplete="email" testID="forgot-email" />
          {error ? (
            <Banner kind="danger" testID="forgot-error">
              {error}
            </Banner>
          ) : null}
          <Button title={t('mobile.forgot.submit')} busy={busy} disabled={!email.trim().includes('@')} onPress={() => void submit()} testID="forgot-submit" />
          <Button title={t('mobile.signUp.backToSignIn')} kind="ghost" onPress={() => router.replace('/sign-in')} />
        </>
      )}
    </Screen>
  );
}
