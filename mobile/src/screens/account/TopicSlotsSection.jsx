import React, { useState } from 'react';
import { AccessibilityInfo, RefreshControl, View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { topicSlotsApi } from '@/api/account';
import { ButtonRow } from '@/components/Chips';
import { ActionError, ResourceView } from '@/components/states';
import { Badge, Banner, Button, Card, IconButton, KeyValue, Screen, T } from '@/components/ui';
import { TOPIC_STATUS_TONES } from '@/lib/enums';
import { moveItem, offersTopicSlots, runsAt } from '@/lib/comparison';
import { formatDateTime, formatNumber } from '@/lib/format';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';

export const TOPIC_SLOTS_HREF = '/account/slots';

/** Read the keep order without failing the screen that asks: null on an older server or any error. */
export function useTopicSlots() {
  const { client } = useAuth();
  return useResource(() => topicSlotsApi.get(client).catch(() => null), [client]);
}

/** The limit, the count, and what being over it means, in words. */
function LimitCard({ slots }) {
  const { t, i18n } = useTranslation();
  const n = (v) => formatNumber(Number(v) || 0, i18n.language);
  return (
    <Card testID="topic-slots-limit">
      <KeyValue label={t('mobile.topicSlots.limit')} value={slots.limit === null || slots.limit === undefined ? t('mobile.plan.unlimited') : n(slots.limit)} />
      <KeyValue label={t('mobile.topicSlots.counted')} value={n(slots.counted)} />
      {slots.limit === 0 ? <T variant="small">{t('mobile.topicSlots.noCustom')}</T> : null}
      {slots.overLimit > 0 ? (
        <Banner kind="warning" title={t('mobile.topicSlots.overTitle', { count: slots.overLimit })} testID="topic-slots-over">
          {t('mobile.topicSlots.overBody')}
        </Banner>
      ) : slots.limit ? (
        <T variant="small">{t('mobile.topicSlots.within')}</T>
      ) : null}
      <T variant="small">{t('mobile.topicSlots.explain')}</T>
    </Card>
  );
}

/**
 * "Topics that keep running" (plan section 8.4, the downgrade resolution flow): the account's custom topics in keep
 * order, most important first. The first `limit` run; the rest keep their history but do not run, and creating,
 * duplicating, or restoring a topic stays blocked until enough are archived or the plan grows. Move up and Move down
 * reorder (each announces the new position), Save stores the order (`PUT /v1.0/api/account/topic-slots`), and Clear
 * returns to creation order. Saving ahead of a downgrade applies when the limit shrinks. Nothing is paused, archived,
 * or deleted here.
 */
export function TopicSlotsSection() {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const resource = useResource(() => topicSlotsApi.get(client), [client]);
  const [order, setOrder] = useState(null);
  const [busy, setBusy] = useState(null);
  const [error, setError] = useState(null);
  const [saved, setSaved] = useState(false);
  const slots = resource.data;
  const topics = (slots && Array.isArray(slots.topics) ? slots.topics : []).slice().sort((a, b) => (a.position || 0) - (b.position || 0));
  const serverOrder = topics.map((x) => x.topicId);
  const ids = order || serverOrder;
  const byId = {};
  for (const x of topics) byId[x.topicId] = x;
  const dirty = ids.join(',') !== serverOrder.join(',');

  const move = (index, delta) => {
    const next = moveItem(ids, index, delta);
    setOrder(next);
    setSaved(false);
    const moved = byId[ids[index]];
    const to = next.indexOf(ids[index]);
    if (moved && typeof AccessibilityInfo.announceForAccessibility === 'function') {
      AccessibilityInfo.announceForAccessibility(t('mobile.topicSlots.moved', { name: moved.name, position: to + 1, total: next.length }));
    }
  };
  const send = async (kind, keep) => {
    setBusy(kind);
    setError(null);
    setSaved(false);
    try {
      const next = await topicSlotsApi.save(client, keep);
      resource.setData(next);
      setOrder(null);
      setSaved(kind);
    } catch (e) {
      setError(e);
    } finally {
      setBusy(null);
    }
  };

  return (
    <Screen testID="topic-slots-section" refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />}>
      <ResourceView resource={resource} feature={t('mobile.topicSlots.title')}>
        {() => (
          <>
            <T variant="muted">{t('mobile.topicSlots.intro')}</T>
            <LimitCard slots={slots} />
            {ids.length === 0 ? <T variant="muted">{t('mobile.topicSlots.empty')}</T> : null}
            <View style={{ gap: spacing.sm }} accessibilityRole="list">
              {ids.map((id, index) => {
                const topic = byId[id];
                if (!topic) return null;
                const runs = runsAt(index, slots.limit);
                const marker = runs ? t('mobile.topicSlots.runs') : t('mobile.topicSlots.paused');
                const status = t('mobile.enums.topicStatus.' + topic.status);
                return (
                  <Card key={id} testID={'topic-slot-' + id} style={{ padding: spacing.md }}>
                    <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
                      <View
                        style={{ flex: 1, gap: 4 }}
                        accessible
                        accessibilityLabel={t('mobile.topicSlots.rowLabel', { position: index + 1, total: ids.length, name: topic.name, status, marker })}
                      >
                        <T style={{ fontWeight: '600' }}>{index + 1 + '. ' + topic.name}</T>
                        <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs }}>
                          <Badge label={marker} tone={runs ? 'success' : 'warning'} />
                          <Badge label={status} tone={TOPIC_STATUS_TONES[topic.status] || 'neutral'} />
                          {topic.chosen && !order ? <Badge label={t('mobile.topicSlots.chosen')} tone="primary" /> : null}
                        </View>
                        <T variant="small">{topic.lastRunUtc ? t('mobile.topics.lastRun', { date: formatDateTime(topic.lastRunUtc, i18n.language) }) : t('mobile.topics.neverRun')}</T>
                      </View>
                      <IconButton icon="arrow-up" accessibilityLabel={t('mobile.topicSlots.moveUp', { name: topic.name })} disabled={Boolean(busy) || index === 0} onPress={() => move(index, -1)} testID={'topic-slot-up-' + id} />
                      <IconButton icon="arrow-down" accessibilityLabel={t('mobile.topicSlots.moveDown', { name: topic.name })} disabled={Boolean(busy) || index === ids.length - 1} onPress={() => move(index, 1)} testID={'topic-slot-down-' + id} />
                    </View>
                  </Card>
                );
              })}
            </View>
            <ActionError error={error} testID="topic-slots-error" />
            {saved ? (
              <Banner kind="success" testID="topic-slots-saved">
                {saved === 'clear' ? t('mobile.topicSlots.cleared') : t('mobile.topicSlots.saved')}
              </Banner>
            ) : null}
            <ButtonRow>
              <Button title={t('mobile.common.save')} busy={busy === 'save'} disabled={Boolean(busy) || !dirty} onPress={() => void send('save', ids)} testID="topic-slots-save" />
              <Button title={t('mobile.topicSlots.clear')} kind="secondary" busy={busy === 'clear'} disabled={Boolean(busy) || (!slots.customized && !dirty)} accessibilityHint={t('mobile.topicSlots.clearHint')} onPress={() => void send('clear', [])} testID="topic-slots-clear" />
            </ButtonRow>
          </>
        )}
      </ResourceView>
    </Screen>
  );
}

/**
 * Account > Plan's entry to the keep order: shown when the account has custom topics and the plan limits them, and
 * stated as a warning when topics are past the limit.
 */
export function TopicSlotsEntry() {
  const { t, i18n } = useTranslation();
  const resource = useTopicSlots();
  const slots = resource.data;
  if (!offersTopicSlots(slots)) return null;
  const n = (v) => formatNumber(Number(v) || 0, i18n.language);
  return (
    <Card testID="plan-topic-slots">
      <T variant="label">{t('mobile.topicSlots.title')}</T>
      <T variant="small">{t('mobile.topicSlots.summary', { counted: n(slots.counted), limit: n(slots.limit) })}</T>
      {slots.overLimit > 0 ? (
        <Banner kind="warning" title={t('mobile.topicSlots.overTitle', { count: slots.overLimit })}>
          {t('mobile.topicSlots.overBody')}
        </Banner>
      ) : null}
      <Button title={t('mobile.topicSlots.open')} icon="reorder-four-outline" kind={slots.overLimit > 0 ? 'primary' : 'secondary'} onPress={() => router.push(TOPIC_SLOTS_HREF)} testID="plan-topic-slots-open" />
    </Card>
  );
}

/** The Topics banner while topics are past the plan's limit (they keep history but do not run). */
export function TopicSlotsBanner() {
  const { t } = useTranslation();
  const resource = useTopicSlots();
  const slots = resource.data;
  if (!slots || !(slots.overLimit > 0)) return null;
  return (
    <Banner kind="warning" title={t('mobile.topicSlots.overTitle', { count: slots.overLimit })} testID="topics-over-limit">
      <View style={{ gap: spacing.sm }}>
        <T variant="small">{t('mobile.topicSlots.overBody')}</T>
        <Button title={t('mobile.topicSlots.open')} icon="reorder-four-outline" onPress={() => router.push(TOPIC_SLOTS_HREF)} testID="topics-over-limit-open" />
      </View>
    </Banner>
  );
}
