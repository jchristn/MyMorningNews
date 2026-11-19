import React from 'react';
import { useTranslation } from 'react-i18next';
import { TabStack } from '@/components/TabStack';

export default function TodayLayout() {
  const { t } = useTranslation();
  return (
    <TabStack
      titles={{
        index: t('mobile.tabs.today'),
        'briefings/index': t('mobile.briefings.title'),
        'briefings/[briefingId]': t('mobile.briefings.reader'),
        'findings/index': t('mobile.findings.title'),
        'findings/[findingId]': t('mobile.findings.detail'),
        'predictions/index': t('mobile.predictions.title'),
        'predictions/[predictionId]': t('mobile.predictions.detail'),
        'suggestions/index': t('mobile.suggestions.title'),
      }}
    />
  );
}
