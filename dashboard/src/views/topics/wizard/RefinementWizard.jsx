import { useCallback, useEffect, useReducer, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { Link, useLocation, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { ArrowLeft, ArrowRight, LogOut, Trash2, WandSparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useApp } from '../../../context/AppContext.jsx';
import { useAuth } from '../../../context/AuthContext.jsx';
import ConfirmModal from '../../../components/ConfirmModal.jsx';
import ErrorState from '../../../components/ErrorState.jsx';
import LoadingState from '../../../components/LoadingState.jsx';
import PageHeader from '../../../components/PageHeader.jsx';
import StatusBadge from '../../../components/StatusBadge.jsx';
import { TOPIC_STATUS_TONES } from '../../../utils/enums.js';
import { ContextStep, DimensionsStep, QuestionsStep } from './ContentSteps.jsx';
import DescribeStep from './DescribeStep.jsx';
import { DeliveryStep, ReviewStep, SourcesStep } from './FinishSteps.jsx';
import { FailurePanel, GenerationPanel, StepRail } from './WizardParts.jsx';
import { advancePlan, initialWizardState, isCompleted, previousStep, stepAvailable, wizardReducer } from './wizardState.js';

const STEP_GENERATION = { Dimensions: 'dimensions', Questions: 'questions', Context: 'context', Sources: 'sources' };

// The refinement wizard (plan section 13). mode "new" starts a session from the Describe step; mode "refine" resumes
// the topic's active session or starts a "refine again" session. Every step is persisted by the server.
export default function RefinementWizard({ mode }) {
  const { t } = useTranslation();
  const navigate = useNavigate();
  const location = useLocation();
  const { id: topicId } = useParams();
  const [params, setParams] = useSearchParams();
  const { apiClient, identity } = useAuth();
  const { addNotification } = useApp();
  const [state, dispatch] = useReducer(wizardReducer, initialWizardState);
  const [boot, setBoot] = useState({ loading: mode === 'refine', error: null, topic: null });
  const [starting, setStarting] = useState(false);
  const [startError, setStartError] = useState(null);
  const [recipients, setRecipients] = useState([]);
  const [applying, setApplying] = useState(false);
  const [actionError, setActionError] = useState(null);
  const [abandoning, setAbandoning] = useState(false);
  const [hideStoredError, setHideStoredError] = useState(false);
  const [previewState, setPreview] = useState({ starting: false, error: null, run: null });
  const controllerRef = useRef(null);
  const startedRef = useRef(false);
  const autoRef = useRef(null);
  const scheduleRef = useRef(null);
  const optionsRef = useRef(null);
  const contextRef = useRef(null);
  const [draftBrief, setDraftBrief] = useState(false);
  const onContextDraft = useCallback((form) => setDraftBrief(Boolean(form.brief && form.brief.trim())), []);
  const sessionId = params.get('session');
  const setParamsRef = useRef(setParams);
  const data = state.data;

  useEffect(() => {
    setParamsRef.current = setParams;
  }, [setParams]);
  const busy = Boolean(state.generating) || applying;

  // Load (or find, or start) the session for /topics/:id/refine.
  const load = useCallback(async () => {
    if (mode !== 'refine') return;
    setBoot({ loading: true, error: null, topic: null });
    try {
      if (sessionId) {
        dispatch({ type: 'loaded', data: await apiClient.getRefinementSession(sessionId) });
        setBoot({ loading: false, error: null, topic: null });
        return;
      }
      const active = await apiClient.listRefinementSessions({ status: 'Active', pageSize: 100, sortBy: 'createdUtc', sortDirection: 'Descending' });
      const found = ((active && active.items) || []).find((s) => s.topicId === topicId);
      if (found) {
        setParamsRef.current({ session: found.id }, { replace: true });
        return;
      }
      const detail = await apiClient.getTopic(topicId);
      setBoot({ loading: false, error: null, topic: detail.topic });
    } catch (err) {
      setBoot({ loading: false, error: err, topic: null });
    }
  }, [apiClient, mode, sessionId, topicId]);

  useEffect(() => {
    load();
  }, [load]);

  const topicKey = data ? data.topic.id : null;
  useEffect(() => {
    if (!topicKey) return;
    apiClient
      .listTopicRecipients(topicKey)
      .then((r) => setRecipients(Array.isArray(r) ? r : []))
      .catch(() => setRecipients([]));
  }, [apiClient, topicKey]);

  async function startRefineAgain() {
    setStarting(true);
    setStartError(null);
    try {
      const started = await apiClient.refineTopic(topicId);
      dispatch({ type: 'loaded', data: started });
      setParams({ session: started.session.id }, { replace: true });
    } catch (err) {
      setStartError(err);
    } finally {
      setStarting(false);
    }
  }

  // "Refine again" from the topics table arrives with ?start=1 and starts right away.
  useEffect(() => {
    if (mode === 'refine' && boot.topic && params.get('start') === '1' && !startedRef.current) {
      startedRef.current = true;
      startRefineAgain();
    }
    // startRefineAgain is stable for this purpose; the ref prevents a second start.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [mode, boot.topic]);

  async function startNew(body) {
    setStarting(true);
    setStartError(null);
    try {
      const started = await apiClient.startRefinement(body);
      navigate('/dashboard/topics/' + encodeURIComponent(started.topic.id) + '/refine?session=' + encodeURIComponent(started.session.id), { replace: true, state: { autoGenerate: 'dimensions' } });
    } catch (err) {
      setStartError(err);
      setStarting(false);
    }
  }

  async function generate(kind, guidance, nextStep) {
    if (!data) return false;
    const sid = data.session.id;
    const controller = new AbortController();
    controllerRef.current = controller;
    if (nextStep) dispatch({ type: 'goto', step: nextStep });
    dispatch({ type: 'generate', kind });
    setHideStoredError(true);
    try {
      if (kind === 'sources') {
        const checks = await apiClient.suggestSources(sid, controller.signal);
        dispatch({ type: 'sources', checks: Array.isArray(checks) ? checks : [] });
      } else {
        const calls = {
          dimensions: () => apiClient.proposeDimensions(sid, guidance, controller.signal),
          questions: () => apiClient.askQuestions(sid, guidance, controller.signal),
          context: () => apiClient.draftContext(sid, controller.signal)
        };
        dispatch({ type: 'generated', data: await calls[kind]() });
      }
      return true;
    } catch (err) {
      if (err && err.name === 'AbortError') dispatch({ type: 'stopped' });
      else dispatch({ type: 'failed', kind, error: err, guidance });
      return false;
    } finally {
      if (controllerRef.current === controller) controllerRef.current = null;
    }
  }

  // A new session from /topics/new proposes dimensions as soon as it opens.
  useEffect(() => {
    const wanted = location.state && location.state.autoGenerate;
    if (!data || !wanted || autoRef.current === data.session.id) return;
    autoRef.current = data.session.id;
    navigate(location.pathname + location.search, { replace: true, state: null });
    if (wanted === 'dimensions' && (data.dimensions || []).length === 0) generate('dimensions');
    // generate reads the latest state through data.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [data, location.state]);

  async function next() {
    setActionError(null);
    if (state.step === 'Delivery') {
      setApplying(true);
      try {
        if (scheduleRef.current) {
          const saved = await scheduleRef.current.save();
          if (saved === null) return;
        }
        const options = optionsRef.current;
        if (options) dispatch({ type: 'topic', topic: await apiClient.updateTopic(data.topic.id, { ...options, maxEmailFindings: Math.max(1, Math.min(50, Number(options.maxEmailFindings) || 10)) }) });
        if (options && options.emailOnRun && recipients.length === 0 && identity && identity.email) {
          const me = await apiClient.addTopicRecipient(data.topic.id, { email: identity.email, active: true });
          setRecipients([me]);
        }
        dispatch({ type: 'goto', step: 'Review' });
      } catch (err) {
        setActionError(err);
      } finally {
        setApplying(false);
      }
      return;
    }
    let current = data;
    // Next on Context saves the brief and its settings first, so nothing typed is lost.
    if (state.step === 'Context' && contextRef.current && contextRef.current.dirty) {
      setApplying(true);
      try {
        const saved = await contextRef.current.save();
        if (!saved) return;
        current = { ...data, topic: saved };
      } finally {
        setApplying(false);
      }
    }
    const plan = advancePlan(state.step, current, state.sources);
    if (plan.generate) generate(plan.generate, undefined, plan.to);
    else dispatch({ type: 'goto', step: plan.to });
  }

  async function apply() {
    setApplying(true);
    setActionError(null);
    try {
      const done = await apiClient.completeRefinement(data.session.id);
      dispatch({ type: 'loaded', data: done, step: 'Review' });
      addNotification(t('wizard.review.appliedToast', { name: done.topic.name }), 'success');
      navigate('/dashboard/topics/' + encodeURIComponent(done.topic.id));
    } catch (err) {
      setActionError(err);
    } finally {
      setApplying(false);
    }
  }

  // A dry run of the topic as it stands, followed on the Review step until it ends.
  async function preview() {
    setActionError(null);
    setPreview((p) => ({ ...p, starting: true, error: null }));
    try {
      const queued = await apiClient.runTopic(data.topic.id, { dryRun: true });
      setPreview({ starting: false, error: null, run: queued && queued.id ? queued : null });
    } catch (err) {
      setPreview((p) => ({ ...p, starting: false, error: err }));
    }
  }

  async function rephrase() {
    const seedText = data.session.seedText;
    try {
      await apiClient.abandonRefinement(data.session.id);
    } catch {
      // The new session does not depend on the old one.
    }
    navigate('/dashboard/topics/new', { state: { seedText } });
  }

  // Render
  const title = mode === 'new' && !data ? t('wizard.newTitle') : data ? t('wizard.refineTitle', { name: data.topic.name }) : t('wizard.refineTitleShort');
  const headerActions =
    data && !isCompleted(data) ? (
      <>
        <Link className="button secondary" to="/dashboard/topics">
          <LogOut size={14} aria-hidden="true" /> {t('wizard.saveExit')}
        </Link>
        <button type="button" className="button secondary" disabled={busy} onClick={() => setAbandoning(true)}>
          <Trash2 size={14} aria-hidden="true" /> {t('wizard.abandon')}
        </button>
      </>
    ) : null;

  if (mode === 'refine' && boot.loading && !data) {
    return (
      <div className="page">
        <PageHeader title={title} />
        <LoadingState />
      </div>
    );
  }
  if (mode === 'refine' && boot.error && !data) {
    return (
      <div className="page">
        <PageHeader title={title} />
        <ErrorState error={boot.error} onRetry={load} />
      </div>
    );
  }
  if (mode === 'refine' && !data) {
    return (
      <div className="page">
        <PageHeader title={t('wizard.refineTitle', { name: boot.topic ? boot.topic.name : '' })} summary={t('wizard.refineSummary')} />
        <section className="panel stack">
          <ErrorState error={startError} inline onDismiss={() => setStartError(null)} />
          <p>{t('wizard.refineStart', { name: boot.topic ? boot.topic.name : '' })}</p>
          <div className="row-actions">
            <button type="button" className="button primary" disabled={starting || !boot.topic} onClick={startRefineAgain}>
              <WandSparkles size={14} aria-hidden="true" /> {starting ? t('wizard.describe.starting') : t('wizard.refineStartButton')}
            </button>
            <Link className="button secondary" to={'/dashboard/topics/' + encodeURIComponent(topicId)}>
              {t('wizard.backToTopic')}
            </Link>
          </div>
        </section>
      </div>
    );
  }

  const step = data ? state.step : 'Describe';
  const generationKind = STEP_GENERATION[step];
  const storedError = data && data.session.lastError && !state.failure && !state.generating && !hideStoredError ? data.session.lastError : null;
  const failure = state.failure;
  // Next needs dimensions before questions and a brief before sources; generation steps fill in the rest.
  // On Context, a brief typed but not saved yet counts: Next saves it.
  const needsBrief = Boolean(data) && step !== 'Dimensions' && step !== 'Questions' && !stepAvailable('Sources', data) && !(step === 'Context' && draftBrief);
  const nextDisabled = busy || !data || (step === 'Dimensions' && (data.dimensions || []).length === 0) || needsBrief;

  return (
    <div className="page wizard-page">
      <PageHeader
        title={title}
        summary={t('wizard.summary')}
        meta={
          data ? (
            <span className="row-actions">
              <StatusBadge tone={TOPIC_STATUS_TONES[data.topic.status]}>{t('enums.topicStatus.' + data.topic.status)}</StatusBadge>
            </span>
          ) : null
        }
        actions={headerActions}
      />
      <div className="wizard-layout">
        <StepRail step={step} data={data} onSelect={(s) => dispatch({ type: 'goto', step: s })} disabled={busy} />
        <section className="panel wizard-panel" aria-labelledby="wizard-step-title">
          <header className="wizard-step-header">
            <h2 id="wizard-step-title">{t('wizard.steps.' + step)}</h2>
            <p className="muted small">{t('wizard.stepHelp.' + step)}</p>
          </header>
          {state.generating ? <GenerationPanel kind={state.generating} onStop={() => controllerRef.current && controllerRef.current.abort()} /> : null}
          {failure ? (
            <FailurePanel
              error={failure.error}
              onRetry={() => generate(failure.kind, failure.guidance)}
              onDismiss={() => dispatch({ type: 'dismissFailure' })}
            />
          ) : null}
          {storedError ? <FailurePanel message={storedError} onRetry={generationKind ? () => generate(generationKind) : undefined} onDismiss={() => setHideStoredError(true)} /> : null}
          <ErrorState error={actionError} inline onDismiss={() => setActionError(null)} />
          {step === 'Describe' ? (
            <>
              <ErrorState error={startError} inline onDismiss={() => setStartError(null)} />
              <DescribeStep data={data} initialText={(location.state && location.state.seedText) || ''} onStart={startNew} busy={starting} />
            </>
          ) : null}
          {step === 'Dimensions' && data ? <DimensionsStep data={data} dispatch={dispatch} busy={busy} onGenerate={(g) => generate('dimensions', g)} onRephrase={rephrase} /> : null}
          {step === 'Questions' && data ? (
            <QuestionsStep data={data} dispatch={dispatch} busy={busy} onAsk={(g) => generate('questions', g)} onAnswer={(qid, body) => apiClient.answerQuestion(data.session.id, qid, body)} />
          ) : null}
          {step === 'Context' && data ? <ContextStep data={data} dispatch={dispatch} busy={busy} onDraft={() => generate('context')} controlRef={contextRef} onDraftChange={onContextDraft} /> : null}
          {step === 'Sources' && data ? <SourcesStep data={data} sources={state.sources} dispatch={dispatch} busy={busy} sessionId={data.session.id} onSuggest={() => generate('sources')} /> : null}
          {step === 'Delivery' && data ? (
            <DeliveryStep data={data} recipients={recipients} onRecipientsChanged={setRecipients} dispatch={dispatch} scheduleRef={scheduleRef} optionsRef={optionsRef} busy={busy} />
          ) : null}
          {step === 'Review' && data ? <ReviewStep data={data} recipients={recipients} onApply={apply} onPreview={preview} preview={previewState} onPreviewDismiss={() => setPreview({ starting: false, error: null, run: null })} busy={busy} applied={isCompleted(data)} /> : null}
          {data && step !== 'Review' && step !== 'Describe' ? (
            <footer className="wizard-footer">
              <button type="button" className="button secondary" disabled={busy || previousStep(step) === step} onClick={() => dispatch({ type: 'goto', step: previousStep(step) })}>
                <ArrowLeft size={14} aria-hidden="true" /> {t('wizard.back')}
              </button>
              {step === 'Context' && needsBrief ? (
                <span className="muted small wizard-next-hint" data-testid="wizard-next-hint">
                  {t('wizard.context.nextNeedsBrief')}
                </span>
              ) : null}
              <button type="button" className="button primary" disabled={nextDisabled} onClick={next} data-testid="wizard-next">
                {t('wizard.nextTo', { step: t('wizard.steps.' + advancePlan(step, data, state.sources).to) })} <ArrowRight size={14} aria-hidden="true" />
              </button>
            </footer>
          ) : null}
        </section>
      </div>
      <ConfirmModal
        open={abandoning}
        title={t('wizard.abandonTitle')}
        message={data && data.topic.status === 'Draft' ? t('wizard.abandonDraft') : t('wizard.abandonExisting')}
        confirmLabel={t('wizard.abandon')}
        danger
        onClose={() => setAbandoning(false)}
        onConfirm={async () => {
          const wasDraft = data.topic.status === 'Draft';
          await apiClient.abandonRefinement(data.session.id);
          addNotification(t('wizard.abandoned'), 'success');
          navigate(wasDraft ? '/dashboard/topics' : '/dashboard/topics/' + encodeURIComponent(data.topic.id));
        }}
      />
    </div>
  );
}

RefinementWizard.propTypes = { mode: PropTypes.oneOf(['new', 'refine']).isRequired };
