import React, { useState } from 'react';
import { View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { feedbackApi } from '@/api/research';
import { ButtonRow, ChipGroup } from '@/components/Chips';
import { WhySheet } from '@/components/feedback/WhySheet';
import { EmptyState, ResourceView } from '@/components/states';
import { Badge, Button, Card, T } from '@/components/ui';
import { FEEDBACK_TARGET_FILTERS, actionKey, actionOf, feedbackTargetHref } from '@/lib/feedback';
import { formatDateTime } from '@/lib/format';
import { FeedbackQueueFullError } from '@/feedback/FeedbackQueue';
import { useAuth } from '@/state/AuthContext';
import { useFeedbackActions, useFeedbackSettled, useQueuedFeedback } from '@/state/OfflineContext';
import { useResource } from '@/state/useResource';
import { spacing } from '@/theme/palette';

/** How many feedback items the history reads per page. */
export const HISTORY_PAGE_SIZE = 50;

/**
 * Save from the history's edit sheet: the same rating with a new reason edits the reason (`PUT /feedback/{id}`); a
 * different rating is a new rating that supersedes the old one (`POST /feedback`), as on the email pages.
 */
export async function saveHistoryEdit(client, item, { action, reason }, actions) {
  const sender = actions || {
    update: (feedback, text) => feedbackApi.update(client, feedback.id, text),
    submit: (fields) => feedbackApi.submit(client, fields),
  };
  if (!action || action === actionOf(item)) return sender.update(item, reason);
  return sender.submit({ targetType: item.targetType, targetId: item.targetId, action, reason });
}

/**
 * Feedback history (plan section 20): every current rating, newest first, with its reason, target, channel, and date,
 * filtered by target type. Edit the reason or the rating, retract it (the earlier rating, if any, becomes current), or
 * open the item. Changes made without a connection go to the offline feedback queue (`useFeedbackActions`), and the
 * item says "Will send when online" until they are sent.
 */
export function FeedbackHistory() {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const [filter, setFilter] = useState('all');
  const [editing, setEditing] = useState(null);
  const [pageSize, setPageSize] = useState(HISTORY_PAGE_SIZE);
  const resource = useResource(
    () => feedbackApi.list(client, { pageNumber: 1, pageSize, sortBy: 'createdUtc', sortDirection: 'Descending', targetType: filter === 'all' ? undefined : filter }),
    [client, filter, pageSize],
  );
  const actions = useFeedbackActions();
  const isQueued = useQueuedFeedback();
  useFeedbackSettled(() => void resource.reload());
  const send = async (fn) => {
    let result;
    try {
      result = await fn();
    } catch (e) {
      if (e instanceof FeedbackQueueFullError) throw new Error(t('mobile.connection.queueFull'));
      throw e;
    }
    // A queued change shows on the item until it is sent; reading the list again now would only fail.
    if (!result || !result.queued) await resource.reload();
  };
  const items = listItems(resource.data);
  const total = resource.data && typeof resource.data.totalRecords === 'number' ? resource.data.totalRecords : items.length;
  return (
    <View style={{ gap: spacing.sm }} testID="feedback-history">
      <ChipGroup
        scroll
        options={FEEDBACK_TARGET_FILTERS.map((f) => ({ value: f, label: f === 'all' ? t('mobile.insights.history.all') : t('mobile.insights.feedback.targets.' + f), testID: 'history-filter-' + f }))}
        value={filter}
        onChange={setFilter}
        accessibilityLabel={t('mobile.common.filter')}
      />
      <ResourceView resource={resource} feature={t('mobile.insights.history.title')} isEmpty={() => items.length === 0} empty={<EmptyState icon="chatbubbles-outline" title={t('mobile.insights.history.emptyTitle')} body={t('mobile.insights.history.emptyBody')} />}>
        {items.map((item) => {
          const action = actionOf(item);
          const href = feedbackTargetHref(item);
          return (
            <Card key={item.id} testID={'history-' + item.id}>
              <View style={{ flexDirection: 'row', gap: spacing.sm, flexWrap: 'wrap' }}>
                <Badge label={t('mobile.insights.feedback.targets.' + item.targetType, { defaultValue: item.targetType })} />
                <Badge label={action ? t('mobile.feedback.actions.' + actionKey(action)) : t('mobile.insights.history.reasonOnly')} tone="primary" />
                <Badge label={t('mobile.insights.feedback.channels.' + item.channel, { defaultValue: item.channel })} />
              </View>
              {item.reason ? <T selectable>{item.reason}</T> : <T variant="small">{t('mobile.insights.history.noReason')}</T>}
              <T variant="small">{formatDateTime(item.createdUtc, i18n.language)}</T>
              {isQueued(item) ? (
                <T variant="small" testID={'history-queued-' + item.id}>
                  {t('mobile.connection.queued')}
                </T>
              ) : null}
              <ButtonRow>
                <Button title={t('mobile.insights.history.edit')} icon="create-outline" kind="secondary" onPress={() => setEditing(item)} testID={'history-edit-' + item.id} />
                {href ? <Button title={t('mobile.insights.history.open')} kind="ghost" onPress={() => router.push(href)} testID={'history-open-' + item.id} /> : null}
              </ButtonRow>
            </Card>
          );
        })}
        {items.length < total ? <Button title={t('mobile.insights.history.more')} kind="secondary" onPress={() => setPageSize((n) => n + HISTORY_PAGE_SIZE)} testID="history-more" /> : null}
      </ResourceView>
      <WhySheet
        visible={Boolean(editing)}
        targetType={editing ? editing.targetType : 'Finding'}
        current={editing}
        onSave={(change) => send(() => saveHistoryEdit(client, editing, change, actions))}
        onRetract={() => send(() => actions.retract(editing))}
        onClose={() => setEditing(null)}
      />
    </View>
  );
}
