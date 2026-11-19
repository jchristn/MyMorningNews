import React, { useState } from 'react';
import { View } from 'react-native';
import { useTranslation } from 'react-i18next';
import { schedulesApi } from '@/api/research';
import { intlLocale } from '@/i18n/locales';
import { formatDate, formatDateTime } from '@/lib/format';
import { EDITABLE_INTERVALS, EVERY_DAY_MASK, WEEKDAYS_MASK, hasDay, scheduleBody, scheduleForm, scheduleProblem, toggleDay } from '@/lib/schedule';
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

/**
 * The compact schedule editor (plan section 14): weekly days and times, every N days or hours, once, or manual, in an
 * IANA time zone, with "Preview next runs" from `POST /v1.0/api/schedules/preview`. Schedules of other types (every N
 * weeks, days of the month, cron) are shown and can be switched off or deleted; their details are edited on the web.
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
  const editable = EDITABLE_INTERVALS.includes(type);
  const problem = editable ? scheduleProblem(form) : null;
  const names = weekdayNames(i18n.language);
  const body = () => (editable ? scheduleBody(form, schedule ? null : owner) : { enabled: form.enabled });

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

  const typeOptions = (editable ? EDITABLE_INTERVALS : [...EDITABLE_INTERVALS, type]).map((v) => ({ value: v, label: t('mobile.schedule.types.' + v), testID: 'interval-' + v }));

  return (
    <Card testID="schedule-editor">
      <T variant="label">{t('mobile.schedule.repeat')}</T>
      <ChipGroup options={typeOptions} value={type} onChange={(v) => set({ intervalType: v })} accessibilityLabel={t('mobile.schedule.repeat')} />
      {!editable ? <Banner kind="info">{t('mobile.schedule.webOnly', { summary: describeSchedule(schedule, t, i18n.language) })}</Banner> : null}
      {type === 'DaysOfWeek' ? (
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
      {type === 'EveryNDays' || type === 'EveryNHours' ? (
        <Field
          label={type === 'EveryNDays' ? t('mobile.schedule.everyDays') : t('mobile.schedule.everyHours')}
          value={String(form.intervalValue)}
          onChangeText={(v) => set({ intervalValue: v.replace(/[^0-9]/g, '') })}
          keyboardType="number-pad"
          testID="schedule-interval"
        />
      ) : null}
      {type === 'EveryNHours' ? <Field label={t('mobile.schedule.minuteOffset')} value={String(form.minuteOffset)} onChangeText={(v) => set({ minuteOffset: v.replace(/[^0-9]/g, '') })} keyboardType="number-pad" /> : null}
      {type === 'DaysOfWeek' || type === 'EveryNDays' ? (
        <Field label={t('mobile.schedule.times')} hint={t('mobile.schedule.timesHint')} value={form.times} onChangeText={(v) => set({ times: v })} autoCapitalize="none" autoCorrect={false} testID="schedule-times" />
      ) : null}
      {type === 'Once' ? (
        <Field label={t('mobile.schedule.once')} hint={t('mobile.schedule.onceHint')} value={form.runOnce} onChangeText={(v) => set({ runOnce: v })} placeholder="2026-12-01 07:00" autoCapitalize="none" autoCorrect={false} testID="schedule-once" />
      ) : null}
      {type !== 'Manual' ? <Field label={t('mobile.schedule.timeZone')} value={form.timeZone} onChangeText={(v) => set({ timeZone: v.trim() })} autoCapitalize="none" autoCorrect={false} editable={editable} /> : null}
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
        {editable && type !== 'Manual' ? <Button title={t('mobile.schedule.preview')} kind="secondary" icon="eye-outline" busy={busy === 'preview'} disabled={Boolean(problem)} onPress={() => void runPreview()} testID="schedule-preview-button" /> : null}
        <Button title={t('mobile.common.save')} busy={busy === 'save'} disabled={Boolean(problem) || Boolean(busy)} onPress={() => void save()} testID="schedule-save" />
        {schedule && onRemoved ? <Button title={t('mobile.common.delete')} kind="danger" busy={busy === 'remove'} onPress={() => void remove()} testID="schedule-delete" /> : null}
        {onCancel ? <Button title={t('mobile.common.cancel')} kind="ghost" onPress={onCancel} /> : null}
      </ButtonRow>
    </Card>
  );
}
