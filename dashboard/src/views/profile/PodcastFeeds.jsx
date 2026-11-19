import { useCallback, useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Plus, Rss, ShieldAlert, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import CopyButton from '../../components/CopyButton.jsx';
import DateCell from '../../components/DateCell.jsx';
import EmptyState from '../../components/EmptyState.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import Modal from '../../components/Modal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

// True when a feed URL points at a loopback address (127.0.0.0/8, ::1, or 0.0.0.0), which a phone cannot reach.
export function isLoopbackUrl(url) {
  try {
    const host = new URL(url).hostname.toLowerCase();
    return /^127\.\d+\.\d+\.\d+$/.test(host) || host === '[::1]' || host === '::1' || host === '0.0.0.0';
  } catch {
    return false;
  }
}

// The secret and feed URL of a token just created. They are shown once; closing the modal forgets them.
export function FeedCreatedModal({ created, onClose }) {
  const { t } = useTranslation();
  return (
    <Modal
      open={Boolean(created)}
      title={t('feeds.created.title')}
      subtitle={created && created.token ? created.token.name : null}
      onClose={onClose}
      dismissible={false}
      footer={
        <button type="button" className="button primary" onClick={onClose}>
          {t('feeds.created.done')}
        </button>
      }
    >
      {created ? (
        <div className="stack">
          <div className="notice notice-warning">
            <ShieldAlert size={14} aria-hidden="true" /> {t('feeds.created.warning')}
          </div>
          <div className="reveal-value" data-testid="feed-url">
            <span className="field-label">{t('feeds.created.feedUrl')}</span>
            <code>{created.feedUrl}</code>
            <CopyButton value={created.feedUrl} labelKey="feeds.created.copyUrl" />
          </div>
          <div className="reveal-value" data-testid="feed-secret">
            <span className="field-label">{t('feeds.created.secret')}</span>
            <code>{created.secret}</code>
            <CopyButton value={created.secret} labelKey="feeds.created.copySecret" />
          </div>
          {isLoopbackUrl(created.feedUrl) ? <div className="notice notice-warning">{t('feeds.help.loopback')}</div> : null}
        </div>
      ) : null}
    </Modal>
  );
}

FeedCreatedModal.propTypes = { created: PropTypes.shape({ token: PropTypes.object, secret: PropTypes.string, feedUrl: PropTypes.string }), onClose: PropTypes.func.isRequired };

// Profile: private podcast feeds. Lists the signed-in user's feed tokens, creates one (all audio or one edition) and
// shows its secret and URL once, revokes with a confirm, and explains how to subscribe from a phone.
export default function PodcastFeeds() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [tokens, setTokens] = useState(null);
  const [editions, setEditions] = useState([]);
  const [error, setError] = useState(null);
  const [name, setName] = useState('');
  const [editionId, setEditionId] = useState('');
  const [creating, setCreating] = useState(false);
  const [createError, setCreateError] = useState(null);
  const [created, setCreated] = useState(null);
  const [revoking, setRevoking] = useState(null);

  const load = useCallback(async () => {
    try {
      const list = await apiClient.listFeedTokens();
      setTokens(Array.isArray(list) ? list : (list && list.items) || []);
      setError(null);
    } catch (err) {
      setError(err);
    }
  }, [apiClient]);

  useEffect(() => {
    load();
    let cancelled = false;
    apiClient
      .listEditions({ pageSize: 100, sortBy: 'name', sortDirection: 'Ascending' })
      .then((r) => !cancelled && setEditions((r && r.items) || []))
      .catch(() => {});
    return () => {
      cancelled = true;
    };
  }, [apiClient, load]);

  const editionNames = useMemo(() => new Map(editions.map((e) => [e.id, e.name])), [editions]);

  async function create(e) {
    e.preventDefault();
    setCreating(true);
    setCreateError(null);
    try {
      const result = await apiClient.createFeedToken({ name: name.trim(), editionId: editionId || null });
      setCreated(result);
      setName('');
      setEditionId('');
      await load();
    } catch (err) {
      setCreateError(err);
    } finally {
      setCreating(false);
    }
  }

  return (
    <section className="panel" aria-labelledby="podcast-feeds-title">
      <div className="section-title">
        <h2 id="podcast-feeds-title">
          <Rss size={16} aria-hidden="true" /> {t('feeds.title')}
        </h2>
      </div>
      <p className="muted small">{t('feeds.summary')}</p>

      <form className="feed-create" onSubmit={create}>
        <ErrorState error={createError} inline onDismiss={() => setCreateError(null)} />
        <div className="form-grid">
          <FormField id="feed-name" label={t('feeds.fields.name')} help={t('feeds.fields.nameHelp')}>
            <input id="feed-name" value={name} onChange={(e) => setName(e.target.value)} placeholder={t('feeds.fields.namePlaceholder')} autoComplete="off" />
          </FormField>
          <FormField id="feed-edition" label={t('feeds.fields.edition')} help={t('feeds.fields.editionHelp')}>
            <select id="feed-edition" value={editionId} onChange={(e) => setEditionId(e.target.value)}>
              <option value="">{t('feeds.allAudio')}</option>
              {editions.map((ed) => (
                <option key={ed.id} value={ed.id}>
                  {ed.name}
                </option>
              ))}
            </select>
          </FormField>
        </div>
        <div className="form-actions">
          <button type="submit" className="button primary" disabled={creating || !name.trim()}>
            <Plus size={14} aria-hidden="true" /> {creating ? t('common.working') : t('feeds.create')}
          </button>
        </div>
      </form>

      <ErrorState error={error} inline onRetry={load} />
      {tokens === null && !error ? <LoadingState compact /> : null}
      {tokens && tokens.length === 0 ? <EmptyState icon={Rss} title={t('feeds.empty.title')} message={t('feeds.empty.message')} /> : null}
      {tokens && tokens.length > 0 ? (
        <div className="data-table">
          <div className="table-scroll">
          <table aria-label={t('feeds.title')}>
            <thead>
              <tr>
                <th>{t('feeds.columns.name')}</th>
                <th>{t('feeds.columns.prefix')}</th>
                <th>{t('feeds.columns.edition')}</th>
                <th>{t('feeds.columns.created')}</th>
                <th>{t('feeds.columns.lastUsed')}</th>
                <th>{t('feeds.columns.status')}</th>
                <th>
                  <span className="sr-only">{t('feeds.columns.actions')}</span>
                </th>
              </tr>
            </thead>
            <tbody>
              {tokens.map((tok) => (
                <tr key={tok.id}>
                  <td>
                    <strong>{tok.name || t('feeds.unnamed')}</strong>
                  </td>
                  <td>
                    <span className="mono">{t('feeds.prefix', { prefix: tok.tokenPrefix || '' })}</span>
                  </td>
                  <td>{tok.editionId ? editionNames.get(tok.editionId) || tok.editionId : <span className="muted">{t('feeds.allAudio')}</span>}</td>
                  <td>
                    <DateCell value={tok.createdUtc} />
                  </td>
                  <td>{tok.lastUsedUtc ? <DateCell value={tok.lastUsedUtc} /> : <span className="muted small">{t('feeds.neverUsed')}</span>}</td>
                  <td>
                    {tok.revokedUtc ? (
                      <span className="date-cell">
                        <StatusBadge tone="neutral">{t('feeds.revoked')}</StatusBadge>
                        <DateCell value={tok.revokedUtc} />
                      </span>
                    ) : (
                      <StatusBadge tone="success">{t('common.active')}</StatusBadge>
                    )}
                  </td>
                  <td>
                    {!tok.revokedUtc ? (
                      <button type="button" className="button danger small" onClick={() => setRevoking(tok)} aria-label={t('feeds.revokeNamed', { name: tok.name || t('feeds.unnamed') })}>
                        <Trash2 size={14} aria-hidden="true" /> {t('feeds.revoke')}
                      </button>
                    ) : null}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          </div>
        </div>
      ) : null}

      <details className="feed-help">
        <summary>{t('feeds.help.title')}</summary>
        <ol className="small">
          <li>{t('feeds.help.step1')}</li>
          <li>{t('feeds.help.apple')}</li>
          <li>{t('feeds.help.overcast')}</li>
          <li>{t('feeds.help.publicUrl')}</li>
        </ol>
      </details>

      <FeedCreatedModal created={created} onClose={() => setCreated(null)} />
      <ConfirmModal
        open={Boolean(revoking)}
        title={t('feeds.revokeConfirm.title')}
        message={revoking ? t('feeds.revokeConfirm.message', { name: revoking.name || t('feeds.unnamed') }) : ''}
        confirmLabel={t('feeds.revoke')}
        danger
        onClose={() => setRevoking(null)}
        onConfirm={async () => {
          await apiClient.revokeFeedToken(revoking.id);
          addNotification(t('feeds.revokeConfirm.done'), 'success');
          await load();
        }}
      />
    </section>
  );
}
