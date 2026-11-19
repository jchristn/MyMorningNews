import { useState } from 'react';
import PropTypes from 'prop-types';
import { Lock, LockOpen, Pencil, Plus, Trash2, User } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { DIMENSION_WEIGHTS } from '../../utils/enums.js';
import ConfirmModal from '../../components/ConfirmModal.jsx';
import EmptyState from '../../components/EmptyState.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import Modal from '../../components/Modal.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';

const EMPTY = { name: '', description: '', rationale: '', weight: 'Normal', includeTerms: '', excludeTerms: '' };

export function splitTerms(text) {
  return String(text || '')
    .split(/[,\n]/)
    .map((s) => s.trim())
    .filter(Boolean);
}

export function dimensionBody(form) {
  return {
    name: form.name.trim(),
    description: form.description,
    rationale: form.rationale,
    weight: form.weight,
    includeTerms: splitTerms(form.includeTerms),
    excludeTerms: splitTerms(form.excludeTerms)
  };
}

function Terms({ label, terms, tone }) {
  if (!terms || terms.length === 0) return null;
  return (
    <div className="term-row">
      <span className="muted small">{label}</span>
      <span className="chip-list">
        {terms.map((term) => (
          <span key={term} className={'term-chip ' + tone}>
            {term}
          </span>
        ))}
      </span>
    </div>
  );
}

Terms.propTypes = { label: PropTypes.string.isRequired, terms: PropTypes.array, tone: PropTypes.string };

// Dimension cards with lock, edit (the dimension becomes yours), weight, delete, and add. Locked and your own
// dimensions survive regeneration. Changes are saved immediately and reported through the callbacks.
export default function DimensionList({ topicId, dimensions, onUpdated, onRemoved, onAdded, disabled = false, hideEmpty = false }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [editing, setEditing] = useState(null);
  const [form, setForm] = useState(EMPTY);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const [busyId, setBusyId] = useState(null);
  const [deleting, setDeleting] = useState(null);

  function open(d) {
    setError(null);
    setEditing(d ? { mode: 'edit', id: d.id } : { mode: 'create' });
    setForm(
      d
        ? { name: d.name, description: d.description || '', rationale: d.rationale || '', weight: d.weight, includeTerms: (d.includeTerms || []).join(', '), excludeTerms: (d.excludeTerms || []).join(', ') }
        : EMPTY
    );
  }

  async function save() {
    setSaving(true);
    setError(null);
    try {
      if (editing.mode === 'create') onAdded(await apiClient.createTopicDimension(topicId, dimensionBody(form)));
      else onUpdated(await apiClient.updateTopicDimension(topicId, editing.id, dimensionBody(form)));
      setEditing(null);
    } catch (err) {
      setError(err);
    } finally {
      setSaving(false);
    }
  }

  async function patch(d, body) {
    setBusyId(d.id);
    setError(null);
    try {
      onUpdated(await apiClient.updateTopicDimension(topicId, d.id, body));
    } catch (err) {
      setError(err);
    } finally {
      setBusyId(null);
    }
  }

  return (
    <div className="dimension-list">
      {!editing ? <ErrorState error={error} inline onDismiss={() => setError(null)} /> : null}
      {dimensions.length === 0 && !hideEmpty ? <EmptyState title={t('dimensions.emptyTitle')} message={t('dimensions.emptyMessage')} /> : null}
      <div className="dimension-grid">
        {dimensions.map((d) => (
          <article key={d.id} className={'dimension-card' + (d.isLocked ? ' locked' : '') + (d.weight === 'Exclude' ? ' excluded' : '')} data-testid={'dimension-' + d.id}>
            <header className="dimension-header">
              <strong className="dimension-name">{d.name}</strong>
              <span className="dimension-badges">
                {d.isLocked ? (
                  <StatusBadge tone="accent">
                    <Lock size={11} aria-hidden="true" /> {t('dimensions.locked')}
                  </StatusBadge>
                ) : null}
                {d.origin === 'User' ? (
                  <StatusBadge tone="info">
                    <User size={11} aria-hidden="true" /> {t('dimensions.yours')}
                  </StatusBadge>
                ) : (
                  <StatusBadge>{t('dimensions.suggested')}</StatusBadge>
                )}
              </span>
            </header>
            {d.description ? <p className="dimension-description">{d.description}</p> : null}
            {d.rationale ? <p className="muted small">{t('dimensions.why', { reason: d.rationale })}</p> : null}
            <Terms label={t('dimensions.include')} terms={d.includeTerms} tone="include" />
            <Terms label={t('dimensions.exclude')} terms={d.excludeTerms} tone="exclude" />
            <footer className="dimension-footer">
              <div className="segmented" role="radiogroup" aria-label={t('dimensions.weightFor', { name: d.name })}>
                {DIMENSION_WEIGHTS.map((w) => (
                  <button
                    type="button"
                    key={w}
                    role="radio"
                    aria-checked={d.weight === w}
                    className={d.weight === w ? 'active' : ''}
                    disabled={disabled || busyId === d.id}
                    onClick={() => d.weight !== w && patch(d, { weight: w })}
                  >
                    {t('enums.dimensionWeight.' + w)}
                  </button>
                ))}
              </div>
              <span className="row-actions">
                <button
                  type="button"
                  className="icon-button"
                  disabled={disabled || busyId === d.id}
                  aria-label={d.isLocked ? t('dimensions.unlock', { name: d.name }) : t('dimensions.lock', { name: d.name })}
                  title={d.isLocked ? t('dimensions.unlockHint') : t('dimensions.lockHint')}
                  onClick={() => patch(d, { isLocked: !d.isLocked })}
                >
                  {d.isLocked ? <Lock size={15} aria-hidden="true" /> : <LockOpen size={15} aria-hidden="true" />}
                </button>
                <button type="button" className="icon-button" disabled={disabled} aria-label={t('dimensions.edit', { name: d.name })} title={t('common.edit')} onClick={() => open(d)}>
                  <Pencil size={15} aria-hidden="true" />
                </button>
                <button type="button" className="icon-button" disabled={disabled} aria-label={t('dimensions.delete', { name: d.name })} title={t('common.delete')} onClick={() => setDeleting(d)}>
                  <Trash2 size={15} aria-hidden="true" />
                </button>
              </span>
            </footer>
          </article>
        ))}
      </div>
      <div>
        <button type="button" className="button secondary small" disabled={disabled} onClick={() => open(null)}>
          <Plus size={14} aria-hidden="true" /> {t('dimensions.add')}
        </button>
      </div>

      <Modal
        open={Boolean(editing)}
        title={editing && editing.mode === 'create' ? t('dimensions.addTitle') : t('dimensions.editTitle')}
        subtitle={editing && editing.mode === 'edit' ? t('dimensions.editHint') : null}
        onClose={() => setEditing(null)}
        footer={
          <>
            <button type="button" className="button secondary" onClick={() => setEditing(null)}>
              {t('common.cancel')}
            </button>
            <button type="button" className="button primary" onClick={save} disabled={saving || !form.name.trim()}>
              {saving ? t('common.saving') : t('common.save')}
            </button>
          </>
        }
      >
        <ErrorState error={error} inline onDismiss={() => setError(null)} />
        <div className="form-grid">
          <FormField id="dim-name" label={t('dimensions.fields.name')} wide>
            <input id="dim-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} autoComplete="off" />
          </FormField>
          <FormField id="dim-description" label={t('dimensions.fields.description')} wide>
            <textarea id="dim-description" rows={2} value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} />
          </FormField>
          <FormField id="dim-rationale" label={t('dimensions.fields.rationale')} wide>
            <textarea id="dim-rationale" rows={2} value={form.rationale} onChange={(e) => setForm({ ...form, rationale: e.target.value })} />
          </FormField>
          <FormField id="dim-weight" label={t('dimensions.fields.weight')}>
            <select id="dim-weight" value={form.weight} onChange={(e) => setForm({ ...form, weight: e.target.value })}>
              {DIMENSION_WEIGHTS.map((w) => (
                <option key={w} value={w}>
                  {t('enums.dimensionWeight.' + w)}
                </option>
              ))}
            </select>
          </FormField>
          <FormField id="dim-include" label={t('dimensions.fields.includeTerms')} help={t('dimensions.fields.termsHelp')} wide>
            <input id="dim-include" value={form.includeTerms} onChange={(e) => setForm({ ...form, includeTerms: e.target.value })} autoComplete="off" />
          </FormField>
          <FormField id="dim-exclude" label={t('dimensions.fields.excludeTerms')} help={t('dimensions.fields.termsHelp')} wide>
            <input id="dim-exclude" value={form.excludeTerms} onChange={(e) => setForm({ ...form, excludeTerms: e.target.value })} autoComplete="off" />
          </FormField>
        </div>
      </Modal>

      <ConfirmModal
        open={Boolean(deleting)}
        title={t('dimensions.deleteTitle')}
        message={deleting ? t('dimensions.deleteMessage', { name: deleting.name }) : ''}
        confirmLabel={t('common.delete')}
        danger
        onClose={() => setDeleting(null)}
        onConfirm={async () => {
          await apiClient.deleteTopicDimension(topicId, deleting.id);
          onRemoved(deleting.id);
        }}
      />
    </div>
  );
}

DimensionList.propTypes = {
  topicId: PropTypes.string.isRequired,
  dimensions: PropTypes.array.isRequired,
  onUpdated: PropTypes.func.isRequired,
  onRemoved: PropTypes.func.isRequired,
  onAdded: PropTypes.func.isRequired,
  disabled: PropTypes.bool,
  hideEmpty: PropTypes.bool
};
