import { useRef } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { FEATURE_VALUE_MAX, featureSpec } from '../../utils/promo.js';

// A typed control for one entitlement value (a string, as the API stores it). Booleans are a Yes / No choice; counts
// and amounts are a number with a "No limit" checkbox (unlimited); daily audio seconds also show minutes; unknown keys
// are free text. allowUnset adds a "Not set" choice for booleans and lets numbers be empty (a plan value left to the
// built-in default). name is the feature's human name, used to label the inner controls. labelledBy names the
// element labelling the Yes / No group; number and text inputs carry id so a FormField label points at them.
export default function FeatureInput({ id, featureKey, name, value, onChange, labelledBy = undefined, describedBy = undefined, allowUnset = false, disabled = false, invalid = false }) {
  const { t } = useTranslation();
  const spec = featureSpec(featureKey);
  const v = String(value ?? '');
  const lastNumber = useRef(/^\d/.test(v) ? v : '');

  if (spec.kind === 'boolean') {
    const choices = [
      { value: 'true', label: t('common.yes') },
      { value: 'false', label: t('common.no') },
      ...(allowUnset ? [{ value: '', label: t('plans.notSet') }] : [])
    ];
    return (
      <div className="choice-group" role="radiogroup" id={id} aria-labelledby={labelledBy} aria-describedby={describedBy}>
        {choices.map((c) => (
          <label key={c.value || 'unset'} className={v.toLowerCase() === c.value ? 'active' : ''}>
            <input type="radio" name={id} value={c.value} checked={v.toLowerCase() === c.value} disabled={disabled} onChange={() => onChange(c.value)} />
            <span>{c.label}</span>
          </label>
        ))}
      </div>
    );
  }

  if (spec.kind === 'count' || spec.kind === 'amount') {
    const unlimited = v.toLowerCase() === 'unlimited';
    const noLimitKey = spec.noLimit === 'noMinimum' ? 'noMinimum' : 'unlimited';
    const seconds = spec.unit === 'seconds' && /^\d+$/.test(v) ? Number(v) : null;
    return (
      <div className="feature-number">
        <div className="feature-number-row">
          <input
            id={id}
            type="number"
            min={0}
            max={FEATURE_VALUE_MAX}
            step={spec.kind === 'count' ? 1 : 0.01}
            inputMode={spec.kind === 'count' ? 'numeric' : 'decimal'}
            value={unlimited ? '' : v}
            placeholder={unlimited ? t('plans.input.' + noLimitKey) : allowUnset ? t('plans.notSet') : undefined}
            disabled={disabled || unlimited}
            aria-invalid={invalid || undefined}
            aria-describedby={describedBy}
            onChange={(e) => {
              lastNumber.current = e.target.value;
              onChange(e.target.value);
            }}
          />
          {spec.unit ? <span className="feature-unit muted">{t('plans.input.units.' + spec.unit)}</span> : null}
          <label className="checkbox-row feature-unlimited">
            <input
              type="checkbox"
              checked={unlimited}
              disabled={disabled}
              aria-label={t('plans.input.' + noLimitKey + 'For', { name })}
              onChange={(e) => onChange(e.target.checked ? 'unlimited' : lastNumber.current || (allowUnset ? '' : '0'))}
            />
            <span aria-hidden="true">{t('plans.input.' + noLimitKey)}</span>
          </label>
        </div>
        {seconds !== null ? <span className="field-help muted">{t('plans.input.minutesHint', { minutes: Math.round(seconds / 6) / 10 })}</span> : null}
      </div>
    );
  }

  return <input id={id} className="mono" value={v} disabled={disabled} aria-invalid={invalid || undefined} aria-describedby={describedBy} onChange={(e) => onChange(e.target.value)} maxLength={200} />;
}

FeatureInput.propTypes = {
  id: PropTypes.string.isRequired,
  featureKey: PropTypes.string.isRequired,
  name: PropTypes.string.isRequired,
  value: PropTypes.string,
  onChange: PropTypes.func.isRequired,
  labelledBy: PropTypes.string,
  describedBy: PropTypes.string,
  allowUnset: PropTypes.bool,
  disabled: PropTypes.bool,
  invalid: PropTypes.bool
};
