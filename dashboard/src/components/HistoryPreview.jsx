import { useCallback, useEffect, useRef, useState } from 'react';
import PropTypes from 'prop-types';
import { Link } from 'react-router-dom';
import { Eye, RefreshCw } from 'lucide-react';
import { useTranslation } from 'react-i18next';
import useDebounce from '../hooks/useDebounce.js';
import useFormat from '../hooks/useFormat.js';
import { ApiError } from '../utils/api.js';
import { historyPreviewBody, previewBlocker } from '../utils/comparison.js';
import ErrorState from './ErrorState.jsx';
import LoadingState from './LoadingState.jsx';

const REFRESH_DELAY_MS = 400;

// The baseline a topic or edition would compare with if it ran now, for the settings as typed (nothing is saved).
// load(body, signal) posts a HistoryPreviewRequest and resolves to a HistoryPreviewResponse. The first preview is
// on request; after that, edits refresh it. baselineHref(id) links an issue to its reader.
export default function HistoryPreview({ form, load, baselineHref = null, disabled = false, idPrefix = 'history-preview' }) {
  const { t } = useTranslation();
  const f = useFormat();
  const [open, setOpen] = useState(false);
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);
  const [loading, setLoading] = useState(false);
  const abort = useRef(null);
  // The latest load function, so a parent passing a new arrow each render does not re-run the preview.
  const loadRef = useRef(load);
  useEffect(() => {
    loadRef.current = load;
  }, [load]);
  const key = JSON.stringify(historyPreviewBody(form));
  const debouncedKey = useDebounce(key, REFRESH_DELAY_MS);
  const blocker = previewBlocker(form);

  const lastKey = useRef(null);
  const run = useCallback(async (bodyKey) => {
    lastKey.current = bodyKey;
    if (abort.current) abort.current.abort();
    const controller = new AbortController();
    abort.current = controller;
    setLoading(true);
    setError(null);
    try {
      const response = await loadRef.current(JSON.parse(bodyKey), controller.signal);
      if (!controller.signal.aborted) setResult(response && typeof response === 'object' ? response : null);
    } catch (err) {
      if (err && err.name === 'AbortError') return;
      setResult(null);
      setError(err);
    } finally {
      if (abort.current === controller) setLoading(false);
    }
  }, []);

  // Once open, settled edits refresh the preview (the first preview runs from the button with the values as typed).
  useEffect(() => {
    if (open && !blocker && debouncedKey === key && debouncedKey !== lastKey.current) run(debouncedKey);
  }, [open, blocker, debouncedKey, key, run]);

  useEffect(() => () => abort.current && abort.current.abort(), []);

  const baselines = result && Array.isArray(result.baselines) ? result.baselines : [];
  const off = form.historyMode === 'Off';
  const conflict = error instanceof ApiError && error.status === 409;
  const percent = result && result.budgetBytes > 0 ? Math.min(100, Math.round((100 * (result.contextBytes || 0)) / result.budgetBytes)) : 0;

  return (
    <div className="history-preview form-field wide" data-testid={idPrefix}>
      <div className="section-title">
        <span className="field-label" id={idPrefix + '-title'}>
          {t('historyPreview.title')}
        </span>
        <button
          type="button"
          className="button secondary small"
          disabled={disabled || loading || Boolean(blocker)}
          onClick={() => {
            setOpen(true);
            run(key);
          }}
        >
          {open ? <RefreshCw size={14} aria-hidden="true" /> : <Eye size={14} aria-hidden="true" />}{' '}
          {open ? t('historyPreview.refresh') : t('historyPreview.show')}
        </button>
      </div>
      {!open ? <p className="muted small">{t('historyPreview.help')}</p> : null}
      {open && blocker ? <p className="muted small">{t('historyPreview.blocked.' + blocker)}</p> : null}
      <div aria-live="polite" aria-labelledby={idPrefix + '-title'}>
        {open && loading && !result ? <LoadingState /> : null}
        {open && error ? (
          <div className="stack">
            <ErrorState error={error} inline onDismiss={() => setError(null)} />
            {conflict ? <p className="small">{t('historyPreview.conflictHelp')}</p> : null}
          </div>
        ) : null}
        {open && result && !error && !blocker ? (
          <div className="stack history-preview-result">
            {baselines.length > 0 ? (
              <>
                <span className="small">{t('historyPreview.wouldCompare', { count: baselines.length })}</span>
                <ul className="comparison-baselines">
                  {baselines.map((b) => (
                    <li key={b.id}>
                      {b.available !== false && baselineHref ? (
                        <Link to={baselineHref(b.id)} dir="auto">
                          {b.title || t('comparison.unavailableIssue')}
                        </Link>
                      ) : (
                        <span dir="auto">{b.title || t('comparison.unavailableIssue')}</span>
                      )}
                      {b.createdUtc ? <span className="muted small">{f.date(b.createdUtc)}</span> : null}
                    </li>
                  ))}
                </ul>
                <div className="history-budget">
                  <span className="small">
                    {t('historyPreview.size', { used: f.bytes(result.contextBytes || 0), budget: f.bytes(result.budgetBytes || 0) })}
                  </span>
                  <span className="history-budget-bar" role="img" aria-label={t('historyPreview.sizePercent', { percent })}>
                    <span style={{ inlineSize: percent + '%' }} className={result.truncated ? 'over' : undefined} />
                  </span>
                </div>
                {result.truncated ? <p className="small notice notice-warning">{t('historyPreview.truncated')}</p> : null}
              </>
            ) : off ? null : (
              <p className="small notice notice-info">{t('historyPreview.firstRun')}</p>
            )}
            {result.note ? (
              <p className="muted small" dir="auto">
                {result.note}
              </p>
            ) : null}
            {result.cutoffUtc && !off ? (
              <p className="muted small">{t('historyPreview.cutoff', { date: f.dateTime(result.cutoffUtc) })}</p>
            ) : null}
          </div>
        ) : null}
      </div>
    </div>
  );
}

HistoryPreview.propTypes = {
  form: PropTypes.object.isRequired,
  load: PropTypes.func.isRequired,
  baselineHref: PropTypes.func,
  disabled: PropTypes.bool,
  idPrefix: PropTypes.string
};
