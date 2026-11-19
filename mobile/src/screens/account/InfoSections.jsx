import React from 'react';
import { Image, Linking } from 'react-native';
import Constants from 'expo-constants';
import * as Updates from 'expo-updates';
import { useTranslation } from 'react-i18next';
import { serverApi } from '@/api/account';
import { COPYRIGHT, openLink } from '@/components/LegalFooter';
import { Banner, Button, Card, KeyValue, ListRow, Screen, SectionHeader, T } from '@/components/ui';
import { ServerForm } from '@/screens/auth/ServerScreen';
import { useAuth } from '@/state/AuthContext';
import { usePrefs } from '@/state/PrefsContext';
import { SUPPORT_EMAIL, useServer } from '@/state/ServerContext';
import { useResource } from '@/state/useResource';
import { isInsecureServer } from '@/storage/prefs';

/** App version facts from the build: store version, build number, release label, and the running update. */
export function appVersionInfo() {
  const config = Constants.expoConfig || {};
  const extra = config.extra || {};
  const build = (config.ios && config.ios.buildNumber) || (config.android && config.android.versionCode) || Constants.nativeBuildVersion || '';
  return {
    version: config.version || Constants.nativeApplicationVersion || '',
    build: String(build || ''),
    release: extra.releaseVersion || '',
    updateId: Updates.updateId || null,
    channel: Updates.channel || null,
  };
}

/** Server: the address in use, its version, and changing to another server (each keeps its own sign-in). */
export function ServerSection() {
  const { t } = useTranslation();
  const { client } = useAuth();
  const { prefs } = usePrefs();
  const { config } = useServer();
  const info = useResource(() => serverApi.info(client), [client]);
  const version = (config && config.serverVersion) || (info.data && info.data.version) || t('mobile.about.unknown');
  return (
    <Screen testID="server-section">
      <Card>
        <KeyValue label={t('mobile.server.current')} value={prefs.serverUrl} selectable />
        <KeyValue label={t('mobile.about.serverVersion')} value={version} />
      </Card>
      {isInsecureServer(prefs.serverUrl) ? <Banner kind="warning" title={t('mobile.server.insecureTitle')}>{t('mobile.server.insecureBody')}</Banner> : null}
      <SectionHeader title={t('mobile.server.change')} />
      <T variant="small">{t('mobile.server.changeNote')}</T>
      <ServerForm onConnected={() => undefined} />
    </Screen>
  );
}

/** About: versions, copyright, and the legal and support links. */
export function AboutSection() {
  const { t } = useTranslation();
  const { client } = useAuth();
  const { config, links } = useServer();
  const info = useResource(() => serverApi.info(client), [client]);
  const app = appVersionInfo();
  const serverVersion = (config && config.serverVersion) || (info.data && info.data.version) || t('mobile.about.unknown');
  return (
    <Screen testID="about-section">
      <Image source={require('../../../assets/images/logo.png')} style={{ width: 240, height: 80, alignSelf: 'center' }} resizeMode="contain" accessibilityRole="image" accessibilityLabel={t('mobile.app.name')} />
      <Card>
        <KeyValue label={t('mobile.about.appVersion')} value={app.release ? app.release + ' (' + app.version + ')' : app.version} />
        <KeyValue label={t('mobile.about.build')} value={app.build} />
        {app.updateId ? <KeyValue label={t('mobile.about.update')} value={app.updateId.slice(0, 8)} /> : null}
        <KeyValue label={t('mobile.about.serverVersion')} value={serverVersion} />
      </Card>
      <Card>
        <ListRow icon="shield-checkmark-outline" title={t('mobile.legal.privacy')} onPress={() => void openLink(links.privacy)} />
        <ListRow icon="document-text-outline" title={t('mobile.legal.terms')} onPress={() => void openLink(links.terms)} />
        <ListRow icon="help-buoy-outline" title={t('mobile.legal.support')} onPress={() => void openLink(links.support)} />
        <ListRow icon="mail-outline" title={t('mobile.about.contact')} detail={SUPPORT_EMAIL} onPress={() => void Linking.openURL('mailto:' + SUPPORT_EMAIL)} />
      </Card>
      <T variant="small" style={{ textAlign: 'center' }} testID="about-copyright">
        {(config && config.copyright) || COPYRIGHT}
      </T>
    </Screen>
  );
}

/** Button to the web account-deletion page (also required by Google Play as a web link). */
export function DeleteOnWebButton() {
  const { t } = useTranslation();
  const { links } = useServer();
  return <Button kind="secondary" title={t('mobile.delete.web')} onPress={() => void openLink(links.deleteAccount)} />;
}
