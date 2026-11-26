import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { CircleCheck, MessageSquarePlus, PenLine, Sparkles } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../../context/AuthContext.jsx';
import { useApp } from '../../../context/AppContext.jsx';
import { DEFAULT_HISTORY_BUDGET, clampBudget } from '../../../utils/comparison.js';
import { AUDIENCE_LEVELS, CONTEXT_CONSTRAINT_KINDS, HORIZONS } from '../../../utils/enums.js';
import EmptyState from '../../../components/EmptyState.jsx';
import ErrorState from '../../../components/ErrorState.jsx';
import FormField from '../../../components/FormField.jsx';
import HistoryPreview from '../../../components/HistoryPreview.jsx';
import MarkdownEditor from '../../../components/MarkdownEditor.jsx';
import QuestionCard from '../../../components/QuestionCard.jsx';
import ConstraintEditor from '../ConstraintEditor.jsx';
import DimensionList from '../DimensionList.jsx';
import HistoryFields from '../HistoryFields.jsx';
import { GuidanceBar } from './WizardParts.jsx';
import { MAX_ROUNDS, answeredCount, canAskMore, isOutOfScope, questionRounds } from './wizardState.js';

// Step 2: proposed dimensions with lock, edit, weight, delete, add, and guidance for a regenerate.
export function DimensionsStep({ data, dispatch, onGenerate, onRephrase, busy }) {
  const { t } = useTranslation();
  const [guidance, setGuidance] = useState('');
  const topicId = data.topic.id;
  const dimensions = data.dimensions || [];
  const kept = dimensions.filter((d) => d.isLocked || d.origin === 'User').length;

  return (
    <div className="stack">
      {isOutOfScope(data) ? (
        <div className="notice notice-warning out-of-scope" role="alert" data-testid="out-of-scope">
          <strong>{t('wizard.outOfScope.title')}</strong>
          <p>{data.session.outOfScopeReason}</p>
          <div className="row-actions">
            <button type="button" className="button primary small" onClick={onRephrase}>
              <PenLine size={14} aria-hidden="true" /> {t('wizard.outOfScope.rephrase')}
            </button>
          </div>
        </div>
      ) : null}
      {dimensions.length === 0 && !isOutOfScope(data) ? (
        <EmptyState
          icon={Sparkles}
          title={t('wizard.dimensions.noneTitle')}
          message={t('wizard.dimensions.noneMessage')}
          action={
            <button type="button" className="button primary" disabled={busy} onClick={() => onGenerate(guidance)}>
              <Sparkles size={14} aria-hidden="true" /> {t('wizard.dimensions.propose')}
            </button>
          }
        />
      ) : null}
      {dimensions.length > 0 ? <p className="muted small">{t('wizard.dimensions.intro', { count: dimensions.length, kept })}</p> : null}
      <DimensionList
        topicId={topicId}
        dimensions={dimensions}
        disabled={busy}
        hideEmpty
        onUpdated={(d) => dispatch({ type: 'dimension', dimension: d })}
        onAdded={(d) => dispatch({ type: 'dimension', dimension: d })}
        onRemoved={(id) => dispatch({ type: 'dimensionRemoved', id })}
      />
      <GuidanceBar
        id="wiz-dim-guidance"
        value={guidance}
        onChange={setGuidance}
        disabled={busy}
        onRegenerate={() => onGenerate(guidance)}
        buttonLabel={dimensions.length > 0 ? t('wizard.regenerate') : t('wizard.dimensions.propose')}
        help={t('wizard.dimensions.regenerateHelp')}
      />
    </div>
  );
}

DimensionsStep.propTypes = {
  data: PropTypes.object.isRequired,
  dispatch: PropTypes.func.isRequired,
  onGenerate: PropTypes.func.isRequired,
  onRephrase: PropTypes.func.isRequired,
  busy: PropTypes.bool
};

// Step 3: rounds of questions of every kind, each skippable, with "Ask me more" for another round.
export function QuestionsStep({ data, dispatch, onAsk, onAnswer, busy }) {
  const { t } = useTranslation();
  const [guidance, setGuidance] = useState('');
  const rounds = questionRounds(data.questions);
  const total = (data.questions || []).length;
  const done = answeredCount(data.questions);
  const more = canAskMore(data);

  return (
    <div className="stack">
      {data.session.sufficientContext ? (
        <div className="notice notice-info" data-testid="sufficient-context">
          <CircleCheck size={14} aria-hidden="true" /> {t('wizard.questions.sufficient')}
        </div>
      ) : null}
      {total === 0 ? (
        <EmptyState
          icon={MessageSquarePlus}
          title={t('wizard.questions.noneTitle')}
          message={t('wizard.questions.noneMessage')}
          action={
            <button type="button" className="button primary" disabled={busy} onClick={() => onAsk(guidance)}>
              <MessageSquarePlus size={14} aria-hidden="true" /> {t('wizard.questions.ask')}
            </button>
          }
        />
      ) : (
        <p className="muted small">{t('wizard.questions.progress', { done, total })}</p>
      )}
      {rounds.map((r) => (
        <section key={r.round} className="question-round">
          <h3>{t('wizard.questions.round', { round: r.round })}</h3>
          <div className="stack">
            {r.items.map((item, i) => (
              <QuestionCard
                key={item.question.id}
                index={i}
                item={item}
                disabled={busy}
                onAnswer={async (body) => {
                  const updated = await onAnswer(item.question.id, body);
                  dispatch({ type: 'question', item: updated });
                }}
              />
            ))}
          </div>
        </section>
      ))}
      {total > 0 ? (
        <GuidanceBar
          id="wiz-q-guidance"
          value={guidance}
          onChange={setGuidance}
          disabled={busy || !more}
          onRegenerate={() => onAsk(guidance)}
          label={t('wizard.questions.moreLabel')}
          buttonLabel={t('wizard.questions.askMore')}
          help={
            more
              ? t('wizard.questions.moreHelp', { left: MAX_ROUNDS - (data.session.round || 0) })
              : t('wizard.questions.maxed', { max: MAX_ROUNDS })
          }
        />
      ) : null}
    </div>
  );
}

QuestionsStep.propTypes = {
  data: PropTypes.object.isRequired,
  dispatch: PropTypes.func.isRequired,
  onAsk: PropTypes.func.isRequired,
  onAnswer: PropTypes.func.isRequired,
  busy: PropTypes.bool
};

function topicForm(topic) {
  return {
    name: topic.name || '',
    brief: topic.brief || '',
    audienceLevel: topic.audienceLevel || 'Practitioner',
    horizon: topic.horizon || 'Weeks',
    noveltyPreference: topic.noveltyPreference ?? 50,
    seedQueries: (topic.seedQueries || []).join('\n'),
    historyMode: topic.historyMode || 'Off',
    historyCount: topic.historyCount ?? 5,
    historySinceUtc: topic.historySinceUtc?.slice(0, 10) || '',
    historyBriefingId: topic.historyBriefingId || '',
    historyTokenBudget: topic.historyTokenBudget ?? DEFAULT_HISTORY_BUDGET
  };
}

export function topicContextBody(form) {
  return {
    name: form.name.trim() || undefined,
    brief: form.brief,
    audienceLevel: form.audienceLevel,
    horizon: form.horizon,
    noveltyPreference: Number(form.noveltyPreference),
    historyMode: form.historyMode,
    historyCount: Number(form.historyCount),
    historySinceUtc:
      form.historyMode === 'SinceDate' && form.historySinceUtc ? new Date(form.historySinceUtc + 'T00:00:00Z').toISOString() : null,
    historyBriefingId: form.historyMode === 'SpecificIssue' ? form.historyBriefingId : null,
    historyTokenBudget: clampBudget(form.historyTokenBudget),
    seedQueries: form.seedQueries
      .split('\n')
      .map((s) => s.trim())
      .filter(Boolean)
  };
}

// The brief (Markdown), audience, horizon, novelty, and seed queries, saved with one button. controlRef (optional)
// receives { dirty, brief, save } so a parent can save unsaved edits itself: save() resolves to the saved topic, the
// topic unchanged when nothing was edited, or null when saving failed (the error shows in the form).
// onDraftChange(form) reports every edit.
export function TopicContextForm({ topic, onSaved, disabled = false, controlRef, onDraftChange }) {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const { addNotification } = useApp();
  const [form, setForm] = useState(() => topicForm(topic));
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const dirty = JSON.stringify(topicForm(topic)) !== JSON.stringify(form);

  useEffect(() => {
    setForm(topicForm(topic));
  }, [topic]);

  async function save() {
    setSaving(true);
    setError(null);
    try {
      const saved = await apiClient.updateTopic(topic.id, topicContextBody(form));
      onSaved(saved);
      addNotification(t('wizard.context.saved'), 'success');
      return saved;
    } catch (err) {
      setError(err);
      return null;
    } finally {
      setSaving(false);
    }
  }

  useEffect(() => {
    if (!controlRef) return;
    controlRef.current = { dirty, brief: form.brief, save: () => (dirty ? save() : Promise.resolve(topic)) };
  });

  useEffect(() => {
    if (onDraftChange) onDraftChange(form);
  }, [form, onDraftChange]);

  return (
    <div className="stack">
      <ErrorState error={error} inline onDismiss={() => setError(null)} />
      <div className="form-grid">
        <FormField id="ctx-name" label={t('wizard.context.name')} wide>
          <input
            id="ctx-name"
            value={form.name}
            maxLength={200}
            disabled={disabled}
            onChange={(e) => setForm({ ...form, name: e.target.value })}
            autoComplete="off"
          />
        </FormField>
        <FormField id="ctx-audience" label={t('wizard.context.audience')}>
          <select
            id="ctx-audience"
            value={form.audienceLevel}
            disabled={disabled}
            onChange={(e) => setForm({ ...form, audienceLevel: e.target.value })}
          >
            {AUDIENCE_LEVELS.map((a) => (
              <option key={a} value={a}>
                {t('enums.audienceLevel.' + a)}
              </option>
            ))}
          </select>
        </FormField>
        <FormField id="ctx-horizon" label={t('wizard.context.horizon')}>
          <select id="ctx-horizon" value={form.horizon} disabled={disabled} onChange={(e) => setForm({ ...form, horizon: e.target.value })}>
            {HORIZONS.map((h) => (
              <option key={h} value={h}>
                {t('enums.horizon.' + h)}
              </option>
            ))}
          </select>
        </FormField>
        <HistoryFields topicId={topic.id} value={form} onChange={setForm} disabled={disabled || saving} showBudget>
          <HistoryPreview
            form={form}
            load={(body, signal) => apiClient.previewTopicHistory(topic.id, body, signal)}
            baselineHref={(id) => '/dashboard/briefings/' + encodeURIComponent(id)}
            disabled={disabled}
            idPrefix="topic-history-preview"
          />
        </HistoryFields>
        <FormField
          id="ctx-novelty"
          label={t('wizard.context.novelty', { value: form.noveltyPreference })}
          help={t('wizard.context.noveltyHelp')}
        >
          <input
            id="ctx-novelty"
            type="range"
            min={0}
            max={100}
            step={5}
            value={form.noveltyPreference}
            disabled={disabled}
            onChange={(e) => setForm({ ...form, noveltyPreference: Number(e.target.value) })}
          />
        </FormField>
        <FormField id="ctx-brief" label={t('wizard.context.brief')} help={t('wizard.context.briefHelp')} wide>
          <MarkdownEditor
            id="ctx-brief"
            value={form.brief}
            onChange={(v) => setForm({ ...form, brief: v })}
            rows={12}
            disabled={disabled}
            label={t('wizard.context.brief')}
          />
        </FormField>
        <FormField id="ctx-seed" label={t('wizard.context.seedQueries')} help={t('wizard.context.seedQueriesHelp')} wide>
          <textarea
            id="ctx-seed"
            rows={4}
            className="code-editor"
            value={form.seedQueries}
            disabled={disabled}
            onChange={(e) => setForm({ ...form, seedQueries: e.target.value })}
          />
        </FormField>
      </div>
      <div>
        <button type="button" className="button primary small" disabled={disabled || saving || !dirty} onClick={save}>
          {saving ? t('common.saving') : dirty ? t('wizard.context.save') : t('wizard.context.savedState')}
        </button>
      </div>
    </div>
  );
}

TopicContextForm.propTypes = {
  topic: PropTypes.object.isRequired,
  onSaved: PropTypes.func.isRequired,
  disabled: PropTypes.bool,
  controlRef: PropTypes.object,
  onDraftChange: PropTypes.func
};

// Step 4: the drafted brief and constraints, every item editable, lockable, and removable.
export function ContextStep({ data, dispatch, onDraft, busy, controlRef, onDraftChange }) {
  const { t } = useTranslation();
  const hasBrief = Boolean(data.topic.brief && data.topic.brief.trim());
  return (
    <div className="stack">
      {!hasBrief ? (
        <EmptyState
          icon={Sparkles}
          title={t('wizard.context.noneTitle')}
          message={t('wizard.context.noneMessage')}
          action={
            <button type="button" className="button primary" disabled={busy} onClick={onDraft}>
              <Sparkles size={14} aria-hidden="true" /> {t('wizard.context.draft')}
            </button>
          }
        />
      ) : (
        <div className="toolbar-row">
          <p className="muted small">{t('wizard.context.intro')}</p>
          <button type="button" className="button secondary small" disabled={busy} onClick={onDraft}>
            <Sparkles size={14} aria-hidden="true" /> {t('wizard.context.redraft')}
          </button>
        </div>
      )}
      <TopicContextForm
        topic={data.topic}
        disabled={busy}
        onSaved={(topic) => dispatch({ type: 'topic', topic })}
        controlRef={controlRef}
        onDraftChange={onDraftChange}
      />
      <h3>{t('wizard.context.constraints')}</h3>
      <ConstraintEditor
        topicId={data.topic.id}
        constraints={data.constraints || []}
        kinds={CONTEXT_CONSTRAINT_KINDS}
        disabled={busy}
        onUpserted={(c) => dispatch({ type: 'constraint', constraint: c })}
        onRemoved={(id) => dispatch({ type: 'constraintRemoved', id })}
      />
    </div>
  );
}

ContextStep.propTypes = {
  data: PropTypes.object.isRequired,
  dispatch: PropTypes.func.isRequired,
  onDraft: PropTypes.func.isRequired,
  busy: PropTypes.bool,
  controlRef: PropTypes.object,
  onDraftChange: PropTypes.func
};
