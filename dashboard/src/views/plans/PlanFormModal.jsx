import { useState } from 'react';
import PropTypes from 'prop-types';
import { Plus, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import { fieldErrors } from '../../utils/errors.js';
import { FEATURE_CATALOG, featureSpec, featureValueError, groupedFeatureKeys, isValidPlanKey, planFeaturesBody } from '../../utils/promo.js';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import Modal from '../../components/Modal.jsx';
import FeatureInput from './FeatureInput.jsx';
import { featureName } from './planParts.jsx';

// Create or edit a plan: name, description, and its values. Known values get typed controls grouped by area; other
// keys are free text under "Advanced". A value left unset is removed on save. item is { plan, features } when
// editing, or null to create. The plan key cannot change once created.
export default function PlanFormModal({ item, onClose, onSaved }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const editing = Boolean(item);
  const original = (item && item.features) || {};
  const [form, setForm] = useState(() => ({
    key: item ? item.plan.key : '',
    name: item ? item.plan.name : '',
    description: item ? item.plan.description || '' : ''
  }));
  const [values, setValues] = useState(() => Object.fromEntries(Object.keys(FEATURE_CATALOG).map((k) => [k, String(original[k] ?? '')])));
  const [custom, setCustom] = useState(() =>
    Object.keys(original)
      .filter((k) => !FEATURE_CATALOG[k])
      .sort()
      .map((key) => ({ key, value: String(original[key] ?? ''), locked: true }))
  );
  const [advancedOpen] = useState(() => custom.length > 0);
  const [touched, setTouched] = useState(false);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const server = fieldErrors(error);
  const keyError = !editing && !isValidPlanKey(form.key) ? t('plans.validation.key') : null;
  const nameError = !form.name.trim() ? t('plans.validation.name') : null;
  const valueErrors = Object.fromEntries(Object.keys(values).map((k) => [k, featureValueError(k, values[k], { allowEmpty: true })]).filter(([, e]) => e));
  const customKeys = custom.map((r) => r.key.trim()).filter(Boolean);
  const duplicate = customKeys.find((k, i) => customKeys.indexOf(k) !== i || Boolean(FEATURE_CATALOG[k]));
  const invalid = Boolean(keyError || nameError || duplicate || Object.keys(valueErrors).length > 0);

  function setRow(index, patch) {
    setCustom((list) => list.map((r, i) => (i === index ? { ...r, ...patch } : r)));
  }

  async function save() {
    setTouched(true);
    if (invalid) return;
    setSaving(true);
    setError(null);
    try {
      const features = planFeaturesBody(values, custom.map((r) => ({ key: r.key, value: r.value })), original);
      const body = { name: form.name.trim(), description: form.description.trim(), features };
      if (editing) onSaved(await apiClient.updatePlan(item.plan.id, body));
      else onSaved(await apiClient.createPlan({ ...body, key: form.key.trim().toLowerCase() }));
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
      <h3 className="plan-form-heading">{t('plans.fields.features')}</h3>
      <p className="muted small">{t('plans.fields.featuresHelp')}</p>
      {groupedFeatureKeys().map((g) => (
        <fieldset className="plan-feature-fieldset" key={g.group}>
          <legend>{t('plans.groups.' + g.group)}</legend>
          {g.keys.map((key) => {
            const id = 'plan-feature-' + key.replace(/\./g, '-');
            const name = featureName(t, key);
            const spec = featureSpec(key);
            const problem = touched && valueErrors[key] ? t(valueErrors[key]) : null;
            return (
              <div className={'feature-edit-row' + (problem ? ' has-error' : '')} key={key}>
                <div className="feature-edit-label">
                  {spec.kind === 'boolean' ? (
                    <span className="field-label" id={id + '-label'}>
                      {name}
                    </span>
                  ) : (
                    <label className="field-label" htmlFor={id}>
                      {name}
                    </label>
                  )}
                  <span className="mono muted small">{key}</span>
                  {spec.alias ? <span className="muted small">{t('plans.fields.aliasOf', { key: spec.alias })}</span> : null}
                </div>
                <div className="feature-edit-control">
                  <FeatureInput id={id} featureKey={key} name={name} value={values[key]} onChange={(v) => setValues((all) => ({ ...all, [key]: v }))} labelledBy={spec.kind === 'boolean' ? id + '-label' : undefined} allowUnset invalid={Boolean(problem)} />
                  {problem ? (
                    <span className="field-error" role="alert">
                      {problem}
                    </span>
                  ) : null}
                </div>
              </div>
            );
          })}
        </fieldset>
      ))}
      <details className="plan-advanced" open={advancedOpen || undefined}>
        <summary>{t('plans.fields.customTitle', { count: custom.length })}</summary>
        <p className="muted small">{t('plans.fields.customHelp')}</p>
        {touched && duplicate ? (
          <p className="field-error" role="alert">
            {t('plans.validation.duplicate', { key: duplicate })}
          </p>
        ) : null}
        <div className="plan-feature-rows">
          {custom.map((row, index) => (
            <div className="plan-feature-row" key={index}>
              <input className="mono" value={row.key} onChange={(e) => setRow(index, { key: e.target.value })} aria-label={t('plans.fields.featureKey')} placeholder={t('plans.fields.featureKey')} disabled={row.locked} />
              <input className="mono" value={row.value} onChange={(e) => setRow(index, { value: e.target.value })} aria-label={t('plans.fields.featureValue', { key: row.key || t('plans.fields.featureKey') })} placeholder={t('plans.notSet')} />
              <button
                type="button"
                className="icon-button"
                onClick={() => (row.locked ? setRow(index, { value: '' }) : setCustom((list) => list.filter((_, i) => i !== index)))}
                aria-label={t('plans.fields.removeKey', { key: row.key || t('plans.fields.featureKey') })}
                title={t('plans.fields.removeKey', { key: row.key || t('plans.fields.featureKey') })}
              >
                <Trash2 size={14} aria-hidden="true" />
              </button>
            </div>
          ))}
        </div>
        <button type="button" className="button secondary small" onClick={() => setCustom((list) => [...list, { key: '', value: '', locked: false }])}>
          <Plus size={14} aria-hidden="true" /> {t('plans.fields.addFeature')}
        </button>
      </details>
    </Modal>
  );
}

PlanFormModal.propTypes = { item: PropTypes.object, onClose: PropTypes.func.isRequired, onSaved: PropTypes.func.isRequired };
