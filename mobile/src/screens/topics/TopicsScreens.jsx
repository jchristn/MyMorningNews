import React, { useState } from 'react';
import { View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { isNotAvailable } from '@/api/errors';
import { editionsApi, refinementApi, topicsApi } from '@/api/research';
import { FeedbackBar } from '@/components/FeedbackBar';
import { NotAvailableState } from '@/components/states';
import { Banner, Button, Card, Field, ListRow, Screen, SectionHeader, T } from '@/components/ui';
import { useAuth } from '@/state/AuthContext';
import { spacing } from '@/theme/palette';
import { ResourceDetail } from '../common/ResourceDetail';
import { ResourceListScreen } from '../common/ResourceListScreen';

const PAGE = { pageNumber: 1, pageSize: 100, sortBy: 'Name', sortDirection: 'Ascending' };

/** Workspace sections of a topic (dashboard `/topics/:id` tabs, plan section 25.4); each fills in with its phase. */
export const WORKSPACE_SECTIONS = ['overview', 'context', 'schedule', 'sources', 'runs', 'findings', 'predictions', 'suggestions', 'feedback', 'preferences', 'analytics', 'delivery', 'prompts'];

export function TopicsScreen() {
  const { t } = useTranslation();
  const { client } = useAuth();
  return (
    <View style={{ flex: 1 }}>
      <View style={{ flexDirection: 'row', gap: spacing.sm, padding: spacing.md }}>
        <Button title={t('mobile.topics.new')} icon="add" onPress={() => router.push('/topics/new')} style={{ flex: 1 }} testID="new-topic" />
        <Button title={t('mobile.editions.title')} icon="calendar-outline" kind="secondary" onPress={() => router.push('/topics/editions')} style={{ flex: 1 }} />
      </View>
      <ResourceListScreen
        testID="topics-screen"
        load={() => topicsApi.list(client, PAGE)}
        deps={[client]}
        feature={t('mobile.features.topics')}
        empty={{ icon: 'list-outline', title: t('mobile.topics.emptyTitle'), body: t('mobile.topics.emptyBody'), actionLabel: t('mobile.topics.new'), onAction: () => router.push('/topics/new') }}
        detailHref={(id) => '/topics/' + encodeURIComponent(id)}
        renderDetail={(id) => <TopicWorkspace key={id} id={id} />}
        placeholder={t('mobile.topics.select')}
      />
    </View>
  );
}

/** Topic workspace: overview now; the other sections arrive with their server phases. */
export function TopicWorkspace({ id }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  const [run, setRun] = useState(null);
  const runNow = async () => {
    setRun('running');
    try {
      await topicsApi.run(client, id);
      setRun('started');
    } catch (error) {
      setRun(isNotAvailable(error) ? 'unavailable' : 'failed');
    }
  };
  return (
    <ResourceDetail testID="topic-workspace" load={() => topicsApi.get(client, id)} deps={[client, id]} feature={t('mobile.features.topics')}>
      {(topic) => (
        <>
          <Button title={t('mobile.topics.runNow')} icon="play-outline" busy={run === 'running'} onPress={() => void runNow()} />
          {run === 'started' ? <Banner kind="success">{t('mobile.topics.runStarted')}</Banner> : null}
          {run === 'unavailable' ? <Banner kind="info">{t('mobile.states.notAvailableBody')}</Banner> : null}
          {run === 'failed' ? <Banner kind="danger">{t('mobile.topics.runFailed')}</Banner> : null}
          <SectionHeader title={t('mobile.topics.workspace')} />
          <Card>
            {WORKSPACE_SECTIONS.map((section) => (
              <ListRow key={section} title={t('mobile.topics.sections.' + section)} detail={t('mobile.topics.sectionLater')} />
            ))}
          </Card>
          <FeedbackBar targetType="Topic" targetId={topic.id} />
        </>
      )}
    </ResourceDetail>
  );
}

/** The session ID from a start response, whether it is the session itself or wraps it. */
export function sessionId(response) {
  if (!response || typeof response !== 'object') return '';
  return String(response.id || (response.session && response.session.id) || '');
}

/**
 * New topic: the refinement wizard as a chat (plan section 35.3) starts from one sentence. The question rounds,
 * dimensions, and editable brief arrive with the refinement API (Phase 4).
 */
export function NewTopicScreen() {
  const { t } = useTranslation();
  const { client } = useAuth();
  const [description, setDescription] = useState('');
  const [status, setStatus] = useState(null);
  const [session, setSession] = useState(null);
  const start = async () => {
    setStatus('starting');
    try {
      setSession(await refinementApi.start(client, { seedText: description.trim() }));
      setStatus('started');
    } catch (error) {
      setStatus(isNotAvailable(error) ? 'unavailable' : 'failed');
    }
  };
  if (status === 'unavailable') {
    return (
      <Screen>
        <NotAvailableState feature={t('mobile.features.wizard')} />
      </Screen>
    );
  }
  return (
    <Screen testID="new-topic-screen">
      <T variant="title">{t('mobile.wizard.prompt')}</T>
      <T variant="muted">{t('mobile.wizard.intro')}</T>
      <Field label={t('mobile.wizard.label')} value={description} onChangeText={setDescription} multiline placeholder={t('mobile.wizard.placeholder')} />
      {status === 'failed' ? <Banner kind="danger">{t('mobile.wizard.failed')}</Banner> : null}
      {status === 'started' ? <Banner kind="success">{t('mobile.wizard.started', { id: sessionId(session) })}</Banner> : null}
      <Button title={t('mobile.wizard.start')} busy={status === 'starting'} disabled={description.trim().length < 3} onPress={() => void start()} />
    </Screen>
  );
}

export function EditionsScreen() {
  const { t } = useTranslation();
  const { client } = useAuth();
  return (
    <ResourceListScreen
      testID="editions-screen"
      load={() => editionsApi.list(client, PAGE)}
      deps={[client]}
      feature={t('mobile.features.editions')}
      empty={{ icon: 'calendar-outline', title: t('mobile.editions.emptyTitle'), body: t('mobile.editions.emptyBody') }}
    />
  );
}
