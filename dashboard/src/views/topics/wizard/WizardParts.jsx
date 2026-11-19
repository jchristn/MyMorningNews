import { useEffect, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { Check, CircleStop, LoaderCircle, RefreshCw, Sparkles, TriangleAlert } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import useFormat from '../../../hooks/useFormat.js';
import { describeError } from '../../../utils/errors.js';
import { WIZARD_STEPS, stepAvailable, stepComplete } from './wizardState.js';

// Numbered step rail. Steps the session cannot reach yet are disabled.
export function StepRail({ step, data, onSelect, disabled = false }) {
  const { t } = useTranslation();
  return (
    <nav className="step-rail" aria-label={t('wizard.railLabel')}>
      <ol>
        {WIZARD_STEPS.map((s, i) => {
          const current = s === step;
          const done = stepComplete(s, data);
          const available = stepAvailable(s, data);
          return (
            <li key={s} className={(current ? 'current ' : '') + (done ? 'done ' : '') + (available ? '' : 'locked')}>
              <button type="button" disabled={disabled || !available || current} aria-current={current ? 'step' : undefined} onClick={() => onSelect(s)}>
                <span className="step-index" aria-hidden="true">
                  {done && !current ? <Check size={12} /> : i + 1}
                </span>
                <span className="step-label">{t('wizard.steps.' + s)}</span>
              </button>
            </li>
          );
        })}
      </ol>
    </nav>
  );
}

StepRail.propTypes = { step: PropTypes.string.isRequired, data: PropTypes.object, onSelect: PropTypes.func.isRequired, disabled: PropTypes.bool };

// Shown while a model call runs. Generation can take tens of seconds; Stop waiting aborts the request.
export function GenerationPanel({ kind, onStop }) {
  const { t } = useTranslation();
  const f = useFormat();
  const [seconds, setSeconds] = useState(0);
  useEffect(() => {
    const started = Date.now();
    const timer = window.setInterval(() => setSeconds(Math.floor((Date.now() - started) / 1000)), 1000);
    return () => window.clearInterval(timer);
  }, [kind]);
  return (
    <div className="generation-panel" role="status" aria-live="polite" data-testid="generation-panel">
      <LoaderCircle size={22} className="spin" aria-hidden="true" />
      <div className="generation-text">
        <strong>{t('wizard.generating.' + kind)}</strong>
        <span className="muted small">{t('wizard.generating.patience', { seconds: f.number(seconds) })}</span>
      </div>
      {onStop ? (
        <button type="button" className="button secondary small" onClick={onStop}>
          <CircleStop size={14} aria-hidden="true" /> {t('wizard.stopWaiting')}
        </button>
      ) : null}
    </div>
  );
}

GenerationPanel.propTypes = { kind: PropTypes.string.isRequired, onStop: PropTypes.func };

// Model failure: the error (stored on the session as lastError), Retry, and a way to switch the Refinement model.
export function FailurePanel({ error, message, onRetry, onDismiss }) {
  const { t } = useTranslation();
  const info = error ? describeError(error, t) : null;
  const code = error && error.code ? error.code : null;
  const text = message || (info ? info.message : '');
  return (
    <div className="failure-panel" role="alert" data-testid="failure-panel">
      <TriangleAlert size={20} aria-hidden="true" />
      <div className="failure-text">
        <strong>{code === 'ModelRefused' ? t('wizard.failure.refusedTitle') : t('wizard.failure.title')}</strong>
        <span>{text}</span>
        {info && info.requestId ? (
          <span className="muted small">
            {t('errors.requestId')} <code>{info.requestId}</code>
          </span>
        ) : null}
        <span className="muted small">
          {t('wizard.failure.hint')} <Link to="/dashboard/models?tab=assignments">{t('wizard.failure.changeModel')}</Link>
        </span>
      </div>
      <div className="row-actions">
        {onRetry ? (
          <button type="button" className="button primary small" onClick={onRetry}>
            <RefreshCw size={14} aria-hidden="true" /> {t('common.retry')}
          </button>
        ) : null}
        {onDismiss ? (
          <button type="button" className="button secondary small" onClick={onDismiss}>
            {t('common.dismiss')}
          </button>
        ) : null}
      </div>
    </div>
  );
}

FailurePanel.propTypes = { error: PropTypes.any, message: PropTypes.string, onRetry: PropTypes.func, onDismiss: PropTypes.func };

// Guidance text plus a Regenerate button for a generation step.
export function GuidanceBar({ id, value, onChange, onRegenerate, label, buttonLabel, disabled = false, help }) {
  const { t } = useTranslation();
  return (
    <div className="guidance-bar">
      <label htmlFor={id} className="field-label">
        {label || t('wizard.guidance.label')}
      </label>
      <div className="input-with-button">
        <input id={id} value={value} disabled={disabled} placeholder={t('wizard.guidance.placeholder')} onChange={(e) => onChange(e.target.value)} autoComplete="off" />
        <button type="button" className="button secondary small" disabled={disabled} onClick={onRegenerate}>
          <Sparkles size={14} aria-hidden="true" /> {buttonLabel || t('wizard.regenerate')}
        </button>
      </div>
      {help ? <span className="field-help muted">{help}</span> : null}
    </div>
  );
}

GuidanceBar.propTypes = {
  id: PropTypes.string.isRequired,
  value: PropTypes.string.isRequired,
  onChange: PropTypes.func.isRequired,
  onRegenerate: PropTypes.func.isRequired,
  label: PropTypes.string,
  buttonLabel: PropTypes.string,
  disabled: PropTypes.bool,
  help: PropTypes.string
};
