import { useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader.jsx';
import SuggestionsTable from './suggestions/SuggestionsTable.jsx';

// The dig-into list: suggestions from every run, with status actions. ?id= opens one (email links use it).
export default function SuggestionsView() {
  const { t } = useTranslation();
  const [params, setParams] = useSearchParams();
  return (
    <div className="page">
      <PageHeader title={t('nav.items.suggestions')} summary={t('routes.suggestions.summary')} />
      <section className="panel">
        <SuggestionsTable initialSuggestionId={params.get('id')} onSuggestionClosed={() => params.get('id') && setParams({}, { replace: true })} />
      </section>
    </div>
  );
}
