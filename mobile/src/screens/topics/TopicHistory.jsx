import React, { useEffect, useState } from 'react';
import { View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { ApiError, fieldErrors } from '@/api/errors';
import { briefingsApi, editionsApi, topicsApi } from '@/api/research';
import { ChipGroup } from '@/components/Chips';
import { ActionError } from '@/components/states';
import { Banner, Button, Card, Field, T } from '@/components/ui';
import { HISTORY_MODES, historyBody, historyForm, sameHistory } from '@/lib/comparison';
import { formatDate, formatNumber } from '@/lib/format';
import { useAuth } from '@/state/AuthContext';
import { spacing } from '@/theme/palette';

/** An inline message for a preview the server refused: 400 lists what to fix, 409 says the pinned issue is gone. */
function PreviewRefusal({ error }) {
  const { t } = useTranslation();
  if (!(error instanceof ApiError) || (error.status !== 400 && error.status !== 409)) return <ActionError error={error} testID="history-preview-error" />;
  const fields = Object.values(fieldErrors(error)).flat().filter((v) => typeof v === 'string' && v);
  return (
    <Banner kind="warning" title={error.status === 409 ? t('mobile.historyPreview.conflictTitle') : t('mobile.historyPreview.invalidTitle')} testID="history-preview-refused">
      <View style={{ gap: spacing.xs }}>
        {error.message ? <T variant="small">{error.message}</T> : null}
        {fields.map((f) => (
          <T key={f} variant="small">
            {f}
          </T>
        ))}
      </View>
    </Banner>
  );
}

/** The answer of a baseline preview: the issues, the size against the budget, truncation, and any note. */
function PreviewResult({ result }) {
  const { t, i18n } = useTranslation();
  const baselines = Array.isArray(result.baselines) ? result.baselines : [];
  const n = (v) => formatNumber(Number(v) || 0, i18n.language);
  return (
    <View style={{ gap: spacing.xs }} testID="history-preview-result">
      <T variant="label" accessibilityRole="header">
        {t('mobile.historyPreview.issues', { count: baselines.length })}
      </T>
      {baselines.map((b) => (
        <T key={b.id} testID={'history-preview-issue-' + b.id}>
          {(b.available !== false && b.title ? b.title : t('mobile.comparison.baselineGone')) + (b.createdUtc ? ' (' + formatDate(b.createdUtc, i18n.language) + ')' : '')}
        </T>
      ))}
      <T variant="small" testID="history-preview-size">
        {t('mobile.historyPreview.size', { used: n(result.contextBytes), budget: n(result.budgetBytes) })}
      </T>
      {result.truncated ? (
        <Banner kind="warning" testID="history-preview-truncated">
          {t('mobile.historyPreview.truncated')}
        </Banner>
      ) : null}
      {result.note ? (
        <T variant="small" testID="history-preview-note">
          {result.note}
        </T>
      ) : null}
      {result.cutoffUtc ? <T variant="small">{t('mobile.historyPreview.cutoff', { date: formatDate(result.cutoffUtc, i18n.language) })}</T> : null}
    </View>
  );
}

/**
 * Comparison settings for a topic or an edition (plan section 6.1): the mode, the issue count, the start date, the
 * pinned issue, and the context budget, with a preview of the baseline the next run would use for the values on
 * screen (nothing is saved until Save). `loadIssues()` lists the ready issues to pin, `save(body)` stores the settings
 * and returns the updated owner, and `preview(body)` asks the server for the baseline.
 */
export function HistorySettings({ owner, loadIssues, save, preview, onSaved, disabled, help, testID }) {
  const { t } = useTranslation();
  const [form, setForm] = useState(() => historyForm(owner));
  const [issues, setIssues] = useState([]);
  const [error, setError] = useState(null);
  const [busy, setBusy] = useState(null);
  const [result, setResult] = useState(null);
  const [refusal, setRefusal] = useState(null);
  useEffect(() => {
    if (form.mode !== 'SpecificIssue') return undefined;
    let live = true;
    loadIssues()
      .then((page) => {
        if (live) {
          setIssues((page && page.objects) || []);
          setError(null);
        }
      })
      .catch((e) => {
        if (live) setError(e);
      });
    return () => {
      live = false;
    };
    // loadIssues is recreated by the parent on every render; the owner's ID decides what it lists.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [owner.id, form.mode]);
  const set = (patch) => {
    setResult(null);
    setRefusal(null);
    setForm((current) => ({ ...current, ...patch }));
  };
  const store = async () => {
    setBusy('save');
    setError(null);
    try {
      onSaved(await save(historyBody(form)));
    } catch (e) {
      setError(e);
    } finally {
      setBusy(null);
    }
  };
  const look = async () => {
    setBusy('preview');
    setResult(null);
    setRefusal(null);
    try {
      setResult(await preview(historyBody(form)));
    } catch (e) {
      setRefusal(e);
    } finally {
      setBusy(null);
    }
  };
  const locked = Boolean(busy) || disabled;
  return (
    <Card testID={testID}>
      <T variant="label">{t('mobile.topicHistory.title')}</T>
      <T variant="muted">{help || t('mobile.topicHistory.help')}</T>
      <ChipGroup options={HISTORY_MODES.map((mode) => ({ value: mode, label: t('mobile.topicHistory.' + mode), testID: testID + '-mode-' + mode }))} value={form.mode} onChange={(mode) => set({ mode })} disabled={locked} accessibilityLabel={t('mobile.topicHistory.title')} />
      {['RecentIssues', 'SinceDate'].includes(form.mode) ? <Field label={t('mobile.topicHistory.count')} value={form.count} onChangeText={(count) => set({ count })} keyboardType="number-pad" maxLength={2} editable={!locked} testID={testID + '-count'} /> : null}
      {form.mode === 'SinceDate' ? <Field label={t('mobile.topicHistory.date')} value={form.date} onChangeText={(date) => set({ date })} placeholder="YYYY-MM-DD" maxLength={10} editable={!locked} testID={testID + '-date'} /> : null}
      {form.mode === 'SpecificIssue' ? (
        <>
          <T variant="label">{t('mobile.topicHistory.issue')}</T>
          <T variant="muted">{t('mobile.topicHistory.choose')}</T>
          <ChipGroup options={issues.map((issue) => ({ value: issue.id, label: issue.title + ' (' + issue.createdUtc?.slice(0, 10) + ')', testID: testID + '-issue-' + issue.id }))} value={form.issue} onChange={(issue) => set({ issue })} disabled={locked} accessibilityLabel={t('mobile.topicHistory.issue')} />
        </>
      ) : null}
      {form.mode !== 'Off' ? <Field label={t('mobile.historyPreview.budget')} hint={t('mobile.historyPreview.budgetHint')} value={form.budget} onChangeText={(budget) => set({ budget: budget.replace(/[^0-9]/g, '') })} keyboardType="number-pad" maxLength={5} editable={!locked} testID={testID + '-budget'} /> : null}
      {form.mode !== 'Off' ? <T variant="muted">{t('mobile.topicHistory.firstRun')}</T> : null}
      <ActionError error={error} />
      <Button title={t('mobile.historyPreview.preview')} icon="eye-outline" kind="secondary" busy={busy === 'preview'} disabled={locked} onPress={() => void look()} accessibilityHint={t('mobile.historyPreview.previewHint')} testID={testID + '-preview'} />
      {refusal ? <PreviewRefusal error={refusal} /> : null}
      {result ? <PreviewResult result={result} /> : null}
      <Button title={t('mobile.common.save')} onPress={() => void store()} busy={busy === 'save'} disabled={locked || sameHistory(form, historyForm(owner))} testID={'save-' + testID} />
    </Card>
  );
}

/** Comparison controls available during topic creation and in the workspace. */
export function TopicHistory({ topic, onSaved, disabled }) {
  const { client } = useAuth();
  return (
    <HistorySettings
      owner={topic}
      testID="topic-history"
      disabled={disabled}
      onSaved={onSaved}
      loadIssues={() => briefingsApi.list(client, { topicId: topic.id, status: 'Ready', pageSize: 100 })}
      save={(body) => topicsApi.update(client, topic.id, body)}
      preview={(body) => topicsApi.previewHistory(client, topic.id, body)}
    />
  );
}

/** An edition's own comparison: Off keeps the topics' research comparisons; any other mode compares with its earlier issues. */
export function EditionHistory({ edition, onSaved, disabled }) {
  const { t } = useTranslation();
  const { client } = useAuth();
  return (
    <HistorySettings
      owner={edition}
      testID="edition-history"
      help={t('mobile.historyPreview.editionHelp')}
      disabled={disabled}
      onSaved={onSaved}
      loadIssues={() => briefingsApi.list(client, { editionId: edition.id, kind: 'Edition', status: 'Ready', pageSize: 100 })}
      save={(body) => editionsApi.update(client, edition.id, body)}
      preview={(body) => editionsApi.previewHistory(client, edition.id, body)}
    />
  );
}
