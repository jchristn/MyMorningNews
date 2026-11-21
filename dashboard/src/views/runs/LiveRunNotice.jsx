import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { CircleCheck, CircleX, LoaderCircle, TriangleAlert, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import CostValue from '../../components/CostValue.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import { costState } from '../../utils/cost.js';
import { RUN_STATUS_TONES } from '../../utils/enums.js';
import { findingsLink, isActiveRun } from './runParts.js';
import useLiveRun from './useLiveRun.js';

const NOTICE_TONES = { Completed: 'info', PartiallySucceeded: 'warning', Skipped: 'warning', Cancelled: 'warning', Failed: 'danger' };
const ICONS = { Completed: CircleCheck, Failed: CircleX };

// The newest stage that is running, for "Running: Search".
function currentStage(stages) {
  const running = (stages || []).filter((s) => s.status === 'Running');
  return running.length > 0 ? running[running.length - 1].stage : null;
}

// A run the user just started, followed live until it ends: queued, running (with the current stage), then the result
// with its new findings, or the failure, and what it has cost so far once it has made model calls. Key it by run ID. onFinished(run) runs once when the run ends.
export default function LiveRunNotice({ runId, initialRun, onFinished, onDismiss }) {
  const { t } = useTranslation();
  const { run, stages, error } = useLiveRun(runId, { initialRun, onFinished });
  const status = run ? run.status : 'Queued';
  const active = !run || isActiveRun(run);
  const dry = Boolean(run && run.isDryRun);
  const stage = currentStage(stages);
  const count = run ? run.newFindingCount || 0 : 0;

  let message;
  if (status === 'Queued') message = t('runs.live.queued');
  else if (status === 'Running') message = stage ? t('runs.live.runningStage', { stage: t('enums.runStage.' + stage, { defaultValue: stage }) }) : t('runs.live.running');
  else if (status === 'Completed') message = t('runs.live.completed', { count });
  else if (status === 'PartiallySucceeded') message = t('runs.live.partial', { count });
  else if (status === 'Failed') message = run.errorMessage ? t('runs.live.failedReason', { reason: run.errorMessage }) : t('runs.live.failed');
  else if (status === 'Skipped') message = run.skippedReason ? t('runs.live.skippedReason', { reason: run.skippedReason }) : t('runs.live.skipped');
  else message = t('runs.live.cancelled');

  const Icon = active ? LoaderCircle : ICONS[status] || TriangleAlert;
  const tone = active ? 'info' : NOTICE_TONES[status] || 'info';
  return (
    <div className={'notice notice-' + tone + ' live-run-notice'} role="status" aria-live="polite" data-testid="live-run-notice">
      <Icon size={16} aria-hidden="true" className={active ? 'spin' : undefined} />
      <div className="stack-tight">
        <span className="row-actions">
          <strong>{dry ? t('runs.dryRun') : t('runs.detail.title')}</strong>
          <StatusBadge tone={RUN_STATUS_TONES[status]}>{t('enums.runStatus.' + status)}</StatusBadge>
        </span>
        <span dir="auto">{message}</span>
        {run && costState(run) !== 'none' ? (
          <span className="small row-actions" data-testid="live-run-cost">
            <span className="muted">{active ? t('costs.header.costSoFar') : t('costs.header.cost')}</span>
            <CostValue totals={run} />
          </span>
        ) : null}
        {!active && run && run.summary ? (
          <span className="muted small run-summary live-run-summary" dir="auto">
            {run.summary}
          </span>
        ) : null}
        {error && !run ? <span className="small">{t('runs.live.unreadable')}</span> : null}
        <span className="row-actions small">
          <Link to={'/dashboard/runs?run=' + encodeURIComponent(runId)}>{t('runs.live.openRun')}</Link>
          {!active && run && count > 0 ? <Link to={findingsLink(run)}>{t('runs.detail.viewFindings', { count })}</Link> : null}
        </span>
      </div>
      {!active && onDismiss ? (
        <button type="button" className="icon-button live-run-dismiss" onClick={onDismiss} aria-label={t('common.dismiss')} title={t('common.dismiss')}>
          <X size={14} aria-hidden="true" />
        </button>
      ) : null}
    </div>
  );
}

LiveRunNotice.propTypes = {
  runId: PropTypes.string.isRequired,
  initialRun: PropTypes.object,
  onFinished: PropTypes.func,
  onDismiss: PropTypes.func
};
