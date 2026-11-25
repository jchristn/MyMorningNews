import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import { featureSpec, groupedFeatures, orderedFeatures } from '../../utils/promo.js';

// A translated name for an entitlement key, falling back to the key itself.
export function featureName(t, key) {
  return t('plans.featureNames.' + String(key).replace(/\./g, '_'), { defaultValue: key });
}

// Only the explicit unlimited value means unlimited; finite counts remain visible, including zero.
export function featureValue(t, value, _key = '', formatNumber = null) {
  const v = String(value ?? '').trim();
  if (v.toLowerCase() === 'true') return t('common.yes');
  if (v.toLowerCase() === 'false') return t('common.no');
  if (v.toLowerCase() === 'unlimited') return featureSpec(_key).noLimit === 'noMinimum' ? t('plans.input.noMinimum') : t('promo.usage.unlimited');
  // Whole counts are formatted for the locale; amounts (a cost such as 30.00) stay as stored.
  if (/^\d+$/.test(v)) {
    const n = Number(v);
    return formatNumber ? formatNumber(n) : v;
  }
  return v;
}

// One value for display (feature names already carry their units), or a muted "Not set" when the key has no value.
export function FeatureValue({ featureKey, value }) {
  const { t } = useTranslation();
  const f = useFormat();
  const v = String(value ?? '').trim();
  if (!v) return <span className="muted">{t('plans.notSet')}</span>;
  return (
    <span className="feature-value" title={v}>
      {featureValue(t, v, featureKey, f.number)}
    </span>
  );
}

FeatureValue.propTypes = { featureKey: PropTypes.string.isRequired, value: PropTypes.string };

// Entitlement values as a definition list. grouped splits them under group headings (the Plans page cards).
export function FeatureList({ features, grouped = false }) {
  const { t } = useTranslation();
  const f = useFormat();
  const rows = orderedFeatures(features);
  if (rows.length === 0) return <p className="muted small">{t('plans.noFeatures')}</p>;
  if (grouped) {
    return (
      <div className="plan-feature-groups">
        {groupedFeatures(features).map((g) => (
          <div className="plan-feature-group" key={g.group}>
            <h3 className="plan-feature-group-title">{t('plans.groups.' + g.group)}</h3>
            <dl className="plan-feature-pairs">
              {g.rows.map((r) => (
                <div className="plan-feature-pair" key={r.key}>
                  <dt title={r.key}>{featureName(t, r.key)}</dt>
                  <dd>
                    <FeatureValue featureKey={r.key} value={String(r.value ?? '')} />
                  </dd>
                </div>
              ))}
            </dl>
          </div>
        ))}
      </div>
    );
  }
  return (
    <dl className="kv-grid plan-features">
      {rows.map((r) => (
        <div className="kv-row" key={r.key}>
          <dt title={r.key}>{featureName(t, r.key)}</dt>
          <dd className="mono" title={String(r.value ?? '')}>
            {featureValue(t, r.value, r.key, f.number)}
          </dd>
        </div>
      ))}
    </dl>
  );
}

FeatureList.propTypes = { features: PropTypes.object, grouped: PropTypes.bool };
