import React from 'react';
import { View } from 'react-native';
import { Redirect, useSegments } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { TabStack } from '@/components/TabStack';
import { SetupGate } from '@/components/SetupGate';
import { LoadingState } from '@/components/states';
import { ConnectionBanner } from '@/components/ConnectionBanner';
import { MiniPlayer } from '@/components/player/MiniPlayer';
import { ACCOUNT_SECTIONS } from '@/screens/account/AccountScreen';
import { useAuth } from '@/state/AuthContext';

/** One native history for every secondary screen, including links opened from a different tab. */
export default function AppLayout() {
  const { state } = useAuth();
  const { t } = useTranslation();
  const segments = useSegments();
  if (state.status === 'loading') return <LoadingState />;
  if (state.status === 'signedOut') return <Redirect href="/sign-in" />;
  const titles = {
    '(tabs)': '',
    'today/catalog/index': t('mobile.catalog.title'),
    'today/catalog/issues': t('mobile.catalog.issues'),
    'today/catalog/[publicationId]/[issueId]': t('mobile.catalog.title'),
    'today/briefings/index': t('mobile.briefings.title'),
    'today/briefings/[briefingId]': t('mobile.briefings.reader'),
    'today/findings/index': t('mobile.findings.title'),
    'today/findings/[findingId]': t('mobile.findings.detail'),
    'today/predictions/index': t('mobile.predictions.title'),
    'today/predictions/[predictionId]': t('mobile.predictions.detail'),
    'today/suggestions/index': t('mobile.suggestions.title'),
    'today/requests/index': t('mobile.requests.title'),
    'today/requests/[requestId]': t('mobile.requests.detail'),
    'today/new-request': t('mobile.requests.new'),
    'today/notifications': t('mobile.inbox.title'),
    'listen/player': t('mobile.player.title'),
    'listen/downloads': t('mobile.downloads.title'),
    'listen/dev-player': t('mobile.devPlayer.title'),
    'topics/new': t('mobile.topics.new'),
    'topics/[topicId]': t('mobile.topics.workspace'),
    'topics/editions/index': t('mobile.editions.title'),
    'topics/editions/[editionId]': t('mobile.editions.detail'),
    'topics/runs/[runId]': t('mobile.runs.detail'),
    'topics/findings/[findingId]': t('mobile.findings.detail'),
    'insights/relevance': t('mobile.insights.relevance.title'),
    'insights/accuracy': t('mobile.insights.accuracy.title'),
    'insights/feedback': t('mobile.insights.feedback.title'),
    'insights/preferences': t('mobile.insights.preferences.title'),
    'insights/usage': t('mobile.insights.usage.title'),
  };
  for (const section of ACCOUNT_SECTIONS) titles['account/' + section.key] = t('mobile.account.sections.' + section.key);
  return <SetupGate><View style={{ flex: 1 }}>
    <TabStack titles={titles} options={{ '(tabs)': { headerShown: false } }} modal={['listen/player']} />
    {!segments.includes('(tabs)') ? <><ConnectionBanner /><MiniPlayer /></> : null}
  </View></SetupGate>;
}

/** A directly opened detail can always go back to the root tabs. */
export const unstable_settings = { anchor: '(tabs)' };
