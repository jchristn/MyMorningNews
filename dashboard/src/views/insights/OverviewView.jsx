import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import ErrorState from '../../components/ErrorState.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import OverviewTiles from './OverviewTiles.jsx';
import { ScopeBar, useInsightsScope, useReport, useTopics } from './insightsParts.jsx';

// The Insights landing page: headline numbers for the chosen range and topic, each opening the page behind it.
export default function OverviewView() {
  const { t } = useTranslation();
  const f = useFormat();
  const topics = useTopics();
  const scope = useInsightsScope();
  const overview = useReport('overview', scope);
  const r = overview.data;
  return (
    <div className="page">
      <PageHeader
        title={t('nav.items.insightsOverview')}
        summary={t('routes.insightsOverview.summary')}
        meta={r && r.fromUtc && r.toUtc ? <span className="muted small">{t('insights.overview.window', { from: f.date(r.fromUtc), to: f.date(r.toUtc) })}</span> : null}
      />
      <ScopeBar scope={scope} topics={topics} onRefresh={overview.reload} loading={overview.loading} />
      <ErrorState error={overview.error} onRetry={overview.reload} />
      <OverviewTiles report={r} />
      <p className="muted small">{t('insights.overview.help')}</p>
    </div>
  );
}
