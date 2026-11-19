import PropTypes from 'prop-types';
import { Construction } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import PageHeader from './PageHeader.jsx';
import EmptyState from './EmptyState.jsx';

// Route whose backend arrives in a later plan phase. The route exists so navigation is complete and honest.
export default function PhasePlaceholder({ routeKey, phase }) {
  const { t } = useTranslation();
  return (
    <div className="page">
      <PageHeader title={t('nav.items.' + routeKey)} summary={t('routes.' + routeKey + '.summary')} />
      <section className="panel">
        <EmptyState icon={Construction} title={t('placeholder.title', { phase })} message={t('placeholder.message', { feature: t('nav.items.' + routeKey), phase })} />
      </section>
    </div>
  );
}

PhasePlaceholder.propTypes = {
  routeKey: PropTypes.string.isRequired,
  phase: PropTypes.number.isRequired
};
