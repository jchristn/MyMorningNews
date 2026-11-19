import React from 'react';
import { useTranslation } from 'react-i18next';
import { TabStack } from '@/components/TabStack';

export default function ListenLayout() {
  const { t } = useTranslation();
  return (
    <TabStack
      titles={{
        index: t('mobile.tabs.listen'),
        player: t('mobile.player.title'),
        downloads: t('mobile.downloads.title'),
        'dev-player': t('mobile.devPlayer.title'),
      }}
      modal={['player']}
    />
  );
}
