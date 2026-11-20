import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader.jsx';
import PredictionsTable from './predictions/PredictionsTable.jsx';

// Every prediction the caller can see, with accuracy KPIs, the calibration chart, and a detail drawer (?id= or
// ?prediction= opens one; email links use ?id=).
export default function PredictionsView() {
  const { t } = useTranslation();
  const [params, setParams] = useSearchParams();
  return (
    <div className="page">
      <PageHeader title={t('nav.items.predictions')} summary={t('routes.predictions.summary')} />
      <section className="panel">
        <PredictionsTable initialPredictionId={params.get('id') || params.get('prediction')} onPredictionClosed={() => (params.get('id') || params.get('prediction')) && setParams({}, { replace: true })} />
      </section>
    </div>
  );
}
