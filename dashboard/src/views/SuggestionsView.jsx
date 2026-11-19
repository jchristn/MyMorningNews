import { useTranslation } from 'react-i18next';
import PageHeader from '../components/PageHeader.jsx';
import SuggestionsTable from './suggestions/SuggestionsTable.jsx';

// The dig-into list: suggestions from every run, with status actions.
export default function SuggestionsView() {
  const { t } = useTranslation();
  return (
    <div className="page">
      <PageHeader title={t('nav.items.suggestions')} summary={t('routes.suggestions.summary')} />
      <section className="panel">
        <SuggestionsTable />
      </section>
    </div>
  );
}
