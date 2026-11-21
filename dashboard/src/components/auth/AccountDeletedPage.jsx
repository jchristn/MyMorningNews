import { useEffect, useRef } from 'react';
import { Link, useLocation, useSearchParams } from 'react-router-dom';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { parsePurgeDate } from '../../utils/accountDeletion.js';
import AuthShell from './AuthShell.jsx';

// Where account deletion lands (/account-deleted?until=...): the user is signed out everywhere, the account goes on
// the date shown, and signing in before then cancels the deletion. Arriving from the deletion itself (state
// endSession), it ends the local session, which the server has already ended.
export default function AccountDeletedPage() {
  const { t } = useTranslation();
  const f = useFormat();
  const location = useLocation();
  const { endSession } = useAuth();
  const [params] = useSearchParams();
  const ending = Boolean(location.state && location.state.endSession);
  const ended = useRef(false);

  // Once, even if a late request already cleared the session (endSession also drops an "expired" flag it set).
  useEffect(() => {
    if (!ending || ended.current) return;
    ended.current = true;
    endSession();
  }, [ending, endSession]);

  const until = parsePurgeDate(params.get('until'));
  const title = t('account.deleted.title');
  return (
    <AuthShell title={title}>
      <div className="auth-form" data-testid="account-deleted">
        <h1>{title}</h1>
        <div className="notice notice-warning">{until ? t('account.delete.scheduled', { date: f.date(until) }) : t('account.deleted.noDate')}</div>
        <p>{t('account.deleted.signedOut')}</p>
        <p className="muted small">{t('account.deleted.howToCancel')}</p>
        <Link to="/" className="button primary block">
          {t('account.deleted.signIn')}
        </Link>
      </div>
    </AuthShell>
  );
}
