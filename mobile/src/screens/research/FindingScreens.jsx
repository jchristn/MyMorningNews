import React, { useState } from 'react';
import { View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { findingsApi } from '@/api/research';
import { HorizontalBarChart } from '@/charts/HorizontalBarChart';
import { ChipGroup } from '@/components/Chips';
import { FeedbackBar } from '@/components/FeedbackBar';
import { RatingControl, useFeedbackMap } from '@/components/feedback/RatingControl';
import { openLink } from '@/components/LegalFooter';
import { Badge, Button, Card, KeyValue, SectionHeader, T } from '@/components/ui';
import { EVIDENCE_ROLE_TONES, FINDING_TIERS, FINDING_TIER_TONES, SCORE_COMPONENTS } from '@/lib/enums';
import { formatDate, formatDateTime, formatNumber } from '@/lib/format';
import { isSafeUrl } from '@/lib/markdown';
import { useAuth } from '@/state/AuthContext';
import { useTheme } from '@/theme/ThemeContext';
import { radius, spacing } from '@/theme/palette';
import { DetailHeader, DetailScreen, ItemCard } from '../common/DetailScreen';
import { ResourceListScreen } from '../common/ResourceListScreen';

export const findingHref = (id) => '/today/findings/' + encodeURIComponent(id);

const MIN_SCORES = [0, 40, 60, 80];

/** The findings query for the filters: tier (or all above the threshold), minimum score, and an optional topic or run. */
export function findingsQuery({ tier, minScore, topicId, runId } = {}) {
  return { pageNumber: 1, pageSize: 50, sortBy: 'relevanceScore', sortDirection: 'Descending', tier: tier && tier !== 'all' ? tier : undefined, minScore: minScore > 0 ? minScore : undefined, topicId, runId };
}

/**
 * One finding row: title, tier, score, category, and the summary. With `rating` (`{ current, onChange }`) the inline
 * rating chips sit under the card (outside its button, so screen readers reach each chip).
 */
export function FindingRow({ finding, onPress, selected, rating }) {
  const { t, i18n } = useTranslation();
  const card = (
    <ItemCard
      title={finding.title}
      onPress={onPress}
      selected={selected}
      testID={'finding-row-' + finding.id}
      accessibilityLabel={finding.title + ', ' + t('mobile.findings.score', { score: formatNumber(finding.relevanceScore, i18n.language, { maximumFractionDigits: 0 }) })}
      badges={[
        { label: t('mobile.enums.tier.' + finding.tier), tone: FINDING_TIER_TONES[finding.tier] || 'neutral' },
        { label: t('mobile.findings.score', { score: formatNumber(finding.relevanceScore, i18n.language, { maximumFractionDigits: 0 }) }), tone: 'neutral' },
        finding.category ? { label: t('mobile.enums.category.' + finding.category), tone: 'neutral' } : null,
      ]}
      lines={[finding.summary, formatDate(finding.publishedUtc || finding.createdUtc, i18n.language)]}
    />
  );
  if (!rating) return card;
  return (
    <View style={{ gap: spacing.xs }}>
      {card}
      <RatingControl compact targetType="Finding" targetId={finding.id} title={finding.title} current={rating.current} onChange={rating.onChange} />
    </View>
  );
}

/** Findings across topics (or one topic or run), ranked, with tier and minimum score filters. */
export function FindingsList({ topicId, runId, testID = 'findings-screen', embedded, hrefFor = findingHref }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  const [tier, setTier] = useState('all');
  const [minScore, setMinScore] = useState(0);
  const ratings = useFeedbackMap('Finding', topicId ? { topicId } : undefined);
  const filters = (
    <View style={{ gap: spacing.sm }}>
      <ChipGroup scroll options={['all', ...FINDING_TIERS].map((v) => ({ value: v, label: v === 'all' ? t('mobile.findings.allTiers') : t('mobile.enums.tier.' + v), testID: 'tier-' + v }))} value={tier} onChange={setTier} accessibilityLabel={t('mobile.findings.tier')} />
      <ChipGroup scroll options={MIN_SCORES.map((v) => ({ value: v, label: v === 0 ? t('mobile.findings.anyScore') : t('mobile.findings.minScore', { score: v }), testID: 'min-score-' + v }))} value={minScore} onChange={setMinScore} accessibilityLabel={t('mobile.findings.minScoreLabel')} />
    </View>
  );
  return (
    <ResourceListScreen
      testID={testID}
      load={() => findingsApi.list(client, findingsQuery({ tier, minScore, topicId, runId }))}
      deps={[client, tier, minScore, topicId, runId]}
      feature={t('mobile.features.findings')}
      header={filters}
      empty={{ icon: 'search-outline', title: t('mobile.findings.emptyTitle'), body: t('mobile.findings.emptyBody') }}
      detailHref={hrefFor}
      renderDetail={embedded ? undefined : (id) => <FindingDetail key={id} id={id} />}
      placeholder={t('mobile.findings.select')}
      renderRow={(f, { selected, onPress }) => (
        <FindingRow finding={f} selected={selected} onPress={onPress} rating={ratings.available ? { current: ratings.map[f.id] || null, onChange: (fb) => ratings.set(f.id, fb) } : null} />
      )}
    />
  );
}

export function FindingsScreen() {
  return <FindingsList />;
}

/** One quote with its source: the role, the quoted text in its context, and the source link. */
export function EvidenceQuote({ view }) {
  const p = useTheme();
  const { t, i18n } = useTranslation();
  const e = view.evidence || {};
  const role = view.role || 'Supports';
  return (
    <Card testID={'evidence-' + e.id}>
      <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs }}>
        <Badge label={t('mobile.enums.evidenceRole.' + role)} tone={EVIDENCE_ROLE_TONES[role] || 'neutral'} />
        {e.verified === false ? <Badge label={t('mobile.evidence.unverified')} tone="warning" /> : null}
      </View>
      <View style={{ borderStartWidth: 3, borderColor: p.primary, paddingStart: spacing.md, backgroundColor: p.surfaceAlt, borderRadius: radius.sm, paddingVertical: spacing.sm }}>
        <T selectable style={{ fontStyle: 'italic' }}>
          {'"' + e.quote + '"'}
        </T>
      </View>
      {e.contextText && e.contextText !== e.quote ? (
        <T variant="small" selectable numberOfLines={6}>
          {e.contextText}
        </T>
      ) : null}
      <T style={{ fontWeight: '600' }}>{view.sourceTitle || view.sourceDomain || view.sourceUrl || t('mobile.evidence.source')}</T>
      <T variant="small">{[view.sourceDomain, view.sourcePublishedUtc ? formatDate(view.sourcePublishedUtc, i18n.language) : null].filter(Boolean).join(' · ')}</T>
      {isSafeUrl(view.sourceUrl) ? <Button title={t('mobile.evidence.openSource')} kind="ghost" icon="open-outline" onPress={() => void openLink(view.sourceUrl)} accessibilityHint={view.sourceUrl} /> : null}
    </Card>
  );
}

/** Score rows for the relevance breakdown chart (plan section 17). */
export function scoreRows(finding, t) {
  return SCORE_COMPONENTS.map((c) => ({ label: t('mobile.findings.components.' + c.key), value: Number(finding[c.field]) || 0 }));
}

/** A finding: tier and score, why it matters, the score breakdown, the evidence quotes with sources, and feedback. */
export function FindingDetail({ id }) {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  return (
    <DetailScreen testID="finding-detail" load={() => findingsApi.get(client, id)} deps={[client, id]} feature={t('mobile.features.findings')}>
      {(detail) => {
        const f = detail.finding;
        const score = formatNumber(f.relevanceScore, i18n.language, { maximumFractionDigits: 0 });
        const evidence = (detail.evidence || []).filter((v) => v && v.evidence && v.evidence.verified !== false);
        return (
          <>
            <DetailHeader
              title={f.title}
              subtitle={[detail.topicName, detail.dimension ? detail.dimension.name : null].filter(Boolean).join(' · ')}
              badges={[
                { label: t('mobile.enums.tier.' + f.tier), tone: FINDING_TIER_TONES[f.tier] || 'neutral' },
                { label: t('mobile.findings.score', { score }), tone: 'neutral' },
                f.category ? { label: t('mobile.enums.category.' + f.category), tone: 'neutral' } : null,
                f.isDuplicate ? { label: t('mobile.findings.duplicate'), tone: 'warning' } : null,
              ]}
            />
            {f.summary ? <T selectable>{f.summary}</T> : null}
            {f.whyItMatters ? (
              <Card>
                <T variant="label">{t('mobile.findings.whyItMatters')}</T>
                <T selectable>{f.whyItMatters}</T>
              </Card>
            ) : null}
            <SectionHeader title={t('mobile.findings.breakdown')} />
            <Card>
              <HorizontalBarChart testID="score-chart" rows={scoreRows(f, t)} formatValue={(n) => formatNumber(n, i18n.language, { maximumFractionDigits: 0 })} accessibilityLabel={t('mobile.findings.breakdown')} />
              {f.relevanceRationale ? <T variant="small">{f.relevanceRationale}</T> : null}
            </Card>
            <SectionHeader title={t('mobile.evidence.title', { count: evidence.length })} />
            {evidence.length ? evidence.map((v) => <EvidenceQuote key={v.evidence.id} view={v} />) : <T variant="muted">{t('mobile.evidence.none')}</T>}
            <Card>
              {f.publishedUtc ? <KeyValue label={t('mobile.findings.published')} value={formatDate(f.publishedUtc, i18n.language)} /> : null}
              <KeyValue label={t('mobile.findings.found')} value={formatDateTime(f.createdUtc, i18n.language)} />
              <KeyValue label={t('mobile.findings.confidence')} value={formatNumber(f.confidence, i18n.language, { style: 'percent', maximumFractionDigits: 0 })} />
            </Card>
            {f.runId ? <Button title={t('mobile.findings.openRun')} kind="secondary" icon="git-network-outline" onPress={() => router.push('/topics/runs/' + encodeURIComponent(f.runId))} /> : null}
            {f.isUpdateOfFindingId ? <Button title={t('mobile.findings.openEarlier')} kind="ghost" onPress={() => router.push(findingHref(f.isUpdateOfFindingId))} /> : null}
            <FeedbackBar targetType="Finding" targetId={f.id} title={f.title} />
          </>
        );
      }}
    </DetailScreen>
  );
}
