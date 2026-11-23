import React, { useState } from 'react';
import { View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { schedulesApi } from '@/api/research';
import { intlLocale } from '@/i18n/locales';
import { formatDate, formatDateTime } from '@/lib/format';
import { EDITABLE_INTERVALS, EVERY_DAY_MASK, MISSED_RUN_POLICIES, WEEKDAYS_MASK, hasDay, scheduleBody, scheduleForm, scheduleProblem, toggleDay, usesDays, usesTimes, usesWindow } from '@/lib/schedule';
import { useAuth } from '@/state/AuthContext';
import { spacing } from '@/theme/palette';
import { ButtonRow, ChipGroup } from './Chips';
import { Banner, Button, Card, Field, SwitchRow, T } from './ui';

const WEEKENDS_MASK = 65;

/** Short weekday names in the UI language, Sunday first (the server's bit order). */
export function weekdayNames(locale, style = 'short') {
  return [0, 1, 2, 3, 4, 5, 6].map((i) => {
    try {
      return new Intl.DateTimeFormat(intlLocale(locale), { weekday: style, timeZone: 'UTC' }).format(new Date(Date.UTC(2026, 10, 15 + i)));
    } catch {
      return ['Sun', 'Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat'][i];
    }
  });
}

function daysText(mask, t, locale) {
  if (Number(mask) === EVERY_DAY_MASK) return t('mobile.schedule.everyDay');
  if (Number(mask) === WEEKDAYS_MASK) return t('mobile.schedule.weekdays');
  if (Number(mask) === WEEKENDS_MASK) return t('mobile.schedule.weekends');
  const names = weekdayNames(locale);
  return names.filter((_, i) => hasDay(mask, i)).join(', ');
}

/** One line describing a schedule, for example "Weekdays at 07:00". */
export function describeSchedule(schedule, t, locale) {
  if (!schedule) return '';
  const times = (schedule.timesOfDay || []).join(', ');
  const count = schedule.intervalValue || 1;
  switch (schedule.intervalType) {
    case 'DaysOfWeek':
      return t('mobile.schedule.describe.daysOfWeek', { days: daysText(schedule.daysOfWeekMask, t, locale), times });
    case 'EveryNDays':
      return t('mobile.schedule.describe.everyNDays', { count, times });
    case 'EveryNHours':
      return t('mobile.schedule.describe.everyNHours', { count });
    case 'EveryNMinutes':
      return t('mobile.schedule.describe.everyNMinutes', { count });
    case 'EveryNWeeks':
      return t('mobile.schedule.describe.everyNWeeks', { count, days: daysText(schedule.daysOfWeekMask, t, locale), times });
    case 'DaysOfMonth':
      return t('mobile.schedule.describe.daysOfMonth', { days: (schedule.daysOfMonth || []).map((d) => (d === -1 ? t('mobile.schedule.lastDay') : String(d))).join(', '), times });
    case 'Once':
      return t('mobile.schedule.describe.once', { date: formatDateTime(schedule.runOnceUtc, locale) });
    case 'Cron':
      return t('mobile.schedule.describe.cron', { expression: schedule.cronExpression || '' });
    default:
      return t('mobile.schedule.describe.manual');
  }
}

/** A schedule's summary card: what it does, the zone, and when it runs next. */
export function ScheduleSummary({ schedule, onPress, testID }) {
  const { t, i18n } = useTranslation();
  return (
    <Card onPress={onPress} accessibilityLabel={describeSchedule(schedule, t, i18n.language)} testID={testID}>
      <T style={{ fontWeight: '600' }}>{describeSchedule(schedule, t, i18n.language)}</T>
      <T variant="small">
        {[schedule.timeZone, schedule.enabled === false ? t('mobile.schedule.off') : schedule.nextRunUtc ? t('mobile.schedule.next', { date: formatDateTime(schedule.nextRunUtc, i18n.language) }) : ''].filter(Boolean).join(' · ')}
      </T>
    </Card>
  );
}

/** The interval size field's label per type. */
const INTERVAL_LABELS = { EveryNDays: 'everyDays', EveryNHours: 'everyHours', EveryNWeeks: 'everyWeeks', EveryNMinutes: 'everyMinutes' };

/**
 * The schedule editor (plan section 14), with every interval type the server has: weekly days and times, every N
 * days, weeks, hours, or minutes, days of the month, once, cron, or manual, in an IANA time zone, with an optional
 * start and end, what to do with a missed run, and "Preview next runs" from `POST /v1.0/api/schedules/preview`.
 *
 * `owner` is `{ ownerType, ownerId }` for a new schedule. `onSaved(schedule)` and `onRemoved(id)` report changes.
 */
export function ScheduleEditor({ schedule, owner, onSaved, onRemoved, onCancel }) {
  const { t, i18n } = useTranslation();
  const { client } = useAuth();
  const [form, setForm] = useState(() => scheduleForm(schedule));
  const [busy, setBusy] = useState(null);
  const [error, setError] = useState(null);
  const [preview, setPreview] = useState(null);
  const set = (patch) => {
    setForm((f) => ({ ...f, ...patch }));
    setPreview(null);
  };
  const type = form.intervalType;
  const problem = scheduleProblem(form);
  const names = weekdayNames(i18n.language);
  const body = () => scheduleBody(form, schedule ? null : owner);

  const act = async (kind, work) => {
    setBusy(kind);
    setError(null);
    try {
      await work();
    } catch (e) {
      setError(e.message);
    } finally {
      setBusy(null);
    }
  };
  const save = () =>
    act('save', async () => {
      const saved = schedule ? await schedulesApi.update(client, schedule.id, body()) : await schedulesApi.create(client, body());
      if (onSaved) onSaved(saved);
    });
  const remove = () =>
    act('remove', async () => {
      await schedulesApi.remove(client, schedule.id);
      if (onRemoved) onRemoved(schedule.id);
    });
  const runPreview = () =>
    act('preview', async () => {
      setPreview(await schedulesApi.preview(client, { ...scheduleBody(form, owner), previewCount: 5 }));
    });

  const typeOptions = EDITABLE_INTERVALS.map((v) => ({ value: v, label: t('mobile.schedule.types.' + v), testID: 'interval-' + v }));

  return (
    <Card testID="schedule-editor">
      <T variant="label">{t('mobile.schedule.repeat')}</T>
      <ChipGroup options={typeOptions} value={type} onChange={(v) => set({ intervalType: v })} accessibilityLabel={t('mobile.schedule.repeat')} />
      {INTERVAL_LABELS[type] ? (
        <Field label={t('mobile.schedule.' + INTERVAL_LABELS[type])} value={String(form.intervalValue)} onChangeText={(v) => set({ intervalValue: v.replace(/[^0-9]/g, '') })} keyboardType="number-pad" testID="schedule-interval" />
      ) : null}
      {usesDays(type) ? (
        <>
          <T variant="label">{t('mobile.schedule.days')}</T>
          <ChipGroup
            multiple
            options={names.map((name, i) => ({ value: i, label: name, testID: 'day-' + i }))}
            value={names.map((_, i) => i).filter((i) => hasDay(form.daysOfWeekMask, i))}
            onChange={(next) => {
              const before = names.map((_, i) => i).filter((i) => hasDay(form.daysOfWeekMask, i));
              const changed = [...next.filter((d) => !before.includes(d)), ...before.filter((d) => !next.includes(d))][0];
              if (changed !== undefined) set({ daysOfWeekMask: toggleDay(form.daysOfWeekMask, changed) });
            }}
            accessibilityLabel={t('mobile.schedule.days')}
          />
        </>
      ) : null}
      {type === 'DaysOfMonth' ? (
        <Field label={t('mobile.schedule.monthDays')} hint={t('mobile.schedule.monthDaysHint')} value={form.monthDays} onChangeText={(v) => set({ monthDays: v })} autoCapitalize="none" autoCorrect={false} testID="schedule-month-days" />
      ) : null}
      {type === 'EveryNHours' ? <Field label={t('mobile.schedule.minuteOffset')} value={String(form.minuteOffset)} onChangeText={(v) => set({ minuteOffset: v.replace(/[^0-9]/g, '') })} keyboardType="number-pad" /> : null}
      {usesTimes(type) ? (
        <Field label={t('mobile.schedule.times')} hint={t('mobile.schedule.timesHint')} value={form.times} onChangeText={(v) => set({ times: v })} autoCapitalize="none" autoCorrect={false} testID="schedule-times" />
      ) : null}
      {type === 'Once' ? (
        <Field label={t('mobile.schedule.once')} hint={t('mobile.schedule.onceHint')} value={form.runOnce} onChangeText={(v) => set({ runOnce: v })} placeholder="2026-12-01 07:00" autoCapitalize="none" autoCorrect={false} testID="schedule-once" />
      ) : null}
      {type === 'Cron' ? (
        <Field label={t('mobile.schedule.cron')} hint={t('mobile.schedule.cronHint')} value={form.cronExpression} onChangeText={(v) => set({ cronExpression: v })} placeholder="0 7 * * 1-5" autoCapitalize="none" autoCorrect={false} testID="schedule-cron" />
      ) : null}
      {type !== 'Manual' ? <Field label={t('mobile.schedule.timeZone')} value={form.timeZone} onChangeText={(v) => set({ timeZone: v.trim() })} autoCapitalize="none" autoCorrect={false} testID="schedule-time-zone" /> : null}
      {usesWindow(type) ? (
        <>
          <Field label={t('mobile.schedule.start')} hint={t('mobile.schedule.windowHint')} value={form.start} onChangeText={(v) => set({ start: v })} placeholder="2026-12-01 07:00" autoCapitalize="none" autoCorrect={false} testID="schedule-start" />
          <Field label={t('mobile.schedule.end')} hint={t('mobile.schedule.windowHint')} value={form.end} onChangeText={(v) => set({ end: v })} placeholder="2027-01-31 23:59" autoCapitalize="none" autoCorrect={false} testID="schedule-end" />
        </>
      ) : null}
      {type !== 'Manual' ? (
        <>
          <T variant="label">{t('mobile.schedule.missed')}</T>
          <ChipGroup
            options={MISSED_RUN_POLICIES.map((v) => ({ value: v, label: t('mobile.schedule.missedPolicies.' + v), testID: 'missed-' + v }))}
            value={form.missedRunPolicy}
            onChange={(v) => set({ missedRunPolicy: v })}
            accessibilityLabel={t('mobile.schedule.missed')}
          />
        </>
      ) : null}
      <SwitchRow title={t('mobile.schedule.enabled')} value={form.enabled} onValueChange={(v) => set({ enabled: v })} testID="schedule-enabled" />
      {problem ? <T variant="small">{t('mobile.schedule.problems.' + problem)}</T> : null}
      {error ? <Banner kind="danger">{error}</Banner> : null}
      {preview ? (
        <View testID="schedule-preview" style={{ gap: spacing.xs }}>
          {preview.valid === false ? (
            <Banner kind="warning">{(preview.errors || []).join(' ')}</Banner>
          ) : (preview.nextRunsUtc || []).length === 0 ? (
            <T variant="small">{t('mobile.schedule.noUpcoming')}</T>
          ) : (
            <>
              <T variant="label">{t('mobile.schedule.nextRuns')}</T>
              {(preview.nextRunsUtc || []).map((d) => (
                <T key={d} variant="small">
                  {formatDate(d, i18n.language, { dateStyle: 'full', timeStyle: 'short' })}
                </T>
              ))}
            </>
          )}
        </View>
      ) : null}
      <ButtonRow>
        {type !== 'Manual' ? <Button title={t('mobile.schedule.preview')} kind="secondary" icon="eye-outline" busy={busy === 'preview'} disabled={Boolean(problem)} onPress={() => void runPreview()} testID="schedule-preview-button" /> : null}
        <Button title={t('mobile.common.save')} busy={busy === 'save'} disabled={Boolean(problem) || Boolean(busy)} onPress={() => void save()} testID="schedule-save" />
        {schedule && onRemoved ? <Button title={t('mobile.common.delete')} kind="danger" busy={busy === 'remove'} onPress={() => void remove()} testID="schedule-delete" /> : null}
        {onCancel ? <Button title={t('mobile.common.cancel')} kind="ghost" onPress={onCancel} /> : null}
      </ButtonRow>
    </Card>
  );
}
