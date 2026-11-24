import React, { useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { isNotAvailable } from '@/api/errors';
import { feedbackApi } from '@/api/research';
import { ACTION_ICONS, actionKey, actionOf, actionsFor, feedbackByTarget } from '@/lib/feedback';
import { FeedbackQueueFullError } from '@/feedback/FeedbackQueue';
import { useAuth } from '@/state/AuthContext';
import { useFeedbackActions, useFeedbackSettled, usePendingFeedback } from '@/state/OfflineContext';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { MIN_TOUCH, font, radius, spacing } from '@/theme/palette';
import { MAX_FONT_SCALE, T } from '../ui';
import { WhySheet } from './WhySheet';

/** The current rating on one item (`GET /v1.0/api/feedback?targetType=&targetId=`); `data` is null when unrated. */
export function useCurrentFeedback(targetType, targetId) {
  const { client } = useAuth();
  return useResource(() => (targetId ? feedbackApi.current(client, targetType, targetId) : Promise.resolve(null)), [client, targetType, targetId]);
}

/**
 * Current ratings for a list, in one request: `{ map, set(targetId, feedback | null) }`. `query` narrows the list
 * (for example `{ topicId }`); up to 500 current ratings of the type are read.
 */
export function useFeedbackMap(targetType, query) {
  const { client } = useAuth();
  const key = JSON.stringify(query || {});
  const resource = useResource(() => feedbackApi.list(client, { targetType, pageNumber: 1, pageSize: 500, ...(query || {}) }), [client, targetType, key]);
  const map = resource.status === 'ready' ? feedbackByTarget(listItems(resource.data)) : {};
  const set = (targetId, feedback) =>
    resource.setData((data) => {
      const items = listItems(data).filter((f) => f.targetId !== targetId);
      return { objects: feedback ? [...items, feedback] : items };
    });
  return { map, set, available: resource.status !== 'notAvailable' };
}

function RatingChip({ label, icon, selected, onPress, testID, compact, busy }) {
  const p = useTheme();
  return (
    <Pressable
      testID={testID}
      onPress={busy ? undefined : onPress}
      accessibilityRole="radio"
      accessibilityLabel={label}
      accessibilityState={{ selected, checked: selected, busy: Boolean(busy) }}
      style={({ pressed }) => ({
        minHeight: MIN_TOUCH,
        minWidth: MIN_TOUCH,
        paddingHorizontal: compact ? spacing.sm : spacing.md,
        borderRadius: radius.pill,
        borderWidth: 1,
        borderColor: selected ? p.primary : p.border,
        backgroundColor: selected ? p.primarySoft : p.surface,
        flexDirection: 'row',
        alignItems: 'center',
        justifyContent: 'center',
        gap: spacing.xs,
        opacity: pressed || busy ? 0.6 : 1,
      })}
    >
      <Ionicons name={selected ? String(icon).replace('-outline', '') : icon} size={16} color={selected ? p.primary : p.muted} />
      {compact ? null : (
        <Text maxFontSizeMultiplier={MAX_FONT_SCALE} style={{ color: selected ? p.primary : p.text, fontSize: font.small, fontWeight: selected ? '700' : '500' }}>
          {label}
        </Text>
      )}
    </Pressable>
  );
}

/**
 * Inline rating controls (plan section 35.3, "feedback everywhere"): the actions for the target type as chips, the
 * current rating selected, and a "why" button that opens the sheet to add a reason, change, or retract. A tap posts
 * `POST /v1.0/api/feedback` with `channel: "Mobile"` at once and keeps any reason already given. `compact` shows icon
 * chips for list rows (each still labeled for screen readers).
 *
 * `current` is the current Feedback (or null) and `onChange(feedback | null)` receives the new one.
 *
 * Without a connection the rating goes to the offline feedback queue (plan section 35.4): the control shows the
 * queued rating as selected with "Will send when online", and `onChange` runs when the queue has sent it.
 */
export function RatingControl({ targetType, targetId, current, onChange, compact, title, testID }) {
  const p = useTheme();
  const { t } = useTranslation();
  const actions = useFeedbackActions();
  const pending = usePendingFeedback(targetType, targetId);
  const [busy, setBusy] = useState(null);
  const [error, setError] = useState(null);
  const [sheet, setSheet] = useState(false);
  // What the control shows: the queued change on top of the saved rating (a queued retraction shows no rating).
  const shown = !pending ? current : pending.kind === 'retract' ? null : { ...(current || {}), targetType, targetId, reason: pending.reason || '' };
  const selected = pending && pending.kind === 'submit' && pending.action ? pending.action : actionOf(shown);

  useFeedbackSettled(({ item, outcome, feedback }) => {
    if (outcome !== 'sent' || item.targetType !== targetType || item.targetId !== targetId) return;
    if (item.kind === 'retract') onChange(null);
    else if (feedback) onChange(feedback);
  });

  const guarded = async (fn) => {
    try {
      return await fn();
    } catch (e) {
      if (e instanceof FeedbackQueueFullError) throw new Error(t('mobile.connection.queueFull'));
      throw e;
    }
  };

  const submit = async ({ action, reason }) => {
    const result = await guarded(() => actions.submit({ targetType, targetId, action, reason }));
    if (result.queued) return;
    const saved = result.feedback;
    onChange(saved && typeof saved === 'object' ? saved : { targetType, targetId, reason, isCurrent: true });
  };

  const rate = async (action) => {
    setBusy(action);
    setError(null);
    try {
      await submit({ action, reason: shown && shown.reason ? shown.reason : undefined });
    } catch (e) {
      setError(isNotAvailable(e) ? t('mobile.feedback.notAvailable') : e.message || t('mobile.feedback.failed'));
    } finally {
      setBusy(null);
    }
  };

  const retract = async () => {
    const saved = current && current.id ? current : null;
    const result = await guarded(() => actions.retract({ id: saved ? saved.id : null, targetType, targetId }));
    if (saved && !result.queued) onChange(null);
  };

  const id = testID || 'rating-' + targetId;
  return (
    <View testID={id} style={{ gap: spacing.xs }}>
      <ScrollView horizontal showsHorizontalScrollIndicator={false} accessibilityRole="radiogroup" accessibilityLabel={t('mobile.feedback.rateLabel', { title: title || '' })} contentContainerStyle={{ gap: spacing.xs, paddingVertical: 2 }}>
        {actionsFor(targetType).map((action) => (
          <RatingChip
            key={action}
            compact={compact}
            testID={id + '-' + action}
            label={t('mobile.feedback.actions.' + actionKey(action))}
            icon={ACTION_ICONS[action]}
            selected={selected === action}
            busy={busy === action}
            onPress={() => void rate(action)}
          />
        ))}
        <RatingChip compact={compact} testID={id + '-why'} label={shown && shown.reason ? t('mobile.feedback.editWhy') : t('mobile.feedback.tellWhy')} icon="chatbubble-ellipses-outline" selected={Boolean(shown && shown.reason)} onPress={() => setSheet(true)} />
      </ScrollView>
      {shown && shown.reason && !compact ? (
        <T variant="small" numberOfLines={2} testID={id + '-reason'}>
          {t('mobile.feedback.yourReason', { reason: shown.reason })}
        </T>
      ) : null}
      {pending ? (
        <View testID={id + '-queued'} accessibilityLiveRegion="polite" style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.xs }}>
          <Ionicons name="cloud-offline-outline" size={14} color={p.muted} />
          <View style={{ flex: 1, minWidth: 0 }}>
            <T variant="small">{t('mobile.connection.queued')}</T>
          </View>
        </View>
      ) : null}
      {error ? (
        <T variant="small" style={{ color: p.danger }} accessibilityRole="alert">
          {error}
        </T>
      ) : null}
      <WhySheet visible={sheet} targetType={targetType} current={shown} title={title} onSave={submit} onRetract={(current && current.id) || (pending && pending.kind === 'submit') ? retract : null} onClose={() => setSheet(false)} />
    </View>
  );
}
