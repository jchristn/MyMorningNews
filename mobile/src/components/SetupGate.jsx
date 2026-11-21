import React, { useCallback, useEffect, useRef, useState } from 'react';
import { Linking, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { setupApi } from '@/api/account';
import { isNetworkError } from '@/api/errors';
import { formatDateTime } from '@/lib/format';
import { useSignOut } from '@/screens/account/useSignOut';
import { useAuth } from '@/state/AuthContext';
import { useServer } from '@/state/ServerContext';
import { spacing } from '@/theme/palette';
import { LoadingState } from './states';
import { Badge, Banner, Button, Card, Screen, T } from './ui';

const TONES = { Ready: 'success', Missing: 'danger', Unverified: 'warning', Failed: 'danger' };

/** The setup status in a shape the gate relies on. Throws when the answer is not a setup status. */
export function normalizeSetupStatus(raw) {
  if (!raw || typeof raw !== 'object' || typeof raw.ready !== 'boolean') throw new Error('The server did not return a setup status.');
  return {
    ready: raw.ready,
    canConfigure: Boolean(raw.canConfigure),
    checkedUtc: typeof raw.checkedUtc === 'string' ? raw.checkedUtc : null,
    checks: Array.isArray(raw.checks) ? raw.checks.filter((c) => c && typeof c === 'object') : [],
  };
}

/** Required checks that are not Ready yet, in the server's order. */
export function pendingChecks(status) {
  return status ? status.checks.filter((c) => c.required && c.state !== 'Ready') : [];
}

/** The web dashboard address from client config, when it is an http(s) URL. */
export function dashboardUrl(config) {
  const url = config && typeof config.appUrl === 'string' ? config.appUrl.trim() : '';
  return /^https?:\/\//i.test(url) ? url : null;
}

/**
 * The tenant's setup status for the signed-in session. `phase` is loading, ready, blocked (setup not finished), or
 * error (the status could not be read). A 409 SetupRequired from any request checks again, so the gate re-engages.
 * Any failure to read the status, including a server without the setup route, keeps the app closed.
 */
export function useSetupStatus() {
  const { client } = useAuth();
  const [state, setState] = useState({ phase: 'loading', status: null, error: null, checkedAt: null, busy: true });
  const sequence = useRef(0);

  const check = useCallback(async () => {
    const id = ++sequence.current;
    setState((s) => ({ ...s, busy: true }));
    try {
      const status = normalizeSetupStatus(await setupApi.status(client));
      if (id === sequence.current) setState({ phase: status.ready ? 'ready' : 'blocked', status, error: null, checkedAt: new Date(), busy: false });
    } catch (error) {
      if (id !== sequence.current) return;
      setState((s) => ({ ...s, phase: s.phase === 'blocked' ? 'blocked' : 'error', error, checkedAt: new Date(), busy: false }));
    }
  }, [client]);

  useEffect(() => {
    void check();
    const remove = typeof client.onSetupRequired === 'function' ? client.onSetupRequired(() => void check()) : () => undefined;
    return remove;
  }, [client, check]);

  return { ...state, check };
}

/**
 * Full screen while the workspace is not ready (or its status could not be read): what is missing, where an
 * administrator finishes setup (the web dashboard), Check again, and Sign out.
 */
export function SetupBlockedScreen({ setup }) {
  const { t, i18n } = useTranslation();
  const { config } = useServer();
  const signOut = useSignOut();
  const [signingOut, setSigningOut] = useState(false);
  const failed = setup.phase === 'error';
  const admin = Boolean(setup.status && setup.status.canConfigure);
  const url = dashboardUrl(config);
  const pending = pendingChecks(setup.status);
  const checkedAt = setup.status && setup.status.checkedUtc ? setup.status.checkedUtc : setup.checkedAt;
  const body = failed ? t('mobile.setup.loadErrorBody') : admin ? t('mobile.setup.adminBody') : t('mobile.setup.body');

  return (
    <Screen testID="setup-blocked" contentStyle={{ paddingTop: spacing.xxl }}>
      <T variant="heading" accessibilityRole="header">
        {failed ? t('mobile.setup.loadErrorTitle') : t('mobile.setup.title')}
      </T>
      <T>{body}</T>
      {admin && url ? (
        <Card testID="setup-dashboard">
          <View style={{ gap: spacing.sm }}>
            <T variant="small" selectable>
              {t('mobile.setup.dashboardAt', { url })}
            </T>
            <Button title={t('mobile.setup.openDashboard')} kind="secondary" onPress={() => void Linking.openURL(url).catch(() => undefined)} testID="setup-open-dashboard" />
          </View>
        </Card>
      ) : null}
      {pending.map((c) => (
        <Card key={c.key} testID={'setup-check-' + c.key}>
          <View style={{ gap: spacing.xs }}>
            <View style={{ flexDirection: 'row', justifyContent: 'space-between', alignItems: 'center', gap: spacing.sm }}>
              <T style={{ flex: 1, fontWeight: '600' }}>
                {c.resourceName ? t('mobile.setup.checkTitle', { kind: t('mobile.setup.kind.' + c.kind, { defaultValue: c.kind }), name: c.resourceName }) : t('mobile.setup.kind.' + c.kind, { defaultValue: c.kind })}
              </T>
              <Badge label={t('mobile.setup.state.' + c.state, { defaultValue: c.state })} tone={TONES[c.state] || 'neutral'} />
            </View>
            {c.message ? <T variant="small">{c.message}</T> : null}
          </View>
        </Card>
      ))}
      {setup.error ? (
        <Banner kind="danger" title={isNetworkError(setup.error) ? t('mobile.states.offlineTitle') : t('mobile.states.errorTitle')} testID="setup-error">
          {isNetworkError(setup.error) ? t('mobile.states.offlineBody') : setup.error.message || t('mobile.states.errorBody')}
        </Banner>
      ) : null}
      <Button title={setup.busy ? t('mobile.setup.checking') : t('mobile.setup.checkAgain')} busy={setup.busy} onPress={() => void setup.check()} testID="setup-check-again" />
      <Button
        title={t('mobile.setup.signOut')}
        kind="secondary"
        busy={signingOut}
        onPress={async () => {
          setSigningOut(true);
          try {
            await signOut();
          } finally {
            setSigningOut(false);
          }
        }}
        testID="setup-sign-out"
      />
      {checkedAt ? <T variant="small">{t('mobile.setup.checked', { time: formatDateTime(checkedAt, i18n.language) })}</T> : null}
    </Screen>
  );
}

/** Between sign-in and the tabs: nothing in the app opens until the tenant's required setup checks are Ready. */
export function SetupGate({ children }) {
  const setup = useSetupStatus();
  if (setup.phase === 'loading') return <LoadingState />;
  if (setup.phase === 'ready') return children;
  return <SetupBlockedScreen setup={setup} />;
}
