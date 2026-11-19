import { useState } from 'react';
import PropTypes from 'prop-types';
import { describe, expect, it, vi } from 'vitest';
import { render, screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { AppProvider } from '../context/AppContext.jsx';
import { AuthProvider, STORAGE_KEYS } from '../context/AuthContext.jsx';
import { fakeClient } from '../test/fakeClient.js';
import {
  WEEKDAYS_MASK,
  daysFromMask,
  defaultScheduleForm,
  fieldsFor,
  formFromSchedule,
  hasDay,
  maskFromDays,
  scheduleBody,
  toggleDay,
  utcToZonedLocal,
  zonedLocalToUtc
} from '../utils/schedule.js';
import ScheduleEditor, { describeSchedule } from './ScheduleEditor.jsx';
import i18next from '../i18n/index.js';

describe('days-of-week mask', () => {
  it('uses Sunday 1 through Saturday 64, with weekdays 62', () => {
    expect(maskFromDays([1, 2, 3, 4, 5])).toBe(WEEKDAYS_MASK);
    expect(maskFromDays([0])).toBe(1);
    expect(maskFromDays([6])).toBe(64);
    expect(daysFromMask(62)).toEqual([1, 2, 3, 4, 5]);
    expect(daysFromMask(65)).toEqual([0, 6]);
    expect(hasDay(62, 0)).toBe(false);
    expect(hasDay(62, 1)).toBe(true);
  });

  it('toggles a single day on and off', () => {
    expect(toggleDay(62, 6)).toBe(126);
    expect(toggleDay(126, 6)).toBe(62);
    expect(toggleDay(0, 3)).toBe(8);
  });
});

describe('field visibility per interval type', () => {
  it('shows only what each type uses', () => {
    expect([...fieldsFor('EveryNMinutes')].sort()).toEqual(['intervalValue', 'missedRunPolicy', 'timeZone', 'window']);
    expect(fieldsFor('EveryNHours').has('minuteOffset')).toBe(true);
    expect(fieldsFor('EveryNDays').has('timesOfDay')).toBe(true);
    expect(fieldsFor('EveryNDays').has('daysOfWeek')).toBe(false);
    expect(fieldsFor('EveryNWeeks').has('daysOfWeek')).toBe(true);
    expect(fieldsFor('DaysOfWeek').has('intervalValue')).toBe(false);
    expect(fieldsFor('DaysOfMonth').has('daysOfMonth')).toBe(true);
    expect([...fieldsFor('Once')].sort()).toEqual(['missedRunPolicy', 'runOnce', 'timeZone']);
    expect(fieldsFor('Cron').has('cronExpression')).toBe(true);
    expect(fieldsFor('Manual').size).toBe(0);
  });
});

describe('time zone conversion', () => {
  it('converts wall-clock times in a zone to UTC across daylight saving', () => {
    expect(zonedLocalToUtc('2026-01-15T07:00', 'America/Los_Angeles')).toBe('2026-01-15T15:00:00.000Z');
    expect(zonedLocalToUtc('2026-07-01T07:00', 'America/Los_Angeles')).toBe('2026-07-01T14:00:00.000Z');
    expect(zonedLocalToUtc('2026-07-01T07:00', 'UTC')).toBe('2026-07-01T07:00:00.000Z');
    expect(zonedLocalToUtc('', 'UTC')).toBeNull();
  });

  it('round-trips UTC to a datetime-local value in the zone', () => {
    expect(utcToZonedLocal('2026-07-01T14:00:00Z', 'America/Los_Angeles')).toBe('2026-07-01T07:00');
    expect(utcToZonedLocal('2026-01-15T15:00:00Z', 'Asia/Tokyo')).toBe('2026-01-16T00:00');
    expect(utcToZonedLocal(null, 'UTC')).toBe('');
  });
});

describe('schedule request body', () => {
  it('sends only the fields of the chosen type', () => {
    const form = { ...defaultScheduleForm('America/Los_Angeles'), timesOfDay: ['18:00', '07:00', '07:00'], startLocal: '2026-12-01T00:00' };
    const body = scheduleBody(form);
    expect(body).toMatchObject({ intervalType: 'DaysOfWeek', daysOfWeekMask: 62, timesOfDay: ['07:00', '18:00'], timeZone: 'America/Los_Angeles', startUtc: '2026-12-01T08:00:00.000Z' });
    expect(body.intervalValue).toBeUndefined();
    expect(body.daysOfMonth).toBeUndefined();
    const hours = scheduleBody({ ...form, intervalType: 'EveryNHours', intervalValue: 4, minuteOffset: 75 });
    expect(hours).toMatchObject({ intervalValue: 4, minuteOffset: 59 });
    expect(hours.timesOfDay).toBeUndefined();
    const monthly = scheduleBody({ ...form, intervalType: 'DaysOfMonth', daysOfMonth: [-1, 15, 1] });
    expect(monthly.daysOfMonth).toEqual([1, 15, -1]);
    const once = scheduleBody({ ...form, intervalType: 'Once', runOnceLocal: '2026-12-01T10:00' });
    expect(once.runOnceUtc).toBe('2026-12-01T18:00:00.000Z');
    expect(once.startUtc).toBeUndefined();
    expect(scheduleBody({ ...form, intervalType: 'Manual' })).toEqual({ name: '', intervalType: 'Manual', timeZone: 'America/Los_Angeles', enabled: true, missedRunPolicy: 'Skip' });
  });

  it('builds a form from a saved schedule in its own time zone', () => {
    const form = formFromSchedule({ intervalType: 'Once', runOnceUtc: '2026-12-01T18:00:00Z', timeZone: 'America/Los_Angeles', enabled: false, missedRunPolicy: 'RunOnceOnRecovery' });
    expect(form).toMatchObject({ intervalType: 'Once', runOnceLocal: '2026-12-01T10:00', enabled: false, missedRunPolicy: 'RunOnceOnRecovery' });
  });

  it('describes schedules in words', () => {
    const t = i18next.t.bind(i18next);
    const f = { locale: 'en', list: (items) => new Intl.ListFormat('en').format(items), dateTime: () => 'Dec 1' };
    expect(describeSchedule({ intervalType: 'DaysOfWeek', daysOfWeekMask: 62, timesOfDay: ['07:00'] }, t, f)).toBe('Weekdays at 07:00');
    expect(describeSchedule({ intervalType: 'EveryNHours', intervalValue: 4, minuteOffset: 5 }, t, f)).toBe('Every 4 hours at :05');
    expect(describeSchedule({ intervalType: 'EveryNMinutes', intervalValue: 1 }, t, f)).toBe('Every minute');
    expect(describeSchedule({ intervalType: 'Manual' }, t, f)).toBe('Only when you run it');
  });
});

const PREVIEW = {
  valid: true,
  errors: [],
  nextRunsUtc: ['2026-11-20T15:00:00Z', '2026-11-23T15:00:00Z'],
  nextRunsLocal: ['2026-11-20T07:00:00-08:00', '2026-11-23T07:00:00-08:00']
};

function Harness({ initial, onChange }) {
  const [form, setForm] = useState(initial);
  return (
    <ScheduleEditor
      value={form}
      onChange={(next) => {
        setForm(next);
        onChange(next);
      }}
    />
  );
}

Harness.propTypes = { initial: PropTypes.object.isRequired, onChange: PropTypes.func.isRequired };

function renderEditor(client, initial = defaultScheduleForm('America/Los_Angeles')) {
  window.localStorage.setItem(STORAGE_KEYS.token, 'tok_1');
  const onChange = vi.fn();
  render(
    <AuthProvider clientFactory={() => client}>
      <AppProvider>
        <Harness initial={initial} onChange={onChange} />
      </AppProvider>
    </AuthProvider>
  );
  return onChange;
}

describe('ScheduleEditor', () => {
  it('previews the next runs from the server in the schedule time zone', async () => {
    const client = fakeClient({ previewSchedule: vi.fn(async () => PREVIEW) });
    renderEditor(client);
    const preview = await screen.findByTestId('schedule-preview');
    await waitFor(() => expect(within(preview).getAllByRole('listitem')).toHaveLength(2));
    expect(client.previewSchedule).toHaveBeenCalledWith(expect.objectContaining({ intervalType: 'DaysOfWeek', daysOfWeekMask: 62, timeZone: 'America/Los_Angeles', previewCount: 5 }), expect.anything());
    expect(within(preview).getByText(/Fri, Nov 20, 2026/)).toBeInTheDocument();
    expect(within(preview).getAllByText(/07:00 AM PST/)).toHaveLength(2);
  });

  it('toggles a day chip and switches fields with the interval type', async () => {
    const client = fakeClient({ previewSchedule: vi.fn(async () => PREVIEW) });
    const onChange = renderEditor(client);
    await userEvent.click(screen.getByRole('button', { name: 'Sat' }));
    expect(onChange).toHaveBeenLastCalledWith(expect.objectContaining({ daysOfWeekMask: 126 }));
    expect(screen.getByText('Times of day')).toBeInTheDocument();
    await userEvent.selectOptions(screen.getByLabelText('Repeats'), 'EveryNHours');
    expect(screen.queryByRole('button', { name: 'Sat' })).not.toBeInTheDocument();
    expect(screen.getByLabelText('At minute')).toBeInTheDocument();
    await userEvent.selectOptions(screen.getByLabelText('Repeats'), 'Manual');
    expect(screen.queryByLabelText('Time zone')).not.toBeInTheDocument();
    await waitFor(() => expect(client.previewSchedule).toHaveBeenLastCalledWith(expect.objectContaining({ intervalType: 'Manual' }), expect.anything()));
  });

  it('lists validation errors from the preview', async () => {
    const client = fakeClient({ previewSchedule: vi.fn(async () => ({ valid: false, errors: ['cronExpression is not a valid cron expression.'], nextRunsUtc: [], nextRunsLocal: [] })) });
    renderEditor(client, { ...defaultScheduleForm('UTC'), intervalType: 'Cron', cronExpression: 'nope' });
    expect(await screen.findByText('cronExpression is not a valid cron expression.')).toBeInTheDocument();
  });
});
