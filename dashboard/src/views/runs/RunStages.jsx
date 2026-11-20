import PropTypes from 'prop-types';
import { Play, RotateCcw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import useFormat from '../../hooks/useFormat.js';
import { STAGE_STATUS_COLORS, STAGE_STATUS_TONES } from '../../utils/enums.js';
import StageTimeline from '../../charts/StageTimeline.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import { layoutStages } from './runParts.js';

// The stage timeline (bars by duration, colored by status) and a table with items in and out and any error. now
// moves running stages forward while a run is live. retryFor(stage) returns the retry a row offers (see
// runParts.retryAction) or null, and onRetry(action, stage) runs it.
export default function RunStages({ stages, now, retryFor, onRetry }) {
  const { t } = useTranslation();
  const f = useFormat();
  if (!stages || stages.length === 0) return <p className="muted">{t('runs.stages.empty')}</p>;
  const layout = layoutStages(stages, now);
  const actions = retryFor && onRetry ? new Map(layout.map(({ stage }) => [stage.stage, retryFor(stage)])) : new Map();
  const hasActions = [...actions.values()].some(Boolean);
  const label = (s) => t('enums.runStage.' + s.stage, { defaultValue: s.stage });
  const items = layout.map(({ stage, offsetMs, durationMs }) => ({
    key: stage.id || stage.stage,
    label: label(stage),
    offsetMs,
    durationMs,
    color: STAGE_STATUS_COLORS[stage.status] || 'var(--color-border-strong)',
    durationText: stage.status === 'Pending' ? '-' : f.duration(durationMs),
    lines: [
      { label: t('runs.stages.status'), value: t('enums.stageStatus.' + stage.status), color: STAGE_STATUS_COLORS[stage.status] },
      { label: t('runs.stages.duration'), value: f.duration(durationMs) },
      { label: t('runs.stages.items'), value: t('runs.stages.inOut', { in: f.number(stage.itemsIn || 0), out: f.number(stage.itemsOut || 0) }) }
    ]
  }));
  return (
    <div className="stack" data-testid="run-stages">
      <StageTimeline items={items} ariaLabel={t('runs.stages.aria', { count: stages.length })} formatDuration={f.duration} />
      <div className="chart-legend">
        {['Succeeded', 'Failed', 'Skipped', 'Running', 'Pending'].map((s) => (
          <span key={s}>
            <i style={{ background: STAGE_STATUS_COLORS[s] }} /> {t('enums.stageStatus.' + s)}
          </span>
        ))}
      </div>
      <div className="table-scroll">
        <table className="compact-table">
          <thead>
            <tr>
              <th scope="col">{t('runs.stages.stage')}</th>
              <th scope="col">{t('runs.stages.status')}</th>
              <th scope="col" className="align-right">
                {t('runs.stages.duration')}
              </th>
              <th scope="col" className="align-right">
                {t('runs.stages.in')}
              </th>
              <th scope="col" className="align-right">
                {t('runs.stages.out')}
              </th>
              {hasActions ? (
                <th scope="col">
                  <span className="sr-only">{t('runs.retry.column')}</span>
                </th>
              ) : null}
            </tr>
          </thead>
          <tbody>
            {layout.map(({ stage, durationMs }) => (
              <tr key={stage.id || stage.stage}>
                <td>
                  <span className="date-cell">
                    <span>{label(stage)}</span>
                    {stage.errorMessage ? <span className={'small ' + (stage.status === 'Failed' ? 'text-danger' : 'muted')}>{stage.errorMessage}</span> : null}
                  </span>
                </td>
                <td>
                  <StatusBadge tone={STAGE_STATUS_TONES[stage.status] || 'neutral'}>{t('enums.stageStatus.' + stage.status)}</StatusBadge>
                </td>
                <td className="align-right">{stage.status === 'Pending' ? '-' : f.duration(durationMs)}</td>
                <td className="align-right">{f.number(stage.itemsIn || 0)}</td>
                <td className="align-right">{f.number(stage.itemsOut || 0)}</td>
                {hasActions ? (
                  <td className="align-right">
                    {actions.get(stage.stage) ? (
                      <button
                        type="button"
                        className="button secondary small"
                        onClick={() => onRetry(actions.get(stage.stage), stage)}
                        aria-label={actions.get(stage.stage).kind === 'resume' ? t('runs.retry.fromStageAria', { stage: label(stage) }) : t('runs.retry.runAgainAria', { stage: label(stage) })}
                      >
                        {actions.get(stage.stage).kind === 'resume' ? <RotateCcw size={12} aria-hidden="true" /> : <Play size={12} aria-hidden="true" />}{' '}
                        {actions.get(stage.stage).kind === 'resume' ? t('runs.retry.fromStage') : t('runs.retry.runAgain')}
                      </button>
                    ) : null}
                  </td>
                ) : null}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}

RunStages.propTypes = { stages: PropTypes.array, now: PropTypes.number, retryFor: PropTypes.func, onRetry: PropTypes.func };
