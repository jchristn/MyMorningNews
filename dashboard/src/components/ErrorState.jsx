import PropTypes from 'prop-types';
import { RefreshCw, TriangleAlert, X } from 'lucide-react';
import { Link, useInRouterContext } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useOptionalAuth } from '../context/AuthContext.jsx';
import { describeError, isSetupRequiredError } from '../utils/errors.js';

// The way forward from a SetupRequired refusal: administrators open the Setup screen, everyone else asks one.
function SetupRequiredHint() {
  const { t } = useTranslation();
  const auth = useOptionalAuth();
  const inRouter = useInRouterContext();
  if (auth && auth.isTenantAdmin && inRouter) {
    return (
      <Link to="/dashboard/setup" className="small" data-testid="setup-required-link">
        {t('setup.required.open')}
      </Link>
    );
  }
  return <span className="muted small">{t('setup.required.askAdmin')}</span>;
}

// Error presenter with retry and the request ID. inline renders a dismissible banner instead of a block.
export default function ErrorState({ error, onRetry, inline = false, onDismiss }) {
  const { t } = useTranslation();
  if (!error) return null;
  const info = describeError(error, t);
  return (
    <div className={inline ? 'error-banner' : 'state-block error-state'} role="alert">
      <TriangleAlert size={inline ? 16 : 28} aria-hidden="true" />
      <div className="error-text">
        <strong>{info.title}</strong>
        <span>{info.message}</span>
        {isSetupRequiredError(error) ? <SetupRequiredHint /> : null}
        {info.requestId ? (
          <span className="muted small">
            {t('errors.requestId')} <code>{info.requestId}</code>
          </span>
        ) : null}
      </div>
      <div className="error-actions">
        {onRetry ? (
          <button type="button" className="button secondary small" onClick={onRetry}>
            <RefreshCw size={14} aria-hidden="true" /> {t('common.retry')}
          </button>
        ) : null}
        {inline && onDismiss ? (
          <button type="button" className="icon-button" onClick={onDismiss} aria-label={t('common.dismiss')} title={t('common.dismiss')}>
            <X size={14} aria-hidden="true" />
          </button>
        ) : null}
      </div>
    </div>
  );
}

ErrorState.propTypes = {
  error: PropTypes.any,
  onRetry: PropTypes.func,
  inline: PropTypes.bool,
  onDismiss: PropTypes.func
};
