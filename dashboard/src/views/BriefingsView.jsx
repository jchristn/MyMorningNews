import { Route, Routes, useParams, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader.jsx';
import BriefingReader from './briefings/BriefingReader.jsx';
import BriefingsTable from './briefings/BriefingsTable.jsx';

// The briefing archive: every edition, run, and one-time briefing, with filters and downloads. ?kind= preselects one.
function BriefingsList() {
  const { t } = useTranslation();
  const [params] = useSearchParams();
  const editionId = params.get('editionId');
  return (
    <div className="page">
      <PageHeader title={t('nav.items.briefings')} summary={t('routes.briefings.summary')} />
      <section className="panel">
        <BriefingsTable key={editionId || 'all'} extraFilters={editionId ? { editionId } : null} initialKind={params.get('kind') || ''} />
      </section>
    </div>
  );
}

function BriefingPage() {
  const { id } = useParams();
  return <BriefingReader key={id} briefingId={id} />;
}

export default function BriefingsView() {
  return (
    <Routes>
      <Route index element={<BriefingsList />} />
      <Route path=":id" element={<BriefingPage />} />
    </Routes>
  );
}
