import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { ArrowLeft, FlaskConical, Play, WandSparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { CONTEXT_CONSTRAINT_KINDS, SOURCE_CONSTRAINT_KINDS, TOPIC_STATUS_TONES } from '../../utils/enums.js';
import { coveringEditions, scheduleState } from '../../utils/topicSchedule.js';
import ActionMenu from '../../components/ActionMenu.jsx';
import CopyableId from '../../components/CopyableId.jsx';
import DateCell from '../../components/DateCell.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import KeyValueGrid from '../../components/KeyValueGrid.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import MarkdownView from '../../components/MarkdownView.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import { describeSchedule } from '../../components/ScheduleEditor.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import FindingsTable from '../findings/FindingsTable.jsx';
import FeedbackTable from '../insights/FeedbackTable.jsx';
import PreferencesTable from '../insights/PreferencesTable.jsx';
import TopicInsights from '../insights/TopicInsights.jsx';
import PredictionsTable from '../predictions/PredictionsTable.jsx';
import LiveRunNotice from '../runs/LiveRunNotice.jsx';
import RunsTable from '../runs/RunsTable.jsx';
import TopicRunCosts from '../runs/TopicRunCosts.jsx';
import SuggestionsTable from '../suggestions/SuggestionsTable.jsx';
import AgenticSearchSettings from './AgenticSearchSettings.jsx';
import ConstraintEditor from './ConstraintEditor.jsx';
import DeliverySettings from './DeliverySettings.jsx';
import DimensionList from './DimensionList.jsx';
import TopicModels from './TopicModels.jsx';
import TopicPrompts from './TopicPrompts.jsx';
import TopicAudio from './TopicAudio.jsx';
import TopicSchedules from './TopicSchedules.jsx';
import TopicSearchProviders from './TopicSearchProviders.jsx';
import useTopicActions from './useTopicActions.jsx';
import { TopicContextForm } from './wizard/ContentSteps.jsx';

// Workspace views, in order. The ?tab= value names a view, so links to a view (?tab=findings) keep working.
export const WORKSPACE_TABS = [
  { id: 'overview' },
  { id: 'context' },
  { id: 'schedule' },
  { id: 'sources' },
  { id: 'runs' },
  { id: 'findings' },
  { id: 'predictions' },
  { id: 'suggestions' },
  { id: 'feedback' },
  { id: 'preferences' },
  { id: 'analytics' },
  { id: 'delivery' },
  { id: 'prompts' },
  { id: 'models' }
];

// The five tabs, each with its views (the first opens when the tab is chosen). A tab with several views shows a small
// switch between them.
export const WORKSPACE_GROUPS = [
  { id: 'overview', views: ['overview'] },
  { id: 'runs', views: ['runs'] },
  { id: 'results', views: ['findings', 'predictions', 'suggestions'] },
  { id: 'insights', views: ['analytics', 'feedback', 'preferences'] },
  { id: 'settings', views: ['context', 'schedule', 'sources', 'delivery', 'prompts', 'models'] }
];

export function groupOf(view) {
  return WORKSPACE_GROUPS.find((g) => g.views.includes(view)) || WORKSPACE_GROUPS[0];
}

// Editions are few per user; at most this many are read to find the ones that research a topic.
const MAX_EDITIONS = 20;

function upsert(list, item) {
  return list.some((x) => x.id === item.id) ? list.map((x) => (x.id === item.id ? item : x)) : [...list, item];
}

export default function TopicWorkspace() {
  const { t } = useTranslation();
  const f = useFormat();
  const navigate = useNavigate();
  const { id } = useParams();
  const [params, setParams] = useSearchParams();
  const { apiClient } = useAuth();
  const [detail, setDetail] = useState(null);
  const [error, setError] = useState(null);
  const [schedules, setSchedules] = useState([]);
  const [editions, setEditions] = useState([]);
  const [runsRefresh, setRunsRefresh] = useState(0);
  const [liveRun, setLiveRun] = useState(null);
  const [openRunId, setOpenRunId] = useState(null);
  const requested = params.get('tab') || 'overview';
  const tab = WORKSPACE_TABS.some((x) => x.id === requested) ? requested : 'overview';
  const group = groupOf(tab);

  const load = useCallback(async () => {
    setError(null);
    try {
      const [d, s] = await Promise.all([apiClient.getTopic(id), apiClient.listSchedules({ ownerType: 'Topic', ownerId: id, pageSize: 50 }).catch(() => null)]);
      setDetail(d);
      setSchedules((s && s.objects) || []);
    } catch (err) {
      setError(err);
    }
  }, [apiClient, id]);

  useEffect(() => {
    load();
  }, [load]);

  // The editions that research this topic before composing, with whether each is paused.
  useEffect(() => {
    let live = true;
    apiClient
      .listEditions({ pageSize: MAX_EDITIONS })
      .then((page) => Promise.all(((page && page.objects) || []).slice(0, MAX_EDITIONS).map((e) => apiClient.getEdition(e.id).catch(() => null))))
      .then((details) => live && setEditions(coveringEditions(details, id)))
      .catch(() => live && setEditions([]));
    return () => {
      live = false;
    };
  }, [apiClient, id]);

  const { actionsFor, modals, run } = useTopicActions({
    onChanged: () => {
      load();
      setRunsRefresh((n) => n + 1);
    },
    onDeleted: () => navigate('/dashboard/topics')
  });

  // Run now and Dry run follow the queued run on the page until it ends, then refresh the topic and its runs.
  const startRun = async (dryRun) => {
    const queued = await run(detail.topic, dryRun);
    if (queued && queued.id) setLiveRun(queued);
  };
  const runFinished = useCallback(() => {
    load();
    setRunsRefresh((n) => n + 1);
  }, [load]);

  if (error && !detail) {
    return (
      <div className="page">
        <PageHeader title={t('nav.items.topics')} />
        <ErrorState error={error} onRetry={load} />
      </div>
    );
  }
  if (!detail) return <LoadingState />;

  const topic = detail.topic;
  const setTopic = (next) => setDetail((d) => ({ ...d, topic: next }));
  const isDraft = topic.status === 'Draft';
  const own = scheduleState(schedules);
  const editionNext =
    editions
      .filter((e) => !e.paused && e.nextRunUtc)
      .map((e) => e.nextRunUtc)
      .sort()[0] || null;
  const nextRun = own.nextRunUtc || editionNext;
  const allPaused = !nextRun && (own.state === 'paused' || (own.state === 'none' && editions.length > 0 && editions.every((e) => e.paused)));
  const describeOwn = (s) => (s.enabled ? describeSchedule(s, t, f) : t('topics.schedule.pausedItem', { schedule: describeSchedule(s, t, f) }));
  const scheduleValue =
    own.state !== 'none'
      ? f.list(own.enabled.concat(own.paused).map(describeOwn))
      : editions.length > 0
        ? t('topics.schedule.viaEditions', { editions: f.list(editions.map((e) => (e.paused ? t('topics.schedule.pausedItem', { schedule: e.name }) : e.name))) })
        : t('wizard.review.noSchedule');
  const select = (next) => setParams(next === 'overview' ? {} : { tab: next }, { replace: true });
  const menu = actionsFor(topic).filter((a) => !['view', 'run', 'dry', 'refine'].includes(a.key));

  return (
    <div className="page">
      <Link to="/dashboard/topics" className="link-button small back-link">
        <ArrowLeft size={14} aria-hidden="true" /> {t('topics.backToList')}
      </Link>
      <PageHeader
        title={topic.name}
        summary={topic.description || t('routes.topics.summary')}
        meta={
          <span className="row-actions">
            <StatusBadge tone={TOPIC_STATUS_TONES[topic.status]}>{t('enums.topicStatus.' + topic.status)}</StatusBadge>
            {topic.isOneTime ? <StatusBadge tone="accent">{t('topics.oneTime')}</StatusBadge> : null}
            <CopyableId value={topic.id} />
          </span>
        }
        actions={
          <>
            <button type="button" className="button primary" disabled={isDraft || topic.status === 'Archived'} title={isDraft ? t('topics.draftNoRun') : undefined} onClick={() => startRun(false)}>
              <Play size={14} aria-hidden="true" /> {t('topics.actions.runNow')}
            </button>
            <button type="button" className="button secondary" disabled={topic.status === 'Archived'} onClick={() => startRun(true)}>
              <FlaskConical size={14} aria-hidden="true" /> {t('topics.actions.dryRun')}
            </button>
            {!topic.isOneTime ? (
              <Link className="button secondary" to={'/dashboard/topics/' + encodeURIComponent(topic.id) + '/refine' + (isDraft ? '' : '?start=1')}>
                <WandSparkles size={14} aria-hidden="true" /> {isDraft ? t('topics.actions.continueRefining') : t('topics.actions.refine')}
              </Link>
            ) : null}
            <ActionMenu actions={menu} />
          </>
        }
      />
      {liveRun ? <LiveRunNotice key={liveRun.id} runId={liveRun.id} initialRun={liveRun} onFinished={runFinished} onDismiss={() => setLiveRun(null)} /> : null}
      {isDraft ? (
        <div className="notice notice-info">
          {t('topics.draftNotice')} <Link to={'/dashboard/topics/' + encodeURIComponent(topic.id) + '/refine'}>{t('topics.actions.continueRefining')}</Link>
        </div>
      ) : null}
      <section className="panel">
        <div className="tabs" role="tablist" aria-label={t('topics.workspace.tabsLabel')}>
          {WORKSPACE_GROUPS.map((g) => (
            <button type="button" role="tab" key={g.id} aria-selected={group.id === g.id} className={group.id === g.id ? 'active' : ''} onClick={() => select(g.views[0])}>
              {t('topics.workspace.groups.' + g.id)}
            </button>
          ))}
        </div>
        {group.views.length > 1 ? (
          <div className="segmented workspace-views" role="tablist" aria-label={t('topics.workspace.viewsLabel', { group: t('topics.workspace.groups.' + group.id) })}>
            {group.views.map((v) => (
              <button type="button" role="tab" key={v} aria-selected={tab === v} className={tab === v ? 'active' : ''} onClick={() => select(v)}>
                {t('topics.workspace.tabs.' + v)}
              </button>
            ))}
          </div>
        ) : null}
        <ErrorState error={error} inline onRetry={load} onDismiss={() => setError(null)} />

        {tab === 'overview' ? (
          <div className="two-column">
            <div className="stack">
              <KeyValueGrid
                rows={[
                  { key: 'status', label: t('topics.columns.status'), value: t('enums.topicStatus.' + topic.status) },
                  { key: 'kind', label: t('topics.columns.oneTime'), value: topic.isOneTime ? t('topics.oneTime') : t('topics.recurring') },
                  { key: 'audience', label: t('wizard.context.audience'), value: t('enums.audienceLevel.' + topic.audienceLevel) },
                  { key: 'horizon', label: t('wizard.context.horizon'), value: t('enums.horizon.' + topic.horizon) },
                  { key: 'dimensions', label: t('topics.workspace.dimensionCount'), value: f.number((detail.dimensions || []).length) },
                  { key: 'schedule', label: t('wizard.delivery.schedule'), value: <span data-testid="topic-schedule">{scheduleValue}</span> },
                  {
                    key: 'nextRun',
                    label: t('schedule.nextRun'),
                    value: nextRun ? <DateCell value={nextRun} /> : allPaused ? <span data-testid="topic-next-run">{t('topics.schedule.paused')}</span> : null
                  },
                  { key: 'lastRun', label: t('topics.columns.lastRun'), value: <DateCell value={topic.lastRunUtc} /> },
                  { key: 'lastRefined', label: t('topics.columns.lastRefined'), value: <DateCell value={topic.lastRefinedUtc} /> },
                  { key: 'created', label: t('topics.columns.created'), value: <DateCell value={topic.createdUtc} /> }
                ]}
              />
            </div>
            <div className="stack">
              <h3>{t('wizard.context.brief')}</h3>
              {topic.brief ? (
                <div className="brief-box" dir="auto">
                  <MarkdownView source={topic.brief} />
                </div>
              ) : (
                <p className="muted">{t('topics.workspace.noBrief')}</p>
              )}
            </div>
          </div>
        ) : null}

        {tab === 'context' ? (
          <div className="stack">
            <h3>{t('topics.workspace.dimensions')}</h3>
            <DimensionList
              topicId={topic.id}
              dimensions={detail.dimensions || []}
              onUpdated={(d) => setDetail((x) => ({ ...x, dimensions: upsert(x.dimensions, d) }))}
              onAdded={(d) => setDetail((x) => ({ ...x, dimensions: upsert(x.dimensions, d) }))}
              onRemoved={(rid) => setDetail((x) => ({ ...x, dimensions: x.dimensions.filter((d) => d.id !== rid) }))}
            />
            <h3>{t('topics.workspace.brief')}</h3>
            <TopicContextForm topic={topic} onSaved={setTopic} />
            <h3>{t('wizard.context.constraints')}</h3>
            <ConstraintEditor
              topicId={topic.id}
              constraints={detail.constraints || []}
              kinds={CONTEXT_CONSTRAINT_KINDS}
              onUpserted={(c) => setDetail((x) => ({ ...x, constraints: upsert(x.constraints, c) }))}
              onRemoved={(rid) => setDetail((x) => ({ ...x, constraints: x.constraints.filter((c) => c.id !== rid) }))}
            />
          </div>
        ) : null}

        {tab === 'schedule' ? (
          <div className="stack">
            <TopicSchedules topicId={topic.id} onSaved={load} />
            <fieldset>
              <legend>{t('topicAudio.title')}</legend>
              <TopicAudio topic={topic} onTopicUpdated={setTopic} idPrefix="schedule-audio" />
            </fieldset>
          </div>
        ) : null}

        {tab === 'sources' ? (
          <div className="stack">
            <p className="muted small">{t('topics.workspace.sourcesHelp')}</p>
            <ConstraintEditor
              topicId={topic.id}
              constraints={detail.constraints || []}
              kinds={SOURCE_CONSTRAINT_KINDS}
              onUpserted={(c) => setDetail((x) => ({ ...x, constraints: upsert(x.constraints, c) }))}
              onRemoved={(rid) => setDetail((x) => ({ ...x, constraints: x.constraints.filter((c) => c.id !== rid) }))}
            />
            <TopicSearchProviders topicId={topic.id} />
            <AgenticSearchSettings topic={topic} onTopicUpdated={setTopic} />
          </div>
        ) : null}

        {tab === 'runs' ? (
          <div className="stack">
            <TopicRunCosts topicId={topic.id} refreshSignal={runsRefresh} onOpenRun={setOpenRunId} />
            <RunsTable
              topicId={topic.id}
              tableKey="topic-runs"
              refreshSignal={runsRefresh}
              initialRunId={openRunId}
              onRunClosed={() => setOpenRunId(null)}
              toolbar={
                <>
                  <button type="button" className="button primary small" disabled={isDraft || topic.status === 'Archived'} onClick={() => startRun(false)}>
                    <Play size={14} aria-hidden="true" /> {t('topics.actions.runNow')}
                  </button>
                  <button type="button" className="button secondary small" disabled={topic.status === 'Archived'} onClick={() => startRun(true)}>
                    <FlaskConical size={14} aria-hidden="true" /> {t('topics.actions.dryRun')}
                  </button>
                </>
              }
            />
          </div>
        ) : null}

        {tab === 'findings' ? <FindingsTable topicId={topic.id} tableKey="topic-findings" /> : null}

        {tab === 'predictions' ? <PredictionsTable topicId={topic.id} tableKey="topic-predictions" /> : null}

        {tab === 'suggestions' ? <SuggestionsTable topicId={topic.id} tableKey="topic-suggestions" /> : null}

        {tab === 'feedback' ? <FeedbackTable topicId={topic.id} tableKey="topic-feedback" /> : null}

        {tab === 'preferences' ? <PreferencesTable topicId={topic.id} topics={[topic]} tableKey="topic-preferences" /> : null}

        {tab === 'analytics' ? <TopicInsights topic={topic} onTopicUpdated={setTopic} /> : null}

        {tab === 'delivery' ? <DeliverySettings topic={topic} recipients={detail.recipients || []} onTopicUpdated={setTopic} onRecipientsChanged={(recipients) => setDetail((x) => ({ ...x, recipients }))} /> : null}

        {tab === 'prompts' ? <TopicPrompts topicId={topic.id} /> : null}

        {tab === 'models' ? <TopicModels topicId={topic.id} /> : null}
      </section>
      {modals}
    </div>
  );
}
