import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import useFormat from '../../hooks/useFormat.js';
import { PREDICTION_RESOLUTIONS } from '../../utils/enums.js';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';
import Modal from '../../components/Modal.jsx';
import { brierFor } from './predictionMath.js';

// Resolve a prediction by hand: pick the outcome, add a note, and see the Brier score it will get.
export default function ResolveModal({ prediction, onClose, onResolved }) {
  const { t } = useTranslation();
  const f = useFormat();
  const { apiClient } = useAuth();
  const [status, setStatus] = useState('Accurate');
  const [note, setNote] = useState('');
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState(null);

  useEffect(() => {
    setStatus('Accurate');
    setNote('');
    setError(null);
  }, [prediction]);

  if (!prediction) return null;
  const brier = brierFor(prediction.confidence, status);

  async function submit(event) {
    event.preventDefault();
    setBusy(true);
    setError(null);
    try {
      const updated = await apiClient.resolvePrediction(prediction.id, status, note.trim());
      onResolved(updated);
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
      title={t('predictions.resolve.title')}
      onClose={() => !busy && onClose()}
      size="small"
      footer={
        <>
          <button type="button" className="button secondary" onClick={onClose} disabled={busy}>
            {t('common.cancel')}
          </button>
          <button type="submit" form="resolve-prediction" className="button primary" disabled={busy}>
            {busy ? t('common.working') : t('predictions.resolve.submit')}
          </button>
        </>
      }
    >
      <form id="resolve-prediction" className="stack" onSubmit={submit}>
        <p className="prediction-statement">{prediction.statement}</p>
        <FormField id="resolve-status" label={t('predictions.resolve.outcome')} help={t('predictions.resolve.outcomeHelp')}>
          <select id="resolve-status" value={status} onChange={(e) => setStatus(e.target.value)}>
            {PREDICTION_RESOLUTIONS.map((s) => (
              <option key={s} value={s}>
                {t('enums.predictionStatus.' + s)}
              </option>
            ))}
          </select>
        </FormField>
        <FormField id="resolve-note" label={t('predictions.resolve.note')}>
          <textarea id="resolve-note" rows={3} value={note} onChange={(e) => setNote(e.target.value)} placeholder={t('predictions.resolve.notePlaceholder')} />
        </FormField>
        <p className="muted small" data-testid="resolve-brier">
          {brier === null
            ? t('predictions.resolve.notGraded')
            : t('predictions.resolve.brierPreview', { confidence: f.percent(prediction.confidence, 0), brier: f.number(brier, { minimumFractionDigits: 2, maximumFractionDigits: 4 }) })}
        </p>
        <ErrorState error={error} inline onDismiss={() => setError(null)} />
      </form>
    </Modal>
  );
}

ResolveModal.propTypes = {
  prediction: PropTypes.object,
  onClose: PropTypes.func.isRequired,
  onResolved: PropTypes.func.isRequired
};
