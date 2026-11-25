import React, { useState } from 'react';
import { View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { enumerateObjects } from '@/api/enumeration';
import { topicReviewsApi } from '@/api/research';
import { ButtonRow } from '@/components/Chips';
import { ActionError } from '@/components/states';
import { Banner, Button, SectionHeader, T } from '@/components/ui';
import { formatDate } from '@/lib/format';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';
import { spacing } from '@/theme/palette';

/** The reviews still waiting for a choice, optionally only the one for `topicId`. */
export function pendingReviews(list, topicId) {
  const items = Array.isArray(list) ? list : [];
  return items.filter((r) => r && typeof r === 'object' && (!r.status || r.status === 'Pending') && (!topicId || r.topicId === topicId));
}

/**
 * Keep-or-delete prompts for the signed-in user's recurring topics that found nothing new for a week
 * (`GET /v1.0/api/topic-reviews`). Each shows the topic, how long it has found nothing, and the date the server
 * deletes it unless it is kept, with Keep (`POST .../keep`) and Delete (`POST .../delete`). Delete asks again and says
 * that the topic's briefings, findings, and audio go with it. Renders nothing when there are no pending reviews, and
 * on a server without the route.
 *
 * - `topicId` shows only that topic's review (the topic workspace).
 * - `heading` adds a section title; `showOpen` adds Open topic (lists, where the topic is not on screen).
 * - `onChanged(review, action)` runs after a Keep or Delete succeeds; `onDeleted(review)` also runs after a Delete.
 * - `refreshKey` reloads the reviews when it changes (pull to refresh on the screen around it).
 */
export function TopicReviewBanner({ topicId, heading, showOpen, onChanged, onDeleted, refreshKey, testID = 'topic-reviews' }) {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const resource = useResource(() => enumerateObjects((query) => topicReviewsApi.list(client, query), { pageSize: 50 }), [client, refreshKey]);
  const [busy, setBusy] = useState(null);
  const [confirming, setConfirming] = useState(null);
  const [error, setError] = useState(null);
  const [notice, setNotice] = useState(null);
  const reviews = resource.status === 'ready' ? pendingReviews(resource.data, topicId) : [];
  const nameOf = (r) => r.topicName || t('mobile.topicReviews.untitled');

  const act = async (review, action) => {
    setBusy(review.id + ':' + action);
    setError(null);
    setNotice(null);
    try {
      if (action === 'keep') await topicReviewsApi.keep(client, review.id);
      else await topicReviewsApi.remove(client, review.id);
      setConfirming(null);
      resource.setData((list) => (Array.isArray(list) ? list.filter((r) => r.id !== review.id) : list));
      setNotice({ action, name: nameOf(review) });
      if (onChanged) onChanged(review, action);
      if (action === 'delete' && onDeleted) onDeleted(review);
    } catch (e) {
      setError(e);
      // A 409 means the review was settled elsewhere (kept on the web, or deleted by the server); show what is left.
      void resource.poll();
    } finally {
      setBusy(null);
    }
  };

  if (!reviews.length && !notice && !error) return null;
  return (
    <View testID={testID} style={{ gap: spacing.sm }}>
      {heading && reviews.length ? <SectionHeader title={t('mobile.topicReviews.heading')} /> : null}
      {reviews.map((r) => {
        const name = nameOf(r);
        const asking = confirming === r.id;
        return (
          <Banner key={r.id} kind="warning" title={t('mobile.topicReviews.title', { topic: name })} testID={'topic-review-' + r.id}>
            <View style={{ gap: spacing.sm }}>
              <T variant="small">{t('mobile.topicReviews.stale', { count: Number(r.staleDays) || 0 })}</T>
              <T variant="small">{t('mobile.topicReviews.deleteOn', { date: formatDate(r.deleteAfterUtc, i18n.language) })}</T>
              {asking ? (
                <T variant="small" testID={'topic-review-confirm-' + r.id} accessibilityRole="alert">
                  {t('mobile.topicReviews.confirmBody', { topic: name })}
                </T>
              ) : null}
              <ButtonRow>
                {asking ? (
                  <>
                    <Button title={t('mobile.topicReviews.confirmDelete')} kind="danger" icon="trash-outline" busy={busy === r.id + ':delete'} disabled={Boolean(busy)} onPress={() => void act(r, 'delete')} accessibilityLabel={t('mobile.topicReviews.deleteLabel', { topic: name })} testID={'topic-review-confirm-delete-' + r.id} />
                    <Button title={t('mobile.common.cancel')} kind="ghost" disabled={Boolean(busy)} onPress={() => setConfirming(null)} testID={'topic-review-cancel-' + r.id} />
                  </>
                ) : (
                  <>
                    <Button title={t('mobile.topicReviews.keep')} icon="checkmark-outline" busy={busy === r.id + ':keep'} disabled={Boolean(busy)} onPress={() => void act(r, 'keep')} accessibilityLabel={t('mobile.topicReviews.keepLabel', { topic: name })} testID={'topic-review-keep-' + r.id} />
                    <Button title={t('mobile.topicReviews.delete')} kind="secondary" icon="trash-outline" disabled={Boolean(busy)} onPress={() => setConfirming(r.id)} accessibilityLabel={t('mobile.topicReviews.deleteLabel', { topic: name })} testID={'topic-review-delete-' + r.id} />
                    {showOpen ? <Button title={t('mobile.topicReviews.open')} kind="ghost" onPress={() => router.push('/topics/' + encodeURIComponent(r.topicId))} accessibilityLabel={t('mobile.topicReviews.openLabel', { topic: name })} testID={'topic-review-open-' + r.id} /> : null}
                  </>
                )}
              </ButtonRow>
            </View>
          </Banner>
        );
      })}
      {notice ? (
        <Banner kind="success" testID="topic-review-notice">
          {t(notice.action === 'keep' ? 'mobile.topicReviews.kept' : 'mobile.topicReviews.deleted', { topic: notice.name })}
        </Banner>
      ) : null}
      <ActionError error={error} testID="topic-review-error" />
    </View>
  );
}
