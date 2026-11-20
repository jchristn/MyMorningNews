import { useState } from 'react';
import PropTypes from 'prop-types';
import { Plus, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { fieldErrors } from '../../utils/errors.js';
import { KNOWN_FEATURES, featuresBody, isValidPlanKey, orderedFeatures } from '../../utils/promo.js';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import Modal from '../../components/Modal.jsx';

// Create or edit a plan: name, description, and entitlement values. Clearing a value removes that key on save.
// item is { plan, features } when editing, or null to create.
export default function PlanFormModal({ item, onClose, onSaved }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const editing = Boolean(item);
  const [form, setForm] = useState(() => ({
    key: item ? item.plan.key : '',
    name: item ? item.plan.name : '',
    description: item ? item.plan.description || '' : ''
  }));
  const [rows, setRows] = useState(() => (item ? orderedFeatures(item.features) : KNOWN_FEATURES.map((key) => ({ key, value: '' }))));
  const [touched, setTouched] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const server = fieldErrors(error);
  const keyError = !editing && !isValidPlanKey(form.key) ? t('plans.validation.key') : null;
  const nameError = !form.name.trim() ? t('plans.validation.name') : null;
  const keys = rows.map((r) => r.key.trim()).filter(Boolean);
  const duplicate = keys.find((k, i) => keys.indexOf(k) !== i);

  function setRow(index, patch) {
    setRows((list) => list.map((r, i) => (i === index ? { ...r, ...patch } : r)));
  }

  async function save() {
    setTouched(true);
    if (keyError || nameError || duplicate) return;
    setSaving(true);
    setError(null);
    try {
      const body = { name: form.name.trim(), description: form.description.trim(), features: featuresBody(rows, item ? item.features : {}) };
      if (editing) onSaved(await apiClient.updatePlan(item.plan.id, body));
      else {
        for (const k of Object.keys(body.features)) if (!body.features[k]) delete body.features[k];
        onSaved(await apiClient.createPlan({ ...body, key: form.key.trim().toLowerCase() }));
      }
    } catch (err) {
      setError(err);
    } finally {
      setSaving(false);
    }
  }

  return (
    <Modal
      open
      size="large"
      title={editing ? t('plans.edit.title', { name: item.plan.name }) : t('plans.create.title')}
      onClose={() => !saving && onClose()}
      footer={
        <>
          <button type="button" className="button secondary" onClick={onClose} disabled={saving}>
            {t('common.cancel')}
          </button>
          <button type="button" className="button primary" onClick={save} disabled={saving}>
            {saving ? t('common.saving') : t('common.save')}
          </button>
        </>
      }
    >
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <div className="form-grid">
        <FormField id="plan-key" label={t('plans.fields.key')} help={editing ? t('plans.fields.keyLocked') : t('plans.fields.keyHelp')} error={(touched && keyError) || server.key}>
          <input id="plan-key" className="mono" value={form.key} disabled={editing} onChange={(e) => setForm({ ...form, key: e.target.value.toLowerCase() })} maxLength={63} />
        </FormField>
        <FormField id="plan-name" label={t('plans.fields.name')} error={(touched && nameError) || server.name}>
          <input id="plan-name" value={form.name} onChange={(e) => setForm({ ...form, name: e.target.value })} maxLength={128} />
        </FormField>
        <FormField id="plan-description" label={t('plans.fields.description')} wide>
          <input id="plan-description" value={form.description} onChange={(e) => setForm({ ...form, description: e.target.value })} maxLength={500} />
        </FormField>
      </div>
      <h3>{t('plans.fields.features')}</h3>
      <p className="muted small">{t('plans.fields.featuresHelp')}</p>
      {touched && duplicate ? (
        <p className="field-error" role="alert">
          {t('plans.validation.duplicate', { key: duplicate })}
        </p>
      ) : null}
      <div className="plan-feature-rows">
        {rows.map((row, index) => (
          <div className="plan-feature-row" key={index}>
            <input
              className="mono"
              value={row.key}
              onChange={(e) => setRow(index, { key: e.target.value })}
              aria-label={t('plans.fields.featureKey')}
              placeholder={t('plans.fields.featureKey')}
              disabled={editing && Object.prototype.hasOwnProperty.call(item.features || {}, row.key)}
            />
            <input className="mono" value={row.value} onChange={(e) => setRow(index, { value: e.target.value })} aria-label={t('plans.fields.featureValue', { key: row.key })} placeholder={t('plans.fields.emptyRemoves')} />
            <button type="button" className="icon-button" onClick={() => setRow(index, { value: '' })} aria-label={t('plans.fields.clearValue', { key: row.key })} title={t('plans.fields.clearValue', { key: row.key })}>
              <X size={14} aria-hidden="true" />
            </button>
          </div>
        ))}
      </div>
      <button type="button" className="button secondary small" onClick={() => setRows((list) => [...list, { key: '', value: '' }])}>
        <Plus size={14} aria-hidden="true" /> {t('plans.fields.addFeature')}
      </button>
    </Modal>
  );
}

PlanFormModal.propTypes = { item: PropTypes.object, onClose: PropTypes.func.isRequired, onSaved: PropTypes.func.isRequired };
