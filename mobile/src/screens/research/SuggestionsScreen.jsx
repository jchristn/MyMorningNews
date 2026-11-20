import React, { useState } from 'react';
import { RefreshControl, View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { suggestionsApi } from '@/api/research';
import { ButtonRow, ChipGroup } from '@/components/Chips';
import { RatingControl, useFeedbackMap } from '@/components/feedback/RatingControl';
import { EmptyState, ResourceView } from '@/components/states';
import { Banner, Button, ListRow, Screen, T } from '@/components/ui';
import { SUGGESTION_PRIORITY_TONES, SUGGESTION_STATUS_TONES } from '@/lib/enums';
import { formatDate } from '@/lib/format';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { ItemCard } from '../common/DetailScreen';
import { findingHref } from './FindingScreens';

/** Snooze lengths offered on a suggestion, in days. */
export const SNOOZE_DAYS = [1, 7, 30];

export const SUGGESTION_FILTERS = ['New', 'Accepted', 'Snoozed', 'Done', 'Dismissed', 'all'];

/** The SuggestionStatusRequest for an action: accept, done, dismiss, reopen, or snooze for `days`. */
export function suggestionStatusBody(status, days, now = Date.now()) {
  if (status === 'Snoozed') return { status, snoozedUntilUtc: new Date(now + (days || 1) * 86400000).toISOString() };
  return { status };
}

/**
 * One suggestion with its actions: Accept, Snooze (1 day, 1 week, 1 month), Dismiss, and Done, and, with `rating`
 * (`{ current, onChange }`), Useful or Not useful with a reason.
 */
export function SuggestionCard({ suggestion, onChanged, rating }) {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const [busy, setBusy] = useState(null);
  const [snoozing, setSnoozing] = useState(false);
  const [error, setError] = useState(null);
  const s = suggestion;
  const act = async (status, days) => {
    setBusy(status);
    setError(null);
    try {
      onChanged(await suggestionsApi.setStatus(client, s.id, suggestionStatusBody(status, days)));
      setSnoozing(false);
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(null);
    }
  };
  const open = s.status === 'New' || s.status === 'Accepted' || s.status === 'Snoozed';
  return (
    <ItemCard
      title={s.title}
      testID={'suggestion-' + s.id}
      badges={[
        { label: t('mobile.enums.suggestionStatus.' + s.status), tone: SUGGESTION_STATUS_TONES[s.status] || 'neutral' },
        s.priority ? { label: t('mobile.enums.priority.' + s.priority), tone: SUGGESTION_PRIORITY_TONES[s.priority] || 'neutral' } : null,
        s.effort ? { label: t('mobile.enums.effort.' + s.effort), tone: 'neutral' } : null,
      ]}
      lines={[s.description, s.rationale ? t('mobile.suggestions.why', { text: s.rationale }) : null, s.status === 'Snoozed' && s.snoozedUntilUtc ? t('mobile.suggestions.snoozedUntil', { date: formatDate(s.snoozedUntilUtc, i18n.language) }) : null]}
    >
      {(s.findingIds || []).map((fid, i) => (
        <ListRow key={fid} icon="search-outline" title={t('mobile.suggestions.basedOn', { number: i + 1 })} onPress={() => router.push(findingHref(fid))} />
      ))}
      {rating ? <RatingControl targetType="Suggestion" targetId={s.id} title={s.title} current={rating.current} onChange={rating.onChange} /> : null}
      {error ? <Banner kind="danger">{error}</Banner> : null}
      {open ? (
        <ButtonRow>
          {s.status !== 'Accepted' ? <Button title={t('mobile.suggestions.accept')} icon="checkmark" busy={busy === 'Accepted'} onPress={() => void act('Accepted')} testID={'suggestion-accept-' + s.id} /> : null}
          <Button title={t('mobile.suggestions.done')} icon="checkmark-done" kind="secondary" busy={busy === 'Done'} onPress={() => void act('Done')} testID={'suggestion-done-' + s.id} />
          <Button title={t('mobile.suggestions.snooze')} icon="alarm-outline" kind="secondary" onPress={() => setSnoozing((v) => !v)} testID={'suggestion-snooze-' + s.id} />
          <Button title={t('mobile.suggestions.dismiss')} icon="close" kind="ghost" busy={busy === 'Dismissed'} onPress={() => void act('Dismissed')} testID={'suggestion-dismiss-' + s.id} />
        </ButtonRow>
      ) : (
        <Button title={t('mobile.suggestions.reopen')} kind="ghost" icon="arrow-undo-outline" busy={busy === 'New'} onPress={() => void act('New')} testID={'suggestion-reopen-' + s.id} />
      )}
      {snoozing ? (
        <View style={{ gap: spacing.xs }}>
          <T variant="label">{t('mobile.suggestions.snoozeFor')}</T>
          <ButtonRow>
            {SNOOZE_DAYS.map((d) => (
              <Button key={d} title={t('mobile.suggestions.snoozeDays', { count: d })} kind="secondary" busy={busy === 'Snoozed'} onPress={() => void act('Snoozed', d)} testID={'snooze-' + d} />
            ))}
          </ButtonRow>
        </View>
      ) : null}
    </ItemCard>
  );
}

/** Suggestions to dig into next, by status, with their actions inline (no separate detail screen). */
export function SuggestionsScreen({ topicId }) {
  const p = useTheme();
  const { t } = useTranslation();
  const { client } = useAuth();
  const [filter, setFilter] = useState('New');
  const ratings = useFeedbackMap('Suggestion', topicId ? { topicId } : undefined);
  const resource = useResource(() => suggestionsApi.list(client, { pageNumber: 1, pageSize: 100, sortBy: 'createdUtc', sortDirection: 'Descending', status: filter === 'all' ? undefined : filter, topicId }), [client, filter, topicId]);
  const items = listItems(resource.data);
  const replace = (updated) =>
    resource.setData((data) => {
      const next = listItems(data).map((x) => (x.id === updated.id ? updated : x));
      const shown = filter === 'all' ? next : next.filter((x) => x.status === filter);
      return { ...(data && !Array.isArray(data) ? data : {}), items: shown };
    });
  return (
    <Screen testID="suggestions-screen" refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />}>
      <ChipGroup scroll options={SUGGESTION_FILTERS.map((f) => ({ value: f, label: f === 'all' ? t('mobile.suggestions.all') : t('mobile.enums.suggestionStatus.' + f), testID: 'suggestions-filter-' + f }))} value={filter} onChange={setFilter} accessibilityLabel={t('mobile.common.filter')} />
      <ResourceView resource={resource} feature={t('mobile.features.suggestions')} isEmpty={() => items.length === 0} empty={<EmptyState icon="bulb-outline" title={t('mobile.suggestions.emptyTitle')} body={t('mobile.suggestions.emptyBody')} />}>
        <View style={{ gap: spacing.sm }}>
          {items.map((s) => (
            <SuggestionCard key={s.id} suggestion={s} onChanged={replace} rating={ratings.available ? { current: ratings.map[s.id] || null, onChange: (fb) => ratings.set(s.id, fb) } : null} />
          ))}
        </View>
      </ResourceView>
    </Screen>
  );
}
