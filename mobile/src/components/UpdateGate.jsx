import React, { useEffect, useState } from 'react';
import { AppState, Linking, Platform, View } from 'react-native';
import Constants from 'expo-constants';
import * as SplashScreen from 'expo-splash-screen';
import Ionicons from '@expo/vector-icons/Ionicons';
import { useTranslation } from 'react-i18next';
import { SafeAreaView } from 'react-native-safe-area-context';
import { isUpdateRequired, storeUrl } from '@/lib/version';
import { usePrefs } from '@/state/PrefsContext';
import { useServer } from '@/state/ServerContext';
import { DEFAULT_SERVER_URL } from '@/storage/prefs';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { Banner, Button, T } from './ui';

/** The version the stores know this build by (`0.1.0`; the `-alpha` label is not part of it). */
export function installedAppVersion() {
  const config = Constants.expoConfig || {};
  return config.version || Constants.nativeApplicationVersion || '';
}

/**
 * Enforces the server's `minimumAppVersion` (client config, plan section 35): when the installed version is older,
 * a full-screen message replaces the app, with a button to the store listing when one is known. There is no way to
 * dismiss it. Client config is read at launch and again each time the app returns to the foreground.
 *
 * Nobody is locked out by accident: a missing or unreadable minimum, or a missing installed version, never blocks.
 * A failed config read keeps the last verdict (so going offline does not lift the block, and never creates one).
 * When the block comes from a server other than the hosted service, the screen offers the hosted service, so a
 * self-hosted address that asks for a newer app is not a dead end.
 */
export function UpdateGate({ children, installedVersion, platform = Platform.OS }) {
  const { config, links, reload } = useServer();
  const installed = installedVersion === undefined ? installedAppVersion() : installedVersion;
  const minimum = config ? config.minimumAppVersion : undefined;
  const verdict = config ? isUpdateRequired(installed, minimum) : null;
  // The last known verdict, kept while a config read fails (adjusted during render, as React documents).
  const [last, setLast] = useState(false);
  if (verdict !== null && verdict !== last) setLast(verdict);
  const required = verdict === null ? last : verdict;

  useEffect(() => {
    const subscription = AppState.addEventListener('change', (next) => {
      if (next === 'active') reload();
    });
    return () => subscription.remove();
  }, [reload]);

  useEffect(() => {
    // The navigator, which normally hides the splash screen, is not mounted behind this screen.
    if (required) void SplashScreen.hideAsync().catch(() => undefined);
  }, [required]);

  if (!required) return children;
  const android = (Constants.expoConfig && Constants.expoConfig.android) || {};
  return <UpdateRequiredScreen installed={installed} minimum={minimum || ''} platform={platform} url={storeUrl({ platform, links, androidPackage: android.package })} />;
}

function UpdateRequiredScreen({ installed, minimum, platform, url }) {
  const p = useTheme();
  const { t } = useTranslation();
  const { prefs, update } = usePrefs();
  const [failed, setFailed] = useState(false);
  const open = async () => {
    setFailed(false);
    try {
      await Linking.openURL(url);
    } catch {
      setFailed(true);
    }
  };
  return (
    <SafeAreaView testID="update-required" accessibilityViewIsModal style={{ flex: 1, backgroundColor: p.bg }}>
      <View style={{ flex: 1, alignItems: 'center', justifyContent: 'center', padding: spacing.xl }}>
        <View style={{ width: '100%', maxWidth: 480, gap: spacing.md, alignItems: 'center' }}>
          <Ionicons name="cloud-download-outline" size={56} color={p.primary} />
          <T variant="title" accessibilityRole="header" style={{ textAlign: 'center' }}>
            {t('mobile.update.title')}
          </T>
          <T style={{ textAlign: 'center' }}>{t('mobile.update.body')}</T>
          <T variant="small" style={{ textAlign: 'center' }} testID="update-versions">
            {t('mobile.update.versions', { installed, required: minimum })}
          </T>
          {url ? (
            <Button title={platform === 'ios' ? t('mobile.update.openAppStore') : t('mobile.update.openPlayStore')} icon="open-outline" onPress={() => void open()} style={{ alignSelf: 'stretch' }} testID="update-open-store" />
          ) : (
            <T variant="muted" style={{ textAlign: 'center' }} testID="update-no-store">
              {t('mobile.update.noStore')}
            </T>
          )}
          {failed ? <Banner kind="danger">{t('mobile.update.failed')}</Banner> : null}
          {prefs.serverUrl !== DEFAULT_SERVER_URL ? (
            <Button title={t('mobile.update.hostedService')} kind="ghost" onPress={() => update({ serverUrl: DEFAULT_SERVER_URL })} testID="update-hosted-service" />
          ) : null}
        </View>
      </View>
    </SafeAreaView>
  );
}
