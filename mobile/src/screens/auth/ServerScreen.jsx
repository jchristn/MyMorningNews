import React, { useState } from 'react';
import { Platform, View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { ApiClient } from '@/api/client';
import { serverApi } from '@/api/account';
import { Banner, Button, Card, Field, ListRow, Screen, T } from '@/components/ui';
import { usePrefs } from '@/state/PrefsContext';
import { DEFAULT_SERVER_URL, isInsecureServer, normalizeServerUrl } from '@/storage/prefs';
import { spacing } from '@/theme/palette';

/** Local servers offered in development builds (simulator and emulator loopback aliases). */
export function developmentServers() {
  return Platform.OS === 'android' ? ['http://10.0.2.2:12100'] : ['http://127.0.0.1:12100'];
}

/**
 * Check that a URL is a My Morning News server: `GET /v1.0/api/health` (anonymous) must answer with a status and a
 * version. Resolves `{ ok, version }` or `{ ok: false, reason: 'unreachable' | 'notServer' }`.
 */
export async function probeServer(url, fetchImpl) {
  const client = new ApiClient({ baseUrl: url, fetchImpl, timeoutMs: 10000 });
  try {
    const health = await serverApi.health(client);
    if (health && typeof health === 'object' && typeof health.status === 'string') return { ok: true, version: health.version || null };
    return { ok: false, reason: 'notServer' };
  } catch (error) {
    return { ok: false, reason: error && error.status ? 'notServer' : 'unreachable' };
  }
}

/** Change server: the hosted service, or a self-hosted server (each server keeps its own sign-in). */
export function ServerScreen() {
  return (
    <Screen testID="server-screen">
      <ServerForm />
    </Screen>
  );
}

/** The server form, also embedded in Account > Server. */
export function ServerForm({ onConnected }) {
  const { t } = useTranslation();
  const { prefs, update } = usePrefs();
  const [value, setValue] = useState(prefs.serverUrl);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const normalized = normalizeServerUrl(value);

  const connect = async (target) => {
    const url = normalizeServerUrl(target);
    if (!url) {
      setError(t('mobile.server.invalid'));
      return;
    }
    setBusy(true);
    setError(null);
    const result = await probeServer(url);
    setBusy(false);
    if (!result.ok) {
      setError(result.reason === 'unreachable' ? t('mobile.server.unreachable', { url }) : t('mobile.server.notServer'));
      return;
    }
    update({ serverUrl: url });
    if (onConnected) onConnected(url);
    else if (router.canGoBack()) router.back();
    else router.replace('/sign-in');
  };

  return (
    <>
      <T variant="muted">{t('mobile.server.subtitle')}</T>
      <Field label={t('mobile.server.url')} value={value} onChangeText={setValue} autoCapitalize="none" autoCorrect={false} keyboardType="url" clearButtonMode="while-editing" selectTextOnFocus placeholder={DEFAULT_SERVER_URL} testID="server-url" onSubmitEditing={() => void connect(value)} />
      {normalized && isInsecureServer(normalized) ? <Banner kind="warning" title={t('mobile.server.insecureTitle')}>{t('mobile.server.insecureBody')}</Banner> : null}
      {error ? <Banner kind="danger">{error}</Banner> : null}
      <Button title={t('mobile.server.connect')} busy={busy} onPress={() => void connect(value)} testID="server-connect" />
      <Card>
        <ListRow title={t('mobile.server.useHosted')} detail={DEFAULT_SERVER_URL} icon="cloud-outline" onPress={() => void connect(DEFAULT_SERVER_URL)} />
        {__DEV__
          ? developmentServers().map((url) => <ListRow key={url} title={t('mobile.server.useLocal')} detail={url} icon="laptop-outline" onPress={() => void connect(url)} />)
          : null}
      </Card>
      <View style={{ gap: spacing.xs }}>
        <T variant="small">{t('mobile.server.selfHostedHint')}</T>
      </View>
    </>
  );
}
