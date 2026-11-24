import React from 'react';
import { useTranslation } from 'react-i18next';
import { TabStack } from '@/components/TabStack';
import { NotificationBell } from '@/screens/notifications/NotificationsScreen';

const bell = () => <NotificationBell />;

export default function TodayLayout() {
  const { t } = useTranslation();
  return (
    <TabStack
      titles={{
        index: t('mobile.tabs.today'),
        'catalog/index': t('mobile.catalog.title'),
        'catalog/issues': t('mobile.catalog.issues'),
        'catalog/[publicationId]/[issueId]': t('mobile.catalog.title'),
        'briefings/index': t('mobile.briefings.title'),
        'briefings/[briefingId]': t('mobile.briefings.reader'),
        'findings/index': t('mobile.findings.title'),
        'findings/[findingId]': t('mobile.findings.detail'),
        'predictions/index': t('mobile.predictions.title'),
        'predictions/[predictionId]': t('mobile.predictions.detail'),
        'suggestions/index': t('mobile.suggestions.title'),
        'requests/index': t('mobile.requests.title'),
        'requests/[requestId]': t('mobile.requests.detail'),
        'new-request': t('mobile.requests.new'),
        notifications: t('mobile.inbox.title'),
      }}
      headerRight={{ index: bell }}
    />
  );
}

/** Deep links straight to a detail screen still get the tab's list underneath, so Back works. */
export const unstable_settings = { anchor: 'index' };
