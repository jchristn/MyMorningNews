import React from 'react';
import { View } from 'react-native';
import * as WebBrowser from 'expo-web-browser';
import { useTranslation } from 'react-i18next';
import { useServer } from '@/state/ServerContext';
import { spacing } from '@/theme/palette';
import { Button, T } from './ui';

export const COPYRIGHT = '(c) 2026 Lattice AI LLC';

/** Open a web page in the in-app browser. */
export function openLink(url) {
  return WebBrowser.openBrowserAsync(url).catch(() => undefined);
}

/** Privacy, terms, and support links plus the copyright line (sign-in, sign-up, About). */
export function LegalFooter() {
  const { t } = useTranslation();
  const { links, config } = useServer();
  return (
    <View style={{ alignItems: 'center', gap: spacing.xs, marginTop: spacing.lg }}>
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', justifyContent: 'center' }}>
        <Button kind="ghost" title={t('mobile.legal.privacy')} onPress={() => void openLink(links.privacy)} />
        <Button kind="ghost" title={t('mobile.legal.terms')} onPress={() => void openLink(links.terms)} />
        <Button kind="ghost" title={t('mobile.legal.support')} onPress={() => void openLink(links.support)} />
      </View>
      <T variant="small">{(config && config.copyright) || COPYRIGHT}</T>
    </View>
  );
}
