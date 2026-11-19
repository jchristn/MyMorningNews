import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import Modal from '../../components/Modal.jsx';

const pad = (n) => String(n).padStart(2, '0');

// A Date as the value of a datetime-local input, in local time.
export function toLocalInput(date) {
  return date.getFullYear() + '-' + pad(date.getMonth() + 1) + '-' + pad(date.getDate()) + 'T' + pad(date.getHours()) + ':' + pad(date.getMinutes());
}

// Eight in the morning, local time, a number of days from now.
export function morningIn(days, now = new Date()) {
  const d = new Date(now.getFullYear(), now.getMonth(), now.getDate() + days, 8, 0, 0, 0);
  return d;
}

const QUICK = [
  { key: 'tomorrow', days: 1 },
  { key: 'nextWeek', days: 7 },
  { key: 'nextMonth', days: 30 }
];

// Pick when a snoozed suggestion comes back. The time must be in the future; the server rejects anything else.
export default function SnoozeModal({ suggestion, onClose, onSnooze }) {
  const { t } = useTranslation();
  const [value, setValue] = useState(() => toLocalInput(morningIn(1)));
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    setValue(toLocalInput(morningIn(1)));
    setError(null);
  }, [suggestion]);

  if (!suggestion) return null;
  const when = value ? new Date(value) : null;
  const invalid = !when || Number.isNaN(when.getTime()) || when.getTime() <= Date.now();

  async function submit(event) {
    event.preventDefault();
    if (invalid) return;
    setBusy(true);
    setError(null);
    try {
      await onSnooze(suggestion, when.toISOString());
      onClose();
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <Modal
      open
      title={t('suggestions.snooze.title')}
      onClose={() => !busy && onClose()}
      size="small"
      footer={
        <>
          <button type="button" className="button secondary" onClick={onClose} disabled={busy}>
            {t('common.cancel')}
          </button>
          <button type="submit" form="snooze-suggestion" className="button primary" disabled={busy || invalid}>
            {busy ? t('common.working') : t('suggestions.actions.snooze')}
          </button>
        </>
      }
    >
      <form id="snooze-suggestion" className="stack" onSubmit={submit}>
        <p>{suggestion.title}</p>
        <div className="chip-list" role="group" aria-label={t('suggestions.snooze.quick')}>
          {QUICK.map((q) => (
            <button type="button" key={q.key} className="chip-toggle" onClick={() => setValue(toLocalInput(morningIn(q.days)))}>
              {t('suggestions.snooze.' + q.key)}
            </button>
          ))}
        </div>
        <FormField id="snooze-until" label={t('suggestions.snooze.until')} error={value && invalid ? t('suggestions.snooze.future') : undefined} help={t('suggestions.snooze.help')}>
          <input id="snooze-until" type="datetime-local" value={value} min={toLocalInput(new Date())} onChange={(e) => setValue(e.target.value)} required />
        </FormField>
        <ErrorState error={error} inline onDismiss={() => setError(null)} />
      </form>
    </Modal>
  );
}

SnoozeModal.propTypes = {
  suggestion: PropTypes.object,
  onClose: PropTypes.func.isRequired,
  onSnooze: PropTypes.func.isRequired
};
