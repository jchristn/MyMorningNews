import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useNavigate } from 'react-router-dom';
import { Download, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useClientConfig from '../../hooks/useClientConfig.js';
import useFormat from '../../hooks/useFormat.js';
import { rememberPendingDeletion } from '../../utils/accountDeletion.js';
import { saveBlob } from '../../utils/briefing.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';

// A labeled bar for one limit. Without a limit it shows the count alone.
export function UsageBar({ label, used, max, display }) {
  const { t } = useTranslation();
  const limited = max !== null && max !== undefined;
  const fraction = limited && max > 0 ? Math.min(1, used / max) : 0;
  const level = fraction >= 1 ? 'danger' : fraction >= 0.8 ? 'warning' : 'ok';
  return (
    <div className="usage-bar">
      <div className="usage-bar-label">
        <span>{label}</span>
        <span className="mono">{limited ? t('account.usage.ofLimit', { used: display(used), max: display(max) }) : t('account.usage.noLimit', { used: display(used) })}</span>
      </div>
      {limited ? (
        <svg className={'usage-bar-track usage-' + level} viewBox="0 0 100 6" preserveAspectRatio="none" role="img" aria-label={label + ': ' + Math.round(fraction * 100) + '%'}>
          <rect x="0" y="0" width="100" height="6" rx="3" className="usage-bar-bg" />
          <rect x="0" y="0" width={Math.max(0, fraction * 100)} height="6" rx="3" className="usage-bar-fill" />
        </svg>
      ) : null}
    </div>
  );
}

UsageBar.propTypes = { label: PropTypes.string.isRequired, used: PropTypes.number.isRequired, max: PropTypes.number, display: PropTypes.func.isRequired };

// The confirmation text for account deletion: the date the data goes when the server says how long the grace period
// is (client config deletionGraceDays), else the general rule.
export function deletionMessage(t, f, config, now = new Date()) {
  const days = config && Number.isInteger(config.deletionGraceDays) && config.deletionGraceDays >= 0 ? config.deletionGraceDays : null;
  if (days === null) return t('account.delete.message');
  return t('account.delete.messageDated', { date: f.date(new Date(now.getTime() + days * 86400000)) });
}

// Usage against the plan (GET /v1.0/api/account/usage), the data export, and account deletion. Deletion signs out
// every session and opens a signed-out page with the date; signing in again within the grace period keeps the
// account.
export default function AccountCard() {
  const { t } = useTranslation();
  const f = useFormat();
  const navigate = useNavigate();
  const { apiClient, identity } = useAuth();
  const { config } = useClientConfig();
  const [meter, setMeter] = useState(null);
  const [error, setError] = useState(null);
  const [exporting, setExporting] = useState(false);
  const [confirming, setConfirming] = useState(false);
  const [typed, setTyped] = useState('');
  const keyUser = Boolean(identity && identity.credentialId);

  useEffect(() => {
    apiClient
      .getAccountUsage()
      .then((m) => setMeter(m && m.planKey ? m : null))
      .catch(() => setMeter(null));
  }, [apiClient]);

  async function exportData() {
    setExporting(true);
    setError(null);
    try {
      const file = await apiClient.exportAccount();
      saveBlob(file.filename || 'mymorningnews-export.zip', file.blob);
    } catch (err) {
      setError(err);
    } finally {
      setExporting(false);
    }
  }

  // The server has already ended every session. The signed-out page ends it here too, without a sign-out request
  // (which would fail and read as an expired session), once it has replaced this page.
  async function deleteAccount() {
    const result = await apiClient.deleteAccount();
    const purge = result && result.purgeAfterUtc ? String(result.purgeAfterUtc) : null;
    rememberPendingDeletion(email, purge);
    navigate('/account-deleted' + (purge ? '?until=' + encodeURIComponent(purge) : ''), { replace: true, state: { endSession: true } });
  }

  const number = (v) => f.number(v);
  const minutes = (v) => f.number(v, { maximumFractionDigits: 1 });
  const email = identity && identity.email ? identity.email : '';

  return (
    <section className="panel" aria-labelledby="account-heading">
      <h2 id="account-heading">{t('account.title')}</h2>
      {meter ? (
        <div className="usage-meter">
          <p className="muted small">{t('account.usage.summary', { plan: meter.planName, date: f.date(meter.resetUtc) })}</p>
          <UsageBar label={t('account.usage.cost')} used={Number(meter.monthToDateCostUsd) || 0} max={meter.monthlyCostCapUsd === null || meter.monthlyCostCapUsd === undefined ? null : Number(meter.monthlyCostCapUsd)} display={(v) => f.usd(v)} />
          <UsageBar label={t('account.usage.topics')} used={meter.topics} max={meter.topicsMax} display={number} />
          <UsageBar label={t('account.usage.runsToday')} used={meter.runsToday} max={meter.runsDailyMax} display={number} />
          {meter.audioEnabled ? <UsageBar label={t('account.usage.audio')} used={meter.audioMinutes} max={meter.audioMinutesMax} display={minutes} /> : <p className="muted small">{t('account.usage.noAudio')}</p>}
          {meter.scheduleMinIntervalMinutes ? <p className="muted small">{t('account.usage.schedule', { minutes: f.number(meter.scheduleMinIntervalMinutes) })}</p> : null}
        </div>
      ) : null}
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <div className="form-actions">
        <button type="button" className="button" onClick={exportData} disabled={exporting || keyUser}>
          <Download size={16} aria-hidden="true" /> {exporting ? t('account.export.working') : t('account.export.button')}
        </button>
        <button type="button" className="button danger" onClick={() => setConfirming(true)} disabled={keyUser}>
          <Trash2 size={16} aria-hidden="true" /> {t('account.delete.button')}
        </button>
      </div>
      <p className="muted small">{t('account.export.help')}</p>
      <ConfirmModal
        open={confirming}
        danger
        title={t('account.delete.title')}
        message={deletionMessage(t, f, config)}
        confirmLabel={t('account.delete.confirm')}
        onConfirm={async () => {
          if (typed.trim().toLowerCase() !== email.toLowerCase()) throw new Error(t('account.delete.mismatch'));
          await deleteAccount();
        }}
        onClose={() => {
          setConfirming(false);
          setTyped('');
        }}
      >
        <FormField id="delete-confirm-email" label={t('account.delete.typeEmail')}>
          <input id="delete-confirm-email" type="email" value={typed} onChange={(e) => setTyped(e.target.value)} autoComplete="off" placeholder={email} />
        </FormField>
      </ConfirmModal>
    </section>
  );
}
