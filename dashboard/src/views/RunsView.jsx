import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader.jsx';
import RunsTable from './runs/RunsTable.jsx';

// Every run the caller can see, with topic, status, and trigger filters and a detail drawer (?run= opens one).
export default function RunsView() {
  const { t } = useTranslation();
  const [params, setParams] = useSearchParams();
  return (
    <div className="page">
      <PageHeader title={t('nav.items.runs')} summary={t('routes.runs.summary')} />
      <div className="notice notice-info">{t('runs.pipelineNotice')}</div>
      <section className="panel">
        <RunsTable initialRunId={params.get('run')} onRunClosed={() => params.get('run') && setParams({}, { replace: true })} />
      </section>
    </div>
  );
}
