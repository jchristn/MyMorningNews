import React, { useState } from 'react';
import { View } from 'react-native';
import { router, useLocalSearchParams } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { refinementApi, topicsApi } from '@/api/research';
import { ButtonRow, ChipGroup } from '@/components/Chips';
import { QuestionCard } from '@/components/QuestionCard';
import { ActionError, ResourceView } from '@/components/states';
import { Badge, Banner, BusyLine, Button, Card, Field, IconButton, ListRow, Screen, SectionHeader, T } from '@/components/ui';
import { AUDIENCE_LEVELS, CONTEXT_CONSTRAINT_KINDS, DIMENSION_WEIGHTS, HORIZONS } from '@/lib/enums';
import { WIZARD_STEPS, answeredCount, canAskMore, hasBrief, initialStep, isCompleted, isOutOfScope, nextStep, previousStep, questionRounds, stepAvailable, upsertById } from '@/lib/wizard';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';
import { spacing } from '@/theme/palette';
import { topicHref } from './TopicsScreens';

const GOALS = ['work', 'investing', 'curiosity', 'hiring', 'building'];

/** The RefinementStartRequest for the Describe form. */
export function startBody(form, goalLabel) {
  return { seedText: form.seedText.trim(), goal: form.goal ? goalLabel(form.goal) : null, depth: form.depth || null, horizon: form.horizon.trim() || null, known: form.known.trim() || null };
}

function Describe({ onStart, busy }) {
  const { t } = useTranslation();
  const [form, setForm] = useState({ seedText: '', goal: '', depth: '', horizon: '', known: '' });
  const set = (patch) => setForm((f) => ({ ...f, ...patch }));
  return (
    <>
      <T variant="title">{t('mobile.wizard.prompt')}</T>
      <T variant="muted">{t('mobile.wizard.intro')}</T>
      <Field label={t('mobile.wizard.label')} value={form.seedText} onChangeText={(v) => set({ seedText: v })} multiline maxLength={4000} placeholder={t('mobile.wizard.placeholder')} testID="wizard-seed" />
      <T variant="label">{t('mobile.wizard.goal')}</T>
      <ChipGroup options={GOALS.map((g) => ({ value: g, label: t('mobile.wizard.goals.' + g) }))} value={form.goal} onChange={(v) => set({ goal: form.goal === v ? '' : v })} accessibilityLabel={t('mobile.wizard.goal')} />
      <T variant="label">{t('mobile.wizard.depth')}</T>
      <ChipGroup options={AUDIENCE_LEVELS.map((a) => ({ value: a, label: t('mobile.enums.audience.' + a) }))} value={form.depth} onChange={(v) => set({ depth: form.depth === v ? '' : v })} accessibilityLabel={t('mobile.wizard.depth')} />
      <Field label={t('mobile.wizard.horizon')} hint={t('mobile.wizard.horizonHint')} value={form.horizon} onChangeText={(v) => set({ horizon: v })} maxLength={128} />
      <Field label={t('mobile.wizard.known')} hint={t('mobile.wizard.optional')} value={form.known} onChangeText={(v) => set({ known: v })} multiline maxLength={4000} />
      <Button title={t('mobile.wizard.start')} icon="sparkles-outline" busy={busy} disabled={form.seedText.trim().length < 3} onPress={() => onStart(startBody(form, (g) => t('mobile.wizard.goals.' + g)))} testID="wizard-start" />
    </>
  );
}

function DimensionRow({ dimension, topicId, onChanged, onRemoved, disabled }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  const [error, setError] = useState(null);
  const save = async (patch) => {
    setError(null);
    try {
      onChanged(await topicsApi.updateDimension(client, topicId, dimension.id, patch));
    } catch (e) {
      setError(e);
    }
  };
  const remove = async () => {
    setError(null);
    try {
      await topicsApi.removeDimension(client, topicId, dimension.id);
      onRemoved(dimension.id);
    } catch (e) {
      setError(e);
    }
  };
  return (
    <Card testID={'dimension-' + dimension.id}>
      <View style={{ flexDirection: 'row', alignItems: 'center', gap: spacing.sm }}>
        <View style={{ flex: 1, gap: 2 }}>
          <T style={{ fontWeight: '600' }}>{dimension.name}</T>
          {dimension.origin === 'User' ? <Badge label={t('mobile.wizard.yours')} tone="primary" /> : null}
        </View>
        <IconButton icon={dimension.isLocked ? 'lock-closed' : 'lock-open-outline'} accessibilityLabel={dimension.isLocked ? t('mobile.wizard.unlock') : t('mobile.wizard.lock')} disabled={disabled} onPress={() => void save({ isLocked: !dimension.isLocked })} testID={'lock-' + dimension.id} />
        <IconButton icon="trash-outline" accessibilityLabel={t('mobile.wizard.removeDimension', { name: dimension.name })} disabled={disabled} onPress={() => void remove()} testID={'remove-' + dimension.id} />
      </View>
      {dimension.description ? <T variant="small">{dimension.description}</T> : null}
      {dimension.rationale ? <T variant="small">{t('mobile.wizard.whyItMatters', { text: dimension.rationale })}</T> : null}
      <ChipGroup
        disabled={disabled}
        options={DIMENSION_WEIGHTS.map((w) => ({ value: w, label: t('mobile.enums.weight.' + w), testID: 'weight-' + dimension.id + '-' + w }))}
        value={dimension.weight}
        onChange={(w) => void save({ weight: w })}
        accessibilityLabel={t('mobile.wizard.weight', { name: dimension.name })}
      />
      <ActionError error={error} />
    </Card>
  );
}

function Dimensions({ data, update, generate, busy }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  const [guidance, setGuidance] = useState('');
  const [name, setName] = useState('');
  const [description, setDescription] = useState('');
  const [error, setError] = useState(null);
  const topicId = data.topic.id;
  const dims = (data.dimensions || []).slice().sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));
  const add = async () => {
    setError(null);
    try {
      const created = await topicsApi.addDimension(client, topicId, { name: name.trim(), description: description.trim() || null, weight: 'Normal' });
      update((d) => ({ ...d, dimensions: upsertById(d.dimensions, created) }));
      setName('');
      setDescription('');
    } catch (e) {
      setError(e);
    }
  };
  return (
    <>
      {isOutOfScope(data) ? <Banner kind="warning" title={t('mobile.wizard.outOfScope')}>{data.session.outOfScopeReason}</Banner> : null}
      <T variant="muted">{dims.length ? t('mobile.wizard.dimensionsIntro', { count: dims.length }) : t('mobile.wizard.dimensionsEmpty')}</T>
      {dims.map((d) => (
        <DimensionRow
          key={d.id}
          dimension={d}
          topicId={topicId}
          disabled={Boolean(busy)}
          onChanged={(next) => update((s) => ({ ...s, dimensions: upsertById(s.dimensions, next) }))}
          onRemoved={(id) => update((s) => ({ ...s, dimensions: s.dimensions.filter((x) => x.id !== id) }))}
        />
      ))}
      <Card>
        <T variant="label">{t('mobile.wizard.addDimension')}</T>
        <Field label={t('mobile.wizard.dimensionName')} value={name} onChangeText={setName} maxLength={200} testID="dimension-name" />
        <Field label={t('mobile.wizard.dimensionDescription')} value={description} onChangeText={setDescription} multiline />
        <ActionError error={error} />
        <Button title={t('mobile.wizard.add')} kind="secondary" icon="add" disabled={!name.trim() || Boolean(busy)} onPress={() => void add()} testID="dimension-add" />
      </Card>
      <Card>
        <Field label={t('mobile.wizard.guidance')} hint={t('mobile.wizard.guidanceHint')} value={guidance} onChangeText={setGuidance} multiline />
        <Button title={dims.length ? t('mobile.wizard.regenerate') : t('mobile.wizard.propose')} icon="sparkles-outline" kind="secondary" busy={busy === 'dimensions'} disabled={Boolean(busy)} onPress={() => generate('dimensions', guidance)} testID="propose-dimensions" />
      </Card>
    </>
  );
}

function Questions({ data, update, generate, busy }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  const [guidance, setGuidance] = useState('');
  const rounds = questionRounds(data.questions);
  const total = (data.questions || []).length;
  return (
    <>
      {data.session.sufficientContext ? <Banner kind="success">{t('mobile.wizard.sufficient')}</Banner> : null}
      <T variant="muted">{total ? t('mobile.wizard.questionsProgress', { done: answeredCount(data.questions), total }) : t('mobile.wizard.questionsEmpty')}</T>
      {rounds.map((r) => (
        <View key={r.round} style={{ gap: spacing.sm }}>
          <SectionHeader title={t('mobile.wizard.round', { round: r.round })} />
          {r.items.map((item, i) => (
            <QuestionCard
              key={item.question.id + ':' + (item.question.lastUpdateUtc || '')}
              item={item}
              index={i}
              disabled={Boolean(busy)}
              onAnswer={async (body) => {
                const updated = await refinementApi.answer(client, data.session.id, item.question.id, body);
                update((d) => ({ ...d, questions: d.questions.map((q) => (q.question.id === item.question.id && updated && updated.question ? updated : q)) }));
              }}
            />
          ))}
        </View>
      ))}
      {canAskMore(data) ? (
        <Card>
          {total ? <Field label={t('mobile.wizard.moreGuidance')} value={guidance} onChangeText={setGuidance} multiline /> : null}
          <Button title={total ? t('mobile.wizard.askMore') : t('mobile.wizard.ask')} icon="chatbubbles-outline" kind="secondary" busy={busy === 'questions'} disabled={Boolean(busy)} onPress={() => generate('questions', guidance)} testID="ask-questions" />
        </Card>
      ) : null}
    </>
  );
}

function Context({ data, update, generate, busy }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  const topic = data.topic;
  const [form, setForm] = useState({ name: topic.name || '', brief: topic.brief || '', audienceLevel: topic.audienceLevel || 'Practitioner', horizon: topic.horizon || 'Weeks' });
  const [kind, setKind] = useState('IncludeKeyword');
  const [value, setValue] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState(null);
  const dirty = form.name !== (topic.name || '') || form.brief !== (topic.brief || '') || form.audienceLevel !== topic.audienceLevel || form.horizon !== topic.horizon;
  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      const updated = await topicsApi.update(client, topic.id, { name: form.name.trim() || undefined, brief: form.brief, audienceLevel: form.audienceLevel, horizon: form.horizon });
      update((d) => ({ ...d, topic: updated }));
    } catch (e) {
      setError(e);
    } finally {
      setSaving(false);
    }
  };
  const addConstraint = async () => {
    setError(null);
    try {
      const created = await topicsApi.addConstraint(client, topic.id, { kind, value: value.trim() });
      update((d) => ({ ...d, constraints: upsertById(d.constraints, created) }));
      setValue('');
    } catch (e) {
      setError(e);
    }
  };
  const removeConstraint = async (id) => {
    setError(null);
    try {
      await topicsApi.removeConstraint(client, topic.id, id);
      update((d) => ({ ...d, constraints: d.constraints.filter((c) => c.id !== id) }));
    } catch (e) {
      setError(e);
    }
  };
  const constraints = (data.constraints || []).filter((c) => CONTEXT_CONSTRAINT_KINDS.includes(c.kind));
  return (
    <>
      <Button title={hasBrief(data) ? t('mobile.wizard.redraft') : t('mobile.wizard.draft')} icon="sparkles-outline" kind="secondary" busy={busy === 'context'} disabled={Boolean(busy)} onPress={() => generate('context')} testID="draft-context" />
      <Field label={t('mobile.wizard.name')} value={form.name} onChangeText={(v) => setForm((f) => ({ ...f, name: v }))} maxLength={200} testID="context-name" />
      <Field label={t('mobile.wizard.brief')} hint={t('mobile.wizard.briefHint')} value={form.brief} onChangeText={(v) => setForm((f) => ({ ...f, brief: v }))} multiline testID="context-brief" />
      <T variant="label">{t('mobile.wizard.audience')}</T>
      <ChipGroup options={AUDIENCE_LEVELS.map((a) => ({ value: a, label: t('mobile.enums.audience.' + a) }))} value={form.audienceLevel} onChange={(v) => setForm((f) => ({ ...f, audienceLevel: v }))} accessibilityLabel={t('mobile.wizard.audience')} />
      <T variant="label">{t('mobile.wizard.horizonLabel')}</T>
      <ChipGroup options={HORIZONS.map((h) => ({ value: h, label: t('mobile.enums.horizon.' + h) }))} value={form.horizon} onChange={(v) => setForm((f) => ({ ...f, horizon: v }))} accessibilityLabel={t('mobile.wizard.horizonLabel')} />
      <Button title={dirty ? t('mobile.wizard.saveContext') : t('mobile.wizard.contextSaved')} busy={saving} disabled={!dirty || Boolean(busy)} onPress={() => void save()} testID="context-save" />
      <SectionHeader title={t('mobile.wizard.constraints')} />
      {constraints.length ? (
        <Card>
          {constraints.map((c) => (
            <ListRow key={c.id} title={c.value} detail={t('mobile.enums.constraint.' + c.kind)} right={<IconButton icon="close" accessibilityLabel={t('mobile.wizard.removeConstraint', { value: c.value })} onPress={() => void removeConstraint(c.id)} />} />
          ))}
        </Card>
      ) : null}
      <Card>
        <ChipGroup scroll options={CONTEXT_CONSTRAINT_KINDS.map((k) => ({ value: k, label: t('mobile.enums.constraint.' + k) }))} value={kind} onChange={setKind} accessibilityLabel={t('mobile.wizard.constraintKind')} />
        <Field label={t('mobile.wizard.constraintValue')} value={value} onChangeText={setValue} autoCapitalize="none" testID="constraint-value" />
        <Button title={t('mobile.wizard.add')} kind="secondary" icon="add" disabled={!value.trim()} onPress={() => void addConstraint()} testID="constraint-add" />
      </Card>
      <ActionError error={error} />
    </>
  );
}

function Review({ data, onComplete, onAbandon, busy }) {
  const { t } = useTranslation();
  const [abandoning, setAbandoning] = useState(false);
  const done = isCompleted(data);
  return (
    <>
      <Card testID="wizard-review">
        <T variant="title">{data.topic.name}</T>
        <T variant="small">{t('mobile.wizard.reviewCounts', { dimensions: (data.dimensions || []).length, constraints: (data.constraints || []).length, answered: answeredCount(data.questions) })}</T>
        <T numberOfLines={8}>{data.topic.brief}</T>
      </Card>
      {done ? (
        <Banner kind="success" title={t('mobile.wizard.applied')}>
          <Button title={t('mobile.wizard.openTopic')} kind="secondary" onPress={() => router.replace(topicHref(data.topic.id))} />
        </Banner>
      ) : (
        <>
          <T variant="muted">{t('mobile.wizard.reviewBody')}</T>
          <Button title={data.topic.status === 'Draft' ? t('mobile.wizard.create') : t('mobile.wizard.apply')} icon="checkmark-circle-outline" busy={busy === 'complete'} disabled={Boolean(busy)} onPress={onComplete} testID="wizard-complete" />
          <Button title={t('mobile.wizard.abandon')} kind="ghost" icon="trash-outline" onPress={() => setAbandoning(true)} testID="wizard-abandon" />
          {abandoning ? (
            <Banner kind="danger" title={data.topic.status === 'Draft' ? t('mobile.wizard.abandonDraft') : t('mobile.wizard.abandonExisting')}>
              <ButtonRow>
                <Button title={t('mobile.wizard.abandonConfirm')} kind="danger" busy={busy === 'abandon'} onPress={onAbandon} testID="wizard-abandon-confirm" />
                <Button title={t('mobile.common.cancel')} kind="ghost" onPress={() => setAbandoning(false)} />
              </ButtonRow>
            </Banner>
          ) : null}
        </>
      )}
    </>
  );
}

/**
 * The refinement wizard as a compact mobile flow (plan section 13): Describe, Dimensions, Questions, Context, and
 * Review. Every step is saved by the server, so leaving and coming back resumes (`?session=` or `?topicId=`). The
 * model-backed steps (propose dimensions, ask questions, draft the context) fail with 502 ModelError when no model is
 * set up; each step can then be done by hand (add dimensions, skip questions, write the brief).
 */
export function WizardScreen() {
  const { t } = useTranslation();
  const { client } = useAuth();
  const params = useLocalSearchParams();
  const sessionParam = params && params.session ? String(params.session) : null;
  const topicParam = params && params.topicId ? String(params.topicId) : null;
  const boot = useResource(async () => {
    if (sessionParam) return { state: await refinementApi.get(client, sessionParam) };
    if (!topicParam) return { state: null };
    const active = listItems(await refinementApi.list(client, { status: 'Active', topicId: topicParam, pageNumber: 1, pageSize: 20, sortBy: 'createdUtc', sortDirection: 'Descending' }));
    const found = active.find((s) => s.topicId === topicParam);
    return found ? { state: await refinementApi.get(client, found.id) } : { state: null, needsStart: true };
  }, [client, sessionParam, topicParam]);
  const [local, setLocal] = useState(null);
  const [chosenStep, setStep] = useState(null);
  const [busy, setBusy] = useState(null);
  const [error, setError] = useState(null);
  const booted = (boot.data && boot.data.state) || null;
  const data = local || booted;
  const step = chosenStep || (data ? initialStep(data) : 'Describe');
  const setData = (next) => setLocal(next);

  const loaded = (state, to) => {
    setLocal(state);
    setStep(to || initialStep(state));
  };

  const run = async (kind, work) => {
    setBusy(kind);
    setError(null);
    try {
      return await work();
    } catch (e) {
      setError({ kind, error: e });
      return null;
    } finally {
      setBusy(null);
    }
  };

  const generate = (kind, guidance, current = data) =>
    run(kind, async () => {
      const id = current.session.id;
      const next = kind === 'dimensions' ? await refinementApi.proposeDimensions(client, id, guidance) : kind === 'questions' ? await refinementApi.askQuestions(client, id, guidance) : await refinementApi.draftContext(client, id);
      setData(next);
    });

  const start = (body) =>
    run('start', async () => {
      const state = await refinementApi.start(client, body);
      loaded(state, 'Dimensions');
      if (!(state.dimensions || []).length) await generate('dimensions', undefined, state);
    });

  const startAgain = () =>
    run('start', async () => {
      loaded(await topicsApi.refine(client, topicParam));
    });

  const goNext = async () => {
    const to = nextStep(step);
    setError(null);
    setStep(to);
    if (to === 'Questions' && (data.session.round || 0) === 0 && !(data.questions || []).length) await generate('questions');
    else if (to === 'Context' && !hasBrief(data)) await generate('context');
  };

  const complete = () =>
    run('complete', async () => {
      const done = await refinementApi.complete(client, data.session.id);
      setData(done);
      router.replace(topicHref(done.topic.id));
    });

  const abandon = () =>
    run('abandon', async () => {
      const wasDraft = data.topic.status === 'Draft';
      await refinementApi.abandon(client, data.session.id);
      router.replace(wasDraft ? '/topics' : topicHref(data.topic.id));
    });

  const update = (fn) => setLocal((d) => fn(d || booted));

  if (!local && boot.status !== 'ready') {
    return (
      <Screen testID="wizard-loading">
        <ResourceView resource={boot} feature={t('mobile.features.topics')}>
          {() => null}
        </ResourceView>
      </Screen>
    );
  }
  if (!local && boot.data && boot.data.needsStart) {
    return (
      <Screen testID="wizard-refine">
        <T variant="title">{t('mobile.wizard.refineTitle')}</T>
        <T variant="muted">{t('mobile.wizard.refineBody')}</T>
        <ActionError error={error && error.error} />
        <Button title={t('mobile.wizard.refineStart')} icon="color-wand-outline" busy={busy === 'start'} onPress={() => void startAgain()} testID="wizard-refine-start" />
      </Screen>
    );
  }

  const manualHints = {
    dimensions: t('mobile.wizard.manualDimensions'),
    questions: t('mobile.wizard.manualQuestions'),
    context: t('mobile.wizard.manualContext'),
    complete: undefined,
    start: undefined,
  };
  const index = WIZARD_STEPS.indexOf(step);
  const canNext = data && step !== 'Review' && stepAvailable(nextStep(step), data) && !busy;

  return (
    <Screen testID="new-topic-screen">
      {data ? (
        <View style={{ gap: spacing.xs }}>
          <T variant="label">{t('mobile.wizard.stepOf', { number: index + 1, total: WIZARD_STEPS.length })}</T>
          <T variant="heading" testID="wizard-step-title">
            {t('mobile.wizard.steps.' + step)}
          </T>
          <T variant="small">{t('mobile.wizard.stepHelp.' + step)}</T>
        </View>
      ) : null}
      {busy && busy !== 'start' && busy !== 'complete' && busy !== 'abandon' ? (
        <Banner kind="info" title={t('mobile.wizard.thinking')} testID="wizard-busy">
          <BusyLine>{t('mobile.wizard.thinkingBody')}</BusyLine>
        </Banner>
      ) : null}
      <ActionError error={error && error.error} manualHint={error ? manualHints[error.kind] : undefined} onRetry={error && data && ['dimensions', 'questions', 'context'].includes(error.kind) ? () => void generate(error.kind) : undefined} testID="wizard-error" />
      {!data ? <Describe onStart={(body) => void start(body)} busy={busy === 'start'} /> : null}
      {data && step === 'Dimensions' ? <Dimensions data={data} update={update} generate={(k, g) => void generate(k, g)} busy={busy} /> : null}
      {data && step === 'Questions' ? <Questions data={data} update={update} generate={(k, g) => void generate(k, g)} busy={busy} /> : null}
      {data && step === 'Context' ? <Context key={data.topic.lastUpdateUtc || data.topic.id} data={data} update={update} generate={(k) => void generate(k)} busy={busy} /> : null}
      {data && step === 'Review' ? <Review data={data} busy={busy} onComplete={() => void complete()} onAbandon={() => void abandon()} /> : null}
      {data && step !== 'Review' ? (
        <ButtonRow>
          {previousStep(step) !== step ? <Button title={t('mobile.common.back')} kind="secondary" icon="arrow-back" disabled={Boolean(busy)} onPress={() => setStep(previousStep(step))} testID="wizard-back" /> : null}
          <Button title={t('mobile.wizard.next', { step: t('mobile.wizard.steps.' + nextStep(step)) })} icon="arrow-forward" disabled={!canNext} onPress={() => void goNext()} testID="wizard-next" />
        </ButtonRow>
      ) : null}
      {data && step === 'Review' ? <Button title={t('mobile.common.back')} kind="ghost" icon="arrow-back" disabled={Boolean(busy)} onPress={() => setStep(previousStep(step))} /> : null}
      {data && !isCompleted(data) ? <Button title={t('mobile.wizard.saveExit')} kind="ghost" onPress={() => (router.canGoBack() ? router.back() : router.replace('/topics'))} testID="wizard-save-exit" /> : null}
    </Screen>
  );
}
