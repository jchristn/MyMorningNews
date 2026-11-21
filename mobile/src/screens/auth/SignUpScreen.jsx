import React, { useState } from 'react';
import { View } from 'react-native';
import { router, useLocalSearchParams } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { authApi } from '@/api/auth';
import { ApiError, fieldErrors, isNetworkError, isSignupClosed } from '@/api/errors';
import { Banner, Button, CheckRow, Field, Screen, T } from '@/components/ui';
import { LegalFooter, openLink } from '@/components/LegalFooter';
import { PasswordField } from '@/components/PasswordField';
import { LoadingState } from '@/components/states';
import { currentLanguage } from '@/i18n/runtime';
import { MIN_PASSWORD_LENGTH } from '@/screens/account/PasswordSection';
import { useAuth } from '@/state/AuthContext';
import { usePrefs } from '@/state/PrefsContext';
import { useServer } from '@/state/ServerContext';
import { spacing } from '@/theme/palette';
import { ResendLink } from './ResendLink';

/** The age the server asks people to confirm when client config does not say (Hosting.MinimumAge default). */
export const DEFAULT_MINIMUM_AGE = 18;

function timeZone() {
  try {
    return Intl.DateTimeFormat().resolvedOptions().timeZone || 'UTC';
  } catch {
    return 'UTC';
  }
}

/** True when the form can be sent: an email, a long enough password, and both boxes checked. */
export function signUpReady(form) {
  return form.email.trim().includes('@') && form.password.length >= MIN_PASSWORD_LENGTH && form.acceptTerms && form.confirmAge;
}

/** The request body for `POST /v1.0/public/signup`. Empty optional fields are left out. */
export function signUpBody(form, { locale, zone } = {}) {
  return {
    email: form.email.trim(),
    password: form.password,
    displayName: form.displayName.trim() || undefined,
    timeZone: zone || timeZone(),
    locale: locale || 'en',
    acceptTerms: form.acceptTerms,
    confirmAge: form.confirmAge,
    promoCode: form.promoCode.trim() || undefined,
  };
}

/** The web sign-up page on the same origin as the server (the hosted service serves the API and the web app together). */
export function webSignUpUrl(serverUrl) {
  return String(serverUrl || '').replace(/\/+$/, '') + '/signup';
}

/**
 * Create an account (plan section 33.1): `POST /v1.0/public/signup`, shown only when client config has
 * `signupEnabled`. The server answers the same way whether or not the email already has an account, so the screen
 * always ends on "check your email" (or "your account is ready" when verification is off).
 *
 * Cloudflare Turnstile: when client config carries a `turnstileSiteKey`, the server needs a human check the app
 * cannot show (it has no WebView), so the screen sends people to the web sign-up page and back to sign in.
 */
export function SignUpScreen() {
  const { t } = useTranslation();
  const { client } = useAuth();
  const { config, loaded, links } = useServer();
  const { prefs, update } = usePrefs();
  const params = useLocalSearchParams();
  const [form, setForm] = useState({ displayName: '', email: '', password: '', promoCode: typeof params.code === 'string' ? params.code : '', acceptTerms: false, confirmAge: false });
  const [busy, setBusy] = useState(false);
  const [closed, setClosed] = useState(false);
  const [result, setResult] = useState(null);
  const [errors, setErrors] = useState({});

  if (!loaded) return <LoadingState />;

  if (closed || !config || !config.signupEnabled) {
    return (
      <Screen testID="sign-up-closed">
        <Banner kind="info">{t('mobile.signUp.disabled')}</Banner>
        <Button title={t('mobile.signUp.backToSignIn')} onPress={() => router.replace('/sign-in')} />
        <LegalFooter />
      </Screen>
    );
  }

  if (config.turnstileSiteKey) {
    return (
      <Screen testID="sign-up-web">
        <Banner kind="info" title={t('mobile.signUp.webTitle')}>
          {t('mobile.signUp.webBody')}
        </Banner>
        <Button title={t('mobile.signUp.webOpen')} icon="open-outline" onPress={() => void openLink(webSignUpUrl(prefs.serverUrl))} testID="sign-up-open-web" />
        <Button title={t('mobile.signUp.backToSignIn')} kind="secondary" onPress={() => router.replace('/sign-in')} />
        <LegalFooter />
      </Screen>
    );
  }

  if (result) {
    const email = form.email.trim();
    return (
      <Screen testID="sign-up-done">
        {result.verify ? (
          <>
            <Banner kind="success" title={t('mobile.signUp.sentTitle')}>
              {t('mobile.signUp.sentBody', { email })}
            </Banner>
            <ResendLink email={email} />
          </>
        ) : (
          <Banner kind="success" title={t('mobile.signUp.readyTitle')}>
            {t('mobile.signUp.readyBody')}
          </Banner>
        )}
        <Button title={t('mobile.signUp.backToSignIn')} onPress={() => router.replace('/sign-in')} testID="sign-up-sign-in" />
      </Screen>
    );
  }

  const age = Number(config.minimumAge) > 0 ? Number(config.minimumAge) : DEFAULT_MINIMUM_AGE;
  const ready = signUpReady(form);

  const submit = async () => {
    if (!ready) return;
    setBusy(true);
    setErrors({});
    try {
      const response = await authApi.signUp(client, signUpBody(form, { locale: currentLanguage() || 'en' }));
      update({ lastEmail: form.email.trim(), lastTenantId: '' });
      setResult({ verify: !response || response.verificationRequired !== false });
    } catch (error) {
      if (isSignupClosed(error)) setClosed(true);
      else if (isNetworkError(error)) setErrors({ form: t('mobile.signIn.errorUnreachable') });
      else if (error instanceof ApiError && error.status === 429) setErrors({ form: t('mobile.signIn.errorRateLimited') });
      else setErrors({ ...fieldErrors(error), form: error.message });
    } finally {
      setBusy(false);
    }
  };

  const set = (key) => (value) => setForm({ ...form, [key]: value });
  return (
    <Screen testID="sign-up-screen">
      <T variant="muted">{t('mobile.signUp.intro')}</T>
      <Field label={t('mobile.signIn.email')} value={form.email} onChangeText={set('email')} autoCapitalize="none" autoCorrect={false} keyboardType="email-address" autoComplete="email" textContentType="emailAddress" error={errors.email} testID="sign-up-email" />
      <PasswordField label={t('mobile.signIn.password')} value={form.password} onChangeText={set('password')} hint={t('mobile.password.rule', { min: MIN_PASSWORD_LENGTH })} error={errors.password} testID="sign-up-password" />
      <Field label={t('mobile.signUp.displayName')} value={form.displayName} onChangeText={set('displayName')} autoComplete="name" textContentType="name" error={errors.displayName} testID="sign-up-name" />
      <Field label={t('mobile.signUp.promoCode')} hint={t('mobile.signUp.promoHint')} value={form.promoCode} onChangeText={set('promoCode')} autoCapitalize="characters" autoCorrect={false} error={errors.promoCode} testID="sign-up-code" />
      <CheckRow title={t('mobile.signUp.consent')} value={form.acceptTerms} onValueChange={set('acceptTerms')} testID="sign-up-terms" />
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs }}>
        <Button kind="ghost" title={t('mobile.legal.terms')} icon="document-text-outline" onPress={() => void openLink(links.terms)} testID="sign-up-terms-link" />
        <Button kind="ghost" title={t('mobile.legal.privacy')} icon="shield-checkmark-outline" onPress={() => void openLink(links.privacy)} testID="sign-up-privacy-link" />
      </View>
      <CheckRow title={t('mobile.signUp.confirmAge', { age })} value={form.confirmAge} onValueChange={set('confirmAge')} testID="sign-up-age" />
      {errors.form ? (
        <Banner kind="danger" testID="sign-up-error">
          {errors.form}
        </Banner>
      ) : null}
      <Button title={t('mobile.signUp.submit')} busy={busy} disabled={!ready} onPress={() => void submit()} testID="sign-up-submit" />
      <Button title={t('mobile.signUp.haveAccount')} kind="ghost" onPress={() => router.replace('/sign-in')} />
    </Screen>
  );
}
