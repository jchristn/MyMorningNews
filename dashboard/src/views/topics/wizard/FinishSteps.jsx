import { useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { CircleCheck, FlaskConical, Globe, Rss, Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../../context/AuthContext.jsx';
import useFormat from '../../../hooks/useFormat.js';
import { CONSTRAINT_KINDS, DIMENSION_WEIGHT_TONES, SOURCE_CONSTRAINT_KINDS } from '../../../utils/enums.js';
import EmptyState from '../../../components/EmptyState.jsx';
import ErrorState from '../../../components/ErrorState.jsx';
import KeyValueGrid from '../../../components/KeyValueGrid.jsx';
import MarkdownView from '../../../components/MarkdownView.jsx';
import { describeSchedule } from '../../../components/ScheduleEditor.jsx';
import StatusBadge, { statusTone } from '../../../components/StatusBadge.jsx';
import ConstraintEditor from '../ConstraintEditor.jsx';
import DeliverySettings from '../DeliverySettings.jsx';
import TopicSchedules from '../TopicSchedules.jsx';
import LiveRunNotice from '../../runs/LiveRunNotice.jsx';

// URLs suggested by the model that are not saved on the topic yet.
export function pendingSources(checks, constraints) {
  const saved = new Set((constraints || []).filter((c) => SOURCE_CONSTRAINT_KINDS.includes(c.kind)).map((c) => c.value.toLowerCase()));
  return (checks || []).filter((c) => !saved.has(String(c.url).toLowerCase()));
}

// Step 5: suggested feeds and seed pages, each checked with a live fetch, then accepted onto the topic.
export function SourcesStep({ data, sources, dispatch, onSuggest, sessionId, busy }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const pending = useMemo(() => pendingSources(sources, data.constraints), [sources, data.constraints]);
  const [selected, setSelected] = useState(() => new Set());
  const [accepting, setAccepting] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    setSelected(new Set(pending.filter((c) => c.reachable).map((c) => c.url)));
  }, [pending]);

  async function accept() {
    setAccepting(true);
    setError(null);
    try {
      const chosen = pending.filter((c) => selected.has(c.url));
      const state = await apiClient.acceptSources(sessionId, {
        feedUrls: chosen.filter((c) => c.kind === 'FeedUrl').map((c) => c.url),
        seedUrls: chosen.filter((c) => c.kind !== 'FeedUrl').map((c) => c.url)
      });
      dispatch({ type: 'generated', data: state });
    } catch (err) {
      setError(err);
    } finally {
      setAccepting(false);
    }
  }

  const toggle = (url) =>
    setSelected((s) => {
      const next = new Set(s);
      if (next.has(url)) next.delete(url);
      else next.add(url);
      return next;
    });

  return (
    <div className="stack">
      <div className="toolbar-row">
        <p className="muted small">{t('wizard.sources.intro')}</p>
        <button type="button" className="button secondary small" disabled={busy} onClick={onSuggest}>
          <Sparkles size={14} aria-hidden="true" /> {sources ? t('wizard.sources.again') : t('wizard.sources.suggest')}
        </button>
      </div>
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      {sources && pending.length === 0 ? <p className="muted small">{sources.length === 0 ? t('wizard.sources.noneSuggested') : t('wizard.sources.allAccepted')}</p> : null}
      {pending.length > 0 ? (
        <>
          <ul className="source-checks" data-testid="source-checks">
            {pending.map((c) => (
              <li key={c.url} className={c.reachable ? 'reachable' : 'unreachable'}>
                <label className="checkbox-row">
                  <input type="checkbox" checked={selected.has(c.url)} disabled={busy || accepting} onChange={() => toggle(c.url)} aria-label={t('wizard.sources.select', { url: c.url })} />
                </label>
                <span className="source-icon" aria-hidden="true">
                  {c.kind === 'FeedUrl' ? <Rss size={14} /> : <Globe size={14} />}
                </span>
                <span className="source-main">
                  <a href={c.url} target="_blank" rel="noreferrer" className="mono truncate">
                    {c.url}
                  </a>
                  {c.reason ? <span className="muted small">{c.reason}</span> : null}
                  {c.error ? <span className="text-danger small">{c.error}</span> : null}
                </span>
                <span className="source-badges">
                  <StatusBadge>{t('enums.constraintKind.' + (c.kind === 'FeedUrl' ? 'FeedUrl' : 'SeedUrl'))}</StatusBadge>
                  {c.dimension ? <StatusBadge tone="info">{c.dimension}</StatusBadge> : null}
                  <StatusBadge tone={c.reachable ? 'success' : c.statusCode ? statusTone(c.statusCode) : 'danger'} title={c.contentType || undefined}>
                    {c.statusCode ? String(c.statusCode) : t('wizard.sources.unreachable')}
                  </StatusBadge>
                </span>
              </li>
            ))}
          </ul>
          <div>
            <button type="button" className="button primary small" disabled={busy || accepting || selected.size === 0} onClick={accept}>
              {accepting ? t('common.saving') : t('wizard.sources.accept', { count: selected.size })}
            </button>
          </div>
        </>
      ) : null}
      {!sources && !SOURCE_CONSTRAINT_KINDS.some((k) => (data.constraints || []).some((c) => c.kind === k)) ? (
        <EmptyState icon={Rss} title={t('wizard.sources.noneTitle')} message={t('wizard.sources.noneMessage')} />
      ) : null}
      <h3>{t('wizard.sources.accepted')}</h3>
      <ConstraintEditor
        topicId={data.topic.id}
        constraints={data.constraints || []}
        kinds={SOURCE_CONSTRAINT_KINDS}
        disabled={busy}
        onUpserted={(c) => dispatch({ type: 'constraint', constraint: c })}
        onRemoved={(id) => dispatch({ type: 'constraintRemoved', id })}
      />
    </div>
  );
}

SourcesStep.propTypes = {
  data: PropTypes.object.isRequired,
  sources: PropTypes.array,
  dispatch: PropTypes.func.isRequired,
  onSuggest: PropTypes.func.isRequired,
  sessionId: PropTypes.string.isRequired,
  busy: PropTypes.bool
};

// Step 6: the research schedule, email on every run, recipients, automatic audio, and the voice that reads the
// briefings aloud.
export function DeliveryStep({ data, recipients, onRecipientsChanged, dispatch, scheduleRef, optionsRef, busy }) {
  const { t } = useTranslation();
  return (
    <div className="stack">
      <h3>{t('wizard.delivery.schedule')}</h3>
      <p className="muted small">{t('wizard.delivery.scheduleHelp')}</p>
      <TopicSchedules topicId={data.topic.id} single controlRef={scheduleRef} disabled={busy} />
      <h3>{t('wizard.delivery.email')}</h3>
      <DeliverySettings topic={data.topic} recipients={recipients} onRecipientsChanged={onRecipientsChanged} onTopicUpdated={(topic) => dispatch({ type: 'topic', topic })} showSave={false} optionsRef={optionsRef} disabled={busy} />
    </div>
  );
}

DeliveryStep.propTypes = {
  data: PropTypes.object.isRequired,
  recipients: PropTypes.array.isRequired,
  onRecipientsChanged: PropTypes.func.isRequired,
  dispatch: PropTypes.func.isRequired,
  scheduleRef: PropTypes.object.isRequired,
  optionsRef: PropTypes.object.isRequired,
  busy: PropTypes.bool
};

// Step 7: everything captured, a dry-run preview, and Apply.
export function ReviewStep({ data, recipients, onApply, onPreview, preview, onPreviewDismiss, busy, applied }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [schedules, setSchedules] = useState(null);
  const topic = data.topic;
  const constraints = data.constraints || [];

  useEffect(() => {
    let cancelled = false;
    apiClient
      .listSchedules({ ownerType: 'Topic', ownerId: topic.id, pageSize: 50 })
      .then((r) => !cancelled && setSchedules((r && r.items) || []))
      .catch(() => !cancelled && setSchedules([]));
    return () => {
      cancelled = true;
    };
  }, [apiClient, topic.id]);

  const counts = CONSTRAINT_KINDS.map((kind) => ({ kind, n: constraints.filter((c) => c.kind === kind).length })).filter((x) => x.n > 0);

  return (
    <div className="stack" data-testid="review-step">
      {applied ? (
        <div className="notice notice-info">
          <CircleCheck size={14} aria-hidden="true" /> {t('wizard.review.applied')}{' '}
          <Link to={'/dashboard/topics/' + encodeURIComponent(topic.id)}>{t('wizard.review.openTopic')}</Link>
        </div>
      ) : null}
      <KeyValueGrid
        rows={[
          { key: 'name', label: t('wizard.context.name'), value: topic.name },
          { key: 'audience', label: t('wizard.context.audience'), value: t('enums.audienceLevel.' + topic.audienceLevel) },
          { key: 'horizon', label: t('wizard.context.horizon'), value: t('enums.horizon.' + topic.horizon) },
          { key: 'novelty', label: t('wizard.review.novelty'), value: f.number(topic.noveltyPreference) },
          { key: 'queries', label: t('wizard.context.seedQueries'), value: (topic.seedQueries || []).length > 0 ? f.list(topic.seedQueries) : null },
          { key: 'schedule', label: t('wizard.delivery.schedule'), value: schedules ? (schedules.length > 0 ? f.list(schedules.map((s) => describeSchedule(s, t, f))) : t('wizard.review.noSchedule')) : '...' },
          { key: 'email', label: t('delivery.emailOnRun'), value: topic.emailOnRun ? t('common.yes') : t('common.no') },
          { key: 'audio', label: t('topicAudio.label'), value: topic.audioEnabled ? t('common.yes') : t('common.no') },
          { key: 'recipients', label: t('delivery.recipients'), value: recipients.length > 0 ? f.list(recipients.map((r) => r.email)) : null }
        ]}
      />
      <section>
        <h3>{t('wizard.context.brief')}</h3>
        <div className="brief-box">
          <MarkdownView source={topic.brief} />
        </div>
      </section>
      <section>
        <h3>{t('wizard.review.dimensions', { count: (data.dimensions || []).length })}</h3>
        <ul className="chip-list">
          {(data.dimensions || []).map((d) => (
            <li key={d.id}>
              <StatusBadge tone={DIMENSION_WEIGHT_TONES[d.weight]}>
                {d.name} {' · '} {t('enums.dimensionWeight.' + d.weight)}
              </StatusBadge>
            </li>
          ))}
        </ul>
      </section>
      {counts.length > 0 ? (
        <section>
          <h3>{t('wizard.context.constraints')}</h3>
          <ul className="chip-list">
            {counts.map((x) => (
              <li key={x.kind}>
                <StatusBadge>{t('wizard.review.constraintCount', { kind: t('enums.constraintKind.' + x.kind), count: x.n })}</StatusBadge>
              </li>
            ))}
          </ul>
        </section>
      ) : null}
      {preview && preview.run ? <LiveRunNotice key={preview.run.id} runId={preview.run.id} initialRun={preview.run} onDismiss={onPreviewDismiss} /> : null}
      {preview && preview.error ? <ErrorState error={preview.error} inline onDismiss={onPreviewDismiss} /> : null}
      {!applied ? (
        <div className="review-actions">
          <div className="stack">
            <button type="button" className="button secondary" disabled={busy || (preview && preview.starting)} onClick={onPreview} data-testid="preview-run">
              <FlaskConical size={14} aria-hidden="true" /> {preview && preview.starting ? t('wizard.review.previewStarting') : t('wizard.review.preview')}
            </button>
            <span className="muted small">{t('wizard.review.previewHelp')}</span>
          </div>
          <button type="button" className="button primary" disabled={busy} onClick={onApply} data-testid="apply-topic">
            <CircleCheck size={14} aria-hidden="true" /> {busy ? t('common.working') : t('wizard.review.apply')}
          </button>
        </div>
      ) : null}
    </div>
  );
}

ReviewStep.propTypes = {
  data: PropTypes.object.isRequired,
  recipients: PropTypes.array.isRequired,
  onApply: PropTypes.func.isRequired,
  onPreview: PropTypes.func.isRequired,
  preview: PropTypes.shape({ starting: PropTypes.bool, error: PropTypes.object, run: PropTypes.object }),
  onPreviewDismiss: PropTypes.func,
  busy: PropTypes.bool,
  applied: PropTypes.bool
};
