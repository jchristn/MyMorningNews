import React, { useState } from 'react';
import { View } from 'react-native';
import Ionicons from '@expo/vector-icons/Ionicons';
import { useTranslation } from 'react-i18next';
import { catalogApi } from '@/api/catalog';
import { listItems } from '@/api/client';
import { ApiError, isNotAvailable } from '@/api/errors';
import { editionsApi, topicsApi } from '@/api/research';
import { ButtonRow, ChipGroup } from '@/components/Chips';
import { Markdown } from '@/components/Markdown';
import { ScheduleEditor, ScheduleSummary } from '@/components/ScheduleEditor';
import { ActionError, ResourceView } from '@/components/states';
import { Banner, Button, Card, Field, ListRow, SectionHeader, SwitchRow, T } from '@/components/ui';
import { LOOKBACK_MODES } from '@/lib/enums';
import { formatDateTime } from '@/lib/format';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { DetailHeader, DetailScreen, ItemCard } from '../common/DetailScreen';
import { ResourceListScreen } from '../common/ResourceListScreen';
import { ComposeStatus, useCompose } from '../today/TodayScreens';
import { EditionHistory } from '../topics/TopicHistory';

export const editionHref = (id) => '/topics/editions/' + encodeURIComponent(id);

/** The settings form for an edition. */
export function editionForm(edition) {
  return {
    name: edition.name || '',
    description: edition.description || '',
    enabled: edition.enabled !== false,
    audioEnabled: Boolean(edition.audioEnabled),
    emailEnabled: Boolean(edition.emailEnabled),
    freshResearchBeforeCompose: Boolean(edition.freshResearchBeforeCompose),
    lookbackMode: edition.lookbackMode || 'SinceLastEdition',
    lookbackHours: String(edition.lookbackHours || 24),
  };
}

/** The EditionUpsertRequest for the form (look-back hours 1 to 720). */
export function editionBody(form) {
  return {
    name: form.name.trim(),
    description: form.description.trim() || null,
    enabled: form.enabled,
    audioEnabled: form.audioEnabled,
    emailEnabled: form.emailEnabled,
    freshResearchBeforeCompose: form.freshResearchBeforeCompose,
    lookbackMode: form.lookbackMode,
    lookbackHours: Math.min(720, Math.max(1, Math.round(Number(form.lookbackHours) || 24))),
  };
}

/** Toggle a topic in an ordered selection (new picks go last). */
export function toggleTopic(selection, topicId) {
  return selection.includes(topicId) ? selection.filter((x) => x !== topicId) : [...selection, topicId];
}

export function EditionsScreen() {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  return (
    <ResourceListScreen
      testID="editions-screen"
      load={() => editionsApi.list(client, { pageNumber: 1, pageSize: 100, sortBy: 'name', sortDirection: 'Ascending' })}
      deps={[client]}
      feature={t('mobile.features.editions')}
      empty={{ icon: 'calendar-outline', title: t('mobile.editions.emptyTitle'), body: t('mobile.editions.emptyBody') }}
      detailHref={editionHref}
      renderDetail={(id, { onChanged }) => <EditionDetail key={id} id={id} onChanged={onChanged} />}
      placeholder={t('mobile.editions.select')}
      renderRow={(e, { selected, onPress }) => (
        <ItemCard
          title={e.name}
          selected={selected}
          onPress={onPress}
          testID={'edition-row-' + e.id}
          badges={[
            { label: e.enabled === false ? t('mobile.editions.off') : t('mobile.editions.on'), tone: e.enabled === false ? 'neutral' : 'success' },
            e.audioEnabled ? { label: t('mobile.editions.audio'), tone: 'neutral' } : null,
            e.emailEnabled ? { label: t('mobile.editions.email'), tone: 'neutral' } : null,
          ]}
          lines={[e.description, e.lastComposedUtc ? t('mobile.editions.lastComposed', { date: formatDateTime(e.lastComposedUtc, i18n.language) }) : t('mobile.editions.neverComposed')]}
        />
      )}
    />
  );
}

function SettingsCard({ detail, onSaved }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  const [form, setForm] = useState(() => editionForm(detail.edition));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const [saved, setSaved] = useState(false);
  const set = (patch) => {
    setSaved(false);
    setForm((f) => ({ ...f, ...patch }));
  };
  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      const next = await editionsApi.update(client, detail.edition.id, editionBody(form));
      if (next && next.edition) setForm(editionForm(next.edition));
      onSaved(next);
      setSaved(true);
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card testID="edition-settings">
      <Field label={t('mobile.editions.name')} value={form.name} onChangeText={(v) => set({ name: v })} maxLength={200} testID="edition-name" />
      <Field label={t('mobile.editions.description')} value={form.description} onChangeText={(v) => set({ description: v })} multiline />
      <SwitchRow title={t('mobile.editions.enabled')} detail={t('mobile.editions.enabledHint')} value={form.enabled} onValueChange={(v) => set({ enabled: v })} testID="edition-enabled" />
      <SwitchRow title={t('mobile.editions.audioEnabled')} value={form.audioEnabled} onValueChange={(v) => set({ audioEnabled: v })} />
      <SwitchRow title={t('mobile.editions.emailEnabled')} value={form.emailEnabled} onValueChange={(v) => set({ emailEnabled: v })} />
      <SwitchRow title={t('mobile.editions.fresh')} detail={t('mobile.editions.freshHint')} value={form.freshResearchBeforeCompose} onValueChange={(v) => set({ freshResearchBeforeCompose: v })} />
      <T variant="label">{t('mobile.editions.lookback')}</T>
      <ChipGroup options={LOOKBACK_MODES.map((m) => ({ value: m, label: t('mobile.editions.lookbackModes.' + m) }))} value={form.lookbackMode} onChange={(v) => set({ lookbackMode: v })} accessibilityLabel={t('mobile.editions.lookback')} />
      {form.lookbackMode === 'FixedHours' ? <Field label={t('mobile.editions.lookbackHours')} value={form.lookbackHours} onChangeText={(v) => set({ lookbackHours: v.replace(/[^0-9]/g, '') })} keyboardType="number-pad" /> : null}
      <ActionError error={error} />
      {saved ? <Banner kind="success">{t('mobile.editions.saved')}</Banner> : null}
      <Button title={t('mobile.common.save')} busy={busy} disabled={!form.name.trim()} onPress={() => void save()} testID="edition-save" />
    </Card>
  );
}

function TopicsCard({ detail, onSaved }) {
  const p = useTheme();
  const { t } = useTranslation();
  const { client } = useAuth();
  const topics = useResource(() => topicsApi.list(client, { pageNumber: 1, pageSize: 200, sortBy: 'name', sortDirection: 'Ascending', isOneTime: false }), [client]);
  const current = (detail.topics || []).slice().sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0)).map((x) => x.topicId);
  const [selection, setSelection] = useState(current);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const key = current.join(',');
  const dirty = selection.join(',') !== key;
  const all = listItems(topics.data).filter((x) => x.status !== 'Draft');
  const covered = detail.coveredTopicIds || [];
  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      onSaved(await editionsApi.setTopics(client, detail.edition.id, selection));
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card testID="edition-topics">
      <T variant="small">{selection.length ? t('mobile.editions.topicsChosen', { count: selection.length }) : t('mobile.editions.topicsAll')}</T>
      {all.map((topic) => {
        const on = selection.includes(topic.id);
        const order = selection.indexOf(topic.id);
        return (
          <ListRow
            key={topic.id}
            title={(on ? order + 1 + '. ' : '') + topic.name}
            detail={[t('mobile.enums.topicStatus.' + topic.status), !selection.length && covered.includes(topic.id) ? t('mobile.editions.included') : null].filter(Boolean).join(' · ')}
            onPress={() => setSelection((s) => toggleTopic(s, topic.id))}
            accessibilityLabel={topic.name + (on ? ', ' + t('mobile.editions.selected') : '')}
            right={<Ionicons name={on ? 'checkmark-circle' : 'ellipse-outline'} size={22} color={on ? p.primary : p.muted} />}
            testID={'edition-topic-' + topic.id}
          />
        );
      })}
      {topics.status === 'ready' && all.length === 0 ? <T variant="muted">{t('mobile.editions.noTopics')}</T> : null}
      <ActionError error={error} />
      <ButtonRow>
        <Button title={t('mobile.editions.saveTopics')} busy={busy} disabled={!dirty} onPress={() => void save()} testID="edition-topics-save" />
        {selection.length ? <Button title={t('mobile.editions.useAll')} kind="ghost" onPress={() => setSelection([])} /> : null}
      </ButtonRow>
    </Card>
  );
}

/** The most system briefings one edition can include (EditionPublicationsRequest). */
export const MAX_EDITION_PUBLICATIONS = 20;

/**
 * System briefings inside a custom edition (`GET/PUT /v1.0/api/editions/{id}/publications`): pick subscribed
 * publications in order. The edition summarizes their issues in its period and links to each; it never starts system
 * research. Entries that are no longer readable stay listed (marked, and skipped at composition) until removed. Hidden
 * on a server without the route.
 */
export function PublicationsCard({ detail, onSaved }) {
  const p = useTheme();
  const { t } = useTranslation();
  const { client } = useAuth();
  const editionId = detail.edition.id;
  const current = useResource(() => (Array.isArray(detail.publications) ? Promise.resolve(detail.publications) : editionsApi.publications(client, editionId, { pageNumber: 1, pageSize: MAX_EDITION_PUBLICATIONS }).then(listItems)), [client, editionId, detail.publications]);
  const catalog = useResource(() => catalogApi.list(client, { pageNumber: 1, pageSize: 100 }), [client]);
  const saved = (Array.isArray(current.data) ? current.data : []).slice().sort((a, b) => (a.sortOrder || 0) - (b.sortOrder || 0));
  const savedKey = saved.map((x) => x.publicationId).join(',');
  const [selection, setSelection] = useState(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  if (current.status === 'notAvailable' || (current.status === 'error' && isNotAvailable(current.error))) return null;
  const chosen = selection || saved.map((x) => x.publicationId);
  const subscribed = listItems(catalog.data).filter((entry) => entry.subscribed && !entry.suspended);
  const titles = {};
  for (const x of saved) titles[x.publicationId] = x.title;
  for (const entry of subscribed) titles[entry.id] = entry.title;
  const unavailable = new Set(saved.filter((x) => x.available === false).map((x) => x.publicationId));
  // Saved picks that are not (or no longer) subscribed are listed first so they can be removed.
  const rows = [...saved.map((x) => x.publicationId).filter((id) => !subscribed.some((e) => e.id === id)), ...subscribed.map((e) => e.id)];
  const dirty = chosen.join(',') !== savedKey;
  const save = async () => {
    setBusy(true);
    setError(null);
    try {
      const next = await editionsApi.setPublications(client, editionId, chosen);
      setSelection(null);
      onSaved(next);
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  };
  const capped = error instanceof ApiError && error.status === 402;
  return (
    <>
      <SectionHeader title={t('mobile.editionPublications.title')} />
      <Card testID="edition-publications">
        <T variant="muted">{t('mobile.editionPublications.help')}</T>
        <ResourceView resource={current} feature={t('mobile.editionPublications.title')}>
          {() => (
            <>
              <T variant="small">{chosen.length ? t('mobile.editionPublications.chosen', { count: chosen.length, max: MAX_EDITION_PUBLICATIONS }) : t('mobile.editionPublications.none')}</T>
              {rows.map((id) => {
                const on = chosen.includes(id);
                const order = chosen.indexOf(id);
                const gone = unavailable.has(id) || !subscribed.some((e) => e.id === id);
                const title = titles[id] || t('mobile.editionPublications.untitled');
                const full = !on && chosen.length >= MAX_EDITION_PUBLICATIONS;
                return (
                  <ListRow
                    key={id}
                    title={(on ? order + 1 + '. ' : '') + title}
                    detail={gone ? t('mobile.editionPublications.unavailable') : undefined}
                    onPress={full || (gone && !on) ? undefined : () => setSelection(toggleTopic(chosen, id))}
                    accessibilityLabel={title + (on ? ', ' + t('mobile.editions.selected') : '') + (gone ? ', ' + t('mobile.editionPublications.unavailable') : '')}
                    right={<Ionicons name={on ? 'checkmark-circle' : 'ellipse-outline'} size={22} color={on ? p.primary : p.muted} />}
                    testID={'edition-publication-' + id}
                  />
                );
              })}
              {catalog.status === 'ready' && rows.length === 0 ? <T variant="muted">{t('mobile.editionPublications.noSubscriptions')}</T> : null}
            </>
          )}
        </ResourceView>
        {capped ? (
          <Banner kind="warning" testID="edition-publications-capped">
            {t('mobile.editionPublications.capped')}
          </Banner>
        ) : (
          <ActionError error={error} />
        )}
        <ButtonRow>
          <Button title={t('mobile.editionPublications.save')} busy={busy} disabled={!dirty} onPress={() => void save()} testID="edition-publications-save" />
          {chosen.length ? <Button title={t('mobile.editionPublications.clear')} kind="ghost" onPress={() => setSelection([])} testID="edition-publications-clear" /> : null}
        </ButtonRow>
      </Card>
    </>
  );
}

function ScheduleCard({ detail, onChanged }) {
  const { t } = useTranslation();
  const [editing, setEditing] = useState(null);
  const schedules = detail.schedules || [];
  const done = () => {
    setEditing(null);
    onChanged();
  };
  return (
    <View style={{ gap: spacing.sm }}>
      {schedules.map((s) =>
        editing === s.id ? (
          <ScheduleEditor key={s.id} schedule={s} onSaved={done} onRemoved={done} onCancel={() => setEditing(null)} />
        ) : (
          <ScheduleSummary key={s.id} schedule={s} onPress={() => setEditing(s.id)} testID={'edition-schedule-' + s.id} />
        ),
      )}
      {schedules.length === 0 && editing !== 'new' ? <T variant="muted">{t('mobile.editions.noSchedule')}</T> : null}
      {editing === 'new' ? (
        <ScheduleEditor owner={{ ownerType: 'Edition', ownerId: detail.edition.id }} onSaved={done} onCancel={() => setEditing(null)} />
      ) : (
        <Button title={t('mobile.editions.addSchedule')} kind="secondary" icon="add" onPress={() => setEditing('new')} testID="edition-add-schedule" />
      )}
    </View>
  );
}

function PreviewCard({ editionId }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  const [state, setState] = useState({ busy: false, briefing: null, error: null });
  const run = async () => {
    setState({ busy: true, briefing: null, error: null });
    try {
      setState({ busy: false, briefing: await editionsApi.preview(client, editionId), error: null });
    } catch (e) {
      setState({ busy: false, briefing: null, error: e });
    }
  };
  return (
    <>
      <Button title={state.briefing ? t('mobile.editions.previewAgain') : t('mobile.editions.preview')} kind="secondary" icon="eye-outline" busy={state.busy} onPress={() => void run()} testID="edition-preview" />
      <ActionError error={state.error} />
      {state.briefing ? (
        <Card testID="edition-preview-body">
          <T variant="label">{t('mobile.editions.previewLabel')}</T>
          <Markdown text={state.briefing.markdown || ''} />
        </Card>
      ) : null}
    </>
  );
}

/** One edition (plan section 21.3): Compose now with status polling, preview, settings, comparison, topics in order, system briefings, and schedule. */
export function EditionDetail({ id, onChanged }) {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  return (
    <DetailScreen testID="edition-detail" load={() => editionsApi.get(client, id)} deps={[client, id]} feature={t('mobile.features.editions')}>
      {(detail, resource) => (
        <EditionBody
          detail={detail}
          locale={i18n.language}
          t={t}
          onSaved={(next) => {
            resource.setData(next && next.edition ? next : detail);
            if (onChanged) onChanged();
          }}
          onReload={() => void resource.poll()}
        />
      )}
    </DetailScreen>
  );
}

function EditionBody({ detail, onSaved, onReload, t, locale }) {
  const compose = useCompose(onReload);
  const e = detail.edition;
  return (
    <>
      <DetailHeader
        title={e.name}
        subtitle={e.lastComposedUtc ? t('mobile.editions.lastComposed', { date: formatDateTime(e.lastComposedUtc, locale) }) : t('mobile.editions.neverComposed')}
        badges={[{ label: e.enabled === false ? t('mobile.editions.off') : t('mobile.editions.on'), tone: e.enabled === false ? 'neutral' : 'success' }]}
      />
      <Button title={t('mobile.compose.now')} icon="flash-outline" busy={compose.status === 'starting' || compose.status === 'composing'} onPress={() => void compose.compose(e.id)} testID="edition-compose" />
      <ComposeStatus compose={compose} />
      <PreviewCard editionId={e.id} />
      <SectionHeader title={t('mobile.editions.settings')} />
      <SettingsCard key={e.id} detail={detail} onSaved={onSaved} />
      <EditionHistory key={'history-' + e.id + (e.lastUpdateUtc || '')} edition={e} onSaved={onSaved} />
      <SectionHeader title={t('mobile.editions.topics')} />
      <TopicsCard key={(detail.topics || []).map((x) => x.topicId).join(',')} detail={detail} onSaved={onSaved} />
      <PublicationsCard key={'publications-' + e.id} detail={detail} onSaved={onSaved} />
      <SectionHeader title={t('mobile.editions.schedule')} />
      <ScheduleCard detail={detail} onChanged={onReload} />
    </>
  );
}
