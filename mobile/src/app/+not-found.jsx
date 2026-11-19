import React from 'react';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { EmptyState } from '@/components/states';
import { Screen } from '@/components/ui';

/** A link the app does not know (for example from a newer server). */
export default function NotFound() {
  const { t } = useTranslation();
  return (
    <Screen>
      <EmptyState icon="compass-outline" title={t('mobile.notFound.title')} body={t('mobile.notFound.body')} actionLabel={t('mobile.notFound.action')} onAction={() => router.replace('/today')} />
    </Screen>
  );
}
