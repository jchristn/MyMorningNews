import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { History } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import useFormat from '../hooks/useFormat.js';
import { deltaHref, groupDeltas, isComparison, parseMode } from '../utils/comparison.js';

// What a briefing was compared with (plan section 6.2 item 7): one compact line that opens into the baseline issues
// and the changes found, grouped by kind with a text label each (never color alone). A standalone briefing shows a
// plain note. baselineHref(id) links a baseline issue; linkDeltas false hides evidence links (catalog issues carry no
// item identifiers). Renders nothing until a comparison response is given.
export default function ComparisonCallout({ comparison, baselineHref, linkDeltas = true, idPrefix = 'comparison' }) {
  const { t } = useTranslation();
  const f = useFormat();
  if (!isComparison(comparison)) return null;

  if (!comparison.compared) {
    return (
      <p className="comparison-note muted small" data-testid="comparison-standalone">
        <History size={14} aria-hidden="true" /> <span dir="auto">{comparison.note || t('comparison.standalone')}</span>
      </p>
    );
  }

  const baselines = Array.isArray(comparison.baselines) ? comparison.baselines : [];
  const groups = groupDeltas(comparison.deltas);
  const { mode, edition } = parseMode(comparison.mode);
  const titleOf = (b) => (b.available === false || !b.title ? t('comparison.unavailableIssue') : b.title);
  const byId = new Map(baselines.map((b) => [b.id, b]));
  const fallback = t('comparison.comparedWith', { issues: f.list(baselines.map(titleOf)) || t('comparison.earlierIssues') });
  const headingId = idPrefix + '-title';

  return (
    <details className="comparison-callout" data-testid="comparison-callout">
      <summary>
        <History size={16} aria-hidden="true" />
        <span className="comparison-summary-text">
          <span id={headingId} className="comparison-label">
            {t('comparison.title')}
          </span>
          <span dir="auto">{comparison.summary || fallback}</span>
          {comparison.noMaterialChanges ? <strong className="comparison-quiet">{t('comparison.noMaterialChanges')}</strong> : null}
        </span>
      </summary>
      <div className="comparison-body stack" aria-labelledby={headingId}>
        <div>
          <span className="field-label">{t('comparison.baselines')}</span>
          {baselines.length === 0 ? (
            <p className="muted small">{t('comparison.noBaselines')}</p>
          ) : (
            <ul className="comparison-baselines">
              {baselines.map((b) => (
                <li key={b.id}>
                  {b.available !== false && baselineHref ? (
                    <Link to={baselineHref(b.id)} dir="auto">
                      {titleOf(b)}
                    </Link>
                  ) : (
                    <span className="muted" dir="auto">
                      {titleOf(b)}
                    </span>
                  )}
                  {b.createdUtc ? <span className="muted small">{f.date(b.createdUtc)}</span> : null}
                </li>
              ))}
            </ul>
          )}
          <p className="muted small">
            {edition
              ? t('comparison.modeEdition', { mode: t('topicHistory.' + mode, { defaultValue: mode }) })
              : t('comparison.mode', { mode: t('topicHistory.' + mode, { defaultValue: mode }) })}
            {comparison.cutoffUtc ? ' ' + t('comparison.cutoff', { date: f.dateTime(comparison.cutoffUtc) }) : ''}
          </p>
          {comparison.truncated ? <p className="muted small">{t('comparison.truncated')}</p> : null}
        </div>
        {comparison.noMaterialChanges ? <p className="small">{t('comparison.noMaterialChangesHelp')}</p> : null}
        {groups.length > 0 ? (
          <div className="comparison-groups">
            {groups.map((group) => (
              <section
                key={group.kind}
                className="comparison-group"
                aria-label={t('comparison.kinds.' + group.kind, { defaultValue: group.kind })}
              >
                <h3 className="comparison-kind">
                  <span className={'comparison-kind-label kind-' + group.kind}>
                    {t('comparison.kinds.' + group.kind, { defaultValue: group.kind })}
                  </span>
                  <span className="muted small">{f.number(group.deltas.length)}</span>
                </h3>
                <ul className="comparison-deltas">
                  {group.deltas.map((d) => {
                    const href = linkDeltas ? deltaHref(d) : null;
                    const baseline = d.baselineBriefingId && baselines.length > 1 ? byId.get(d.baselineBriefingId) : null;
                    return (
                      <li key={d.id}>
                        {href ? (
                          <Link to={href} dir="auto">
                            {d.title}
                          </Link>
                        ) : (
                          <span dir="auto">{d.title}</span>
                        )}
                        {d.summary ? (
                          <span className="muted small" dir="auto">
                            {d.summary}
                          </span>
                        ) : null}
                        {baseline ? <span className="muted small">{t('comparison.versus', { issue: titleOf(baseline) })}</span> : null}
                      </li>
                    );
                  })}
                </ul>
              </section>
            ))}
          </div>
        ) : null}
      </div>
    </details>
  );
}

ComparisonCallout.propTypes = {
  comparison: PropTypes.object,
  baselineHref: PropTypes.func,
  linkDeltas: PropTypes.bool,
  idPrefix: PropTypes.string
};
