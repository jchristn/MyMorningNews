import React, { useState } from 'react';
import { Pressable, ScrollView, Text, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { isNotAvailable } from '@/api/errors';
import { feedbackApi } from '@/api/research';
import { ACTION_ICONS, actionKey, actionOf, actionsFor, feedbackByTarget } from '@/lib/feedback';
import { useAuth } from '@/state/AuthContext';
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
      return { items: feedback ? [...items, feedback] : items };
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
 */
export function RatingControl({ targetType, targetId, current, onChange, compact, title, testID }) {
  const p = useTheme();
  const { t } = useTranslation();
  const { client } = useAuth();
  const [busy, setBusy] = useState(null);
  const [error, setError] = useState(null);
  const [sheet, setSheet] = useState(false);
  const selected = actionOf(current);

  const submit = async ({ action, reason }) => {
    const saved = await feedbackApi.submit(client, { targetType, targetId, action, reason });
    onChange(saved && typeof saved === 'object' ? saved : { targetType, targetId, reason, isCurrent: true });
  };

  const rate = async (action) => {
    setBusy(action);
    setError(null);
    try {
      await submit({ action, reason: current && current.reason ? current.reason : undefined });
    } catch (e) {
      setError(isNotAvailable(e) ? t('mobile.feedback.notAvailable') : e.message || t('mobile.feedback.failed'));
    } finally {
      setBusy(null);
    }
  };

  const retract = async () => {
    await feedbackApi.retract(client, current.id);
    onChange(null);
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
        <RatingChip compact={compact} testID={id + '-why'} label={current && current.reason ? t('mobile.feedback.editWhy') : t('mobile.feedback.tellWhy')} icon="chatbubble-ellipses-outline" selected={Boolean(current && current.reason)} onPress={() => setSheet(true)} />
      </ScrollView>
      {current && current.reason && !compact ? (
        <T variant="small" numberOfLines={2} testID={id + '-reason'}>
          {t('mobile.feedback.yourReason', { reason: current.reason })}
        </T>
      ) : null}
      {error ? (
        <T variant="small" style={{ color: p.danger }} accessibilityRole="alert">
          {error}
        </T>
      ) : null}
      <WhySheet visible={sheet} targetType={targetType} current={current} title={title} onSave={submit} onRetract={current && current.id ? retract : null} onClose={() => setSheet(false)} />
    </View>
  );
}
