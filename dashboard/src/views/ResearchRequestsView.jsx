import { Route, Routes, useLocation } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader.jsx';
import RequestDetail from './requests/RequestDetail.jsx';
import RequestForm from './requests/RequestForm.jsx';
import RequestsList from './requests/RequestsList.jsx';

function NewRequest() {
  const { t } = useTranslation();
  const location = useLocation();
  return (
    <div className="page">
      <PageHeader title={t('oneTime.newTitle')} summary={t('oneTime.newSummary')} />
      <section className="panel">
        <RequestForm initialText={(location.state && location.state.requestText) || ''} />
      </section>
    </div>
  );
}

// One-time requests (plan section 13.1): /one-time, /one-time/new, and /one-time/:id.
export default function ResearchRequestsView() {
  return (
    <Routes>
      <Route index element={<RequestsList />} />
      <Route path="new" element={<NewRequest />} />
      <Route path=":id" element={<RequestDetail />} />
    </Routes>
  );
}
