import React, { useState } from 'react';
import { RefreshControl, View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { listItems } from '@/api/client';
import { learningApi, topicsApi } from '@/api/research';
import { ButtonRow, ChipGroup } from '@/components/Chips';
import { EmptyState, ResourceView } from '@/components/states';
import { Banner, Button, Card, Field, Screen, SectionHeader, T } from '@/components/ui';
import { formatDate, formatNumber } from '@/lib/format';
import { useAuth } from '@/state/AuthContext';
import { useResource } from '@/state/useResource';
import { useTheme } from '@/theme/ThemeContext';
import { spacing } from '@/theme/palette';
import { ItemCard } from '../common/DetailScreen';

export const PREFERENCE_STATUSES = ['Proposed', 'Active', 'Disabled', 'Rejected'];
export const PREFERENCE_KINDS = ['Prefer', 'Avoid', 'PreferSource', 'AvoidSource', 'StyleNote'];
export const PREFERENCE_STATUS_TONES = { Proposed: 'warning', Active: 'success', Disabled: 'neutral', Rejected: 'danger' };
export const MAX_STATEMENT_LENGTH = 500;

/** The actions a preference in this status offers (the server's accept, reject, disable, and enable). */
export function preferenceActions(status) {
  if (status === 'Proposed') return ['accept', 'reject'];
  if (status === 'Active') return ['disable'];
  if (status === 'Disabled') return ['enable'];
  return [];
}

/** Proposed first (they need a decision), then active, disabled, rejected; newest first within each. */
export function sortPreferences(items) {
  const order = (s) => {
    const i = PREFERENCE_STATUSES.indexOf(s);
    return i < 0 ? 99 : i;
  };
  return (Array.isArray(items) ? items.slice() : []).sort((a, b) => order(a.status) - order(b.status) || String(b.createdUtc || '').localeCompare(String(a.createdUtc || '')));
}

function PreferenceCard({ pref, topicName, onChanged, onRemoved }) {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const [busy, setBusy] = useState(null);
  const [error, setError] = useState(null);
  const [editing, setEditing] = useState(false);
  const [text, setText] = useState(pref.statement || '');
  const [confirmDelete, setConfirmDelete] = useState(false);
  const run = async (key, fn) => {
    setBusy(key);
    setError(null);
    try {
      await fn();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(null);
    }
  };
  const save = () =>
    run('save', async () => {
      onChanged(await learningApi.update(client, pref.id, { statement: text.trim() }));
      setEditing(false);
    });
  const remove = () => {
    if (!confirmDelete) {
      setConfirmDelete(true);
      return;
    }
    void run('delete', async () => {
      await learningApi.remove(client, pref.id);
      onRemoved(pref.id);
    });
  };
  return (
    <ItemCard
      title={pref.statement}
      testID={'preference-' + pref.id}
      badges={[
        { label: t('mobile.preferences.statuses.' + pref.status, { defaultValue: pref.status }), tone: PREFERENCE_STATUS_TONES[pref.status] || 'neutral' },
        { label: t('mobile.preferences.kinds.' + pref.kind, { defaultValue: pref.kind }), tone: 'neutral' },
        { label: pref.origin === 'User' ? t('mobile.preferences.yours') : t('mobile.preferences.learned'), tone: 'neutral' },
      ]}
      lines={[
        topicName || t('mobile.preferences.allTopics'),
        pref.origin !== 'User' && Number.isFinite(Number(pref.confidence)) ? t('mobile.preferences.confidence', { value: formatNumber(pref.confidence, i18n.language, { style: 'percent', maximumFractionDigits: 0 }) }) : null,
        pref.decidedUtc ? t('mobile.preferences.decided', { date: formatDate(pref.decidedUtc, i18n.language) }) : null,
      ]}
    >
      {editing ? (
        <View style={{ gap: spacing.sm }}>
          <Field label={t('mobile.preferences.statement')} value={text} onChangeText={setText} multiline maxLength={MAX_STATEMENT_LENGTH} testID={'preference-text-' + pref.id} />
          <ButtonRow>
            <Button title={t('mobile.common.save')} busy={busy === 'save'} disabled={!text.trim()} onPress={() => void save()} testID={'preference-save-' + pref.id} />
            <Button title={t('mobile.common.cancel')} kind="ghost" onPress={() => setEditing(false)} />
          </ButtonRow>
        </View>
      ) : null}
      {error ? <Banner kind="danger">{error}</Banner> : null}
      {!editing ? (
        <ButtonRow>
          {preferenceActions(pref.status).map((action) => (
            <Button
              key={action}
              title={t('mobile.preferences.actions.' + action)}
              kind={action === 'accept' || action === 'enable' ? 'primary' : 'secondary'}
              busy={busy === action}
              onPress={() => void run(action, async () => onChanged(await learningApi.act(client, pref.id, action)))}
              testID={'preference-' + action + '-' + pref.id}
            />
          ))}
          <Button title={t('mobile.preferences.edit')} kind="ghost" icon="create-outline" onPress={() => setEditing(true)} testID={'preference-edit-' + pref.id} />
          <Button title={confirmDelete ? t('mobile.preferences.confirmDelete') : t('mobile.common.delete')} kind={confirmDelete ? 'danger' : 'ghost'} busy={busy === 'delete'} onPress={remove} testID={'preference-delete-' + pref.id} />
        </ButtonRow>
      ) : null}
    </ItemCard>
  );
}

/** Write your own preference: the statement, its kind, and one topic or all of them. Active at once. */
function NewPreference({ topics, onCreated }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  const [statement, setStatement] = useState('');
  const [kind, setKind] = useState('Prefer');
  const [topicId, setTopicId] = useState('all');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);
  const create = async () => {
    setBusy(true);
    setError(null);
    try {
      onCreated(await learningApi.create(client, { statement: statement.trim(), kind, topicId: topicId === 'all' ? undefined : topicId }));
      setStatement('');
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(false);
    }
  };
  return (
    <Card testID="new-preference">
      <Field label={t('mobile.preferences.statement')} hint={t('mobile.preferences.statementHint')} value={statement} onChangeText={setStatement} multiline maxLength={MAX_STATEMENT_LENGTH} testID="preference-statement" />
      <T variant="label">{t('mobile.preferences.kind')}</T>
      <ChipGroup options={PREFERENCE_KINDS.map((k) => ({ value: k, label: t('mobile.preferences.kinds.' + k), testID: 'preference-kind-' + k }))} value={kind} onChange={setKind} accessibilityLabel={t('mobile.preferences.kind')} />
      {topics.length ? (
        <>
          <T variant="label">{t('mobile.preferences.topic')}</T>
          <ChipGroup
            scroll
            options={[{ value: 'all', label: t('mobile.preferences.allTopics'), testID: 'preference-topic-all' }, ...topics.map((tp) => ({ value: tp.id, label: tp.name, testID: 'preference-topic-' + tp.id }))]}
            value={topicId}
            onChange={setTopicId}
            accessibilityLabel={t('mobile.preferences.topic')}
          />
        </>
      ) : null}
      {error ? <Banner kind="danger">{error}</Banner> : null}
      <Button title={t('mobile.preferences.add')} icon="add" busy={busy} disabled={!statement.trim()} onPress={() => void create()} testID="preference-add" />
    </Card>
  );
}

/**
 * Learned preferences (dashboard `/insights/preferences`, plan section 18): what the research learned from feedback
 * (Proposed until you accept or reject them) and the ones you wrote. Accept, reject, disable, enable, edit, delete,
 * filter by status, and write your own.
 */
export function PreferencesScreen() {
  const p = useTheme();
  const { t } = useTranslation();
  const { client } = useAuth();
  const [status, setStatus] = useState('all');
  const resource = useResource(() => learningApi.list(client, { pageNumber: 1, pageSize: 200, status: status === 'all' ? undefined : status }), [client, status]);
  const topics = useResource(() => topicsApi.list(client, { pageNumber: 1, pageSize: 100 }), [client]);
  const topicList = listItems(topics.data);
  const topicName = (id) => (id ? (topicList.find((tp) => tp.id === id) || {}).name || null : null);
  const items = sortPreferences(listItems(resource.data));
  const proposed = items.filter((x) => x.status === 'Proposed').length;
  const replace = (updated) =>
    resource.setData((data) => {
      const next = listItems(data).map((x) => (x.id === updated.id ? updated : x));
      return { ...(data && !Array.isArray(data) ? data : {}), items: status === 'all' ? next : next.filter((x) => x.status === status) };
    });
  const removed = (id) => resource.setData((data) => ({ ...(data && !Array.isArray(data) ? data : {}), items: listItems(data).filter((x) => x.id !== id) }));
  const created = (pref) => resource.setData((data) => ({ ...(data && !Array.isArray(data) ? data : {}), items: [pref, ...listItems(data)] }));
  return (
    <Screen testID="preferences-screen" refreshControl={<RefreshControl refreshing={resource.refreshing} onRefresh={resource.reload} tintColor={p.primary} />}>
      <T variant="muted">{t('mobile.preferences.intro')}</T>
      <ChipGroup
        scroll
        options={['all', ...PREFERENCE_STATUSES].map((s) => ({ value: s, label: s === 'all' ? t('mobile.preferences.all') : t('mobile.preferences.statuses.' + s), testID: 'preferences-filter-' + s }))}
        value={status}
        onChange={setStatus}
        accessibilityLabel={t('mobile.common.filter')}
      />
      {proposed > 0 ? <Banner kind="info">{t('mobile.preferences.proposedCount', { count: proposed })}</Banner> : null}
      <ResourceView resource={resource} feature={t('mobile.insights.preferences.title')} isEmpty={() => items.length === 0} empty={<EmptyState icon="options-outline" title={t('mobile.insights.preferences.emptyTitle')} body={t('mobile.insights.preferences.emptyBody')} />}>
        <View style={{ gap: spacing.sm }}>
          {items.map((pref) => (
            <PreferenceCard key={pref.id + (pref.lastUpdateUtc || '')} pref={pref} topicName={topicName(pref.topicId)} onChanged={replace} onRemoved={removed} />
          ))}
        </View>
      </ResourceView>
      {resource.status !== 'notAvailable' ? (
        <>
          <SectionHeader title={t('mobile.preferences.writeYourOwn')} />
          <NewPreference topics={topicList} onCreated={created} />
        </>
      ) : null}
    </Screen>
  );
}
