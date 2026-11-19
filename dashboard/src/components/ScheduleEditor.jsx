import { useEffect, useMemo, useState } from 'react';
import PropTypes from 'prop-types';
import { CalendarClock, Plus, X } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../context/AuthContext.jsx';
import useDebounce from '../hooks/useDebounce.js';
import useFormat from '../hooks/useFormat.js';
import { INTERVAL_TYPES, MISSED_RUN_POLICIES } from '../utils/enums.js';
import { EVERY_DAY_MASK, INTERVAL_BOUNDS, LAST_DAY_OF_MONTH, WEEKDAYS_MASK, WEEKENDS_MASK, fieldsFor, hasDay, scheduleBody, timeZoneOptions, toggleDay } from '../utils/schedule.js';
import ErrorState from './ErrorState.jsx';
import FormField from './FormField.jsx';
import LoadingState from './LoadingState.jsx';

const DAYS_OF_MONTH = [...Array.from({ length: 31 }, (_, i) => i + 1), LAST_DAY_OF_MONTH];

// Weekday names in the active locale, Sunday first to match the server mask.
export function weekdayNames(locale, style = 'short') {
  const format = new Intl.DateTimeFormat(locale, { weekday: style, timeZone: 'UTC' });
  return Array.from({ length: 7 }, (_, i) => format.format(new Date(Date.UTC(2023, 0, 1 + i))));
}

// One-line description of a saved schedule or a form ("Weekdays at 07:00").
export function describeSchedule(schedule, t, f) {
  if (!schedule) return '';
  const names = weekdayNames(f.locale);
  const mask = Number(schedule.daysOfWeekMask) || 0;
  const days =
    mask === WEEKDAYS_MASK ? t('schedule.presets.weekdays') : mask === WEEKENDS_MASK ? t('schedule.presets.weekends') : mask === EVERY_DAY_MASK ? t('schedule.presets.everyDay') : f.list(names.filter((_, i) => hasDay(mask, i)));
  const times = f.list([...(schedule.timesOfDay || [])].filter(Boolean).sort());
  const monthDays = f.list((schedule.daysOfMonth || []).map((d) => (Number(d) === LAST_DAY_OF_MONTH ? t('schedule.lastDay') : String(d))));
  const n = Number(schedule.intervalValue) || 1;
  switch (schedule.intervalType) {
    case 'EveryNMinutes':
      return t('schedule.summary.EveryNMinutes', { count: n });
    case 'EveryNHours':
      return t('schedule.summary.EveryNHours', { count: n, minute: String(schedule.minuteOffset || 0).padStart(2, '0') });
    case 'EveryNDays':
      return t('schedule.summary.EveryNDays', { count: n, times });
    case 'EveryNWeeks':
      return t('schedule.summary.EveryNWeeks', { count: n, days, times });
    case 'DaysOfWeek':
      return t('schedule.summary.DaysOfWeek', { days, times });
    case 'DaysOfMonth':
      return t('schedule.summary.DaysOfMonth', { days: monthDays, times });
    case 'Once':
      return t('schedule.summary.Once', { when: schedule.runOnceUtc ? f.dateTime(schedule.runOnceUtc, { dateStyle: 'medium', timeStyle: 'short', timeZone: schedule.timeZone || 'UTC' }) : '-' });
    case 'Cron':
      return t('schedule.summary.Cron', { expression: schedule.cronExpression || '' });
    default:
      return t('schedule.summary.Manual');
  }
}

// Schedule form with the fields each interval type needs and a live "Next 5 runs" preview from the server.
// value is a form from utils/schedule.js; onChange receives the next form.
export default function ScheduleEditor({ value, onChange, idPrefix = 'schedule', disabled = false, onPreview }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const fields = fieldsFor(value.intervalType);
  const body = useMemo(() => scheduleBody(value), [value]);
  const bodyKey = JSON.stringify(body);
  const debouncedKey = useDebounce(bodyKey, 350);
  const [preview, setPreview] = useState(null);
  const [previewError, setPreviewError] = useState(null);
  const [previewing, setPreviewing] = useState(false);
  const days = useMemo(() => weekdayNames(f.locale), [f.locale]);
  const zones = useMemo(() => timeZoneOptions(value.timeZone), [value.timeZone]);
  const set = (patch) => onChange({ ...value, ...patch });
  const id = (name) => idPrefix + '-' + name;

  useEffect(() => {
    const controller = new AbortController();
    setPreviewing(true);
    setPreviewError(null);
    apiClient
      .previewSchedule({ ...JSON.parse(debouncedKey), previewCount: 5 }, controller.signal)
      .then((result) => {
        setPreview(result);
        if (onPreview) onPreview(result);
      })
      .catch((err) => {
        if (err && err.name === 'AbortError') return;
        setPreview(null);
        setPreviewError(err);
      })
      .finally(() => {
        if (!controller.signal.aborted) setPreviewing(false);
      });
    return () => controller.abort();
    // onPreview is a notification callback; re-running the preview for a new function identity is not wanted.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [apiClient, debouncedKey]);

  const bounds = INTERVAL_BOUNDS[value.intervalType] || [1, 1000];
  const unitKey = { EveryNMinutes: 'minutes', EveryNHours: 'hours', EveryNDays: 'days', EveryNWeeks: 'weeks' }[value.intervalType];
  const times = value.timesOfDay && value.timesOfDay.length > 0 ? value.timesOfDay : [''];
  const runs = preview && preview.valid ? preview.nextRunsUtc || [] : [];

  return (
    <div className="schedule-editor" data-testid="schedule-editor">
      <div className="form-grid">
        <FormField id={id('type')} label={t('schedule.fields.intervalType')}>
          <select id={id('type')} value={value.intervalType} disabled={disabled} onChange={(e) => set({ intervalType: e.target.value })}>
            {INTERVAL_TYPES.map((type) => (
              <option key={type} value={type}>
                {t('enums.intervalType.' + type)}
              </option>
            ))}
          </select>
        </FormField>
        {fields.has('intervalValue') ? (
          <FormField id={id('value')} label={t('schedule.fields.every')} help={t('schedule.fields.everyHelp', { min: bounds[0], max: bounds[1] })}>
            <div className="input-with-button">
              <input id={id('value')} type="number" min={bounds[0]} max={bounds[1]} value={value.intervalValue} disabled={disabled} onChange={(e) => set({ intervalValue: e.target.value === '' ? '' : Number(e.target.value) })} />
              <span className="muted small">{t('schedule.units.' + unitKey)}</span>
            </div>
          </FormField>
        ) : null}
        {fields.has('minuteOffset') ? (
          <FormField id={id('minute')} label={t('schedule.fields.minuteOffset')} help={t('schedule.fields.minuteOffsetHelp')}>
            <input id={id('minute')} type="number" min={0} max={59} value={value.minuteOffset} disabled={disabled} onChange={(e) => set({ minuteOffset: e.target.value === '' ? 0 : Number(e.target.value) })} />
          </FormField>
        ) : null}
        {fields.has('timeZone') ? (
          <FormField id={id('tz')} label={t('schedule.fields.timeZone')} help={t('schedule.fields.timeZoneHelp')}>
            <select id={id('tz')} value={value.timeZone} disabled={disabled} onChange={(e) => set({ timeZone: e.target.value })}>
              {zones.map((z) => (
                <option key={z} value={z}>
                  {z}
                </option>
              ))}
            </select>
          </FormField>
        ) : null}
        {fields.has('runOnce') ? (
          <FormField id={id('once')} label={t('schedule.fields.runOnce')} help={t('schedule.fields.localTimeHelp', { zone: value.timeZone })}>
            <input id={id('once')} type="datetime-local" value={value.runOnceLocal} disabled={disabled} onChange={(e) => set({ runOnceLocal: e.target.value })} />
          </FormField>
        ) : null}
        {fields.has('cronExpression') ? (
          <FormField id={id('cron')} label={t('schedule.fields.cron')} help={t('schedule.fields.cronHelp')} wide>
            <input id={id('cron')} className="mono" value={value.cronExpression} disabled={disabled} placeholder="0 */6 * * 1-5" onChange={(e) => set({ cronExpression: e.target.value })} autoComplete="off" />
          </FormField>
        ) : null}
      </div>

      {fields.has('daysOfWeek') ? (
        <fieldset className="schedule-fieldset">
          <legend>{t('schedule.fields.daysOfWeek')}</legend>
          <div className="day-chips" role="group" aria-label={t('schedule.fields.daysOfWeek')}>
            {days.map((name, i) => (
              <button
                type="button"
                key={name + i}
                className={'chip-toggle' + (hasDay(value.daysOfWeekMask, i) ? ' active' : '')}
                aria-pressed={hasDay(value.daysOfWeekMask, i)}
                disabled={disabled}
                onClick={() => set({ daysOfWeekMask: toggleDay(value.daysOfWeekMask, i) })}
              >
                {name}
              </button>
            ))}
          </div>
          <div className="row-actions">
            <button type="button" className="link-button small" disabled={disabled} onClick={() => set({ daysOfWeekMask: WEEKDAYS_MASK })}>
              {t('schedule.presets.weekdays')}
            </button>
            <button type="button" className="link-button small" disabled={disabled} onClick={() => set({ daysOfWeekMask: WEEKENDS_MASK })}>
              {t('schedule.presets.weekends')}
            </button>
            <button type="button" className="link-button small" disabled={disabled} onClick={() => set({ daysOfWeekMask: EVERY_DAY_MASK })}>
              {t('schedule.presets.everyDay')}
            </button>
          </div>
        </fieldset>
      ) : null}

      {fields.has('daysOfMonth') ? (
        <fieldset className="schedule-fieldset">
          <legend>{t('schedule.fields.daysOfMonth')}</legend>
          <div className="day-chips month" role="group" aria-label={t('schedule.fields.daysOfMonth')}>
            {DAYS_OF_MONTH.map((d) => {
              const on = (value.daysOfMonth || []).map(Number).includes(d);
              return (
                <button
                  type="button"
                  key={d}
                  className={'chip-toggle' + (on ? ' active' : '')}
                  aria-pressed={on}
                  disabled={disabled}
                  onClick={() => set({ daysOfMonth: on ? value.daysOfMonth.filter((x) => Number(x) !== d) : [...(value.daysOfMonth || []), d] })}
                >
                  {d === LAST_DAY_OF_MONTH ? t('schedule.lastDay') : f.number(d)}
                </button>
              );
            })}
          </div>
        </fieldset>
      ) : null}

      {fields.has('timesOfDay') ? (
        <fieldset className="schedule-fieldset">
          <legend>{t('schedule.fields.timesOfDay')}</legend>
          <div className="time-list">
            {times.map((time, i) => (
              <span className="time-row" key={i}>
                <input
                  type="time"
                  value={time}
                  disabled={disabled}
                  aria-label={t('schedule.fields.timeN', { n: i + 1 })}
                  onChange={(e) => set({ timesOfDay: times.map((x, j) => (j === i ? e.target.value : x)) })}
                />
                {times.length > 1 ? (
                  <button type="button" className="icon-button" disabled={disabled} aria-label={t('schedule.removeTime')} title={t('schedule.removeTime')} onClick={() => set({ timesOfDay: times.filter((_, j) => j !== i) })}>
                    <X size={14} aria-hidden="true" />
                  </button>
                ) : null}
              </span>
            ))}
            <button type="button" className="button secondary small" disabled={disabled || times.length >= 24} onClick={() => set({ timesOfDay: [...times, '12:00'] })}>
              <Plus size={14} aria-hidden="true" /> {t('schedule.addTime')}
            </button>
          </div>
        </fieldset>
      ) : null}

      <div className="form-grid">
        {fields.has('window') ? (
          <>
            <FormField id={id('start')} label={t('schedule.fields.start')} help={t('schedule.fields.optionalLocal', { zone: value.timeZone })}>
              <input id={id('start')} type="datetime-local" value={value.startLocal} disabled={disabled} onChange={(e) => set({ startLocal: e.target.value })} />
            </FormField>
            <FormField id={id('end')} label={t('schedule.fields.end')} help={t('schedule.fields.optionalLocal', { zone: value.timeZone })}>
              <input id={id('end')} type="datetime-local" value={value.endLocal} disabled={disabled} onChange={(e) => set({ endLocal: e.target.value })} />
            </FormField>
          </>
        ) : null}
        {fields.has('missedRunPolicy') ? (
          <FormField id={id('missed')} label={t('schedule.fields.missedRunPolicy')} help={t('schedule.missedHelp.' + value.missedRunPolicy)}>
            <select id={id('missed')} value={value.missedRunPolicy} disabled={disabled} onChange={(e) => set({ missedRunPolicy: e.target.value })}>
              {MISSED_RUN_POLICIES.map((p) => (
                <option key={p} value={p}>
                  {t('enums.missedRunPolicy.' + p)}
                </option>
              ))}
            </select>
          </FormField>
        ) : null}
        <FormField id={id('enabled')} label={t('schedule.fields.enabled')}>
          <label className="checkbox-row">
            <input id={id('enabled')} type="checkbox" checked={value.enabled !== false} disabled={disabled} onChange={(e) => set({ enabled: e.target.checked })} />
            <span>{value.enabled !== false ? t('schedule.enabledOn') : t('schedule.enabledOff')}</span>
          </label>
        </FormField>
      </div>

      <section className="schedule-preview" aria-live="polite" data-testid="schedule-preview">
        <div className="detail-section-header">
          <strong>
            <CalendarClock size={14} aria-hidden="true" /> {t('schedule.preview.title')}
          </strong>
          {previewing ? <LoadingState compact label={t('schedule.preview.checking')} /> : null}
        </div>
        <ErrorState error={previewError} inline onDismiss={() => setPreviewError(null)} />
        {preview && !preview.valid ? (
          <ul className="validation-list" data-testid="schedule-errors">
            {(preview.errors || []).map((e) => (
              <li key={e} className="text-danger">
                {e}
              </li>
            ))}
          </ul>
        ) : null}
        {preview && preview.valid && value.intervalType === 'Manual' ? <p className="muted small">{t('schedule.preview.manual')}</p> : null}
        {preview && preview.valid && value.intervalType !== 'Manual' && runs.length === 0 ? <p className="muted small">{t('schedule.preview.none')}</p> : null}
        {runs.length > 0 ? (
          <ol className="next-runs">
            {runs.map((utc) => (
              <li key={utc}>
                <span>{f.dateTime(utc, { weekday: 'short', year: 'numeric', month: 'short', day: 'numeric', hour: '2-digit', minute: '2-digit', timeZone: value.timeZone || 'UTC', timeZoneName: 'short' })}</span>
                <span className="muted small">{f.relative(utc)}</span>
              </li>
            ))}
          </ol>
        ) : null}
      </section>
    </div>
  );
}

ScheduleEditor.propTypes = {
  value: PropTypes.shape({
    intervalType: PropTypes.string.isRequired,
    intervalValue: PropTypes.oneOfType([PropTypes.number, PropTypes.string]),
    minuteOffset: PropTypes.number,
    daysOfWeekMask: PropTypes.number,
    daysOfMonth: PropTypes.array,
    timesOfDay: PropTypes.array,
    cronExpression: PropTypes.string,
    timeZone: PropTypes.string,
    startLocal: PropTypes.string,
    endLocal: PropTypes.string,
    runOnceLocal: PropTypes.string,
    missedRunPolicy: PropTypes.string,
    enabled: PropTypes.bool
  }).isRequired,
  onChange: PropTypes.func.isRequired,
  idPrefix: PropTypes.string,
  disabled: PropTypes.bool,
  onPreview: PropTypes.func
};
