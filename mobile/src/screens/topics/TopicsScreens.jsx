import React, { useState } from 'react';
import { RefreshControl, View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { planApi } from '@/api/account';
import { listItems } from '@/api/client';
import { runsApi, schedulesApi, sourcesApi, topicsApi } from '@/api/research';
import { ButtonRow, ChipGroup } from '@/components/Chips';
import { openLink } from '@/components/LegalFooter';
import { Markdown } from '@/components/Markdown';
import { ScheduleEditor, ScheduleSummary } from '@/components/ScheduleEditor';
import { ActionError, EmptyState, ResourceView } from '@/components/states';
import { Badge, Banner, Button, Card, ListRow, Screen, SectionHeader, SwitchRow, T } from '@/components/ui';
import { costLabel, costState } from '@/lib/costs';
import { ACTIVE_RUN_STATUSES, DIMENSION_WEIGHT_TONES, RUN_STATUS_TONES, TOPIC_STATUS_TONES } from '@/lib/enums';
import { formatDate, formatDateTime } from '@/lib/format';
import { isSafeUrl } from '@/lib/markdown';
import { useAuth } from '@/state/AuthContext';
import { usePolling } from '@/state/usePolling';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { DetailHeader, ItemCard } from '../common/DetailScreen';
import { ResourceListScreen } from '../common/ResourceListScreen';
import { FindingsList } from '../research/FindingScreens';
import { TopicHistory } from './TopicHistory';

export const topicHref = (id) => '/topics/' + encodeURIComponent(id);
export const runHref = (id) => '/topics/runs/' + encodeURIComponent(id);
export const topicFindingHref = (id) => '/topics/findings/' + encodeURIComponent(id);
export const wizardHref = ({ sessionId, topicId } = {}) => '/topics/new' + (sessionId ? '?session=' + encodeURIComponent(sessionId) : topicId ? '?topicId=' + encodeURIComponent(topicId) : '');

/** Workspace tabs on mobile (dashboard `/topics/:id`): overview, findings, runs, sources, and schedule. */
export const WORKSPACE_TABS = ['overview', 'findings', 'runs', 'sources', 'schedule'];

export function TopicsScreen() {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const plan = useResource(() => planApi.get(client), [client]);
  const customAllowed = plan.data?.features?.['customResearch.enabled'] !== 'false';
  return (
    <ResourceListScreen
      testID="topics-screen"
      load={() => topicsApi.list(client, { pageNumber: 1, pageSize: 100, sortBy: 'name', sortDirection: 'Ascending', isOneTime: false })}
      deps={[client]}
      feature={t('mobile.features.topics')}
      header={
        <ButtonRow>
          <Button title={t('mobile.catalog.title')} icon="library-outline" kind="secondary" onPress={() => router.push('/today/catalog')} />
          <Button title={t('mobile.topics.new')} disabled={!customAllowed} icon="add" onPress={() => router.push('/topics/new')} style={{ flexGrow: 1 }} testID="new-topic" />
          <Button title={t('mobile.editions.title')} icon="calendar-outline" kind="secondary" onPress={() => router.push('/topics/editions')} style={{ flexGrow: 1 }} testID="open-editions" />
        </ButtonRow>
      }
      empty={{ icon: 'list-outline', title: t('mobile.topics.emptyTitle'), body: t('mobile.topics.emptyBody'), actionLabel: t('mobile.topics.new'), onAction: () => router.push('/topics/new') }}
      detailHref={topicHref}
      renderDetail={(id) => <TopicWorkspace key={id} id={id} />}
      placeholder={t('mobile.topics.select')}
      renderRow={(topic, { selected, onPress }) => (
        <ItemCard
          title={topic.name}
          selected={selected}
          onPress={onPress}
          testID={'topic-row-' + topic.id}
          badges={[{ label: t('mobile.enums.topicStatus.' + topic.status), tone: TOPIC_STATUS_TONES[topic.status] || 'neutral' }]}
          lines={[topic.description, topic.lastRunUtc ? t('mobile.topics.lastRun', { date: formatDateTime(topic.lastRunUtc, i18n.language) }) : t('mobile.topics.neverRun')]}
        />
      )}
    />
  );
}

/** The topic workspace: a header with the topic's name and status, the tabs, and the chosen tab below. */
export function TopicWorkspace({ id }) {
  const p = useTheme();
  const { t } = useTranslation();
  const { client } = useAuth();
  const resource = useResource(() => topicsApi.get(client, id), [client, id]);
  const [tab, setTab] = useState('overview');
  if (!resource.data) {
    return (
      <Screen testID="topic-workspace">
        <ResourceView resource={resource} feature={t('mobile.features.topics')}>
          {() => null}
        </ResourceView>
      </Screen>
    );
  }
  const topic = resource.data.topic;
  return (
    <View testID="topic-workspace" style={{ flex: 1, backgroundColor: p.bg }}>
      <View style={{ paddingHorizontal: spacing.lg, paddingTop: spacing.md, gap: spacing.sm }}>
        <DetailHeader title={topic.name} badges={[{ label: t('mobile.enums.topicStatus.' + topic.status), tone: TOPIC_STATUS_TONES[topic.status] || 'neutral' }]} />
        <ChipGroup scroll options={WORKSPACE_TABS.map((k) => ({ value: k, label: t('mobile.topics.tabs.' + k), testID: 'topic-tab-' + k }))} value={tab} onChange={setTab} accessibilityLabel={t('mobile.topics.workspace')} />
      </View>
      {tab === 'overview' ? <TopicOverview resource={resource} /> : null}
      {tab === 'findings' ? <FindingsList topicId={topic.id} embedded hrefFor={topicFindingHref} testID="topic-findings" /> : null}
      {tab === 'runs' ? <TopicRuns topicId={topic.id} /> : null}
      {tab === 'sources' ? <TopicSources topicId={topic.id} /> : null}
      {tab === 'schedule' ? <TopicSchedule topic={topic} onTopicUpdated={(updated) => resource.setData({ ...resource.data, topic: updated })} /> : null}
    </View>
  );
}

/** Overview: Run now, dry run, pause or resume, refine; the brief, dimensions, and constraints. */
function TopicOverview({ resource }) {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const [busy, setBusy] = useState(null);
  const [error, setError] = useState(null);
  const [started, setStarted] = useState(null);
  const detail = resource.data;
  const topic = detail.topic;

  const act = async (kind, work) => {
    setBusy(kind);
    setError(null);
    try {
      return await work();
    } catch (e) {
      setError(e);
      return null;
    } finally {
      setBusy(null);
    }
  };
  const run = (dryRun) =>
    act(dryRun ? 'dry' : 'run', async () => {
      const queued = await topicsApi.run(client, topic.id, { dryRun });
      setStarted(queued);
      void resource.poll();
    });
  const toggle = () =>
    act('pause', async () => {
      const updated = topic.status === 'Paused' || topic.status === 'Archived' ? await topicsApi.resume(client, topic.id) : await topicsApi.pause(client, topic.id);
      resource.setData({ ...detail, topic: updated && updated.id ? updated : topic });
    });
  const refine = () =>
    act('refine', async () => {
      const state = await topicsApi.refine(client, topic.id);
      router.push(wizardHref({ sessionId: state.session.id }));
    });
  const draft = topic.status === 'Draft';
  const constraints = detail.constraints || [];

  return (
    <Screen testID="topic-overview" refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />}>
      {draft ? (
        <Banner kind="info" title={t('mobile.topics.draftTitle')}>
          <View style={{ gap: spacing.sm }}>
            <T variant="small">{t('mobile.topics.draftBody')}</T>
            <Button title={t('mobile.topics.continueSetup')} icon="sparkles-outline" onPress={() => router.push(wizardHref({ topicId: topic.id }))} testID="topic-continue" />
          </View>
        </Banner>
      ) : null}
      <ButtonRow>
        <Button title={t('mobile.topics.runNow')} icon="play-outline" busy={busy === 'run'} disabled={draft} onPress={() => void run(false)} testID="topic-run" />
        <Button title={t('mobile.topics.dryRun')} icon="flask-outline" kind="secondary" busy={busy === 'dry'} disabled={draft} onPress={() => void run(true)} testID="topic-dry-run" />
        {!draft ? (
          <Button
            title={topic.status === 'Active' ? t('mobile.topics.pause') : t('mobile.topics.resume')}
            icon={topic.status === 'Active' ? 'pause-outline' : 'play-skip-forward-outline'}
            kind="secondary"
            busy={busy === 'pause'}
            onPress={() => void toggle()}
            testID="topic-pause"
          />
        ) : null}
        {!draft ? <Button title={t('mobile.topics.refine')} icon="color-wand-outline" kind="secondary" busy={busy === 'refine'} onPress={() => void refine()} testID="topic-refine" /> : null}
      </ButtonRow>
      {started ? (
        <Banner kind="success" title={started.isDryRun ? t('mobile.topics.dryRunStarted') : t('mobile.topics.runStarted')} testID="run-started">
          <Button title={t('mobile.topics.openRun')} kind="secondary" onPress={() => router.push(runHref(started.id))} testID="open-started-run" />
        </Banner>
      ) : null}
      <ActionError error={error} />
      <Card>
        <T variant="small">{[topic.lastRunUtc ? t('mobile.topics.lastRun', { date: formatDateTime(topic.lastRunUtc, i18n.language) }) : t('mobile.topics.neverRun'), t('mobile.enums.audience.' + topic.audienceLevel), t('mobile.enums.horizon.' + topic.horizon)].join(' · ')}</T>
        {topic.brief ? <Markdown text={topic.brief} testID="topic-brief" /> : <T variant="muted">{topic.description || t('mobile.topics.noBrief')}</T>}
      </Card>
      <TopicHistory key={topic.id + topic.lastUpdateUtc} topic={topic} onSaved={(updated) => resource.setData({ ...detail, topic: updated })} />
      <SectionHeader title={t('mobile.topics.dimensions', { count: (detail.dimensions || []).length })} />
      {(detail.dimensions || []).length ? (
        <Card>
          {detail.dimensions.map((d) => (
            <View key={d.id} style={{ gap: 2, paddingVertical: spacing.xs }}>
              <View style={{ flexDirection: 'row', gap: spacing.sm, alignItems: 'center', flexWrap: 'wrap' }}>
                <T style={{ fontWeight: '600', flexShrink: 1 }}>{d.name}</T>
                <Badge label={t('mobile.enums.weight.' + d.weight)} tone={DIMENSION_WEIGHT_TONES[d.weight] || 'neutral'} />
              </View>
              {d.description ? <T variant="small">{d.description}</T> : null}
            </View>
          ))}
        </Card>
      ) : (
        <T variant="muted">{t('mobile.topics.noDimensions')}</T>
      )}
      {constraints.length ? (
        <>
          <SectionHeader title={t('mobile.topics.constraints')} />
          <Card>
            {constraints.map((c) => (
              <ListRow key={c.id} title={c.value} detail={t('mobile.enums.constraint.' + c.kind)} />
            ))}
          </Card>
        </>
      ) : null}
    </Screen>
  );
}

/** A topic's runs, newest first; refreshes while any is queued or running. */
function TopicRuns({ topicId }) {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const resource = useResource(() => runsApi.list(client, { topicId, pageNumber: 1, pageSize: 50, sortBy: 'queuedUtc', sortDirection: 'Descending' }), [client, topicId]);
  const runs = listItems(resource.data);
  usePolling(resource.poll, runs.some((r) => ACTIVE_RUN_STATUSES.includes(r.status)));
  return (
    <Screen testID="topic-runs" refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />}>
      <ResourceView resource={resource} feature={t('mobile.features.runs')} isEmpty={() => runs.length === 0} empty={<EmptyState icon="git-network-outline" title={t('mobile.runs.emptyTitle')} body={t('mobile.runs.emptyBody')} />}>
        <View style={{ gap: spacing.sm }}>
          {runs.map((r) => (
            <ItemCard
              key={r.id}
              title={formatDateTime(r.startedUtc || r.queuedUtc, i18n.language)}
              onPress={() => router.push(runHref(r.id))}
              testID={'run-row-' + r.id}
              badges={[
                { label: t('mobile.enums.runStatus.' + r.status), tone: RUN_STATUS_TONES[r.status] || 'neutral' },
                { label: t('mobile.enums.trigger.' + r.trigger), tone: 'neutral' },
                r.isDryRun ? { label: t('mobile.runs.dryRun'), tone: 'warning' } : null,
              ]}
              lines={[t('mobile.runs.counts', { findings: r.newFindingCount || 0, sources: r.sourceCount || 0 }), ACTIVE_RUN_STATUSES.includes(r.status) && costState(r) === 'none' ? null : t('mobile.runs.rowCost', { cost: costLabel(r, t, i18n.language) }), r.errorMessage || r.skippedReason || r.summary]}
            />
          ))}
        </View>
      </ResourceView>
    </Screen>
  );
}

/** Sources fetched for a topic: title, domain, and when; tapping opens the page in the browser. */
function TopicSources({ topicId }) {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const resource = useResource(() => sourcesApi.list(client, { topicId, pageNumber: 1, pageSize: 100 }), [client, topicId]);
  const sources = listItems(resource.data);
  return (
    <Screen testID="topic-sources" refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />}>
      <ResourceView resource={resource} feature={t('mobile.features.sources')} isEmpty={() => sources.length === 0} empty={<EmptyState icon="globe-outline" title={t('mobile.sources.emptyTitle')} body={t('mobile.sources.emptyBody')} />}>
        <Card>
          {sources.map((s) => (
            <ListRow
              key={s.id}
              icon="globe-outline"
              title={s.title || s.domain || s.canonicalUrl}
              detail={[s.domain, s.publishedUtc ? formatDate(s.publishedUtc, i18n.language) : null, t('mobile.sources.seen', { count: s.timesSeen || 1 })].filter(Boolean).join(' · ')}
              onPress={isSafeUrl(s.canonicalUrl) ? () => void openLink(s.canonicalUrl) : undefined}
              testID={'source-' + s.id}
            />
          ))}
        </Card>
      </ResourceView>
    </Screen>
  );
}

/**
 * Automatic audio for a topic: when on, every briefing from the topic's research runs is also made into audio. Saved
 * on the topic right away; a failed save puts the switch back and says why.
 */
export function TopicAudioSwitch({ topic, onTopicUpdated }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [value, setValue] = useState(Boolean(topic.audioEnabled));
  const change = async (next) => {
    setBusy(true);
    setError(null);
    setValue(next);
    try {
      const updated = await topicsApi.update(client, topic.id, { audioEnabled: next });
      if (updated && updated.id && onTopicUpdated) onTopicUpdated(updated);
    } catch (e) {
      setValue(!next);
      setError(e);
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card testID="topic-audio">
      <SwitchRow title={t('mobile.topics.audio.label')} detail={t('mobile.topics.audio.help')} value={value} disabled={busy} onValueChange={(v) => void change(v)} testID="topic-audio-switch" />
      <ActionError error={error} />
    </Card>
  );
}

/** A topic's research schedules (summaries, edit, add, and delete) and whether its briefings are made into audio. */
export function TopicSchedule({ topic, onTopicUpdated }) {
  const p = useTheme();
  const { t } = useTranslation();
  const { client } = useAuth();
  const resource = useResource(() => schedulesApi.list(client, { ownerType: 'Topic', ownerId: topic.id, pageNumber: 1, pageSize: 20 }), [client, topic.id]);
  const [editing, setEditing] = useState(null);
  const schedules = listItems(resource.data);
  const done = () => {
    setEditing(null);
    void resource.poll();
  };
  return (
    <Screen testID="topic-schedule" refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />}>
      <T variant="muted">{t('mobile.topics.scheduleIntro')}</T>
      <ResourceView resource={resource} feature={t('mobile.features.schedules')}>
        {() => (
          <>
            {schedules.map((s) =>
              editing === s.id ? (
                <ScheduleEditor key={s.id} schedule={s} onSaved={done} onRemoved={done} onCancel={() => setEditing(null)} />
              ) : (
                <ScheduleSummary key={s.id} schedule={s} onPress={() => setEditing(s.id)} testID={'schedule-' + s.id} />
              ),
            )}
            {schedules.length === 0 && editing !== 'new' ? <T variant="muted">{t('mobile.topics.noSchedule')}</T> : null}
            {editing === 'new' ? (
              <ScheduleEditor owner={{ ownerType: 'Topic', ownerId: topic.id }} onSaved={done} onCancel={() => setEditing(null)} />
            ) : (
              <Button title={t('mobile.topics.addSchedule')} icon="add" kind="secondary" onPress={() => setEditing('new')} testID="add-schedule" />
            )}
          </>
        )}
      </ResourceView>
      <TopicAudioSwitch topic={topic} onTopicUpdated={onTopicUpdated} />
    </Screen>
  );
}
