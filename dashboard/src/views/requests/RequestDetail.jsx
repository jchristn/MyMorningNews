import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router-dom';
import { ArrowLeft, Ban, Braces, CircleCheck, PenLine, RefreshCw, Repeat, Trash2 } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import useInterval from '../../hooks/useInterval.js';
import { DIMENSION_WEIGHT_TONES, REQUEST_STATUS_TONES } from '../../utils/enums.js';
import { browserTimeZone, utcToZonedLocal, zonedLocalToUtc } from '../../utils/schedule.js';
import CopyableId from '../../components/CopyableId.jsx';
import DateCell from '../../components/DateCell.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import KeyValueGrid from '../../components/KeyValueGrid.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import MarkdownView from '../../components/MarkdownView.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import QuestionCard from '../../components/QuestionCard.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import { FailurePanel, GenerationPanel } from '../topics/wizard/WizardParts.jsx';
import useRequestActions, { canCancel, canConfirm, canConvert, confirmTime } from './useRequestActions.jsx';

// One request: the plan (title, brief, dimensions), clarifying questions with replan, and confirm now or at a time.
export default function RequestDetail() {
  const { t } = useTranslation();
  const f = useFormat();
  const navigate = useNavigate();
  const { id } = useParams();
  const { apiClient } = useAuth();
  const [data, setData] = useState(null);
  const [error, setError] = useState(null);
  const [replanning, setReplanning] = useState(false);
  const [replanError, setReplanError] = useState(null);
  const [timing, setTiming] = useState({ mode: 'now', local: '' });
  const zone = browserTimeZone();

  const load = useCallback(async () => {
    try {
      const next = await apiClient.getResearchRequest(id);
      setData(next);
      setError(null);
      const at = confirmTime(next.request);
      setTiming((current) => (current.touched ? current : { mode: at ? 'at' : 'now', local: at ? utcToZonedLocal(at, zone) : '' }));
    } catch (err) {
      setError(err);
    }
  }, [apiClient, id, zone]);

  useEffect(() => {
    load();
  }, [load]);

  const r = data ? data.request : null;
  useInterval(load, r && (r.status === 'Scheduled' || r.status === 'Running') ? 5000 : 0);

  const { modals, confirm, cancel, setConverting, setDeleting, setJson } = useRequestActions({
    onChanged: (result) => {
      if (result && result.request) setData(result);
      else load();
    },
    onDeleted: () => navigate('/dashboard/one-time')
  });

  async function replan() {
    setReplanning(true);
    setReplanError(null);
    try {
      setData(await apiClient.replanResearchRequest(id));
    } catch (err) {
      setReplanError(err);
      load();
    } finally {
      setReplanning(false);
    }
  }

  if (error && !data) {
    return (
      <div className="page">
        <PageHeader title={t('nav.items.oneTimeRequests')} />
        <ErrorState error={error} onRetry={load} />
      </div>
    );
  }
  if (!data) return <LoadingState />;

  const topic = data.topic;
  const answered = (data.questions || []).filter((q) => q.question.answered).length;
  const declined = r.status === 'Declined';
  const runAt = timing.mode === 'at' ? zonedLocalToUtc(timing.local, zone) : null;

  return (
    <div className="page">
      <Link to="/dashboard/one-time" className="link-button small back-link">
        <ArrowLeft size={14} aria-hidden="true" /> {t('oneTime.backToList')}
      </Link>
      <PageHeader
        title={r.title || r.requestText}
        summary={r.title && r.title !== r.requestText ? r.requestText : undefined}
        meta={
          <span className="row-actions">
            <StatusBadge tone={REQUEST_STATUS_TONES[r.status]}>{t('enums.requestStatus.' + r.status)}</StatusBadge>
            <CopyableId value={r.id} />
          </span>
        }
        actions={
          <>
            {canCancel(r) ? (
              <button type="button" className="button secondary" onClick={() => cancel(r)}>
                <Ban size={14} aria-hidden="true" /> {t('oneTime.actions.cancel')}
              </button>
            ) : null}
            {canConvert(r) ? (
              <button type="button" className="button secondary" onClick={() => setConverting(r)}>
                <Repeat size={14} aria-hidden="true" /> {t('oneTime.actions.convert')}
              </button>
            ) : null}
            {r.convertedTopicId ? (
              <Link className="button secondary" to={'/dashboard/topics/' + encodeURIComponent(r.convertedTopicId)}>
                {t('oneTime.actions.openTopic')}
              </Link>
            ) : null}
            <button type="button" className="icon-button" onClick={() => setJson(data)} aria-label={t('common.viewJson')} title={t('common.viewJson')}>
              <Braces size={16} aria-hidden="true" />
            </button>
            <button type="button" className="icon-button" onClick={() => setDeleting(r)} aria-label={t('common.delete')} title={t('common.delete')}>
              <Trash2 size={16} aria-hidden="true" />
            </button>
          </>
        }
      />

      {declined ? (
        <div className="notice notice-warning out-of-scope" role="alert" data-testid="declined">
          <strong>{t('oneTime.declined.title')}</strong>
          <p>{r.outOfScopeReason}</p>
          <button type="button" className="button primary small" onClick={() => navigate('/dashboard/one-time/new', { state: { requestText: r.requestText } })}>
            <PenLine size={14} aria-hidden="true" /> {t('oneTime.declined.rephrase')}
          </button>
        </div>
      ) : null}
      {replanning ? <GenerationPanel kind="replan" /> : null}
      {replanError ? <FailurePanel error={replanError} onRetry={replan} onDismiss={() => setReplanError(null)} /> : null}
      {!replanError && !replanning && r.errorMessage && r.status === 'Clarifying' ? <FailurePanel message={r.errorMessage} onRetry={replan} /> : null}
      {r.status === 'Failed' && r.errorMessage ? <div className="notice notice-danger">{r.errorMessage}</div> : null}

      <div className="two-column">
        <section className="panel stack">
          <h2>{t('oneTime.detail.request')}</h2>
          <KeyValueGrid
            rows={[
              { key: 'text', label: t('oneTime.form.text'), value: r.requestText },
              {
                key: 'deliverables',
                label: t('oneTime.form.deliverables'),
                value: (
                  <span className="chip-list">
                    {(r.deliverables || []).map((d) => (
                      <StatusBadge key={d}>{t('enums.deliverable.' + d, { defaultValue: d })}</StatusBadge>
                    ))}
                  </span>
                )
              },
              { key: 'length', label: t('oneTime.form.length'), value: t('enums.deliverableLength.' + r.length) },
              { key: 'runAt', label: t('oneTime.columns.runAt'), value: r.runAtUtc ? <DateCell value={r.runAtUtc} /> : t('oneTime.whenConfirmed') },
              { key: 'recipients', label: t('oneTime.form.recipients'), value: (r.emailRecipients || []).length > 0 ? f.list(r.emailRecipients) : null, hidden: !(r.deliverables || []).includes('Email') },
              { key: 'feed', label: t('oneTime.form.publishToFeed'), value: r.publishToFeed ? t('common.yes') : t('common.no'), hidden: !(r.deliverables || []).includes('Podcast') },
              { key: 'cost', label: t('oneTime.detail.estimatedCost'), value: r.estimatedCostUsd !== null && r.estimatedCostUsd !== undefined ? f.usd(r.estimatedCostUsd) : null },
              { key: 'created', label: t('oneTime.columns.created'), value: <DateCell value={r.createdUtc} /> },
              { key: 'confirmed', label: t('oneTime.detail.confirmed'), value: <DateCell value={r.confirmedUtc} />, hidden: !r.confirmedUtc },
              { key: 'completed', label: t('oneTime.detail.completed'), value: <DateCell value={r.completedUtc} />, hidden: !r.completedUtc },
              { key: 'run', label: t('oneTime.detail.run'), value: r.runId ? <Link to={'/dashboard/runs?run=' + encodeURIComponent(r.runId)}>{t('oneTime.detail.openRun')}</Link> : null, hidden: !r.runId }
            ]}
          />
          {canConfirm(r) ? (
            <div className="confirm-box" data-testid="confirm-box">
              <h3>{t('oneTime.detail.confirmTitle')}</h3>
              <div className="form-grid">
                <FormField id="rr-timing" label={t('oneTime.form.timing')}>
                  <select id="rr-timing" value={timing.mode} onChange={(e) => setTiming({ ...timing, mode: e.target.value, touched: true })}>
                    <option value="now">{t('oneTime.form.runNow')}</option>
                    <option value="at">{t('oneTime.form.runAt')}</option>
                  </select>
                </FormField>
                {timing.mode === 'at' ? (
                  <FormField id="rr-at" label={t('oneTime.form.when')} help={t('schedule.fields.localTimeHelp', { zone })}>
                    <input id="rr-at" type="datetime-local" value={timing.local} onChange={(e) => setTiming({ ...timing, local: e.target.value, touched: true })} />
                  </FormField>
                ) : null}
              </div>
              <div className="row-actions">
                <button type="button" className="button primary" disabled={replanning || !topic || !topic.brief || (timing.mode === 'at' && !runAt)} onClick={() => confirm(r, runAt)}>
                  <CircleCheck size={14} aria-hidden="true" /> {timing.mode === 'at' ? t('oneTime.detail.confirmAt') : t('oneTime.detail.confirmNow')}
                </button>
                {!topic || !topic.brief ? <span className="muted small">{t('oneTime.detail.needsPlan')}</span> : null}
              </div>
            </div>
          ) : null}
        </section>

        <section className="panel stack">
          <h2>{t('oneTime.detail.plan')}</h2>
          {topic && topic.brief ? (
            <div className="brief-box">
              <MarkdownView source={topic.brief} />
            </div>
          ) : (
            <p className="muted">{declined ? t('oneTime.detail.noPlanDeclined') : t('oneTime.detail.noPlan')}</p>
          )}
          {(data.dimensions || []).length > 0 ? (
            <>
              <h3>{t('oneTime.detail.dimensions')}</h3>
              <ul className="plan-dimensions">
                {data.dimensions.map((d) => (
                  <li key={d.id}>
                    <span className="row-actions">
                      <strong>{d.name}</strong>
                      <StatusBadge tone={DIMENSION_WEIGHT_TONES[d.weight]}>{t('enums.dimensionWeight.' + d.weight)}</StatusBadge>
                    </span>
                    {d.description ? <span className="muted small">{d.description}</span> : null}
                  </li>
                ))}
              </ul>
            </>
          ) : null}
        </section>
      </div>

      {(data.questions || []).length > 0 ? (
        <section className="panel stack">
          <div className="toolbar-row">
            <div>
              <h2>{t('oneTime.detail.questions')}</h2>
              <p className="muted small">{t('oneTime.detail.questionsHelp', { answered, total: data.questions.length })}</p>
            </div>
            {r.status === 'Clarifying' ? (
              <button type="button" className="button secondary" disabled={replanning || answered === 0} onClick={replan}>
                <RefreshCw size={14} aria-hidden="true" /> {t('oneTime.detail.replan')}
              </button>
            ) : null}
          </div>
          {data.questions.map((item, i) => (
            <QuestionCard
              key={item.question.id}
              index={i}
              item={item}
              disabled={replanning || r.status !== 'Clarifying'}
              onAnswer={async (body) => {
                const updated = await apiClient.answerResearchQuestion(r.id, item.question.id, body);
                setData((d) => ({ ...d, questions: d.questions.map((q) => (q.question.id === updated.question.id ? updated : q)) }));
              }}
            />
          ))}
        </section>
      ) : null}
      {modals}
    </div>
  );
}
