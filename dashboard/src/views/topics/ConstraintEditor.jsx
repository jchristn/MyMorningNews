import { useState } from 'react';
import PropTypes from 'prop-types';
import { Check, Lock, LockOpen, Pencil, Plus, Trash2, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

// Constraints grouped by kind with add, edit, lock, and delete. Locked items survive regeneration.
export default function ConstraintEditor({ topicId, constraints, kinds, onUpserted, onRemoved, disabled = false }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [drafts, setDrafts] = useState({});
  const [editing, setEditing] = useState(null);
  const [busy, setBusy] = useState(null);
  const [error, setError] = useState(null);

  async function run(key, work) {
    setBusy(key);
    setError(null);
    try {
      await work();
      return true;
    } catch (err) {
      setError(err);
      return false;
    } finally {
      setBusy(null);
    }
  }

  async function add(kind) {
    const value = (drafts[kind] || '').trim();
    if (!value) return;
    const ok = await run('add-' + kind, async () => onUpserted(await apiClient.createTopicConstraint(topicId, { kind, value })));
    if (ok) setDrafts((d) => ({ ...d, [kind]: '' }));
  }

  async function saveEdit(c) {
    const ok = await run(c.id, async () => onUpserted(await apiClient.updateTopicConstraint(topicId, c.id, { value: editing.value, note: editing.note })));
    if (ok) setEditing(null);
  }

  return (
    <div className="constraint-editor">
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <div className="constraint-groups">
        {kinds.map((kind) => {
          const items = constraints.filter((c) => c.kind === kind);
          const inputId = 'constraint-add-' + kind;
          return (
            <section key={kind} className="constraint-group" data-testid={'constraints-' + kind}>
              <header className="constraint-group-header">
                <strong>{t('enums.constraintKind.' + kind)}</strong>
                <span className="muted small">{t('constraints.count', { count: items.length })}</span>
              </header>
              <p className="muted small">{t('constraints.help.' + kind)}</p>
              <ul className="constraint-items">
                {items.map((c) =>
                  editing && editing.id === c.id ? (
                    <li key={c.id} className="constraint-item editing">
                      <input value={editing.value} aria-label={t('constraints.value')} onChange={(e) => setEditing({ ...editing, value: e.target.value })} />
                      <input value={editing.note} aria-label={t('constraints.note')} placeholder={t('constraints.note')} onChange={(e) => setEditing({ ...editing, note: e.target.value })} />
                      <span className="row-actions">
                        <button type="button" className="icon-button" aria-label={t('common.save')} title={t('common.save')} disabled={busy === c.id || !editing.value.trim()} onClick={() => saveEdit(c)}>
                          <Check size={14} aria-hidden="true" />
                        </button>
                        <button type="button" className="icon-button" aria-label={t('common.cancel')} title={t('common.cancel')} onClick={() => setEditing(null)}>
                          <X size={14} aria-hidden="true" />
                        </button>
                      </span>
                    </li>
                  ) : (
                    <li key={c.id} className={'constraint-item' + (c.isLocked ? ' locked' : '')}>
                      <span className="constraint-value">
                        <span className={kind === 'FeedUrl' || kind === 'SeedUrl' ? 'mono truncate' : ''} title={c.value}>
                          {c.value}
                        </span>
                        {c.note ? <span className="muted small">{c.note}</span> : null}
                      </span>
                      <span className="row-actions">
                        {c.origin === 'User' ? <StatusBadge tone="info">{t('dimensions.yours')}</StatusBadge> : null}
                        <button
                          type="button"
                          className="icon-button"
                          disabled={disabled || busy === c.id}
                          aria-label={c.isLocked ? t('constraints.unlock', { value: c.value }) : t('constraints.lock', { value: c.value })}
                          title={c.isLocked ? t('dimensions.unlockHint') : t('dimensions.lockHint')}
                          onClick={() => run(c.id, async () => onUpserted(await apiClient.updateTopicConstraint(topicId, c.id, { isLocked: !c.isLocked })))}
                        >
                          {c.isLocked ? <Lock size={14} aria-hidden="true" /> : <LockOpen size={14} aria-hidden="true" />}
                        </button>
                        <button type="button" className="icon-button" disabled={disabled} aria-label={t('constraints.edit', { value: c.value })} title={t('common.edit')} onClick={() => setEditing({ id: c.id, value: c.value, note: c.note || '' })}>
                          <Pencil size={14} aria-hidden="true" />
                        </button>
                        <button
                          type="button"
                          className="icon-button"
                          disabled={disabled || busy === c.id}
                          aria-label={t('constraints.delete', { value: c.value })}
                          title={t('common.delete')}
                          onClick={() =>
                            run(c.id, async () => {
                              await apiClient.deleteTopicConstraint(topicId, c.id);
                              onRemoved(c.id);
                            })
                          }
                        >
                          <Trash2 size={14} aria-hidden="true" />
                        </button>
                      </span>
                    </li>
                  )
                )}
              </ul>
              <form
                className="input-with-button"
                onSubmit={(e) => {
                  e.preventDefault();
                  add(kind);
                }}
              >
                <input
                  id={inputId}
                  value={drafts[kind] || ''}
                  disabled={disabled}
                  aria-label={t('constraints.addTo', { kind: t('enums.constraintKind.' + kind) })}
                  placeholder={t('constraints.placeholder.' + kind)}
                  onChange={(e) => setDrafts((d) => ({ ...d, [kind]: e.target.value }))}
                  autoComplete="off"
                />
                <button type="submit" className="button secondary small" disabled={disabled || busy === 'add-' + kind || !(drafts[kind] || '').trim()}>
                  <Plus size={14} aria-hidden="true" /> {t('common.add')}
                </button>
              </form>
            </section>
          );
        })}
      </div>
    </div>
  );
}

ConstraintEditor.propTypes = {
  topicId: PropTypes.string.isRequired,
  constraints: PropTypes.array.isRequired,
  kinds: PropTypes.arrayOf(PropTypes.string).isRequired,
  onUpserted: PropTypes.func.isRequired,
  onRemoved: PropTypes.func.isRequired,
  disabled: PropTypes.bool
};
