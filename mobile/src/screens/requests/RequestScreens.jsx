import React, { useState } from 'react';
import { View } from 'react-native';
import { router } from 'expo-router';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { isModelError } from '@/api/errors';
import { requestsApi } from '@/api/research';
import { ButtonRow, ChipGroup } from '@/components/Chips';
import { Markdown } from '@/components/Markdown';
import { QuestionCard } from '@/components/QuestionCard';
import { ActionError } from '@/components/states';
import { Badge, Banner, Button, Card, Field, KeyValue, Screen, SectionHeader, SwitchRow, T } from '@/components/ui';
import { DELIVERABLES, DELIVERABLE_LENGTHS, DIMENSION_WEIGHT_TONES, REQUEST_STATUS_TONES } from '@/lib/enums';
import { formatCost, formatDateTime } from '@/lib/format';
import { deviceTimeZone, isLocalDateTime, localToUtc, utcToLocal } from '@/lib/schedule';
import { useAuth } from '@/state/AuthContext';
import { usePolling } from '@/state/usePolling';
import { spacing } from '@/theme/palette';
import { DetailHeader, DetailScreen, ItemCard } from '../common/DetailScreen';
import { ResourceListScreen } from '../common/ResourceListScreen';

export const requestHref = (id) => '/today/requests/' + encodeURIComponent(id);

export const canConfirm = (r) => r.status === 'Clarifying' && !r.outOfScopeReason;
export const canCancel = (r) => r.status === 'Clarifying' || r.status === 'Scheduled';
export const canConvert = (r) => r.status !== 'Declined' && !r.convertedTopicId;

export const EMPTY_REQUEST = { requestText: '', deliverables: ['Report'], length: 'Standard', timing: 'now', runAt: '', emailRecipients: '', publishToFeed: false, skipQuestions: false };

/** The ResearchRequestCreateRequest for the form. "Run once at" is entered in the device's time zone. */
export function requestBody(form, timeZone = deviceTimeZone()) {
  const deliverables = form.deliverables.length ? form.deliverables : ['Report'];
  return {
    requestText: form.requestText.trim(),
    deliverables,
    length: form.length,
    runAtUtc: form.timing === 'at' ? localToUtc(form.runAt, timeZone) : null,
    emailRecipients: deliverables.includes('Email')
      ? form.emailRecipients
          .split(/[,\s;]+/)
          .map((s) => s.trim())
          .filter(Boolean)
      : [],
    publishToFeed: deliverables.includes('Podcast') && Boolean(form.publishToFeed),
    skipQuestions: Boolean(form.skipQuestions),
  };
}

/** One-time requests, newest first, with their status and deliverables. */
export function RequestsScreen() {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  return (
    <ResourceListScreen
      testID="requests-screen"
      load={() => requestsApi.list(client, { pageNumber: 1, pageSize: 50, sortBy: 'createdUtc', sortDirection: 'Descending' })}
      deps={[client]}
      feature={t('mobile.features.requests')}
      header={<Button title={t('mobile.requests.new')} icon="add" onPress={() => router.push('/today/new-request')} testID="requests-new" />}
      empty={{ icon: 'sparkles-outline', title: t('mobile.requests.emptyTitle'), body: t('mobile.requests.emptyBody') }}
      detailHref={requestHref}
      renderDetail={(id, { onChanged }) => <RequestDetail key={id} id={id} onChanged={onChanged} />}
      placeholder={t('mobile.requests.select')}
      renderRow={(r, { selected, onPress }) => (
        <ItemCard
          title={r.title || r.requestText}
          selected={selected}
          onPress={onPress}
          testID={'request-row-' + r.id}
          badges={[{ label: t('mobile.enums.requestStatus.' + r.status), tone: REQUEST_STATUS_TONES[r.status] || 'neutral' }, ...(r.deliverables || []).map((d) => ({ label: t('mobile.enums.deliverable.' + d), tone: 'neutral' }))]}
          lines={[formatDateTime(r.createdUtc, i18n.language), r.errorMessage && r.status === 'Clarifying' ? t('mobile.requests.needsPlan') : null]}
        />
      )}
    />
  );
}

/** "Ask for anything once": text, deliverables, length, timing, recipients, feed, and "Just do it". */
export function NewRequestScreen() {
  const { t } = useTranslation();
  const { client, state } = useAuth();
  const [form, setForm] = useState(EMPTY_REQUEST);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [saved, setSaved] = useState(null);
  const set = (patch) => setForm((f) => ({ ...f, ...patch }));
  const zone = deviceTimeZone();
  const email = state.user && state.user.email ? state.user.email : '';
  const invalidTime = form.timing === 'at' && !isLocalDateTime(form.runAt);

  const submit = async () => {
    const body = requestBody(form, zone);
    if (!body.requestText) return;
    setBusy(true);
    setError(null);
    setSaved(null);
    try {
      const created = await requestsApi.create(client, body);
      let result = created;
      const planned = created.request.status === 'Clarifying' && created.topic && created.topic.brief;
      if (body.skipQuestions && planned) result = await requestsApi.confirm(client, created.request.id, body.runAtUtc);
      router.replace(requestHref(result.request.id));
    } catch (e) {
      setError(e);
      if (isModelError(e)) {
        // The server keeps the request (in Clarifying) even when planning fails; find it so it can be opened.
        try {
          const latest = listItems(await requestsApi.list(client, { pageNumber: 1, pageSize: 1, sortBy: 'createdUtc', sortDirection: 'Descending' }))[0];
          if (latest && latest.requestText === body.requestText) setSaved(latest);
        } catch {
          setSaved(null);
        }
      }
    } finally {
      setBusy(false);
    }
  };

  return (
    <Screen testID="new-request-screen">
      <T variant="muted">{t('mobile.requests.intro')}</T>
      <Field label={t('mobile.requests.text')} value={form.requestText} onChangeText={(v) => set({ requestText: v })} multiline maxLength={4000} placeholder={t('mobile.requests.placeholder')} testID="request-text" />
      <T variant="label">{t('mobile.requests.deliverables')}</T>
      <ChipGroup multiple options={DELIVERABLES.map((d) => ({ value: d, label: t('mobile.enums.deliverable.' + d), testID: 'deliverable-' + d }))} value={form.deliverables} onChange={(v) => set({ deliverables: v })} accessibilityLabel={t('mobile.requests.deliverables')} />
      <T variant="label">{t('mobile.requests.length')}</T>
      <ChipGroup options={DELIVERABLE_LENGTHS.map((l) => ({ value: l, label: t('mobile.enums.length.' + l) }))} value={form.length} onChange={(v) => set({ length: v })} accessibilityLabel={t('mobile.requests.length')} />
      <T variant="label">{t('mobile.requests.timing')}</T>
      <ChipGroup
        options={[
          { value: 'now', label: t('mobile.requests.runNow'), testID: 'timing-now' },
          { value: 'at', label: t('mobile.requests.runAt'), testID: 'timing-at' },
        ]}
        value={form.timing}
        onChange={(v) => set({ timing: v })}
        accessibilityLabel={t('mobile.requests.timing')}
      />
      {form.timing === 'at' ? <Field label={t('mobile.requests.when')} hint={t('mobile.requests.whenHint', { zone })} value={form.runAt} onChangeText={(v) => set({ runAt: v })} placeholder="2026-12-01 07:00" autoCapitalize="none" autoCorrect={false} testID="request-when" /> : null}
      {form.deliverables.includes('Email') ? (
        <Field label={t('mobile.requests.recipients')} hint={t('mobile.requests.recipientsHint', { email })} value={form.emailRecipients} onChangeText={(v) => set({ emailRecipients: v })} placeholder={email} autoCapitalize="none" keyboardType="email-address" autoCorrect={false} />
      ) : null}
      {form.deliverables.includes('Podcast') ? <SwitchRow title={t('mobile.requests.publishToFeed')} detail={t('mobile.requests.publishToFeedHint')} value={form.publishToFeed} onValueChange={(v) => set({ publishToFeed: v })} /> : null}
      <SwitchRow title={t('mobile.requests.justDoIt')} detail={t('mobile.requests.justDoItHint')} value={form.skipQuestions} onValueChange={(v) => set({ skipQuestions: v })} testID="request-skip" />
      {busy ? <Banner kind="info">{t('mobile.requests.planning')}</Banner> : null}
      <ActionError error={error} manualHint={saved ? t('mobile.requests.savedAnyway') : undefined} />
      {saved ? <Button title={t('mobile.requests.openSaved')} kind="secondary" onPress={() => router.replace(requestHref(saved.id))} testID="request-open-saved" /> : null}
      <Button title={form.skipQuestions ? t('mobile.requests.submitNow') : t('mobile.requests.submit')} icon="send" busy={busy} disabled={!form.requestText.trim() || invalidTime} onPress={() => void submit()} testID="request-submit" />
    </Screen>
  );
}

/** A future run time stays; a past one runs now. */
export function confirmTime(r, now = Date.now()) {
  return r && r.runAtUtc && new Date(r.runAtUtc).getTime() > now ? r.runAtUtc : null;
}

function ConfirmBox({ detail, onDone }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  const zone = deviceTimeZone();
  const r = detail.request;
  const at = confirmTime(r);
  const [mode, setMode] = useState(at ? 'at' : 'now');
  const [local, setLocal] = useState(at ? utcToLocal(at, zone) : '');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const planned = Boolean(detail.topic && detail.topic.brief);
  const confirm = async () => {
    setBusy(true);
    setError(null);
    try {
      onDone(await requestsApi.confirm(client, r.id, mode === 'at' ? localToUtc(local, zone) : null));
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card testID="confirm-box">
      <T variant="label">{t('mobile.requests.confirmTitle')}</T>
      <ChipGroup
        options={[
          { value: 'now', label: t('mobile.requests.runNow') },
          { value: 'at', label: t('mobile.requests.runAt') },
        ]}
        value={mode}
        onChange={setMode}
        accessibilityLabel={t('mobile.requests.timing')}
      />
      {mode === 'at' ? <Field label={t('mobile.requests.when')} hint={t('mobile.requests.whenHint', { zone })} value={local} onChangeText={setLocal} placeholder="2026-12-01 07:00" autoCapitalize="none" autoCorrect={false} /> : null}
      {!planned ? <T variant="small">{t('mobile.requests.needsPlan')}</T> : null}
      <ActionError error={error} />
      <Button title={mode === 'at' ? t('mobile.requests.confirmAt') : t('mobile.requests.confirmNow')} icon="checkmark-circle-outline" busy={busy} disabled={!planned || (mode === 'at' && !isLocalDateTime(local))} onPress={() => void confirm()} testID="request-confirm" />
    </Card>
  );
}

/** One request: the plan, clarifying questions with replan, confirm now or at a time, and the request's actions. */
export function RequestDetail({ id, onChanged }) {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const [busy, setBusy] = useState(null);
  const [error, setError] = useState(null);
  const [deleting, setDeleting] = useState(false);
  return (
    <DetailScreen testID="request-detail" load={() => requestsApi.get(client, id)} deps={[client, id]} feature={t('mobile.features.requests')}>
      {(detail, resource) => (
        <RequestBody
          detail={detail}
          resource={resource}
          busy={busy}
          error={error}
          deleting={deleting}
          i18n={i18n}
          t={t}
          act={async (kind, work, after) => {
            setBusy(kind);
            setError(null);
            try {
              const result = await work();
              if (after) after(result);
              if (onChanged) onChanged();
            } catch (e) {
              setError(e);
            } finally {
              setBusy(null);
            }
          }}
          setDeleting={setDeleting}
          client={client}
        />
      )}
    </DetailScreen>
  );
}

function RequestBody({ detail, resource, busy, error, deleting, setDeleting, act, client, t, i18n }) {
  const r = detail.request;
  const running = r.status === 'Scheduled' || r.status === 'Running';
  usePolling(resource.poll, running, 5000);
  const apply = (next) => {
    if (next && next.request) resource.setData(next);
    else void resource.poll();
  };
  const questions = (detail.questions || []).slice().sort((a, b) => (a.question.sortOrder || 0) - (b.question.sortOrder || 0));
  return (
    <>
      <DetailHeader
        title={r.title || r.requestText}
        subtitle={r.title && r.title !== r.requestText ? r.requestText : undefined}
        badges={[{ label: t('mobile.enums.requestStatus.' + r.status), tone: REQUEST_STATUS_TONES[r.status] || 'neutral' }, ...(r.deliverables || []).map((d) => ({ label: t('mobile.enums.deliverable.' + d), tone: 'neutral' }))]}
      />
      {r.status === 'Declined' ? <Banner kind="warning" title={t('mobile.requests.declinedTitle')}>{r.outOfScopeReason || ''}</Banner> : null}
      {r.status === 'Failed' && r.errorMessage ? <Banner kind="danger" title={t('mobile.requests.failedTitle')}>{r.errorMessage}</Banner> : null}
      {r.status === 'Clarifying' && r.errorMessage && !error ? (
        <Banner kind="warning" title={t('mobile.requests.planFailedTitle')} testID="plan-failed">
          <View style={{ gap: spacing.sm }}>
            <T variant="small">{r.errorMessage}</T>
            <Button title={t('mobile.requests.replan')} kind="secondary" icon="refresh" busy={busy === 'replan'} onPress={() => void act('replan', () => requestsApi.replan(client, r.id), apply)} testID="request-replan" />
          </View>
        </Banner>
      ) : null}
      <ActionError error={error} />
      {running ? <Banner kind="info">{r.status === 'Scheduled' && r.runAtUtc ? t('mobile.requests.scheduledFor', { date: formatDateTime(r.runAtUtc, i18n.language) }) : t('mobile.requests.runningBody')}</Banner> : null}
      {r.briefingId ? <Button title={t('mobile.requests.openReport')} icon="newspaper-outline" onPress={() => router.push('/today/briefings/' + encodeURIComponent(r.briefingId))} testID="request-open-briefing" /> : null}
      <Card>
        <KeyValue label={t('mobile.requests.length')} value={t('mobile.enums.length.' + r.length)} />
        <KeyValue label={t('mobile.requests.runAtLabel')} value={r.runAtUtc ? formatDateTime(r.runAtUtc, i18n.language) : t('mobile.requests.whenConfirmed')} />
        {(r.deliverables || []).includes('Email') ? <KeyValue label={t('mobile.requests.recipients')} value={(r.emailRecipients || []).join(', ') || t('mobile.requests.toYou')} /> : null}
        {r.estimatedCostUsd !== undefined && r.estimatedCostUsd !== null ? <KeyValue label={t('mobile.requests.estimatedCost')} value={formatCost(r.estimatedCostUsd, i18n.language)} /> : null}
        <KeyValue label={t('mobile.requests.created')} value={formatDateTime(r.createdUtc, i18n.language)} />
        {r.completedUtc ? <KeyValue label={t('mobile.requests.completed')} value={formatDateTime(r.completedUtc, i18n.language)} /> : null}
      </Card>
      {detail.topic && detail.topic.brief ? (
        <>
          <SectionHeader title={t('mobile.requests.plan')} />
          <Card>
            <Markdown text={detail.topic.brief} />
            {(detail.dimensions || []).length ? (
              <View style={{ flexDirection: 'row', flexWrap: 'wrap', gap: spacing.xs }}>
                {detail.dimensions.map((d) => (
                  <Badge key={d.id} label={d.name} tone={DIMENSION_WEIGHT_TONES[d.weight] || 'neutral'} />
                ))}
              </View>
            ) : null}
          </Card>
        </>
      ) : null}
      {questions.length ? (
        <>
          <SectionHeader title={t('mobile.requests.questions')} />
          {questions.map((item, i) => (
            <QuestionCard
              key={item.question.id + ':' + (item.question.lastUpdateUtc || '')}
              item={item}
              index={i}
              disabled={r.status !== 'Clarifying'}
              onAnswer={async (body) => {
                const updated = await requestsApi.answer(client, r.id, item.question.id, body);
                resource.setData((d) => ({ ...d, questions: d.questions.map((q) => (q.question.id === item.question.id ? (updated && updated.question ? updated : q) : q)) }));
              }}
            />
          ))}
          {r.status === 'Clarifying' ? <Button title={t('mobile.requests.replanWithAnswers')} kind="secondary" icon="sparkles-outline" busy={busy === 'replan'} onPress={() => void act('replan', () => requestsApi.replan(client, r.id), apply)} /> : null}
        </>
      ) : null}
      {canConfirm(r) ? <ConfirmBox detail={detail} onDone={apply} /> : null}
      <ButtonRow>
        {canCancel(r) ? <Button title={t('mobile.requests.cancel')} kind="secondary" icon="close-circle-outline" busy={busy === 'cancel'} onPress={() => void act('cancel', () => requestsApi.cancel(client, r.id), apply)} testID="request-cancel" /> : null}
        {canConvert(r) ? (
          <Button
            title={t('mobile.requests.convert')}
            kind="secondary"
            icon="repeat"
            busy={busy === 'convert'}
            onPress={() => void act('convert', () => requestsApi.convert(client, r.id), (topic) => router.push('/topics/' + encodeURIComponent(topic.id)))}
            testID="request-convert"
          />
        ) : null}
        {r.convertedTopicId ? <Button title={t('mobile.requests.openTopic')} kind="secondary" onPress={() => router.push('/topics/' + encodeURIComponent(r.convertedTopicId))} /> : null}
        <Button title={t('mobile.common.delete')} kind="ghost" icon="trash-outline" onPress={() => setDeleting(true)} testID="request-delete" />
      </ButtonRow>
      {deleting ? (
        <Banner kind="danger" title={t('mobile.requests.deleteTitle')}>
          <ButtonRow>
            <Button
              title={t('mobile.requests.deleteConfirm')}
              kind="danger"
              busy={busy === 'delete'}
              onPress={() =>
                void act(
                  'delete',
                  () => requestsApi.remove(client, r.id),
                  () => (router.canGoBack() ? router.back() : router.replace('/today/requests')),
                )
              }
              testID="request-delete-confirm"
            />
            <Button title={t('mobile.common.cancel')} kind="ghost" onPress={() => setDeleting(false)} />
          </ButtonRow>
        </Banner>
      ) : null}
    </>
  );
}
