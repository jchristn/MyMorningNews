import React, { useState } from 'react';
import { Image, View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { ApiError, TenantSelectionRequiredError, isEmailNotVerified, isNetworkError } from '@/api/errors';
import { Banner, Button, Choice, Field, IconButton, Screen, T } from '@/components/ui';
import { LegalFooter } from '@/components/LegalFooter';
import { useAuth } from '@/state/AuthContext';
import { usePrefs } from '@/state/PrefsContext';
import { useServer } from '@/state/ServerContext';
import { isInsecureServer } from '@/storage/prefs';
import { spacing } from '@/theme/palette';
import { ResendLink } from './ResendLink';

/** Map a sign-in failure to a message key. */
export function signInErrorKey(error) {
  if (isNetworkError(error)) return 'mobile.signIn.errorUnreachable';
  if (isEmailNotVerified(error)) return 'mobile.signIn.unverifiedBody';
  if (error instanceof ApiError) {
    if (error.status === 401 || error.status === 403) return 'mobile.signIn.errorCredentials';
    if (error.status === 404) return 'mobile.signIn.errorNotServer';
    if (error.status === 429) return 'mobile.signIn.errorRateLimited';
  }
  return 'mobile.signIn.errorGeneric';
}

/** The server's own words for a request it rejected (400), shown as they are; null for every other failure. */
export function signInServerMessage(error) {
  if (!(error instanceof ApiError) || error.status !== 400) return null;
  const message = error.body && typeof error.body.message === 'string' ? error.body.message.trim() : '';
  return message || null;
}

/** The workspace to preselect in the picker: the one chosen last time when it is offered again, else the first. */
export function preselectedTenant(tenants, lastTenantId) {
  const last = tenants.find((tn) => tn.id === lastTenantId);
  return last ? last.id : tenants[0].id;
}

/** Picker rows: each workspace by name. The ID appears only to tell apart workspaces that share a name. */
export function tenantOptions(tenants) {
  const labels = tenants.map((tn) => tn.name || tn.id);
  return tenants.map((tn, i) => ({ value: tn.id, label: labels[i], detail: labels.indexOf(labels[i]) !== labels.lastIndexOf(labels[i]) ? tn.id : undefined }));
}

/**
 * Sign in with email and password (plan section 33.1). The screen never asks for a tenant: the server finds it from
 * the credentials (contract B). When the email belongs to several workspaces the server lists them (409) and the
 * reader picks one; a list of one is used directly. The last pick is remembered only to preselect it the next time
 * the same choice comes up. An account whose email is not confirmed yet (403 EmailNotVerified, after
 * sign-up) is offered a new verification link. "Create account" appears only when client config has sign-up open.
 */
export function SignInScreen() {
  const { t } = useTranslation();
  const { auth, state } = useAuth();
  const { prefs, update } = usePrefs();
  const { config } = useServer();
  const [email, setEmail] = useState(prefs.lastEmail);
  const [password, setPassword] = useState('');
  const [reveal, setReveal] = useState(false);
  const [tenants, setTenants] = useState(null);
  const [chosenTenant, setChosenTenant] = useState(null);
  const [busy, setBusy] = useState(false);
  const [errorKey, setErrorKey] = useState(null);
  const [errorText, setErrorText] = useState(null);

  const submit = async (tenant = '') => {
    if (!email.trim() || !password) {
      setErrorKey('mobile.signIn.errorMissing');
      return;
    }
    setBusy(true);
    setErrorKey(null);
    setErrorText(null);
    try {
      let chosen = tenant;
      try {
        await auth.signIn({ email, password, tenantId: chosen });
      } catch (error) {
        // A list of one is not a choice: sign in to it without asking.
        if (chosen || !(error instanceof TenantSelectionRequiredError) || error.tenants.length !== 1) throw error;
        chosen = error.tenants[0].id;
        await auth.signIn({ email, password, tenantId: chosen });
      }
      update({ lastEmail: email.trim(), lastTenantId: chosen });
      setTenants(null);
    } catch (error) {
      if (!tenant && error instanceof TenantSelectionRequiredError && error.tenants.length > 1) {
        setTenants(error.tenants);
        setChosenTenant(preselectedTenant(error.tenants, prefs.lastTenantId));
      } else {
        setTenants(null);
        setErrorKey(signInErrorKey(error));
        setErrorText(signInServerMessage(error));
      }
    } finally {
      setBusy(false);
    }
  };

  const insecure = isInsecureServer(prefs.serverUrl);
  return (
    <Screen testID="sign-in-screen">
      <View style={{ alignItems: 'center', gap: spacing.sm, paddingTop: spacing.xxl }}>
        <Image source={require('../../../assets/images/logo.png')} style={{ width: 260, height: 87 }} resizeMode="contain" accessibilityRole="image" accessibilityLabel={t('mobile.app.name')} />
        <T variant="muted" style={{ textAlign: 'center' }}>
          {t('mobile.signIn.tagline')}
        </T>
      </View>

      {state.reason === 'expired' ? <Banner kind="warning" title={t('mobile.signIn.expiredTitle')}>{t('mobile.signIn.expiredBody')}</Banner> : null}

      <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
        <T variant="small" style={{ flex: 1 }} numberOfLines={2} testID="sign-in-server">
          {t('mobile.signIn.server', { url: prefs.serverUrl })}
        </T>
        <Button title={t('mobile.signIn.changeServer')} kind="ghost" onPress={() => router.push('/server')} testID="change-server" />
      </View>
      {insecure ? <Banner kind="warning" title={t('mobile.server.insecureTitle')}>{t('mobile.server.insecureBody')}</Banner> : null}

      {tenants ? (
        <View style={{ gap: spacing.md }} testID="tenant-picker">
          <T variant="title">{t('mobile.signIn.chooseTenant')}</T>
          <T variant="muted">{t('mobile.signIn.chooseTenantBody')}</T>
          <Choice accessibilityLabel={t('mobile.signIn.chooseTenant')} value={chosenTenant} onChange={setChosenTenant} options={tenantOptions(tenants)} />
          <Button title={t('mobile.signIn.continue')} busy={busy} onPress={() => void submit(chosenTenant)} testID="tenant-continue" />
          <Button title={t('mobile.common.cancel')} kind="ghost" onPress={() => setTenants(null)} />
        </View>
      ) : (
        <View style={{ gap: spacing.md }}>
          <Field label={t('mobile.signIn.email')} value={email} onChangeText={setEmail} autoCapitalize="none" autoComplete="email" keyboardType="email-address" textContentType="username" autoCorrect={false} testID="sign-in-email" />
          <View>
            <Field label={t('mobile.signIn.password')} value={password} onChangeText={setPassword} secureTextEntry={!reveal} autoComplete="current-password" textContentType="password" testID="sign-in-password" onSubmitEditing={() => void submit()} />
            <IconButton icon={reveal ? 'eye-off-outline' : 'eye-outline'} accessibilityLabel={reveal ? t('mobile.signIn.hidePassword') : t('mobile.signIn.showPassword')} onPress={() => setReveal(!reveal)} style={{ position: 'absolute', end: 0, bottom: 0 }} />
          </View>
          {errorKey === 'mobile.signIn.unverifiedBody' ? (
            <>
              <Banner kind="warning" title={t('mobile.signIn.unverifiedTitle')} testID="sign-in-unverified">
                {t(errorKey)}
              </Banner>
              <ResendLink key={email.trim()} email={email} />
            </>
          ) : errorKey ? (
            <Banner kind="danger" testID="sign-in-error">
              {errorText || t(errorKey)}
            </Banner>
          ) : null}
          <Button title={t('mobile.signIn.submit')} busy={busy} onPress={() => void submit()} testID="sign-in-submit" />
          <View style={{ flexDirection: 'row', justifyContent: 'space-between', flexWrap: 'wrap' }}>
            {config && config.signupEnabled ? <Button title={t('mobile.signIn.createAccount')} kind="ghost" onPress={() => router.push('/sign-up')} testID="sign-in-create" /> : null}
            <Button title={t('mobile.signIn.forgot')} kind="ghost" onPress={() => router.push('/forgot-password')} testID="sign-in-forgot" />
          </View>
        </View>
      )}
      <LegalFooter />
    </Screen>
  );
}
