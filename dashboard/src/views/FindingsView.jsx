import { Link, Route, Routes, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { ArrowLeft } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader.jsx';
import { FindingDetailBody } from './findings/FindingDetail.jsx';
import FindingsTable from './findings/FindingsTable.jsx';

// Ranked findings across topics. ?runId= scopes to one run, ?finding= opens a drawer, and /findings/:id is the full page.
function FindingsList() {
  const { t } = useTranslation();
  const [params, setParams] = useSearchParams();
  const runId = params.get('runId');
  const without = (key) => {
    const next = new URLSearchParams(params);
    next.delete(key);
    setParams(next, { replace: true });
  };
  return (
    <div className="page">
      <PageHeader title={t('nav.items.findings')} summary={t('routes.findings.summary')} />
      <section className="panel">
        <FindingsTable
          key={runId || 'all'}
          runId={runId}
          initialIncludeDryRun={params.get('includeDryRun') === 'true'}
          initialFindingId={params.get('finding')}
          onFindingClosed={() => params.get('finding') && without('finding')}
          onClearRun={() => without('runId')}
        />
      </section>
    </div>
  );
}

function FindingPage() {
  const { t } = useTranslation();
  const { id } = useParams();
  const navigate = useNavigate();
  return (
    <div className="page">
      <Link to="/dashboard/findings" className="link-button small back-link">
        <ArrowLeft size={14} aria-hidden="true" /> {t('findings.backToList')}
      </Link>
      <PageHeader title={t('findings.detail.title')} />
      <section className="panel">
        <FindingDetailBody findingId={id} onDeleted={() => navigate('/dashboard/findings', { replace: true })} />
      </section>
    </div>
  );
}

export default function FindingsView() {
  return (
    <Routes>
      <Route index element={<FindingsList />} />
      <Route path=":id" element={<FindingPage />} />
    </Routes>
  );
}
