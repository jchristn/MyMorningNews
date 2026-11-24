import { useCallback, useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link2, Share2, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import useClientConfig from '../../hooks/useClientConfig.js';
import useFormat from '../../hooks/useFormat.js';
import { describeError } from '../../utils/errors.js';
import CopyButton from '../../components/CopyButton.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import Modal from '../../components/Modal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

export const SHARE_MIN_DAYS = 1;
export const SHARE_MAX_DAYS = 14;
export const SHARE_DEFAULT_DAYS = 7;

// Live, Expired, or Revoked: a share stops working when it is revoked or its expiry has passed.
export function shareStatus(share, now = Date.now()) {
  if (!share) return 'Expired';
  if (share.revokedUtc) return 'Revoked';
  const expires = Date.parse(share.expiresUtc);
  return Number.isFinite(expires) && expires > now ? 'Live' : 'Expired';
}

// The day choices the server accepts: 1 up to its maximum (14 unless the server says otherwise).
export function shareDayOptions(config) {
  const max = Math.max(SHARE_MIN_DAYS, Math.min(365, Number(config && config.sharingMaxExpiryDays) || SHARE_MAX_DAYS));
  const wanted = Number(config && config.sharingDefaultExpiryDays) || SHARE_DEFAULT_DAYS;
  const days = [];
  for (let d = SHARE_MIN_DAYS; d <= max; d++) days.push(d);
  return { days, max, initial: Math.max(SHARE_MIN_DAYS, Math.min(max, wanted)) };
}

const STATUS_TONE = { Live: 'success', Expired: 'warning', Revoked: 'neutral' };

function LinkRow({ id, label, url }) {
  return (
    <div className="share-link-row">
      <label htmlFor={id} className="field-label">
        {label}
      </label>
      <div className="share-link-field">
        <input id={id} type="text" className="mono" readOnly value={url} dir="ltr" onFocus={(e) => e.target.select()} />
        <CopyButton value={url} labelKey="briefings.share.copy" />
      </div>
    </div>
  );
}

LinkRow.propTypes = { id: PropTypes.string.isRequired, label: PropTypes.string.isRequired, url: PropTypes.string.isRequired };

// The Share dialog for one briefing: create a public share (a reading link and a listening link that need no
// sign-in, shown once), choose how many days it lasts (1 to 14), and see, revoke, or remove existing shares with
// their expiry and how often they were opened.
export function ShareDialog({ briefing, open, onClose, config = null }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const options = shareDayOptions(config);
  const [days, setDays] = useState(options.initial);
  const [showName, setShowName] = useState(false);
  const [shares, setShares] = useState(null);
  const [error, setError] = useState(null);
  const [created, setCreated] = useState(null);
  const [busy, setBusy] = useState(null);
  const briefingId = briefing.id;

  const load = useCallback(async () => {
    try {
      const page = await apiClient.listBriefingShares(briefingId, { pageSize: 100 });
      setShares((page && page.objects) || []);
      setError(null);
    } catch (err) {
      setError(err);
    }
  }, [apiClient, briefingId]);

  useEffect(() => {
    if (!open) return;
    setCreated(null);
    setDays(options.initial);
    setShowName(false);
    load();
    // options.initial changes only with the server configuration.
  }, [open, load, options.initial]);

  async function create() {
    setBusy('create');
    try {
      const result = await apiClient.createBriefingShare(briefingId, { expiresInDays: days, showOwnerName: showName });
      setCreated(result);
      setError(null);
      await load();
    } catch (err) {
      setError(err);
    } finally {
      setBusy(null);
    }
  }

  async function remove(share, purge) {
    setBusy(share.id);
    try {
      await apiClient.revokeBriefingShare(briefingId, share.id, { purge });
      if (created && created.share && created.share.id === share.id) setCreated(null);
      addNotification(t(purge ? 'briefings.share.removed' : 'briefings.share.revoked'), 'success');
      await load();
    } catch (err) {
      addNotification(describeError(err, t).message, 'error', 8000);
    } finally {
      setBusy(null);
    }
  }

  return (
    <Modal open={open} title={t('briefings.share.title')} subtitle={briefing.title || ''} onClose={onClose} size="large">
      <div className="stack share-dialog">
        <p className="muted">{t('briefings.share.about')}</p>
        <ErrorState error={error} inline onDismiss={() => setError(null)} />

        {created ? (
          <section className="share-created" aria-label={t('briefings.share.newLinks')} data-testid="share-created">
            <strong>{t('briefings.share.newLinks')}</strong>
            <LinkRow id={'share-link-' + briefingId} label={t('briefings.share.link')} url={created.url || created.readUrl} />
            <p className="small">
              <span data-testid="share-expires">{t('briefings.share.expiresOn', { date: f.dateTime(created.share.expiresUtc) })}</span>
            </p>
            {created.audioReady ? null : <p className="muted small">{t('briefings.share.noAudio')}</p>}
            <p className="muted small">{t('briefings.share.shownOnce')}</p>
          </section>
        ) : null}

        <section className="share-create">
          <div className="share-create-fields">
            <div className="form-field">
              <label htmlFor={'share-days-' + briefingId} className="field-label">
                {t('briefings.share.days')}
              </label>
              <select id={'share-days-' + briefingId} value={days} disabled={busy !== null} onChange={(e) => setDays(Number(e.target.value))}>
                {options.days.map((d) => (
                  <option key={d} value={d}>
                    {f.number(d)}
                  </option>
                ))}
              </select>
              <span className="field-help muted">{t('briefings.share.daysHelp', { min: f.number(SHARE_MIN_DAYS), max: f.number(options.max) })}</span>
            </div>
            <div className="form-field">
              <label className="checkbox-row" htmlFor={'share-name-' + briefingId}>
                <input id={'share-name-' + briefingId} type="checkbox" checked={showName} disabled={busy !== null} onChange={(e) => setShowName(e.target.checked)} />
                {t('briefings.share.showName')}
              </label>
              <span className="field-help muted">{t('briefings.share.showNameHelp')}</span>
            </div>
          </div>
          <button type="button" className="button primary" onClick={create} disabled={busy !== null}>
            <Link2 size={14} aria-hidden="true" /> {busy === 'create' ? t('common.working') : created ? t('briefings.share.createAnother') : t('briefings.share.create')}
          </button>
        </section>

        <section>
          <h3 className="share-list-title">{t('briefings.share.existing')}</h3>
          {shares === null && !error ? <LoadingState compact /> : null}
          {shares !== null && shares.length === 0 ? <p className="muted small">{t('briefings.share.none')}</p> : null}
          {shares !== null && shares.length > 0 ? (
            <ul className="share-list">
              {shares.map((s) => {
                const status = shareStatus(s);
                const live = status === 'Live';
                return (
                  <li key={s.id} data-testid={'share-' + s.id}>
                    <span className="share-list-main">
                      <span className="share-list-head">
                        <StatusBadge tone={STATUS_TONE[status]}>{t('briefings.share.status.' + status)}</StatusBadge>
                        <code className="small" dir="ltr">
                          {s.tokenPrefix}...
                        </code>
                      </span>
                      <span className="muted small">
                        {status === 'Revoked'
                          ? t('briefings.share.revokedOn', { date: f.dateTime(s.revokedUtc) })
                          : live
                            ? t('briefings.share.expiresOn', { date: f.dateTime(s.expiresUtc) })
                            : t('briefings.share.expiredOn', { date: f.dateTime(s.expiresUtc) })}
                      </span>
                      <span className="muted small">
                        {t('briefings.share.views', { count: f.number(s.viewCount || 0) })}
                        {s.lastViewedUtc ? ' · ' + t('briefings.share.lastOpened', { date: f.dateTime(s.lastViewedUtc) }) : ''}
                      </span>
                    </span>
                    {live ? (
                      <button type="button" className="button danger small" disabled={busy !== null} onClick={() => remove(s, false)}>
                        {t('briefings.share.revoke')}
                      </button>
                    ) : (
                      <button type="button" className="icon-button" disabled={busy !== null} onClick={() => remove(s, true)} aria-label={t('briefings.share.remove')} title={t('briefings.share.remove')}>
                        <Trash2 size={14} aria-hidden="true" />
                      </button>
                    )}
                  </li>
                );
              })}
            </ul>
          ) : null}
        </section>
      </div>
    </Modal>
  );
}

ShareDialog.propTypes = {
  briefing: PropTypes.shape({ id: PropTypes.string.isRequired, title: PropTypes.string }).isRequired,
  open: PropTypes.bool.isRequired,
  onClose: PropTypes.func.isRequired,
  config: PropTypes.object
};

// The Share button on the briefing reader. Hidden when the server has public sharing turned off; disabled until the
// briefing is ready.
export default function BriefingShare({ briefing, disabled = false }) {
  const { t } = useTranslation();
  const { config } = useClientConfig();
  const [open, setOpen] = useState(false);
  if (config && config.sharingEnabled === false) return null;
  return (
    <>
      <button type="button" className="button secondary small" disabled={disabled || briefing.status !== 'Ready'} onClick={() => setOpen(true)}>
        <Share2 size={14} aria-hidden="true" /> {t('briefings.share.action')}
      </button>
      {open ? <ShareDialog briefing={briefing} open={open} onClose={() => setOpen(false)} config={config} /> : null}
    </>
  );
}

BriefingShare.propTypes = {
  briefing: PropTypes.shape({ id: PropTypes.string.isRequired, title: PropTypes.string, status: PropTypes.string }).isRequired,
  disabled: PropTypes.bool
};
