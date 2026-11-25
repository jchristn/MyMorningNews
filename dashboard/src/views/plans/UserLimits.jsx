import { Fragment, useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { Pencil, Undo2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../context/AppContext.jsx';
import { useAuth } from '../../context/AuthContext.jsx';
import { featureSpec, featureValueError, fromLocalInput, groupedFeatureKeys } from '../../utils/promo.js';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import FeatureInput from './FeatureInput.jsx';
import { FeatureValue, featureName } from './planParts.jsx';

// The focused editor for one override, shown under its row: a typed value, a required reason, and an optional expiry.
function OverrideEditor({ featureKey, name, planValue, initialValue, busy, onSave, onCancel }) {
  const { t } = useTranslation();
  const [value, setValue] = useState(initialValue);
  const [reason, setReason] = useState('');
  const [expires, setExpires] = useState('');
  const [touched, setTouched] = useState(false);
  const formRef = useRef(null);
  const base = 'override-' + featureKey.replace(/\./g, '-');
  const valueError = featureValueError(featureKey, value);
  const expiresIso = fromLocalInput(expires);
  const expiresError = expires && (!expiresIso || new Date(expiresIso) <= new Date()) ? t('plans.validation.future') : null;
  const reasonError = !reason.trim() ? t('plans.validation.reason') : null;

  useEffect(() => {
    const first = formRef.current ? formRef.current.querySelector('input:not([disabled]), select, textarea') : null;
    if (first) first.focus();
  }, []);

  function submit(event) {
    event.preventDefault();
    setTouched(true);
    if (valueError || reasonError || expiresError) return;
    onSave({ featureKey, value: value.trim(), reason: reason.trim(), expiresUtc: expiresIso });
  }

  return (
    <form
      ref={formRef}
      className="override-editor"
      aria-label={t('plans.overrides.editorTitle', { name })}
      onSubmit={submit}
      onKeyDown={(e) => {
        if (e.key === 'Escape') {
          e.stopPropagation();
          onCancel();
        }
      }}
    >
      <div className="form-grid">
        <div className="form-field wide">
          {featureSpec(featureKey).kind === 'boolean' ? (
            <span className="field-label" id={base + '-label'}>
              {t('plans.overrides.value')}
            </span>
          ) : (
            <label className="field-label" htmlFor={base + '-value'}>
              {t('plans.overrides.value')}
            </label>
          )}
          <FeatureInput id={base + '-value'} featureKey={featureKey} name={name} value={value} onChange={setValue} labelledBy={featureSpec(featureKey).kind === 'boolean' ? base + '-label' : undefined} describedBy={base + '-plan'} disabled={busy} invalid={touched && Boolean(valueError)} />
          {touched && valueError ? (
            <span className="field-error" role="alert">
              {t(valueError)}
            </span>
          ) : (
            <span className="field-help muted" id={base + '-plan'}>
              {t('plans.overrides.planValueHelp')} <FeatureValue featureKey={featureKey} value={planValue} />
            </span>
          )}
        </div>
        <FormField id={base + '-reason'} label={t('plans.overrides.reason')} help={t('plans.overrides.reasonHelp')} error={(touched && reasonError) || undefined}>
          <input id={base + '-reason'} value={reason} maxLength={1000} disabled={busy} aria-required="true" onChange={(e) => setReason(e.target.value)} />
        </FormField>
        <FormField id={base + '-expires'} label={t('plans.overrides.expires')} help={t('plans.overrides.expiresHelp')} error={expiresError || undefined}>
          <input id={base + '-expires'} type="datetime-local" value={expires} disabled={busy} onChange={(e) => setExpires(e.target.value)} />
        </FormField>
      </div>
      <div className="form-actions">
        <button type="button" className="button secondary" disabled={busy} onClick={onCancel}>
          {t('common.cancel')}
        </button>
        <button type="submit" className="button primary" disabled={busy}>
          {busy ? t('common.saving') : t('plans.overrides.save')}
        </button>
      </div>
    </form>
  );
}

OverrideEditor.propTypes = {
  featureKey: PropTypes.string.isRequired,
  name: PropTypes.string.isRequired,
  planValue: PropTypes.string,
  initialValue: PropTypes.string,
  busy: PropTypes.bool,
  onSave: PropTypes.func.isRequired,
  onCancel: PropTypes.func.isRequired
};

// Every known limit for one user, grouped: the plan's value, the user's effective value, and an "Overridden" badge.
// Edit opens an inline editor under the row; Reset to plan removes the override. effective is the GET
// .../entitlements response; planFeatures is the assigned plan's own values.
export default function UserLimits({ tenantId, userId, effective, planFeatures, onChanged }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [editing, setEditing] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const editButtons = useRef({});
  const features = (effective && effective.features) || {};
  const sources = (effective && effective.featureSources) || {};

  function closeEditor(key) {
    setEditing(null);
    const button = editButtons.current[key];
    if (button) setTimeout(() => button.focus(), 0);
  }

  async function run(action, done) {
    setBusy(true);
    setError(null);
    try {
      await action();
      done();
      await onChanged();
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <section className="plan-section" aria-labelledby="user-limits-title">
      <div className="section-title">
        <h3 id="user-limits-title">{t('plans.overrides.title')}</h3>
      </div>
      <p className="muted small">{t('plans.overrides.help')}</p>
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <div className="table-scroll">
        <table className="limits-table">
          <thead>
            <tr>
              <th scope="col">{t('plans.overrides.limit')}</th>
              <th scope="col">{t('plans.overrides.planValue')}</th>
              <th scope="col">{t('plans.overrides.effective')}</th>
              <th scope="col">
                <span className="sr-only">{t('table.actions')}</span>
              </th>
            </tr>
          </thead>
          {groupedFeatureKeys({ skipAliases: true }).map((g) => (
            <tbody key={g.group}>
              <tr className="limits-group-row">
                <th scope="colgroup" colSpan={4}>
                  {t('plans.groups.' + g.group)}
                </th>
              </tr>
              {g.keys.map((key) => {
                const name = featureName(t, key);
                const overridden = sources[key] === 'userOverride';
                const planValue = String(planFeatures[key] ?? '');
                const value = String(features[key] ?? planValue);
                return (
                  <Fragment key={key}>
                    <tr className={overridden ? 'limits-row overridden' : 'limits-row'}>
                      <th scope="row">
                        <span className="limits-name" title={key}>
                          {name}
                        </span>
                        {overridden ? <StatusBadge tone="accent">{t('plans.overrides.overridden')}</StatusBadge> : null}
                      </th>
                      <td data-label={t('plans.overrides.planValue')}>
                        <FeatureValue featureKey={key} value={planValue} />
                      </td>
                      <td className="limits-effective" data-label={t('plans.overrides.effective')}>
                        <FeatureValue featureKey={key} value={value} />
                      </td>
                      <td className="limits-actions">
                        <div className="row-actions">
                          <button
                            type="button"
                            className="button secondary small"
                            ref={(node) => {
                              editButtons.current[key] = node;
                            }}
                            disabled={busy || editing === key}
                            aria-expanded={editing === key}
                            aria-label={t('plans.overrides.editFor', { name })}
                            onClick={() => setEditing(key)}
                          >
                            <Pencil size={14} aria-hidden="true" /> {t('common.edit')}
                          </button>
                          {overridden ? (
                            <button
                              type="button"
                              className="button secondary small"
                              disabled={busy}
                              aria-label={t('plans.overrides.resetFor', { name })}
                              onClick={() => run(() => apiClient.clearUserEntitlement(tenantId, userId, key), () => addNotification(t('plans.overrides.resetDone', { name }), 'success'))}
                            >
                              <Undo2 size={14} aria-hidden="true" /> {t('plans.overrides.reset')}
                            </button>
                          ) : null}
                        </div>
                      </td>
                    </tr>
                    {editing === key ? (
                      <tr className="limits-editor-row">
                        <td colSpan={4}>
                          <OverrideEditor
                            featureKey={key}
                            name={name}
                            planValue={planValue}
                            initialValue={value}
                            busy={busy}
                            onCancel={() => closeEditor(key)}
                            onSave={(body) =>
                              run(
                                () => apiClient.setUserEntitlement(tenantId, userId, body),
                                () => {
                                  addNotification(t('plans.overrides.saved', { name }), 'success');
                                  closeEditor(key);
                                }
                              )
                            }
                          />
                        </td>
                      </tr>
                    ) : null}
                  </Fragment>
                );
              })}
            </tbody>
          ))}
        </table>
      </div>
    </section>
  );
}

UserLimits.propTypes = {
  tenantId: PropTypes.string.isRequired,
  userId: PropTypes.string.isRequired,
  effective: PropTypes.object,
  planFeatures: PropTypes.object.isRequired,
  onChanged: PropTypes.func.isRequired
};
