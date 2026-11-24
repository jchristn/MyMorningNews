import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import { orderedFeatures } from '../../utils/promo.js';

// A translated name for an entitlement key, falling back to the key itself.
export function featureName(t, key) {
  return t('plans.featureNames.' + String(key).replace(/\./g, '_'), { defaultValue: key });
}

// Only the explicit unlimited value means unlimited; finite counts remain visible, including zero.
export function featureValue(t, value, _key = '', formatNumber = null) {
  const v = String(value ?? '').trim();
  if (v.toLowerCase() === 'true') return t('common.yes');
  if (v.toLowerCase() === 'false') return t('common.no');
  if (v.toLowerCase() === 'unlimited') return t('promo.usage.unlimited');
  // Whole counts are formatted for the locale; amounts (a cost such as 30.00) stay as stored.
  if (/^\d+$/.test(v)) {
    const n = Number(v);
    return formatNumber ? formatNumber(n) : v;
  }
  return v;
}

// Entitlement values as a definition list.
export function FeatureList({ features }) {
  const { t } = useTranslation();
  const f = useFormat();
  const rows = orderedFeatures(features);
  if (rows.length === 0) return <p className="muted small">{t('plans.noFeatures')}</p>;
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

FeatureList.propTypes = { features: PropTypes.object };
