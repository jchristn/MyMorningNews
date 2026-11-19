import React, { useState } from 'react';
import { Image, View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { ApiError, TenantSelectionRequiredError, isNetworkError } from '@/api/errors';
import { Banner, Button, Choice, Field, IconButton, Screen, T } from '@/components/ui';
import { LegalFooter } from '@/components/LegalFooter';
import { useAuth } from '@/state/AuthContext';
import { usePrefs } from '@/state/PrefsContext';
import { isInsecureServer } from '@/storage/prefs';
import { spacing } from '@/theme/palette';

/** Map a sign-in failure to a message. */
export function signInErrorKey(error) {
  if (isNetworkError(error)) return 'mobile.signIn.errorUnreachable';
  if (error instanceof ApiError) {
    if (error.status === 400 && /tenant/i.test(error.message || '')) return 'mobile.signIn.errorTenantRequired';
    if (error.status === 401 || error.status === 403) return 'mobile.signIn.errorCredentials';
    if (error.status === 404) return 'mobile.signIn.errorNotServer';
    if (error.status === 429) return 'mobile.signIn.errorRateLimited';
  }
  return 'mobile.signIn.errorGeneric';
}

/**
 * Sign in with email and password (plan section 33.1). The tenant ID is optional: servers with tenant-less sign-in
 * (contract B) find the tenant, and ask which one when the account belongs to several. Older servers that require a
 * tenant say so, and the field takes it.
 */
export function SignInScreen() {
  const { t } = useTranslation();
  const { auth, state } = useAuth();
  const { prefs, update } = usePrefs();
  const [email, setEmail] = useState(prefs.lastEmail);
  const [password, setPassword] = useState('');
  const [reveal, setReveal] = useState(false);
  const [tenantId, setTenantId] = useState(prefs.lastTenantId);
  const [showTenant, setShowTenant] = useState(Boolean(prefs.lastTenantId));
  const [tenants, setTenants] = useState(null);
  const [chosenTenant, setChosenTenant] = useState(null);
  const [busy, setBusy] = useState(false);
  const [errorKey, setErrorKey] = useState(null);

  const submit = async (tenantOverride) => {
    if (!email.trim() || !password) {
      setErrorKey('mobile.signIn.errorMissing');
      return;
    }
    setBusy(true);
    setErrorKey(null);
    const tenant = tenantOverride || tenantId.trim();
    try {
      await auth.signIn({ email, password, tenantId: tenant });
      update({ lastEmail: email.trim(), lastTenantId: tenant || '' });
      setTenants(null);
    } catch (error) {
      if (error instanceof TenantSelectionRequiredError && error.tenants.length > 0) {
        setTenants(error.tenants);
        setChosenTenant(error.tenants[0].id);
      } else {
        const key = signInErrorKey(error);
        if (key === 'mobile.signIn.errorTenantRequired') setShowTenant(true);
        setErrorKey(key);
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
          <Choice accessibilityLabel={t('mobile.signIn.chooseTenant')} value={chosenTenant} onChange={setChosenTenant} options={tenants.map((tn) => ({ value: tn.id, label: tn.name || tn.id, detail: tn.id }))} />
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
          {showTenant ? (
            <Field label={t('mobile.signIn.tenant')} hint={t('mobile.signIn.tenantHint')} value={tenantId} onChangeText={setTenantId} autoCapitalize="none" autoCorrect={false} testID="sign-in-tenant" />
          ) : (
            <Button title={t('mobile.signIn.addTenant')} kind="ghost" onPress={() => setShowTenant(true)} />
          )}
          {errorKey ? (
            <Banner kind="danger" testID="sign-in-error">
              {t(errorKey)}
            </Banner>
          ) : null}
          <Button title={t('mobile.signIn.submit')} busy={busy} onPress={() => void submit()} testID="sign-in-submit" />
          <View style={{ flexDirection: 'row', justifyContent: 'space-between', flexWrap: 'wrap' }}>
            <Button title={t('mobile.signIn.createAccount')} kind="ghost" onPress={() => router.push('/sign-up')} />
            <Button title={t('mobile.signIn.forgot')} kind="ghost" onPress={() => router.push('/forgot-password')} />
          </View>
        </View>
      )}
      <LegalFooter />
    </Screen>
  );
}
