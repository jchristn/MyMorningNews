import React, { useState } from 'react';
import { router, useLocalSearchParams } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { authApi } from '@/api/auth';
import { ApiError, isNetworkError } from '@/api/errors';
import { Banner, Button, Screen, T } from '@/components/ui';
import { PasswordField } from '@/components/PasswordField';
import { MIN_PASSWORD_LENGTH } from '@/screens/account/PasswordSection';
import { useAuth } from '@/state/AuthContext';
import { usePrefs } from '@/state/PrefsContext';

/** Client checks before `POST /v1.0/public/password/reset`; the server checks again. Returns an i18n key or null. */
export function resetProblem(password, confirm) {
  if (!password || password.length < MIN_PASSWORD_LENGTH) return 'mobile.password.errorShort';
  if (password !== confirm) return 'mobile.password.errorMismatch';
  return null;
}

function sameEmail(a, b) {
  return Boolean(a && b) && String(a).trim().toLowerCase() === String(b).trim().toLowerCase();
}

/**
 * The link from the reset email (`/reset-password?token=...`, opened in the app by a universal link): choose a new
 * password with `POST /v1.0/public/password/reset`. The server signs out every session, including one on this
 * device, so the screen ends on "Sign in". A link that no longer works (used, expired, or unknown) offers a new one.
 */
export function ResetPasswordScreen() {
  const { t } = useTranslation();
  const { auth, client, state: authState } = useAuth();
  const { update } = usePrefs();
  const params = useLocalSearchParams();
  const token = typeof params.token === 'string' ? params.token.trim() : '';
  const [password, setPassword] = useState('');
  const [confirm, setConfirm] = useState('');
  const [busy, setBusy] = useState(false);
  const [done, setDone] = useState(false);
  const [error, setError] = useState(null);

  if (!token) {
    return (
      <Screen testID="reset-no-token">
        <Banner kind="warning">{t('mobile.reset.noToken')}</Banner>
        <Button title={t('mobile.reset.requestNew')} onPress={() => router.replace('/forgot-password')} testID="reset-request-new" />
        <Button title={t('mobile.signUp.backToSignIn')} kind="ghost" onPress={() => router.replace('/sign-in')} />
      </Screen>
    );
  }

  if (done) {
    return (
      <Screen testID="reset-done">
        <Banner kind="success" title={t('mobile.reset.doneTitle')}>
          {t('mobile.reset.doneBody')}
        </Banner>
        {authState.status === 'signedIn' ? (
          <Button title={t('mobile.signIn.continue')} onPress={() => router.replace('/today')} testID="reset-continue" />
        ) : (
          <Button title={t('mobile.signIn.submit')} onPress={() => router.replace('/sign-in')} testID="reset-sign-in" />
        )}
      </Screen>
    );
  }

  const submit = async () => {
    const problem = resetProblem(password, confirm);
    if (problem) {
      setError({ message: t(problem, { min: MIN_PASSWORD_LENGTH }) });
      return;
    }
    setBusy(true);
    setError(null);
    try {
      const result = await authApi.resetPassword(client, token, password);
      const email = result && typeof result.email === 'string' ? result.email : '';
      if (email) update({ lastEmail: email, lastTenantId: '' });
      // The server ended every session of that account. When it is the one signed in here, drop this device's copy
      // too, so the next screen is sign-in. Another account signed in on this device stays signed in.
      if (authState.status === 'signedIn' && sameEmail(authState.user && authState.user.email, email)) await auth.signOut().catch(() => undefined);
      setDone(true);
    } catch (e) {
      if (isNetworkError(e)) setError({ message: t('mobile.signIn.errorUnreachable') });
      else if (e instanceof ApiError && e.status === 429) setError({ message: t('mobile.signIn.errorRateLimited') });
      else setError({ message: e.message, link: e instanceof ApiError && e.status === 400 });
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen testID="reset-screen">
      <T variant="muted">{t('mobile.reset.intro')}</T>
      <PasswordField label={t('mobile.password.new')} value={password} onChangeText={setPassword} hint={t('mobile.password.rule', { min: MIN_PASSWORD_LENGTH })} testID="reset-password" />
      <PasswordField label={t('mobile.password.confirm')} value={confirm} onChangeText={setConfirm} error={confirm && confirm !== password ? t('mobile.password.errorMismatch') : undefined} testID="reset-confirm" />
      {error ? (
        <Banner kind="danger" testID="reset-error">
          {error.message}
        </Banner>
      ) : null}
      {error && error.link ? <Button title={t('mobile.reset.requestNew')} kind="secondary" onPress={() => router.replace('/forgot-password')} testID="reset-request-new" /> : null}
      <Button title={t('mobile.reset.submit')} busy={busy} disabled={password.length < MIN_PASSWORD_LENGTH || password !== confirm} onPress={() => void submit()} testID="reset-submit" />
      <Button title={t('mobile.signUp.backToSignIn')} kind="ghost" onPress={() => router.replace('/sign-in')} />
    </Screen>
  );
}
