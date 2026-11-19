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
        editions: t('mobile.editions.title'),
        '[topicId]': t('mobile.topics.workspace'),
      }}
    />
  );
}
