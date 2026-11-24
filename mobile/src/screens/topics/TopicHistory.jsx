import React, { useEffect, useState } from 'react';
import { briefingsApi, topicsApi } from '@/api/research';
import { ChipGroup } from '@/components/Chips';
import { ActionError } from '@/components/states';
import { Button, Card, Field, T } from '@/components/ui';
import { useAuth } from '@/state/AuthContext';
import { useTranslation } from 'react-i18next';

const modes = ['Off', 'PreviousIssue', 'RecentIssues', 'SinceDate', 'SpecificIssue'];
const fromTopic = (topic) => ({ mode: topic.historyMode || 'Off', count: String(topic.historyCount ?? 5), date: topic.historySinceUtc?.slice(0, 10) || '', issue: topic.historyBriefingId || '' });

/** Comparison controls available during topic creation and in the workspace. */
export function TopicHistory({ topic, onSaved, disabled }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  const [form, setForm] = useState(() => fromTopic(topic));
  const [issues, setIssues] = useState([]);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(false);
  useEffect(() => {
    if (form.mode !== 'SpecificIssue') return;
    let live = true;
    briefingsApi.list(client, { topicId: topic.id, status: 'Ready', pageSize: 100 }).then((page) => {
      if (live) { setIssues(page.items || []); setError(null); }
    }).catch((e) => { if (live) setError(e); });
    return () => { live = false; };
  }, [client, topic.id, form.mode]);
  const set = (patch) => setForm((current) => ({ ...current, ...patch }));
  const save = async () => {
    setBusy(true); setError(null);
    try {
      const updated = await topicsApi.update(client, topic.id, {
        historyMode: form.mode, historyCount: Number(form.count),
        historySinceUtc: form.mode === 'SinceDate' && form.date ? new Date(form.date + 'T00:00:00Z').toISOString() : null,
        historyBriefingId: form.mode === 'SpecificIssue' ? form.issue : null,
      });
      onSaved(updated);
    } catch (e) { setError(e); } finally { setBusy(false); }
  };
  return <Card testID="topic-history">
    <T variant="label">{t('mobile.topicHistory.title')}</T>
    <T variant="muted">{t('mobile.topicHistory.help')}</T>
    <ChipGroup options={modes.map((mode) => ({ value: mode, label: t('mobile.topicHistory.' + mode) }))} value={form.mode} onChange={(mode) => set({ mode })} disabled={busy || disabled} accessibilityLabel={t('mobile.topicHistory.title')} />
    {['RecentIssues', 'SinceDate'].includes(form.mode) ? <Field label={t('mobile.topicHistory.count')} value={form.count} onChangeText={(count) => set({ count })} keyboardType="number-pad" maxLength={2} editable={!busy && !disabled} /> : null}
    {form.mode === 'SinceDate' ? <Field label={t('mobile.topicHistory.date')} value={form.date} onChangeText={(date) => set({ date })} placeholder="YYYY-MM-DD" maxLength={10} editable={!busy && !disabled} /> : null}
    {form.mode === 'SpecificIssue' ? <>
      <T variant="label">{t('mobile.topicHistory.issue')}</T>
      <T variant="muted">{t('mobile.topicHistory.choose')}</T>
      <ChipGroup options={issues.map((issue) => ({ value: issue.id, label: issue.title + ' (' + issue.createdUtc?.slice(0, 10) + ')' }))} value={form.issue} onChange={(issue) => set({ issue })} disabled={busy || disabled} accessibilityLabel={t('mobile.topicHistory.issue')} />
    </> : null}
    {form.mode !== 'Off' ? <T variant="muted">{t('mobile.topicHistory.firstRun')}</T> : null}
    <ActionError error={error} />
    <Button title={t('mobile.common.save')} onPress={() => void save()} busy={busy} disabled={disabled || JSON.stringify(form) === JSON.stringify(fromTopic(topic))} testID="save-topic-history" />
  </Card>;
}
