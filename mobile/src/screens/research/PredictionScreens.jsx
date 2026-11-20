import React, { useState } from 'react';
import { View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { predictionsApi } from '@/api/research';
import { ButtonRow, ChipGroup } from '@/components/Chips';
import { FeedbackBar } from '@/components/FeedbackBar';
import { RatingControl, useFeedbackMap } from '@/components/feedback/RatingControl';
import { Banner, Button, Card, Field, KeyValue, ListRow, SectionHeader, T } from '@/components/ui';
import { PREDICTION_RESOLUTIONS, PREDICTION_STATUS_TONES, isPredictionDue } from '@/lib/enums';
import { formatDate, formatDateTime, formatNumber } from '@/lib/format';
import { useAuth } from '@/state/AuthContext';
import { spacing } from '@/theme/palette';
import { DetailHeader, DetailScreen, ItemCard } from '../common/DetailScreen';
import { ResourceListScreen } from '../common/ResourceListScreen';
import { findingHref } from './FindingScreens';

export const predictionHref = (id) => '/today/predictions/' + encodeURIComponent(id);

/** List filters: due to grade first, then open, proposed, resolved, and everything. */
export const PREDICTION_FILTERS = ['due', 'Open', 'ProposedResolved', 'resolved', 'all'];

export function predictionsQuery(filter, topicId) {
  const query = { pageNumber: 1, pageSize: 100, sortBy: 'horizonUtc', sortDirection: 'Ascending', topicId };
  if (filter === 'Open' || filter === 'ProposedResolved') query.status = filter;
  return query;
}

/** Narrow a page of predictions to a filter the server has no single status for (due, resolved). */
export function filterPredictions(items, filter, now = Date.now()) {
  if (filter === 'due') return items.filter((p) => isPredictionDue(p, now) || p.status === 'ProposedResolved');
  if (filter === 'resolved') return items.filter((p) => PREDICTION_RESOLUTIONS.includes(p.status));
  return items;
}

const pct = (n, locale) => formatNumber(n, locale, { style: 'percent', maximumFractionDigits: 0 });

export function PredictionsScreen() {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const [filter, setFilter] = useState('due');
  const ratings = useFeedbackMap('Prediction');
  return (
    <ResourceListScreen
      testID="predictions-screen"
      load={() => predictionsApi.list(client, predictionsQuery(filter))}
      deps={[client, filter]}
      filter={(items) => filterPredictions(items, filter)}
      feature={t('mobile.features.predictions')}
      header={<ChipGroup scroll options={PREDICTION_FILTERS.map((f) => ({ value: f, label: t('mobile.predictions.filters.' + f), testID: 'predictions-filter-' + f }))} value={filter} onChange={setFilter} accessibilityLabel={t('mobile.common.filter')} />}
      empty={{ icon: 'trending-up-outline', title: filter === 'due' ? t('mobile.predictions.noneDue') : t('mobile.predictions.emptyTitle'), body: t('mobile.predictions.emptyBody') }}
      detailHref={predictionHref}
      renderDetail={(id, { onChanged }) => <PredictionDetail key={id} id={id} onChanged={onChanged} />}
      placeholder={t('mobile.predictions.select')}
      renderRow={(p, { selected, onPress }) => (
        <View style={{ gap: spacing.xs }}>
          <ItemCard
          title={p.statement}
          selected={selected}
          onPress={onPress}
          testID={'prediction-row-' + p.id}
          badges={[
            { label: t('mobile.enums.predictionStatus.' + p.status), tone: PREDICTION_STATUS_TONES[p.status] || 'neutral' },
            isPredictionDue(p) ? { label: t('mobile.predictions.due'), tone: 'warning' } : null,
            { label: t('mobile.predictions.confidence', { value: pct(p.confidence, i18n.language) }), tone: 'neutral' },
          ]}
          lines={[t('mobile.predictions.by', { date: formatDate(p.horizonUtc, i18n.language) })]}
          />
          {ratings.available ? <RatingControl compact targetType="Prediction" targetId={p.id} title={p.statement} current={ratings.map[p.id] || null} onChange={(fb) => ratings.set(p.id, fb)} /> : null}
        </View>
      )}
    />
  );
}

/** A proposed resolution from the pipeline, with Accept and Reject while it waits for you. */
function ReviewCard({ review, onAccept, onReject, busy }) {
  const { t, i18n } = useTranslation();
  const decided = review.acceptedByUser === true || review.acceptedByUser === false;
  return (
    <Card testID={'review-' + review.id}>
      <T style={{ fontWeight: '600' }}>{t('mobile.predictions.proposed', { status: t('mobile.enums.predictionStatus.' + review.proposedStatus) })}</T>
      <T variant="small">{[formatDateTime(review.createdUtc, i18n.language), t('mobile.predictions.confidence', { value: pct(review.confidence, i18n.language) })].join(' · ')}</T>
      {review.rationale ? <T selectable>{review.rationale}</T> : null}
      {(review.findingIds || []).map((fid, i) => (
        <ListRow key={fid} icon="search-outline" title={t('mobile.predictions.evidenceN', { number: i + 1 })} onPress={() => router.push(findingHref(fid))} />
      ))}
      {decided ? (
        <T variant="small">{review.acceptedByUser ? t('mobile.predictions.accepted') : t('mobile.predictions.rejected')}</T>
      ) : (
        <ButtonRow>
          <Button title={t('mobile.predictions.accept')} icon="checkmark" busy={busy === 'accept:' + review.id} onPress={() => onAccept(review)} testID={'review-accept-' + review.id} />
          <Button title={t('mobile.predictions.reject')} icon="close" kind="secondary" busy={busy === 'reject:' + review.id} onPress={() => onReject(review)} testID={'review-reject-' + review.id} />
        </ButtonRow>
      )}
    </Card>
  );
}

/** Grade a prediction yourself: a resolution and an optional note. */
function ResolveForm({ prediction, onResolved }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  const [status, setStatus] = useState(null);
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const submit = async () => {
    setBusy(true);
    setError(null);
    try {
      onResolved(await predictionsApi.resolve(client, prediction.id, { status, note: note.trim() || null }));
      setStatus(null);
      setNote('');
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card testID="resolve-form">
      <T variant="label">{PREDICTION_RESOLUTIONS.includes(prediction.status) ? t('mobile.predictions.regrade') : t('mobile.predictions.resolve')}</T>
      <ChipGroup options={PREDICTION_RESOLUTIONS.map((s) => ({ value: s, label: t('mobile.enums.predictionStatus.' + s), testID: 'resolve-' + s }))} value={status} onChange={setStatus} accessibilityLabel={t('mobile.predictions.resolve')} />
      <Field label={t('mobile.predictions.note')} value={note} onChangeText={setNote} multiline testID="resolve-note" />
      {error ? <Banner kind="danger">{error}</Banner> : null}
      <Button title={t('mobile.predictions.saveResolution')} disabled={!status} busy={busy} onPress={() => void submit()} testID="resolve-submit" />
    </Card>
  );
}

/** A prediction: the statement, confidence, horizon, rationale, the pipeline's proposed resolutions, and grading. */
export function PredictionDetail({ id, onChanged }) {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const [busy, setBusy] = useState(null);
  const [error, setError] = useState(null);
  return (
    <DetailScreen testID="prediction-detail" load={() => predictionsApi.get(client, id)} deps={[client, id]} feature={t('mobile.features.predictions')}>
      {(detail, resource) => {
        const p = detail.prediction;
        const decide = async (kind, review) => {
          setBusy(kind + ':' + review.id);
          setError(null);
          try {
            const updated = kind === 'accept' ? await predictionsApi.acceptReview(client, review.id) : await predictionsApi.rejectReview(client, review.id);
            resource.setData({ ...detail, prediction: updated && updated.id ? updated : p, reviews: detail.reviews.map((r) => (r.id === review.id ? { ...r, acceptedByUser: kind === 'accept' } : r)) });
            if (onChanged) onChanged();
          } catch (e) {
            setError(e.message);
          } finally {
            setBusy(null);
          }
        };
        const reviews = (detail.reviews || []).slice().sort((a, b) => String(b.createdUtc).localeCompare(String(a.createdUtc)));
        return (
          <>
            <DetailHeader
              title={p.statement}
              badges={[{ label: t('mobile.enums.predictionStatus.' + p.status), tone: PREDICTION_STATUS_TONES[p.status] || 'neutral' }, isPredictionDue(p) ? { label: t('mobile.predictions.due'), tone: 'warning' } : null]}
            />
            <Card>
              <KeyValue label={t('mobile.predictions.confidenceLabel')} value={pct(p.confidence, i18n.language)} />
              <KeyValue label={t('mobile.predictions.horizon')} value={formatDate(p.horizonUtc, i18n.language)} />
              <KeyValue label={t('mobile.predictions.made')} value={formatDate(p.createdUtc, i18n.language)} />
              {p.resolvedUtc ? <KeyValue label={t('mobile.predictions.resolvedOn')} value={formatDate(p.resolvedUtc, i18n.language)} /> : null}
              {p.brierScore !== undefined && p.brierScore !== null ? <KeyValue label={t('mobile.predictions.brier')} value={formatNumber(p.brierScore, i18n.language, { maximumFractionDigits: 3 })} /> : null}
            </Card>
            {p.rationale ? (
              <Card>
                <T variant="label">{t('mobile.predictions.rationale')}</T>
                <T selectable>{p.rationale}</T>
              </Card>
            ) : null}
            {p.resolutionNote ? <Banner kind="info" title={t('mobile.predictions.note')}>{p.resolutionNote}</Banner> : null}
            {error ? <Banner kind="danger">{error}</Banner> : null}
            {reviews.length ? <SectionHeader title={t('mobile.predictions.reviews')} /> : null}
            {reviews.map((r) => (
              <ReviewCard key={r.id} review={r} busy={busy} onAccept={(rv) => void decide('accept', rv)} onReject={(rv) => void decide('reject', rv)} />
            ))}
            <View style={{ gap: spacing.sm }}>
              <ResolveForm
                prediction={p}
                onResolved={(updated) => {
                  resource.setData({ ...detail, prediction: updated });
                  if (onChanged) onChanged();
                }}
              />
            </View>
            <FeedbackBar targetType="Prediction" targetId={p.id} title={p.statement} />
          </>
        );
      }}
    </DetailScreen>
  );
}
