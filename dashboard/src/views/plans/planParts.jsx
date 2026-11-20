import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { orderedFeatures } from '../../utils/promo.js';

// A translated name for an entitlement key, falling back to the key itself.
export function featureName(t, key) {
  return t('plans.featureNames.' + String(key).replace(/\./g, '_'), { defaultValue: key });
}

// A readable entitlement value: true and false as Yes and No, unlimited translated, numbers as given.
export function featureValue(t, value) {
  const v = String(value ?? '').trim();
  if (v.toLowerCase() === 'true') return t('common.yes');
  if (v.toLowerCase() === 'false') return t('common.no');
  if (v.toLowerCase() === 'unlimited') return t('promo.usage.unlimited');
  return v;
}

// Entitlement values as a definition list.
export function FeatureList({ features }) {
  const { t } = useTranslation();
  const rows = orderedFeatures(features);
  if (rows.length === 0) return <p className="muted small">{t('plans.noFeatures')}</p>;
  return (
    <dl className="kv-grid plan-features">
      {rows.map((r) => (
        <div className="kv-row" key={r.key}>
          <dt title={r.key}>{featureName(t, r.key)}</dt>
          <dd className="mono">{featureValue(t, r.value)}</dd>
        </div>
      ))}
    </dl>
  );
}

FeatureList.propTypes = { features: PropTypes.object };
