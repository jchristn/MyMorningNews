import React, { useEffect, useRef, useState } from 'react';
import { router, useLocalSearchParams } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { authApi } from '@/api/auth';
import { isNetworkError } from '@/api/errors';
import { Banner, Button, Field, Screen } from '@/components/ui';
import { LoadingState } from '@/components/states';
import { useAuth } from '@/state/AuthContext';
import { usePrefs } from '@/state/PrefsContext';
import { ResendLink } from './ResendLink';

/**
 * The link from the verification email (`/verify?token=...`, opened in the app by a universal link):
 * `POST /v1.0/public/verify-email` once, then "Sign in". A link that does not work (never valid, used, or expired;
 * the server does not say which) offers to send a new one. A network failure offers to try again, since the link may
 * still be good.
 */
export function VerifyEmailScreen() {
  const { t } = useTranslation();
  const { client, state: authState } = useAuth();
  const { prefs, update } = usePrefs();
  const params = useLocalSearchParams();
  const token = typeof params.token === 'string' ? params.token.trim() : '';
  const [state, setState] = useState(token ? 'working' : 'failed');
  const [email, setEmail] = useState(prefs.lastEmail || '');
  const [attempt, setAttempt] = useState(0);
  const started = useRef(-1);

  useEffect(() => {
    if (!token || started.current === attempt) return;
    // One request per attempt: a token works once, so a second call would fail a link that just succeeded.
    started.current = attempt;
    authApi
      .verifyEmail(client, token)
      .then((r) => {
        if (r && typeof r.email === 'string' && r.email) {
          setEmail(r.email);
          update({ lastEmail: r.email, lastTenantId: '' });
        }
        setState('verified');
      })
      .catch((e) => setState(isNetworkError(e) ? 'offline' : 'failed'));
  }, [client, token, attempt, update]);

  if (state === 'working') return <LoadingState label={t('mobile.verify.working')} />;

  const signedIn = authState.status === 'signedIn';
  const backTitle = signedIn ? t('mobile.signIn.continue') : t('mobile.signUp.backToSignIn');
  const back = () => router.replace(signedIn ? '/today' : '/sign-in');

  if (state === 'verified') {
    return (
      <Screen testID="verify-done">
        <Banner kind="success" title={t('mobile.verify.doneTitle')}>
          {t('mobile.verify.doneBody')}
        </Banner>
        <Button title={signedIn ? t('mobile.signIn.continue') : t('mobile.signIn.submit')} onPress={back} testID="verify-sign-in" />
      </Screen>
    );
  }

  if (state === 'offline') {
    return (
      <Screen testID="verify-offline">
        <Banner kind="danger">{t('mobile.signIn.errorUnreachable')}</Banner>
        <Button
          title={t('mobile.common.retry')}
          onPress={() => {
            setState('working');
            setAttempt(attempt + 1);
          }}
          testID="verify-retry"
        />
        <Button title={backTitle} kind="ghost" onPress={back} />
      </Screen>
    );
  }

  return (
    <Screen testID="verify-failed">
      <Banner kind="warning" title={t('mobile.verify.failedTitle')}>
        {t('mobile.verify.failedBody')}
      </Banner>
      <Field label={t('mobile.signIn.email')} value={email} onChangeText={setEmail} autoCapitalize="none" autoCorrect={false} keyboardType="email-address" autoComplete="email" testID="verify-email" />
      <ResendLink email={email} kind="primary" />
      <Button title={backTitle} kind="ghost" onPress={back} />
    </Screen>
  );
}
