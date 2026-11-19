import React from 'react';
import { useTranslation } from 'react-i18next';
import { TabStack } from '@/components/TabStack';

export default function TopicsLayout() {
  const { t } = useTranslation();
  return (
    <TabStack
      titles={{
        index: t('mobile.tabs.topics'),
        new: t('mobile.topics.new'),
        '[topicId]': t('mobile.topics.workspace'),
        'editions/index': t('mobile.editions.title'),
        'editions/[editionId]': t('mobile.editions.detail'),
        'runs/[runId]': t('mobile.runs.detail'),
        'findings/[findingId]': t('mobile.findings.detail'),
      }}
    />
  );
}

/** Deep links straight to a detail screen still get the tab's list underneath, so Back works. */
export const unstable_settings = { anchor: 'index' };
