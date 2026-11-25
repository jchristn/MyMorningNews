import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { useAuth } from '../../context/AuthContext.jsx';
import ErrorState from '../../components/ErrorState.jsx';
import FormField from '../../components/FormField.jsx';

// Administrative diagnostic uses the same fetch/cache policy as scheduled research.
export default function FetchDiagnostic() {
  const { t } = useTranslation();
  const { apiClient } = useAuth();
  const [url, setUrl] = useState('');
  const [forceRefresh, setForceRefresh] = useState(false);
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState(null);
  const [error, setError] = useState(null);

  async function retrieve(event) {
    event.preventDefault();
    if (busy) return;
    setBusy(true);
    setError(null);
    setResult(null);
    try {
      setResult(await apiClient.fetchTest(url.trim(), forceRefresh));
    } catch (err) {
      setError(err);
    } finally {
      setBusy(false);
    }
  }

  return (
    <details className="panel">
      <summary>{t('searchProviders.fetch.title')}</summary>
      <form className="stack" onSubmit={retrieve}>
        <FormField id="fetch-url" label={t('searchProviders.fetch.url')}>
          <input id="fetch-url" type="url" required value={url} onChange={(event) => setUrl(event.target.value)} />
        </FormField>
        <label className="checkbox-label">
          <input type="checkbox" checked={forceRefresh} onChange={(event) => setForceRefresh(event.target.checked)} />{' '}
          {t('searchProviders.fetch.force')}
        </label>
        <button type="submit" className="button small" disabled={busy || !url.trim()}>
          {busy ? t('common.working') : t('searchProviders.fetch.run')}
        </button>
        <ErrorState error={error} inline onDismiss={() => setError(null)} />
        {result ? (
          <div className="stack" aria-live="polite">
            <p>{result.disposition} · {t('searchProviders.fetch.status')}: {result.cacheStatus} · {t('searchProviders.fetch.updated')}: {result.cacheUpdated ? t('common.yes') : t('common.no')}</p>
            {result.error ? <p role="alert">{result.error}</p> : null}
            {result.text ? <pre className="small">{result.text}</pre> : null}
          </div>
        ) : null}
      </form>
    </details>
  );
}
