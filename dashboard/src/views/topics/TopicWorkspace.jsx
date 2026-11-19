import { useCallback, useEffect, useState } from 'react';
import { Link, useNavigate, useParams, useSearchParams } from 'react-router-dom';
import { ArrowLeft, Construction, FlaskConical, Play, WandSparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { CONTEXT_CONSTRAINT_KINDS, SOURCE_CONSTRAINT_KINDS, TOPIC_STATUS_TONES } from '../../utils/enums.js';
import ActionMenu from '../../components/ActionMenu.jsx';
import CopyableId from '../../components/CopyableId.jsx';
import DateCell from '../../components/DateCell.jsx';
import EmptyState from '../../components/EmptyState.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import KeyValueGrid from '../../components/KeyValueGrid.jsx';
import LoadingState from '../../components/LoadingState.jsx';
import MarkdownView from '../../components/MarkdownView.jsx';
import PageHeader from '../../components/PageHeader.jsx';
import { describeSchedule } from '../../components/ScheduleEditor.jsx';
import StatusBadge from '../../components/StatusBadge.jsx';
import RunsTable from '../runs/RunsTable.jsx';
import ConstraintEditor from './ConstraintEditor.jsx';
import DeliverySettings from './DeliverySettings.jsx';
import DimensionList from './DimensionList.jsx';
import TopicSchedules from './TopicSchedules.jsx';
import useTopicActions from './useTopicActions.jsx';
import { TopicContextForm } from './wizard/ContentSteps.jsx';

// Workspace tabs from the plan. Tabs with a phase show where the feature will live.
export const WORKSPACE_TABS = [
  { id: 'overview' },
  { id: 'context' },
  { id: 'schedule' },
  { id: 'sources' },
  { id: 'runs' },
  { id: 'findings', phase: 6 },
  { id: 'predictions', phase: 6 },
  { id: 'suggestions', phase: 6 },
  { id: 'feedback', phase: 10 },
  { id: 'preferences', phase: 10 },
  { id: 'analytics', phase: 10 },
  { id: 'delivery' },
  { id: 'prompts', phase: 12 },
  { id: 'models', phase: 12 }
];

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
  const [runsRefresh, setRunsRefresh] = useState(0);
  const requested = params.get('tab') || 'overview';
  const tab = WORKSPACE_TABS.some((x) => x.id === requested) ? requested : 'overview';

  const load = useCallback(async () => {
    setError(null);
    try {
      const [d, s] = await Promise.all([apiClient.getTopic(id), apiClient.listSchedules({ ownerType: 'Topic', ownerId: id, pageSize: 50 }).catch(() => null)]);
      setDetail(d);
      setSchedules((s && s.items) || []);
    } catch (err) {
      setError(err);
    }
  }, [apiClient, id]);

  useEffect(() => {
    load();
  }, [load]);

  const { actionsFor, modals, run } = useTopicActions({
    onChanged: () => {
      load();
      setRunsRefresh((n) => n + 1);
    },
    onDeleted: () => navigate('/dashboard/topics')
  });

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
  const nextRun = schedules
    .filter((s) => s.enabled && s.nextRunUtc)
    .map((s) => s.nextRunUtc)
    .sort()[0];
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
            <button type="button" className="button primary" disabled={isDraft || topic.status === 'Archived'} title={isDraft ? t('topics.draftNoRun') : undefined} onClick={() => run(topic, false)}>
              <Play size={14} aria-hidden="true" /> {t('topics.actions.runNow')}
            </button>
            <button type="button" className="button secondary" disabled={topic.status === 'Archived'} onClick={() => run(topic, true)}>
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
      {isDraft ? (
        <div className="notice notice-info">
          {t('topics.draftNotice')} <Link to={'/dashboard/topics/' + encodeURIComponent(topic.id) + '/refine'}>{t('topics.actions.continueRefining')}</Link>
        </div>
      ) : null}
      <section className="panel">
        <div className="tabs" role="tablist" aria-label={t('topics.workspace.tabsLabel')}>
          {WORKSPACE_TABS.map((x) => (
            <button type="button" role="tab" key={x.id} aria-selected={tab === x.id} className={(tab === x.id ? 'active' : '') + (x.phase ? ' upcoming' : '')} onClick={() => select(x.id)}>
              {t('topics.workspace.tabs.' + x.id)}
              {x.phase ? <span className="nav-phase">{t('nav.phaseShort', { phase: x.phase })}</span> : null}
            </button>
          ))}
        </div>
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
                  { key: 'schedule', label: t('wizard.delivery.schedule'), value: schedules.length > 0 ? f.list(schedules.map((s) => describeSchedule(s, t, f))) : t('wizard.review.noSchedule') },
                  { key: 'nextRun', label: t('schedule.nextRun'), value: nextRun ? <DateCell value={nextRun} /> : null },
                  { key: 'lastRun', label: t('topics.columns.lastRun'), value: <DateCell value={topic.lastRunUtc} /> },
                  { key: 'lastRefined', label: t('topics.columns.lastRefined'), value: <DateCell value={topic.lastRefinedUtc} /> },
                  { key: 'created', label: t('topics.columns.created'), value: <DateCell value={topic.createdUtc} /> }
                ]}
              />
            </div>
            <div className="stack">
              <h3>{t('wizard.context.brief')}</h3>
              {topic.brief ? (
                <div className="brief-box">
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

        {tab === 'schedule' ? <TopicSchedules topicId={topic.id} onSaved={load} /> : null}

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
            <div className="notice notice-info">{t('topics.workspace.searchProvidersLater')}</div>
          </div>
        ) : null}

        {tab === 'runs' ? (
          <RunsTable
            topicId={topic.id}
            tableKey="topic-runs"
            refreshSignal={runsRefresh}
            toolbar={
              <>
                <button type="button" className="button primary small" disabled={isDraft || topic.status === 'Archived'} onClick={() => run(topic, false)}>
                  <Play size={14} aria-hidden="true" /> {t('topics.actions.runNow')}
                </button>
                <button type="button" className="button secondary small" disabled={topic.status === 'Archived'} onClick={() => run(topic, true)}>
                  <FlaskConical size={14} aria-hidden="true" /> {t('topics.actions.dryRun')}
                </button>
              </>
            }
          />
        ) : null}

        {tab === 'delivery' ? <DeliverySettings topic={topic} recipients={detail.recipients || []} onTopicUpdated={setTopic} onRecipientsChanged={(recipients) => setDetail((x) => ({ ...x, recipients }))} /> : null}

        {WORKSPACE_TABS.some((x) => x.id === tab && x.phase) ? (
          <EmptyState
            icon={Construction}
            title={t('placeholder.title', { phase: WORKSPACE_TABS.find((x) => x.id === tab).phase })}
            message={t('placeholder.message', { feature: t('topics.workspace.tabs.' + tab), phase: WORKSPACE_TABS.find((x) => x.id === tab).phase })}
          />
        ) : null}
      </section>
      {modals}
    </div>
  );
}
