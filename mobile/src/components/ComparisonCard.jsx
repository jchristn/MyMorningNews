import React, { useState } from 'react';
import { I18nManager, Pressable, View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { useTranslation } from 'react-i18next';
import { DELTA_TONES, comparisonMode, comparisonState, deltaCounts, sortedDeltas } from '@/lib/comparison';
import { formatDate } from '@/lib/format';
import { useTheme } from '@/theme/ThemeContext';
import { MIN_TOUCH, radius, spacing } from '@/theme/palette';
import { Badge, Card, T } from './ui';

/** One baseline issue: its title and date, tappable while it still exists. */
function BaselineRow({ baseline, onOpen, locale }) {
  const p = useTheme();
  const { t } = useTranslation();
  const available = baseline.available !== false;
  const title = available && baseline.title ? baseline.title : t('mobile.comparison.baselineGone');
  const date = available && baseline.createdUtc ? formatDate(baseline.createdUtc, locale) : null;
  const content = (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm, minHeight: MIN_TOUCH, paddingVertical: spacing.xs }}>
      <Ionicons name={available ? 'newspaper-outline' : 'remove-circle-outline'} size={20} color={available ? p.primary : p.muted} />
      <View style={{ flex: 1, gap: 2 }}>
        <T style={available ? null : { color: p.muted }}>{title}</T>
        {date ? <T variant="small">{date}</T> : null}
      </View>
      {available && onOpen ? <Ionicons name={I18nManager.isRTL ? 'chevron-back' : 'chevron-forward'} size={18} color={p.muted} /> : null}
    </View>
  );
  if (!available || !onOpen) return <View testID={'comparison-baseline-' + baseline.id}>{content}</View>;
  return (
    <Pressable
      testID={'comparison-baseline-' + baseline.id}
      onPress={() => onOpen(baseline.id)}
      accessibilityRole="button"
      accessibilityLabel={t('mobile.comparison.openBaseline', { title: date ? title + ', ' + date : title })}
      style={({ pressed }) => [{ borderRadius: radius.md, paddingHorizontal: spacing.xs }, pressed ? { opacity: 0.6 } : null]}
    >
      {content}
    </Pressable>
  );
}

/** One change: its kind, title, and one-line explanation; tappable when it names current evidence. */
function DeltaRow({ delta, onOpenFinding, onOpenPrediction }) {
  const p = useTheme();
  const { t } = useTranslation();
  const kind = t('mobile.comparison.kinds.' + (DELTA_TONES[delta.kind] ? delta.kind : 'Unavailable'));
  const open = delta.findingId && onOpenFinding ? () => onOpenFinding(delta.findingId) : delta.predictionId && onOpenPrediction ? () => onOpenPrediction(delta.predictionId) : null;
  const content = (
    <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm, minHeight: MIN_TOUCH, paddingVertical: spacing.xs }}>
      <View style={{ flex: 1, gap: 4 }}>
        <Badge label={kind} tone={DELTA_TONES[delta.kind] || 'neutral'} />
        <T>{delta.title}</T>
        {delta.summary ? <T variant="small">{delta.summary}</T> : null}
      </View>
      {open ? <Ionicons name={I18nManager.isRTL ? 'chevron-back' : 'chevron-forward'} size={18} color={p.muted} /> : null}
    </View>
  );
  const label = [kind, delta.title, delta.summary].filter(Boolean).join(', ');
  if (!open) {
    return (
      <View testID={'comparison-delta-' + delta.id} accessible accessibilityLabel={label}>
        {content}
      </View>
    );
  }
  return (
    <Pressable
      testID={'comparison-delta-' + delta.id}
      onPress={open}
      accessibilityRole="button"
      accessibilityLabel={label}
      accessibilityHint={t('mobile.comparison.openEvidence')}
      style={({ pressed }) => [{ borderRadius: radius.md, paddingHorizontal: spacing.xs }, pressed ? { opacity: 0.6 } : null]}
    >
      {content}
    </Pressable>
  );
}

/**
 * "Compared with ..." (plan section 6.2, items 7 and 8): what a briefing or a system briefing issue was compared with
 * and what changed. Collapsed it is one line (the summary sentence the briefing text carries); expanded it lists the
 * baseline issues (`onOpenBaseline(id)`) and the changes labeled New, Changed, Correction, Resolved, Unchanged, and
 * Unavailable (`onOpenFinding(id)` and `onOpenPrediction(id)` when a change names current evidence). A standalone
 * briefing gets a plain note instead of a card.
 */
export function ComparisonCard({ comparison, onOpenBaseline, onOpenFinding, onOpenPrediction, testID = 'comparison' }) {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const [open, setOpen] = useState(false);
  if (!comparison) return null;
  const state = comparisonState(comparison);
  if (state === 'standalone') {
    return (
      <View testID={testID + '-standalone'} style={{ flexDirection: 'row', gap: spacing.sm, alignItems: 'flex-start' }}>
        <Ionicons name="document-outline" size={16} color={p.muted} style={{ marginTop: 2 }} />
        <T variant="small" style={{ flex: 1 }}>
          {comparison.note || t('mobile.comparison.standalone')}
        </T>
      </View>
    );
  }
  const baselines = Array.isArray(comparison.baselines) ? comparison.baselines : [];
  const first = baselines.find((b) => b.available !== false && b.title);
  const headline = comparison.summary || (first ? t('mobile.comparison.comparedWith', { title: first.title }) : t('mobile.comparison.comparedWithEarlier'));
  const deltas = sortedDeltas(comparison);
  const counts = deltaCounts(comparison);
  const { mode, edition } = comparisonMode(comparison);
  const modeLabel = t('mobile.topicHistory.' + (['PreviousIssue', 'RecentIssues', 'SinceDate', 'SpecificIssue'].includes(mode) ? mode : 'PreviousIssue'));
  return (
    <Card testID={testID} style={{ padding: spacing.md, gap: spacing.xs }}>
      <Pressable
        testID={testID + '-toggle'}
        onPress={() => setOpen((v) => !v)}
        accessibilityRole="button"
        accessibilityLabel={headline + (state === 'noChanges' ? ', ' + t('mobile.comparison.noChanges') : '')}
        accessibilityHint={open ? t('mobile.comparison.hideHint') : t('mobile.comparison.showHint')}
        accessibilityState={{ expanded: open }}
        style={({ pressed }) => [{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm, minHeight: MIN_TOUCH }, pressed ? { opacity: 0.6 } : null]}
      >
        <Ionicons name="git-compare-outline" size={20} color={p.primary} />
        <View style={{ flex: 1, gap: 2 }}>
          <T variant="small" testID={testID + '-summary'}>
            {headline}
          </T>
          {state === 'noChanges' ? (
            <T variant="small" style={{ fontWeight: '600', color: p.text }} testID={testID + '-no-changes'}>
              {t('mobile.comparison.noChanges')}
            </T>
          ) : null}
        </View>
        <Ionicons name={open ? 'chevron-up' : 'chevron-down'} size={18} color={p.muted} />
      </Pressable>
      {open ? (
        <View style={{ gap: spacing.sm }} testID={testID + '-details'}>
          <T variant="small">{edition ? t('mobile.comparison.editionMode', { mode: modeLabel }) : t('mobile.comparison.mode', { mode: modeLabel })}</T>
          {comparison.truncated ? <T variant="small">{t('mobile.comparison.truncated')}</T> : null}
          <T variant="label" accessibilityRole="header">
            {t('mobile.comparison.baselines')}
          </T>
          {baselines.length ? baselines.map((b) => <BaselineRow key={b.id} baseline={b} onOpen={onOpenBaseline} locale={i18n.language} />) : <T variant="small">{t('mobile.comparison.noBaselines')}</T>}
          {counts.length ? (
            <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs }} accessible accessibilityLabel={counts.map((c) => t('mobile.comparison.count', { kind: t('mobile.comparison.kinds.' + c.kind), count: c.count })).join(', ')}>
              {counts.map((c) => (
                <Badge key={c.kind} label={t('mobile.comparison.count', { kind: t('mobile.comparison.kinds.' + c.kind), count: c.count })} tone={DELTA_TONES[c.kind]} />
              ))}
            </View>
          ) : null}
          <T variant="label" accessibilityRole="header">
            {t('mobile.comparison.changes')}
          </T>
          {state === 'noChanges' && !deltas.some((d) => d.kind !== 'Unchanged') ? <T variant="small">{t('mobile.comparison.noChangesBody')}</T> : null}
          {deltas.map((d) => (
            <DeltaRow key={d.id} delta={d} onOpenFinding={onOpenFinding} onOpenPrediction={onOpenPrediction} />
          ))}
        </View>
      ) : null}
    </Card>
  );
}
