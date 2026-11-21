import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import SetupStateBadge from '../../components/SetupStateBadge.jsx';

// Where each optional check is set up. Settings are the operator's, so only global administrators get that link.
const LINKS = {
  Embeddings: { to: '/dashboard/models', key: 'setup.extras.openModels', admin: false },
  Email: { to: '/dashboard/settings', key: 'setup.extras.openSettings', admin: true }
};

// Embeddings and email: what each adds, its state, and the server's message. Collapsed, and never blocking.
export default function OptionalExtras({ checks }) {
  const { t } = useTranslation();
  const { isAdmin } = useAuth();
  if (checks.length === 0) return null;
  const readyCount = checks.filter((c) => c.state === 'Ready').length;
  return (
    <details className="panel setup-extras" data-testid="setup-extras">
      <summary>
        <span className="setup-extras-title">{t('setup.extras.title')}</span>{' '}
        <span className="muted small">{t('setup.extras.count', { ready: readyCount, count: checks.length })}</span>
      </summary>
      <p className="muted small">{t('setup.extras.note')}</p>
      <ul className="setup-extras-list">
        {checks.map((check) => {
          const link = LINKS[check.kind];
          return (
            <li key={check.key} data-testid={'setup-check-' + check.key}>
              <div className="setup-extras-head">
                <strong>{t('setup.kind.' + check.kind, { defaultValue: check.kind || '' })}</strong>
                <SetupStateBadge state={check.state} />
              </div>
              <p className="small">{t('setup.extras.what.' + check.kind, { defaultValue: '' })}</p>
              {check.message ? (
                <p className="muted small" dir="auto">
                  {check.message}
                </p>
              ) : null}
              {link && (!link.admin || isAdmin) ? (
                <Link to={link.to} className="link-button small">
                  {t(link.key)}
                </Link>
              ) : null}
            </li>
          );
        })}
      </ul>
    </details>
  );
}

OptionalExtras.propTypes = { checks: PropTypes.arrayOf(PropTypes.object).isRequired };
