import React from 'react';
import { useTranslation } from 'react-i18next';
import { TabStack } from '@/components/TabStack';

export default function InsightsLayout() {
  const { t } = useTranslation();
  return (
    <TabStack
      titles={{
        index: t('mobile.tabs.insights'),
        relevance: t('mobile.insights.relevance.title'),
        accuracy: t('mobile.insights.accuracy.title'),
        feedback: t('mobile.insights.feedback.title'),
        preferences: t('mobile.insights.preferences.title'),
        usage: t('mobile.insights.usage.title'),
      }}
    />
  );
}

/** Deep links straight to a detail screen still get the tab's list underneath, so Back works. */
export const unstable_settings = { anchor: 'index' };
