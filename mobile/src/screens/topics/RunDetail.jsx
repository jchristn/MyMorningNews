import React, { useState } from 'react';
import { View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { findingsApi, runsApi } from '@/api/research';
import { HorizontalBarChart } from '@/charts/HorizontalBarChart';
import { ActionError } from '@/components/states';
import { Badge, Banner, Button, Card, KeyValue, ListRow, SectionHeader, T } from '@/components/ui';
import { ACTIVE_RUN_STATUSES, RUN_STATUS_TONES, STAGE_STATUS_TONES } from '@/lib/enums';
import { formatCompact, formatCost, formatDateTime, formatNumber } from '@/lib/format';
import { useAuth } from '@/state/AuthContext';
import { usePolling } from '@/state/usePolling';
import { spacing } from '@/theme/palette';
import { DetailHeader, DetailScreen } from '../common/DetailScreen';

/** Milliseconds as "850 ms", "4.2 s", or "3 min 10 s". */
export function formatDuration(ms, locale) {
  const n = Number(ms) || 0;
  if (n < 1000) return formatNumber(n, locale, { maximumFractionDigits: 0 }) + ' ms';
  const s = n / 1000;
  if (s < 60) return formatNumber(s, locale, { maximumFractionDigits: 1 }) + ' s';
  return Math.floor(s / 60) + ' min ' + Math.round(s % 60) + ' s';
}

/** Results per disposition, largest first. */
export function dispositionCounts(results) {
  const counts = {};
  for (const r of results || []) counts[r.disposition || 'Pending'] = (counts[r.disposition || 'Pending'] || 0) + 1;
  return Object.entries(counts)
    .map(([disposition, count]) => ({ disposition, count }))
    .sort((a, b) => b.count - a.count);
}

/** Everything the run detail shows, read together: the run, its stages, queries, results, and new findings. */
export async function loadRun(client, id) {
  const run = await runsApi.get(client, id);
  const [stages, queries, results, findings] = await Promise.all([
    runsApi.stages(client, id).catch(() => []),
    runsApi.queries(client, id).catch(() => []),
    runsApi.results(client, id).catch(() => []),
    findingsApi.list(client, { runId: id, pageNumber: 1, pageSize: 20, sortBy: 'relevanceScore', sortDirection: 'Descending', includeDryRun: run.isDryRun || undefined }).catch(() => null),
  ]);
  return { run, stages: Array.isArray(stages) ? stages : listItems(stages), queries: listItems(queries), results: listItems(results), findings: listItems(findings) };
}

/**
 * One run (dashboard `/runs/:id`): status, totals (duration, findings, duplicates, sources, tokens, cost), the stage
 * list with a duration chart, the queries it sent, what happened to the results, and its findings. Polls while the run
 * is queued or running, and offers Cancel then.
 */
export function RunDetail({ id }) {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  return (
    <DetailScreen testID="run-detail" load={() => loadRun(client, id)} deps={[client, id]} feature={t('mobile.features.runs')}>
      {(data, resource) => (
        <RunBody
          data={data}
          resource={resource}
          busy={busy}
          error={error}
          locale={i18n.language}
          t={t}
          onCancel={async () => {
            setBusy(true);
            setError(null);
            try {
              const run = await runsApi.cancel(client, id);
              resource.setData({ ...data, run: run && run.id ? run : data.run });
            } catch (e) {
              setError(e);
            } finally {
              setBusy(false);
            }
          }}
        />
      )}
    </DetailScreen>
  );
}

function RunBody({ data, resource, busy, error, onCancel, t, locale }) {
  const { run, stages, queries, results, findings } = data;
  const active = ACTIVE_RUN_STATUSES.includes(run.status);
  usePolling(resource.poll, active);
  const ordered = stages.slice().sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));
  const timed = ordered.filter((s) => s.durationMs > 0);
  const tokens = (run.inputTokens || 0) + (run.outputTokens || 0);
  return (
    <>
      <DetailHeader
        title={t('mobile.runs.title', { date: formatDateTime(run.startedUtc || run.queuedUtc, locale) })}
        badges={[
          { label: t('mobile.enums.runStatus.' + run.status), tone: RUN_STATUS_TONES[run.status] || 'neutral' },
          { label: t('mobile.enums.trigger.' + run.trigger), tone: 'neutral' },
          run.isDryRun ? { label: t('mobile.runs.dryRun'), tone: 'warning' } : null,
        ]}
      />
      {run.errorMessage ? <Banner kind="danger" title={t('mobile.runs.failed')}>{run.errorMessage}</Banner> : null}
      {run.skippedReason ? <Banner kind="warning">{run.skippedReason}</Banner> : null}
      {active ? (
        <Banner kind="info" title={run.cancelRequested ? t('mobile.runs.cancelling') : t('mobile.runs.activeTitle')}>
          {!run.cancelRequested ? <Button title={t('mobile.runs.cancel')} kind="secondary" icon="stop-circle-outline" busy={busy} onPress={() => void onCancel()} testID="run-cancel" /> : null}
        </Banner>
      ) : null}
      <ActionError error={error} />
      {run.summary ? (
        <Card>
          <T variant="label">{t('mobile.runs.summary')}</T>
          <T selectable>{run.summary}</T>
        </Card>
      ) : null}
      <SectionHeader title={t('mobile.runs.totals')} />
      <Card testID="run-totals">
        <KeyValue label={t('mobile.runs.duration')} value={formatDuration(run.durationMs, locale)} />
        <KeyValue label={t('mobile.runs.newFindings')} value={String(run.newFindingCount || 0)} />
        <KeyValue label={t('mobile.runs.duplicates')} value={String(run.duplicateCount || 0)} />
        <KeyValue label={t('mobile.runs.sources')} value={String(run.sourceCount || 0)} />
        <KeyValue label={t('mobile.runs.queries')} value={String(queries.length)} />
        <KeyValue label={t('mobile.runs.tokens')} value={t('mobile.runs.tokensValue', { total: formatCompact(tokens, locale), input: formatCompact(run.inputTokens || 0, locale), output: formatCompact(run.outputTokens || 0, locale) })} />
        {run.estimatedCostUsd !== undefined && run.estimatedCostUsd !== null ? <KeyValue label={t('mobile.runs.cost')} value={formatCost(run.estimatedCostUsd, locale)} /> : null}
        <KeyValue label={t('mobile.runs.queued')} value={formatDateTime(run.queuedUtc, locale)} />
        {run.completedUtc ? <KeyValue label={t('mobile.runs.completed')} value={formatDateTime(run.completedUtc, locale)} /> : null}
      </Card>
      <SectionHeader title={t('mobile.runs.stages', { count: ordered.length })} />
      {ordered.length ? (
        <Card testID="run-stages">
          {ordered.map((s) => (
            <View key={s.id} style={{ gap: 2, paddingVertical: spacing.xs }} testID={'stage-' + s.stage}>
              <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm, flexWrap: 'wrap' }}>
                <T style={{ flex: 1, fontWeight: '600' }}>{t('mobile.enums.stage.' + s.stage, { defaultValue: s.stage })}</T>
                <Badge label={t('mobile.enums.stageStatus.' + s.status)} tone={STAGE_STATUS_TONES[s.status] || 'neutral'} />
              </View>
              <T variant="small">{t('mobile.runs.stageLine', { duration: formatDuration(s.durationMs, locale), in: s.itemsIn || 0, out: s.itemsOut || 0 })}</T>
              {s.errorMessage ? <T variant="small">{s.errorMessage}</T> : null}
            </View>
          ))}
        </Card>
      ) : (
        <T variant="muted">{t('mobile.runs.noStages')}</T>
      )}
      {timed.length > 1 ? (
        <Card>
          <HorizontalBarChart testID="stage-chart" rows={timed.map((s) => ({ label: t('mobile.enums.stage.' + s.stage, { defaultValue: s.stage }), value: s.durationMs }))} formatValue={(n) => formatDuration(n, locale)} accessibilityLabel={t('mobile.runs.stageChart')} />
        </Card>
      ) : null}
      {results.length ? (
        <>
          <SectionHeader title={t('mobile.runs.results', { count: results.length })} />
          <Card>
            {dispositionCounts(results).map((d) => (
              <KeyValue key={d.disposition} label={t('mobile.enums.disposition.' + d.disposition, { defaultValue: d.disposition })} value={String(d.count)} />
            ))}
          </Card>
        </>
      ) : null}
      {queries.length ? (
        <>
          <SectionHeader title={t('mobile.runs.queryList')} />
          <Card>
            {queries.slice(0, 25).map((q) => (
              <ListRow key={q.id} icon={q.succeeded === false ? 'alert-circle-outline' : 'search-outline'} title={q.queryText} detail={t('mobile.runs.queryLine', { count: q.resultCount || 0, provider: q.provider || '' })} />
            ))}
          </Card>
        </>
      ) : null}
      {findings.length ? (
        <>
          <SectionHeader title={t('mobile.runs.findingList')} />
          <Card>
            {findings.map((f) => (
              <ListRow key={f.id} icon="search-outline" title={f.title} detail={t('mobile.enums.tier.' + f.tier)} onPress={() => router.push('/topics/findings/' + encodeURIComponent(f.id))} />
            ))}
          </Card>
        </>
      ) : null}
      <Button title={t('mobile.runs.openTopic')} kind="ghost" onPress={() => router.push('/topics/' + encodeURIComponent(run.topicId))} />
    </>
  );
}
