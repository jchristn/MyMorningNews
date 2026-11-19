import React from 'react';
import { useTranslation } from 'react-i18next';
import { TabStack } from '@/components/TabStack';
import { ACCOUNT_SECTIONS } from '@/screens/account/AccountScreen';

export default function AccountLayout() {
  const { t } = useTranslation();
  const titles = { index: t('mobile.tabs.account') };
  for (const section of ACCOUNT_SECTIONS) titles[section.key] = t('mobile.account.sections.' + section.key);
  return <TabStack titles={titles} />;
}

/** Deep links straight to a detail screen still get the tab's list underneath, so Back works. */
export const unstable_settings = { anchor: 'index' };
