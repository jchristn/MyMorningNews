import PropTypes from 'prop-types';
import { useNavigate } from 'react-router-dom';
import { BookOpen, CirclePlay, Coins, Compass, Lightbulb, ListChecks, Mail, MessageSquare, Quote, Target, TrendingUp } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import KpiTile from '../../components/KpiTile.jsx';
import { pctOrDash } from './insightsParts.jsx';

// Every overview tile, in display order. Home shows a subset by key.
export const OVERVIEW_TILE_KEYS = ['topics', 'runs', 'findings', 'precision', 'briefings', 'emails', 'predictions', 'accuracy', 'feedback', 'suggestions', 'quotes', 'cost'];

// The headline numbers of GET /v1.0/api/analytics/overview as KPI tiles. Each tile opens the page behind it. Pass
// keys to show a subset; a null report shows placeholders.
export default function OverviewTiles({ report, keys = OVERVIEW_TILE_KEYS, testId = 'overview-tiles' }) {
  const { t } = useTranslation();
  const f = useFormat();
  const navigate = useNavigate();
  const r = report || null;
  const n = (v) => (r ? f.number(v || 0) : '...');
  const go = (path) => () => navigate('/dashboard/' + path);
  const failedRuns = r ? r.runsFailed || 0 : 0;
  const quoteFailure = r ? r.quoteFailureRate : null;
  const tiles = {
    topics: { icon: Lightbulb, label: t('insights.overview.activeTopics'), value: n(r && r.activeTopics), onClick: go('topics') },
    runs: {
      icon: CirclePlay,
      label: t('insights.overview.runs'),
      value: n(r && r.runs),
      detail: r ? t('insights.overview.runOutcomes', { completed: f.number(r.runsCompleted || 0), partial: f.number(r.runsPartiallySucceeded || 0), failed: f.number(failedRuns) }) : null,
      tone: failedRuns > 0 ? 'warning' : 'neutral',
      onClick: go('runs')
    },
    findings: { icon: ListChecks, label: t('insights.overview.findingsShown'), value: n(r && r.findingsShown), detail: r ? t('insights.overview.findingsRated', { count: f.number(r.findingsRated || 0) }) : null, onClick: go('findings') },
    precision: {
      icon: Target,
      label: t('insights.overview.precision'),
      value: r ? pctOrDash(f, r.relevancePrecision) : '...',
      detail: t('insights.overview.precisionHelp'),
      tone: 'success',
      onClick: go('insights/relevance')
    },
    briefings: { icon: BookOpen, label: t('insights.overview.briefingsReady'), value: n(r && r.briefingsReady), onClick: go('briefings') },
    emails: {
      icon: Mail,
      label: t('insights.overview.emailsSent'),
      value: n(r && r.emailsSent),
      detail: r ? t('insights.overview.emailsFailed', { count: f.number(r.emailsFailed || 0) }) : null,
      tone: r && r.emailsFailed > 0 ? 'warning' : 'neutral',
      onClick: go('insights/email')
    },
    predictions: {
      icon: TrendingUp,
      label: t('insights.overview.predictionsOpen'),
      value: n(r && r.predictionsOpen),
      detail: r ? t('insights.overview.predictionsResolved', { count: f.number(r.predictionsResolved || 0) }) : null,
      onClick: go('predictions')
    },
    accuracy: {
      icon: Target,
      label: t('insights.overview.accuracy'),
      value: r ? pctOrDash(f, r.predictionAccuracy) : '...',
      detail: r && r.meanBrier !== null && r.meanBrier !== undefined ? t('insights.overview.brier', { value: f.number(r.meanBrier, { maximumFractionDigits: 3 }) }) : null,
      tone: 'info',
      onClick: go('insights/accuracy')
    },
    feedback: {
      icon: MessageSquare,
      label: t('insights.overview.feedback'),
      value: n(r && r.feedbackTotal),
      detail: r ? t('insights.overview.feedbackWithReason', { count: f.number(r.feedbackWithReason || 0) }) : null,
      onClick: go('insights/feedback')
    },
    suggestions: {
      icon: Compass,
      label: t('insights.overview.suggestionsNew'),
      value: n(r && r.suggestionsNew),
      detail: r ? t('insights.overview.suggestionsAccepted', { count: f.number(r.suggestionsAccepted || 0) }) : null,
      onClick: go('suggestions')
    },
    quotes: {
      icon: Quote,
      label: t('insights.overview.quoteFailure'),
      value: r ? pctOrDash(f, quoteFailure, 1) : '...',
      detail: t('insights.overview.quoteFailureHelp'),
      tone: quoteFailure !== null && quoteFailure !== undefined && quoteFailure > 0.1 ? 'danger' : 'neutral',
      onClick: go('insights/relevance')
    },
    cost: {
      icon: Coins,
      label: t('insights.overview.modelCost'),
      value: r ? f.usd(Number(r.modelCostUsd) || 0) : '...',
      detail: r ? t('insights.overview.modelCalls', { count: r.modelCalls || 0 }) : null,
      onClick: go('insights/usage')
    }
  };
  return (
    <div className="kpi-grid" data-testid={testId}>
      {keys
        .filter((key) => tiles[key])
        .map((key) => (
          <KpiTile key={key} {...tiles[key]} />
        ))}
    </div>
  );
}

OverviewTiles.propTypes = { report: PropTypes.object, keys: PropTypes.arrayOf(PropTypes.string), testId: PropTypes.string };
