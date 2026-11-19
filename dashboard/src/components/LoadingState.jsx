import PropTypes from 'prop-types';
import { LoaderCircle } from 'lucide-react';
import { useTranslation } from 'react-i18next';

export default function LoadingState({ label, compact = false }) {
  const { t } = useTranslation();
  return (
    <div className={compact ? 'loading-inline' : 'state-block loading-state'} role="status" aria-live="polite">
      <LoaderCircle size={compact ? 14 : 24} className="spin" aria-hidden="true" />
      <span>{label || t('common.loading')}</span>
    </div>
  );
}

LoadingState.propTypes = {
  label: PropTypes.string,
  compact: PropTypes.bool
};
