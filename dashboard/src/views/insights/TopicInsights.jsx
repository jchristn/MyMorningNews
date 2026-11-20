import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { RefreshCw, Scale } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import ChartCard from '../../charts/ChartCard.jsx';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import KpiTile from '../../components/KpiTile.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import { WEIGHT_FIELDS, rateBars, weightsApplied, weightsBody } from './insightsData.js';
import { RateChart, pctOrDash, useReport } from './insightsParts.jsx';

// Suggested ranking weights from your ratings (GET /topics/{id}/ranking-recommendation), with Apply these weights,
// which writes the topic's weight fields.
export function RankingRecommendation({ topic, onTopicUpdated }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [rec, setRec] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(true);
  const [confirming, setConfirming] = useState(false);
  const [notStored, setNotStored] = useState(false);

  const load = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      setRec(await apiClient.getRankingRecommendation(topic.id));
    } catch (err) {
      setError(err);
    } finally {
      setLoading(false);
    }
  }, [apiClient, topic.id]);

  useEffect(() => {
    load();
  }, [load]);

  const parts = Object.keys(WEIGHT_FIELDS);
  const body = useMemo(() => weightsBody(rec && rec.recommended), [rec]);
  const differs = rec ? parts.some((p) => rec.current && rec.recommended && Math.abs((rec.current[p] || 0) - (rec.recommended[p] || 0)) >= 0.001) : false;
  const w = (v) => (v === undefined || v === null ? '-' : f.number(v, { minimumFractionDigits: 2, maximumFractionDigits: 3 }));

  return (
    <ChartCard
      title={t('insights.ranking.title')}
      subtitle={rec ? t('insights.ranking.rated', { count: rec.ratedFindings || 0 }) : null}
      onRefresh={load}
      loading={loading}
      className="wide"
      controls={
        <button type="button" className="button primary small" onClick={() => setConfirming(true)} disabled={!rec || !rec.enough || !differs}>
          <Scale size={14} aria-hidden="true" /> {t('insights.ranking.apply')}
        </button>
      }
    >
      <ErrorState error={error} inline onRetry={load} />
      {loading && !rec ? <LoadingState /> : null}
      {rec ? (
        <>
          {rec.note ? <p className="muted small">{rec.note}</p> : null}
          {!rec.enough ? <div className="notice notice-info">{t('insights.ranking.notEnough')}</div> : null}
          {notStored ? <div className="notice notice-warning">{t('insights.ranking.notStored')}</div> : null}
          <div className="table-scroll">
            <table className="kv-table ranking-table">
              <thead>
                <tr>
                  <th scope="col">{t('insights.ranking.component')}</th>
                  <th scope="col" className="align-right">
                    {t('insights.ranking.current')}
                  </th>
                  <th scope="col" className="align-right">
                    {t('insights.ranking.recommended')}
                  </th>
                  <th scope="col" className="align-right">
                    {t('insights.ranking.separation')}
                  </th>
                </tr>
              </thead>
              <tbody>
                {parts.map((p) => (
                  <tr key={p}>
                    <td>{t('findings.components.' + p, { defaultValue: p })}</td>
                    <td className="align-right mono">{w(rec.current && rec.current[p])}</td>
                    <td className="align-right mono">
                      <strong>{w(rec.recommended && rec.recommended[p])}</strong>
                    </td>
                    <td className="align-right mono">{rec.separation && rec.separation[p] !== undefined ? f.number(rec.separation[p], { maximumFractionDigits: 1 }) : '-'}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="muted small">{t('insights.ranking.help')}</p>
        </>
      ) : null}
      <ConfirmModal
        open={confirming}
        title={t('insights.ranking.applyTitle')}
        message={t('insights.ranking.applyMessage', { topic: topic.name })}
        confirmLabel={t('insights.ranking.apply')}
        onClose={() => setConfirming(false)}
        onConfirm={async () => {
          const updated = await apiClient.updateTopic(topic.id, body);
          const stored = weightsApplied(updated, body);
          setNotStored(!stored);
          if (updated && onTopicUpdated) onTopicUpdated(updated);
          addNotification(stored ? t('insights.ranking.applied') : t('insights.ranking.notStored'), stored ? 'success' : 'warning', 8000);
          load();
        }}
      />
    </ChartCard>
  );
}

RankingRecommendation.propTypes = { topic: PropTypes.object.isRequired, onTopicUpdated: PropTypes.func };

// The topic Analytics tab: ranking recommendation, relevance and accuracy for this topic, and precision by dimension.
export default function TopicInsights({ topic, onTopicUpdated }) {
  const { t } = useTranslation();
  const f = useFormat();
  const scope = useMemo(() => ({ days: 90, topicId: topic.id, everyone: false }), [topic.id]);
  const relevance = useReport('relevance', scope);
  const accuracy = useReport('accuracy', scope);
  const r = relevance.data;
  const a = accuracy.data;
  return (
    <div className="stack">
      <div className="kpi-grid">
        <KpiTile label={t('insights.relevance.precision')} value={pctOrDash(f, r && r.precision)} tone="success" />
        <KpiTile label={t('insights.relevance.coverage')} value={pctOrDash(f, r && r.coverage)} tone="info" />
        <KpiTile label={t('insights.accuracy.rate')} value={pctOrDash(f, a && a.accuracyRate)} />
        <KpiTile label={t('insights.accuracy.due')} value={a ? f.number(a.dueForReview || 0) : '-'} tone={a && a.dueForReview > 0 ? 'warning' : 'neutral'} />
      </div>
      <ErrorState error={relevance.error || accuracy.error} inline onRetry={relevance.reload} />
      <div className="chart-grid">
        <RankingRecommendation topic={topic} onTopicUpdated={onTopicUpdated} />
        <ChartCard
          title={t('insights.relevance.byDimension')}
          subtitle={t('insights.topic.last90')}
          controls={
            <button type="button" className="icon-button" onClick={relevance.reload} aria-label={t('common.refresh')} title={t('common.refresh')}>
              <RefreshCw size={16} className={relevance.loading ? 'spin' : ''} aria-hidden="true" />
            </button>
          }
        >
          <RateChart items={rateBars(r && r.byDimension)} ariaLabel={t('insights.relevance.byDimension')} loading={relevance.loading} />
        </ChartCard>
        <ChartCard title={t('insights.relevance.byTier')} subtitle={t('insights.topic.last90')}>
          <RateChart items={rateBars(r && r.byTier, (row) => t('enums.findingTier.' + row.key, { defaultValue: row.key }))} ariaLabel={t('insights.relevance.byTier')} loading={relevance.loading} />
        </ChartCard>
      </div>
    </div>
  );
}

TopicInsights.propTypes = { topic: PropTypes.object.isRequired, onTopicUpdated: PropTypes.func };
