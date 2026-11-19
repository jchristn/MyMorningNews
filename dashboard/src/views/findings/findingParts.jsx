import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import { FINDING_TIER_TONES, SCORE_COMPONENTS } from '../../utils/enums.js';
import StatusBadge from '../../components/StatusBadge.jsx';

// The five composite components of a finding, labeled and colored for charts and tooltips.
export function scoreComponents(finding, t) {
  return SCORE_COMPONENTS.map((c) => ({ key: c.key, label: t('findings.components.' + c.key), value: Number(finding[c.field]) || 0, color: c.color }));
}

export function TierBadge({ tier }) {
  const { t } = useTranslation();
  if (!tier) return null;
  return <StatusBadge tone={FINDING_TIER_TONES[tier] || 'neutral'}>{t('enums.findingTier.' + tier)}</StatusBadge>;
}

TierBadge.propTypes = { tier: PropTypes.string };

// The composite relevance score as a badge. The tooltip lists every component.
export function ScoreBadge({ finding }) {
  const { t } = useTranslation();
  const f = useFormat();
  const score = Number(finding.relevanceScore) || 0;
  const tone = score >= 80 ? 'accent' : score >= 60 ? 'success' : score >= 40 ? 'info' : 'neutral';
  const title = scoreComponents(finding, t)
    .map((c) => c.label + ': ' + f.number(c.value, { maximumFractionDigits: 1 }))
    .join(', ');
  return (
    <span className="score-badge" title={title} aria-label={t('findings.scoreAria', { score: f.number(score, { maximumFractionDigits: 1 }) })}>
      <StatusBadge tone={tone} mono>
        {f.number(score, { maximumFractionDigits: 0 })}
      </StatusBadge>
      <span className="score-meter" aria-hidden="true">
        <span style={{ width: Math.max(0, Math.min(100, score)) + '%' }} />
      </span>
    </span>
  );
}

ScoreBadge.propTypes = { finding: PropTypes.object.isRequired };
